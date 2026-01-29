using System;
using System.Drawing;
using System.Windows.Forms;

namespace NoFences
{
    public class ColorPickerDialog : Form
    {
        private Panel colorPreview;
        private TrackBar alphaSlider;
        private Label alphaLabel;
        private Button btnSelectColor;
        private Button btnOk;
        private Button btnCancel;
        private Color selectedColor;

        public Color SelectedColor => selectedColor;

        public ColorPickerDialog(Color initialColor)
        {
            selectedColor = initialColor;
            InitializeComponent();
            UpdatePreview();
        }

        private void InitializeComponent()
        {
            this.Text = "设置颜色";
            this.Size = new Size(320, 220);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;

            // Color Preview
            var previewLabel = new Label
            {
                Text = "预览:",
                Location = new Point(12, 15),
                AutoSize = true
            };
            this.Controls.Add(previewLabel);

            colorPreview = new Panel
            {
                Location = new Point(60, 12),
                Size = new Size(140, 30),
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(colorPreview);

            btnSelectColor = new Button
            {
                Text = "选择颜色",
                Location = new Point(210, 12),
                Size = new Size(80, 30)
            };
            btnSelectColor.Click += BtnSelectColor_Click;
            this.Controls.Add(btnSelectColor);

            // Alpha Slider
            alphaLabel = new Label
            {
                Text = "透明度: 100",
                Location = new Point(12, 60),
                AutoSize = true
            };
            this.Controls.Add(alphaLabel);

            alphaSlider = new TrackBar
            {
                Location = new Point(12, 85),
                Size = new Size(278, 45),
                Minimum = 0,
                Maximum = 255,
                Value = selectedColor.A,
                TickFrequency = 25
            };
            alphaSlider.ValueChanged += AlphaSlider_ValueChanged;
            this.Controls.Add(alphaSlider);

            // Buttons
            btnOk = new Button
            {
                Text = "确定",
                Location = new Point(120, 140),
                Size = new Size(80, 30),
                DialogResult = DialogResult.OK
            };
            this.Controls.Add(btnOk);

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(210, 140),
                Size = new Size(80, 30),
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void BtnSelectColor_Click(object sender, EventArgs e)
        {
            using (var dialog = new System.Windows.Forms.ColorDialog())
            {
                dialog.Color = Color.FromArgb(255, selectedColor);
                dialog.FullOpen = true;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    selectedColor = Color.FromArgb(alphaSlider.Value, dialog.Color);
                    UpdatePreview();
                }
            }
        }

        private void AlphaSlider_ValueChanged(object sender, EventArgs e)
        {
            selectedColor = Color.FromArgb(alphaSlider.Value, selectedColor);
            alphaLabel.Text = $"透明度: {alphaSlider.Value}";
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            colorPreview.BackColor = Color.FromArgb(255, selectedColor);
            alphaSlider.Value = selectedColor.A;
            alphaLabel.Text = $"透明度: {selectedColor.A}";
        }
    }
}
