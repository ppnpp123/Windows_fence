using NoFences.Model;
using NoFences.Win32;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace NoFences
{
    public class NoteWindow : Form
    {
        private readonly NoteInfo noteInfo;
        private TextBox txtContent;
        private Panel titleBar;
        private Label lblTitle;
        private ContextMenuStrip noteContextMenu;
        private ToolStripMenuItem lockedMenuItem;
        private ToolStripMenuItem boldMenuItem;
        private volatile bool isDisposing = false;
        private readonly object saveLock = new object();

        private const int TITLE_HEIGHT = 28;
        private const int RESIZE_BORDER = 6;

        public NoteInfo NoteInfo => noteInfo;

        public NoteWindow(NoteInfo noteInfo)
        {
            this.noteInfo = noteInfo;
            InitializeComponent();
            ApplySettings();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form settings
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;
            this.MinimumSize = new Size(150, 100);
            this.DoubleBuffered = true;

            // Title bar
            titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = TITLE_HEIGHT,
                Cursor = Cursors.SizeAll
            };
            titleBar.MouseDown += TitleBar_MouseDown;
            titleBar.Paint += TitleBar_Paint;

            lblTitle = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            lblTitle.MouseDown += TitleBar_MouseDown;
            titleBar.Controls.Add(lblTitle);

            // Content text box
            txtContent = new TextBox
            {
                Multiline = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                AcceptsReturn = true,
                AcceptsTab = true
            };
            txtContent.TextChanged += TxtContent_TextChanged;

            // Context menu
            CreateContextMenu();

            // Add controls
            this.Controls.Add(txtContent);
            this.Controls.Add(titleBar);

            // Events
            this.Load += NoteWindow_Load;
            this.FormClosed += NoteWindow_FormClosed;
            this.Resize += NoteWindow_Resize;
            this.LocationChanged += NoteWindow_LocationChanged;

            this.ResumeLayout(false);
        }

        private void CreateContextMenu()
        {
            noteContextMenu = new ContextMenuStrip();

            lockedMenuItem = new ToolStripMenuItem("锁定置顶") { CheckOnClick = true };
            lockedMenuItem.Click += LockedMenuItem_Click;

            boldMenuItem = new ToolStripMenuItem("加粗") { CheckOnClick = true };
            boldMenuItem.Click += BoldMenuItem_Click;

            var fontSizeMenu = new ToolStripMenuItem("字体大小");
            foreach (var size in new[] { 10, 12, 14, 16, 18, 20, 24, 28, 32 })
            {
                var item = new ToolStripMenuItem($"{size}");
                item.Tag = size;
                item.Click += FontSizeItem_Click;
                fontSizeMenu.DropDownItems.Add(item);
            }

            var textColorItem = new ToolStripMenuItem("文字颜色");
            textColorItem.Click += TextColorItem_Click;

            var bgColorItem = new ToolStripMenuItem("背景颜色");
            bgColorItem.Click += BgColorItem_Click;

            var renameItem = new ToolStripMenuItem("重命名");
            renameItem.Click += RenameItem_Click;

            var deleteItem = new ToolStripMenuItem("删除便签");
            deleteItem.Click += DeleteItem_Click;

            noteContextMenu.Items.AddRange(new ToolStripItem[]
            {
                lockedMenuItem,
                new ToolStripSeparator(),
                fontSizeMenu,
                boldMenuItem,
                textColorItem,
                bgColorItem,
                new ToolStripSeparator(),
                renameItem,
                deleteItem
            });

            titleBar.ContextMenuStrip = noteContextMenu;
            txtContent.ContextMenuStrip = noteContextMenu;
        }

        private void ApplySettings()
        {
            this.Location = new Point(noteInfo.PosX, noteInfo.PosY);
            this.Size = new Size(noteInfo.Width, noteInfo.Height);
            this.Text = noteInfo.Title;
            lblTitle.Text = noteInfo.Title;
            txtContent.Text = noteInfo.Content;

            var bgColor = noteInfo.GetBackgroundColor();
            this.BackColor = bgColor;
            titleBar.BackColor = DarkenColor(bgColor, 0.1f);
            txtContent.BackColor = bgColor;

            UpdateFont();
            lockedMenuItem.Checked = noteInfo.Locked;
            boldMenuItem.Checked = noteInfo.IsBold;
            UpdateTopMost();
        }

        private void UpdateFont()
        {
            var style = noteInfo.IsBold ? FontStyle.Bold : FontStyle.Regular;
            txtContent.Font = new Font("Segoe UI", noteInfo.FontSize, style);
            txtContent.ForeColor = noteInfo.GetTextColor();
        }

        private void UpdateTopMost()
        {
            this.TopMost = noteInfo.Locked;
        }

        private Color DarkenColor(Color color, float factor)
        {
            return Color.FromArgb(
                color.A,
                (int)(color.R * (1 - factor)),
                (int)(color.G * (1 - factor)),
                (int)(color.B * (1 - factor))
            );
        }

        private void NoteWindow_Load(object sender, EventArgs e)
        {
            DropShadow.ApplyShadows(this);
        }

        private void TitleBar_Paint(object sender, PaintEventArgs e)
        {
            // Draw close button area hint
            var closeRect = new Rectangle(Width - 25, 5, 18, 18);
            using (var brush = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
            {
                e.Graphics.FillEllipse(brush, closeRect);
            }
            
            // Draw X
            using (var pen = new Pen(Color.FromArgb(150, 0, 0, 0), 1.5f))
            {
                e.Graphics.DrawLine(pen, closeRect.X + 5, closeRect.Y + 5, closeRect.Right - 5, closeRect.Bottom - 5);
                e.Graphics.DrawLine(pen, closeRect.Right - 5, closeRect.Y + 5, closeRect.X + 5, closeRect.Bottom - 5);
            }
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Check if clicked on close button
                var closeRect = new Rectangle(Width - 25, 5, 18, 18);
                if (closeRect.Contains(e.Location))
                {
                    this.Close();
                    return;
                }

                // Drag window if not locked
                if (!noteInfo.Locked)
                {
                    WindowUtil.ReleaseCapture();
                    WindowUtil.SendMessage(Handle, 0x00A1, (IntPtr)2, IntPtr.Zero);
                }
            }
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
                // Allow resizing from edges if not locked
                if (m.Msg == 0x0084 && !noteInfo.Locked) // WM_NCHITTEST
                {
                    base.WndProc(ref m);
                    var pt = PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF, (m.LParam.ToInt32() >> 16) & 0xFFFF));
                    
                    if (pt.Y >= Height - RESIZE_BORDER)
                    {
                        if (pt.X <= RESIZE_BORDER)
                            m.Result = (IntPtr)16; // HTBOTTOMLEFT
                        else if (pt.X >= Width - RESIZE_BORDER)
                            m.Result = (IntPtr)17; // HTBOTTOMRIGHT
                        else
                            m.Result = (IntPtr)15; // HTBOTTOM
                    }
                    else if (pt.X <= RESIZE_BORDER)
                        m.Result = (IntPtr)10; // HTLEFT
                    else if (pt.X >= Width - RESIZE_BORDER)
                        m.Result = (IntPtr)11; // HTRIGHT
                    return;
                }

                base.WndProc(ref m);
            }
            catch
            {
                base.WndProc(ref m);
            }
        }

        private void TxtContent_TextChanged(object sender, EventArgs e)
        {
            noteInfo.Content = txtContent.Text;
            Save();
        }

        private void NoteWindow_Resize(object sender, EventArgs e)
        {
            if (!isDisposing && IsHandleCreated)
            {
                noteInfo.Width = Width;
                noteInfo.Height = Height;
                Save();
                titleBar.Invalidate();
            }
        }

        private void NoteWindow_LocationChanged(object sender, EventArgs e)
        {
            if (!isDisposing && IsHandleCreated)
            {
                noteInfo.PosX = Location.X;
                noteInfo.PosY = Location.Y;
                Save();
            }
        }

        private void NoteWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            isDisposing = true;
        }

        private void LockedMenuItem_Click(object sender, EventArgs e)
        {
            noteInfo.Locked = lockedMenuItem.Checked;
            UpdateTopMost();
            Save();
        }

        private void BoldMenuItem_Click(object sender, EventArgs e)
        {
            noteInfo.IsBold = boldMenuItem.Checked;
            UpdateFont();
            Save();
        }

        private void FontSizeItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item && item.Tag is int size)
            {
                noteInfo.FontSize = size;
                UpdateFont();
                Save();
            }
        }

        private void TextColorItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new ColorDialog())
            {
                dialog.Color = noteInfo.GetTextColor();
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    noteInfo.SetTextColor(dialog.Color);
                    UpdateFont();
                    Save();
                }
            }
        }

        private void BgColorItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new ColorDialog())
            {
                dialog.Color = noteInfo.GetBackgroundColor();
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    noteInfo.SetBackgroundColor(dialog.Color);
                    var bgColor = noteInfo.GetBackgroundColor();
                    this.BackColor = bgColor;
                    titleBar.BackColor = DarkenColor(bgColor, 0.1f);
                    txtContent.BackColor = bgColor;
                    Save();
                }
            }
        }

        private void RenameItem_Click(object sender, EventArgs e)
        {
            var dialog = new EditDialog(noteInfo.Title);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                noteInfo.Title = dialog.NewName;
                lblTitle.Text = noteInfo.Title;
                this.Text = noteInfo.Title;
                Save();
            }
        }

        private void DeleteItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "确定要删除这个便签吗？", "删除便签", 
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                NoteManager.Instance.RemoveNote(noteInfo);
                this.Close();
            }
        }

        private void Save()
        {
            if (isDisposing) return;
            lock (saveLock)
            {
                NoteManager.Instance.UpdateNote(noteInfo);
            }
        }
    }
}
