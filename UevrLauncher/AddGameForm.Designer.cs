namespace UevrLauncher
{
    partial class AddGameForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Label lblSearch;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.ListView listGames;
        private System.Windows.Forms.ColumnHeader colGame;
        private System.Windows.Forms.ColumnHeader colAppId;
        private System.Windows.Forms.Label lblGameLocked;
        private System.Windows.Forms.Label lblExe;
        private System.Windows.Forms.TextBox txtExePath;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Label lblDelay;
        private System.Windows.Forms.NumericUpDown numDelay;
        private System.Windows.Forms.Label lblDelayUnit;
        private System.Windows.Forms.Label lblUevrBuild;
        private System.Windows.Forms.RadioButton radioRelease;
        private System.Windows.Forms.RadioButton radioNightly;
        private System.Windows.Forms.Label lblUevrBuildHint;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblSearch = new System.Windows.Forms.Label();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.listGames = new System.Windows.Forms.ListView();
            this.colGame = new System.Windows.Forms.ColumnHeader();
            this.colAppId = new System.Windows.Forms.ColumnHeader();
            this.lblGameLocked = new System.Windows.Forms.Label();
            this.lblExe = new System.Windows.Forms.Label();
            this.txtExePath = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.lblDelay = new System.Windows.Forms.Label();
            this.numDelay = new System.Windows.Forms.NumericUpDown();
            this.lblDelayUnit = new System.Windows.Forms.Label();
            this.lblUevrBuild = new System.Windows.Forms.Label();
            this.radioRelease = new System.Windows.Forms.RadioButton();
            this.radioNightly = new System.Windows.Forms.RadioButton();
            this.lblUevrBuildHint = new System.Windows.Forms.Label();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();

            ((System.ComponentModel.ISupportInitialize)(this.numDelay)).BeginInit();
            this.SuspendLayout();

            this.lblSearch.AutoSize = true;
            this.lblSearch.Location = new System.Drawing.Point(16, 16);
            this.lblSearch.Text = "Pick a Steam game:";

            this.txtSearch.Location = new System.Drawing.Point(16, 36);
            this.txtSearch.Size = new System.Drawing.Size(580, 23);
            // PlaceholderText isn't on net48 WinForms; leave blank.
            this.txtSearch.TextChanged += new System.EventHandler(this.txtSearch_TextChanged);

            this.colGame.Text = "Game";
            this.colGame.Width = 470;
            this.colAppId.Text = "App ID";
            this.colAppId.Width = 90;

            this.listGames.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
                this.colGame, this.colAppId });
            this.listGames.FullRowSelect = true;
            this.listGames.HideSelection = false;
            this.listGames.Location = new System.Drawing.Point(16, 66);
            this.listGames.MultiSelect = false;
            this.listGames.Size = new System.Drawing.Size(580, 220);
            this.listGames.UseCompatibleStateImageBehavior = false;
            this.listGames.View = System.Windows.Forms.View.Details;
            this.listGames.SelectedIndexChanged += new System.EventHandler(this.listGames_SelectedIndexChanged);

            this.lblGameLocked.AutoSize = false;
            this.lblGameLocked.Location = new System.Drawing.Point(16, 16);
            this.lblGameLocked.Size = new System.Drawing.Size(580, 60);
            this.lblGameLocked.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.lblGameLocked.Visible = false;

            this.lblExe.AutoSize = true;
            this.lblExe.Location = new System.Drawing.Point(16, 300);
            this.lblExe.Text = "Game exe:";

            this.txtExePath.Location = new System.Drawing.Point(96, 297);
            this.txtExePath.Size = new System.Drawing.Size(420, 23);
            this.txtExePath.TextChanged += new System.EventHandler(this.txtExePath_TextChanged);

            this.btnBrowse.Location = new System.Drawing.Point(522, 295);
            this.btnBrowse.Size = new System.Drawing.Size(75, 26);
            this.btnBrowse.Text = "Browse…";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);

            this.lblDelay.AutoSize = true;
            this.lblDelay.Location = new System.Drawing.Point(16, 336);
            this.lblDelay.Text = "VR start delay:";

            this.numDelay.Location = new System.Drawing.Point(112, 333);
            this.numDelay.Size = new System.Drawing.Size(70, 23);
            this.numDelay.Minimum = 1;
            this.numDelay.Maximum = 120;
            this.numDelay.Value = 15;

            this.lblDelayUnit.AutoSize = true;
            this.lblDelayUnit.Location = new System.Drawing.Point(190, 336);
            this.lblDelayUnit.Text = "seconds  (try 15 for most UE games; bump up if UEVR misses the boot)";

            this.lblUevrBuild.AutoSize = true;
            this.lblUevrBuild.Location = new System.Drawing.Point(16, 374);
            this.lblUevrBuild.Text = "UEVR build:";

            this.radioRelease.AutoSize = true;
            this.radioRelease.Location = new System.Drawing.Point(112, 372);
            this.radioRelease.Text = "Release";
            this.radioRelease.UseVisualStyleBackColor = true;

            this.radioNightly.AutoSize = true;
            this.radioNightly.Location = new System.Drawing.Point(192, 372);
            this.radioNightly.Text = "Nightly";
            this.radioNightly.UseVisualStyleBackColor = true;

            this.lblUevrBuildHint.AutoSize = true;
            this.lblUevrBuildHint.Location = new System.Drawing.Point(270, 374);
            this.lblUevrBuildHint.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblUevrBuildHint.Text = "Nightly = bleeding-edge praydog/UEVR; needed for some new games";

            this.btnOk.Location = new System.Drawing.Point(416, 414);
            this.btnOk.Size = new System.Drawing.Size(85, 28);
            this.btnOk.Text = "OK";
            this.btnOk.Enabled = false;
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);

            this.btnCancel.Location = new System.Drawing.Point(510, 414);
            this.btnCancel.Size = new System.Drawing.Size(85, 28);
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;

            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(614, 460);
            this.Controls.Add(this.lblSearch);
            this.Controls.Add(this.txtSearch);
            this.Controls.Add(this.listGames);
            this.Controls.Add(this.lblGameLocked);
            this.Controls.Add(this.lblExe);
            this.Controls.Add(this.txtExePath);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.lblDelay);
            this.Controls.Add(this.numDelay);
            this.Controls.Add(this.lblDelayUnit);
            this.Controls.Add(this.lblUevrBuild);
            this.Controls.Add(this.radioRelease);
            this.Controls.Add(this.radioNightly);
            this.Controls.Add(this.lblUevrBuildHint);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnCancel);
            this.AcceptButton = this.btnOk;
            this.CancelButton = this.btnCancel;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            ((System.ComponentModel.ISupportInitialize)(this.numDelay)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
