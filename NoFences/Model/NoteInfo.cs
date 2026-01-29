using System;
using System.Drawing;

namespace NoFences.Model
{
    public class NoteInfo
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "便签";
        public string Content { get; set; } = "";
        public int PosX { get; set; }
        public int PosY { get; set; }
        public int Width { get; set; } = 250;
        public int Height { get; set; } = 200;
        public bool Locked { get; set; }
        public float FontSize { get; set; } = 12f;
        public bool IsBold { get; set; }
        public int TextColorArgb { get; set; } = Color.Black.ToArgb();
        public int BackgroundColorArgb { get; set; } = Color.FromArgb(255, 255, 250, 205).ToArgb(); // LemonChiffon

        public NoteInfo() { }

        public NoteInfo(Guid id)
        {
            Id = id;
        }

        public Color GetTextColor() => Color.FromArgb(TextColorArgb);
        public void SetTextColor(Color color) => TextColorArgb = color.ToArgb();
        public Color GetBackgroundColor() => Color.FromArgb(BackgroundColorArgb);
        public void SetBackgroundColor(Color color) => BackgroundColorArgb = color.ToArgb();
    }
}
