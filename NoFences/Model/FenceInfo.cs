using System;
using System.Collections.Generic;
using System.Drawing;

namespace NoFences.Model
{
    public class FenceInfo
    {
        /* 
         * DO NOT RENAME PROPERTIES. Used for XML serialization.
         */

        public Guid Id { get; set; }

        public string Name { get; set; }

        public int PosX { get; set; }

        public int PosY { get; set; }

        /// <summary>
        /// Gets or sets the DPI scaled window width.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the DPI scaled window height.
        /// </summary>
        public int Height { get; set; }

        public bool Locked { get; set; }

        public bool CanMinify { get; set; }

        /// <summary>
        /// Gets or sets the logical window title height.
        /// </summary>
        public int TitleHeight { get; set; } = 35;

        /// <summary>
        /// Background color ARGB value.
        /// </summary>
        public int BackgroundColorArgb { get; set; } = Color.FromArgb(100, 0, 0, 0).ToArgb();

        public List<string> Files { get; set; } = new List<string>();

        public FenceInfo()
        {

        }

        public FenceInfo(Guid id)
        {
            Id = id;
        }

        public Color GetBackgroundColor()
        {
            return Color.FromArgb(BackgroundColorArgb);
        }

        public void SetBackgroundColor(Color color)
        {
            BackgroundColorArgb = color.ToArgb();
        }
    }
}
