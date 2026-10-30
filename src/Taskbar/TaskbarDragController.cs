using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace TokenDock
{
    interface ITaskbarDragHost
    {
        float DpiScale { get; }

        Rectangle TaskbarBounds { get; }

        Rectangle WidgetBounds { get; }

        bool CompactDisplay { get; }

        bool PositionLocked { get; }

        List<TaskbarItem> CaptureItems();
        void UpdateDragLayout();
        void CommitDragPosition();
    }

    class TaskbarDragController : IDisposable
    {
        readonly Control owner;
        readonly ITaskbarDragHost host;
        readonly Timer timer = new Timer
        {
            Interval = 16
        };
        bool tracking, finishing;
        Point origin, grab, lastPoint;
        int originalLeft;
        long lastFrame;
        DragPreview preview;
        public bool IsActive { get; private set; }
        public bool SuppressClick { get; private set; }
        public int RequestedX { get; private set; }
        public List<TaskbarItem> Items { get; private set; }

        public bool PreviewVisible
        {
            get
            {
                return preview != null && preview.Visible;
            }
        }

        public int PreviewWidth
        {
            get
            {
                return Items == null ? 0 : TaskbarDrawing.Measure(Items, host.CompactDisplay, true);
            }
        }

        public TaskbarDragController(Control view, ITaskbarDragHost callbacks)
        {
            owner = view;
            host = callbacks;
            owner.MouseDown += MouseDown;
            owner.MouseMove += MouseMove;
            owner.MouseUp += MouseUp;
            owner.MouseCaptureChanged += CaptureChanged;
            timer.Tick += Tick;
        }

        void MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            SuppressClick = false;
            if (host.PositionLocked)
            {
                ApplyPositionLock();
                return;
            }

            tracking = true;
            origin = Cursor.Position;
            grab = e.Location;
            originalLeft = host.WidgetBounds.Left;
            owner.Capture = true;
        }

        void MouseMove(object sender, MouseEventArgs e)
        {
            if (host.PositionLocked)
            {
                ApplyPositionLock();
                owner.Cursor = Cursors.Hand;
                return;
            }

            if (!tracking)
            {
                owner.Cursor = Cursors.Hand;
                return;
            }

            Point current = Cursor.Position;
            if (!IsActive && Math.Abs(current.X - origin.X) < SystemInformation.DragSize.Width / 2 && Math.Abs(current.Y - origin.Y) < SystemInformation.DragSize.Height / 2)
                return;
            if (!IsActive)
            {
                Items = host.CaptureItems();
                IsActive = true;
                SuppressClick = true;
                owner.Cursor = Cursors.SizeAll;
                lastFrame = 0;
                lastPoint = new Point(int.MinValue, int.MinValue);
                float scale = host.DpiScale;
                int logicalHeight = (int)(owner.Height / scale);
                preview = new DragPreview((int)Math.Ceiling(PreviewWidth * scale), owner.Height, scale, delegate (Graphics graphics)
                {
                    TaskbarDrawing.Paint(graphics, logicalHeight, PreviewWidth, Items, false, false, true, host.CompactDisplay, true);
                });
                preview.Location = PreviewLocation(current);
                preview.Show();
                NativeMethods.PopupFrame(preview);
                timer.Start();
            }

            UpdateFrame(false);
        }

        Point PreviewLocation(Point pointer)
        {
            int padding = (int)(4 * host.DpiScale);
            return new Point(pointer.X - grab.X - padding, pointer.Y - grab.Y - padding);
        }

        void MouseUp(object sender, MouseEventArgs e)
        {
            Finish(IsActive && TaskbarLayout.CanDrop(host.TaskbarBounds, Cursor.Position, (int)(48 * host.DpiScale)));
        }

        void CaptureChanged(object sender, EventArgs e)
        {
            if (tracking && !owner.Capture && !finishing)
                Finish(false);
        }

        void Tick(object sender, EventArgs e)
        {
            if (tracking && (NativeMethods.GetAsyncKeyState(0x1B) & 0x8000) != 0)
                Finish(false);
            else
                UpdateFrame(false);
        }

        void UpdateFrame(bool force)
        {
            if (host.PositionLocked)
            {
                ApplyPositionLock();
                return;
            }

            if (!tracking || !IsActive || preview == null)
                return;
            long now = Stopwatch.GetTimestamp();
            if (!force && now - lastFrame < Stopwatch.Frequency / 60)
                return;
            Point current = Cursor.Position;
            if (!force && current == lastPoint)
                return;
            lastFrame = now;
            lastPoint = current;
            RequestedX = originalLeft + current.X - origin.X;
            preview.Location = PreviewLocation(current);
            host.UpdateDragLayout();
        }

        void Finish(bool commit)
        {
            if (!tracking || finishing)
                return;
            finishing = true;
            try
            {
                bool save = commit && IsActive && !host.PositionLocked;
                if (save)
                {
                    UpdateFrame(true);
                    host.CommitDragPosition();
                }

                tracking = false;
                IsActive = false;
                timer.Stop();
                owner.Capture = false;
                owner.Cursor = Cursors.Hand;
                Items = null;
                if (preview != null)
                {
                    preview.Close();
                    preview.Dispose();
                    preview = null;
                }

                host.UpdateDragLayout();
            }
            finally
            {
                finishing = false;
            }
        }

        public void ApplyPositionLock()
        {
            if (host.PositionLocked && tracking)
                Finish(false);
        }

        public void Dispose()
        {
            owner.MouseDown -= MouseDown;
            owner.MouseMove -= MouseMove;
            owner.MouseUp -= MouseUp;
            owner.MouseCaptureChanged -= CaptureChanged;
            timer.Stop();
            timer.Dispose();
            if (preview != null)
                preview.Dispose();
        }
    }
}
