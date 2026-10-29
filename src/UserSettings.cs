using System;
using System.IO;
using System.Web.Script.Serialization;

namespace NowcastPure
{
    public class UserSettings
    {
        public bool AfkMode { get; set; }
        public bool IgnoreTelegram { get; set; }
        public bool IgnoreBrowser { get; set; }
        public bool CleanTitles { get; set; }

        public UserSettings()
        {
            AfkMode = true;
            IgnoreTelegram = true;
            IgnoreBrowser = false;
            CleanTitles = true;
        }

        private static string ConfigPath
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            }
        }

        public static UserSettings Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string text = File.ReadAllText(ConfigPath);
                    var js = new JavaScriptSerializer();
                    var loaded = js.Deserialize<UserSettings>(text);
                    if (loaded != null) return loaded;
                }
            }
            catch { }

            var def = new UserSettings();
            def.Save();
            return def;
        }

        public void Save()
        {
            try
            {
                var js = new JavaScriptSerializer();
                File.WriteAllText(ConfigPath, js.Serialize(this));
            }
            catch { }
        }
    }
}
