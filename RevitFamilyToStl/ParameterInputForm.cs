using System.Drawing;
using System.Windows.Forms;

namespace RevitFamilyToStl
{
    public class ParameterInputForm : Form
    {
        // Controls
        private TextBox txtPanelHeight, txtPanelWidth, txtWorkOrder, txtComponentId;
        private ComboBox cmbFloorClearance, cmbSeriesId;
        private CheckBox chkInlineLeft, chkInlineRight;
        private Button btnOk, btnCancel;

        // Public properties to access values
        public string PanelHeightValue => txtPanelHeight.Text;
        public string PanelWidthValue => txtPanelWidth.Text;
        public int FloorClearanceSelectorValue => cmbFloorClearance.SelectedIndex + 1;
        public bool IsInlineLeft => chkInlineLeft.Checked;
        public bool IsInlineRight => chkInlineRight.Checked;
        public string WorkOrderNumber => txtWorkOrder.Text;
        public string ComponentId => txtComponentId.Text;
        public string SeriesId => cmbSeriesId.SelectedItem.ToString();

        public ParameterInputForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Enter Instance Parameters";
            this.Size = new Size(400, 420);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int yPos = 20;

            // Panel Height
            AddLabelAndTextBox("Panel Height (72\"-96\"):", yPos, out txtPanelHeight);
            yPos += 30;

            // Panel Width
            AddLabelAndTextBox("Panel Width (5\"-80\"):", yPos, out txtPanelWidth);
            yPos += 30;

            // Panel Thickness (read-only)
            AddLabel("Panel Thickness:", yPos);
            AddReadOnlyLabel("0.5\"", yPos);
            yPos += 30;

            // Floor Clearance
            AddLabel("Floor Clearance:", yPos);
            cmbFloorClearance = AddComboBox(new[] { "1\"", "4.5\"", "9\"", "12\"" }, yPos);
            yPos += 30;

            // Inline Sides
            chkInlineLeft = AddCheckBox("Inline Left Side", yPos, 180);
            chkInlineRight = AddCheckBox("Inline Right Side", yPos, 280);
            yPos += 30;

            // Work Order Number
            AddLabelAndTextBox("Work Order Number:", yPos, out txtWorkOrder, 12);
            yPos += 30;

            // Component ID
            AddLabelAndTextBox("Component ID:", yPos, out txtComponentId, 4);
            yPos += 30;

            // Series ID
            AddLabel("Series ID:", yPos);
            cmbSeriesId = AddComboBox(new[] { "3082G", "3082G.67P", "3086G", "3086G.67P", "3182G", "3182G.67P" }, yPos);
            yPos += 40;

            // OK and Cancel Buttons
            btnOk = new Button { Text = "OK", Location = new Point(200, yPos), DialogResult = DialogResult.OK };
            btnCancel = new Button { Text = "Cancel", Location = new Point(290, yPos), DialogResult = DialogResult.Cancel };
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void AddLabel(string text, int y)
        {
            var label = new Label { Text = text, Location = new Point(20, y), AutoSize = true };
            this.Controls.Add(label);
        }

        private void AddReadOnlyLabel(string text, int y)
        {
            var label = new Label { Text = text, Location = new Point(180, y), AutoSize = true, ForeColor = SystemColors.GrayText };
            this.Controls.Add(label);
        }

        private void AddLabelAndTextBox(string labelText, int y, out TextBox textBox, int maxLength = 0)
        {
            AddLabel(labelText, y);
            textBox = new TextBox { Location = new Point(180, y), Width = 180 };
            if (maxLength > 0) textBox.MaxLength = maxLength;
            this.Controls.Add(textBox);
        }

        private ComboBox AddComboBox(string[] items, int y)
        {
            var comboBox = new ComboBox { Location = new Point(180, y), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            comboBox.Items.AddRange(items);
            comboBox.SelectedIndex = 0;
            this.Controls.Add(comboBox);
            return comboBox;
        }

        private CheckBox AddCheckBox(string text, int y, int x)
        {
            var checkBox = new CheckBox { Text = text, Location = new Point(x, y), AutoSize = true };
            this.Controls.Add(checkBox);
            return checkBox;
        }
    }
}
