using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CrayonTodo
{
    public class MainForm : Form
    {
        private readonly TodoModel model;
        private readonly FlowLayoutPanel tasks;
        private readonly TextBox input;
        private readonly Label hint, timerTitle, timerClock, opacityLabel;
        private readonly Panel timerCard;
        private readonly FlowLayoutPanel timerButtons;
        private readonly TrackBar opacitySlider;
        private readonly System.Windows.Forms.Timer clock;
        private AvatarForm avatar;
        private CaptionMinimizeForm captionMinimize;
        private CaptionMinimizeForm captionSettings;
        private readonly NotifyIcon trayIcon;
        private Panel header;
        private PictureBox heroArt;
        private Label subtitle;
        private Button addButton;
        private ThemePalette palette;
        private string appliedTheme;
        private bool exiting;
        private string lastTasks = null;
        private TimerPhase previousPhase;
        private string previousActiveId;
        private string breakCompletedId;
        private bool suppressCompletion;
        private bool syncing;
        private int saveTicks;
        private Color Ink = Color.FromArgb(66, 49, 42);
        private Color Cream = Color.FromArgb(255, 249, 226);
        private Color Red = Color.FromArgb(248, 105, 83);
        private Color Yellow = Color.FromArgb(255, 218, 93);
        private Color Blue = Color.FromArgb(152, 220, 246);
        private Color Green = Color.FromArgb(172, 225, 152);
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);
        [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }

        public MainForm(TodoModel model)
        {
            if (model == null) throw new ArgumentNullException("model");
            this.model = model;
            previousPhase = model.Phase; previousActiveId = model.ActiveId;
            Name = "MainForm"; AccessibleName = "To do 清单";
            Text = "To do 清单"; StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            TopMost = true; MinimumSize = new Size(410, 610); Size = new Size(450, 680);
            BackColor = Cream; ForeColor = Ink;
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Font;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill; layout.Padding = new Padding(12, 4, 12, 8);
            layout.ColumnCount = 1; layout.RowCount = 6;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 57));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 185));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            Controls.Add(layout);

            header = new Panel(); header.Dock = DockStyle.Fill; header.BackColor = Cream; header.Cursor = Cursors.SizeAll;
            header.Paint += PaintHeader; header.MouseDown += DragWindow;
            Label title = new Label(); title.Name = "Heading"; title.AutoSize = false; title.Text = "To do 清单"; title.Font = new Font(Font.FontFamily, 19F, FontStyle.Bold);
            title.ForeColor = Ink; title.BackColor = Color.Transparent; title.Location = new Point(5, 13); title.Size = new Size(255, 46); title.TextAlign = ContentAlignment.MiddleLeft; title.Cursor = Cursors.SizeAll; title.MouseDown += DragWindow;
            header.Controls.Add(title);
            subtitle = new Label(); subtitle.Name = "ThemeSubtitle"; subtitle.Text = "今天的事情，一件一件来。"; subtitle.Font = new Font(Font.FontFamily, 9F); subtitle.ForeColor = Color.FromArgb(131, 93, 76); subtitle.BackColor = Color.Transparent; subtitle.Location = new Point(8, 56); subtitle.Size = new Size(270, 23); subtitle.Cursor = Cursors.SizeAll; subtitle.MouseDown += DragWindow; header.Controls.Add(subtitle);
            try
            {
                System.IO.Stream sticker = typeof(MainForm).Assembly.GetManifestResourceStream("CrayonTodo.Shinchan");
                if (sticker == null) sticker = typeof(MainForm).Assembly.GetManifestResourceStream("CrayonTodo.TomatoSticker");
                using (sticker)
                {
                    if (sticker != null)
                    {
                        using (Image source = Image.FromStream(sticker))
                        {
                            PictureBox art = new PictureBox(); heroArt = art; art.Name = "ShinchanSticker"; art.AccessibleName = "蜡笔小新人物贴纸";
                            art.Image = new Bitmap(source); art.SizeMode = PictureBoxSizeMode.Zoom; art.Size = new Size(132, 108);
                            art.Location = new Point(Math.Max(0, header.Width - 132), 0); art.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                            art.BackColor = Color.Transparent; art.Cursor = Cursors.SizeAll; art.MouseDown += DragWindow;
                            art.Disposed += delegate { if (art.Image != null) { art.Image.Dispose(); art.Image = null; } };
                            header.Controls.Add(art); art.BringToFront();
                        }
                    }
                }
            } catch (Exception) { /* The functional UI works without the optional illustration. */ }
            layout.Controls.Add(header, 0, 0);

            TableLayoutPanel add = new TableLayoutPanel(); add.Dock = DockStyle.Fill; add.ColumnCount = 2; add.RowCount = 1; add.Margin = Padding.Empty;
            add.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); add.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 79)); add.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Panel inputFrame = new Panel(); inputFrame.Dock = DockStyle.Fill; inputFrame.Margin = new Padding(0, 4, 7, 5); inputFrame.Padding = new Padding(9, 7, 7, 6); inputFrame.BackColor = Color.White;
            inputFrame.Paint += PaintCard; RoundCorners(inputFrame, 12);
            input = new TextBox(); input.Name = "TaskInput"; input.AccessibleName = "新待办内容"; input.MaxLength = 120; input.BorderStyle = BorderStyle.None; input.Dock = DockStyle.Fill;
            input.Font = new Font(Font.FontFamily, 11F); input.ForeColor = Ink;
            input.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddTask(); } };
            inputFrame.Controls.Add(input); add.Controls.Add(inputFrame, 0, 0);
            addButton = MakeButton("添加", Yellow, "AddTaskButton"); addButton.Dock = DockStyle.Fill; addButton.Margin = new Padding(0, 4, 0, 5); addButton.Click += delegate { AddTask(); }; add.Controls.Add(addButton, 1, 0);
            layout.Controls.Add(add, 0, 1);

            hint = new Label(); hint.Name = "TaskHint"; hint.AccessibleName = "待办提示"; hint.Text = "今日清单 · 写下一件事，按 Enter 添加"; hint.Dock = DockStyle.Fill; hint.Font = new Font(Font.FontFamily, 9F); hint.ForeColor = Color.FromArgb(110, 82, 70); hint.TextAlign = ContentAlignment.MiddleLeft;
            layout.Controls.Add(hint, 0, 2);
            tasks = new FlowLayoutPanel(); tasks.Name = "TaskList"; tasks.AccessibleName = "待办事项列表"; tasks.Dock = DockStyle.Fill; tasks.FlowDirection = FlowDirection.TopDown; tasks.WrapContents = false; tasks.AutoScroll = true; tasks.BackColor = Cream; tasks.Margin = Padding.Empty;
            tasks.SizeChanged += delegate { ResizeRows(); }; layout.Controls.Add(tasks, 0, 3);

            timerCard = new Panel(); timerCard.Name = "TimerPanel"; timerCard.AccessibleName = "番茄钟面板"; timerCard.Dock = DockStyle.Fill; timerCard.BackColor = Color.FromArgb(255, 232, 187); timerCard.Padding = new Padding(10, 6, 10, 6); timerCard.Margin = new Padding(0, 4, 0, 4); timerCard.Paint += PaintCard; RoundCorners(timerCard, 15);
            TableLayoutPanel timerLayout = new TableLayoutPanel(); timerLayout.Dock = DockStyle.Fill; timerLayout.RowCount = 3; timerLayout.ColumnCount = 1;
            timerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); timerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66)); timerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            timerTitle = new Label(); timerTitle.Name = "TimerTitle"; timerTitle.AccessibleName = "当前计时事项"; timerTitle.Dock = DockStyle.Fill; timerTitle.TextAlign = ContentAlignment.MiddleLeft; timerTitle.AutoEllipsis = true; timerTitle.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            timerClock = new Label(); timerClock.Name = "TimerClock"; timerClock.AccessibleName = "番茄钟剩余时间"; timerClock.Dock = DockStyle.Fill; timerClock.TextAlign = ContentAlignment.MiddleLeft; timerClock.Font = new Font("Segoe UI", 25F, FontStyle.Bold); timerClock.ForeColor = Ink; timerClock.Margin = Padding.Empty;
            timerButtons = new FlowLayoutPanel(); timerButtons.Name = "TimerActions"; timerButtons.Dock = DockStyle.Fill; timerButtons.FlowDirection = FlowDirection.LeftToRight; timerButtons.WrapContents = false; timerButtons.Margin = Padding.Empty;
            timerLayout.Controls.Add(timerTitle, 0, 0); timerLayout.Controls.Add(timerClock, 0, 1); timerLayout.Controls.Add(timerButtons, 0, 2); timerCard.Controls.Add(timerLayout); layout.Controls.Add(timerCard, 0, 4);
            // Reserve space based on the rendered font, including on high-DPI displays.
            int titleHeight = Math.Max(32, timerTitle.GetPreferredSize(Size.Empty).Height + 6);
            int clockHeight = Math.Max(66, timerClock.GetPreferredSize(Size.Empty).Height + 14);
            timerLayout.RowStyles[0].Height = titleHeight; timerLayout.RowStyles[1].Height = clockHeight;
            layout.RowStyles[4].Height = Math.Max(185, titleHeight + clockHeight + 74);

            TableLayoutPanel opacityRow = new TableLayoutPanel(); opacityRow.Dock = DockStyle.Fill; opacityRow.ColumnCount = 3; opacityRow.RowCount = 1; opacityRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82)); opacityRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); opacityRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68)); opacityRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); opacityRow.Margin = Padding.Empty;
            Label opacityText = new Label(); opacityText.Text = "透明度"; opacityText.TextAlign = ContentAlignment.MiddleLeft; opacityText.Dock = DockStyle.Fill; opacityText.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold); opacityRow.Controls.Add(opacityText, 0, 0);
            opacitySlider = new TrackBar(); opacitySlider.Name = "OpacitySlider"; opacitySlider.AccessibleName = "调整窗口透明度"; opacitySlider.Dock = DockStyle.Fill; opacitySlider.Minimum = 40; opacitySlider.Maximum = 100; opacitySlider.TickStyle = TickStyle.None; opacitySlider.SmallChange = 5; opacitySlider.LargeChange = 10;
            opacitySlider.ValueChanged += delegate { if (!syncing) SafeAction(delegate { model.SetOpacity(opacitySlider.Value / 100.0); }); }; opacityRow.Controls.Add(opacitySlider, 1, 0);
            opacityLabel = new Label(); opacityLabel.Name = "OpacityValue"; opacityLabel.AccessibleName = "当前不透明度"; opacityLabel.TextAlign = ContentAlignment.MiddleRight; opacityLabel.Dock = DockStyle.Fill; opacityLabel.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold); opacityRow.Controls.Add(opacityLabel, 2, 0); layout.Controls.Add(opacityRow, 0, 5);

            ContextMenuStrip trayMenu = new ContextMenuStrip();
            ToolStripMenuItem openMenu = new ToolStripMenuItem("打开清单"); openMenu.Name = "TrayOpen"; openMenu.Click += delegate { RestoreFromAvatar(); }; trayMenu.Items.Add(openMenu);
            ToolStripMenuItem quitMenu = new ToolStripMenuItem("退出程序"); quitMenu.Name = "TrayExit"; quitMenu.Click += delegate { exiting = true; Close(); }; trayMenu.Items.Add(quitMenu);
            trayIcon = new NotifyIcon(); trayIcon.Text = "To do 清单"; trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application; trayIcon.ContextMenuStrip = trayMenu; trayIcon.Visible = true;
            trayIcon.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) RestoreFromAvatar(); };
            model.Changed += ModelChanged;
            FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (!exiting && e.CloseReason == CloseReason.UserClosing) {
                    e.Cancel = true;
                    if (avatar != null && !avatar.IsDisposed) avatar.Hide();
                    Hide();
                    return;
                }
                try { model.Save(); clock.Stop(); if (avatar != null) avatar.Dispose(); if (captionMinimize != null) captionMinimize.Dispose(); if (captionSettings != null) captionSettings.Dispose(); trayIcon.Visible = false; trayIcon.Dispose(); trayMenu.Dispose(); }
                catch (Exception ex) { exiting = false; e.Cancel = true; MessageBox.Show("保存失败，窗口会保持打开。请检查磁盘空间或目录权限，再试一次。\n" + ex.Message, "To do 清单", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            clock = new System.Windows.Forms.Timer(); clock.Interval = 1000;
            clock.Tick += delegate { try { model.Tick(); UpdateClock(); if ((model.Phase == TimerPhase.Focus || model.Phase == TimerPhase.Break) && !model.Paused) { if (++saveTicks >= 30) { model.Save(); saveTicks = 0; } } else saveTicks = 0; } catch (Exception ex) { clock.Stop(); if (!Visible) RestoreFromAvatar(); MessageBox.Show("计时或保存失败：" + ex.Message, "To do 清单", MessageBoxButtons.OK, MessageBoxIcon.Warning); } };
            Shown += delegate { UpdateCaptionMinimize(); };
            Move += delegate { UpdateCaptionMinimize(); };
            Resize += delegate { UpdateCaptionMinimize(); };
            VisibleChanged += delegate { UpdateCaptionMinimize(); };
            RefreshAll(); clock.Start();
        }

        // Tool windows have a compact title bar, but Windows does not display their
        // minimize button. Put a click-through mark beside the close button and
        // route that area and the system minimize command to the desktop avatar.
        private Rectangle CaptionMinimizeBounds()
        {
            int borderX = GetSystemMetrics(32) + GetSystemMetrics(92);
            int borderY = GetSystemMetrics(33) + GetSystemMetrics(92);
            int buttonWidth = GetSystemMetrics(52);
            int buttonHeight = GetSystemMetrics(53);
            return new Rectangle(Width - borderX - buttonWidth * 2, borderY, buttonWidth, buttonHeight);
        }
        private Rectangle CaptionSettingsBounds()
        {
            Rectangle r = CaptionMinimizeBounds(); r.Offset(-r.Width, 0); return r;
        }
        private bool IsCaptionSettingsPoint(IntPtr lParam)
        {
            NativeRect window; if (!GetWindowRect(Handle, out window)) return false;
            long point = lParam.ToInt64();
            int x = (short)(point & 0xFFFF) - window.Left;
            int y = (short)((point >> 16) & 0xFFFF) - window.Top;
            return CaptionSettingsBounds().Contains(x, y);
        }
        private bool IsCaptionMinimizePoint(IntPtr lParam)
        {
            NativeRect window;
            if (!IsHandleCreated || !GetWindowRect(Handle, out window)) return false;
            long point = lParam.ToInt64();
            int x = (short)(point & 0xFFFF) - window.Left;
            int y = (short)((point >> 16) & 0xFFFF) - window.Top;
            return CaptionMinimizeBounds().Contains(x, y);
        }
        private void UpdateCaptionMinimize()
        {
            if (!Visible || !IsHandleCreated || IsDisposed || WindowState != FormWindowState.Normal)
            {
                if (captionMinimize != null && !captionMinimize.IsDisposed) captionMinimize.Hide();
                if (captionSettings != null && !captionSettings.IsDisposed) captionSettings.Hide();
                return;
            }
            NativeRect window;
            if (!GetWindowRect(Handle, out window)) return;
            Rectangle r = CaptionMinimizeBounds();
            r.Offset(window.Left, window.Top);
            if (captionMinimize == null || captionMinimize.IsDisposed) captionMinimize = new CaptionMinimizeForm(false);
            captionMinimize.Bounds = r;
            captionMinimize.Opacity = Opacity;
            if (!captionMinimize.Visible) captionMinimize.Show(this);
            Rectangle settingsBounds = CaptionSettingsBounds(); settingsBounds.Offset(window.Left, window.Top);
            if (captionSettings == null || captionSettings.IsDisposed) captionSettings = new CaptionMinimizeForm(true);
            captionSettings.Bounds = settingsBounds; captionSettings.Opacity = Opacity;
            if (!captionSettings.Visible) captionSettings.Show(this);
        }
        private sealed class CaptionMinimizeForm : Form
        {
            private readonly bool settings;
            public CaptionMinimizeForm(bool settings)
            {
                this.settings = settings;
                FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
                BackColor = Color.Magenta; TransparencyKey = Color.Magenta; TopMost = true;
            }
            protected override bool ShowWithoutActivation { get { return true; } }
            protected override CreateParams CreateParams
            {
                get { CreateParams p = base.CreateParams; p.ExStyle |= 0x08000020; return p; }
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (settings)
                {
                    int cx = ClientSize.Width / 2, cy = ClientSize.Height / 2;
                    e.Graphics.SmoothingMode = SmoothingMode.None;
                    using (Pen pen = new Pen(SystemColors.WindowText, 2F))
                    {
                        e.Graphics.DrawEllipse(pen, cx - 5, cy - 5, 10, 10);
                        for (int i = 0; i < 8; i++)
                        {
                            double a = i * Math.PI / 4;
                            e.Graphics.DrawLine(pen, cx + (int)Math.Round(Math.Cos(a) * 5), cy + (int)Math.Round(Math.Sin(a) * 5), cx + (int)Math.Round(Math.Cos(a) * 8), cy + (int)Math.Round(Math.Sin(a) * 8));
                        }
                    }
                    return;
                }
                int lineWidth = Math.Max(8, ClientSize.Width / 3);
                int x = (ClientSize.Width - lineWidth) / 2, y = ClientSize.Height / 2;
                using (Pen pen = new Pen(SystemColors.WindowText, 2F)) e.Graphics.DrawLine(pen, x, y, x + lineWidth, y);
            }
        }
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84, WM_NCLBUTTONDOWN = 0xA1, WM_SYSCOMMAND = 0x112;
            const int HTCAPTION = 2, HTMINBUTTON = 8, SC_MINIMIZE = 0xF020;
            if (m.Msg == WM_SYSCOMMAND && (m.WParam.ToInt64() & 0xFFF0) == SC_MINIMIZE)
            {
                MinimizeToAvatar(); m.Result = IntPtr.Zero; return;
            }
            if (m.Msg == WM_NCLBUTTONDOWN && m.WParam.ToInt64() == HTCAPTION && IsCaptionSettingsPoint(m.LParam))
            {
                ShowSettings(); m.Result = IntPtr.Zero; return;
            }
            if (m.Msg == WM_NCLBUTTONDOWN && (m.WParam.ToInt64() == HTMINBUTTON ||
                (m.WParam.ToInt64() == HTCAPTION && IsCaptionMinimizePoint(m.LParam))))
            {
                MinimizeToAvatar(); m.Result = IntPtr.Zero; return;
            }
            if (m.Msg == WM_NCHITTEST && IsHandleCreated)
            {
                if (IsCaptionMinimizePoint(m.LParam)) { m.Result = (IntPtr)HTMINBUTTON; return; }
                if (IsCaptionSettingsPoint(m.LParam)) { m.Result = (IntPtr)HTCAPTION; return; }
            }
            base.WndProc(ref m);
        }

        private void MinimizeToAvatar()
        {
            if (avatar == null || avatar.IsDisposed)
            {
                avatar = new AvatarForm(model.ThemeKey);
                avatar.RestoreRequested += RestoreFromAvatar;
                avatar.PlaceNear(Bounds);
            }
            avatar.Opacity = Opacity;
            avatar.Show(); avatar.BringToFront();
            Hide();
        }
        private void RestoreFromAvatar()
        {
            if (avatar != null && !avatar.IsDisposed) avatar.Hide();
            Show(); WindowState = FormWindowState.Normal; TopMost = true; BringToFront(); Activate();
        }

        private void ShowSettings()
        {
            using (Form dialog = new Form())
            {
                dialog.Name = "SettingsDialog"; dialog.Text = "设置"; dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog; dialog.MaximizeBox = false; dialog.MinimizeBox = false; dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(360, 245); dialog.Font = Font; dialog.BackColor = Cream;
                Label label = new Label(); label.Text = "每个番茄钟的专注时间"; label.SetBounds(18, 16, 320, 27); dialog.Controls.Add(label);
                NumericUpDown duration = new NumericUpDown(); duration.Name = "FocusMinutes"; duration.Minimum = 25; duration.Maximum = 60; duration.Value = model.FocusMinutes; duration.SetBounds(20, 49, 100, 30); dialog.Controls.Add(duration);
                Label unit = new Label(); unit.Text = "分钟（25–60）"; unit.SetBounds(130, 50, 200, 30); dialog.Controls.Add(unit);
                Label note = new Label(); note.Text = "当前计时保持不变，下一个番茄开始生效。"; note.SetBounds(18, 88, 320, 25); note.Font = new Font(Font.FontFamily, 8F); dialog.Controls.Add(note);
                Label themeLabel = new Label(); themeLabel.Text = "主题"; themeLabel.SetBounds(18, 120, 55, 27); dialog.Controls.Add(themeLabel);
                string[] themeKeys = { "shinchan", "kitty", "kuromi", "spongebob", "doraemon" };
                ComboBox theme = new ComboBox(); theme.Name = "ThemeSelect"; theme.DropDownStyle = ComboBoxStyle.DropDownList;
                theme.Items.AddRange(new object[] { "蜡笔小新", "Hello Kitty", "库洛米", "海绵宝宝", "哆啦 A 梦" });
                theme.SetBounds(77, 118, 260, 32); theme.SelectedIndex = Math.Max(0, Array.IndexOf(themeKeys, model.ThemeKey)); dialog.Controls.Add(theme);
                Button save = MakeButton("保存", Yellow, "SaveSettings"); save.SetBounds(165, 191, 82, 40); dialog.Controls.Add(save);
                Button cancel = MakeButton("取消", Color.White, "CancelSettings"); cancel.SetBounds(255, 191, 82, 40); cancel.DialogResult = DialogResult.Cancel; dialog.Controls.Add(cancel);
                save.Click += delegate { SafeAction(delegate { if (model.SetFocusMinutes((int)duration.Value) && model.SetTheme(themeKeys[theme.SelectedIndex])) dialog.DialogResult = DialogResult.OK; }); };
                dialog.AcceptButton = save; dialog.CancelButton = cancel; dialog.ShowDialog(this);
            }
        }

        private void ModelChanged()
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(ModelChanged)); return; }
            bool focusFinished = !suppressCompletion && previousPhase == TimerPhase.Focus && model.Phase == TimerPhase.Decision;
            bool breakFinished = !suppressCompletion && previousPhase == TimerPhase.Break && model.Phase == TimerPhase.Idle;
            if (breakFinished) breakCompletedId = previousActiveId;
            else if (model.Phase != TimerPhase.Idle) breakCompletedId = null;
            previousPhase = model.Phase; previousActiveId = model.ActiveId;
            RefreshAll();
            if (focusFinished || breakFinished)
            {
                System.Media.SystemSounds.Exclamation.Play();
                if (!Visible || (avatar != null && avatar.Visible)) RestoreFromAvatar();
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                TopMost = true; BringToFront();
            }
        }
        private void SafeAction(Action action) { try { action(); } catch (Exception ex) { MessageBox.Show("操作没能完成：" + ex.Message, "To do 清单", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
        private void AddTask()
        {
            string title = input.Text.Trim(); if (title.Length == 0) { hint.Text = "先写一点内容，再点添加哦。"; input.Focus(); return; }
            SafeAction(delegate { if (model.Add(title)) { input.Clear(); hint.Text = "今日清单 · 写下一件事，按 Enter 添加"; input.Focus(); } else hint.Text = "暂时添加不了，请检查内容。"; });
        }
        private void EditTask(TodoItem item)
        {
            using (Form dialog = new Form())
            {
                dialog.Name = "EditTaskDialog"; dialog.AccessibleName = "编辑或删除待办"; dialog.Text = "编辑或删除待办"; dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog; dialog.MaximizeBox = false; dialog.MinimizeBox = false; dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(365, 150); dialog.BackColor = Cream; dialog.Font = Font;
                Label label = new Label(); label.Text = "这件事改成："; label.SetBounds(15, 12, 320, 25); dialog.Controls.Add(label);
                TextBox edit = new TextBox(); edit.Name = "EditTaskInput"; edit.AccessibleName = "待办新名称"; edit.MaxLength = 120; edit.Text = item.Title; edit.SetBounds(15, 42, 335, 32); dialog.Controls.Add(edit);
                Button delete = MakeButton("删除", Color.FromArgb(255, 218, 209), "TaskDelete_" + item.Id); delete.AccessibleName = "删除 " + item.Title; delete.SetBounds(15, 91, 86, 44); dialog.Controls.Add(delete);
                delete.Click += delegate { if (MessageBox.Show(dialog, "删除这件待办？\n" + item.Title, "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) SafeAction(delegate { suppressCompletion = true; try { model.Delete(item.Id); dialog.DialogResult = DialogResult.OK; } finally { suppressCompletion = false; } }); };
                Button save = MakeButton("保存", Yellow, "SaveTaskEdit"); save.SetBounds(169, 91, 86, 44); dialog.Controls.Add(save);
                Button cancel = MakeButton("取消", Color.White, "CancelTaskEdit"); cancel.SetBounds(264, 91, 86, 44); cancel.DialogResult = DialogResult.Cancel; dialog.Controls.Add(cancel);
                save.Click += delegate { if (String.IsNullOrWhiteSpace(edit.Text)) { MessageBox.Show(dialog, "先写一点内容吧。", "编辑待办", MessageBoxButtons.OK, MessageBoxIcon.Information); return; } SafeAction(delegate { if (model.Rename(item.Id, edit.Text)) dialog.DialogResult = DialogResult.OK; else MessageBox.Show(dialog, "没有保存成功，请检查内容。", "编辑待办", MessageBoxButtons.OK, MessageBoxIcon.Warning); }); };
                dialog.AcceptButton = save; dialog.CancelButton = cancel; dialog.Shown += delegate { edit.Focus(); edit.SelectAll(); }; dialog.ShowDialog(this);
            }
        }
        private void RefreshAll()
        {
            if (appliedTheme != model.ThemeKey) ApplyTheme();
            syncing = true;
            try { int value = Math.Max(opacitySlider.Minimum, Math.Min(opacitySlider.Maximum, (int)Math.Round(model.Opacity * 100))); opacitySlider.Value = value; Opacity = value / 100.0; if (captionMinimize != null && !captionMinimize.IsDisposed) captionMinimize.Opacity = Opacity; if (captionSettings != null && !captionSettings.IsDisposed) captionSettings.Opacity = Opacity; opacityLabel.Text = value + "%"; }
            finally { syncing = false; }
            string signature = model.ThemeKey + "|" + model.FocusMinutes + "|" + string.Join("|", model.Items.Select(x => x.Id + "~" + x.Title + "~" + x.Done + "~" + x.CompletedPomodoros).ToArray());
            if (signature != lastTasks) { lastTasks = signature; RebuildTasks(); }
            BuildTimerActions(); UpdateClock();
        }
        private void ApplyTheme()
        {
            palette = ThemePalette.For(model.ThemeKey);
            Ink = palette.Ink; Cream = palette.Background;
            if (model.ThemeKey == "shinchan")
            {
                Red = Color.FromArgb(248, 105, 83); Yellow = Color.FromArgb(255, 218, 93);
                Blue = Color.FromArgb(152, 220, 246); Green = Color.FromArgb(172, 225, 152);
            }
            else { Red = palette.Accent; Yellow = palette.Accent; Blue = palette.Soft; Green = palette.Soft; }
            BackColor = Cream; ForeColor = Ink; header.BackColor = Cream; tasks.BackColor = Cream;
            Control[] heading = Controls.Find("Heading", true); if (heading.Length > 0) heading[0].ForeColor = Ink;
            hint.ForeColor = palette.Secondary; subtitle.ForeColor = palette.Secondary; subtitle.Text = palette.Phrase;
            timerCard.BackColor = palette.Timer; timerTitle.ForeColor = Ink; timerClock.ForeColor = Ink;
            input.ForeColor = Ink; addButton.BackColor = Yellow; addButton.ForeColor = Ink;
            if (heroArt != null)
            {
                Image next = ThemePalette.LoadImage(palette.Resource);
                if (next != null) { Image old = heroArt.Image; heroArt.Image = next; if (old != null) old.Dispose(); }
                heroArt.AccessibleName = palette.Name + "人物贴纸";
            }
            if (avatar != null && !avatar.IsDisposed) avatar.SetTheme(model.ThemeKey);
            appliedTheme = model.ThemeKey; lastTasks = null;
            header.Invalidate(); timerCard.Invalidate(); addButton.Invalidate();
        }
        private void RebuildTasks()
        {
            tasks.SuspendLayout(); int scroll = -tasks.AutoScrollPosition.Y;
            foreach (Control c in tasks.Controls.Cast<Control>().ToArray()) { tasks.Controls.Remove(c); c.Dispose(); }
            if (model.Items.Count == 0)
            {
                Label empty = new Label(); empty.Name = "EmptyTasks"; empty.Text = "今天想做什么？\n先从一件小事开始吧 ✦"; empty.ForeColor = Color.FromArgb(125, 95, 77); empty.TextAlign = ContentAlignment.MiddleCenter; empty.Size = new Size(Math.Max(250, tasks.ClientSize.Width - 10), 90); tasks.Controls.Add(empty);
            }
            int rowIndex = 0;
            foreach (TodoItem task in model.Items)
            {
                TodoItem item = task;
                Color[] pastel = model.ThemeKey == "shinchan" ? new[] { Color.FromArgb(255, 229, 225), Color.FromArgb(223, 240, 251), Color.FromArgb(255, 243, 199) } : new[] { palette.Card, palette.Card };
                Color cardColor = item.Done ? palette.Soft : pastel[rowIndex % pastel.Length];
                rowIndex++;
                Panel row = new Panel(); row.Name = "TaskRow_" + item.Id; row.AccessibleName = "待办：" + item.Title; row.Height = 56; row.Margin = new Padding(0, 1, 0, 2); row.Padding = new Padding(2); row.BackColor = cardColor; row.Paint += PaintCard; RoundCorners(row, 12);
                TableLayoutPanel cells = new TableLayoutPanel(); cells.Dock = DockStyle.Fill; cells.Padding = new Padding(3, 3, 3, 3); cells.Margin = Padding.Empty; cells.ColumnCount = 4; cells.RowCount = 1; cells.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                cells.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42)); cells.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); cells.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56)); cells.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 71));
                CheckBox done = new CheckBox(); done.Name = "TaskDone_" + item.Id; done.AccessibleName = (item.Done ? "取消完成 " : "完成 ") + item.Title; done.Checked = item.Done; done.Appearance = Appearance.Button; done.Text = item.Done ? "✓" : "○"; done.TextAlign = ContentAlignment.MiddleCenter; done.Font = new Font(Font.FontFamily, 15F, FontStyle.Bold); done.Dock = DockStyle.Fill; done.Margin = Padding.Empty; done.FlatStyle = FlatStyle.Flat; done.FlatAppearance.BorderSize = 0; done.BackColor = Color.Transparent;
                done.Click += delegate { SafeAction(delegate { model.Toggle(item.Id); }); }; cells.Controls.Add(done, 0, 0);
                Label text = new Label(); text.Name = "TaskTitle_" + item.Id; text.AccessibleName = "事项 " + item.Title; text.AutoSize = false; text.TextAlign = ContentAlignment.MiddleLeft; text.ForeColor = item.Done ? palette.Secondary : Ink;
                Panel titleBox = new Panel(); titleBox.Dock = DockStyle.Fill; titleBox.Margin = new Padding(3, 0, 3, 0); titleBox.Controls.Add(text); cells.Controls.Add(titleBox, 1, 0);
                string fullTitle = item.Title + (item.CompletedPomodoros > 0 ? "  番茄×" + item.CompletedPomodoros : "");
                titleBox.SizeChanged += delegate { text.Bounds = titleBox.ClientRectangle; FitTwoLines(text, fullTitle); };
                text.Bounds = titleBox.ClientRectangle; FitTwoLines(text, fullTitle);
                ToolTip tip = new ToolTip(); tip.SetToolTip(text, fullTitle);
                Button edit = MakeButton("编辑", model.ThemeKey == "shinchan" ? Color.FromArgb(225, 239, 255) : palette.Soft, "TaskEdit_" + item.Id); edit.AccessibleName = "编辑或删除 " + item.Title; edit.Dock = DockStyle.Fill; edit.Margin = new Padding(0, 2, 2, 2); edit.Font = new Font(Font.FontFamily, 8F, FontStyle.Bold);
                edit.Click += delegate { EditTask(item); }; cells.Controls.Add(edit, 2, 0);
                Button start = MakeButton("番茄", Red, "TaskStart_" + item.Id); start.AccessibleName = "开始 " + item.Title + " 的" + model.FocusMinutes + "分钟番茄钟"; start.Dock = DockStyle.Fill; start.Margin = new Padding(0, 2, 2, 2); start.Font = new Font(Font.FontFamily, 8F, FontStyle.Bold); start.Enabled = !item.Done && model.Phase == TimerPhase.Idle;
                start.Click += delegate { SafeAction(delegate { if (!model.Start(item.Id)) MessageBox.Show("请先结束当前番茄钟，再开始这一项。", "To do 清单", MessageBoxButtons.OK, MessageBoxIcon.Information); }); }; cells.Controls.Add(start, 3, 0);
                row.Controls.Add(cells); tasks.Controls.Add(row);
            }
            ResizeRows(); tasks.AutoScrollPosition = new Point(0, scroll); tasks.ResumeLayout();
        }
        private static void FitTwoLines(Label label, string value)
        {
            int width = Math.Max(1, label.ClientSize.Width - 3);
            string[] lines = { "", "" }; int line = 0; bool clipped = false;
            System.Globalization.TextElementEnumerator elements = System.Globalization.StringInfo.GetTextElementEnumerator(value);
            while (elements.MoveNext())
            {
                string next = elements.GetTextElement();
                if (TextRenderer.MeasureText(lines[line] + next, label.Font, new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width > width)
                {
                    if (line == 1) { clipped = true; break; }
                    line = 1;
                }
                lines[line] += next;
            }
            if (clipped)
            {
                while (lines[1].Length > 0 && TextRenderer.MeasureText(lines[1] + "…", label.Font, new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width > width)
                {
                    int[] indices = System.Globalization.StringInfo.ParseCombiningCharacters(lines[1]);
                    lines[1] = lines[1].Substring(0, indices[indices.Length - 1]);
                }
                lines[1] += "…";
            }
            label.Text = lines[0] + (lines[1].Length > 0 ? "\n" + lines[1] : "");
        }
        private void ResizeRows() { foreach (Control c in tasks.Controls) c.Width = Math.Max(260, tasks.ClientSize.Width - 8); }
        private void BuildTimerActions()
        {
            timerButtons.SuspendLayout(); foreach (Control c in timerButtons.Controls.Cast<Control>().ToArray()) { timerButtons.Controls.Remove(c); c.Dispose(); }
            if (model.Phase == TimerPhase.Decision)
            {
                AddTimerButton("休息5分钟", Green, "ChooseBreak", delegate { model.ChooseBreak(); }, 105);
                AddTimerButton("再来" + model.FocusMinutes + "分", Yellow, "ChooseNextFocus", delegate { model.ChooseNextFocus(); }, 100);
                AddTimerButton("结束", Color.White, "FinishTimer", delegate { EndTimer(); }, 64);
            }
            else if (model.Phase == TimerPhase.Idle && breakCompletedId != null && model.Items.Exists(x => x.Id == breakCompletedId && !x.Done))
            {
                string resumeId = breakCompletedId;
                AddTimerButton("继续" + model.FocusMinutes + "分钟", Yellow, "RestartAfterBreak", delegate { model.Start(resumeId); }, 120);
                AddTimerButton("结束", Color.White, "DismissBreak", delegate { breakCompletedId = null; RefreshAll(); }, 64);
            }
            else if (model.Phase == TimerPhase.Focus || model.Phase == TimerPhase.Break)
            {
                AddTimerButton(model.Paused ? "继续" : "暂停", Blue, "PauseResume", delegate { model.PauseOrResume(); }, 90);
                AddTimerButton("结束计时", Color.White, "FinishTimer", delegate { EndTimer(); }, 105);
            }
            timerButtons.ResumeLayout();
            // Starting a timer changes which task buttons are enabled, without resetting the text input.
            foreach (Control row in tasks.Controls) foreach (Control child in row.Controls) foreach (Control cell in child.Controls) if (cell.Name.StartsWith("TaskStart_")) { TodoItem item = model.Items.Find(x => "TaskStart_" + x.Id == cell.Name); cell.Enabled = item != null && !item.Done && model.Phase == TimerPhase.Idle; }
        }
        private void EndTimer() { suppressCompletion = true; try { model.Finish(); } finally { suppressCompletion = false; } }
        private void AddTimerButton(string text, Color color, string name, Action action, int width)
        {
            Button b = MakeButton(text, color, name); b.Size = new Size(width, 44); b.Margin = new Padding(0, 2, 5, 0); b.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold); b.Click += delegate { SafeAction(action); }; timerButtons.Controls.Add(b);
        }
        private void UpdateClock()
        {
            TodoItem item = model.Items.Find(x => x.Id == model.ActiveId); string name = item == null ? "这件事" : item.Title;
            if (model.Phase == TimerPhase.Idle) { timerTitle.Text = breakCompletedId == null ? "选一件待办，开始番茄钟" : "休息结束啦，准备好再开始？"; timerClock.Text = model.FocusMinutes.ToString("00") + ":00"; }
            else if (model.Phase == TimerPhase.Decision) { timerTitle.Text = "一个番茄完成啦！接下来？"; timerClock.Text = "00:00"; }
            else
            {
                timerTitle.Text = (model.Paused ? (model.Phase == TimerPhase.Break ? "休息暂停  ·  " : "已暂停  ·  ") : (model.Phase == TimerPhase.Break ? "休息一下  ·  " : "正在专注  ·  ")) + name;
                TimeSpan remain = model.Remaining(); int seconds = Math.Max(0, (int)Math.Ceiling(remain.TotalSeconds)); timerClock.Text = string.Format("{0:00}:{1:00}", seconds / 60, seconds % 60);
            }
        }
        private sealed class CrayonButton : Button
        {
            public CrayonButton() { DoubleBuffered = true; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false; }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
                Color fill = Enabled ? BackColor : Color.FromArgb(225, 222, 213);
                using (GraphicsPath path = RoundedPath(bounds, 10))
                using (Brush brush = new SolidBrush(fill))
                using (Pen pen = new Pen(Enabled ? ForeColor : Color.FromArgb(161, 151, 143), 1.5F))
                {
                    e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path);
                }
                TextRenderer.DrawText(e.Graphics, Text, Font, bounds, Enabled ? ForeColor : Color.FromArgb(145, 137, 129), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
        }
        private Button MakeButton(string text, Color color, string name)
        {
            Button b = new CrayonButton(); b.Name = name; b.AccessibleName = text; b.Text = text; b.BackColor = color; b.ForeColor = Ink; b.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold); b.Cursor = Cursors.Hand; return b;
        }
        private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            GraphicsPath path = new GraphicsPath();
            if (diameter <= 1) { path.AddRectangle(bounds); return path; }
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure(); return path;
        }
        private static void RoundCorners(Control control, int radius)
        {
            Action resize = delegate
            {
                if (control.Width <= 0 || control.Height <= 0) return;
                using (GraphicsPath path = RoundedPath(new Rectangle(0, 0, control.Width, control.Height), radius))
                {
                    Region old = control.Region; control.Region = new Region(path); if (old != null) old.Dispose();
                }
                // The old rounded border may otherwise be retained inside an enlarged control.
                control.Invalidate(true);
            };
            control.SizeChanged += delegate { resize(); }; resize();
        }
        private void PaintCard(object sender, PaintEventArgs e)
        {
            Control c = (Control)sender; e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = RoundedPath(new Rectangle(1, 1, Math.Max(1, c.Width - 3), Math.Max(1, c.Height - 3)), 12))
            using (Pen pen = new Pen(palette == null ? Ink : palette.Border, 1.5F)) e.Graphics.DrawPath(pen, path);
        }
        private void PaintHeader(object sender, PaintEventArgs e)
        {
            Control c = (Control)sender;
            using (Pen p = new Pen(Red, 3)) { e.Graphics.DrawLine(p, 5, c.Height - 5, c.Width - 9, c.Height - 8); }
            using (Pen p = new Pen(palette == null || model.ThemeKey == "shinchan" ? Color.FromArgb(239, 173, 47) : palette.Soft, 2)) { e.Graphics.DrawLine(p, 9, c.Height - 2, Math.Min(c.Width - 10, 125), c.Height - 4); }
        }
        private void DragWindow(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 2, 0); } }
    }
}
