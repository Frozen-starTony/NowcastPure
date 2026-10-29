using System;
using System.Text.RegularExpressions;

namespace NowcastPure
{
    public static class TextCleaner
    {
        private static readonly Regex BracketGarbageRegex = new Regex(
            @"\s*[\(\[](official\s*(music\s*)?(video|audio|lyric\s*video|track)?|prod\.\s*by\s*[^)\]]+|slowed\s*(\+\s*reverb)?|speed\s*up|remix|hd|4k|hq|audio|video|lyrics|clip)[\)\]]",
            RegexOptions.IgnoreCase);

        public static string CleanTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return string.Empty;

            string cleaned = BracketGarbageRegex.Replace(title, string.Empty).Trim();
            if (string.IsNullOrEmpty(cleaned))
                return title.Trim();

            return cleaned;
        }
    }
}
