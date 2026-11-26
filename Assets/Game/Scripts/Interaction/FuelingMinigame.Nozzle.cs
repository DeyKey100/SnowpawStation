using UnityEngine;

public sealed partial class FuelingMinigame
{
    // ---------- Геометрия пистолета (локальные координаты из прототипа) ----------

    private static readonly Vector2[] NozBarrelA =
    {
        new Vector2(-51, -47), new Vector2(-35, -40), new Vector2(-19, -30),
        new Vector2(-23, -24), new Vector2(-39, -34), new Vector2(-54, -41),
    };

    private static readonly Vector2[] NozBarrelB =
    {
        new Vector2(-51, -47), new Vector2(-35, -40), new Vector2(-19, -30),
        new Vector2(-20, -28), new Vector2(-36, -38), new Vector2(-52, -45),
    };

    private static readonly Vector2[] NozBarrelC =
    {
        new Vector2(-54, -41), new Vector2(-39, -34), new Vector2(-23, -24),
        new Vector2(-22, -26), new Vector2(-38, -36), new Vector2(-53, -43),
    };

    private static readonly Vector2[] NozBarrelTip =
    {
        new Vector2(-51, -47), new Vector2(-45, -44), new Vector2(-48, -38), new Vector2(-54, -41),
    };

    private static readonly Vector2[] NozBandA =
    {
        new Vector2(-30, -38), new Vector2(-27, -36), new Vector2(-32, -28), new Vector2(-35, -30),
    };

    private static readonly Vector2[] NozBandB =
    {
        new Vector2(-25, -35), new Vector2(-22, -33), new Vector2(-27, -25), new Vector2(-30, -27),
    };

    private static readonly Vector2[] NozCouplingA =
    {
        new Vector2(-19, -32), new Vector2(-11, -27), new Vector2(-18, -16), new Vector2(-25, -22),
    };

    private static readonly Vector2[] NozCouplingB =
    {
        new Vector2(-19, -32), new Vector2(-11, -27), new Vector2(-13, -24), new Vector2(-21, -29),
    };

    private static readonly Vector2[] NozGuardA =
    {
        new Vector2(-17, -16), new Vector2(-11, -13), new Vector2(-9, 10), new Vector2(10, 17),
        new Vector2(16, 13), new Vector2(18, 18), new Vector2(9, 22), new Vector2(-3, 18),
        new Vector2(-6, 24), new Vector2(-10, 23), new Vector2(-9, 16), new Vector2(-15, 11),
    };

    private static readonly Vector2[] NozGuardInnerA =
    {
        new Vector2(-10, -11), new Vector2(7, -4), new Vector2(12, 9), new Vector2(6, 13), new Vector2(-8, 8),
    };

    private static readonly Vector2[] NozGuardInnerB =
    {
        new Vector2(-4, 12), new Vector2(4, 15), new Vector2(0, 18),
    };

    private static readonly Vector2[] NozTriggerA =
    {
        new Vector2(-5, -9), new Vector2(0, -7), new Vector2(3, 6), new Vector2(-1, 8), new Vector2(-5, 0),
    };

    private static readonly Vector2[] NozTriggerB =
    {
        new Vector2(4, 5), new Vector2(11, 4), new Vector2(12, 8), new Vector2(5, 9),
    };

    private static readonly Vector2[] NozBodyA =
    {
        new Vector2(-11, -27), new Vector2(1, -24), new Vector2(13, -15), new Vector2(16, -4),
        new Vector2(26, 26), new Vector2(16, 30), new Vector2(8, 8), new Vector2(4, -4),
        new Vector2(-11, -12), new Vector2(-18, -16),
    };

    private static readonly Vector2[] NozBodyLight =
    {
        new Vector2(-11, -27), new Vector2(1, -24), new Vector2(13, -15), new Vector2(15, -7),
        new Vector2(10, -9), new Vector2(0, -18), new Vector2(-13, -22),
    };

    private static readonly Vector2[] NozBodyDark =
    {
        new Vector2(-13, -17), new Vector2(6, -9), new Vector2(13, 1), new Vector2(22, 27),
        new Vector2(16, 29), new Vector2(9, 8), new Vector2(4, -4), new Vector2(-12, -12),
    };

    private static readonly Vector2[] NozGripLine =
    {
        new Vector2(-13, -17), new Vector2(6, -9), new Vector2(13, 1), new Vector2(22, 27),
    };

    private static readonly Vector2[] NozSwivelA =
    {
        new Vector2(16, 30), new Vector2(26, 26), new Vector2(28, 32), new Vector2(18, 36),
    };

    private static readonly Vector2[] NozSwivelB =
    {
        new Vector2(18, 29), new Vector2(23, 27), new Vector2(25, 32), new Vector2(20, 34),
    };

    private static readonly Vector2[] NozSwivelC =
    {
        new Vector2(19, 35), new Vector2(27, 32), new Vector2(30, 40), new Vector2(22, 43),
    };

    private static readonly Vector2[] NozSwivelD =
    {
        new Vector2(21, 35), new Vector2(25, 33), new Vector2(28, 40), new Vector2(24, 42),
    };

    private static readonly Vector2[] NozHighlight =
    {
        new Vector2(-11, -28), new Vector2(2, -25), new Vector2(14, -15), new Vector2(17, -4),
        new Vector2(27, 26), new Vector2(15, 31),
    };

    // ---------- Пистолет сбоку ----------

    private void DrawNozzleSide(PixelCanvas c, float tipX, float tipY, float rot, float scale, bool highlight)
    {
        c.Push();
        c.Translate(Mathf.Round(tipX), Mathf.Round(tipY));
        c.Rotate(rot);
        c.Scale(scale);
        c.Translate(-NozTip.x, -NozTip.y);

        // 1. Металлическая трубка (ствол).
        Poly(c, NozBarrelA, Hex(0x99a7bd), Hex(0x141a24));
        Poly(c, NozBarrelB, Hex(0xd4e0f2));
        Poly(c, NozBarrelC, Hex(0x5b677a));
        Poly(c, NozBarrelTip, Hex(0x525c6e), Hex(0x141a24));
        Poly(c, NozBandA, Hex(0xdce6f7), Hex(0x252e40));
        Poly(c, NozBandB, Hex(0xdce6f7), Hex(0x252e40));

        // 2. Чёрная муфта-переходник.
        Poly(c, NozCouplingA, Hex(0x1c222e), Hex(0x0b0e14));
        Poly(c, NozCouplingB, Hex(0x343e52));

        // 3. Скоба защиты курка.
        Poly(c, NozGuardA, Hex(0x171c26), Hex(0x090c12));
        Poly(c, NozGuardInnerA, Hex(0x0b0f17));
        Poly(c, NozGuardInnerB, Hex(0x0b0f17));
        Poly(c, NozTriggerA, Hex(0xb8c5d9), Hex(0x171c26));
        Poly(c, NozTriggerB, Hex(0x8c9ab0), Hex(0x171c26));

        // 4. Основной корпус и прямая рукоятка.
        Poly(c, NozBodyA, Hex(0x232a38), Hex(0x0b0e14));
        Poly(c, NozBodyLight, Hex(0x3d485e));
        Poly(c, NozBodyDark, Hex(0x181d28));
        c.StrokePolyline(NozGripLine, Hex(0x48556e), false);

        for (int i = 0; i < 4; i++)
        {
            c.FillRect(16f + i * 2f, 4f + i * 5f, 5f, 2f, Hex(0x10141c));
        }

        // 5. Двухступенчатый стальной поворотный штуцер.
        Poly(c, NozSwivelA, Hex(0xb5c2d6), Hex(0x141a24));
        Poly(c, NozSwivelB, Hex(0xdce6f7));
        Poly(c, NozSwivelC, Hex(0x8c9ab0), Hex(0x141a24));
        Poly(c, NozSwivelD, Hex(0xc8d4e8));
        c.FillRect(25f, 35f, 2f, 5f, Hex(0x495366));

        if (highlight)
        {
            c.StrokePolyline(NozHighlight, capOpen ? Cream : Bad, false);
        }

        c.Pop();
    }

    // ---------- Пистолет в горловине ----------

    private static readonly Vector2[] PlugShadow =
    {
        new Vector2(-6, 6), new Vector2(20, 4), new Vector2(46, 44), new Vector2(24, 56), new Vector2(2, 22),
    };

    private static readonly Vector2[] PlugTipA =
    {
        new Vector2(-5, 0), new Vector2(2, -5), new Vector2(10, 6), new Vector2(3, 11),
    };

    private static readonly Vector2[] PlugTipB =
    {
        new Vector2(-2, -2), new Vector2(2, -5), new Vector2(9, 5), new Vector2(5, 8),
    };

    private static readonly Vector2[] PlugTipC =
    {
        new Vector2(0, 7), new Vector2(7, 2), new Vector2(9, 5), new Vector2(2, 10),
    };

    private static readonly Vector2[] PlugBracket =
    {
        new Vector2(4, 17), new Vector2(11, 16), new Vector2(22, 34), new Vector2(29, 33),
        new Vector2(31, 38), new Vector2(19, 40), new Vector2(13, 31), new Vector2(10, 36),
        new Vector2(6, 34),
    };

    private static readonly Vector2[] PlugTrigger =
    {
        new Vector2(10, 20), new Vector2(15, 19), new Vector2(20, 29), new Vector2(15, 30),
    };

    private static readonly Vector2[] PlugBodyShape =
    {
        new Vector2(3, 14), new Vector2(15, 5), new Vector2(28, 17), new Vector2(28, 28),
        new Vector2(18, 35), new Vector2(9, 26),
    };

    private static readonly Vector2[] PlugBodyLight =
    {
        new Vector2(6, 13), new Vector2(15, 7), new Vector2(25, 17), new Vector2(21, 23), new Vector2(12, 19),
    };

    private static readonly Vector2[] PlugGrip =
    {
        new Vector2(13, 26), new Vector2(22, 19), new Vector2(35, 36), new Vector2(25, 44),
    };

    private static readonly Vector2[] PlugGripLight =
    {
        new Vector2(15, 25), new Vector2(20, 21), new Vector2(33, 38), new Vector2(28, 42),
    };

    private static readonly Vector2[] PlugGripLine =
    {
        new Vector2(17, 23), new Vector2(30, 40),
    };

    private static readonly Vector2[] PlugSwivelA =
    {
        new Vector2(26, 43), new Vector2(34, 37), new Vector2(38, 42), new Vector2(30, 48),
    };

    private static readonly Vector2[] PlugSwivelB =
    {
        new Vector2(28, 42), new Vector2(32, 39), new Vector2(35, 43), new Vector2(31, 46),
    };

    private static readonly Vector2[] PlugSwivelC =
    {
        new Vector2(30, 47), new Vector2(37, 42), new Vector2(41, 48), new Vector2(35, 53),
    };

    private static readonly Vector2[] PlugSwivelD =
    {
        new Vector2(32, 46), new Vector2(35, 44), new Vector2(39, 49), new Vector2(36, 51),
    };

    private void DrawNozzlePlugged(PixelCanvas c)
    {
        c.Push();
        c.Translate(HubX, HubY);

        Poly(c, PlugShadow, Rgba(5, 7, 12, 0.58f));

        Poly(c, PlugTipA, Hex(0x99a7bd), Hex(0x121722));
        Poly(c, PlugTipB, Hex(0xd4dff2));
        Poly(c, PlugTipC, Hex(0xdce6f7), Hex(0x232c3d));

        c.Disc(8f, 11f, 9f, 8, Hex(0x161b24), 0.2f);
        c.Disc(9f, 12f, 7f, 8, Hex(0x252d3d), 0.2f);

        Poly(c, PlugBracket, Hex(0x151a24), Hex(0x090c12));
        Poly(c, PlugTrigger, Hex(0xb8c5d9), Hex(0x141a24));

        Poly(c, PlugBodyShape, Hex(0x222938), Hex(0x0b0e14));
        Poly(c, PlugBodyLight, Hex(0x3c475e));

        c.Disc(17f, 23f, 7f, 8, Hex(0x191f2b), 0.38f);
        c.Ring(17f, 23f, 7f, 8, Hex(0x414e69), 1f, 0.38f);

        Poly(c, PlugGrip, Hex(0x1c222e), Hex(0x0b0e14));
        Poly(c, PlugGripLight, Hex(0x2f384b));

        c.StrokePolyline(PlugGripLine, Hex(0x141821), false);

        for (int i = 0; i < 4; i++)
        {
            float s = 33f + i * 4f;
            float cx = 0.6f * s;
            float cy = 0.8f * s;
            Quad(c, cx - 3.2f, cy + 2.4f, cx + 3.2f, cy - 2.4f, cx + 4.1f, cy - 1.2f, cx - 2.3f, cy + 3.6f,
                Hex(0x121721));
        }

        Poly(c, PlugSwivelA, Hex(0xb8c5d9), Hex(0x141a24));
        Poly(c, PlugSwivelB, Hex(0xe1ebfa));
        Poly(c, PlugSwivelC, Hex(0x8c9ab0), Hex(0x141a24));
        Poly(c, PlugSwivelD, Hex(0xc8d4e8));

        c.Pop();
    }

    private static void Poly(PixelCanvas c, Vector2[] pts, Color32 fill, Color32? stroke = null) =>
        c.PolyFill(pts, pts.Length, fill, stroke);
}
