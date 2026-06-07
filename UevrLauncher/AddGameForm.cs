using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using UevrLauncher.Models;
using UevrLauncher.Services;

namespace UevrLauncher
{
    // Modal dialog for both Add (pick a game from the Steam library) and Edit
    // (game is locked in; user can tweak exe path and delay).
    public partial class AddGameForm : Form
    {
        private readonly bool _editMode;
        private List<SteamGame> _allGames;

        public SteamGame SelectedGame { get; private set; }
        public string ResultExePath { get; private set; }
        public int ResultDelaySeconds { get; private set; }
        public string ResultGameName { get; private set; }
        public string ResultUevrBuild { get; private set; }    // "Release", "Nightly", or "Custom"
        public bool ResultManualInjection { get; private set; }
        public string ResultCustomUevrDir { get; private set; }

        // Add mode.
        public AddGameForm()
        {
            _editMode = false;
            InitializeComponent();
            radioRelease.Checked = true;
            WireBuildToggle();
        }

        // Edit mode: lock in the existing game, pre-fill fields.
        public AddGameForm(SteamGame existingGame, string existingExe, int existingDelay, string existingUevrBuild, bool existingManual, string existingCustomUevrDir)
        {
            _editMode = true;
            InitializeComponent();
            SelectedGame = existingGame;
            txtExePath.Text = existingExe ?? "";
            numDelay.Value = Math.Max(numDelay.Minimum, Math.Min(numDelay.Maximum, existingDelay <= 0 ? 15 : existingDelay));
            if (string.Equals(existingUevrBuild, WrapperIo.UevrBuildCustom, StringComparison.OrdinalIgnoreCase))
                radioCustom.Checked = true;
            else if (string.Equals(existingUevrBuild, WrapperIo.UevrBuildNightly, StringComparison.OrdinalIgnoreCase))
                radioNightly.Checked = true;
            else
                radioRelease.Checked = true;
            chkManual.Checked = existingManual;
            txtCustomPath.Text = existingCustomUevrDir ?? "";
            WireBuildToggle();
        }

        // Build/Manual state interlock.
        //
        // Two invariants:
        //   1. Manual without Custom ⇒ Release (only praydog's tagged release
        //      ships a UEVRInjector frontend).
        //   2. Custom ⇒ Manual (chihuahua can't be told to use a user-supplied
        //      DLL set; we go through the user's own UEVRInjector.exe).
        //
        // The UI reflects this by greying out controls so the user can't
        // configure an impossible combination.
        private void WireBuildToggle()
        {
            chkManual.CheckedChanged += (s, e) => ApplyBuildState();
            radioRelease.CheckedChanged += (s, e) => ApplyBuildState();
            radioNightly.CheckedChanged += (s, e) => ApplyBuildState();
            radioCustom.CheckedChanged += (s, e) => ApplyBuildState();
            txtCustomPath.TextChanged += (s, e) => UpdateOkEnabled();
            ApplyBuildState();
        }

        private void ApplyBuildState()
        {
            bool custom = radioCustom.Checked;
            bool manual = chkManual.Checked;

            // Custom shows the path row; others hide it. Custom is independent
            // of Manual: with Manual=off we'll auto-inject the custom UEVR DLLs
            // via a chihuahua.exe co-located in the custom dir; with Manual=on
            // we'll launch the custom UEVRInjector.exe and the user injects.
            lblCustomPath.Visible = custom;
            txtCustomPath.Visible = custom;
            btnBrowseCustom.Visible = custom;

            // Delay is meaningless in Manual mode (chihuahua isn't called).
            numDelay.Enabled = !manual;

            // Manual-without-Custom forces Release and disables Nightly (no
            // nightly frontend exists). Custom + Manual still uses the custom
            // dir's frontend, so it stays available.
            radioRelease.Enabled = true;
            radioCustom.Enabled = true;
            radioNightly.Enabled = !(manual && !custom);
            if (manual && !custom && !radioRelease.Checked && !radioCustom.Checked)
                radioRelease.Checked = true;

            UpdateOkEnabled();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (_editMode)
            {
                lblSearch.Visible = false;
                txtSearch.Visible = false;
                listGames.Visible = false;
                lblGameLocked.Visible = true;
                lblGameLocked.Text = SelectedGame?.Name ?? "";
                this.Text = "Edit wrapper";
                UpdateOkEnabled();
                return;
            }

            this.Text = "Add game";
            lblGameLocked.Visible = false;

            // Load Steam library in the background-ish but it's fast enough to
            // just do it on the UI thread; even 45 games scans in <50 ms.
            _allGames = SteamLibrary.GetInstalledGames()
                .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            PopulateList("");
        }

        private void PopulateList(string filter)
        {
            listGames.BeginUpdate();
            listGames.Items.Clear();
            foreach (var g in _allGames)
            {
                if (filter.Length == 0 ||
                    g.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var item = new ListViewItem(g.Name) { Tag = g };
                    listGames.Items.Add(item);
                }
            }
            listGames.EndUpdate();
            UpdateOkEnabled();
        }

        private void txtSearch_TextChanged(object sender, EventArgs e) => PopulateList(txtSearch.Text.Trim());

        private void listGames_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listGames.SelectedItems.Count == 0)
            {
                SelectedGame = null;
                UpdateOkEnabled();
                return;
            }
            SelectedGame = (SteamGame)listGames.SelectedItems[0].Tag;

            // Auto-fill exe from heuristic. User can Browse to override.
            var best = ExeFinder.BestGuess(SelectedGame.InstallPath);
            txtExePath.Text = best?.Path ?? "";
            UpdateOkEnabled();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Game executable (*.exe)|*.exe";
                ofd.Title = "Pick the game's main exe";
                if (SelectedGame != null && Directory.Exists(SelectedGame.InstallPath))
                    ofd.InitialDirectory = SelectedGame.InstallPath;
                else if (!string.IsNullOrEmpty(txtExePath.Text) && File.Exists(txtExePath.Text))
                    ofd.InitialDirectory = Path.GetDirectoryName(txtExePath.Text);
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    txtExePath.Text = ofd.FileName;
                    UpdateOkEnabled();
                }
            }
        }

        private void txtExePath_TextChanged(object sender, EventArgs e) => UpdateOkEnabled();

        private void UpdateOkEnabled()
        {
            bool ok = SelectedGame != null
                && !string.IsNullOrWhiteSpace(txtExePath.Text)
                && File.Exists(txtExePath.Text);

            // Custom mode also requires a path to a dir containing UEVRInjector.exe.
            if (ok && radioCustom != null && radioCustom.Checked)
            {
                var dir = txtCustomPath.Text?.Trim();
                ok = !string.IsNullOrEmpty(dir)
                    && Directory.Exists(dir)
                    && File.Exists(Path.Combine(dir, "UEVRInjector.exe"));
            }

            btnOk.Enabled = ok;
        }

        private void btnBrowseCustom_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Pick your custom UEVR install directory (must contain UEVRInjector.exe).";
                if (!string.IsNullOrEmpty(txtCustomPath.Text) && Directory.Exists(txtCustomPath.Text))
                    fbd.SelectedPath = txtCustomPath.Text;
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    txtCustomPath.Text = fbd.SelectedPath;
                    UpdateOkEnabled();
                }
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            ResultExePath = txtExePath.Text.Trim();
            ResultDelaySeconds = (int)numDelay.Value;
            ResultGameName = SelectedGame?.Name;

            if (radioCustom.Checked) ResultUevrBuild = WrapperIo.UevrBuildCustom;
            else if (radioNightly.Checked) ResultUevrBuild = WrapperIo.UevrBuildNightly;
            else ResultUevrBuild = WrapperIo.UevrBuildRelease;

            ResultManualInjection = chkManual.Checked;
            ResultCustomUevrDir = radioCustom.Checked ? txtCustomPath.Text.Trim() : null;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
