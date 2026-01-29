using NoFences.Model;
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using NoFences.Win32;

namespace NoFences
{
    static class Program
    {
        private static NotifyIcon trayIcon;
        private static ContextMenuStrip trayMenu;
        private static FenceManagerDialog managerDialog;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Global exception handlers
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            //allows the context menu to be in dark mode
            //inherits from the system settings
            WindowUtil.SetPreferredAppMode(1);

            using (var mutex = new Mutex(true, "No_fences", out var createdNew))
            {
                if (createdNew)
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    // Create tray icon
                    CreateTrayIcon();

                    // Load fences
                    FenceManager.Instance.LoadFences();
                    if (FenceManager.Instance.OpenFences.Count == 0)
                        FenceManager.Instance.CreateFence("First fence");

                    // Load notes
                    NoteManager.Instance.LoadNotes();

                    Application.ApplicationExit += (s, e) => 
                    {
                        if (trayIcon != null)
                        {
                            trayIcon.Visible = false;
                            trayIcon.Dispose();
                        }
                    };

                    Application.Run();
                }
            }
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Thread exception: {e.Exception.Message}\n{e.Exception.StackTrace}");
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            System.Diagnostics.Debug.WriteLine($"Unhandled exception: {ex?.Message}\n{ex?.StackTrace}");
        }

        private static void CreateTrayIcon()
        {
            try
            {
                trayMenu = new ContextMenuStrip();
                trayMenu.Items.Add("围栏管理器", null, TrayMenu_ShowManager);
                trayMenu.Items.Add("新建围栏", null, TrayMenu_CreateFence);
                trayMenu.Items.Add("新建便签", null, TrayMenu_CreateNote);
                trayMenu.Items.Add(new ToolStripSeparator());
                trayMenu.Items.Add("退出", null, TrayMenu_Exit);

                trayIcon = new NotifyIcon
                {
                    Text = "NoFences",
                    Icon = SystemIcons.Application,
                    ContextMenuStrip = trayMenu,
                    Visible = true
                };

                trayIcon.DoubleClick += TrayIcon_DoubleClick;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateTrayIcon error: {ex.Message}");
            }
        }

        private static void TrayIcon_DoubleClick(object sender, EventArgs e)
        {
            ShowManager();
        }

        private static void TrayMenu_ShowManager(object sender, EventArgs e)
        {
            ShowManager();
        }

        private static void TrayMenu_CreateFence(object sender, EventArgs e)
        {
            try
            {
                FenceManager.Instance.CreateFence("New fence");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateFence error: {ex.Message}");
            }
        }

        private static void TrayMenu_CreateNote(object sender, EventArgs e)
        {
            try
            {
                NoteManager.Instance.CreateNote();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateNote error: {ex.Message}");
            }
        }

        private static void TrayMenu_Exit(object sender, EventArgs e)
        {
            ExitApplication();
        }

        private static void ShowManager()
        {
            try
            {
                // If manager dialog exists and is not disposed, just activate it
                if (managerDialog != null && !managerDialog.IsDisposed)
                {
                    managerDialog.Activate();
                    managerDialog.BringToFront();
                    return;
                }

                // Create new manager dialog
                managerDialog = new FenceManagerDialog();
                managerDialog.FormClosed += (s, e) => managerDialog = null;
                managerDialog.Show();
                managerDialog.Activate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowManager error: {ex.Message}");
                managerDialog = null;
            }
        }

        private static void ExitApplication()
        {
            try
            {
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                }
                Application.Exit();
            }
            catch { }
        }
    }
}
