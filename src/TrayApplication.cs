using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NowcastPure
{
    public class TrayApplication : ApplicationContext
    {
        private readonly NotifyIcon notifyIcon;
        private readonly Timer timer;
        private readonly MediaManager mediaManager;
        private readonly DiscordRpc discordRpc;
        private readonly UserSettings settings;

        private MenuItem trackMenuItem;
        private MenuItem discordMenuItem;
        private MenuItem afkMenuItem;
        private MenuItem cleanTitlesMenuItem;
        private MenuItem ignoreTelegramMenuItem;
        private MenuItem ignoreBrowserMenuItem;
        private MenuItem autostartMenuItem;

        private TrackSnapshot lastTrack;
        private string lastArtworkUrl;
        private DateTime lastArtworkCheck = DateTime.MinValue;

        private string activeTrackKey;
        private long? currentTrackStartSec;
        private long? currentTrackEndSec;
        private double lastReportedPosition;
        private string lastSentArtworkUrl;
        private bool lastSentPlaying;
        private DateTime lastSentTime = DateTime.MinValue;

        // AFK tracking
        private bool isAfkSent;
        private long? afkStartSec;
        private string afkLastTrackTitle;
        private string afkLastArtworkUrl;

        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryName = "NowcastPure";

        public TrayApplication()
        {
            settings = UserSettings.Load();
            mediaManager = new MediaManager();
            discordRpc = new DiscordRpc();

            var contextMenu = new ContextMenu();

            var titleItem = new MenuItem("Nowcast Pure");
            titleItem.Enabled = false;
            contextMenu.MenuItems.Add(titleItem);

            contextMenu.MenuItems.Add("-");

            trackMenuItem = new MenuItem("Ожидание трека...");
            trackMenuItem.Enabled = false;
            contextMenu.MenuItems.Add(trackMenuItem);

            discordMenuItem = new MenuItem("Discord: Ожидание...");
            discordMenuItem.Enabled = false;
            contextMenu.MenuItems.Add(discordMenuItem);

            contextMenu.MenuItems.Add("-");

            afkMenuItem = new MenuItem("Режим AFK на паузе", OnToggleAfk);
            afkMenuItem.Checked = settings.AfkMode;
            contextMenu.MenuItems.Add(afkMenuItem);

            cleanTitlesMenuItem = new MenuItem("Очищать названия от мусора", OnToggleCleanTitles);
            cleanTitlesMenuItem.Checked = settings.CleanTitles;
            contextMenu.MenuItems.Add(cleanTitlesMenuItem);

            ignoreTelegramMenuItem = new MenuItem("Игнорировать Telegram", OnToggleIgnoreTelegram);
            ignoreTelegramMenuItem.Checked = settings.IgnoreTelegram;
            contextMenu.MenuItems.Add(ignoreTelegramMenuItem);

            ignoreBrowserMenuItem = new MenuItem("Игнорировать браузеры", OnToggleIgnoreBrowser);
            ignoreBrowserMenuItem.Checked = settings.IgnoreBrowser;
            contextMenu.MenuItems.Add(ignoreBrowserMenuItem);

            contextMenu.MenuItems.Add("-");

            autostartMenuItem = new MenuItem("Автозапуск с Windows", OnToggleAutostart);
            autostartMenuItem.Checked = IsAutostartEnabled();
            contextMenu.MenuItems.Add(autostartMenuItem);

            var aboutItem = new MenuItem("О программе", OnAbout);
            contextMenu.MenuItems.Add(aboutItem);

            contextMenu.MenuItems.Add("-");

            var exitItem = new MenuItem("Выход", OnExit);
            contextMenu.MenuItems.Add(exitItem);

            notifyIcon = new NotifyIcon
            {
                Icon = CreateAppIcon(),
                ContextMenu = contextMenu,
                Text = "Nowcast Pure",
                Visible = true
            };

            timer = new Timer();
            timer.Interval = 1500;
            timer.Tick += OnTick;
            timer.Start();

            OnTick(null, EventArgs.Empty);
        }

        private Icon CreateAppIcon()
        {
            try
            {
                using (var bmp = new Bitmap(32, 32))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    using (var brush = new SolidBrush(Color.FromArgb(88, 101, 242)))
                    {
                        g.FillEllipse(brush, 2, 2, 28, 28);
                    }

                    using (var pen = new Pen(Color.White, 2.5f))
                    using (var brush = new SolidBrush(Color.White))
                    {
                        g.FillEllipse(brush, 7, 18, 7, 6);
                        g.DrawLine(pen, 13, 20, 13, 8);
                        g.DrawLine(pen, 13, 8, 22, 6);
                        g.DrawLine(pen, 22, 6, 22, 16);
                        g.FillEllipse(brush, 16, 15, 7, 6);
                    }

                    IntPtr hIcon = bmp.GetHicon();
                    return Icon.FromHandle(hIcon);
                }
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                var track = mediaManager.GetCurrentTrack();

                // 1. Update Discord connection status in menu
                discordMenuItem.Text = discordRpc.IsConnected ? "Discord: Подключен" : "Discord: Не подключен";

                // 2. Filter out ignored apps
                if (track != null)
                {
                    var sourceCheck = AppSources.Detect(track.AppId, track.Title);
                    if (sourceCheck.IsTelegram && settings.IgnoreTelegram)
                    {
                        track = null;
                    }
                    else if (sourceCheck.IsBrowser && settings.IgnoreBrowser)
                    {
                        track = null;
                    }
                }

                // 3. Clean track title from brackets if enabled
                if (track != null && settings.CleanTitles)
                {
                    track.Title = TextCleaner.CleanTitle(track.Title);
                }

                long nowSec = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

                // 4. Handle Stopped / Paused
                if (track == null || !track.IsPlaying)
                {
                    activeTrackKey = null;
                    currentTrackStartSec = null;
                    currentTrackEndSec = null;
                    lastSentPlaying = false;
                    lastSentArtworkUrl = null;

                    if (settings.AfkMode)
                    {
                        if (!afkStartSec.HasValue)
                        {
                            afkStartSec = nowSec;
                            if (lastTrack != null && !string.IsNullOrEmpty(lastTrack.Title))
                            {
                                afkLastTrackTitle = (string.IsNullOrEmpty(lastTrack.Artist) ? lastTrack.Title : lastTrack.Artist + " - " + lastTrack.Title);
                                afkLastArtworkUrl = lastArtworkUrl;
                            }
                        }

                        if (!isAfkSent || (DateTime.UtcNow - lastSentTime).TotalSeconds > 60)
                        {
                            string stateText = !string.IsNullOrEmpty(afkLastTrackTitle) ? "Ранее: " + afkLastTrackTitle : "Отошел";
                            string afkIcon = !string.IsNullOrEmpty(afkLastArtworkUrl) ? afkLastArtworkUrl : null;
                            discordRpc.SetAfkActivity("AFK", stateText, afkStartSec.Value, afkIcon);
                            isAfkSent = true;
                            lastSentTime = DateTime.UtcNow;
                        }

                        trackMenuItem.Text = "AFK";
                        SafeSetNotifyText("Nowcast: AFK");
                    }
                    else
                    {
                        if (isAfkSent || lastTrack != null)
                        {
                            discordRpc.ClearActivity();
                            isAfkSent = false;
                        }

                        if (track != null && !track.IsPlaying)
                        {
                            trackMenuItem.Text = "Пауза: " + Truncate(track.Title, 25);
                            SafeSetNotifyText("Nowcast: Пауза");
                        }
                        else
                        {
                            trackMenuItem.Text = "Ожидание трека";
                            SafeSetNotifyText("Nowcast: Ожидание");
                        }
                    }

                    if (track != null) lastTrack = track;
                    return;
                }

                // 5. Music is actively PLAYING!
                isAfkSent = false;
                afkStartSec = null;

                string displayTrack = (string.IsNullOrEmpty(track.Artist) ? track.Title : track.Artist + " - " + track.Title);
                trackMenuItem.Text = "Трек: " + Truncate(displayTrack, 30);
                SafeSetNotifyText("Nowcast: " + Truncate(displayTrack, 40));

                // Track identity & changes
                string trackKey = (track.AppId ?? string.Empty) + "|" + (track.Artist ?? string.Empty) + "|" + (track.Title ?? string.Empty) + "|" + (track.Album ?? string.Empty);
                bool trackChanged = !string.Equals(activeTrackKey, trackKey, StringComparison.Ordinal);

                bool seekHappened = false;

                if (trackChanged || !currentTrackStartSec.HasValue)
                {
                    currentTrackStartSec = nowSec - (long)track.PositionSec;
                    if (track.DurationSec > 0)
                        currentTrackEndSec = currentTrackStartSec.Value + (long)track.DurationSec;
                    else
                        currentTrackEndSec = null;

                    lastReportedPosition = track.PositionSec;
                    activeTrackKey = trackKey;
                }
                else
                {
                    // A seek can ONLY occur if the player actively reported a DIFFERENT position from before
                    if (Math.Abs(track.PositionSec - lastReportedPosition) > 0.5)
                    {
                        long expectedPos = nowSec - currentTrackStartSec.Value;
                        if (Math.Abs(expectedPos - (long)track.PositionSec) > 3)
                        {
                            currentTrackStartSec = nowSec - (long)track.PositionSec;
                            if (track.DurationSec > 0)
                                currentTrackEndSec = currentTrackStartSec.Value + (long)track.DurationSec;
                            else
                                currentTrackEndSec = null;

                            seekHappened = true;
                        }
                        lastReportedPosition = track.PositionSec;
                    }
                }

                var source = AppSources.Detect(track.AppId, track.Title);

                // Artwork
                if (source.IsVideo)
                {
                    // Fastly GitHub raw CDN (always 200, never blocked by Discord media proxy)
                    if (source.Name.IndexOf("Twitch", StringComparison.OrdinalIgnoreCase) >= 0)
                        lastArtworkUrl = "https://raw.githubusercontent.com/walkxcode/dashboard-icons/main/png/twitch.png";
                    else
                        lastArtworkUrl = "https://raw.githubusercontent.com/walkxcode/dashboard-icons/main/png/youtube.png";
                }
                else
                {
                    if (trackChanged || (DateTime.UtcNow - lastArtworkCheck).TotalMinutes > 10)
                    {
                        string artArtist = track.Artist;
                        string artTitle = track.Title;
                        string artKey = trackKey;
                        lastArtworkCheck = DateTime.UtcNow;

                        string cached = ArtworkService.GetCachedUrl(artArtist, artTitle);
                        if (!string.IsNullOrEmpty(cached))
                        {
                            lastArtworkUrl = cached;
                        }
                        else
                        {
                            System.Threading.Tasks.Task.Run(() =>
                            {
                                string url = ArtworkService.GetArtworkUrl(artArtist, artTitle);
                                if (!string.IsNullOrEmpty(url) && activeTrackKey == artKey)
                                {
                                    lastArtworkUrl = url;
                                }
                            });
                        }
                    }
                }

                bool artworkChanged = (lastArtworkUrl != lastSentArtworkUrl && !string.IsNullOrEmpty(lastArtworkUrl));
                bool shouldSend = trackChanged || seekHappened || (!lastSentPlaying) || artworkChanged || ((DateTime.UtcNow - lastSentTime).TotalSeconds > 60);

                if (shouldSend)
                {
                    string searchQuery = Uri.EscapeDataString(((track.Artist ?? string.Empty) + " " + (track.Title ?? string.Empty)).Trim());
                    
                    // Button 1: YouTube
                    string btn1Label = source.IsVideo ? "Смотреть" : "Слушать на YouTube";
                    string btn1Url = "https://www.youtube.com/results?search_query=" + searchQuery;

                    // Button 2: Native Player (e.g. Yandex Music, Spotify, VK, Apple Music)
                    string btn2Label = null;
                    string btn2Url = null;
                    if (!string.IsNullOrEmpty(source.SearchPrefix))
                    {
                        btn2Label = source.Name;
                        btn2Url = source.SearchPrefix + searchQuery;
                    }

                    discordRpc.SetActivity(
                        source.ClientId,
                        track.Title,
                        track.Artist,
                        track.Album,
                        source.Name,
                        lastArtworkUrl,
                        btn1Label,
                        btn1Url,
                        btn2Label,
                        btn2Url,
                        currentTrackStartSec,
                        currentTrackEndSec,
                        source.IsVideo
                    );

                    lastSentPlaying = true;
                    lastSentArtworkUrl = lastArtworkUrl;
                    lastSentTime = DateTime.UtcNow;
                }

                lastTrack = track;
            }
            catch
            {
                // Background safety
            }
        }

        private void SafeSetNotifyText(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) text = "Nowcast";
                if (text.Length >= 64) text = text.Substring(0, 60) + "...";
                notifyIcon.Text = text;
            }
            catch { }
        }

        private string Truncate(string str, int maxLen)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            if (str.Length <= maxLen) return str;
            return str.Substring(0, maxLen - 3) + "...";
        }

        private void OnToggleAfk(object sender, EventArgs e)
        {
            settings.AfkMode = !settings.AfkMode;
            afkMenuItem.Checked = settings.AfkMode;
            settings.Save();
            OnTick(null, EventArgs.Empty);
        }

        private void OnToggleCleanTitles(object sender, EventArgs e)
        {
            settings.CleanTitles = !settings.CleanTitles;
            cleanTitlesMenuItem.Checked = settings.CleanTitles;
            settings.Save();
            activeTrackKey = null; // force refresh
            OnTick(null, EventArgs.Empty);
        }

        private void OnToggleIgnoreTelegram(object sender, EventArgs e)
        {
            settings.IgnoreTelegram = !settings.IgnoreTelegram;
            ignoreTelegramMenuItem.Checked = settings.IgnoreTelegram;
            settings.Save();
            activeTrackKey = null;
            OnTick(null, EventArgs.Empty);
        }

        private void OnToggleIgnoreBrowser(object sender, EventArgs e)
        {
            settings.IgnoreBrowser = !settings.IgnoreBrowser;
            ignoreBrowserMenuItem.Checked = settings.IgnoreBrowser;
            settings.Save();
            activeTrackKey = null;
            OnTick(null, EventArgs.Empty);
        }

        private bool IsAutostartEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
                {
                    if (key != null)
                    {
                        var val = key.GetValue(AppRegistryName);
                        return val != null;
                    }
                }
            }
            catch { }
            return false;
        }

        private void OnToggleAutostart(object sender, EventArgs e)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                {
                    if (key != null)
                    {
                        if (autostartMenuItem.Checked)
                        {
                            key.DeleteValue(AppRegistryName, false);
                            autostartMenuItem.Checked = false;
                        }
                        else
                        {
                            string exePath = Application.ExecutablePath;
                            key.SetValue(AppRegistryName, "\"" + exePath + "\"");
                            autostartMenuItem.Checked = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось изменить реестр: " + ex.Message, "Ошибка автозапуска", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnAbout(object sender, EventArgs e)
        {
            string info = "Nowcast Pure Edition v1.3\n\n" +
                          "- Чистая версия без сторонних VPS и серверов.\n" +
                          "- Режим AFK на паузе.\n" +
                          "- Двойные кнопки: YouTube + родной плеер.\n" +
                          "- Поддержка YouTube видео с официальным логотипом.\n" +
                          "- Очистка названий от лишнего мусора.\n" +
                          "- Защита от спама: фильтр Telegram и браузеров.\n\n" +
                          "Все настройки сохраняются в config.json.";
            MessageBox.Show(info, "О программе Nowcast Pure", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnExit(object sender, EventArgs e)
        {
            timer.Stop();
            timer.Dispose();
            discordRpc.Dispose();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            Application.Exit();
        }
    }
}
