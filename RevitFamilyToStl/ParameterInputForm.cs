using System;
using System.Drawing;
using System.Windows.Forms;

namespace RevitFamilyToStl
{
    public class ParameterInputForm : Form
    {
        private Label lblPanelHeight;
        private TextBox txtPanelHeight;
        private Label lblPanelWidth;
        private TextBox txtPanelWidth;
        private Button btnOk;
        private Button btnCancel;

        public string PanelHeightValue => txtPanelHeight.Text;
        public string PanelWidthValue => txtPanelWidth.Text;

        public ParameterInputForm(string heightRange, string lengthRange)
        {
            InitializeComponent(heightRange, lengthRange);
        }

        private void InitializeComponent(string heightRange, string lengthRange)
        {
            this.Text = "Enter Parameters";
            this.Size = new Size(300, 200);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Panel Height
            lblPanelHeight = new Label();
            lblPanelHeight.Text = $"Panel Height ({heightRange}):";
            lblPanelHeight.Location = new Point(10, 20);
            lblPanelHeight.AutoSize = true;
            this.Controls.Add(lblPanelHeight);

            txtPanelHeight = new TextBox();
            txtPanelHeight.Location = new Point(150, 20);
            this.Controls.Add(txtPanelHeight);

            // Panel Width
            lblPanelWidth = new Label();
            lblPanelWidth.Text = $"Panel Width ({lengthRange}):";
            lblPanelWidth.Location = new Point(10, 50);
            lblPanelWidth.AutoSize = true;
            this.Controls.Add(lblPanelWidth);

            txtPanelWidth = new TextBox();
            txtPanelWidth.Location = new Point(150, 50);
            this.Controls.Add(txtPanelWidth);

            // OK Button
            btnOk = new Button();
            btnOk.Text = "OK";
            btnOk.Location = new Point(110, 100);
            btnOk.DialogResult = DialogResult.OK;
            this.Controls.Add(btnOk);

            // Cancel Button
            btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.Location = new Point(190, 100);
            btnCancel.DialogResult = DialogResult.Cancel;
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }
    }
}
