using System;
using System.Drawing;
using System.Windows.Forms;

namespace SmartWindow
{
    internal sealed class TimerPresetDialog : Form
    {
        private readonly TextBox nameBox;
        private readonly NumericUpDown minutesBox;
        private readonly NumericUpDown secondsBox;

        public TimerPreset Preset { get; private set; }

        public TimerPresetDialog()
            : this(10 * 60)
        {
        }

        public TimerPresetDialog(int initialSeconds)
        {
            Text = Localization.Get("timerAddPreset");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(18, 21, 27);
            ForeColor = Color.FromArgb(236, 236, 231);
            ClientSize = new Size(380, 220);
            Font = RuntimeAssets.CreateUiFont(14f, FontStyle.Regular);

            Label nameLabel = new Label();
            nameLabel.Text = Localization.Get("timerPresetName");
            nameLabel.AutoSize = false;
            nameLabel.SetBounds(22, 20, 336, 22);
            nameLabel.ForeColor = Color.FromArgb(178, 214, 216, 213);
            Controls.Add(nameLabel);

            nameBox = new TextBox();
            nameBox.SetBounds(22, 45, 336, 30);
            nameBox.BackColor = Color.FromArgb(30, 34, 42);
            nameBox.ForeColor = Color.FromArgb(245, 244, 239);
            nameBox.BorderStyle = BorderStyle.FixedSingle;
            nameBox.Text = Localization.Get("timerPresetDefaultName");
            nameBox.SelectAll();
            Controls.Add(nameBox);

            Label durationLabel = new Label();
            durationLabel.Text = Localization.Get("timerPresetMinutes");
            durationLabel.SetBounds(22, 88, 200, 22);
            durationLabel.ForeColor = Color.FromArgb(178, 214, 216, 213);
            Controls.Add(durationLabel);

            minutesBox = new NumericUpDown();
            minutesBox.SetBounds(22, 113, 108, 30);
            minutesBox.Minimum = 1;
            minutesBox.Maximum = 24 * 60;
            int initialMinutes = Math.Max(
                0,
                Math.Min(24 * 60, initialSeconds / 60));
            int initialSecondRemainder = Math.Max(
                0,
                Math.Min(59, initialSeconds % 60));
            minutesBox.Value = initialMinutes;
            minutesBox.BackColor = Color.FromArgb(30, 34, 42);
            minutesBox.ForeColor = Color.FromArgb(245, 244, 239);
            Controls.Add(minutesBox);

            Label secondsLabel = new Label();
            secondsLabel.Text = Localization.Get("timerSeconds");
            secondsLabel.SetBounds(146, 88, 120, 22);
            secondsLabel.ForeColor = Color.FromArgb(178, 214, 216, 213);
            Controls.Add(secondsLabel);

            secondsBox = new NumericUpDown();
            secondsBox.SetBounds(146, 113, 92, 30);
            secondsBox.Minimum = 0;
            secondsBox.Maximum = 59;
            secondsBox.Value = initialSecondRemainder;
            secondsBox.BackColor = Color.FromArgb(30, 34, 42);
            secondsBox.ForeColor = Color.FromArgb(245, 244, 239);
            Controls.Add(secondsBox);

            Button cancel = CreateButton(
                Localization.Get("close"),
                222,
                141,
                62);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            Button save = CreateButton(
                Localization.Get("save"),
                292,
                141,
                66);
            save.DialogResult = DialogResult.OK;
            save.Click += delegate
            {
                string name = nameBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = Localization.Get("timerPresetDefaultName");
                }
                Preset = new TimerPreset(
                    name.Substring(0, Math.Min(32, name.Length)),
                    Math.Max(
                        1,
                        (int)minutesBox.Value * 60 +
                        (int)secondsBox.Value));
            };
            Controls.Add(save);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private Button CreateButton(
            string text,
            int x,
            int y,
            int width)
        {
            Button button = new Button();
            button.Text = text;
            button.SetBounds(x, y, width, 34);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(74, 82, 96);
            button.BackColor = Color.FromArgb(35, 40, 49);
            button.ForeColor = Color.FromArgb(235, 234, 229);
            return button;
        }
    }

    internal sealed class RgbColorDialog : Form
    {
        private readonly NumericUpDown redBox;
        private readonly NumericUpDown greenBox;
        private readonly NumericUpDown blueBox;
        private readonly Panel preview;

        public Color SelectedColor { get; private set; }

        public RgbColorDialog(Color initial)
        {
            Text = Localization.Get("timerRgbInput");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(18, 21, 27);
            ForeColor = Color.FromArgb(236, 236, 231);
            ClientSize = new Size(360, 180);
            Font = RuntimeAssets.CreateUiFont(14f, FontStyle.Regular);
            SelectedColor = initial;

            redBox = CreateChannel(initial.R, 22);
            greenBox = CreateChannel(initial.G, 132);
            blueBox = CreateChannel(initial.B, 242);
            AddChannelLabel("R", 22);
            AddChannelLabel("G", 132);
            AddChannelLabel("B", 242);

            preview = new Panel();
            preview.SetBounds(22, 78, 204, 42);
            preview.BackColor = initial;
            Controls.Add(preview);

            redBox.ValueChanged += UpdatePreview;
            greenBox.ValueChanged += UpdatePreview;
            blueBox.ValueChanged += UpdatePreview;

            Button cancel = CreateButton(
                Localization.Get("close"),
                234,
                80,
                52);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            Button save = CreateButton(
                Localization.Get("save"),
                292,
                80,
                46);
            save.DialogResult = DialogResult.OK;
            save.Click += delegate
            {
                SelectedColor = Color.FromArgb(
                    (int)redBox.Value,
                    (int)greenBox.Value,
                    (int)blueBox.Value);
            };
            Controls.Add(save);
            AcceptButton = save;
            CancelButton = cancel;
            UpdatePreview(null, EventArgs.Empty);
        }

        private NumericUpDown CreateChannel(int value, int x)
        {
            NumericUpDown control = new NumericUpDown();
            control.SetBounds(x, 35, 94, 30);
            control.Minimum = 0;
            control.Maximum = 255;
            control.Value = value;
            control.BackColor = Color.FromArgb(30, 34, 42);
            control.ForeColor = Color.FromArgb(245, 244, 239);
            Controls.Add(control);
            return control;
        }

        private void AddChannelLabel(string text, int x)
        {
            Label label = new Label();
            label.Text = text;
            label.SetBounds(x, 12, 94, 20);
            label.ForeColor = Color.FromArgb(178, 214, 216, 213);
            Controls.Add(label);
        }

        private Button CreateButton(
            string text,
            int x,
            int y,
            int width)
        {
            Button button = new Button();
            button.Text = text;
            button.SetBounds(x, y, width, 34);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(74, 82, 96);
            button.BackColor = Color.FromArgb(35, 40, 49);
            button.ForeColor = Color.FromArgb(235, 234, 229);
            return button;
        }

        private void UpdatePreview(object sender, EventArgs e)
        {
            if (preview == null)
            {
                return;
            }
            SelectedColor = Color.FromArgb(
                (int)redBox.Value,
                (int)greenBox.Value,
                (int)blueBox.Value);
            preview.BackColor = SelectedColor;
        }
    }
}
