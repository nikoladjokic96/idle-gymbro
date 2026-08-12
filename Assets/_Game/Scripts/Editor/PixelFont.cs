using System.Collections.Generic;
using UnityEngine;

namespace IdleGymBro.EditorTools
{
    // Minimal 5x7 uppercase bitmap font baked directly into placeholder texture pixel arrays,
    // so an artist opening a placeholder file/thumbnail instantly knows what it represents
    // without needing to inspect metadata.
    public static class PixelFont
    {
        private const int GlyphWidth = 5;
        private const int GlyphHeight = 7;

        // Each glyph = 7 bytes, top row first. Low 5 bits of each byte are the pixel row;
        // bit4 = leftmost column, bit0 = rightmost column. Unknown chars render as blank.
        public static readonly Dictionary<char, byte[]> Glyphs = new Dictionary<char, byte[]>
        {
            ['A'] = new byte[] { 0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 },
            ['B'] = new byte[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110 },
            ['C'] = new byte[] { 0b01111, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b01111 },
            ['D'] = new byte[] { 0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110 },
            ['E'] = new byte[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111 },
            ['F'] = new byte[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000 },
            ['G'] = new byte[] { 0b01111, 0b10000, 0b10000, 0b10111, 0b10001, 0b10001, 0b01111 },
            ['H'] = new byte[] { 0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 },
            ['I'] = new byte[] { 0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b11111 },
            ['J'] = new byte[] { 0b00111, 0b00010, 0b00010, 0b00010, 0b00010, 0b10010, 0b01100 },
            ['K'] = new byte[] { 0b10001, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010, 0b10001 },
            ['L'] = new byte[] { 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111 },
            ['M'] = new byte[] { 0b10001, 0b11011, 0b10101, 0b10001, 0b10001, 0b10001, 0b10001 },
            ['N'] = new byte[] { 0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001, 0b10001 },
            ['O'] = new byte[] { 0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 },
            ['P'] = new byte[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000 },
            ['Q'] = new byte[] { 0b01110, 0b10001, 0b10001, 0b10001, 0b10101, 0b10010, 0b01101 },
            ['R'] = new byte[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001 },
            ['S'] = new byte[] { 0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110 },
            ['T'] = new byte[] { 0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100 },
            ['U'] = new byte[] { 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 },
            ['V'] = new byte[] { 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100 },
            ['W'] = new byte[] { 0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b11011, 0b10001 },
            ['X'] = new byte[] { 0b10001, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001, 0b10001 },
            ['Y'] = new byte[] { 0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100 },
            ['Z'] = new byte[] { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111 },
            ['0'] = new byte[] { 0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110 },
            ['1'] = new byte[] { 0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b11111 },
            ['2'] = new byte[] { 0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b01000, 0b11111 },
            ['3'] = new byte[] { 0b11110, 0b00001, 0b00001, 0b01110, 0b00001, 0b00001, 0b11110 },
            ['4'] = new byte[] { 0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010 },
            ['5'] = new byte[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b00001, 0b00001, 0b11110 },
            ['6'] = new byte[] { 0b01110, 0b10000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110 },
            ['7'] = new byte[] { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000 },
            ['8'] = new byte[] { 0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110 },
            ['9'] = new byte[] { 0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00001, 0b01110 },
            [' '] = new byte[] { 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000 },
            [':'] = new byte[] { 0b00000, 0b00100, 0b00000, 0b00000, 0b00100, 0b00000, 0b00000 },
            ['-'] = new byte[] { 0b00000, 0b00000, 0b00000, 0b11111, 0b00000, 0b00000, 0b00000 },

            // Everything below exists because this font now also renders the live UI (see
            // PixelFontAssetGenerator), not just baked placeholder labels. A missing glyph in a
            // bitmap font draws nothing at all, so the set has to cover every character the HUD can
            // produce: costs, levels, percentages, rates, countdowns and the odd bit of punctuation.
            ['.'] = new byte[] { 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00100 },
            [','] = new byte[] { 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00100, 0b01000 },
            ['+'] = new byte[] { 0b00000, 0b00100, 0b00100, 0b11111, 0b00100, 0b00100, 0b00000 },
            ['/'] = new byte[] { 0b00001, 0b00010, 0b00010, 0b00100, 0b01000, 0b01000, 0b10000 },
            ['%'] = new byte[] { 0b11001, 0b11010, 0b00010, 0b00100, 0b01000, 0b01011, 0b10011 },
            ['('] = new byte[] { 0b00010, 0b00100, 0b01000, 0b01000, 0b01000, 0b00100, 0b00010 },
            [')'] = new byte[] { 0b01000, 0b00100, 0b00010, 0b00010, 0b00010, 0b00100, 0b01000 },
            ['!'] = new byte[] { 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00000, 0b00100 },
            ['?'] = new byte[] { 0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b00000, 0b00100 },
            ['\''] = new byte[] { 0b00100, 0b00100, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000 },
            ['"'] = new byte[] { 0b01010, 0b01010, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000 },
            ['$'] = new byte[] { 0b00100, 0b01111, 0b10100, 0b01110, 0b00101, 0b11110, 0b00100 },
            ['='] = new byte[] { 0b00000, 0b00000, 0b11111, 0b00000, 0b11111, 0b00000, 0b00000 },
            ['*'] = new byte[] { 0b00000, 0b10101, 0b01110, 0b11111, 0b01110, 0b10101, 0b00000 },
            ['#'] = new byte[] { 0b01010, 0b11111, 0b01010, 0b01010, 0b01010, 0b11111, 0b01010 },
            ['<'] = new byte[] { 0b00010, 0b00100, 0b01000, 0b10000, 0b01000, 0b00100, 0b00010 },
            ['>'] = new byte[] { 0b01000, 0b00100, 0b00010, 0b00001, 0b00010, 0b00100, 0b01000 },
            ['['] = new byte[] { 0b00110, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00110 },
            [']'] = new byte[] { 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01100 },
        };

        // originX/originY = bottom-left of the whole string, in pixel coords with y up.
        public static void DrawLabel(Color[] pixels, int texWidth, int texHeight, string text, int originX, int originY, Color color, int scale)
        {
            if (pixels == null || string.IsNullOrEmpty(text) || scale <= 0)
            {
                return;
            }

            string upper = text.ToUpperInvariant();
            int x = originX;

            foreach (char c in upper)
            {
                if (Glyphs.TryGetValue(c, out byte[] rows))
                {
                    DrawGlyph(pixels, texWidth, texHeight, rows, x, originY, color, scale);
                }

                // Advance even for unknown/space chars so the caller's spacing stays predictable.
                x += (GlyphWidth + 1) * scale;
            }
        }

        public static int MeasureWidth(string text, int scale)
        {
            return text.Length * 6 * scale;
        }

        private static void DrawGlyph(Color[] pixels, int texWidth, int texHeight, byte[] rows, int originX, int originY, Color color, int scale)
        {
            for (int row = 0; row < GlyphHeight; row++)
            {
                byte bits = rows[row];
                // Row 0 is the top of the glyph; originY is the glyph's bottom, so the top row
                // lands at the highest y.
                int yStart = originY + (GlyphHeight - 1 - row) * scale;

                for (int col = 0; col < GlyphWidth; col++)
                {
                    int bitIndex = GlyphWidth - 1 - col; // bit4 = leftmost column (col 0)
                    if (((bits >> bitIndex) & 1) == 0)
                    {
                        continue;
                    }

                    int xStart = originX + col * scale;
                    FillBlock(pixels, texWidth, texHeight, xStart, yStart, scale, color);
                }
            }
        }

        private static void FillBlock(Color[] pixels, int texWidth, int texHeight, int xStart, int yStart, int scale, Color color)
        {
            for (int dy = 0; dy < scale; dy++)
            {
                int y = yStart + dy;
                if (y < 0 || y >= texHeight)
                {
                    continue;
                }

                int rowOffset = y * texWidth;
                for (int dx = 0; dx < scale; dx++)
                {
                    int x = xStart + dx;
                    if (x < 0 || x >= texWidth)
                    {
                        continue;
                    }

                    pixels[rowOffset + x] = color;
                }
            }
        }
    }
}
