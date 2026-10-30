using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TokenDock
{
    static class IconAndLockTests
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
            foreach (string key in new[]
            {
                "codex",
                "claude",
                "deepseek"
            }

            )
            {
                check(ItemIcons.HasBrand(key), key + " logo is embedded in the executable");
                using (var bitmap = new Bitmap(36, 36))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Transparent);
                    ItemIcons.Draw(graphics, new TaskbarItem { Key = key }, 0, 0, 36);
                    int colored = 0;
                    for (int y = 0; y < 36; y++)
                        for (int x = 0; x < 36; x++)
                        {
                            Color pixel = bitmap.GetPixel(x, y);
                            if (pixel.A > 100 && Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) - Math.Min(pixel.R, Math.Min(pixel.G, pixel.B)) > 35)
                                colored++;
                        }

                    check(colored > 60, key + " renders its color logo rather than a letter substitute");
                }
            }

            check(DeviceIconResolver.Resolve("RK R98 5.0 ") == "keyboard", "connected RK keyboard uses a keyboard icon");
            check(DeviceIconResolver.Resolve("Bluetooth Keyboard") == "keyboard" && DeviceIconResolver.Resolve("机械键盘") == "keyboard", "generic keyboard names work across languages");
            check(DeviceIconResolver.Resolve("Wireless Mouse") == "mouse" && DeviceIconResolver.Resolve("蓝牙鼠标") == "mouse", "mouse names use a mouse icon");
            check(DeviceIconResolver.Resolve("AirPods Pro") == "headphones" && DeviceIconResolver.Resolve("耳机") == "headphones", "headphones use their device type");
            check(DeviceIconResolver.Resolve(null) == "device" && DeviceIconResolver.Resolve("Unidentified device") == "device", "unknown device does not receive a guessed brand or keyboard icon");
            var items = new List<TaskbarItem>
            {
                new TaskbarItem
                {
                    Key = "codex",
                    Label = "Codex",
                    Value = "87%"
                }
            };
            int idle = TaskbarDrawing.Measure(items, true);
            check(TaskbarDrawing.Measure(items, true, true) == idle + 16, "only drag preview reserves a grip gutter");
            foreach (bool hover in new[]
            {
                false,
                true
            }

            )
                using (var bitmap = new Bitmap(idle, 28))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Transparent);
                    TaskbarDrawing.Paint(graphics, 28, idle, items, hover, false, true, true);
                    int highestAlpha = 0;
                    for (int y = 5; y < 23; y++)
                        for (int x = 0; x < 6; x++)
                            highestAlpha = Math.Max(highestAlpha, bitmap.GetPixel(x, y).A);
                    check(highestAlpha < 200, hover ? "hover does not restore the opaque grip dots" : "idle has no grip dots");
                }

            check(TaskbarDrawing.HitTest(items, idle, 9, true) == "codex", "logo remains a hover target after removing the grip gutter");
            var settings = Settings.FromJson("{}");
            check(!settings.TaskbarPositionLocked, "existing configurations remain draggable until explicitly locked");
            settings.TaskbarPositionLocked = true;
            check(Settings.FromJson(Json.Write(settings)).TaskbarPositionLocked, "position lock survives configuration serialization");
            using (var control = new TestControl())
            {
                var host = new LockedHost();
                using (var drag = new TaskbarDragController(control, host))
                {
                    int clicks = 0;
                    control.MouseClick += delegate
                    {
                        if (!drag.SuppressClick)
                            clicks++;
                    };
                    control.Down();
                    control.MovePointer();
                    control.Up();
                    control.ClickPointer();
                    check(!drag.IsActive && !drag.PreviewVisible && !control.Capture, "locked gestures do not capture the pointer or create a preview");
                    check(host.Commits == 0 && host.Captures == 0 && host.Layouts == 0, "locked gestures never move or save a taskbar position");
                    check(clicks == 1 && control.Cursor == Cursors.Hand, "position lock preserves click behavior and a normal pointer");
                }
            }

            return "PASS " + count + " icon and lock checks";
        }

        sealed class TestControl : Control
        {
            public void Down()
            {
                OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
            }

            public void MovePointer()
            {
                OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 120, 10, 0));
            }

            public void Up()
            {
                OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 120, 10, 0));
            }

            public void ClickPointer()
            {
                OnMouseClick(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
            }
        }

        sealed class LockedHost : ITaskbarDragHost
        {
            public int Commits, Captures, Layouts;
            public float DpiScale
            {
                get
                {
                    return 1;
                }
            }

            public Rectangle TaskbarBounds
            {
                get
                {
                    return new Rectangle(0, 1000, 1920, 40);
                }
            }

            public Rectangle WidgetBounds
            {
                get
                {
                    return new Rectangle(100, 1000, 300, 28);
                }
            }

            public bool CompactDisplay
            {
                get
                {
                    return true;
                }
            }

            public bool PositionLocked
            {
                get
                {
                    return true;
                }
            }

            public List<TaskbarItem> CaptureItems()
            {
                Captures++;
                return new List<TaskbarItem>();
            }

            public void UpdateDragLayout()
            {
                Layouts++;
            }

            public void CommitDragPosition()
            {
                Commits++;
            }
        }
    }
}
