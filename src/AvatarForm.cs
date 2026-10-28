using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CrayonTodo
{
    // Per-pixel alpha keeps the desktop visible around the character, including its anti-aliased edges.
    public sealed class AvatarForm : Form
    {
        private readonly Timer animation;
        private Image portrait;
        private RectangleF portraitRegion;
        private string portraitTheme;
        private int animationFrame;
        private double avatarOpacity = 1;
        private Point mouseStart, windowStart;
        private bool mouseDown, dragging;
        public event Action RestoreRequested;

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int Width, Height; public SIZE(int w, int h) { Width = w; Height = h; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);

        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x00080000; return cp; }
        }

        // Form.Opacity calls SetLayeredWindowAttributes, which cannot be mixed with
        // UpdateLayeredWindow. Use the per-frame alpha instead.
        public new double Opacity
        {
            get { return avatarOpacity; }
            set { avatarOpacity = Math.Max(0, Math.Min(1, value)); if (Visible) DrawFrame(); }
        }

        public AvatarForm() : this("shinchan") { }
        public AvatarForm(string themeKey)
        {
            Name = "FloatingAvatar"; AccessibleName = "蜡笔小新悬浮头像，点击展开，拖动移动";
            Text = "蜡笔小新悬浮头像"; StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            AutoScaleMode = AutoScaleMode.None; MinimumSize = new Size(1, 1); Cursor = Cursors.Hand;
            float scale; using (Graphics screen = Graphics.FromHwnd(IntPtr.Zero)) scale = screen.DpiX / 96F;
            Size = new Size((int)Math.Round(60 * scale), (int)Math.Round(53.333 * scale));
            SetTheme(themeKey);
            animation = new Timer(); animation.Interval = 65;
            animation.Tick += delegate { animationFrame = (animationFrame + 1) % 10000; DrawFrame(); };
        }

        public void SetTheme(string themeKey)
        {
            if (portraitTheme == themeKey && portrait != null) return;
            ThemePalette palette = ThemePalette.For(themeKey);
            string resource = themeKey == "shinchan" ? "CrayonTodo.ShinchanAvatar" : palette.Resource;
            using (Image source = ThemePalette.LoadImage(resource))
            {
                if (source == null) return;
                Image next = themeKey == "shinchan" ? MakeHead(source) : new Bitmap(source);
                Image old = portrait; portrait = next; if (old != null) old.Dispose();
                if (themeKey == "shinchan") portraitRegion = new RectangleF(0, 60, 1328, 990);
                else if (themeKey == "kitty") portraitRegion = new RectangleF(source.Width * 0.06F, source.Height * 0.02F, source.Width * 0.88F, source.Height * 0.62F);
                else if (themeKey == "kuromi") portraitRegion = new RectangleF(source.Width * 0.12F, 0, source.Width * 0.78F, source.Height * 0.72F);
                else if (themeKey == "doraemon") portraitRegion = new RectangleF(source.Width * 0.16F, source.Height * 0.04F, source.Width * 0.68F, source.Height * 0.56F);
                else portraitRegion = new RectangleF(source.Width * 0.14F, source.Height * 0.04F, source.Width * 0.72F, source.Height * 0.51F);
                portraitTheme = themeKey;
                AccessibleName = palette.Name + "悬浮头像，点击展开，拖动移动";
                Text = palette.Name + "悬浮头像";
                if (Visible) DrawFrame();
            }
        }
        private static Image MakeHead(Image source)
        {
            Bitmap head = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(head))
            using (GraphicsPath clip = new GraphicsPath())
            {
                // Clip along the face's lower outline, preserving the artist's line while leaving the shirt out.
                float w = source.Width / 1328F, h = source.Height / 1184F;
                clip.AddLine(0, 0, 1328 * w, 0);
                clip.AddLine(1328 * w, 0, 1328 * w, 930 * h);
                clip.AddBezier(1328 * w, 930 * h, 1295 * w, 1010 * h, 1190 * w, 1030 * h, 1050 * w, 970 * h);
                clip.AddLines(new PointF[] { new PointF(1050 * w, 970 * h), new PointF(1000 * w, 985 * h), new PointF(950 * w, 995 * h), new PointF(900 * w, 1007 * h), new PointF(850 * w, 1014 * h), new PointF(800 * w, 1018 * h), new PointF(750 * w, 1022 * h), new PointF(700 * w, 1024 * h), new PointF(650 * w, 1024 * h), new PointF(600 * w, 1021 * h), new PointF(550 * w, 1018 * h), new PointF(500 * w, 1013 * h), new PointF(450 * w, 1009 * h), new PointF(400 * w, 998 * h), new PointF(350 * w, 987 * h), new PointF(330 * w, 978 * h) });
                clip.AddBezier(330 * w, 978 * h, 200 * w, 950 * h, 90 * w, 880 * h, 0, 800 * h);
                clip.CloseFigure();
                g.SetClip(clip); g.DrawImageUnscaled(source, 0, 0);
            }
            return head;
        }

        public void PlaceNear(Rectangle window)
        {
            Rectangle area = Screen.FromRectangle(window).WorkingArea;
            Location = KeepVisible(new Point(window.Right - Width, window.Top), area);
        }
        private Point KeepVisible(Point position, Rectangle area)
        {
            return new Point(Math.Max(area.Left, Math.Min(position.X, Math.Max(area.Left, area.Right - Width))),
                             Math.Max(area.Top, Math.Min(position.Y, Math.Max(area.Top, area.Bottom - Height))));
        }
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (animation != null) { if (Visible) { DrawFrame(); animation.Start(); } else animation.Stop(); }
        }
        private void DrawFrame()
        {
            if (!IsHandleCreated || !Visible || portrait == null) return;
            using (Bitmap frame = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(frame))
                {
                    g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.ScaleTransform(Width / 90F, Height / 80F);
                    g.TranslateTransform(45, 40);
                    g.RotateTransform((float)(Math.Sin(animationFrame * 0.16) * 6));
                    g.DrawImage(portrait, new RectangleF(-42, -33, 84, 66), portraitRegion, GraphicsUnit.Pixel);
                }
                IntPtr screen = GetDC(IntPtr.Zero), memory = CreateCompatibleDC(screen), bitmap = frame.GetHbitmap(Color.FromArgb(0));
                IntPtr previous = SelectObject(memory, bitmap);
                try
                {
                    POINT dst = new POINT(Left, Top), src = new POINT(0, 0); SIZE size = new SIZE(Width, Height);
                    BLENDFUNCTION blend = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = (byte)Math.Round(avatarOpacity * 255), AlphaFormat = 1 };
                    UpdateLayeredWindow(Handle, screen, ref dst, ref size, memory, ref src, 0, ref blend, 2);
                }
                finally { SelectObject(memory, previous); DeleteObject(bitmap); DeleteDC(memory); ReleaseDC(IntPtr.Zero, screen); }
            }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            mouseDown = true; dragging = false; mouseStart = PointToScreen(e.Location); windowStart = Location; Capture = true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!mouseDown) return;
            Point current = PointToScreen(e.Location);
            int dx = current.X - mouseStart.X, dy = current.Y - mouseStart.Y;
            Size threshold = SystemInformation.DragSize;
            if (Math.Abs(dx) >= threshold.Width / 2 || Math.Abs(dy) >= threshold.Height / 2) dragging = true;
            if (dragging) Location = KeepVisible(new Point(windowStart.X + dx, windowStart.Y + dy), Screen.FromPoint(current).WorkingArea);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !mouseDown) return;
            bool click = !dragging; mouseDown = false; Capture = false;
            if (click && RestoreRequested != null) RestoreRequested();
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if ((e.CloseReason == CloseReason.UserClosing || e.CloseReason == CloseReason.None || e.CloseReason == CloseReason.TaskManagerClosing) && RestoreRequested != null) { e.Cancel = true; RestoreRequested(); }
            base.OnFormClosing(e);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { if (animation != null) animation.Dispose(); if (portrait != null) portrait.Dispose(); }
            base.Dispose(disposing);
        }
    }
}







