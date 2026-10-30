using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TokenDock
{
    class TaskbarControl : Control, ITaskbarDragHost
    {
        readonly UsageMonitor monitor;
        readonly HoverPreview hoverPreview = new HoverPreview();
        readonly Timer hoverTimer = new Timer
        {
            Interval = 400
        };
        string hoverKey;
        DateTime hoverSince;
        readonly Timer layoutTimer = new Timer
        {
            Interval = 1000
        };
        readonly LayeredSurface surface = new LayeredSurface();
        readonly TaskbarDragController drag;
        IntPtr taskbar, hitWindow;
        List<Rectangle> occupied;
        Task<List<Rectangle>> scan;
        DateTime nextScan = DateTime.MinValue, lastDragLog = DateTime.MinValue;
        float scale = 1;
        int contentWidth = 352;
        bool hover;
        string lastDiagnostic = "", placement = "正在连接任务栏";
        Settings Config
        {
            get
            {
                return monitor.Config;
            }
        }

        public event Action OpenDetails;
        public event Action<string> OpenItemDetails;
        public event Action PlacementChanged;
        string notifiedPlacement;
        public event Action OpenHistory;
        public string Placement
        {
            get
            {
                return placement;
            }
        }

        public bool Embedded
        {
            get
            {
                return IsHandleCreated && taskbar != IntPtr.Zero && NativeMethods.GetParent(Handle) == taskbar;
            }
        }

        public Rectangle ScreenBounds
        {
            get
            {
                return IsHandleCreated ? NativeMethods.Bounds(Handle) : Rectangle.Empty;
            }
        }

        public TaskbarControl(UsageMonitor source)
        {
            monitor = source;
            Text = "TokenDock 任务栏";
            ClientSize = new Size(352, 28);
            BackColor = Theme.Light ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);
            DoubleBuffered = true;
            AccessibleName = "TokenDock 任务栏用量";
            AccessibleRole = AccessibleRole.Pane;
            drag = new TaskbarDragController(this, this);
            ContextMenuStrip = BuildMenu();
            MouseClick += delegate (object sender, MouseEventArgs e)
            {
                HidePreview();
                if (e.Button == MouseButtons.Left && !drag.SuppressClick)
                {
                    string key = TaskbarDrawing.HitTest(CurrentItems(), contentWidth, e.X / scale, Config.TaskbarStyle == "compact");
                    if (key != null && OpenItemDetails != null)
                        OpenItemDetails(key);
                    else if (OpenDetails != null)
                        OpenDetails();
                }
            };
            MouseEnter += delegate
            {
                hover = true;
                Render();
            };
            MouseLeave += delegate
            {
                HidePreview();
                hover = false;
                Render();
            };
            monitor.Changed += OnChanged;
            MouseMove += PreviewMouseMove;
            MouseDown += delegate
            {
                HidePreview();
            };
            hoverTimer.Tick += delegate
            {
                UpdatePreview();
            };
            hoverTimer.Start();
            VisibleChanged += delegate
            {
                if (!Visible)
                    HidePreview();
            };
            layoutTimer.Tick += delegate
            {
                try
                {
                    Maintain();
                }
                catch (Exception error)
                {
                    layoutTimer.Stop();
                    Hide();
                    placement = "任务栏连接失败";
                    WriteDiagnostic();
                    ReportError(error);
                }
            };
        }

        ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("用量与电量", null, delegate
            {
                if (OpenDetails != null)
                    OpenDetails();
            });
            menu.Items.Add("历史趋势", null, delegate
            {
                if (OpenHistory != null)
                    OpenHistory();
            });
            menu.Items.Add("调整显示顺序…", null, delegate
            {
                using (var dialog = new ItemOrderWindow(TaskbarOrder.Choices(monitor, Config.ItemOrder)))
                {
                    if (dialog.ShowDialog() != DialogResult.OK)
                        return;
                    try
                    {
                        var staged = Settings.FromJson(Json.Write(Config));
                        staged.ItemOrder = dialog.Order;
                        staged.Save();
                        monitor.Config = staged;
                        monitor.Emit();
                    }
                    catch (Exception error)
                    {
                        MessageBox.Show("保存顺序失败：" + error.Message, "TokenDock");
                    }
                }
            });
            var positions = new ToolStripMenuItem("位置");
            string[] modes =
            {
                "auto",
                "left",
                "right",
                "custom"
            }, labels =
            {
                "自动寻找空位",
                "靠左",
                "靠右",
                "自定义（拖动）"
            };
            for (int i = 0; i < modes.Length; i++)
            {
                string mode = modes[i];
                var item = new ToolStripMenuItem(labels[i])
                {
                    Tag = mode
                };
                item.Click += delegate
                {
                    SetPlacement(mode);
                };
                positions.DropDownItems.Add(item);
            }

            menu.Opening += delegate
            {
                foreach (ToolStripMenuItem item in positions.DropDownItems)
                    item.Checked = (string)item.Tag == (Config.TaskbarPosition ?? "auto");
            };
            menu.Items.Add(positions);
            var lockPosition = new ToolStripMenuItem("锁定位置");
            lockPosition.Click += delegate
            {
                try
                {
                    var staged = Settings.FromJson(Json.Write(Config));
                    staged.TaskbarPositionLocked = !staged.TaskbarPositionLocked;
                    if (staged.TaskbarPositionLocked && !ScreenBounds.IsEmpty)
                    {
                        staged.TaskbarPosition = "custom";
                        staged.TaskbarCustomOffset = (int)Math.Round((ScreenBounds.Left - NativeMethods.Bounds(taskbar).Left) / scale);
                    }

                    staged.Save();
                    monitor.Config = staged;
                    monitor.Emit();
                }
                catch (Exception error)
                {
                    MessageBox.Show("保存锁定状态失败：" + error.Message, "TokenDock");
                }
            };
            menu.Items.Add(lockPosition);
            menu.Opening += delegate
            {
                lockPosition.Checked = Config.TaskbarPositionLocked;
                positions.Enabled = true;
            };
            var compact = new ToolStripMenuItem("紧凑显示（图标 + 数值）");
            compact.Click += delegate
            {
                Config.TaskbarStyle = Config.TaskbarStyle == "compact" ? "full" : "compact";
                SavePlacement();
                OnChanged();
            };
            menu.Items.Add(compact);
            menu.Opening += delegate
            {
                HidePreview();
                compact.Checked = Config.TaskbarStyle == "compact";
            };
            menu.Items.Add("刷新", null, async delegate
            {
                await monitor.Refresh(true);
            });
            return menu;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var value = base.CreateParams;
                value.Parent = NativeMethods.FindWindow("Shell_TrayWnd", null);
                value.Style = (int)(((uint)value.Style & ~0x90000000u) | 0x40000000u);
                value.ExStyle |= 0x08000000;
                if (surface.IsLayered)
                    value.ExStyle |= 0x00080000;
                return value;
            }
        }

        public void Start()
        {
            Maintain();
            layoutTimer.Start();
        }

        public void SetPlacement(string mode)
        {
            if (mode == "custom" && !ScreenBounds.IsEmpty)
                Config.TaskbarCustomOffset = (int)Math.Round((ScreenBounds.Left - NativeMethods.Bounds(taskbar).Left) / scale);
            Config.TaskbarPosition = mode;
            if (mode == "auto")
                Config.TaskbarCustomOffset = 12;
            SavePlacement();
            Maintain();
        }

        void SavePlacement()
        {
            try
            {
                Config.Save();
            }
            catch (Exception error)
            {
                ReportError(error);
            }
        }

        List<TaskbarItem> ReadItems()
        {
            return TaskbarItemBuilder.Build(monitor.Codex, monitor.Claude, monitor.DeepSeek, monitor.Devices, monitor.BluetoothUpdated.HasValue, Config);
        }

        List<TaskbarItem> CurrentItems()
        {
            return drag.IsActive ? drag.Items : ReadItems();
        }

        void OnChanged()
        {
            drag.ApplyPositionLock();
            var items = CurrentItems();
            AccessibleDescription = string.Join("；", items.Select(item => (item.Tooltip ?? item.Label) + " " + item.Value));
            if (!Config.HoverPreviewEnabled || !items.Any(item => item.Key == hoverKey))
                HidePreview();
            Maintain();
        }

        void HidePreview()
        {
            hoverKey = null;
            hoverPreview.Hide();
        }

        void PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (drag.IsActive || Capture || !Config.HoverPreviewEnabled || ContextMenuStrip.Visible)
            {
                HidePreview();
                return;
            }

            string key = TaskbarDrawing.HitTest(CurrentItems(), contentWidth, e.X / scale, Config.TaskbarStyle == "compact");
            if (hoverKey != key)
            {
                hoverPreview.Hide();
                hoverKey = key;
                hoverSince = DateTime.UtcNow;
            }
        }

        void UpdatePreview()
        {
            if (hoverKey == null || !Visible || drag.IsActive || Capture || !Config.HoverPreviewEnabled || ContextMenuStrip.Visible)
            {
                HidePreview();
                return;
            }

            Point pointer = PointToClient(Cursor.Position);
            if (!ClientRectangle.Contains(pointer) || TaskbarDrawing.HitTest(CurrentItems(), contentWidth, pointer.X / scale, Config.TaskbarStyle == "compact") != hoverKey)
            {
                HidePreview();
                return;
            }

            if ((DateTime.UtcNow - hoverSince).TotalMilliseconds < 450)
                return;
            hoverPreview.Present(HoverContent.Build(hoverKey, monitor, DateTime.Now), ScreenBounds, scale);
        }

        void Maintain()
        {
            if (IsDisposed)
                return;
            IntPtr found = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (found == IntPtr.Zero)
            {
                Hide();
                placement = "未找到 Windows 任务栏";
                WriteDiagnostic();
                return;
            }

            if (!IsHandleCreated)
            {
                IntPtr ignored = Handle;
            }

            if (found != taskbar || NativeMethods.GetParent(Handle) != found)
            {
                taskbar = found;
                occupied = null;
                scan = null;
                nextScan = DateTime.MinValue;
                NativeMethods.SetStyle(Handle, -16, (NativeMethods.GetStyle(Handle, -16) & ~0x80000000L) | 0x40000000L);
                NativeMethods.SetParent(Handle, taskbar);
                if (!Embedded)
                {
                    Hide();
                    placement = "任务栏嵌入失败，可从托盘查看详情";
                    WriteDiagnostic();
                    return;
                }
            }

            scale = NativeMethods.Dpi(taskbar);
            Rectangle bounds = NativeMethods.Bounds(taskbar);
            if (bounds.Width < bounds.Height)
            {
                Hide();
                placement = "暂不支持竖向任务栏";
                WriteDiagnostic();
                return;
            }

            int natural = TaskbarDrawing.Measure(CurrentItems(), Config.TaskbarStyle == "compact");
            if (natural == 0)
            {
                Hide();
                placement = "暂无可显示的数据，可从托盘连接账户";
                WriteDiagnostic();
                return;
            }

            if (scan != null && scan.IsCompleted)
            {
                if (scan.Status == TaskStatus.RanToCompletion)
                    occupied = scan.Result;
                scan = null;
            }

            if (scan == null && DateTime.UtcNow >= nextScan)
            {
                IntPtr handle = taskbar;
                scan = Task.Run(() => TaskbarOccupancyReader.Read(handle));
                nextScan = DateTime.UtcNow.AddSeconds(5);
            }

            int desired = (int)Math.Ceiling(natural * scale), minimum = Math.Min(desired, (int)(80 * scale)), height = Math.Min(bounds.Height, (int)(28 * scale));
            if (occupied == null)
            {
                Hide();
                placement = "正在检测任务栏可用位置";
                WriteDiagnostic();
                return;
            }

            var blocks = new List<Rectangle>(occupied);
            Rectangle tray = NativeMethods.Bounds(NativeMethods.FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null));
            if (!tray.IsEmpty)
                blocks.Add(tray);
            Rectangle slot = TaskbarLayout.PositionedSlot(bounds, blocks, desired, minimum, (int)(12 * scale), height, drag.IsActive ? "custom" : Config.TaskbarPosition, drag.IsActive ? drag.RequestedX : bounds.Left + (int)(Config.TaskbarCustomOffset * scale), !drag.IsActive);
            if (slot.IsEmpty)
            {
                Hide();
                placement = "任务栏空间不足，可从托盘查看详情";
                WriteDiagnostic();
                return;
            }

            contentWidth = (int)(slot.Width / scale);
            var location = new NativeMethods.Pt(slot.Left, slot.Top);
            NativeMethods.ScreenToClient(taskbar, ref location);
            NativeMethods.SetWindowPos(Handle, IntPtr.Zero, location.X, location.Y, slot.Width, slot.Height, 0x0010 | (Config.ShowBar ? 0x0040u : 0x0080u));
            if (Config.ShowBar && !Visible)
                Show();
            if (!Config.ShowBar && Visible)
                Hide();
            Render();
            Rectangle actual = ScreenBounds;
            hitWindow = surface.Rendered && surface.OpaquePixels > 0 ? NativeMethods.WindowFromPoint(new NativeMethods.Pt(actual.Left + surface.OpaquePoint.X, actual.Top + surface.OpaquePoint.Y)) : IntPtr.Zero;
            bool visible = NativeMethods.IsWindowVisible(Handle) && surface.Rendered && hitWindow == Handle && bounds.Contains(actual);
            placement = visible ? "任务栏控件已显示" : !NativeMethods.IsWindowVisible(Handle) ? "任务栏控件已隐藏" : !surface.Rendered ? "任务栏绘制未完成" : "任务栏控件可能被遮挡";
            WriteDiagnostic();
        }

        void WriteDiagnostic()
        {
            if (notifiedPlacement != placement)
            {
                notifiedPlacement = placement;
                if (PlacementChanged != null)
                    PlacementChanged();
            }
            if (!IsHandleCreated)
                return;
            if (drag.IsActive)
            {
                if ((DateTime.UtcNow - lastDragLog).TotalMilliseconds < 500)
                    return;
                lastDragLog = DateTime.UtcNow;
            }

            Rectangle bounds = ScreenBounds;
            bool fullHitArea = NativeMethods.WindowFromPoint(new NativeMethods.Pt(bounds.Left + bounds.Width / 2, bounds.Bottom - 1)) == Handle;
            string value = Json.Write(new { version = AppIdentity.Version, visibleItems = CurrentItems().Select(item => item.Key).ToArray(), displayStyle = Config.TaskbarStyle, hoverVisible = hoverPreview.Visible, positionMode = Config.TaskbarPosition, positionLocked = Config.TaskbarPositionLocked, dragging = drag.IsActive, previewVisible = drag.PreviewVisible, fullHitArea = fullHitArea, embedded = Embedded, status = placement, parentHandle = taskbar.ToInt64(), actualParent = NativeMethods.GetParent(Handle).ToInt64(), taskbarHandle = taskbar.ToInt64(), widget = bounds, taskbar = NativeMethods.Bounds(taskbar), visibleManaged = Visible, visibleNative = NativeMethods.IsWindowVisible(Handle), rendered = surface.Rendered, opaquePixels = surface.OpaquePixels, hitWindow = hitWindow.ToInt64(), widgetHandle = Handle.ToInt64(), renderError = surface.Error, occupiedControls = occupied == null ? -1 : occupied.Count, layered = surface.IsLayered });
            if (value == lastDiagnostic)
                return;
            lastDiagnostic = value;
            try
            {
                Json.AtomicWrite(Path.Combine(AppPaths.DataDirectory, "taskbar-status.json"), value);
            }
            catch
            {
            }
        }

        void ReportError(Exception error)
        {
            try
            {
                Json.AtomicWrite(Path.Combine(AppPaths.DataDirectory, "taskbar-error.json"), Json.Write(new { error = error.Message, type = error.GetType().FullName }));
            }
            catch
            {
            }
        }

        public void Render()
        {
            if (!IsHandleCreated || !Embedded || Width <= 0 || Height <= 0 || !NativeMethods.IsWindowVisible(Handle))
                return;
            using (var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.FromArgb(1, 0, 0, 0));
                    graphics.ScaleTransform(scale, scale);
                    DrawContents(graphics);
                }

                surface.Update(this, bitmap);
            }
        }

        void DrawContents(Graphics graphics)
        {
            TaskbarDrawing.Paint(graphics, Height / scale, contentWidth, CurrentItems(), hover, drag.IsActive, TaskbarLayout.CanDrop(NativeMethods.Bounds(taskbar), Cursor.Position, (int)(48 * scale)), Config.TaskbarStyle == "compact");
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!surface.IsLayered)
            {
                e.Graphics.Clear(BackColor);
                e.Graphics.ScaleTransform(scale, scale);
                DrawContents(e.Graphics);
                surface.MarkGdiRendered();
            }
        }

        float ITaskbarDragHost.DpiScale
        {
            get
            {
                return scale;
            }
        }

        Rectangle ITaskbarDragHost.TaskbarBounds
        {
            get
            {
                return NativeMethods.Bounds(taskbar);
            }
        }

        Rectangle ITaskbarDragHost.WidgetBounds
        {
            get
            {
                return ScreenBounds;
            }
        }

        bool ITaskbarDragHost.CompactDisplay
        {
            get
            {
                return Config.TaskbarStyle == "compact";
            }
        }

        bool ITaskbarDragHost.PositionLocked
        {
            get
            {
                return Config.TaskbarPositionLocked;
            }
        }

        List<TaskbarItem> ITaskbarDragHost.CaptureItems()
        {
            return ReadItems();
        }

        void ITaskbarDragHost.UpdateDragLayout()
        {
            Maintain();
        }

        void ITaskbarDragHost.CommitDragPosition()
        {
            if (Config.TaskbarPositionLocked)
                return;
            Config.TaskbarPosition = "custom";
            Config.TaskbarCustomOffset = (int)Math.Round((ScreenBounds.Left - NativeMethods.Bounds(taskbar).Left) / scale);
            SavePlacement();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                drag.Dispose();
                layoutTimer.Stop();
                layoutTimer.Dispose();
                monitor.Changed -= OnChanged;
                hoverTimer.Stop();
                hoverTimer.Dispose();
                hoverPreview.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
