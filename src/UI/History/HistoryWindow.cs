using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TokenDock
{
    sealed class HistoryWindow : Form
    {
        sealed class Choice
        {
            public string Key, Text;
            public override string ToString()
            {
                return Text;
            }
        }

        readonly HistoryStore store;
        readonly HistoryRecorder recorder;
        readonly UsageMonitor monitor;
        readonly ComboBox series, range;
        readonly CheckBox recording;
        readonly Label summary, status;
        readonly HistoryChart chart;
        readonly Timer reloadTimer = new Timer
        {
            Interval = 500
        };
        List<HistoryPoint> all = new List<HistoryPoint>();
        bool filling, loading, again;
        public HistoryWindow(HistoryStore source, HistoryRecorder writer, UsageMonitor state)
        {
            store = source;
            recorder = writer;
            monitor = state;
            Text = "TokenDock · 历史趋势";
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Font(9, FontStyle.Regular);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(780, 500);
            MinimumSize = new Size(620, 400);
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 16),
                ColumnCount = 1,
                RowCount = 5
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            Controls.Add(layout);
            var selectors = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2
            };
            selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            series = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                AccessibleName = "历史指标"
            };
            range = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                AccessibleName = "时间范围"
            };
            range.Items.AddRange(new[] { "最近 24 小时", "最近 7 天", "最近 30 天" });
            range.SelectedIndex = 0;
            selectors.Controls.Add(series, 0, 0);
            selectors.Controls.Add(range, 1, 0);
            layout.Controls.Add(selectors, 0, 0);
            summary = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Theme.Text
            };
            layout.Controls.Add(summary, 0, 1);
            chart = new HistoryChart();
            layout.Controls.Add(chart, 0, 2);
            var caption = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Theme.Muted,
                Text = "圆点为真实采样；超过 30 分钟或额度周期更新处断线。"
            };
            layout.Controls.Add(caption, 0, 3);
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 134));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            recording = new CheckBox
            {
                Text = "记录本机历史",
                AutoSize = true,
                Checked = monitor.Config.HistoryEnabled
            };
            status = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Theme.Muted,
                Text = "仅本机保存，保留 30 天"
            };
            var refresh = Theme.Button("刷新", 76);
            footer.Controls.Add(recording, 0, 0);
            footer.Controls.Add(status, 1, 0);
            footer.Controls.Add(refresh, 2, 0);
            layout.Controls.Add(footer, 0, 4);
            series.SelectedIndexChanged += delegate
            {
                if (!filling)
                    UpdateChart();
            };
            range.SelectedIndexChanged += delegate
            {
                UpdateChart();
            };
            refresh.Click += async delegate
            {
                await Reload();
            };
            recording.CheckedChanged += delegate
            {
                try
                {
                    var staged = Settings.FromJson(Json.Write(monitor.Config));
                    staged.HistoryEnabled = recording.Checked;
                    staged.Save();
                    monitor.Config = staged;
                    monitor.Emit();
                    UpdateStatus();
                }
                catch (Exception error)
                {
                    status.Text = "设置保存失败：" + error.Message;
                }
            };
            reloadTimer.Tick += async delegate
            {
                reloadTimer.Stop();
                await Reload();
            };
            recorder.Saved += Saved;
            Shown += async delegate
            {
                await Reload();
            };
        }

        void Saved()
        {
            if (!IsDisposed && Visible)
                reloadTimer.Start();
        }

        async Task Reload()
        {
            if (loading)
            {
                again = true;
                return;
            }

            loading = true;
            try
            {
                await recorder.FlushAsync();
                var values = await Task.Run(() => store.Read(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow));
                if (IsDisposed)
                    return;
                PresentData(values);
            }
            catch (Exception error)
            {
                if (!IsDisposed)
                    status.Text = "历史读取失败：" + error.Message;
            }
            finally
            {
                filling = false;
                loading = false;
                if (again && !IsDisposed)
                {
                    again = false;
                    reloadTimer.Start();
                }
            }
        }

        internal void PresentData(List<HistoryPoint> values)
        {
            all = values;
            string selected = series.SelectedItem is Choice ? ((Choice)series.SelectedItem).Key : null;
            filling = true;
            series.Items.Clear();
            foreach (var group in all.GroupBy(point => point.Series).OrderBy(group => group.Last().Label))
                series.Items.Add(new Choice { Key = group.Key, Text = group.Last().Label });
            int index = series.Items.Cast<Choice>().ToList().FindIndex(choice => choice.Key == selected);
            if (series.Items.Count > 0)
                series.SelectedIndex = Math.Max(0, index);
            filling = false;
            UpdateChart();
            UpdateStatus();
        }

        void UpdateChart()
        {
            DateTime to = DateTime.UtcNow, from = to.AddDays(range.SelectedIndex == 2 ? -30 : range.SelectedIndex == 1 ? -7 : -1);
            string key = series.SelectedItem is Choice ? ((Choice)series.SelectedItem).Key : "";
            var values = HistorySeries.Select(all, key, HistoryCapture.Unix(from), HistoryCapture.Unix(to));
            chart.SetData(values, from, to);
            summary.Text = values.Count == 0 ? "从启用记录时开始积累数据" : "最近 " + values.Last().Value.ToString("0.##") + values.Last().Unit + "    最低 " + values.Min(point => point.Value).ToString("0.##") + "    最高 " + values.Max(point => point.Value).ToString("0.##") + "    " + values.Count + " 个采样";
        }

        void UpdateStatus()
        {
            status.Text = recorder.Error ?? (monitor.Config.HistoryEnabled ? "仅本机保存，保留 30 天" : "记录已暂停，已有历史保留");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                recorder.Saved -= Saved;
                reloadTimer.Stop();
                reloadTimer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
