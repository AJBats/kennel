namespace UevrLauncher
{
    partial class FirstRunForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label lblWelcome;
        private System.Windows.Forms.Label lblPrompt;
        private System.Windows.Forms.RadioButton radioDocs;
        private System.Windows.Forms.RadioButton radioLocal;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblWelcome = new System.Windows.Forms.Label();
            this.lblPrompt = new System.Windows.Forms.Label();
            this.radioDocs = new System.Windows.Forms.RadioButton();
            this.radioLocal = new System.Windows.Forms.RadioButton();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();

            this.lblWelcome.AutoSize = true;
            this.lblWelcome.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.lblWelcome.Location = new System.Drawing.Point(20, 18);
            this.lblWelcome.Text = "Welcome to Kennel";

            this.lblPrompt.AutoSize = false;
            this.lblPrompt.Size = new System.Drawing.Size(540, 50);
            this.lblPrompt.Location = new System.Drawing.Point(22, 56);
            this.lblPrompt.Text =
                "Where should Kennel store its data? This is where generated wrapper scripts, " +
                "the chihuahua injector, and your settings will live. You can change it " +
                "later by editing %LOCALAPPDATA%\\Kennel\\install.json.";

            this.radioDocs.AutoSize = true;
            this.radioDocs.Location = new System.Drawing.Point(28, 118);
            this.radioDocs.Size = new System.Drawing.Size(500, 24);
            this.radioDocs.UseVisualStyleBackColor = true;

            this.radioLocal.AutoSize = true;
            this.radioLocal.Location = new System.Drawing.Point(28, 148);
            this.radioLocal.Size = new System.Drawing.Size(500, 24);
            this.radioLocal.UseVisualStyleBackColor = true;

            this.btnOk.Text = "Continue";
            this.btnOk.Location = new System.Drawing.Point(380, 198);
            this.btnOk.Size = new System.Drawing.Size(90, 28);
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);

            this.btnCancel.Text = "Cancel";
            this.btnCancel.Location = new System.Drawing.Point(476, 198);
            this.btnCancel.Size = new System.Drawing.Size(90, 28);
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;

            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 244);
            this.Controls.Add(this.lblWelcome);
            this.Controls.Add(this.lblPrompt);
            this.Controls.Add(this.radioDocs);
            this.Controls.Add(this.radioLocal);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnCancel);
            this.AcceptButton = this.btnOk;
            this.CancelButton = this.btnCancel;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.ShowInTaskbar = true;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Kennel — first run";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
