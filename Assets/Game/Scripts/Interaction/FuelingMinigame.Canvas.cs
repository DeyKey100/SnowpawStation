using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class FuelingMinigame
{
    /// <summary>
    /// Пиксельный холст 320×180 в духе PS1: рисование примитивами, пиксельный шрифт,
    /// дизеринг Байера и 15-битное квантование цвета. Координаты — сверху вниз, как в
    /// HTML-прототипе; при загрузке в Texture2D строки переворачиваются.
    /// </summary>
    private sealed class PixelCanvas
    {
        public const int W = 320;
        public const int H = 180;
        private const int MaxPoints = 64;

        private static readonly int[,] Bayer4 =
        {
            { 0, 8, 2, 10 },
            { 12, 4, 14, 6 },
            { 3, 11, 1, 9 },
            { 15, 7, 13, 5 },
        };

        private static readonly Color32 Shadow = new Color32(8, 11, 17, 255);

        public readonly Color32[] Pixels = new Color32[W * H];
        private readonly Color32[] upload = new Color32[W * H];
        private readonly Vector2[] mapBuffer = new Vector2[MaxPoints];
        private readonly Vector2[] scratch = new Vector2[MaxPoints];
        private readonly Vector2[] rectScratch = new Vector2[4];
        private readonly float[] crossings = new float[MaxPoints];
        private readonly List<Matrix2D> stack = new List<Matrix2D>(8);

        private Matrix2D matrix = Matrix2D.Identity;

        // ---------- Состояние трансформации ----------

        public void ResetTransform()
        {
            matrix = Matrix2D.Identity;
            stack.Clear();
        }

        public void Push() => stack.Add(matrix);

        public void Pop()
        {
            if (stack.Count == 0)
            {
                matrix = Matrix2D.Identity;
                return;
            }

            matrix = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
        }

        public void Translate(float x, float y) => matrix = Matrix2D.Multiply(matrix, Matrix2D.Translation(x, y));

        public void Rotate(float radians) => matrix = Matrix2D.Multiply(matrix, Matrix2D.Rotation(radians));

        public void Scale(float factor) => matrix = Matrix2D.Multiply(matrix, Matrix2D.Scale(factor));

        // ---------- Очистка и копирование ----------

        public void Clear(Color32 color)
        {
            for (int i = 0; i < Pixels.Length; i++)
            {
                Pixels[i] = color;
            }
        }

        public void CopyFrom(PixelCanvas other) => Array.Copy(other.Pixels, Pixels, Pixels.Length);

        // ---------- Прямоугольники ----------

        public void FillRect(float x, float y, float w, float h, Color32 color)
        {
            if (matrix.IsIdentity)
            {
                int x0 = RoundInt(x);
                int y0 = RoundInt(y);
                FillRectPixels(x0, y0, RoundInt(x + w) - x0, RoundInt(y + h) - y0, color);
                return;
            }

            rectScratch[0] = new Vector2(x, y);
            rectScratch[1] = new Vector2(x + w, y);
            rectScratch[2] = new Vector2(x + w, y + h);
            rectScratch[3] = new Vector2(x, y + h);
            PolyFill(rectScratch, 4, color);
        }

        public void FillRectPixels(int x, int y, int w, int h, Color32 color)
        {
            int x0 = Mathf.Max(0, x);
            int y0 = Mathf.Max(0, y);
            int x1 = Mathf.Min(W, x + w);
            int y1 = Mathf.Min(H, y + h);
            for (int yy = y0; yy < y1; yy++)
            {
                int row = yy * W;
                for (int xx = x0; xx < x1; xx++)
                {
                    Blend(row + xx, color);
                }
            }
        }

        public void StrokeRect(float x, float y, float w, float h, Color32 color)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int x1 = Mathf.FloorToInt(x + w);
            int y1 = Mathf.FloorToInt(y + h);
            FillRectPixels(x0, y0, x1 - x0 + 1, 1, color);
            FillRectPixels(x0, y1, x1 - x0 + 1, 1, color);
            FillRectPixels(x0, y0, 1, y1 - y0 + 1, color);
            FillRectPixels(x1, y0, 1, y1 - y0 + 1, color);
        }

        // ---------- Полигоны ----------

        public void PolyFill(Vector2[] pts, Color32 fill, Color32? stroke = null) => PolyFill(pts, pts.Length, fill, stroke);

        public void PolyFill(Vector2[] pts, int count, Color32 fill, Color32? stroke = null)
        {
            if (count < 3)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                float lx = RoundInt(pts[i].x);
                float ly = RoundInt(pts[i].y);
                mapBuffer[i] = matrix.Map(lx, ly);
            }

            FillMapped(mapBuffer, count, fill);

            if (stroke.HasValue)
            {
                StrokeMapped(mapBuffer, count, true, stroke.Value, 1f);
            }
        }

        private void FillMapped(Vector2[] pts, int count, Color32 fill)
        {
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                if (pts[i].y < minY) minY = pts[i].y;
                if (pts[i].y > maxY) maxY = pts[i].y;
            }

            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY));
            int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(maxY) - 1);

            for (int y = y0; y <= y1; y++)
            {
                float sy = y + 0.5f;
                int n = 0;
                for (int i = 0; i < count; i++)
                {
                    Vector2 a = pts[i];
                    Vector2 b = pts[(i + 1) % count];
                    if ((a.y <= sy && b.y > sy) || (b.y <= sy && a.y > sy))
                    {
                        float t = (sy - a.y) / (b.y - a.y);
                        crossings[n++] = a.x + t * (b.x - a.x);
                    }
                }

                if (n < 2)
                {
                    continue;
                }

                Array.Sort(crossings, 0, n);
                for (int i = 0; i + 1 < n; i += 2)
                {
                    int xa = Mathf.CeilToInt(crossings[i] - 0.5f);
                    int xb = Mathf.FloorToInt(crossings[i + 1] - 0.5f);
                    if (xb < xa)
                    {
                        continue;
                    }

                    xa = Mathf.Max(0, xa);
                    xb = Mathf.Min(W - 1, xb);
                    int row = y * W;
                    for (int x = xa; x <= xb; x++)
                    {
                        Blend(row + x, fill);
                    }
                }
            }
        }

        // ---------- Окружности ----------

        public void Disc(float cx, float cy, float r, int sides, Color32 fill, float rot = 0f)
        {
            for (int i = 0; i < sides; i++)
            {
                float a = rot + i / (float)sides * Mathf.PI * 2f;
                scratch[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }

            PolyFill(scratch, sides, fill);
        }

        public void Ring(float cx, float cy, float r, int sides, Color32 color, float width = 1f, float rot = 0f)
        {
            for (int i = 0; i < sides; i++)
            {
                float a = rot + i / (float)sides * Mathf.PI * 2f;
                scratch[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }

            StrokeMappedLocal(scratch, sides, true, color, width);
        }

        // ---------- Линии ----------

        public void StrokePolyline(Vector2[] pts, Color32 color, bool closed = true, float width = 1f) =>
            StrokeMappedLocal(pts, pts.Length, closed, color, width);

        public void StrokePolyline(Vector2[] pts, int count, Color32 color, bool closed = true, float width = 1f) =>
            StrokeMappedLocal(pts, count, closed, color, width);

        private void StrokeMappedLocal(Vector2[] pts, int count, bool closed, Color32 color, float width)
        {
            for (int i = 0; i < count; i++)
            {
                float lx = RoundInt(pts[i].x);
                float ly = RoundInt(pts[i].y);
                mapBuffer[i] = matrix.Map(lx, ly);
            }

            StrokeMapped(mapBuffer, count, closed, color, width);
        }

        private void StrokeMapped(Vector2[] pts, int count, bool closed, Color32 color, float width)
        {
            if (width > 1f)
            {
                ThickMapped(pts, count, closed, color, width);
                return;
            }

            int segments = closed ? count : count - 1;
            for (int i = 0; i < segments; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[(i + 1) % count];
                Line(a.x, a.y, b.x, b.y, color);
            }
        }

        /// <summary>Толстая ломаная: каждый сегмент — четырёхугольник, стыки — квадраты.</summary>
        public void ThickPolyline(Vector2[] pts, int count, float width, Color32 color, float offsetX = 0f, float offsetY = 0f)
        {
            float half = width * 0.5f;
            for (int i = 0; i + 1 < count; i++)
            {
                Vector2 a = new Vector2(pts[i].x + offsetX, pts[i].y + offsetY);
                Vector2 b = new Vector2(pts[i + 1].x + offsetX, pts[i + 1].y + offsetY);
                Vector2 d = b - a;
                float len = d.magnitude;
                if (len < 0.0001f)
                {
                    continue;
                }

                Vector2 n = new Vector2(-d.y, d.x) / len * half;
                rectScratch[0] = a + n;
                rectScratch[1] = b + n;
                rectScratch[2] = b - n;
                rectScratch[3] = a - n;
                PolyFill(rectScratch, 4, color);
            }

            for (int i = 1; i + 1 < count; i++)
            {
                FillRect(pts[i].x + offsetX - half, pts[i].y + offsetY - half, width, width, color);
            }
        }

        private void ThickMapped(Vector2[] pts, int count, bool closed, Color32 color, float width)
        {
            int segments = closed ? count : count - 1;
            for (int i = 0; i < segments; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[(i + 1) % count];
                Vector2 d = b - a;
                float len = d.magnitude;
                if (len < 0.0001f)
                {
                    continue;
                }

                Vector2 n = new Vector2(-d.y, d.x) / len * (width * 0.5f);
                rectScratch[0] = a + n;
                rectScratch[1] = b + n;
                rectScratch[2] = b - n;
                rectScratch[3] = a - n;
                PolyFill(rectScratch, 4, color);
            }
        }

        private void Line(float x0, float y0, float x1, float y1, Color32 color)
        {
            int ix0 = RoundInt(x0);
            int iy0 = RoundInt(y0);
            int ix1 = RoundInt(x1);
            int iy1 = RoundInt(y1);

            int dx = Mathf.Abs(ix1 - ix0);
            int dy = -Mathf.Abs(iy1 - iy0);
            int sx = ix0 < ix1 ? 1 : -1;
            int sy = iy0 < iy1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                Plot(ix0, iy0, color);
                if (ix0 == ix1 && iy0 == iy1)
                {
                    break;
                }

                int e2 = err * 2;
                if (e2 >= dy)
                {
                    err += dy;
                    ix0 += sx;
                }

                if (e2 <= dx)
                {
                    err += dx;
                    iy0 += sy;
                }
            }
        }

        // ---------- Текст ----------

        public static int TextWidth(string text, int px) => string.IsNullOrEmpty(text) ? 0 : (text.Length * 6 - 1) * px;

        public void Text(string text, int x, int y, int px, Color32 color, bool shadow = true)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (shadow)
            {
                DrawTextPass(text, x + 1, y + 1, px, Shadow);
            }

            DrawTextPass(text, x, y, px, color);
        }

        private void DrawTextPass(string text, int x, int y, int px, Color32 color)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (!Font.TryGetValue(text[i], out string[] glyph))
                {
                    glyph = Font[' '];
                }

                int gx = x + i * 6 * px;
                for (int r = 0; r < 7; r++)
                {
                    string row = glyph[r];
                    for (int c = 0; c < 5; c++)
                    {
                        if (row[c] == '1')
                        {
                            FillRectPixels(gx + c * px, y + r * px, px, px, color);
                        }
                    }
                }
            }
        }

        // ---------- Постобработка PS1 ----------

        /// <summary>Дизеринг Байера 4×4 + квантование 5 бит на канал, как в прототипе.</summary>
        public void ApplyPs1(float strength)
        {
            for (int y = 0; y < H; y++)
            {
                int yOffset = y * W;
                for (int x = 0; x < W; x++)
                {
                    int idx = yOffset + x;
                    Color32 c = Pixels[idx];
                    float b = (Bayer4[y & 3, x & 3] / 15f - 0.5f) * strength;
                    Pixels[idx] = new Color32(Quantize(c.r + b), Quantize(c.g + b), Quantize(c.b + b), c.a);
                }
            }
        }

        private static byte Quantize(float value)
        {
            float v = value < 0f ? 0f : value > 255f ? 255f : value;
            int i = (int)v;
            return (byte)((i >> 3) << 3);
        }

        public void Upload(Texture2D texture)
        {
            for (int y = 0; y < H; y++)
            {
                Array.Copy(Pixels, y * W, upload, (H - 1 - y) * W, W);
            }

            texture.SetPixels32(upload);
            texture.Apply(false, false);
        }

        // ---------- Служебное ----------

        private static int RoundInt(float value) => Mathf.FloorToInt(value + 0.5f);

        private void Plot(int x, int y, Color32 color)
        {
            if (x < 0 || x >= W || y < 0 || y >= H)
            {
                return;
            }

            Blend(y * W + x, color);
        }

        private void Blend(int index, Color32 src)
        {
            if (src.a == 255)
            {
                Pixels[index] = src;
                return;
            }

            if (src.a == 0)
            {
                return;
            }

            Color32 dst = Pixels[index];
            int a = src.a;
            int inv = 255 - a;
            Pixels[index] = new Color32(
                (byte)((src.r * a + dst.r * inv) / 255),
                (byte)((src.g * a + dst.g * inv) / 255),
                (byte)((src.b * a + dst.b * inv) / 255),
                (byte)(a + dst.a * inv / 255));
        }

        private readonly struct Matrix2D
        {
            public static readonly Matrix2D Identity = new Matrix2D(1f, 0f, 0f, 1f, 0f, 0f);

            private readonly float a;
            private readonly float b;
            private readonly float c;
            private readonly float d;
            private readonly float e;
            private readonly float f;

            private Matrix2D(float a, float b, float c, float d, float e, float f)
            {
                this.a = a;
                this.b = b;
                this.c = c;
                this.d = d;
                this.e = e;
                this.f = f;
            }

            public bool IsIdentity =>
                Mathf.Approximately(a, 1f) && Mathf.Approximately(b, 0f) &&
                Mathf.Approximately(c, 0f) && Mathf.Approximately(d, 1f) &&
                Mathf.Approximately(e, 0f) && Mathf.Approximately(f, 0f);

            public Vector2 Map(float x, float y) => new Vector2(a * x + c * y + e, b * x + d * y + f);

            public static Matrix2D Translation(float x, float y) => new Matrix2D(1f, 0f, 0f, 1f, x, y);

            public static Matrix2D Scale(float s) => new Matrix2D(s, 0f, 0f, s, 0f, 0f);

            public static Matrix2D Rotation(float radians)
            {
                float cos = Mathf.Cos(radians);
                float sin = Mathf.Sin(radians);
                return new Matrix2D(cos, sin, -sin, cos, 0f, 0f);
            }

            public static Matrix2D Multiply(Matrix2D m, Matrix2D n) => new Matrix2D(
                m.a * n.a + m.c * n.b,
                m.b * n.a + m.d * n.b,
                m.a * n.c + m.c * n.d,
                m.b * n.c + m.d * n.d,
                m.a * n.e + m.c * n.f + m.e,
                m.b * n.e + m.d * n.f + m.f);
        }

        // ---------- Пиксельный шрифт 5×7 ----------

        private static readonly Dictionary<char, string[]> Font = new Dictionary<char, string[]>
        {
            ['А'] = new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" },
            ['Б'] = new[] { "11111", "10000", "10000", "11110", "10001", "10001", "11110" },
            ['В'] = new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" },
            ['Г'] = new[] { "11111", "10000", "10000", "10000", "10000", "10000", "10000" },
            ['Д'] = new[] { "00110", "01010", "10010", "10010", "10010", "11111", "10001" },
            ['Е'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" },
            ['Ё'] = new[] { "01010", "11111", "10000", "11110", "10000", "10000", "11111" },
            ['Ж'] = new[] { "10101", "10101", "01110", "00100", "01110", "10101", "10101" },
            ['З'] = new[] { "01110", "10001", "00001", "00110", "00001", "10001", "01110" },
            ['И'] = new[] { "10001", "10001", "10011", "10101", "11001", "10001", "10001" },
            ['Й'] = new[] { "01110", "10001", "10011", "10101", "11001", "10001", "10001" },
            ['К'] = new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" },
            ['Л'] = new[] { "00111", "01001", "10001", "10001", "10001", "10001", "10001" },
            ['М'] = new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" },
            ['Н'] = new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" },
            ['О'] = new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" },
            ['П'] = new[] { "11111", "10001", "10001", "10001", "10001", "10001", "10001" },
            ['Р'] = new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" },
            ['С'] = new[] { "01110", "10001", "10000", "10000", "10000", "10001", "01110" },
            ['Т'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" },
            ['У'] = new[] { "10001", "10001", "10001", "01111", "00001", "10001", "01110" },
            ['Ф'] = new[] { "01110", "10101", "10101", "10101", "01110", "00100", "00100" },
            ['Х'] = new[] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" },
            ['Ц'] = new[] { "10010", "10010", "10010", "10010", "10010", "11111", "00001" },
            ['Ч'] = new[] { "10001", "10001", "10001", "01111", "00001", "00001", "00001" },
            ['Ш'] = new[] { "10101", "10101", "10101", "10101", "10101", "10101", "11111" },
            ['Щ'] = new[] { "10101", "10101", "10101", "10101", "10101", "11111", "00001" },
            ['Ъ'] = new[] { "11000", "01000", "01000", "01110", "01001", "01001", "01110" },
            ['Ы'] = new[] { "10001", "10001", "10001", "11101", "10011", "10011", "11101" },
            ['Ь'] = new[] { "10000", "10000", "10000", "11110", "10001", "10001", "11110" },
            ['Э'] = new[] { "01110", "10001", "00001", "00111", "00001", "10001", "01110" },
            ['Ю'] = new[] { "10110", "10101", "10101", "11101", "10101", "10101", "10110" },
            ['Я'] = new[] { "01111", "10001", "10001", "01111", "00101", "01001", "10001" },
            ['0'] = new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" },
            ['1'] = new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" },
            ['2'] = new[] { "01110", "10001", "00001", "00110", "01000", "10000", "11111" },
            ['3'] = new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" },
            ['4'] = new[] { "00100", "01100", "10100", "11111", "00100", "00100", "00100" },
            ['5'] = new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" },
            ['6'] = new[] { "00110", "01000", "10000", "11110", "10001", "10001", "01110" },
            ['7'] = new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
            ['8'] = new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
            ['9'] = new[] { "01110", "10001", "10001", "01111", "00001", "00010", "01100" },
            ['!'] = new[] { "00100", "00100", "00100", "00100", "00100", "00000", "00100" },
            ['.'] = new[] { "00000", "00000", "00000", "00000", "00000", "00000", "00100" },
            [','] = new[] { "00000", "00000", "00000", "00000", "00000", "00100", "01000" },
            ['-'] = new[] { "00000", "00000", "00000", "11111", "00000", "00000", "00000" },
            ['—'] = new[] { "00000", "00000", "00000", "11111", "00000", "00000", "00000" },
            ['·'] = new[] { "00000", "00000", "00000", "00100", "00000", "00000", "00000" },
            ['['] = new[] { "01110", "01000", "01000", "01000", "01000", "01000", "01110" },
            [']'] = new[] { "01110", "00010", "00010", "00010", "00010", "00010", "01110" },
            ['E'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" },
            ['S'] = new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" },
            ['C'] = new[] { "01110", "10001", "10000", "10000", "10000", "10001", "01110" },
            [' '] = new[] { "00000", "00000", "00000", "00000", "00000", "00000", "00000" },
        };
    }
}
