using System;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;

namespace CrayonTodo
{
    public static class Program
    {
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        [STAThread]
        public static void Main()
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                bool created;
                using (Mutex instance = new Mutex(true, @"Local\CrayonTodo-SingleInstance-v1", out created))
                {
                    if (!created)
                    {
                        MessageBox.Show("To do 清单已经打开，可在桌面头像或右下角通知区域找到它。", "To do 清单", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    try
                    {
                        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrayonTodo");
                        TodoModel model = new TodoModel(dir);
                        model.Load();
                        Application.Run(new MainForm(model));
                    }
                    finally { instance.ReleaseMutex(); }
                }
            }
            catch (Exception ex) { MessageBox.Show("无法打开 To do 清单：\n" + ex.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
