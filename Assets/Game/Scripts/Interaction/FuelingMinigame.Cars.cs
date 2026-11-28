using UnityEngine;

public sealed partial class FuelingMinigame
{
    // ---------- Палитра прототипа ----------

    private static readonly Color32 Cream = Hex(0xf0deba);
    private static readonly Color32 Ember = Hex(0xf0a94c);
    private static readonly Color32 EmberDeep = Hex(0xb8762e);
    private static readonly Color32 Ok = Hex(0x8fd694);
    private static readonly Color32 Bad = Hex(0xe2564a);

    private static Color32 Hex(uint hex) => new Color32((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);

    private static Color32 Rgba(byte r, byte g, byte b, float alpha) =>
        new Color32(r, g, b, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));

    // ---------- Машины ----------

    private enum GrainMode
    {
        Vertical,
        Horizontal,
        CleanWeather,
    }

    private readonly struct StripeDef
    {
        public readonly int Y;
        public readonly int H;
        public readonly Color32 Color;

        public StripeDef(int y, int h, Color32 color)
        {
            Y = y;
            H = h;
            Color = color;
        }
    }

    private sealed class CarDef
    {
        public string Id;
        public bool IsExternalTank;

        public Color32 TopFrame;
        public Color32[] Bands;
        public Color32 Skirt;
        public int SkirtY;

        public Color32 RidgeLight;
        public Color32 RidgeDark;

        public int SeamX;
        public Color32 SeamDark;
        public Color32 SeamLight;

        public bool HasHandle;
        public int HandleX;
        public int HandleY;

        public Color32 DoorTop;
        public Color32 DoorBot;
        public Color32 DoorOpen;
        public Color32 DoorEdge;
        public Color32 DoorInner;

        public Color32 ArchOuter;
        public Color32 ArchMid;
        public bool ArchBoxy;

        public GrainMode Grain;
        public StripeDef[] Stripes;
        public bool HasMud;
        public Color32 MudColor1;
        public Color32 MudColor2;
    }

    private const int SuvGreenIndex = 0;
    private const int PickupBlueIndex = 1;
    private const int TruckWhiteIndex = 2;
    private const int VanPostIndex = 3;
    private const int HatchRedIndex = 4;

    private const string SuvGreenId = "suv_green";
    private const string PickupBlueId = "pickup_blue";
    private const string VanPostId = "van_post";
    private const string HatchRedId = "hatch_red";

    // Порядок и цвета — ровно из fueling_v4.html.
    private static readonly CarDef[] Cars =
    {
        new CarDef
        {
            Id = SuvGreenId,
            IsExternalTank = false,
            TopFrame = Hex(0x35472f),
            Bands = new[] { Hex(0x506947), Hex(0x465c3f), Hex(0x3d5236), Hex(0x31422b) },
            Skirt = Hex(0x12161c),
            SkirtY = 166,
            RidgeLight = Hex(0x708f64),
            RidgeDark = Hex(0x243020),
            SeamX = 204,
            SeamDark = Hex(0x1b2418),
            SeamLight = Hex(0x607a56),
            HasHandle = true,
            HandleX = 214,
            HandleY = 56,
            DoorTop = Hex(0x4b6343),
            DoorBot = Hex(0x405439),
            DoorOpen = Hex(0x455b3e),
            DoorEdge = Hex(0x6c8a60),
            DoorInner = Hex(0x232e1f),
            ArchOuter = Hex(0x1a1e24),
            ArchMid = Hex(0x101318),
            ArchBoxy = true,
            Grain = GrainMode.Vertical,
            Stripes = new[] { new StripeDef(114, 7, Hex(0xcfd8cc)) },
            HasMud = true,
            MudColor1 = Hex(0x6e5544),
            MudColor2 = Hex(0xd2dbe3),
        },
        new CarDef
        {
            Id = PickupBlueId,
            IsExternalTank = false,
            TopFrame = Hex(0x253b63),
            Bands = new[] { Hex(0x4164a1), Hex(0x37568c), Hex(0x2e4978), Hex(0x243a61) },
            Skirt = Hex(0x111724),
            SkirtY = 166,
            RidgeLight = Hex(0x688fd1),
            RidgeDark = Hex(0x1c2d4d),
            SeamX = 248,
            SeamDark = Hex(0x131f36),
            SeamLight = Hex(0x537ab8),
            HasHandle = false,
            DoorTop = Hex(0x3d5e96),
            DoorBot = Hex(0x324f80),
            DoorOpen = Hex(0x37568c),
            DoorEdge = Hex(0x6289c9),
            DoorInner = Hex(0x1b2a47),
            ArchOuter = Hex(0x1d2430),
            ArchMid = Hex(0x10151f),
            ArchBoxy = false,
            Grain = GrainMode.Horizontal,
            Stripes = new[] { new StripeDef(116, 7, Hex(0xd4dff0)) },
            HasMud = true,
            MudColor1 = Hex(0x6e4b3b),
            MudColor2 = Hex(0xc8d3e3),
        },
        new CarDef
        {
            Id = "truck_white",
            IsExternalTank = true,
        },
        new CarDef
        {
            Id = VanPostId,
            IsExternalTank = false,
            TopFrame = Hex(0xb8c2d1),
            Bands = new[] { Hex(0xcfd7e3), Hex(0xc6cfde), Hex(0xbcc6d6), Hex(0xb2bccb) },
            Skirt = Hex(0x2c323d),
            SkirtY = 148,
            RidgeLight = Hex(0xe8effa),
            RidgeDark = Hex(0x788394),
            SeamX = 228,
            SeamDark = Hex(0x4c5463),
            SeamLight = Hex(0xe1e8f2),
            HasHandle = true,
            HandleX = 240,
            HandleY = 94,
            DoorTop = Hex(0xc6cfde),
            DoorBot = Hex(0xb8c2d1),
            DoorOpen = Hex(0xbcc6d6),
            DoorEdge = Hex(0xe4ecf7),
            DoorInner = Hex(0x4a5261),
            ArchOuter = Hex(0x20252e),
            ArchMid = Hex(0x12151b),
            ArchBoxy = false,
            Grain = GrainMode.CleanWeather,
            Stripes = new[]
            {
                new StripeDef(112, 12, Hex(0x254385)),
                new StripeDef(128, 5, Hex(0x9e2b2b)),
            },
            HasMud = false,
            MudColor1 = Hex(0x635852),
            MudColor2 = Hex(0x99a3b0),
        },
        new CarDef
        {
            Id = HatchRedId,
            IsExternalTank = false,
            TopFrame = Hex(0x6e2227),
            Bands = new[] { Hex(0xa63e44), Hex(0x94353a), Hex(0x802b30), Hex(0x692125) },
            Skirt = Hex(0x15161c),
            SkirtY = 166,
            RidgeLight = Hex(0xc95b61),
            RidgeDark = Hex(0x47181b),
            SeamX = 218,
            SeamDark = Hex(0x361114),
            SeamLight = Hex(0xbd4d53),
            HasHandle = true,
            HandleX = 230,
            HandleY = 58,
            DoorTop = Hex(0x9c383e),
            DoorBot = Hex(0x852d32),
            DoorOpen = Hex(0x8f3237),
            DoorEdge = Hex(0xc4545a),
            DoorInner = Hex(0x3d1518),
            ArchOuter = Hex(0x1a1c22),
            ArchMid = Hex(0x0f1116),
            ArchBoxy = true,
            Grain = GrainMode.Horizontal,
            Stripes = new[] { new StripeDef(112, 11, Hex(0xdce0e8)) },
            HasMud = true,
            MudColor1 = Hex(0x6e4f42),
            MudColor2 = Hex(0xcfd4de),
        },
    };

    /// <summary>Модель посетителя → одна из пяти машин прототипа.</summary>
    public static int CarIndexForModel(string modelPath)
    {
        if (string.IsNullOrEmpty(modelPath))
        {
            return TruckWhiteIndex;
        }

        string path = modelPath.ToLowerInvariant();
        if (path.Contains("hatchback"))
        {
            return HatchRedIndex;
        }

        if (path.Contains("jeep"))
        {
            return SuvGreenIndex;
        }

        if (path.Contains("pickup"))
        {
            return PickupBlueIndex;
        }

        if (path.Contains("van"))
        {
            return VanPostIndex;
        }

        // truck1, заглушка гостей и всё неизвестное — белый бортовой грузовик.
        return TruckWhiteIndex;
    }
}
