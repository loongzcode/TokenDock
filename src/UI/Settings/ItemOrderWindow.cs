using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TokenDock
{
    sealed class ItemOrderWindow : Form
    {
        readonly ListBox list;
        Point down;
        int dragIndex = -1;
        readonly string dragToken = "TokenDock.Order." + Guid.NewGuid().ToString("N");
        public List<string> Order
        {
            get
            {
                return list.Items.Cast<OrderChoice>().Select(item => item.Key).ToList();
            }
        }

        public ItemOrderWindow(IEnumerable<OrderChoice> choices)
        {
            Text = "调整显示顺序";
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Font(9, FontStyle.Regular);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(420, 360);
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                ColumnCount = 2,
                RowCount = 3
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            Controls.Add(layout);
            var hint = new Label
            {
                Text = "拖动项目排序，也可选中后点击上移、下移。",
                Dock = DockStyle.Fill,
                ForeColor = Theme.Muted
            };
            layout.Controls.Add(hint, 0, 0);
            layout.SetColumnSpan(hint, 2);
            list = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                AllowDrop = true,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                AccessibleName = "任务栏项目顺序"
            };
            list.Items.AddRange(choices.Cast<object>().ToArray());
            if (list.Items.Count > 0)
                list.SelectedIndex = 0;
            layout.Controls.Add(list, 0, 1);
            var moves = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(8, 0, 0, 0)
            };
            var up = Theme.Button("上移", 70);
            var lower = Theme.Button("下移", 70);
            up.Click += delegate
            {
                MoveSelection(list.SelectedIndex, list.SelectedIndex - 1);
            };
            lower.Click += delegate
            {
                MoveSelection(list.SelectedIndex, list.SelectedIndex + 1);
            };
            moves.Controls.Add(up);
            moves.Controls.Add(lower);
            layout.Controls.Add(moves, 1, 1);
            list.MouseDown += delegate (object sender, MouseEventArgs e)
            {
                down = e.Location;
                dragIndex = list.IndexFromPoint(e.Location);
            };
            list.MouseMove += delegate (object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left || dragIndex < 0)
                    return;
                var threshold = new Rectangle(down.X - SystemInformation.DragSize.Width / 2, down.Y - SystemInformation.DragSize.Height / 2, SystemInformation.DragSize.Width, SystemInformation.DragSize.Height);
                if (!threshold.Contains(e.Location))
                {
                    list.DoDragDrop(dragToken, DragDropEffects.Move);
                    dragIndex = -1;
                }
            };
            list.DragOver += delegate (object sender, DragEventArgs e)
            {
                if (dragIndex < 0 || !e.Data.GetDataPresent(DataFormats.Text) || (string)e.Data.GetData(DataFormats.Text) != dragToken)
                    return;
                e.Effect = DragDropEffects.Move;
                int index = list.IndexFromPoint(list.PointToClient(new Point(e.X, e.Y)));
                if (index >= 0)
                    list.SelectedIndex = index;
            };
            list.DragDrop += delegate (object sender, DragEventArgs e)
            {
                if (dragIndex < 0 || !e.Data.GetDataPresent(DataFormats.Text) || (string)e.Data.GetData(DataFormats.Text) != dragToken)
                    return;
                int target = list.IndexFromPoint(list.PointToClient(new Point(e.X, e.Y)));
                MoveSelection(dragIndex, target < 0 ? list.Items.Count - 1 : target);
            };
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 12, 0, 0),
                WrapContents = false
            };
            var save = Theme.Button("确定", 80);
            save.DialogResult = DialogResult.OK;
            var cancel = Theme.Button("取消", 80);
            cancel.DialogResult = DialogResult.Cancel;
            actions.Controls.Add(save);
            actions.Controls.Add(cancel);
            layout.Controls.Add(actions, 0, 2);
            layout.SetColumnSpan(actions, 2);
            AcceptButton = save;
            CancelButton = cancel;
        }

        void MoveSelection(int from, int to)
        {
            var choices = list.Items.Cast<OrderChoice>().ToList();
            if (!TaskbarOrder.Move(choices, from, to))
                return;
            list.BeginUpdate();
            list.Items.Clear();
            list.Items.AddRange(choices.Cast<object>().ToArray());
            list.SelectedIndex = to;
            list.EndUpdate();
        }
    }
}
