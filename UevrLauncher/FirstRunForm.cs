using System;
using System.Windows.Forms;
using UevrLauncher.Services;

namespace UevrLauncher
{
    public partial class FirstRunForm : Form
    {
        public string SelectedDataRoot { get; private set; }

        public FirstRunForm()
        {
            InitializeComponent();
            radioDocs.Text = "Documents folder    " + ConfigStore.DefaultDocumentsRoot;
            radioLocal.Text = "App data folder     " + ConfigStore.DefaultLocalAppDataRoot;
            radioDocs.Checked = true;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            SelectedDataRoot = radioDocs.Checked
                ? ConfigStore.DefaultDocumentsRoot
                : ConfigStore.DefaultLocalAppDataRoot;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
