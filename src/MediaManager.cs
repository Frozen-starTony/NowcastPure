using System;
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
            try
            {
                var genericAsTask = asTaskMethod.MakeGenericMethod(resultType);
                var task = (Task)genericAsTask.Invoke(null, new[] { asyncOp });
                if (!task.Wait(timeoutMs)) return null;
                return task.GetType().GetProperty("Result").GetValue(task, null);
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
                var managerAsync = requestAsyncMethod.Invoke(null, null);
                var manager = Await(managerAsync, managerType, 1500);
                if (manager == null) return null;

                var getSessionMethod = managerType.GetMethod("GetCurrentSession");
                var session = getSessionMethod.Invoke(manager, null);
                if (session == null) return null;

                var sessionType = session.GetType();

                // 1. App ID
                string appId = null;
                var appIdProp = sessionType.GetProperty("SourceAppUserModelId");
                if (appIdProp != null)
                {
                    appId = appIdProp.GetValue(session, null) as string;
                }

                // 2. Playback status
                bool isPlaying = false;
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
                            if (statusVal != null && statusVal.ToString() == "Playing")
                            {
                                isPlaying = true;
                            }
                        }
                    }
                }

                // 3. Media properties (Title, Artist, Album)
                string title = null, artist = null, album = null;
                var getPropsAsyncMethod = sessionType.GetMethod("TryGetMediaPropertiesAsync");
                if (getPropsAsyncMethod != null && mediaPropsType != null)
                {
                    var mediaProps = Await(getPropsAsyncMethod.Invoke(session, null), mediaPropsType, 1500);
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

                // If no title, nothing useful is playing
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
    }
}
