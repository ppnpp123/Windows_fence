using NoFences.Model;
using NoFences.Util;
using NoFences.Win32;
using Peter;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using static NoFences.Win32.WindowUtil;

namespace NoFences
{
    public partial class FenceWindow : Form
    {
        private int logicalTitleHeight;
        private int titleHeight;
        private int logicalTitleOffset = 3;
        private int titleOffset;
        private int logicalItemWidth = 75;
        private int itemWidth;
        private int logicalItemPadding = 15;
        private int itemPadding;
        private int logicalTextHeight = 25;
        private int textHeight;
        private int itemHeight;
        private float shadowDist = 1.5f;

        private float dpiScaleX = 1.0f;
        private float dpiScaleY = 1.0f;

        private readonly FenceInfo fenceInfo;

        public FenceInfo FenceInfo => fenceInfo;

        private Font titleFont;
        private Font iconFont;

        private string selectedItem;
        private string hoveringItem;
        private bool shouldUpdateSelection;
        private bool shouldRunDoubleClick;
        private bool hasSelectionUpdated;
        private bool hasHoverUpdated;
        private bool isMinified;
        private int prevHeight;

        private int scrollHeight;
        private int scrollOffset;

        private readonly ThrottledExecution throttledMove = new ThrottledExecution(TimeSpan.FromSeconds(4));
        private readonly ThrottledExecution throttledResize = new ThrottledExecution(TimeSpan.FromSeconds(4));

        private readonly ShellContextMenu shellContextMenu = new ShellContextMenu();

        private readonly ThumbnailProvider thumbnailProvider = new ThumbnailProvider();
        private readonly string shortcutsDir;
        private volatile bool isDisposing = false;

        // Auto-refresh timer for visibility check
        private System.Windows.Forms.Timer autoRefreshTimer;
        private EventHandler settingsChangedHandler;

        private void ReloadFonts()
        {
            var family = new FontFamily("Segoe UI");
            titleFont = new Font(family, (int)Math.Floor(logicalTitleHeight / 2.0), FontStyle.Regular, GraphicsUnit.Point);
            iconFont = new Font(family, 9 * dpiScaleY, FontStyle.Regular, GraphicsUnit.Point);
        }

        public FenceWindow(FenceInfo fenceInfo)
        {
            InitializeComponent();
            DropShadow.ApplyShadows(this);
            BlurUtil.EnableBlur(Handle, fenceInfo.GetBackgroundColor());
            WindowUtil.HideFromAltTab(Handle);
            DesktopUtil.GlueToDesktop(Handle);
            //DesktopUtil.PreventMinimize(Handle);
            logicalTitleHeight = (fenceInfo.TitleHeight < 16 || fenceInfo.TitleHeight > 100) ? 35 : fenceInfo.TitleHeight;
            
            // Initialize shortcuts directory
            shortcutsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NoFences", "Shortcuts", fenceInfo.Id.ToString());
            Directory.CreateDirectory(shortcutsDir);
            
            // Initialize DPI scaling
            using (var graphics = CreateGraphics())
            {
                dpiScaleX = graphics.DpiX / 96f;
                dpiScaleY = graphics.DpiY / 96f;
            }
            UpdateDpiScaling();
            
            this.MouseWheel += FenceWindow_MouseWheel;
            thumbnailProvider.IconThumbnailLoaded += ThumbnailProvider_IconThumbnailLoaded;

            ReloadFonts();

            AllowDrop = true;

            this.fenceInfo = fenceInfo;
            Text = fenceInfo.Name;
            Location = new Point(fenceInfo.PosX, fenceInfo.PosY);

            Width = fenceInfo.Width;
            Height = fenceInfo.Height;

            prevHeight = Height;
            lockedToolStripMenuItem.Checked = fenceInfo.Locked;
            minifyToolStripMenuItem.Checked = fenceInfo.CanMinify;
            Minify();

            // Setup auto-refresh timer
            SetupAutoRefreshTimer();
            
            // Subscribe to settings changes
            settingsChangedHandler = (s, args) =>
            {
                if (IsHandleCreated && !isDisposing && !IsDisposed)
                {
                    try
                    {
                        BeginInvoke((Action)SetupAutoRefreshTimer);
                    }
                    catch { }
                }
            };
            AppSettings.Instance.SettingsChanged += settingsChangedHandler;
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            dpiScaleX = e.DeviceDpiNew / 96f;
            dpiScaleY = e.DeviceDpiNew / 96f;
            UpdateDpiScaling();
            Refresh();
        }

        private void UpdateDpiScaling()
        {
            titleHeight = LogicalToDeviceUnits(logicalTitleHeight);
            titleOffset = (int)(logicalTitleOffset * dpiScaleY);
            itemWidth = (int)(logicalItemWidth * dpiScaleX);
            itemPadding = (int)(logicalItemPadding * dpiScaleX);
            textHeight = (int)(logicalTextHeight * dpiScaleY);
            itemHeight = 32 + itemPadding + textHeight;
            shadowDist = 1.5f * dpiScaleX;
            ReloadFonts();
        }

        protected override void WndProc(ref Message m)
        {
            if (isDisposing) 
            {
                base.WndProc(ref m);
                return;
            }

            try
            {
                // Remove border
                if (m.Msg == 0x0083)
                {
                    m.Result = IntPtr.Zero;
                    return;
                }

                // Mouse leave
                if (m.Msg == 0x02a2)
                {
                    try
                    {
                        var myrect = new Rectangle(Location, Size);
                        if (!myrect.IntersectsWith(new Rectangle(MousePosition, new Size(1, 1))))
                        {
                            Minify();
                        }
                    }
                    catch { }
                }

                // Handle system commands
                if (m.Msg == WM_SYSCOMMAND)
                {
                    var cmd = GetWParamAsInt(m.WParam) & 0xFFF0;
                    // Prevent maximize
                    if (cmd == 0xF032)
                    {
                        m.Result = IntPtr.Zero;
                        return;
                    }
                }

                // Prevent foreground
                if (m.Msg == WM_SETFOCUS)
                {
                    SetWindowPos(Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
                    return;
                }

                // Other messages
                base.WndProc(ref m);

                // If locked, don't allow dragging/resizing
                if (MouseButtons == MouseButtons.Right || lockedToolStripMenuItem.Checked)
                    return;

                // Allow dragging and resizing
                if (m.Msg == WM_NCHITTEST)
                {
                    var pt = PointFromLParam(m.LParam);
                    pt = PointToClient(pt);

                    if ((int)m.Result == HTCLIENT && pt.Y < titleHeight)
                    {
                        m.Result = (IntPtr)HTCAPTION;
                        FenceWindow_MouseEnter(null, null);
                    }

                    if (pt.X < 10 && pt.Y < 10)
                        m.Result = new IntPtr(HTTOPLEFT);
                    else if (pt.X > (Width - 10) && pt.Y < 10)
                        m.Result = new IntPtr(HTTOPRIGHT);
                    else if (pt.X < 10 && pt.Y > (Height - 10))
                        m.Result = new IntPtr(HTBOTTOMLEFT);
                    else if (pt.X > (Width - 10) && pt.Y > (Height - 10))
                        m.Result = new IntPtr(HTBOTTOMRIGHT);
                    else if (pt.Y > (Height - 10))
                        m.Result = new IntPtr(HTBOTTOM);
                    else if (pt.X < 10)
                        m.Result = new IntPtr(HTLEFT);
                    else if (pt.X > (Width - 10))
                        m.Result = new IntPtr(HTRIGHT);
                }
            }
            catch
            {
                base.WndProc(ref m);
            }
        }

        // Safe conversion for WParam (avoids overflow on 64-bit)
        private static int GetWParamAsInt(IntPtr wParam)
        {
            return unchecked((int)(long)wParam);
        }

        // Safe extraction of Point from LParam (handles 64-bit and multi-monitor negative coords)
        private static Point PointFromLParam(IntPtr lParam)
        {
            int value = unchecked((int)(long)lParam);
            int x = unchecked((short)(value & 0xFFFF));
            int y = unchecked((short)((value >> 16) & 0xFFFF));
            return new Point(x, y);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Really remove this fence?", "Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                FenceManager.Instance.RemoveFence(fenceInfo);
                Close();
            }
        }

        private void deleteItemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fenceInfo.Files.Remove(hoveringItem);
            hoveringItem = null;
            Save();
            Refresh();
        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            deleteItemToolStripMenuItem.Visible = hoveringItem != null;
        }

        private void FenceWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && !lockedToolStripMenuItem.Checked)
                e.Effect = DragDropEffects.Move;
        }

        private void FenceWindow_DragDrop(object sender, DragEventArgs e)
        {
            var dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (var file in dropped)
            {
                if (ItemExists(file))
                {
                    // Create shortcut path
                    var fileName = Path.GetFileName(file);
                    var shortcutPath = Path.Combine(shortcutsDir, fileName + ".lnk");
                    
                    // Create shortcut if it doesn't exist
                    if (!File.Exists(shortcutPath))
                    {
                        shortcutPath = Extensions.CreateShortcut(file, shortcutPath);
                    }
                    
                    // Add shortcut to fence if not already present
                    if (!fenceInfo.Files.Contains(shortcutPath))
                    {
                        fenceInfo.Files.Add(shortcutPath);
                    }
                }
            }
            Save();
            Refresh();
        }

        private void FenceWindow_Resize(object sender, EventArgs e)
        {
            throttledResize.Run(() =>
            {
                fenceInfo.Width = Width;
                fenceInfo.Height = isMinified ? prevHeight : Height;
                Save();
            });

            Refresh();
        }

        private void FenceWindow_MouseMove(object sender, MouseEventArgs e)
        {
            Refresh();
        }

        private void FenceWindow_MouseEnter(object sender, EventArgs e)
        {
            if (minifyToolStripMenuItem.Checked && isMinified)
            {
                isMinified = false;
                Height = prevHeight;
            }
        }

        private void FenceWindow_MouseLeave(object sender, EventArgs e)
        {
            Minify();
            selectedItem = null;
            Refresh();
        }

        private void Minify()
        {
            if (minifyToolStripMenuItem.Checked && !isMinified)
            {
                isMinified = true;
                prevHeight = Height;
                Height = titleHeight;
                Refresh();
            }
        }

        private void minifyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isMinified)
            {
                Height = prevHeight;
                isMinified = false;
            }
            fenceInfo.CanMinify = minifyToolStripMenuItem.Checked;
            Save();

        }

        private void FenceWindow_Click(object sender, EventArgs e)
        {
            shouldUpdateSelection = true;
            Refresh();
        }

        private void FenceWindow_DoubleClick(object sender, EventArgs e)
        {
            shouldRunDoubleClick = true;
            Refresh();
        }

        private void FenceWindow_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.Clip = new Region(ClientRectangle);
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Background - use custom color from fenceInfo
            var bgColor = fenceInfo.GetBackgroundColor();
            e.Graphics.FillRectangle(new SolidBrush(bgColor), ClientRectangle);

            // Title - slightly darker than background
            var titleBgColor = Color.FromArgb(Math.Min(bgColor.A + 50, 255), bgColor.R, bgColor.G, bgColor.B);
            e.Graphics.DrawString(Text, titleFont, Brushes.White, new PointF(Width / 2, titleOffset), new StringFormat { Alignment = StringAlignment.Center });
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(50, 0, 0, 0)), new RectangleF(0, 0, Width, titleHeight));

            // Items
            var x = itemPadding;
            var y = itemPadding;
            scrollHeight = 0;
            e.Graphics.Clip = new Region(new Rectangle(0, titleHeight, Width, Height - titleHeight));
            foreach (var file in fenceInfo.Files)
            {
                var entry = FenceEntry.FromPath(file);
                if (entry == null)
                    continue;

                RenderEntry(e.Graphics, entry, x, y + titleHeight - scrollOffset);

                var itemBottom = y + itemHeight;
                if (itemBottom > scrollHeight)
                    scrollHeight = itemBottom;

                x += itemWidth + itemPadding;
                if (x + itemWidth > Width)
                {
                    x = itemPadding;
                    y += itemHeight + itemPadding;
                }
            }

            scrollHeight -= (ClientRectangle.Height - titleHeight);

            // Scroll bars
            if (scrollHeight > 0)
            {
                var contentHeight = Height - titleHeight;
                var scrollbarHeight = contentHeight - scrollHeight;
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(150, Color.Black)), new Rectangle(Width - 5, titleHeight + scrollOffset, 5, scrollbarHeight));

                scrollOffset = Math.Min(scrollOffset, scrollHeight);
            }



            // Click handlers
            if (shouldUpdateSelection && !hasSelectionUpdated)
                selectedItem = null;

            if (!hasHoverUpdated)
                hoveringItem = null;

            shouldRunDoubleClick = false;
            shouldUpdateSelection = false;
            hasSelectionUpdated = false;
            hasHoverUpdated = false;
        }

        private void RenderEntry(Graphics g, FenceEntry entry, int x, int y)
        {
            var icon = entry.ExtractIcon(thumbnailProvider);
            var name = entry.Name;

            // Set high quality rendering settings
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Calculate text position with proper DPI scaling
            var textPosition = new PointF(x, y + icon.Height + (int)(5 * dpiScaleY));
            var textMaxSize = new SizeF(itemWidth, textHeight);

            var stringFormat = new StringFormat 
            { 
                Alignment = StringAlignment.Center, 
                Trimming = StringTrimming.EllipsisCharacter,
                LineAlignment = StringAlignment.Near
            };

            var textSize = g.MeasureString(name, iconFont, textMaxSize, stringFormat);
            var outlineRect = new Rectangle(
                x - (int)(2 * dpiScaleX), 
                y - (int)(2 * dpiScaleY), 
                itemWidth + (int)(4 * dpiScaleX), 
                icon.Height + (int)textSize.Height + (int)(5 * dpiScaleY) + (int)(4 * dpiScaleY)
            );
            var outlineRectInner = outlineRect.Shrink((int)(1 * dpiScaleX));

            var mousePos = PointToClient(MousePosition);
            var mouseOver = mousePos.X >= x && mousePos.Y >= y && mousePos.X < x + outlineRect.Width && mousePos.Y < y + outlineRect.Height;

            if (mouseOver)
            {
                hoveringItem = entry.Path;
                hasHoverUpdated = true;
            }

            if (mouseOver && shouldUpdateSelection)
            {
                selectedItem = entry.Path;
                shouldUpdateSelection = false;
                hasSelectionUpdated = true;
            }

            if (mouseOver && shouldRunDoubleClick)
            {
                shouldRunDoubleClick = false;
                entry.Open();
            }

            if (selectedItem == entry.Path)
            {
                if (mouseOver)
                {
                    g.DrawRectangle(new Pen(Color.FromArgb(120, SystemColors.ActiveBorder)), outlineRectInner);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(100, SystemColors.GradientActiveCaption)), outlineRect);
                }
                else
                {
                    g.DrawRectangle(new Pen(Color.FromArgb(120, SystemColors.ActiveBorder)), outlineRectInner);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(80, SystemColors.GradientInactiveCaption)), outlineRect);
                }
            }
            else
            {
                if (mouseOver)
                {
                    g.DrawRectangle(new Pen(Color.FromArgb(120, SystemColors.ActiveBorder)), outlineRectInner);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(80, SystemColors.ActiveCaption)), outlineRect);
                }
            }

            // Draw icon with high quality scaling
            var iconX = x + itemWidth / 2 - icon.Width / 2;
            var iconY = y;
            g.DrawIcon(icon, iconX, iconY);
            
            // Draw text with high quality settings
            g.DrawString(name, iconFont, new SolidBrush(Color.FromArgb(180, 15, 15, 15)), 
                new RectangleF(textPosition.Move(shadowDist, shadowDist), textMaxSize), stringFormat);
            g.DrawString(name, iconFont, Brushes.White, 
                new RectangleF(textPosition, textMaxSize), stringFormat);
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dialog = new EditDialog(Text);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Text = dialog.NewName;
                fenceInfo.Name = Text;
                Refresh();
                Save();
            }
        }

        private void newFenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FenceManager.Instance.CreateFence("New fence");
        }

        private void managerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dialog = new FenceManagerDialog();
            dialog.Show();
        }

        private void colorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dialog = new ColorPickerDialog(fenceInfo.GetBackgroundColor());
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                fenceInfo.SetBackgroundColor(dialog.SelectedColor);
                BlurUtil.EnableBlur(Handle, fenceInfo.GetBackgroundColor());
                Save();
                Refresh();
            }
        }

        private void FenceWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            isDisposing = true;
            
            // Cleanup timer
            if (autoRefreshTimer != null)
            {
                autoRefreshTimer.Stop();
                autoRefreshTimer.Dispose();
                autoRefreshTimer = null;
            }
            
            // Unsubscribe from settings changes
            if (settingsChangedHandler != null)
            {
                AppSettings.Instance.SettingsChanged -= settingsChangedHandler;
                settingsChangedHandler = null;
            }
            
            // Don't exit app - tray icon keeps it running
        }

        private void SetupAutoRefreshTimer()
        {
            try
            {
                // Stop existing timer if any
                if (autoRefreshTimer != null)
                {
                    autoRefreshTimer.Stop();
                    autoRefreshTimer.Dispose();
                    autoRefreshTimer = null;
                }

                var settings = AppSettings.Instance;
                if (settings.AutoRefreshEnabled)
                {
                    autoRefreshTimer = new System.Windows.Forms.Timer();
                    autoRefreshTimer.Interval = Math.Max(50, settings.RefreshIntervalMs);
                    autoRefreshTimer.Tick += AutoRefreshTimer_Tick;
                    autoRefreshTimer.Start();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetupAutoRefreshTimer error: {ex.Message}");
            }
        }

        private void AutoRefreshTimer_Tick(object sender, EventArgs e)
        {
            if (isDisposing || IsDisposed)
                return;

            try
            {
                EnsureFenceVisible();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutoRefreshTimer_Tick error: {ex.Message}");
            }
        }

        private void EnsureFenceVisible()
        {
            if (isDisposing || IsDisposed || !IsHandleCreated)
                return;

            try
            {
                // Ensure window is visible and at bottom of Z-order
                if (!Visible)
                {
                    Show();
                }
                
                // Keep window at bottom (behind other windows but above desktop)
                WindowUtil.SetWindowPos(Handle, WindowUtil.HWND_BOTTOM, 0, 0, 0, 0, 
                    WindowUtil.SWP_NOSIZE | WindowUtil.SWP_NOMOVE | WindowUtil.SWP_NOACTIVATE);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnsureFenceVisible error: {ex.Message}");
            }
        }

        private readonly object saveLock = new object();
        private void Save()
        {
            lock (saveLock)
            {
                FenceManager.Instance.UpdateFence(fenceInfo);
            }
        }

        private void FenceWindow_LocationChanged(object sender, EventArgs e)
        {
            throttledMove.Run(() =>
            {
                fenceInfo.PosX = Location.X;
                fenceInfo.PosY = Location.Y;
                Save();
            });
        }

        private void lockedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fenceInfo.Locked = lockedToolStripMenuItem.Checked;
            Save();
            FenceManager.Instance.NotifyFenceChanged();
        }

        public void SetLocked(bool locked)
        {
            fenceInfo.Locked = locked;
            lockedToolStripMenuItem.Checked = locked;
            Save();
            FenceManager.Instance.NotifyFenceChanged();
        }

        private void FenceWindow_Load(object sender, EventArgs e)
        {
            // Nothing special needed on load
        }

        private void titleSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dialog = new HeightDialog(fenceInfo.TitleHeight);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                fenceInfo.TitleHeight = dialog.TitleHeight;
                logicalTitleHeight = dialog.TitleHeight;
                titleHeight = LogicalToDeviceUnits(logicalTitleHeight);
                ReloadFonts();
                Minify();
                if (isMinified)
                {
                    Height = titleHeight;
                }
                Refresh();
                Save();
            }
        }

        private void FenceWindow_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            if (hoveringItem != null && !ModifierKeys.HasFlag(Keys.Shift))
            {
                shellContextMenu.ShowContextMenu(new[] { new FileInfo(hoveringItem) }, MousePosition);
            }
            else
            {
                appContextMenu.Show(this, e.Location);
            }
        }

        private void FenceWindow_MouseWheel(object sender, MouseEventArgs e)
        {
            if (scrollHeight < 1)
                return;

            scrollOffset -= Math.Sign(e.Delta) * 10;
            if (scrollOffset < 0)
                scrollOffset = 0;
            if (scrollOffset > scrollHeight)
                scrollOffset = scrollHeight;

            Invalidate();
        }

        private void ThumbnailProvider_IconThumbnailLoaded(object sender, EventArgs e)
        {
            Invalidate();
        }

        private bool ItemExists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }
    }

}

