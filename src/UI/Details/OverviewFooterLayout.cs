using System;
using System.Drawing;

namespace TokenDock
{
    static class OverviewFooterLayout
    {
        public static Rectangle[] Calculate(Size size, float scale)
        {
            float width = size.Width / scale, height = size.Height / scale;
            float buttonWidth = (width - 56) / 3;
            var result = new Rectangle[4];
            for (int i = 0; i < 3; i++)
                result[i] = new Rectangle((int)Math.Round((20 + i * (buttonWidth + 8)) * scale), (int)Math.Round((height - 54) * scale), (int)Math.Floor(buttonWidth * scale), (int)Math.Round(36 * scale));
            result[3] = new Rectangle((int)Math.Round((width - 52) * scale), (int)Math.Round(14 * scale), (int)Math.Round(32 * scale), (int)Math.Round(32 * scale));
            return result;
        }
    }
}
