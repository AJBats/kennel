using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using UevrLauncher.Models;
using UevrLauncher.Services;

namespace UevrLauncher
{
    public partial class MainForm : Form
    {
        private readonly string _dataRoot;
        private AppConfig _config;
        private readonly List<WrapperRow> _rows = new List<WrapperRow>();

        // Cached release info from the last "check updates" press; null if not
        // fetched this session.
        private ChihuahuaManager.ReleaseInfo _latestRelease;

        public MainForm(string dataRoot)
        {
            _dataRoot = dataRoot;
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _config = ConfigStore.LoadConfig(_dataRoot);
            RefreshAll();

            if (!ChihuahuaManager.IsInstalled(_dataRoot))
            {
                // Prompt to install chihuahua on first run. Non-blocking — user
                // can dismiss and install later via the banner button.
                BeginInvoke((MethodInvoker)(() =>
                {
                    var r = MessageBox.Show(this,
                        "Kennel needs chihuahua (the UEVR injector) to do anything useful. " +
                        "Download the latest release from GitHub now? (~6.5 MB)",
                        "Install chihuahua?",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (r == DialogResult.Yes) InstallOrUpdateChihuahua();
                }));
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            // Cheap revalidate so deleting a .bat by hand or moving a game
            // doesn't leave the list lying.
            if (IsHandleCreated && _config != null) RefreshAll();
        }

        // ----- Refresh / Render -----

        private void RefreshAll()
        {
            RefreshChihuahuaBanner();
            RefreshWrapperList();
        }

        private void RefreshChihuahuaBanner()
        {
            if (ChihuahuaManager.IsInstalled(_dataRoot) && !string.IsNullOrEmpty(_config?.Chihuahua?.Tag))
            {
                string when = "";
                if (DateTime.TryParse(_config.Chihuahua.InstalledAt, out var ts))
                    when = " (installed " + ts.ToLocalTime().ToString("yyyy-MM-dd") + ")";
                lblChihuahuaStatus.Text = "chihuahua: " + _config.Chihuahua.Tag + when;
                btnChihuahua.Text = "Check for chihuahua update";
            }
            else
            {
                lblChihuahuaStatus.Text = "chihuahua: not installed";
                btnChihuahua.Text = "Install chihuahua";
            }
        }

        private void RefreshWrapperList()
        {
            _rows.Clear();
            listWrappers.BeginUpdate();
            listWrappers.Items.Clear();

            var wrappersDir = ConfigStore.WrappersDir(_dataRoot);
            var wrappers = WrapperIo.List(wrappersDir);
            foreach (var w in wrappers)
            {
                var row = new WrapperRow
                {
                    Wrapper = w,
                    AppId = _config.Wrappers.FirstOrDefault(x =>
                        string.Equals(x.Basename, w.Basename, StringComparison.OrdinalIgnoreCase))?.AppId,
                };
                row.Status = ValidateRow(row);
                _rows.Add(row);

                var lv = new ListViewItem(new[]
                {
                    w.GameName ?? w.Basename,
                    w.DelaySeconds + "s",
                    row.Status,
                });
                lv.Tag = row;
                if (row.Status.StartsWith("⚠")) lv.ForeColor = Color.DarkOrange;
                listWrappers.Items.Add(lv);
            }
            listWrappers.EndUpdate();

            btnEdit.Enabled = btnDelete.Enabled = listWrappers.SelectedItems.Count > 0;
        }

        private string ValidateRow(WrapperRow row)
        {
            if (!File.Exists(row.Wrapper.GameExePath)) return "⚠ exe missing";
            if (!ChihuahuaManager.IsInstalled(_dataRoot)) return "⚠ no chihuahua";
            if (string.IsNullOrEmpty(row.AppId)) return "⚠ no Steam link";
            return "✓ valid";
        }

        // ----- Chihuahua install / update -----

        private void btnChihuahua_Click(object sender, EventArgs e) => InstallOrUpdateChihuahua();

        private async void InstallOrUpdateChihuahua()
        {
            btnChihuahua.Enabled = false;
            try
            {
                ChihuahuaManager.ReleaseInfo latest;
                try
                {
                    latest = await Task.Run(() => ChihuahuaManager.GetLatestRelease());
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this,
                        "Couldn't reach GitHub: " + ex.Message,
                        "chihuahua update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _latestRelease = latest;

                if (ChihuahuaManager.IsInstalled(_dataRoot) &&
                    !ChihuahuaManager.IsUpdateAvailable(_config?.Chihuahua?.Tag, latest))
                {
                    MessageBox.Show(this,
                        "Up to date (" + latest.Tag + ").",
                        "chihuahua", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                bool isUpdate = ChihuahuaManager.IsInstalled(_dataRoot);
                string verb = isUpdate ? "Update" : "Install";
                var r = MessageBox.Show(this,
                    $"{verb} chihuahua {latest.Tag}? ({latest.AssetSizeBytes / 1024} KB)" +
                    (isUpdate ? "\n\nThe current version will be kept as a rollback." : ""),
                    verb + " chihuahua", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (r != DialogResult.OK) return;

                // Run the download on a background thread, marshalling progress
                // back to the UI thread.
                using (var progress = new ChihuahuaProgressForm())
                {
                    progress.Show(this);
                    try
                    {
                        await Task.Run(() =>
                        {
                            ChihuahuaManager.Install(_dataRoot, latest, (cur, total) =>
                            {
                                if (progress.IsHandleCreated)
                                    progress.BeginInvoke((MethodInvoker)(() => progress.Report(cur, total)));
                            });
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this,
                            "Install failed: " + ex.Message,
                            "chihuahua", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    finally
                    {
                        if (progress.IsHandleCreated) progress.Close();
                    }
                }

                _config = ConfigStore.LoadConfig(_dataRoot);
                RefreshAll();
                MessageBox.Show(this,
                    "chihuahua " + latest.Tag + " installed.",
                    "chihuahua", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                btnChihuahua.Enabled = true;
            }
        }

        // ----- List selection & buttons (stubs filled in next pass) -----

        private void listWrappers_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnEdit.Enabled = btnDelete.Enabled = listWrappers.SelectedItems.Count > 0;
        }

        private void btnAddGame_Click(object sender, EventArgs e)
        {
            ShowAddOrEdit(existing: null);
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            if (listWrappers.SelectedItems.Count == 0) return;
            var row = (WrapperRow)listWrappers.SelectedItems[0].Tag;
            ShowAddOrEdit(row);
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (listWrappers.SelectedItems.Count == 0) return;
            var row = (WrapperRow)listWrappers.SelectedItems[0].Tag;

            var r = MessageBox.Show(this,
                $"Delete the wrapper for \"{row.Wrapper.GameName ?? row.Wrapper.Basename}\"?\n\n" +
                "This will:\n" +
                "  • delete the wrapper scripts\n" +
                "  • clear the Steam launch options for this game\n\n" +
                "The game itself is not touched.",
                "Delete wrapper", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (r != DialogResult.OK) return;

            if (!string.IsNullOrEmpty(row.AppId) && SteamConfig.IsSteamRunning())
            {
                MessageBox.Show(this,
                    "Please quit Steam first. Kennel can't safely clear launch options while Steam is running.",
                    "Steam is running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(row.AppId))
                {
                    var user = SteamConfig.FindActiveUser();
                    if (user != null) SteamConfig.ClearLaunchOptions(user, row.AppId);
                }
                WrapperIo.Delete(row.Wrapper.Basename, ConfigStore.WrappersDir(_dataRoot));
                _config.Wrappers.RemoveAll(x => string.Equals(x.Basename, row.Wrapper.Basename, StringComparison.OrdinalIgnoreCase));
                ConfigStore.SaveConfig(_dataRoot, _config);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Delete failed: " + ex.Message, "Delete wrapper", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshAll();
        }

        private void ShowAddOrEdit(WrapperRow existing)
        {
            if (!ChihuahuaManager.IsInstalled(_dataRoot))
            {
                var r = MessageBox.Show(this,
                    "chihuahua isn't installed yet. Install it now?",
                    "Need chihuahua", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) return;
                InstallOrUpdateChihuahua();
                if (!ChihuahuaManager.IsInstalled(_dataRoot)) return;
            }

            AddGameForm dlg;
            if (existing == null)
            {
                dlg = new AddGameForm();
            }
            else
            {
                // Re-resolve the SteamGame for the existing wrapper so the
                // dialog has a name to lock in. If Steam no longer knows the
                // appid we still let the user re-pick the exe/delay.
                SteamGame existingGame = null;
                if (!string.IsNullOrEmpty(existing.AppId))
                {
                    existingGame = SteamLibrary.GetInstalledGames()
                        .FirstOrDefault(g => g.AppId == existing.AppId);
                }
                if (existingGame == null)
                {
                    existingGame = new SteamGame
                    {
                        AppId = existing.AppId,
                        Name = existing.Wrapper.GameName,
                        InstallPath = "",
                    };
                }
                dlg = new AddGameForm(existingGame, existing.Wrapper.GameExePath, existing.Wrapper.DelaySeconds);
            }

            using (dlg)
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                CommitWrapper(existing, dlg.SelectedGame, dlg.ResultExePath, dlg.ResultDelaySeconds, dlg.ResultGameName);
            }
        }

        private void CommitWrapper(WrapperRow existing, SteamGame game, string exePath, int delay, string gameName)
        {
            if (game == null || string.IsNullOrEmpty(game.AppId))
            {
                MessageBox.Show(this, "No Steam game selected.", "Add wrapper", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Editing keeps the original basename so the .bat/.vbs paths and
            // the Steam launch options remain in sync.
            string basename = existing != null
                ? existing.Wrapper.Basename
                : MakeUniqueBasename(Slug.FromGameName(gameName ?? game.Name));

            if (SteamConfig.IsSteamRunning())
            {
                MessageBox.Show(this,
                    "Please quit Steam first. Kennel can't safely write launch options while Steam is running.",
                    "Steam is running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var wrappersDir = ConfigStore.WrappersDir(_dataRoot);
                var chihuahuaExe = ChihuahuaManager.ChihuahuaExePath(_dataRoot);

                WrapperIo.Write(basename, gameName ?? game.Name, exePath, delay, chihuahuaExe, wrappersDir);

                var launchOpts = WrapperIo.BuildSteamLaunchOptions(basename, wrappersDir);
                var user = SteamConfig.FindActiveUser();
                if (user == null) throw new InvalidOperationException("No Steam user found.");
                SteamConfig.SetLaunchOptions(user, game.AppId, launchOpts);

                // Update or insert the basename↔appid registry entry.
                var reg = _config.Wrappers.FirstOrDefault(w =>
                    string.Equals(w.Basename, basename, StringComparison.OrdinalIgnoreCase));
                if (reg == null)
                    _config.Wrappers.Add(new WrapperRegistry { Basename = basename, AppId = game.AppId });
                else
                    reg.AppId = game.AppId;
                ConfigStore.SaveConfig(_dataRoot, _config);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Save failed: " + ex.Message, "Save wrapper", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            RefreshAll();
        }

        // If the slug already exists in this wrappers dir, suffix -2, -3, …
        private string MakeUniqueBasename(string slug)
        {
            var dir = ConfigStore.WrappersDir(_dataRoot);
            string candidate = slug;
            int n = 2;
            while (File.Exists(WrapperIo.BatPathFor(candidate, dir)))
            {
                candidate = slug + "-" + n++;
            }
            return candidate;
        }

        private sealed class WrapperRow
        {
            public WrapperInfo Wrapper;
            public string AppId;
            public string Status;
        }
    }
}
