using System;
using UnityEngine;

public sealed partial class FuelingMinigame
{
    // ---------- Фон ----------

    private void BuildBackground()
    {
        background.ResetTransform();
        if (currentCar.IsExternalTank)
        {
            BuildTruckExternalTankSidePanel(background);
        }
        else
        {
            BuildPanel(background);
        }

        background.ApplyPs1(5f);
    }

    /// <summary>Борт машины или плоский вид сбоку на бак грузовика — ровно из прототипа.</summary>
    private void BuildTruckExternalTankSidePanel(PixelCanvas c)
    {
        c.FillRect(0, 0, PixelCanvas.W, PixelCanvas.H, Hex(0x090c12));

        const int bedRight = 268;
        const int cabLeft = 274;

        // Деревянный ящик над белым бортом.
        c.FillRect(128, 0, 126, 7, Hex(0x5c4028));
        c.FillRect(130, 1, 122, 2, Hex(0x7a5738));
        c.FillRect(128, 4, 126, 1, Hex(0x422d1b));

        // Белый борт кузова.
        c.FillRect(0, 6, bedRight, 28, Hex(0xc5cdd9));
        c.FillRect(0, 6, bedRight, 2, Hex(0xdfe6f2));
        for (int i = 0; i < 2; i++)
        {
            int py = i == 0 ? 15 : 25;
            c.FillRect(0, py, bedRight, 1, Hex(0x7b8391));
            c.FillRect(0, py + 1, bedRight, 1, Hex(0xdce3ed));
        }

        // Грязные потёки на белом борту.
        for (int x = 3; x < bedRight - 2; x += 3)
        {
            double h = Math.Sin(x * 19.3) * 43758.5453;
            double f = h - Math.Floor(h);
            if (f < 0.68)
            {
                int len = 8 + (int)Math.Floor(f * 20);
                Color32 color = f < 0.3 ? Rgba(42, 48, 58, 0.34f) : Rgba(65, 72, 84, 0.22f);
                c.FillRect(x, 7, 2, Math.Min(26, len), color);
            }
        }

        // Рифлёный отбойник.
        c.FillRect(0, 34, bedRight, 16, Hex(0x232832));
        for (int ry = 36; ry <= 47; ry += 4)
        {
            c.FillRect(0, ry, bedRight, 2, Hex(0x3c4352));
            c.FillRect(0, ry + 2, bedRight, 1, Hex(0x14171d));
        }

        // Зазор и задняя часть белой кабины.
        c.FillRect(bedRight, 0, cabLeft - bedRight, 148, Hex(0x06080d));
        c.FillRect(cabLeft, 0, PixelCanvas.W - cabLeft, 146, Hex(0xb8c0cc));
        c.FillRect(cabLeft, 0, 2, 146, Hex(0xdce3ed));
        c.FillRect(292, 0, 1, 144, Hex(0x454c59));
        c.FillRect(293, 0, 1, 144, Hex(0xd8e0eb));

        // Потёки и грязь по низу кабины.
        for (int x = cabLeft + 2; x < PixelCanvas.W; x += 3)
        {
            double h = Math.Sin(x * 31.7) * 43758.5453;
            double f = h - Math.Floor(h);
            int streakH = 26 + (int)Math.Floor(f * 70);
            c.FillRect(x, 146 - streakH, 2, streakH, Rgba(45, 50, 60, 0.28f));

            if (f < 0.72)
            {
                int my = 134 + (int)Math.Floor(f * 10);
                c.FillRect(x, my, 2, 2, x % 2 == 0 ? Hex(0x5e473a) : Hex(0xd6dde8));
            }

            c.FillRect(x, 142, 2, 3, x % 3 == 0 ? Hex(0x5e473a) : Hex(0xcfd7e3));
        }

        // Рама и заднее крыло.
        c.FillRect(0, 50, bedRight, 8, Hex(0x05070b));
        c.FillRect(0, 58, PixelCanvas.W, 56, Hex(0x111621));
        c.FillRect(0, 58, bedRight, 2, Hex(0x1c2333));
        c.FillRect(0, 54, 26, 76, Hex(0x12161f));
        c.Disc(-14, 152, 46, 14, Hex(0x171c26));
        c.Disc(-14, 152, 34, 14, Hex(0x0d1017));

        // Кронштейны бака.
        DrawSideTopBracket(c, 90);
        DrawSideTopBracket(c, 202);

        // Чёрный топливный бак.
        const int tx = 48;
        const int ty = 58;
        const int tw = 200;
        const int th = 104;

        c.FillRect(tx + 6, ty + th - 2, tw - 8, 8, Rgba(3, 5, 8, 0.78f));

        Vector2[] tankShape =
        {
            V(tx + 10, ty), V(tx + tw - 10, ty), V(tx + tw, ty + 10), V(tx + tw, ty + th - 10),
            V(tx + tw - 10, ty + th), V(tx + 10, ty + th), V(tx, ty + th - 10), V(tx, ty + 10),
        };
        c.PolyFill(tankShape, Hex(0x1b212c), Hex(0x090c12));

        c.FillRect(tx + 4, ty + 3, tw - 8, 15, Hex(0x2d3647));
        c.FillRect(tx + 6, ty + 6, tw - 12, 8, Hex(0x46536b));
        c.FillRect(tx + 8, ty + 8, tw - 16, 3, Hex(0x647491));
        c.FillRect(tx + 2, ty + 46, tw - 4, 24, Hex(0x141922));
        c.FillRect(tx + 2, ty + 70, tw - 4, 16, Hex(0x212836));
        c.FillRect(tx + 3, ty + 86, tw - 6, 12, Hex(0x10141c));
        c.FillRect(tx + 8, ty + th - 6, tw - 16, 5, Hex(0x0a0d12));

        int[] seams = { tx + 14, tx + tw - 15 };
        for (int i = 0; i < seams.Length; i++)
        {
            int seamX = seams[i];
            c.FillRect(seamX, ty + 2, 2, th - 4, Hex(0x0a0d13));
            c.FillRect(seamX + 2, ty + 3, 1, th - 6, Hex(0x3c475c));
        }

        c.StrokeRect(102.5f, ty + 22.5f, 87f, th - 36f, Hex(0x10141c));
        c.StrokeRect(103.5f, ty + 23.5f, 85f, th - 38f, Hex(0x2b3446));

        c.Disc(HubX, HubY, HoleR + 10f, 14, Hex(0x121721));
        c.Disc(HubX, HubY, HoleR + 8f, 14, Hex(0x252e3f));

        DrawSideStrap(c, 90, ty, th);
        DrawSideStrap(c, 202, ty, th);

        // Шум и следы солярки на баке.
        for (int y = ty + 2; y < ty + th - 2; y += 2)
        {
            for (int x = tx + 2; x < tx + tw - 2; x += 2)
            {
                double h = Math.Sin(x * 12.9898 + y * 78.233) * 43758.5453;
                double f = h - Math.Floor(h);
                if (f < 0.14)
                {
                    c.FillRect(x, y, 2, 2, Rgba(8, 10, 16, 0.20f));
                }
                else if (f > 0.90)
                {
                    c.FillRect(x, y, 2, 2, Rgba(180, 200, 235, 0.05f));
                }
            }
        }
    }

    private static void DrawSideTopBracket(PixelCanvas c, int bx)
    {
        c.FillRect(bx - 9, 50, 18, 12, Hex(0x141924));
        c.FillRect(bx - 7, 52, 14, 4, Hex(0x2c3447));
        c.FillRect(bx - 5, 53, 2, 2, Hex(0x707f99));
        c.FillRect(bx + 3, 53, 2, 2, Hex(0x707f99));
    }

    private static void DrawSideStrap(PixelCanvas c, int sx, int ty, int th)
    {
        c.FillRect(sx - 6, ty - 1, 12, th + 2, Hex(0x080a0f));
        c.FillRect(sx - 4, ty - 1, 8, th + 2, Hex(0x2b3344));
        c.FillRect(sx - 4, ty + 5, 8, 7, Hex(0x8797b3));
        c.FillRect(sx - 3, ty + 22, 6, 18, Hex(0x5c6b85));
        c.FillRect(sx - 3, ty + 42, 6, 18, Hex(0x151922));
        c.FillRect(sx - 3, ty + 64, 6, 22, Hex(0x4d5a70));
        c.FillRect(sx - 4, ty, 1, th, Hex(0x465269));
        c.FillRect(sx + 3, ty, 1, th, Hex(0x10141c));
        c.FillRect(sx - 7, ty + th, 14, 9, Hex(0x12161f));
        c.FillRect(sx - 3, ty + th - 2, 6, 9, Hex(0x7685a1));
        c.FillRect(sx - 1, ty + th + 2, 2, 3, Hex(0x141821));
    }

    private void BuildPanel(PixelCanvas c)
    {
        int sy = currentCar.SkirtY > 0 ? currentCar.SkirtY : 164;

        c.FillRect(0, 0, PixelCanvas.W, 28, currentCar.TopFrame);

        if (currentCar.Id == SuvGreenId)
        {
            DrawSideWindowStrip(c, 14, 190, 0, 22);
            DrawSideWindowStrip(c, 216, PixelCanvas.W, 0, 22);
            c.FillRect(190, 0, 26, 26, Hex(0x2b3926));
        }
        else if (currentCar.Id == PickupBlueId)
        {
            c.FillRect(0, 0, currentCar.SeamX, 26, Hex(0x111622));
            DrawWoodCrate(c, 96, 0, 92, 26, Hex(0xa68c46), Hex(0xc2a85d), Hex(0x735e28));
            DrawWoodCrate(c, 32, 6, 68, 20, Hex(0x997f3d), Hex(0xb59b52), Hex(0x665222));
            DrawWoodCrate(c, 82, 11, 76, 15, Hex(0xb0954c), Hex(0xccb164), Hex(0x78622a));
            DrawWoodCrate(c, 162, 13, 74, 13, Hex(0x593b25), Hex(0x755035), Hex(0x382314));
            c.FillRect(currentCar.SeamX, 0, PixelCanvas.W - currentCar.SeamX, 28, Hex(0x314d7d));
            DrawSideWindowStrip(c, currentCar.SeamX + 12, PixelCanvas.W, 0, 22);
        }
        else if (currentCar.Id == VanPostId)
        {
            c.FillRect(0, 0, PixelCanvas.W, 28, Hex(0xcfd7e3));
            DrawSideWindowStrip(c, currentCar.SeamX + 14, PixelCanvas.W, 0, 24);
        }
        else if (currentCar.Id == HatchRedId)
        {
            c.FillRect(0, 0, 28, 26, Hex(0x111622));
            Vector2[] slope = { V(8, 28), V(26, 0), V(PixelCanvas.W, 0), V(PixelCanvas.W, 28) };
            c.PolyFill(slope, Hex(0x872e33));
            DrawSideWindowStrip(c, 34, 206, 0, 22);
            DrawSideWindowStrip(c, 230, PixelCanvas.W, 0, 22);
        }

        // Горизонтальные полосы кузова.
        c.FillRect(0, 28, PixelCanvas.W, 28, currentCar.Bands[0]);
        c.FillRect(0, 56, PixelCanvas.W, 50, currentCar.Bands[1]);
        c.FillRect(0, 106, PixelCanvas.W, 30, currentCar.Bands[2]);
        c.FillRect(0, 136, PixelCanvas.W, sy - 136, currentCar.Bands[3]);
        c.FillRect(0, sy, PixelCanvas.W, PixelCanvas.H - sy, currentCar.Skirt);

        for (int i = 0; i < currentCar.Stripes.Length; i++)
        {
            StripeDef stripe = currentCar.Stripes[i];
            c.FillRect(0, stripe.Y, PixelCanvas.W, stripe.H, stripe.Color);
        }

        DrawCarDetails(c);

        // Текстура краски и потёков.
        for (int y = 29; y < sy; y += 2)
        {
            for (int x = 0; x < PixelCanvas.W; x += 2)
            {
                double h = Math.Sin(x * 12.9898 + y * 78.233) * 43758.5453;
                double f = h - Math.Floor(h);

                if (currentCar.Grain == GrainMode.Vertical)
                {
                    double vx = Math.Sin(x * 41.3) * 0.5 + 0.5;
                    if (vx > 0.72 && f < 0.42)
                    {
                        c.FillRect(x, y, 2, 6, Rgba(18, 28, 14, 0.15f));
                    }
                    else if (f > 0.88)
                    {
                        c.FillRect(x, y, 2, 4, Rgba(190, 220, 175, 0.07f));
                    }
                }
                else if (currentCar.Grain == GrainMode.Horizontal)
                {
                    double hy = Math.Sin(y * 37.7) * 0.5 + 0.5;
                    if (hy > 0.68 && f < 0.34)
                    {
                        c.FillRect(x, y, 8, 2, Rgba(230, 240, 255, 0.07f));
                    }
                    else if (f < 0.14)
                    {
                        c.FillRect(x, y, 4, 2, Rgba(10, 14, 24, 0.14f));
                    }
                }
                else
                {
                    double streak = Math.Sin(x * 23.7);
                    if (streak > 0.65 && y > 96 && f < 0.45)
                    {
                        c.FillRect(x, y, 2, 8, Rgba(50, 58, 72, 0.13f));
                    }
                    else if (f < 0.10)
                    {
                        c.FillRect(x, y, 2, 2, Rgba(30, 38, 52, 0.07f));
                    }
                }
            }
        }

        // Брызги грязи по порогу.
        if (currentCar.HasMud)
        {
            for (int x = 78; x < PixelCanvas.W; x += 3)
            {
                double r1 = Math.Sin(x * 91.7) * 43758.5453;
                double frac = r1 - Math.Floor(r1);
                if (frac < 0.62)
                {
                    int py = sy - 11 + (int)Math.Floor(frac / 0.62 * 9);
                    c.FillRect(x, py, 2, 2, frac < 0.32 ? currentCar.MudColor1 : currentCar.MudColor2);
                }

                if (frac > 0.72)
                {
                    c.FillRect(x, sy - 4, 3, 3, x % 2 == 0 ? currentCar.MudColor1 : currentCar.MudColor2);
                }
            }
        }

        // Верхняя грань крыла.
        if (currentCar.Id != VanPostId)
        {
            c.FillRect(0, 27, PixelCanvas.W, 1, Hex(0x10151f));
            c.FillRect(0, 28, PixelCanvas.W, 1, currentCar.RidgeLight);
            c.FillRect(0, 29, PixelCanvas.W, 1, currentCar.RidgeDark);
        }
        else
        {
            c.FillRect(currentCar.SeamX, 27, PixelCanvas.W - currentCar.SeamX, 1, Hex(0x6e7887));
            c.FillRect(currentCar.SeamX, 28, PixelCanvas.W - currentCar.SeamX, 1, Hex(0xe8effa));
        }

        // Нижний молдинг.
        c.FillRect(0, sy, PixelCanvas.W, 1, Hex(0x0d121d));
        c.FillRect(0, sy + 1, PixelCanvas.W, 1, currentCar.RidgeDark);

        // Шов двери.
        int seamX = currentCar.SeamX > 0 ? currentCar.SeamX : 236;
        int seamTop = currentCar.Id == VanPostId ? 6 : 28;
        c.FillRect(seamX, seamTop, 2, sy - seamTop, currentCar.SeamDark);
        c.FillRect(seamX + 2, seamTop, 1, sy - seamTop, currentCar.SeamLight);
        if (currentCar.Id == VanPostId)
        {
            c.FillRect(seamX, 6, PixelCanvas.W - seamX, 1, currentCar.SeamDark);
        }

        if (currentCar.HasHandle)
        {
            int hx = currentCar.HandleX;
            int hy = currentCar.HandleY;
            c.FillRect(hx - 1, hy - 1, 16, 6, Hex(0x0b0e14));
            c.FillRect(hx, hy, 14, 4, Hex(0x1d232e));
            c.FillRect(hx, hy, 14, 1, Hex(0x434e63));
        }

        DrawWheelArch(c, sy);
    }

    private void DrawCarDetails(PixelCanvas c)
    {
        if (currentCar.Id == SuvGreenId)
        {
            const int ox = 258;
            const int oy = 84;
            Vector2[] oval =
            {
                V(ox - 24, oy - 3), V(ox - 18, oy - 10), V(ox - 7, oy - 13), V(ox + 7, oy - 13),
                V(ox + 18, oy - 10), V(ox + 24, oy - 3), V(ox + 24, oy + 3), V(ox + 18, oy + 10),
                V(ox + 7, oy + 13), V(ox - 7, oy + 13), V(ox - 18, oy + 10), V(ox - 24, oy + 3),
            };
            c.PolyFill(oval, Hex(0xd6dfd2), Hex(0x2b3b27));

            Vector2[] treeTop = { V(ox, oy - 10), V(ox + 8, oy - 3), V(ox - 8, oy - 3) };
            Vector2[] treeMid = { V(ox, oy - 6), V(ox + 11, oy + 2), V(ox - 11, oy + 2) };
            Vector2[] treeLow = { V(ox, oy - 1), V(ox + 14, oy + 6), V(ox - 14, oy + 6) };
            c.PolyFill(treeTop, Hex(0x253b22));
            c.PolyFill(treeMid, Hex(0x253b22));
            c.PolyFill(treeLow, Hex(0x253b22));
            c.FillRect(ox - 2, oy + 6, 4, 4, Hex(0x253b22));
        }
        else if (currentCar.Id == VanPostId)
        {
            c.Text("ПОЧТА", 18, 22, 3, Hex(0x1f3d7a), false);
            c.Text("ПОЧТА", 17, 21, 3, Hex(0x25478c), false);
        }
        else if (currentCar.Id == HatchRedId)
        {
            const int bx = 34;
            const int by = 60;
            const int bw = 58;
            const int bh = 32;
            c.FillRect(bx - 1, by - 1, bw + 2, bh + 2, Hex(0x2b1215));
            c.FillRect(bx, by, bw, bh, Hex(0xdce2eb));

            Vector2[] mountain =
            {
                V(bx + 5, by + bh - 5), V(bx + 23, by + 8), V(bx + 33, by + 17),
                V(bx + 40, by + 12), V(bx + bw - 5, by + bh - 5),
            };
            c.PolyFill(mountain, Hex(0x254d8a));

            Vector2[] snow = { V(bx + 23, by + 8), V(bx + 27, by + 13), V(bx + 23, by + 15), V(bx + 19, by + 13) };
            c.PolyFill(snow, Hex(0xdce2eb));
        }
    }

    private void DrawWheelArch(PixelCanvas c, int sy)
    {
        if (currentCar.ArchBoxy)
        {
            Vector2[] outer = { V(0, 122), V(68, 122), V(88, 144), V(88, PixelCanvas.H), V(0, PixelCanvas.H) };
            c.PolyFill(outer, currentCar.ArchOuter);
            Vector2[] shelf = { V(0, 122), V(68, 122), V(86, 142), V(78, 144), V(62, 127), V(0, 127) };
            c.PolyFill(shelf, Hex(0x2d333f));
            Vector2[] niche = { V(0, 134), V(58, 134), V(76, 154), V(76, PixelCanvas.H), V(0, PixelCanvas.H) };
            c.PolyFill(niche, Hex(0x080b10));
            c.Disc(18, 194, 48, 14, Hex(0x1e242e));
            c.Disc(18, 194, 28, 12, Hex(0x8c96a6));
            return;
        }

        const int archX = 10;
        const int archY = 196;
        int outerR = currentCar.Id == VanPostId ? 64 : 74;
        c.Disc(archX, archY, outerR, 14, currentCar.ArchOuter);
        c.Disc(archX, archY, outerR - 8, 14, Hex(0x080b10));
        c.Disc(archX, archY, outerR - 18, 14, Hex(0x1c222b));
        c.Disc(archX, archY, outerR - 34, 12, Hex(0x828c9c));
    }

    private static void DrawSideWindowStrip(PixelCanvas c, int x1, int x2, int y1, int y2)
    {
        c.FillRect(x1, y1, x2 - x1, y2 - y1, Hex(0x141820));

        int gx = x1 + 2;
        int gy = y1;
        int gw = Math.Max(2, x2 - x1 - 4);
        int gh = Math.Max(2, y2 - y1 - 2);

        c.FillRect(gx, gy, gw, gh, Hex(0x26354a));
        c.FillRect(gx, gy, gw, Mathf.FloorToInt(gh * 0.55f), Hex(0x354a66));
        c.FillRect(gx, gy, gw, Mathf.FloorToInt(gh * 0.22f), Hex(0x4b668a));
    }

    private static void DrawWoodCrate(PixelCanvas c, int cx, int cy, int cw, int ch,
        Color32 main, Color32 light, Color32 dark)
    {
        c.FillRect(cx, cy, cw, ch, main);
        c.FillRect(cx + 1, cy + 1, cw - 2, 2, light);
        for (int py = cy + 5; py < cy + ch; py += 5)
        {
            c.FillRect(cx, py, cw, 1, dark);
        }

        c.StrokeRect(cx + 0.5f, cy + 0.5f, cw - 1, ch - 1, Hex(0x2b2111));
        c.FillRect(cx + 2, cy + 2, 5, ch - 4, dark);
        c.FillRect(cx + cw - 7, cy + 2, 5, ch - 4, dark);
    }

    private static Vector2 V(float x, float y) => new Vector2(x, y);
}
