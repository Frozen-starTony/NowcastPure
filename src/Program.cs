using System;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace NowcastPure
{
    static class Program
    {
        private static Mutex appMutex;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            bool isFirstInstance;
            appMutex = new Mutex(true, "Local\\NowcastPureInstance", out isFirstInstance);

            if (!isFirstInstance)
            {
                MessageBox.Show("Nowcast Pure уже запущен в системном трее!", "Nowcast Pure", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Application.Run(new TrayApplication());
            }
            finally
            {
                if (appMutex != null)
                {
                    try { appMutex.ReleaseMutex(); } catch { }
                    appMutex.Dispose();
                }
            }
        }
    }
}
