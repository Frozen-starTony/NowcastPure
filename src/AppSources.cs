using System;
using System.Text.RegularExpressions;

namespace NowcastPure
{
    public class SourceInfo
    {
        public string Name { get; set; }
        public string ClientId { get; set; }
        public string SearchPrefix { get; set; }
        public bool IsVideo { get; set; }
        public bool IsBrowser { get; set; }
        public bool IsTelegram { get; set; }

        public SourceInfo(string name, string clientId, string searchPrefix = null, bool isVideo = false, bool isBrowser = false, bool isTelegram = false)
        {
            Name = name;
            ClientId = clientId;
            SearchPrefix = searchPrefix;
            IsVideo = isVideo;
            IsBrowser = isBrowser;
            IsTelegram = isTelegram;
        }
    }

    public static class AppSources
    {
        public const string DefaultClientId = "1562901782579912804";

        private class Rule
        {
            public Regex Pattern;
            public SourceInfo Info;

            public Rule(string regexPattern, string name, string clientId, string searchPrefix = null, bool isVideo = false, bool isBrowser = false, bool isTelegram = false)
            {
                Pattern = new Regex(regexPattern, RegexOptions.IgnoreCase);
                Info = new SourceInfo(name, clientId, searchPrefix, isVideo, isBrowser, isTelegram);
            }
        }

        private static readonly Rule[] Rules = new[]
        {
            new Rule(@"yandex.*music|music.*yandex|яндекс.*музык", "Яндекс Музыка", "1563159088265822392", "https://music.yandex.ru/search?text="),
            new Rule(@"spotify", "Spotify", "1563159215709749389", "https://open.spotify.com/search/"),
            new Rule(@"youtube.?music", "YouTube Music", "1563162689163886642", "https://music.youtube.com/search?q="),
            new Rule(@"soundcloud", "SoundCloud", "1563159356940361779", "https://soundcloud.com/search?q="),
            new Rule(@"youtube", "YouTube", "1563159477673263216", "https://www.youtube.com/results?search_query=", true),
            new Rule(@"twitch", "Twitch", "1563159932218646588", "https://www.twitch.tv/search?term=", true),
            new Rule(@"telegram", "Telegram", "1563159812382920706", null, false, false, true),
            new Rule(@"vk.?music|vk.*музык|vkontakte|\bvk\b|\bвк\b", "VK Музыка", "1563159553619525723", "https://vk.com/audio?q="),
            new Rule(@"applemusic|apple.*music|itunes", "Apple Music", "1563159657676021881", "https://music.apple.com/search?term="),
            new Rule(@"aimp", "AIMP", "1563516800287907893", null),
            new Rule(@"foobar", "foobar2000", DefaultClientId, null),
            new Rule(@"vlc", "VLC Media Player", DefaultClientId, null),
            new Rule(@"zunemusic|wmplayer|media.*player", "Windows Media Player", DefaultClientId, null)
        };

        public static SourceInfo Detect(string appId, string title)
        {
            if (string.IsNullOrEmpty(appId))
                appId = string.Empty;

            foreach (var rule in Rules)
            {
                if (rule.Pattern.IsMatch(appId))
                    return rule.Info;
            }

            // If browser (chrome, msedge, firefox, brave, opera, vivaldi)
            if (Regex.IsMatch(appId, @"chrome|msedge|edge|firefox|opera|brave|vivaldi|browser", RegexOptions.IgnoreCase))
            {
                if (!string.IsNullOrEmpty(title))
                {
                    if (Regex.IsMatch(title, @"youtube.*music", RegexOptions.IgnoreCase))
                        return new SourceInfo("YouTube Music", "1563162689163886642", "https://music.youtube.com/search?q=", false, true);
                    if (Regex.IsMatch(title, @"youtube", RegexOptions.IgnoreCase))
                        return new SourceInfo("YouTube", "1563159477673263216", "https://www.youtube.com/results?search_query=", true, true);
                    if (Regex.IsMatch(title, @"soundcloud", RegexOptions.IgnoreCase))
                        return new SourceInfo("SoundCloud", "1563159356940361779", "https://soundcloud.com/search?q=", false, true);
                    if (Regex.IsMatch(title, @"яндекс.*музык|yandex.*music", RegexOptions.IgnoreCase))
                        return new SourceInfo("Яндекс Музыка", "1563159088265822392", "https://music.yandex.ru/search?text=", false, true);
                    if (Regex.IsMatch(title, @"vk|вконтакте", RegexOptions.IgnoreCase))
                        return new SourceInfo("VK Музыка", "1563159553619525723", "https://vk.com/audio?q=", false, true);
                }
                return new SourceInfo("Браузер", DefaultClientId, null, false, true);
            }

            return new SourceInfo("Медиаплеер", DefaultClientId, null);
        }
    }
}
