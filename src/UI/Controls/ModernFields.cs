using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace TokenDock
{
    sealed class FieldFrame : Panel
    {
        readonly Control input;
        readonly ModernButton minus, plus;
        public FieldFrame(Control value)
        {
            input = value;
            Height = 32;
            DoubleBuffered = true;
            BackColor = Theme.Card;
            Controls.Add(input);
            input.Margin = new Padding(0);
            if (input is NumericUpDown)
            {
                var number = (NumericUpDown)input;
                minus = new ModernButton
                {
                    Text = "−",
                    Subtle = true,
                    AccessibleName = "减少数值",
                    TabStop = false
                };
                plus = new ModernButton
                {
                    Text = "+",
                    Subtle = true,
                    AccessibleName = "增加数值",
                    TabStop = false
                };
                minus.Click += delegate
                {
                    number.Value = Math.Max(number.Minimum, number.Value - number.Increment);
                };
                plus.Click += delegate
                {
                    number.Value = Math.Min(number.Maximum, number.Value + number.Increment);
                };
                Controls.Add(minus);
                Controls.Add(plus);
            }

            if (input is TextBox)
                ((TextBox)input).BorderStyle = BorderStyle.None;
            if (input is NumericUpDown)
                ((NumericUpDown)input).BorderStyle = BorderStyle.None;
            input.Font = Theme.Font(9.5f, FontStyle.Regular);
            input.BackColor = Theme.Card;
            input.ForeColor = Theme.Text;
            input.GotFocus += delegate
            {
                Invalidate();
            };
            input.LostFocus += delegate
            {
                Invalidate();
            };
            input.Enter += delegate
            {
                Invalidate();
            };
            input.Leave += delegate
            {
                Invalidate();
            };
            input.EnabledChanged += delegate
            {
                Invalidate();
            };
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (input == null)
                return;
            float scale = Height / 32f;
            int padding = (int)Math.Round(10 * scale), gap = (int)Math.Round(5 * scale), button = (int)Math.Round(22 * scale);
            int reserved = plus == null ? 0 : button * 2 + gap * 2;
            input.SetBounds(padding, Math.Max(1, (Height - input.PreferredSize.Height) / 2), Math.Max((int)(60 * scale), Width - padding * 2 - reserved), input.PreferredSize.Height);
            if (plus != null)
            {
                plus.SetBounds(Width - button - gap, gap, button, Height - gap * 2);
                minus.SetBounds(Width - button * 2 - gap * 2, gap, button, Height - gap * 2);
                foreach (Control child in input.Controls)
                {
                    if (child.GetType().Name.Contains("UpDownButtons"))
                        child.Visible = false;
                    if (child is TextBox)
                        child.SetBounds(0, child.Top, input.Width - 2, child.Height);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.Path(new RectangleF(.7f, .7f, Width - 1.4f, Height - 1.4f), 3 * e.Graphics.DpiX / 96f))
            using (var pen = new Pen(input.ContainsFocus ? SystemColors.Highlight : Theme.FieldBorder, 1))
                e.Graphics.DrawPath(pen, path);
            base.OnPaint(e);
        }
    }

    sealed class ModernComboBox : ComboBox
    {
        public ModernComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 22;
            Font = Theme.Font(9.5f, FontStyle.Regular);
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
            DropDownHeight = 280;
            IntegralHeight = false;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            bool selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
            using (var brush = new SolidBrush(selected ? Theme.AccentSoft : Theme.Card))
                e.Graphics.FillRectangle(brush, e.Bounds);
            string text = e.Index >= 0 && e.Index < Items.Count ? GetItemText(Items[e.Index]) : Text;
            TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, Math.Max(1, e.Bounds.Width - 10), e.Bounds.Height), Enabled ? Theme.Text : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            if (message.Msg == 0xF && IsHandleCreated)
                using (var graphics = Graphics.FromHwnd(Handle))
                    PaintSelection(graphics);
            else if ((message.Msg == 0x317 || message.Msg == 0x318) && message.WParam != IntPtr.Zero)
                using (var graphics = Graphics.FromHdc(message.WParam))
                    PaintSelection(graphics);
        }

        internal void PaintSelection(Graphics graphics)
        {
            graphics.Clear(Theme.Card);
            var bounds = new Rectangle(4, 0, Math.Max(1, Width - 28), Height);
            TextRenderer.DrawText(graphics, Text, Font, bounds, Enabled ? Theme.Text : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Theme.Muted, 1.4f))
                graphics.DrawLines(pen, new[] { new PointF(Width - 16, Height / 2f - 2), new PointF(Width - 12, Height / 2f + 2), new PointF(Width - 8, Height / 2f - 2) });
        }
    }
}
