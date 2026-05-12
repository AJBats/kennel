namespace UevrLauncher
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label lblChihuahuaStatus;
        private System.Windows.Forms.Button btnChihuahua;
        private System.Windows.Forms.Label lblSteamWarning;
        private System.Windows.Forms.ListView listWrappers;
        private System.Windows.Forms.ColumnHeader colGame;
        private System.Windows.Forms.ColumnHeader colDelay;
        private System.Windows.Forms.ColumnHeader colStatus;
        private System.Windows.Forms.Button btnAddGame;
        private System.Windows.Forms.Button btnEdit;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.Timer stateTimer;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.lblChihuahuaStatus = new System.Windows.Forms.Label();
            this.btnChihuahua = new System.Windows.Forms.Button();
            this.lblSteamWarning = new System.Windows.Forms.Label();
            this.listWrappers = new System.Windows.Forms.ListView();
            this.colGame = new System.Windows.Forms.ColumnHeader();
            this.colDelay = new System.Windows.Forms.ColumnHeader();
            this.colStatus = new System.Windows.Forms.ColumnHeader();
            this.btnAddGame = new System.Windows.Forms.Button();
            this.btnEdit = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.stateTimer = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();

            this.lblChihuahuaStatus.AutoSize = false;
            this.lblChihuahuaStatus.Location = new System.Drawing.Point(16, 14);
            this.lblChihuahuaStatus.Size = new System.Drawing.Size(560, 22);
            this.lblChihuahuaStatus.Text = "chihuahua: ...";
            this.lblChihuahuaStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblChihuahuaStatus.Font = new System.Drawing.Font("Segoe UI", 10F);

            this.btnChihuahua.Location = new System.Drawing.Point(600, 10);
            this.btnChihuahua.Size = new System.Drawing.Size(220, 28);
            this.btnChihuahua.Text = "Install chihuahua";
            this.btnChihuahua.UseVisualStyleBackColor = true;
            this.btnChihuahua.Click += new System.EventHandler(this.btnChihuahua_Click);

            this.lblSteamWarning.AutoSize = false;
            this.lblSteamWarning.Location = new System.Drawing.Point(16, 40);
            this.lblSteamWarning.Size = new System.Drawing.Size(804, 20);
            this.lblSteamWarning.Text = "";
            this.lblSteamWarning.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblSteamWarning.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.lblSteamWarning.ForeColor = System.Drawing.Color.FromArgb(180, 95, 6);
            this.lblSteamWarning.Visible = false;
            this.lblSteamWarning.Anchor = System.Windows.Forms.AnchorStyles.Top |
                System.Windows.Forms.AnchorStyles.Left |
                System.Windows.Forms.AnchorStyles.Right;

            this.colGame.Text = "Game";
            this.colGame.Width = 460;
            this.colDelay.Text = "Delay";
            this.colDelay.Width = 80;
            this.colStatus.Text = "Status";
            this.colStatus.Width = 200;

            this.listWrappers.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
                this.colGame, this.colDelay, this.colStatus });
            this.listWrappers.FullRowSelect = true;
            this.listWrappers.HideSelection = false;
            this.listWrappers.Location = new System.Drawing.Point(16, 68);
            this.listWrappers.MultiSelect = false;
            this.listWrappers.Size = new System.Drawing.Size(804, 362);
            this.listWrappers.UseCompatibleStateImageBehavior = false;
            this.listWrappers.View = System.Windows.Forms.View.Details;
            this.listWrappers.Anchor = System.Windows.Forms.AnchorStyles.Top |
                System.Windows.Forms.AnchorStyles.Left |
                System.Windows.Forms.AnchorStyles.Right |
                System.Windows.Forms.AnchorStyles.Bottom;
            this.listWrappers.SelectedIndexChanged += new System.EventHandler(this.listWrappers_SelectedIndexChanged);
            this.listWrappers.DoubleClick += new System.EventHandler(this.btnEdit_Click);

            this.btnAddGame.Location = new System.Drawing.Point(16, 442);
            this.btnAddGame.Size = new System.Drawing.Size(120, 30);
            this.btnAddGame.Text = "Add game…";
            this.btnAddGame.UseVisualStyleBackColor = true;
            this.btnAddGame.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            this.btnAddGame.Click += new System.EventHandler(this.btnAddGame_Click);

            this.btnEdit.Location = new System.Drawing.Point(142, 442);
            this.btnEdit.Size = new System.Drawing.Size(90, 30);
            this.btnEdit.Text = "Edit";
            this.btnEdit.Enabled = false;
            this.btnEdit.UseVisualStyleBackColor = true;
            this.btnEdit.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            this.btnEdit.Click += new System.EventHandler(this.btnEdit_Click);

            this.btnDelete.Location = new System.Drawing.Point(238, 442);
            this.btnDelete.Size = new System.Drawing.Size(90, 30);
            this.btnDelete.Text = "Delete";
            this.btnDelete.Enabled = false;
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);

            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(836, 484);
            this.Controls.Add(this.lblChihuahuaStatus);
            this.Controls.Add(this.btnChihuahua);
            this.Controls.Add(this.lblSteamWarning);
            this.Controls.Add(this.listWrappers);
            this.Controls.Add(this.btnAddGame);
            this.Controls.Add(this.btnEdit);
            this.Controls.Add(this.btnDelete);

            this.stateTimer.Interval = 3000;
            this.stateTimer.Tick += new System.EventHandler(this.stateTimer_Tick);
            this.MinimumSize = new System.Drawing.Size(700, 400);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Kennel";
            this.ResumeLayout(false);
        }
    }
}
