using NoFences.Model;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NoFences
{
    public partial class FenceManagerDialog : Form
    {
        private ListView listView;
        private Button btnLockAll;
        private Button btnUnlockAll;
        private Button btnClose;
        private CheckBox chkAutoStart;
        private CheckBox chkAutoRefresh;
        private Label lblRefreshInterval;
        private ComboBox cboRefreshInterval;
        private EventHandler fencesChangedHandler;

        private const string AppName = "NoFences";
        private const string RegistryRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        public FenceManagerDialog()
        {
            InitializeComponent();
            RefreshList();
            LoadSettings();
            
            fencesChangedHandler = (s, e) =>
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    try
                    {
                        BeginInvoke((Action)RefreshList);
                    }
                    catch { }
                }
            };
            FenceManager.Instance.FencesChanged += fencesChangedHandler;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            FenceManager.Instance.FencesChanged -= fencesChangedHandler;
            base.OnFormClosed(e);
        }

        private void InitializeComponent()
        {
            this.Text = "围栏管理器";
            this.Size = new Size(400, 450);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;

            // ListView
            listView = new ListView
            {
                Location = new Point(12, 12),
                Size = new Size(360, 180),
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = true,
                GridLines = true
            };
            listView.Columns.Add("围栏名称", 200);
            listView.Columns.Add("锁定状态", 140);
            listView.ItemCheck += ListView_ItemCheck;
            this.Controls.Add(listView);

            // Lock All Button
            btnLockAll = new Button
            {
                Text = "全部锁定",
                Location = new Point(12, 200),
                Size = new Size(100, 30)
            };
            btnLockAll.Click += BtnLockAll_Click;
            this.Controls.Add(btnLockAll);

            // Unlock All Button
            btnUnlockAll = new Button
            {
                Text = "全部解锁",
                Location = new Point(120, 200),
                Size = new Size(100, 30)
            };
            btnUnlockAll.Click += BtnUnlockAll_Click;
            this.Controls.Add(btnUnlockAll);

            // Settings Group
            var grpSettings = new GroupBox
            {
                Text = "设置",
                Location = new Point(12, 240),
                Size = new Size(360, 120)
            };
            this.Controls.Add(grpSettings);

            // Auto Start CheckBox
            chkAutoStart = new CheckBox
            {
                Text = "开机自启动",
                Location = new Point(10, 25),
                AutoSize = true
            };
            chkAutoStart.CheckedChanged += ChkAutoStart_CheckedChanged;
            grpSettings.Controls.Add(chkAutoStart);

            // Auto Refresh CheckBox
            chkAutoRefresh = new CheckBox
            {
                Text = "自动检测围栏显示状态",
                Location = new Point(10, 55),
                AutoSize = true
            };
            chkAutoRefresh.CheckedChanged += ChkAutoRefresh_CheckedChanged;
            grpSettings.Controls.Add(chkAutoRefresh);

            // Refresh Interval Label
            lblRefreshInterval = new Label
            {
                Text = "刷新间隔:",
                Location = new Point(30, 85),
                AutoSize = true
            };
            grpSettings.Controls.Add(lblRefreshInterval);

            // Refresh Interval ComboBox
            cboRefreshInterval = new ComboBox
            {
                Location = new Point(100, 82),
                Size = new Size(100, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboRefreshInterval.Items.AddRange(new object[] { "50ms", "100ms", "200ms", "500ms", "1000ms" });
            cboRefreshInterval.SelectedIndexChanged += CboRefreshInterval_SelectedIndexChanged;
            grpSettings.Controls.Add(cboRefreshInterval);

            // Close Button
            btnClose = new Button
            {
                Text = "关闭",
                Location = new Point(272, 370),
                Size = new Size(100, 30),
                DialogResult = DialogResult.Cancel
            };
            btnClose.Click += (s, e) => Close();
            this.Controls.Add(btnClose);

            this.CancelButton = btnClose;
        }

        private void LoadSettings()
        {
            // Load auto start setting
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, false))
                {
                    if (key != null)
                    {
                        var value = key.GetValue(AppName);
                        chkAutoStart.Checked = value != null;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadAutoStartSetting error: {ex.Message}");
            }

            // Load auto refresh settings
            var settings = AppSettings.Instance;
            chkAutoRefresh.Checked = settings.AutoRefreshEnabled;
            
            // Set combo box selection based on interval
            switch (settings.RefreshIntervalMs)
            {
                case 50: cboRefreshInterval.SelectedIndex = 0; break;
                case 100: cboRefreshInterval.SelectedIndex = 1; break;
                case 200: cboRefreshInterval.SelectedIndex = 2; break;
                case 500: cboRefreshInterval.SelectedIndex = 3; break;
                case 1000: cboRefreshInterval.SelectedIndex = 4; break;
                default: cboRefreshInterval.SelectedIndex = 1; break;
            }

            UpdateRefreshIntervalEnabled();
        }

        private void UpdateRefreshIntervalEnabled()
        {
            lblRefreshInterval.Enabled = chkAutoRefresh.Checked;
            cboRefreshInterval.Enabled = chkAutoRefresh.Checked;
        }

        private void ChkAutoStart_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true))
                {
                    if (key != null)
                    {
                        if (chkAutoStart.Checked)
                        {
                            string exePath = Application.ExecutablePath;
                            key.SetValue(AppName, $"\"{exePath}\"");
                        }
                        else
                        {
                            key.DeleteValue(AppName, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ChkAutoStart_CheckedChanged error: {ex.Message}");
                MessageBox.Show("无法修改开机自启动设置，请以管理员身份运行程序。", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                chkAutoStart.CheckedChanged -= ChkAutoStart_CheckedChanged;
                chkAutoStart.Checked = !chkAutoStart.Checked;
                chkAutoStart.CheckedChanged += ChkAutoStart_CheckedChanged;
            }
        }

        private void ChkAutoRefresh_CheckedChanged(object sender, EventArgs e)
        {
            var settings = AppSettings.Instance;
            settings.AutoRefreshEnabled = chkAutoRefresh.Checked;
            settings.Save();
            settings.NotifySettingsChanged();
            UpdateRefreshIntervalEnabled();
        }

        private void CboRefreshInterval_SelectedIndexChanged(object sender, EventArgs e)
        {
            var settings = AppSettings.Instance;
            switch (cboRefreshInterval.SelectedIndex)
            {
                case 0: settings.RefreshIntervalMs = 50; break;
                case 1: settings.RefreshIntervalMs = 100; break;
                case 2: settings.RefreshIntervalMs = 200; break;
                case 3: settings.RefreshIntervalMs = 500; break;
                case 4: settings.RefreshIntervalMs = 1000; break;
            }
            settings.Save();
            settings.NotifySettingsChanged();
        }

        private void RefreshList()
        {
            try
            {
                listView.ItemCheck -= ListView_ItemCheck;
                listView.Items.Clear();

                var fences = FenceManager.Instance.OpenFences.ToArray();

                foreach (var fence in fences)
                {
                    if (fence == null || fence.IsDisposed)
                        continue;

                    var item = new ListViewItem(fence.FenceInfo?.Name ?? "Unknown")
                    {
                        Tag = fence,
                        Checked = fence.FenceInfo?.Locked ?? false
                    };
                    item.SubItems.Add(fence.FenceInfo?.Locked == true ? "已锁定" : "未锁定");
                    listView.Items.Add(item);
                }

                listView.ItemCheck += ListView_ItemCheck;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshList error: {ex.Message}");
            }
        }

        private void ListView_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            try
            {
                var item = listView.Items[e.Index];
                var fence = item.Tag as FenceWindow;
                if (fence != null && !fence.IsDisposed)
                {
                    bool newLocked = e.NewValue == CheckState.Checked;
                    fence.SetLocked(newLocked);
                    item.SubItems[1].Text = newLocked ? "已锁定" : "未锁定";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ListView_ItemCheck error: {ex.Message}");
            }
        }

        private void BtnLockAll_Click(object sender, EventArgs e)
        {
            try
            {
                var fences = FenceManager.Instance.OpenFences.ToArray();
                foreach (var fence in fences)
                {
                    if (fence != null && !fence.IsDisposed)
                    {
                        fence.SetLocked(true);
                    }
                }
                RefreshList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BtnLockAll_Click error: {ex.Message}");
            }
        }

        private void BtnUnlockAll_Click(object sender, EventArgs e)
        {
            try
            {
                var fences = FenceManager.Instance.OpenFences.ToArray();
                foreach (var fence in fences)
                {
                    if (fence != null && !fence.IsDisposed)
                    {
                        fence.SetLocked(false);
                    }
                }
                RefreshList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BtnUnlockAll_Click error: {ex.Message}");
            }
        }
    }
}
