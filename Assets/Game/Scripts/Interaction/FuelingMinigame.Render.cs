using System;
using UnityEngine;

public sealed partial class FuelingMinigame
{
    // ---------- Переиспользуемые буферы геометрии ----------

    private readonly Vector2[] quad = new Vector2[4];
    private readonly Vector2[] hosePts = new Vector2[13];
    private readonly Vector2[] chainPts = new Vector2[2];
    private readonly Vector2[] gaugeArrow = new Vector2[3];

    private void Render()
    {
        frame.CopyFrom(background);

        DrawFuelWellAndDoor(frame);
        DrawHose(frame);

        if (plugged)
        {
            DrawNozzlePlugged(frame);
        }
        else
        {
            DrawNozzleSide(frame, tipWorld.x, tipWorld.y, carrying ? HandRot : HomeRot, NozzleScale,
                hoverNozzle && !carrying);
        }

        DrawGauge(frame);

        // PS1-пасс по кадру, как в прототипе.
        frame.ApplyPs1(3.5f);

        DrawHint(frame);
        if (resultVisible)
        {
            DrawResultOverlay(frame);
        }

        frame.Upload(texture);
    }

    // ---------- Горловина и лючок ----------

    private void DrawFuelWellAndDoor(PixelCanvas c)
    {
        if (currentCar.IsExternalTank)
        {
            DrawTruckTankCapAndNeck(c);
            return;
        }

        float open = capAnim;
        float wellX = HingeX;
        float wellY = Mathf.Round(HubY - DoorH * 0.5f);

        if (open > 0.02f)
        {
            c.FillRectPixels(Mathf.FloorToInt(wellX - 2f), Mathf.FloorToInt(wellY - 2f),
                Mathf.FloorToInt(DoorW + 4f), Mathf.FloorToInt(DoorH + 4f), Hex(0x080b11));
            c.FillRectPixels(Mathf.FloorToInt(wellX), Mathf.FloorToInt(wellY),
                Mathf.FloorToInt(DoorW), Mathf.FloorToInt(DoorH), Hex(0x121824));

            Quad(c, wellX, wellY, wellX + DoorW, wellY, wellX + DoorW - 4f, wellY + 4f, wellX + 4f, wellY + 4f,
                Hex(0x0a0e16));
            Quad(c, wellX, wellY, wellX + 4f, wellY + 4f, wellX + 4f, wellY + DoorH - 4f, wellX, wellY + DoorH,
                Hex(0x0d121c));
            Quad(c, wellX, wellY + DoorH, wellX + DoorW, wellY + DoorH, wellX + DoorW - 4f, wellY + DoorH - 4f,
                wellX + 4f, wellY + DoorH - 4f, Hex(0x1c2538));
            Quad(c, wellX + DoorW - 4f, wellY + 4f, wellX + DoorW, wellY, wellX + DoorW, wellY + DoorH,
                wellX + DoorW - 4f, wellY + DoorH - 4f, Hex(0x182030));

            c.FillRectPixels(Mathf.FloorToInt(wellX + DoorW - 4f), Mathf.FloorToInt(HubY - 3f), 3, 6,
                Hex(0x263147));

            c.Disc(HubX, HubY, HoleR + 5f, 14, Hex(0x232d42));
            c.Disc(HubX, HubY, HoleR + 2f, 14, Hex(0x313e59));

            DrawBolt(c, -14f, -12f);
            DrawBolt(c, 14f, -12f);
            DrawBolt(c, -14f, 12f);
            DrawBolt(c, 14f, 12f);

            c.Disc(HubX, HubY, HoleR, 14, Hex(0x05070b));
            c.Disc(HubX + 1f, HubY + 1f, HoleR - 4f, 12, Hex(0x020305));
            c.Ring(HubX, HubY, HoleR - 3f, 14, Hex(0x1e2738));

            if (carrying)
            {
                Color32 ringColor = hoverNeck ? Ember : Rgba(240, 222, 186, 0.25f);
                c.Ring(HubX, HubY, HoleR + 1f, 14, ringColor);
            }
        }

        const float maxAngle = 1.98f;
        float angle = open * maxAngle;
        float cosA = Mathf.Cos(angle);
        float sinA = Mathf.Sin(angle);

        float freeX = HingeX + Mathf.Round(DoorW * cosA);
        float perspY = Mathf.Round(sinA * 5f);
        float thickW = Mathf.Max(1f, Mathf.Round(sinA * DoorThick));

        if (open < 0.02f)
        {
            c.StrokeRect(wellX - 0.5f, wellY - 0.5f, DoorW + 1f, DoorH + 1f, Hex(0x121824));

            c.FillRectPixels(Mathf.FloorToInt(wellX), Mathf.FloorToInt(wellY),
                Mathf.FloorToInt(DoorW), 14, currentCar.DoorTop);
            c.FillRectPixels(Mathf.FloorToInt(wellX), Mathf.FloorToInt(wellY + 14f),
                Mathf.FloorToInt(DoorW), Mathf.FloorToInt(DoorH - 14f), currentCar.DoorBot);

            c.FillRectPixels(Mathf.FloorToInt(wellX + 1f), Mathf.FloorToInt(wellY + 1f),
                Mathf.FloorToInt(DoorW - 2f), 1, currentCar.DoorEdge);
            c.FillRectPixels(Mathf.FloorToInt(wellX + 1f), Mathf.FloorToInt(wellY + DoorH - 1f),
                Mathf.FloorToInt(DoorW - 2f), 1, Hex(0x141924));

            float notchX = wellX + DoorW - 8f;
            float notchY = HubY - 6f;
            c.FillRectPixels(Mathf.FloorToInt(notchX), Mathf.FloorToInt(notchY), 3, 12, Hex(0x182030));
            c.FillRectPixels(Mathf.FloorToInt(notchX + 3f), Mathf.FloorToInt(notchY), 1, 12,
                currentCar.DoorEdge);
        }
        else if (cosA >= 0f)
        {
            Quad(c, HingeX, wellY + 2f, freeX + thickW + 3f, wellY - perspY + 4f,
                freeX + thickW + 3f, wellY + DoorH + perspY + 4f, HingeX, wellY + DoorH + 2f,
                Rgba(5, 8, 14, 0.55f));

            Quad(c, freeX, wellY - perspY, freeX + thickW, wellY - perspY + 1f,
                freeX + thickW, wellY + DoorH + perspY - 1f, freeX, wellY + DoorH + perspY,
                currentCar.DoorInner);

            Quad(c, HingeX, wellY, freeX, wellY - perspY, freeX, wellY + DoorH + perspY,
                HingeX, wellY + DoorH, currentCar.DoorOpen, Hex(0x141b29));

            if (freeX - HingeX > 12f)
            {
                float nx = Mathf.Max(HingeX + 2f, freeX - Mathf.Max(2f, Mathf.Round(7f * cosA)));
                float nw = Mathf.Max(1f, Mathf.Round(3f * cosA));
                c.FillRectPixels(Mathf.FloorToInt(nx), Mathf.FloorToInt(HubY - 6f),
                    Mathf.FloorToInt(nw), 12, Hex(0x182030));
            }
        }
        else
        {
            Quad(c, HingeX, wellY + 4f, freeX - thickW - 2f, wellY - perspY + 7f,
                freeX - thickW - 2f, wellY + DoorH + perspY + 5f, HingeX, wellY + DoorH + 4f,
                Rgba(7, 10, 16, 0.48f));

            Quad(c, freeX - thickW, wellY - perspY + 1f, freeX, wellY - perspY,
                freeX, wellY + DoorH + perspY, freeX - thickW, wellY + DoorH + perspY - 1f,
                currentCar.DoorEdge);

            Quad(c, freeX, wellY - perspY, HingeX, wellY, HingeX, wellY + DoorH,
                freeX, wellY + DoorH + perspY, currentCar.DoorInner, Hex(0x0e131d));

            float padL = freeX + 3f;
            float padR = HingeX - 3f;
            if (padR > padL + 2f)
            {
                Quad(c, padL, wellY - Mathf.Round(perspY * 0.6f) + 5f, padR, wellY + 5f,
                    padR, wellY + DoorH - 5f, padL, wellY + DoorH + Mathf.Round(perspY * 0.6f) - 5f,
                    Hex(0x161d2b), currentCar.DoorOpen);
            }

            float h1Y = wellY + 9f;
            float h2Y = wellY + DoorH - 13f;
            c.FillRectPixels(Mathf.FloorToInt(HingeX - 3f), Mathf.FloorToInt(h1Y - 1f), 7, 5, Hex(0x0e131d));
            c.FillRectPixels(Mathf.FloorToInt(HingeX - 3f), Mathf.FloorToInt(h2Y - 1f), 7, 5, Hex(0x0e131d));
            c.FillRectPixels(Mathf.FloorToInt(HingeX - 2f), Mathf.FloorToInt(h1Y), 5, 3, currentCar.DoorOpen);
            c.FillRectPixels(Mathf.FloorToInt(HingeX - 2f), Mathf.FloorToInt(h2Y), 5, 3, currentCar.DoorOpen);
        }

        bool flash = capFlashTimer > 0f && Mathf.FloorToInt(capFlashTimer * 10f) % 2 == 0;
        if ((hoverCap && !carrying && !plugged) || flash)
        {
            Color32 hl = flash ? Ember : Rgba(240, 222, 186, 0.55f);
            if (!capOpen)
            {
                c.StrokeRect(wellX - 1.5f, wellY - 1.5f, DoorW + 3f, DoorH + 3f, hl);
            }
            else
            {
                c.StrokeRect(
                    Mathf.Min(freeX - thickW, wellX) - 1.5f,
                    wellY - perspY - 1.5f,
                    DoorW + Mathf.Abs(HingeX - freeX) + thickW + 3f,
                    DoorH + perspY * 2f + 3f, hl);
            }
        }
    }

    private static void DrawBolt(PixelCanvas c, float bx, float by)
    {
        c.FillRectPixels(Mathf.FloorToInt(HubX + bx - 1f), Mathf.FloorToInt(HubY + by - 1f), 2, 2,
            Hex(0x0b0f17));
        c.FillRectPixels(Mathf.FloorToInt(HubX + bx), Mathf.FloorToInt(HubY + by), 1, 1, Hex(0x526488));
    }

    private void DrawTruckTankCapAndNeck(PixelCanvas c)
    {
        float open = capAnim;

        c.Disc(HubX, HubY + 2f, HoleR + 5f, 14, Hex(0x0d1118));
        c.Disc(HubX, HubY, HoleR + 4f, 14, Hex(0x57657d));
        c.Disc(HubX, HubY - 1f, HoleR + 2f, 14, Hex(0x3b4557));

        if (open > 0.02f)
        {
            c.Disc(HubX, HubY, HoleR, 14, Hex(0x05070b));
            c.Disc(HubX + 1f, HubY + 1f, HoleR - 4f, 12, Hex(0x020305));
            c.Ring(HubX, HubY, HoleR - 2f, 14, Hex(0x5d6c87));
            c.Ring(HubX, HubY, HoleR - 5f, 14, Hex(0x1e2738));

            if (carrying)
            {
                Color32 ringColor = hoverNeck ? Ember : Rgba(240, 222, 186, 0.32f);
                c.Ring(HubX, HubY, HoleR + 1f, 14, ringColor);
            }
        }

        float capX = Mathf.Round(HubX - open * 42f);
        float capY = Mathf.Round(HubY - 1f + open * 10f);
        float capRot = -open * 2.8f;

        if (open > 0.15f)
        {
            DrawChain(c, HubX - HoleR + 2f, HubY + 4f, (HubX + capX) * 0.5f, HubY + 18f,
                capX + 5f, capY + 5f, 3, Hex(0x11151d));
            DrawChain(c, HubX - HoleR + 2f, HubY + 3f, (HubX + capX) * 0.5f, HubY + 17f,
                capX + 5f, capY + 4f, 1, Hex(0x7b89a3));
        }

        c.Push();
        c.Translate(capX, capY);

        c.Disc(2f, 4f, HoleR + 3f, 12, Rgba(4, 6, 10, 0.65f), capRot);
        c.Disc(0f, 1f, HoleR + 2f, 12, Hex(0x0a0d12), capRot);

        const int lugs = 8;
        for (int i = 0; i < lugs; i++)
        {
            float a = capRot + i / (float)lugs * Mathf.PI * 2f;
            float lx = Mathf.Cos(a) * (HoleR + 1f);
            float ly = Mathf.Sin(a) * (HoleR + 1f);
            c.Disc(lx, ly + 1f, 4f, 6, Hex(0x0b0e14), a);
            c.Disc(lx, ly - 1f, 3.6f, 6, Hex(0x252c3b), a);
        }

        c.Disc(0f, 0f, HoleR + 1f, 14, Hex(0x151a24), capRot);
        c.Disc(0f, -1f, HoleR - 2f, 14, Hex(0x202736), capRot);
        c.Ring(0f, -1f, HoleR - 2f, 14, Hex(0x323c52), 1f, capRot);

        c.Push();
        c.Rotate(capRot + 0.35f);
        Quad(c, -9f, -3f, 9f, -3f, 9f, 3f, -9f, 3f, Hex(0x10141c), Hex(0x080a0f));
        Quad(c, -8f, -2f, 8f, -2f, 8f, 1f, -8f, 1f, Hex(0x2c3547));
        c.FillRect(-2f, -1f, 4f, 2f, Hex(0x5c6b87));
        c.Pop();

        c.Pop();

        bool flash = capFlashTimer > 0f && Mathf.FloorToInt(capFlashTimer * 10f) % 2 == 0;
        if ((hoverCap && !carrying && !plugged) || flash)
        {
            Color32 hl = flash ? Ember : Rgba(240, 222, 186, 0.6f);
            c.Ring(capX, capY, HoleR + 5f, 14, hl);
        }
    }

    /// <summary>Квадратичная кривая — цепочка крышки и прочие поводки.</summary>
    private void DrawChain(PixelCanvas c, float x0, float y0, float cx, float cy, float x1, float y1,
        float width, Color32 color)
    {
        const int steps = 10;
        Vector2 prev = new Vector2(x0, y0);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            float u = 1f - t;
            Vector2 p = new Vector2(
                u * u * x0 + 2f * u * t * cx + t * t * x1,
                u * u * y0 + 2f * u * t * cy + t * t * y1);
            chainPts[0] = prev;
            chainPts[1] = p;
            c.ThickPolyline(chainPts, 2, width, color);
            prev = p;
        }
    }

    // ---------- Шланг ----------

    private void DrawHose(PixelCanvas c)
    {
        Vector2 port = GetNozzlePortWorld();
        Vector2 dir = GetNozzlePortDirWorld();
        Vector2 p0 = port;
        Vector2 p3 = HoseAnchor;

        float distX = Mathf.Abs(p3.x - p0.x);
        float tangentLen = 32f + (1f - Mathf.Clamp01(distX / 180f)) * 18f;

        Vector2 p1 = new Vector2(p0.x + dir.x * tangentLen, p0.y + dir.y * tangentLen);
        Vector2 p2 = new Vector2(p3.x - 42f, p3.y + 10f);

        const int steps = 12;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float u = 1f - t;
            hosePts[i] = new Vector2(
                Mathf.Round(u * u * u * p0.x + 3f * u * u * t * p1.x + 3f * u * t * t * p2.x + t * t * t * p3.x),
                Mathf.Round(u * u * u * p0.y + 3f * u * u * t * p1.y + 3f * u * t * t * p2.y + t * t * t * p3.y));
        }

        c.ThickPolyline(hosePts, steps + 1, 7f, Rgba(6, 8, 14, 0.5f), 2f, 4f);
        c.ThickPolyline(hosePts, steps + 1, 7f, Hex(0x0b0f17));
        c.ThickPolyline(hosePts, steps + 1, 5f, Hex(0x1b2333));
        c.ThickPolyline(hosePts, steps + 1, 2f, Hex(0x32405c), -1f, -1f);
    }

    // ---------- Шкала ----------

    private void DrawGauge(PixelCanvas c)
    {
        if (!gaugeVisible)
        {
            return;
        }

        float gx = GaugeX;
        float top = GaugeTop;
        float bot = GaugeBottom;
        float w = GaugeW;
        float h = bot - top;

        c.FillRect(gx - 5f, top - 5f, w + 12f, h + 12f, Rgba(5, 7, 12, 0.78f));
        c.FillRect(gx - 4f, top - 4f, w + 8f, h + 8f, Hex(0x2b3854));

        c.FillRect(gx - 4f, top - 4f, w + 8f, 1f, Hex(0x5c73a3));
        c.FillRect(gx - 4f, top - 4f, 1f, h + 8f, Hex(0x5c73a3));
        c.FillRect(gx - 4f, bot + 3f, w + 8f, 1f, Hex(0x090d14));
        c.FillRect(gx + w + 3f, top - 4f, 1f, h + 8f, Hex(0x090d14));

        c.FillRect(gx - 1f, top - 1f, w + 2f, h + 2f, Hex(0x080b12));
        c.FillRect(gx, top, w, h, Hex(0x0e1420));

        for (int i = 1; i < 10; i++)
        {
            float ty = Mathf.Round(bot - h * i / 10f);
            c.FillRect(gx + 1f, ty, w - 2f, 1f, i % 2 == 0 ? Hex(0x24304a) : Hex(0x172033));
        }

        float ratio = Mathf.Clamp01(poured / (target * OverRatio));
        float fillH = Mathf.Round(h * ratio);
        if (fillH > 0f)
        {
            Color32 colMain = Ember;
            Color32 colLight = Hex(0xffd080);
            Color32 colDark = EmberDeep;

            float r = poured / target;
            if (r > OverRatio)
            {
                colMain = Bad;
                colLight = Hex(0xff8b80);
                colDark = Hex(0x9e2d24);
            }
            else if (r >= GoodRatio)
            {
                colMain = Ok;
                colLight = Hex(0xc2f5c6);
                colDark = Hex(0x4e8f53);
            }

            float fy = bot - fillH;
            c.FillRect(gx, fy, w, fillH, colMain);
            c.FillRect(gx + 1f, fy, 2f, fillH, colLight);
            c.FillRect(gx + w - 2f, fy, 2f, fillH, colDark);
            c.FillRect(gx, fy, w, 1f, Cream);
        }

        float minY = Mathf.Round(bot - h * (MinRatio / OverRatio));
        c.FillRect(gx - 3f, minY, w + 6f, 1f, Rgba(240, 222, 186, 0.55f));

        float targetY = Mathf.Round(bot - h * (1f / OverRatio));
        float goodY = Mathf.Round(bot - h * (GoodRatio / OverRatio));

        c.FillRect(gx + w + 5f, targetY, 2f, goodY - targetY + 1f, Ok);
        c.FillRect(gx + w + 3f, targetY, 4f, 1f, Ok);
        c.FillRect(gx + w + 3f, goodY, 4f, 1f, Ok);

        c.FillRect(gx - 6f, targetY - 1f, w + 12f, 3f, Hex(0x080b11));
        c.FillRect(gx - 5f, targetY, w + 10f, 1f, Cream);

        gaugeArrow[0] = new Vector2(gx - 8f, targetY - 3f);
        gaugeArrow[1] = new Vector2(gx - 3f, targetY);
        gaugeArrow[2] = new Vector2(gx - 8f, targetY + 3f);
        c.PolyFill(gaugeArrow, Cream, Hex(0x080b11));
    }

    // ---------- Подписи ----------

    private void DrawHint(PixelCanvas c)
    {
        if (done || string.IsNullOrEmpty(hintText))
        {
            return;
        }

        int w = PixelCanvas.TextWidth(hintText, 1);
        int x = Mathf.FloorToInt((PixelCanvas.W - w) * 0.5f);
        c.Text(hintText, x, PixelCanvas.H - 12, 1, Cream);
    }

    private void DrawResultOverlay(PixelCanvas c)
    {
        c.FillRectPixels(0, 0, PixelCanvas.W, PixelCanvas.H, Rgba(10, 14, 22, 0.78f));

        int titleW = PixelCanvas.TextWidth(resultTitle, 2);
        int titleX = Mathf.FloorToInt((PixelCanvas.W - titleW) * 0.5f);
        c.Text(resultTitle, titleX, 74, 2, resultColor);

        const string prompt = "[ПРОБЕЛ] — ЗАКРЫТЬ";
        int hintW = PixelCanvas.TextWidth(prompt, 1);
        int hintX = Mathf.FloorToInt((PixelCanvas.W - hintW) * 0.5f);
        c.Text(prompt, hintX, 100, 1, Muted);
    }

    // ---------- Служебное ----------

    private void Quad(PixelCanvas c, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3,
        Color32 fill, Color32? stroke = null)
    {
        quad[0] = new Vector2(x0, y0);
        quad[1] = new Vector2(x1, y1);
        quad[2] = new Vector2(x2, y2);
        quad[3] = new Vector2(x3, y3);
        c.PolyFill(quad, 4, fill, stroke);
    }
}
