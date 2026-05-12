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

        // Cached Font instances for the chihuahua button so RefreshChihuahuaBanner
        // doesn't allocate a new GDI handle on every refresh (the previous
        // pattern leaked one Font per OnActivated → GDI exhaustion in long
        // sessions). Disposed in OnFormClosed.
        private Font _btnFontRegular;
        private Font _btnFontBold;

        public MainForm(string dataRoot)
        {
            _dataRoot = dataRoot;
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _btnFontRegular = new Font(btnChihuahua.Font, FontStyle.Regular);
            _btnFontBold = new Font(btnChihuahua.Font, FontStyle.Bold);

            // One-time migration from the pre-Option-B single-chihuahua\ layout.
            // After this runs, the data root has chihuahua-release\ and
            // chihuahua-nightly\, and every existing wrapper's .bat is rewritten
            // to point at the right mode-specific chihuahua.exe.
            try
            {
                if (ChihuahuaManager.NeedsMigration(_dataRoot))
                    ChihuahuaManager.Migrate(_dataRoot);
                // Always normalize wrapper paths against the current chihuahua
                // layout. Cheap, idempotent, and covers the case where a fresh
                // chihuahua install left old wrappers pointing at the pre-split
                // single-dir path.
                if (ChihuahuaManager.IsInstalled(_dataRoot))
                    RewriteAllWrappersForCurrentChihuahuaLayout();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Migrating to twin chihuahua layout hit a problem: " + ex.Message +
                    "\n\nThe app will continue but wrappers may be broken until you re-add them.",
                    "Kennel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            _config = ConfigStore.LoadConfig(_dataRoot);
            RefreshAll();
            stateTimer.Start();
        }

        // Rewrite every wrapper's .bat to point at the chihuahua.exe matching
        // its UEVR-build mode. Idempotent — safe to call multiple times.
        private void RewriteAllWrappersForCurrentChihuahuaLayout()
        {
            var wrappersDir = ConfigStore.WrappersDir(_dataRoot);
            foreach (var w in WrapperIo.List(wrappersDir))
            {
                var exe = ChihuahuaManager.ChihuahuaExePath(_dataRoot, w.UevrBuild);
                WrapperIo.Write(w.Basename, w.GameName, w.GameExePath, w.DelaySeconds, exe, w.UevrBuild, wrappersDir);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _btnFontRegular?.Dispose();
            _btnFontBold?.Dispose();
            base.OnFormClosed(e);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            // Cheap revalidate so deleting a .bat by hand or moving a game
            // doesn't leave the list lying.
            if (IsHandleCreated && _config != null) RefreshAll();
        }

        private void stateTimer_Tick(object sender, EventArgs e)
        {
            // Re-check Steam-running state without rescanning the wrapper list.
            UpdateButtonGates();
        }

        // ----- Refresh / Render -----

        private void RefreshAll()
        {
            RefreshChihuahuaBanner();
            RefreshWrapperList();
            UpdateButtonGates();
        }

        private void RefreshChihuahuaBanner()
        {
            bool installed = ChihuahuaManager.IsInstalled(_dataRoot);
            if (installed && !string.IsNullOrEmpty(_config?.Chihuahua?.Tag))
            {
                string when = "";
                if (DateTime.TryParse(_config.Chihuahua.InstalledAt, out var ts))
                    when = " (installed " + ts.ToLocalTime().ToString("yyyy-MM-dd") + ")";
                lblChihuahuaStatus.Text = "chihuahua: " + _config.Chihuahua.Tag + when;
                lblChihuahuaStatus.ForeColor = System.Drawing.SystemColors.ControlText;
                btnChihuahua.Text = "Check for chihuahua update";
                btnChihuahua.BackColor = System.Drawing.SystemColors.Control;
                btnChihuahua.ForeColor = System.Drawing.SystemColors.ControlText;
                btnChihuahua.UseVisualStyleBackColor = true;
                if (_btnFontRegular != null) btnChihuahua.Font = _btnFontRegular;
            }
            else
            {
                lblChihuahuaStatus.Text = "⚠  chihuahua is not installed";
                lblChihuahuaStatus.ForeColor = System.Drawing.Color.FromArgb(193, 39, 45);
                btnChihuahua.Text = "Install chihuahua";
                btnChihuahua.UseVisualStyleBackColor = false;
                btnChihuahua.BackColor = System.Drawing.Color.FromArgb(193, 39, 45);
                btnChihuahua.ForeColor = System.Drawing.Color.White;
                if (_btnFontBold != null) btnChihuahua.Font = _btnFontBold;
            }
        }

        // Toggles visibility of the Steam-running warning and enables/disables
        // any action that would touch localconfig.vdf accordingly. Called from
        // the 3-second poll timer and from any state-changing action.
        private void UpdateButtonGates()
        {
            bool steamRunning = SteamConfig.IsSteamRunning();
            bool chihuahuaOk = ChihuahuaManager.IsInstalled(_dataRoot);

            lblSteamWarning.Visible = steamRunning;
            lblSteamWarning.Text = steamRunning
                ? "⚠  Steam is running — quit Steam to add, edit, or delete wrappers"
                : "";

            bool hasSelection = listWrappers.SelectedItems.Count > 0;

            btnAddGame.Enabled = chihuahuaOk && !steamRunning;
            btnEdit.Enabled = hasSelection && chihuahuaOk && !steamRunning;
            btnDelete.Enabled = hasSelection && !steamRunning;

            // Tooltips explain *why* a button is disabled.
            string blockReason = null;
            if (steamRunning) blockReason = "Steam is running — quit Steam first";
            else if (!chihuahuaOk) blockReason = "chihuahua is not installed";

            toolTip.SetToolTip(btnAddGame, blockReason);
            toolTip.SetToolTip(btnEdit, blockReason);
            toolTip.SetToolTip(btnDelete, steamRunning ? "Steam is running — quit Steam first" : null);
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

            UpdateButtonGates();
        }

        private string ValidateRow(WrapperRow row)
        {
            string suffix = string.Equals(row.Wrapper.UevrBuild, WrapperIo.UevrBuildNightly, StringComparison.OrdinalIgnoreCase)
                ? "  (nightly)"
                : "";
            if (!File.Exists(row.Wrapper.GameExePath)) return "⚠ exe missing" + suffix;
            if (!ChihuahuaManager.IsInstalled(_dataRoot)) return "⚠ no chihuahua" + suffix;
            if (string.IsNullOrEmpty(row.AppId)) return "⚠ no Steam link" + suffix;
            return "✓ valid" + suffix;
        }

        // ----- Chihuahua install / update -----

        private void btnChihuahua_Click(object sender, EventArgs e) => InstallOrUpdateChihuahua();

        private async void InstallOrUpdateChihuahua()
        {
            btnChihuahua.Enabled = false;
            // Outer catch is mandatory for any async void handler — anything
            // that escapes here ends the process (unobserved task exceptions
            // on the WinForms sync context terminate on net48).
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

                if (ChihuahuaManager.IsInstalled(_dataRoot) &&
                    !ChihuahuaManager.IsUpdateAvailable(_config?.Chihuahua?.Tag, latest))
                {
                    // Quietly reflect "up to date" in the banner; no popup.
                    FlashBannerOk("✓  chihuahua " + latest.Tag + " — up to date");
                    return;
                }

                // The button label itself already reads "Install" or "Check for
                // update", so the click was the user's confirmation. Skip the
                // popup confirm; if we're updating, the .bak rollback is the
                // safety net.

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
                                // Marshalling onto a possibly-already-disposed
                                // form from a worker thread is racy. Swallow
                                // the standard two exceptions; everything else
                                // bubbles to the outer catch.
                                try
                                {
                                    if (progress.IsHandleCreated)
                                        progress.BeginInvoke((MethodInvoker)(() => progress.Report(cur, total)));
                                }
                                catch (ObjectDisposedException) { }
                                catch (InvalidOperationException) { }
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
                FlashBannerOk("✓  chihuahua " + latest.Tag + " installed");
            }
            catch (Exception ex)
            {
                // Last-resort catch so a stray throw outside the inner blocks
                // doesn't crash the app.
                MessageBox.Show(this,
                    "Unexpected error: " + ex.Message,
                    "chihuahua", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnChihuahua.Enabled = true;
            }
        }

        // Show a transient confirmation in the chihuahua banner area, then
        // restore the real status after a few seconds. Lighter-touch than a
        // popup for "expected good" outcomes.
        private void FlashBannerOk(string message)
        {
            var savedText = lblChihuahuaStatus.Text;
            var savedColor = lblChihuahuaStatus.ForeColor;
            lblChihuahuaStatus.Text = message;
            lblChihuahuaStatus.ForeColor = System.Drawing.Color.FromArgb(34, 124, 70);

            var t = new System.Windows.Forms.Timer { Interval = 3000 };
            t.Tick += (s, e) =>
            {
                t.Stop();
                t.Dispose();
                if (!IsDisposed) RefreshChihuahuaBanner();
            };
            t.Start();
        }

        // ----- List selection & buttons -----

        private void listWrappers_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtonGates();
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

            // Steam-running guard is handled by btnDelete being disabled when
            // Steam is up; no popup needed here.

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
            // Button gates already prevent reaching here when chihuahua is
            // missing or Steam is running; no popup soup needed.

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
                dlg = new AddGameForm(existingGame, existing.Wrapper.GameExePath, existing.Wrapper.DelaySeconds, existing.Wrapper.UevrBuild);
            }

            using (dlg)
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                CommitWrapper(existing, dlg.SelectedGame, dlg.ResultExePath, dlg.ResultDelaySeconds, dlg.ResultGameName, dlg.ResultUevrBuild);
            }
        }

        private void CommitWrapper(WrapperRow existing, SteamGame game, string exePath, int delay, string gameName, string uevrBuild)
        {
            // Dialog only returns DialogResult.OK when a game is selected and
            // its exe exists; UpdateButtonGates blocks Add/Edit entry when
            // Steam is running. So we can go straight to the write here.
            if (game == null || string.IsNullOrEmpty(game.AppId)) return;

            // Editing keeps the original basename so the .bat/.vbs paths and
            // the Steam launch options remain in sync.
            string basename = existing != null
                ? existing.Wrapper.Basename
                : MakeUniqueBasename(Slug.FromGameName(gameName ?? game.Name));

            try
            {
                var wrappersDir = ConfigStore.WrappersDir(_dataRoot);
                // Each wrapper points at its mode-specific chihuahua so toggling
                // a wrapper between Release and Nightly doesn't force chihuahua
                // to redownload UEVR DLLs — each install stays warm.
                var chihuahuaExe = ChihuahuaManager.ChihuahuaExePath(_dataRoot, uevrBuild);

                WrapperIo.Write(basename, gameName ?? game.Name, exePath, delay, chihuahuaExe, uevrBuild, wrappersDir);

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
