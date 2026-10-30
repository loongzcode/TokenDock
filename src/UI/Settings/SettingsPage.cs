using System;
using System.Drawing;
using System.Windows.Forms;

namespace TokenDock
{
    abstract class SettingsPage : UserControl
    {
        readonly TableLayoutPanel stack;
        TableLayoutPanel form;
        protected SettingsPage()
        {
            Dock = DockStyle.Fill;
            AutoScroll = true;
            BackColor = Theme.SettingsBg;
            ForeColor = Theme.Text;
            Font = Theme.Font(9.5f, FontStyle.Regular);
            Padding = new Padding(0, 0, 8, 0);
            stack = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows,
                BackColor = Theme.SettingsBg,
                Margin = new Padding(0)
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(stack);
        }

        protected void Section(string title, string description)
        {
            var card = new SettingsCard
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.Controls.Add(new Label { Text = title, AutoSize = true, Font = Theme.Font(9.5f, FontStyle.Bold), ForeColor = Theme.Text, Margin = new Padding(0, 0, 0, 6) }, 0, 0);
            card.AccessibleDescription = description;
            int row = 1;
            form = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Margin = new Padding(0),
                GrowStyle = TableLayoutPanelGrowStyle.AddRows,
                BackColor = Theme.SettingsBg
            };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.Controls.Add(form, 0, row);
            card.RowCount = row + 1;
            int index = stack.RowCount++;
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stack.Controls.Add(card, 0, index);
        }

        void EnsureSection()
        {
            if (form == null)
                Section("设置", "");
        }

        protected void Row(string label, Control control)
        {
            if (string.IsNullOrEmpty(control.AccessibleName))
                control.AccessibleName = label;
            Row(new Label { Text = label, Name = "Label" + label, AutoSize = true, ForeColor = Theme.Text, MaximumSize = new Size(136, 0) }, control);
        }

        protected void Row(Control text, Control control)
        {
            EnsureSection();
            int row = form.RowCount++;
            form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            text.Anchor = AnchorStyles.Left;
            text.Margin = new Padding(0, 6, 10, 6);
            Control visual = Wrap(control);
            visual.Anchor = visual is Button ? AnchorStyles.Left : AnchorStyles.Left | AnchorStyles.Right;
            visual.Margin = new Padding(0, 2, 0, 5);
            form.Controls.Add(text, 0, row);
            form.Controls.Add(visual, 1, row);
        }

        static Control Wrap(Control control)
        {
            if (control is TextBox || control is ComboBox || control is NumericUpDown)
            {
                var frame = new FieldFrame(control);
                if (control is NumericUpDown)
                {
                    frame.Width = 176;
                    frame.MinimumSize = new Size(176, 32);
                    frame.MaximumSize = new Size(200, 32);
                }

                return frame;
            }

            return control;
        }

        protected void Full(Control control)
        {
            EnsureSection();
            int row = form.RowCount++;
            form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Margin = new Padding(0, 1, 0, 4);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            form.Controls.Add(control, 0, row);
            form.SetColumnSpan(control, 2);
        }

        protected void Hint(string text)
        {
            Full(new Label { Text = text, AutoSize = true, MaximumSize = new Size(620, 0), ForeColor = Theme.Muted, Font = Theme.Font(8.7f, FontStyle.Regular) });
        }

        protected CheckBox Check(string text, bool value)
        {
            var box = new ModernToggle
            {
                Text = text,
                Checked = value
            };
            Full(box);
            return box;
        }

        protected TextBox Input(string text, bool secret)
        {
            return new TextBox
            {
                Text = text,
                UseSystemPasswordChar = secret,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Card,
                ForeColor = Theme.Text
            };
        }

        protected NumericUpDown Number(decimal value, decimal min, decimal max, int decimals)
        {
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = Math.Max(min, Math.Min(max, value)),
                DecimalPlaces = decimals,
                Width = 140,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Card,
                ForeColor = Theme.Text
            };
        }

        protected Control WithAction(Control field, Control action, int width)
        {
            var row = new TableLayoutPanel
            {
                ColumnCount = 2,
                Height = 32,
                Margin = new Padding(0)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width));
            var framed = Wrap(field);
            framed.Dock = DockStyle.Fill;
            framed.Margin = new Padding(0, 0, 8, 0);
            action.Dock = DockStyle.Fill;
            action.Margin = new Padding(0);
            row.Controls.Add(framed, 0, 0);
            row.Controls.Add(action, 1, 0);
            return row;
        }

        public abstract void Apply(Settings settings);
    }
}
