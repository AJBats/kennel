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

        // Add mode.
        public AddGameForm()
        {
            _editMode = false;
            InitializeComponent();
        }

        // Edit mode: lock in the existing game, pre-fill fields.
        public AddGameForm(SteamGame existingGame, string existingExe, int existingDelay)
        {
            _editMode = true;
            InitializeComponent();
            SelectedGame = existingGame;
            txtExePath.Text = existingExe ?? "";
            numDelay.Value = Math.Max(numDelay.Minimum, Math.Min(numDelay.Maximum, existingDelay));
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (_editMode)
            {
                txtSearch.Visible = false;
                listGames.Visible = false;
                lblGameLocked.Visible = true;
                lblGameLocked.Text = "Game: " + (SelectedGame?.Name ?? "?") + "  (appid " + (SelectedGame?.AppId ?? "?") + ")";
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
                    var item = new ListViewItem(new[] { g.Name, g.AppId }) { Tag = g };
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
            btnOk.Enabled = ok;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            ResultExePath = txtExePath.Text.Trim();
            ResultDelaySeconds = (int)numDelay.Value;
            ResultGameName = SelectedGame?.Name;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
