using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Reflection;

namespace TokenDock
{
    static class SettingsRenderer
    {
        public static void Render(SettingsWindow window, string destination, int section)
        {
            window.SelectSection(section);
            window.PerformLayout();
            RenderWindow(window, destination, window.CurrentPage);
        }

        public static void RenderWindow(Form window, string destination, Control currentPage = null)
        {
            window.PerformLayout();
            using (var bitmap = new Bitmap(window.ClientSize.Width, window.ClientSize.Height))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Theme.Bg);
                    foreach (Control child in window.Controls.Cast<Control>().Reverse())
                        Draw(child, graphics, child.Location, currentPage);
                }

                bitmap.Save(destination, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        static void Draw(Control control, Graphics graphics, Point point, Control currentPage)
        {
            if (control is ProgressBar && !control.Visible) return;
            if (control is SettingsPage && control != currentPage)
                return;
            if (control is Panel || control is SettingsPage)
            {
                var previous = graphics.Save();
                graphics.SetClip(new Rectangle(point, control.Size), System.Drawing.Drawing2D.CombineMode.Intersect);
                if (control.BackColor.A > 0)
                    using (var brush = new SolidBrush(control.BackColor))
                        graphics.FillRectangle(brush, new Rectangle(point, control.Size));
                if (control is SettingsCard || control is FieldFrame)
                {
                    var local = graphics.Save();
                    graphics.TranslateTransform(point.X, point.Y);
                    var args = new PaintEventArgs(graphics, control.ClientRectangle);
                    typeof(Control).GetMethod("OnPaintBackground", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, new object[] { args });
                    typeof(Control).GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, new object[] { args });
                    graphics.Restore(local);
                }

                foreach (Control child in control.Controls.Cast<Control>().Reverse())
                    Draw(child, graphics, new Point(point.X + child.Left, point.Y + child.Top), currentPage);
                graphics.Restore(previous);
            }
            else if (control.Width > 0 && control.Height > 0)
                using (var part = new Bitmap(control.Width, control.Height))
                {
                    control.DrawToBitmap(part, new Rectangle(Point.Empty, part.Size));
                    if (control is NumericUpDown)
                        using (var text = Graphics.FromImage(part))
                        {
                            text.Clear(Theme.Card);
                            TextRenderer.DrawText(text, ((NumericUpDown)control).Value.ToString(), control.Font, new Rectangle(Point.Empty, part.Size), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                        }

                    graphics.DrawImageUnscaled(part, point);
                }
        }
    }
}
