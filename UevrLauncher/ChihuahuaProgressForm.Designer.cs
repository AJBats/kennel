namespace UevrLauncher
{
    partial class ChihuahuaProgressForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label lblHeader;
        private System.Windows.Forms.Label lblBytes;
        private System.Windows.Forms.ProgressBar progressBar;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblHeader = new System.Windows.Forms.Label();
            this.lblBytes = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.SuspendLayout();

            this.lblHeader.AutoSize = true;
            this.lblHeader.Location = new System.Drawing.Point(16, 14);
            this.lblHeader.Text = "Downloading chihuahua…";
            this.lblHeader.Font = new System.Drawing.Font("Segoe UI", 10F);

            this.progressBar.Location = new System.Drawing.Point(16, 40);
            this.progressBar.Size = new System.Drawing.Size(360, 22);

            this.lblBytes.AutoSize = true;
            this.lblBytes.Location = new System.Drawing.Point(16, 68);
            this.lblBytes.Text = "...";

            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(392, 100);
            this.Controls.Add(this.lblHeader);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.lblBytes);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "chihuahua";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
