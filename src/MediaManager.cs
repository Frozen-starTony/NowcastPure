using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace NowcastPure
{
    public class TrackSnapshot
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string AppId { get; set; }
        public bool IsPlaying { get; set; }
        public double PositionSec { get; set; }
        public double DurationSec { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as TrackSnapshot;
            if (other == null) return false;
            return string.Equals(Title, other.Title, StringComparison.Ordinal) &&
                   string.Equals(Artist, other.Artist, StringComparison.Ordinal) &&
                   string.Equals(Album, other.Album, StringComparison.Ordinal) &&
                   string.Equals(AppId, other.AppId, StringComparison.Ordinal) &&
                   IsPlaying == other.IsPlaying;
        }

        public override int GetHashCode()
        {
            return (Title ?? string.Empty).GetHashCode() ^ (Artist ?? string.Empty).GetHashCode() ^ IsPlaying.GetHashCode();
        }
    }

    public class MediaManager
    {
        private Type managerType;
        private MethodInfo requestAsyncMethod;
        private Type mediaPropsType;
        private MethodInfo asTaskMethod;

        public bool IsAvailable { get; private set; }

        public MediaManager()
        {
            try
            {
                managerType = Type.GetType("Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media, ContentType=WindowsRuntime", false);
                if (managerType != null)
                {
                    requestAsyncMethod = managerType.GetMethod("RequestAsync", Type.EmptyTypes);
                }

                mediaPropsType = Type.GetType("Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties, Windows.Media, ContentType=WindowsRuntime", false);

                asTaskMethod = typeof(WindowsRuntimeSystemExtensions).GetMethods()
                    .FirstOrDefault(m => m.Name == "AsTask" && m.GetParameters().Length == 1 &&
                                         m.GetParameters()[0].ParameterType.Name == "IAsyncOperation`1");

                IsAvailable = (managerType != null && requestAsyncMethod != null && asTaskMethod != null);
            }
            catch
            {
                IsAvailable = false;
            }
        }

        private object Await(object asyncOp, Type resultType, int timeoutMs = 2000)
        {
            if (asyncOp == null || asTaskMethod == null) return null;
            var prev = System.Threading.SynchronizationContext.Current;
            try
            {
                System.Threading.SynchronizationContext.SetSynchronizationContext(null);
                var genericAsTask = asTaskMethod.MakeGenericMethod(resultType);
                var task = (Task)genericAsTask.Invoke(null, new[] { asyncOp });
                if (!task.Wait(timeoutMs))
                {
                    try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "run.log"), "Await TIMED OUT (" + timeoutMs + "ms), status=" + task.Status + "\r\n"); } catch { }
                    return null;
                }
                return task.GetType().GetProperty("Result").GetValue(task, null);
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "run.log"), "Await EXCEPTION: " + ex + "\r\n"); } catch { }
                return null;
            }
            finally
            {
                System.Threading.SynchronizationContext.SetSynchronizationContext(prev);
            }
        }

        private object managerInstance;
        private Task<object> managerInitTask;
        private DateTime lastInitAttempt = DateTime.MinValue;

        private object GetManager()
        {
            if (managerInstance != null) return managerInstance;

            if (managerInitTask != null)
            {
                if (managerInitTask.IsCompleted)
                {
                    if (managerInitTask.Status == TaskStatus.RanToCompletion && managerInitTask.Result != null)
                    {
                        managerInstance = managerInitTask.Result;
                        try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "run.log"), DateTime.Now.ToString("HH:mm:ss") + " MediaManager: manager initialized successfully!\r\n"); } catch { }
                    }
                    else
                    {
                        managerInitTask = null; // will retry after cooldown
                    }
                }
                return managerInstance;
            }

            // Only attempt initialization once every 15 seconds to avoid blocking Windows RPC
            if ((DateTime.UtcNow - lastInitAttempt).TotalSeconds < 15)
            {
                return null;
            }
            lastInitAttempt = DateTime.UtcNow;

            managerInitTask = Task.Run(() =>
            {
                try
                {
                    if (requestAsyncMethod == null) return null;
                    var asyncOp = requestAsyncMethod.Invoke(null, null);
                    return Await(asyncOp, managerType, 8000);
                }
                catch (Exception ex)
                {
                    try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "run.log"), DateTime.Now.ToString("HH:mm:ss") + " RequestAsync error: " + ex.Message + "\r\n"); } catch { }
                    return null;
                }
            });

            return null;
        }

        private TrackSnapshot ExtractSnapshot(object session, bool? knownPlaying = null)
        {
            if (session == null) return null;
            try
            {
                var sessionType = session.GetType();

                // 1. App ID
                string appId = null;
                var appIdProp = sessionType.GetProperty("SourceAppUserModelId");
                if (appIdProp != null)
                {
                    appId = appIdProp.GetValue(session, null) as string;
                }

                // 2. Playback status
                bool isPlaying = knownPlaying ?? false;
                if (!knownPlaying.HasValue)
                {
                    var getPlaybackInfoMethod = sessionType.GetMethod("GetPlaybackInfo");
                    if (getPlaybackInfoMethod != null)
                    {
                        var playbackInfo = getPlaybackInfoMethod.Invoke(session, null);
                        if (playbackInfo != null)
                        {
                            var statusProp = playbackInfo.GetType().GetProperty("PlaybackStatus");
                            if (statusProp != null)
                            {
                                object statusVal = statusProp.GetValue(playbackInfo, null);
                                if (statusVal != null)
                                {
                                    string sStr = statusVal.ToString();
                                    if (sStr == "Playing" || sStr == "4")
                                    {
                                        isPlaying = true;
                                    }
                                }
                            }
                        }
                    }
                }

                // 3. Media properties (Title, Artist, Album)
                string title = null, artist = null, album = null;
                var getPropsAsyncMethod = sessionType.GetMethod("TryGetMediaPropertiesAsync");
                if (getPropsAsyncMethod != null && mediaPropsType != null)
                {
                    var mediaProps = Await(getPropsAsyncMethod.Invoke(session, null), mediaPropsType, 2500);
                    if (mediaProps != null)
                    {
                        var titleProp = mediaPropsType.GetProperty("Title");
                        if (titleProp != null) title = titleProp.GetValue(mediaProps, null) as string;

                        var artistProp = mediaPropsType.GetProperty("Artist");
                        if (artistProp != null) artist = artistProp.GetValue(mediaProps, null) as string;

                        var albumProp = mediaPropsType.GetProperty("AlbumTitle");
                        if (albumProp != null) album = albumProp.GetValue(mediaProps, null) as string;
                    }
                }

                if (string.IsNullOrEmpty(title))
                    return null;

                // 4. Timeline (Position, Duration)
                double positionSec = 0, durationSec = 0;
                var getTimelineMethod = sessionType.GetMethod("GetTimelineProperties");
                if (getTimelineMethod != null)
                {
                    var timeline = getTimelineMethod.Invoke(session, null);
                    if (timeline != null)
                    {
                        var tType = timeline.GetType();
                        var posProp = tType.GetProperty("Position");
                        var endProp = tType.GetProperty("EndTime");
                        if (posProp != null)
                        {
                            var pos = (TimeSpan)posProp.GetValue(timeline, null);
                            positionSec = pos.TotalSeconds;
                        }
                        if (endProp != null)
                        {
                            var end = (TimeSpan)endProp.GetValue(timeline, null);
                            durationSec = end.TotalSeconds;
                        }
                    }
                }

                return new TrackSnapshot
                {
                    Title = title,
                    Artist = artist,
                    Album = album,
                    AppId = appId,
                    IsPlaying = isPlaying,
                    PositionSec = positionSec,
                    DurationSec = durationSec
                };
            }
            catch
            {
                return null;
            }
        }

        public TrackSnapshot GetCurrentTrack()
        {
            if (!IsAvailable) return null;

            try
            {
                var manager = GetManager();
                try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "run.log"), DateTime.Now.ToString("HH:mm:ss") + " MediaManager: manager=" + (manager == null ? "NULL" : "OK") + "\r\n"); } catch { }
                if (manager == null)
                {
                    return GetTrackFromWindows();
                }

                var getSessionsMethod = managerType.GetMethod("GetSessions");
                var sessionsList = new List<TrackSnapshot>();

                if (getSessionsMethod != null)
                {
                    var sessionsEnum = getSessionsMethod.Invoke(manager, null) as System.Collections.IEnumerable;
                    if (sessionsEnum != null)
                    {
                        foreach (var s in sessionsEnum)
                        {
                            var snap = ExtractSnapshot(s);
                            if (snap != null && !string.IsNullOrEmpty(snap.Title))
                            {
                                sessionsList.Add(snap);
                            }
                        }
                    }
                }

                try { 
                    string sInfo = "Count=" + sessionsList.Count + " -> " + string.Join("; ", sessionsList.Select(s => s.AppId + ":" + s.Title + ":" + s.IsPlaying));
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "run.log"), DateTime.Now.ToString("HH:mm:ss") + " Sessions: " + sInfo + "\r\n"); 
                } catch { }

                // If any session is actively playing, pick from playing ones
                var playingSessions = sessionsList.Where(t => t.IsPlaying).ToList();
                if (playingSessions.Count > 0)
                {
                    var timed = playingSessions.Where(t => t.DurationSec > 0).ToList();
                    if (timed.Count > 0) return timed[0];
                    return playingSessions[0];
                }

                // If nothing playing in SMTC, check desktop windows first before assuming paused
                var windowFallback = GetTrackFromWindows();
                if (windowFallback != null && windowFallback.IsPlaying)
                {
                    return windowFallback;
                }

                // If nothing playing, check GetCurrentSession for paused track
                var getSessionMethod = managerType.GetMethod("GetCurrentSession");
                if (getSessionMethod != null)
                {
                    var curSession = getSessionMethod.Invoke(manager, null);
                    if (curSession != null)
                    {
                        var snap = ExtractSnapshot(curSession);
                        if (snap != null && !string.IsNullOrEmpty(snap.Title))
                            return snap;
                    }
                }

                if (sessionsList.Count > 0)
                {
                    return sessionsList[0];
                }

                return windowFallback;
            }
            catch
            {
                managerInstance = null;
                return GetTrackFromWindows();
            }
        }

        public static TrackSnapshot GetTrackFromWindows()
        {
            try
            {
                var titles = WindowTitles.Read();
                foreach (var rawTitle in titles)
                {
                    if (string.IsNullOrWhiteSpace(rawTitle)) continue;

                    // 1. YouTube in Chrome / Edge / Firefox / Opera
                    int ytIdx = rawTitle.IndexOf(" - YouTube", StringComparison.OrdinalIgnoreCase);
                    if (ytIdx > 0)
                    {
                        string mediaPart = rawTitle.Substring(0, ytIdx).Trim();
                        string artist = null;
                        string title = mediaPart;
                        int dash = mediaPart.IndexOf(" - ");
                        if (dash > 0)
                        {
                            artist = mediaPart.Substring(0, dash).Trim();
                            title = mediaPart.Substring(dash + 3).Trim();
                        }
                        return new TrackSnapshot
                        {
                            AppId = "chrome.exe",
                            Artist = artist,
                            Title = title,
                            IsPlaying = true
                        };
                    }

                    // 2. Yandex Music
                    if (rawTitle.IndexOf("Яндекс Музыка", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string clean = rawTitle;
                        clean = clean.Replace("— Яндекс Музыка", "").Replace("- Яндекс Музыка", "").Trim();
                        if (clean.StartsWith("Яндекс Музыка", StringComparison.OrdinalIgnoreCase))
                        {
                            clean = clean.Substring("Яндекс Музыка".Length).Trim(new[] { ' ', '—', '-' });
                        }

                        string artist = "Яндекс Музыка";
                        string title = "Моя волна";

                        if (!string.IsNullOrEmpty(clean) && !clean.StartsWith("собираем музыку", StringComparison.OrdinalIgnoreCase))
                        {
                            int dash = clean.IndexOf(" — ");
                            if (dash < 0) dash = clean.IndexOf(" - ");
                            if (dash > 0)
                            {
                                artist = clean.Substring(0, dash).Trim();
                                title = clean.Substring(dash + 3).Trim();
                            }
                            else
                            {
                                title = clean;
                            }
                        }

                        return new TrackSnapshot
                        {
                            AppId = "ru.yandex.desktop.music",
                            Artist = artist,
                            Title = title,
                            IsPlaying = true
                        };
                    }

                    // 3. Spotify
                    if (rawTitle.EndsWith(" - Spotify", StringComparison.OrdinalIgnoreCase))
                    {
                        string part = rawTitle.Substring(0, rawTitle.Length - 10).Trim();
                        string artist = null;
                        string title = part;
                        int dash = part.IndexOf(" - ");
                        if (dash > 0)
                        {
                            artist = part.Substring(0, dash).Trim();
                            title = part.Substring(dash + 3).Trim();
                        }
                        return new TrackSnapshot
                        {
                            AppId = "spotify.exe",
                            Artist = artist,
                            Title = title,
                            IsPlaying = true
                        };
                    }

                    // 4. SoundCloud
                    if (rawTitle.IndexOf("SoundCloud", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string part = rawTitle.Replace("SoundCloud", "").Trim(new[] { ' ', '|', '-', '—' });
                        int dash = part.IndexOf(" - ");
                        string artist = null;
                        string title = part;
                        if (dash > 0)
                        {
                            artist = part.Substring(0, dash).Trim();
                            title = part.Substring(dash + 3).Trim();
                        }
                        return new TrackSnapshot
                        {
                            AppId = "soundcloud",
                            Artist = artist,
                            Title = title,
                            IsPlaying = true
                        };
                    }

                    // 5. VK Music
                    if (rawTitle.IndexOf("ВКонтакте", StringComparison.OrdinalIgnoreCase) >= 0 && rawTitle.IndexOf(" - ") > 0)
                    {
                        string part = rawTitle.Substring(0, rawTitle.IndexOf("ВКонтакте")).Trim(new[] { ' ', '|', '-', '—' });
                        int dash = part.IndexOf(" - ");
                        if (dash > 0)
                        {
                            return new TrackSnapshot
                            {
                                AppId = "vk.com",
                                Artist = part.Substring(0, dash).Trim(),
                                Title = part.Substring(dash + 3).Trim(),
                                IsPlaying = true
                            };
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
