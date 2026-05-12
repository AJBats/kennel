using System.Windows.Forms;

namespace UevrLauncher
{
    // Tiny modal-style progress window shown during chihuahua download.
    // Owns no business logic; the caller drives Report() from a worker thread.
    public partial class ChihuahuaProgressForm : Form
    {
        public ChihuahuaProgressForm()
        {
            InitializeComponent();
        }

        public void Report(long current, long total)
        {
            if (total > 0)
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                int pct = (int)System.Math.Min(100, (current * 100L) / total);
                progressBar.Value = pct;
                lblBytes.Text = $"{current / 1024:N0} / {total / 1024:N0} KB";
            }
            else
            {
                progressBar.Style = ProgressBarStyle.Marquee;
                lblBytes.Text = $"{current / 1024:N0} KB";
            }
        }
    }
}
