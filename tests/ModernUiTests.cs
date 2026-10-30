using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TokenDock
{
    static class ModernUiTests
    {
        public static string Run()
        {
            int count = 0;
            Action<bool, string> check = delegate (bool valid, string message)
            {
                if (!valid)
                    throw new Exception("FAIL: " + message);
                count++;
            };
            foreach (float scale in new[]
            {
                1f,
                1.5f,
                2f
            }

            )
            {
                var size = new Size((int)(420 * scale), (int)(600 * scale));
                var bounds = OverviewFooterLayout.Calculate(size, scale);
                check(bounds.All(rect => new Rectangle(Point.Empty, size).Contains(rect)) && !bounds[0].IntersectsWith(bounds[1]) && !bounds[1].IntersectsWith(bounds[2]), "footer targets stay within popup and do not overlap at " + scale);
                check(bounds.Take(3).All(rect => rect.Height >= 36 * scale - 1 && rect.Width > 100 * scale), "footer buttons have usable targets at " + scale);
                using (var number = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0,
                    Increment = 1
                }

                )
                using (var frame = new FieldFrame(number)
                {
                    Size = new Size((int)(200 * scale), (int)(40 * scale))
                }

                )
                {
                    frame.PerformLayout();
                    var buttons = frame.Controls.OfType<ModernButton>().ToList();
                    check(buttons.Count == 2 && buttons.All(button => !button.Bounds.IntersectsWith(number.Bounds)) && !buttons[0].Bounds.IntersectsWith(buttons[1].Bounds), "numeric input never overlaps its step buttons at " + scale);
                    Click(buttons.Single(button => button.AccessibleName == "减少数值"));
                    check(number.Value == 0, "numeric decrease respects minimum at " + scale);
                    Click(buttons.Single(button => button.AccessibleName == "增加数值"));
                    check(number.Value == 1, "numeric increase changes the actual saved value at " + scale);
                }
            }

            var oldContext = SynchronizationContext.Current;
            try
            {
                var provider = new GatedProvider();
                var monitor = new UsageMonitor(new Settings(), new[] { provider }, new EmptyBluetooth());
                using (var overview = new OverviewControl(monitor, null)
                {
                    Size = new Size(420, 600)
                }

                )
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                    int settings = 0, history = 0, closed = 0;
                    overview.SettingsRequested += delegate
                    {
                        settings++;
                    };
                    overview.HistoryRequested += delegate
                    {
                        history++;
                    };
                    overview.CloseRequested += delegate
                    {
                        closed++;
                    };
                    var settingsButton = (ModernButton)overview.Controls.Find("OpenSettings", false).Single();
                    var historyButton = (ModernButton)overview.Controls.Find("OpenHistory", false).Single();
                    var closeButton = (ModernButton)overview.Controls.Find("CloseOverview", false).Single();
                    Click(settingsButton);
                    Click(historyButton);
                    Click(closeButton);
                    check(settings == 1 && history == 1 && closed == 1, "real overview buttons invoke their own actions once");
                    var refresh = (ModernButton)overview.Controls.Find("RefreshOverview", false).Single();
                    var task = overview.RefreshAsync();
                    var repeated = overview.RefreshAsync();
                    check(refresh.Busy && !refresh.Enabled && refresh.Text.Contains("刷新中"), "refresh immediately shows pending state and blocks repeated clicks");
                    check(object.ReferenceEquals(task, repeated), "repeated refresh joins the active operation");
                    provider.Release();
                    task.GetAwaiter().GetResult();
                    check(!refresh.Busy && refresh.Enabled && refresh.Text == "已刷新" && refresh.Glyph == "check", "successful refresh shows completion and re-enables button");
                    check(provider.Count == 1, "overview feedback does not send duplicate provider requests");
                }
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(oldContext);
            }

            oldContext = SynchronizationContext.Current;
            try
            {
                var failureMonitor = new UsageMonitor(new Settings(), new[] { new FailingProvider() }, new EmptyBluetooth());
                using (var overview = new OverviewControl(failureMonitor, null)
                {
                    Size = new Size(420, 600)
                }

                )
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                    overview.RefreshAsync().GetAwaiter().GetResult();
                    var refresh = (ModernButton)overview.Controls.Find("RefreshOverview", false).Single();
                    check(refresh.Text == "部分失败" && refresh.Enabled && !refresh.Busy, "failed source reports partial failure and remains retryable");
                }
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(oldContext);
            }

            using (var button = new KeyboardButton
            {
                Size = new Size(120, 38),
                Text = "保存",
                Primary = true
            }

            )
            {
                int before = Pixel(button);
                button.Press();
                int during = Pixel(button);
                button.Release();
                check(before != during, "keyboard activation has visible pressed feedback");
            }

            var source = new UsageMonitor(new Settings { TaskbarPositionLocked = true, QuotaAlertPercent = 17 });
            using (var bar = new TaskbarControl(source))
            using (var settingsWindow = new SettingsWindow(source, bar))
            {
                settingsWindow.SelectSection(1);
                settingsWindow.PerformLayout();
                check(Find<SettingsCard>(settingsWindow.CurrentPage).Count >= 3 && Find<ModernToggle>(settingsWindow.CurrentPage).Count >= 4, "display settings retain sections and checkable options");
                var staged = Settings.FromJson(Json.Write(source.Config));
                ((SettingsPage)settingsWindow.CurrentPage).Apply(staged);
                check(staged.TaskbarPositionLocked && staged.TaskbarStyle == source.Config.TaskbarStyle && staged.ShowDevices == source.Config.ShowDevices, "redesigned display page preserves actual settings");
                var position = (ComboBox)settingsWindow.CurrentPage.Controls.Find("TaskbarPosition", true).Single();
                check(position.Enabled, "position selector remains enabled when accidental dragging is locked");
                position.SelectedIndex = 1;
                ((SettingsPage)settingsWindow.CurrentPage).Apply(staged);
                check(staged.TaskbarPosition == "left" && staged.TaskbarPositionLocked, "explicit position selection preserves the drag lock");
                settingsWindow.SelectSection(2);
                ((SettingsPage)settingsWindow.CurrentPage).Apply(staged);
                check(staged.QuotaAlertPercent == 17, "redesigned numeric fields retain configured thresholds");
                foreach (var number in Find<NumericUpDown>(settingsWindow.CurrentPage))
                {
                    check(number.Parent.Height >= 32 && number.Parent.Width >= 160 && number.Parent.ClientRectangle.Contains(number.Bounds), "numeric field remains visible inside its input frame");
                }

                var save = settingsWindow.Controls.Find("SaveSettings", true).Single();
                check(save is ModernButton && ((ModernButton)save).Primary, "save uses a distinct primary action");
            }

            return "PASS " + count + " modern UI checks";
        }

        static void Click(Button button)
        {
            typeof(Button).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(button, new object[] { EventArgs.Empty });
        }

        static List<T> Find<T>(Control parent)
            where T : Control
        {
            var result = new List<T>();
            foreach (Control child in parent.Controls)
            {
                if (child is T)
                    result.Add((T)child);
                result.AddRange(Find<T>(child));
            }

            return result;
        }

        static int Pixel(Control control)
        {
            using (var image = new Bitmap(control.Width, control.Height))
            {
                control.DrawToBitmap(image, control.ClientRectangle);
                return image.GetPixel(14, 8).ToArgb();
            }
        }

        sealed class KeyboardButton : ModernButton
        {
            public void Press()
            {
                OnKeyDown(new KeyEventArgs(Keys.Space));
            }

            public void Release()
            {
                OnKeyUp(new KeyEventArgs(Keys.Space));
            }
        }

        sealed class GatedProvider : IUsageProvider
        {
            readonly TaskCompletionSource<UsageSnapshot> gate = new TaskCompletionSource<UsageSnapshot>();
            public int Count;
            public string Id
            {
                get
                {
                    return "codex";
                }
            }

            public string Name
            {
                get
                {
                    return "Codex";
                }
            }

            public Task<UsageSnapshot> ReadAsync(Settings settings)
            {
                Count++;
                return gate.Task;
            }

            public void Release()
            {
                gate.SetResult(new UsageSnapshot { Name = "Codex", Updated = DateTime.Now, Quotas = new List<QuotaWindow> { new QuotaWindow { Label = "7天", Used = 13 } } });
            }
        }

        sealed class EmptyBluetooth : IBluetoothSource
        {
            public Task<BluetoothSnapshot> ReadAsync(Settings settings)
            {
                return Task.FromResult(new BluetoothSnapshot { Status = "成功", Updated = DateTime.Now });
            }
        }

        sealed class FailingProvider : IUsageProvider
        {
            public string Id
            {
                get
                {
                    return "codex";
                }
            }

            public string Name
            {
                get
                {
                    return "Codex";
                }
            }

            public Task<UsageSnapshot> ReadAsync(Settings settings)
            {
                throw new System.IO.IOException("offline");
            }
        }
    }
}
