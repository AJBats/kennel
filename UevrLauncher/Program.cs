using System;
using System.Windows.Forms;
using UevrLauncher.Services;

namespace UevrLauncher
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var bootstrap = ConfigStore.LoadBootstrap();
            if (bootstrap == null)
            {
                using (var dlg = new FirstRunForm())
                {
                    if (dlg.ShowDialog() != DialogResult.OK) return;
                    bootstrap = new InstallBootstrap { DataRoot = dlg.SelectedDataRoot };
                    ConfigStore.SaveBootstrap(bootstrap);
                }
            }
            ConfigStore.InitializeDataRoot(bootstrap.DataRoot);

            Application.Run(new MainForm(bootstrap.DataRoot));
        }
    }
}
