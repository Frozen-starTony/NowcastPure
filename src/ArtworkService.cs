using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace NowcastPure
{
    public static class ArtworkService
    {
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object LockObj = new object();
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public static string GetArtworkUrl(string artist, string title)
        {
            if (string.IsNullOrEmpty(title))
                return null;

            string query = (string.IsNullOrEmpty(artist) ? title : artist + " " + title).Trim();
            string key = query.ToLowerInvariant();

            lock (LockObj)
            {
                string cachedUrl;
                if (Cache.TryGetValue(key, out cachedUrl))
                    return cachedUrl;
            }

            string foundUrl = FindArtwork(query);

            // If not found and title has brackets/features, clean and retry
            if (string.IsNullOrEmpty(foundUrl))
            {
                string cleanedTitle = Regex.Replace(title, @"\((feat\.|ft\.|official|audio|video|remix|slowed|speed up)[^)]*\)", "", RegexOptions.IgnoreCase);
                cleanedTitle = Regex.Replace(cleanedTitle, @"\[(official|audio|video|remix|hd|4k)[^\]]*\]", "", RegexOptions.IgnoreCase).Trim();
                if (!string.Equals(cleanedTitle, title, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(cleanedTitle))
                {
                    string cleanedQuery = (string.IsNullOrEmpty(artist) ? cleanedTitle : artist + " " + cleanedTitle).Trim();
                    foundUrl = FindArtwork(cleanedQuery);
                }
            }

            lock (LockObj)
            {
                Cache[key] = foundUrl;
            }

            return foundUrl;
        }

        private static string FindArtwork(string query)
        {
            // 1. Try Yandex Music first (best for Russian, indie, and popular CIS tracks)
            string url = SearchYandexMusic(query);
            if (!string.IsNullOrEmpty(url)) return url;

            // 2. Try Deezer (global catalog with direct CDN)
            url = SearchDeezer(query);
            if (!string.IsNullOrEmpty(url)) return url;

            // 3. Try iTunes (Apple CDN)
            url = SearchItunes(query);
            if (!string.IsNullOrEmpty(url)) return url;

            return null;
        }

        private static string SearchYandexMusic(string query)
        {
            try
            {
                string escaped = Uri.EscapeDataString(query);
                string url = "https://api.music.yandex.net/search?text=" + escaped + "&type=track&page=0";

                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "YandexMusic/2026.0 (Android)";
                req.Timeout = 2500;

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string jsonText = reader.ReadToEnd();
                    var dict = Json.Deserialize<Dictionary<string, object>>(jsonText);
                    if (dict != null && dict.ContainsKey("result"))
                    {
                        var resDict = dict["result"] as Dictionary<string, object>;
                        if (resDict != null && resDict.ContainsKey("tracks"))
                        {
                            var trkDict = resDict["tracks"] as Dictionary<string, object>;
                            if (trkDict != null && trkDict.ContainsKey("results"))
                            {
                                var tracks = trkDict["results"] as ArrayList;
                                if (tracks != null && tracks.Count > 0)
                                {
                                    var first = tracks[0] as Dictionary<string, object>;
                                    if (first != null && first.ContainsKey("coverUri"))
                                    {
                                        string cover = first["coverUri"] as string;
                                        if (!string.IsNullOrEmpty(cover))
                                        {
                                            return "https://" + cover.Replace("%%", "400x400");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string SearchDeezer(string query)
        {
            try
            {
                string escaped = Uri.EscapeDataString(query);
                string url = "https://api.deezer.com/search?q=" + escaped + "&limit=1";

                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "Mozilla/5.0";
                req.Timeout = 2500;

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string jsonText = reader.ReadToEnd();
                    var dict = Json.Deserialize<Dictionary<string, object>>(jsonText);
                    if (dict != null && dict.ContainsKey("data"))
                    {
                        var list = dict["data"] as ArrayList;
                        if (list != null && list.Count > 0)
                        {
                            var first = list[0] as Dictionary<string, object>;
                            if (first != null && first.ContainsKey("album"))
                            {
                                var album = first["album"] as Dictionary<string, object>;
                                if (album != null && album.ContainsKey("cover_big"))
                                {
                                    string cover = album["cover_big"] as string;
                                    if (!string.IsNullOrEmpty(cover)) return cover;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string SearchItunes(string query)
        {
            try
            {
                string escaped = Uri.EscapeDataString(query);
                string url = "https://itunes.apple.com/search?entity=song&limit=1&term=" + escaped;

                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "Mozilla/5.0";
                req.Timeout = 2500;

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string jsonText = reader.ReadToEnd();
                    var dict = Json.Deserialize<Dictionary<string, object>>(jsonText);
                    if (dict != null && dict.ContainsKey("results"))
                    {
                        var results = dict["results"] as ArrayList;
                        if (results != null && results.Count > 0)
                        {
                            var first = results[0] as Dictionary<string, object>;
                            if (first != null && first.ContainsKey("artworkUrl100"))
                            {
                                string artUrl = first["artworkUrl100"] as string;
                                if (!string.IsNullOrEmpty(artUrl))
                                {
                                    return artUrl.Replace("100x100bb.jpg", "512x512bb.jpg");
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
