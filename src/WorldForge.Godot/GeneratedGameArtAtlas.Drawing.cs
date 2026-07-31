using Godot;
using WorldForge.Core.Simulation;

namespace WorldForge.Presentation;

public sealed partial class GeneratedGameArtAtlas
{
    private static readonly Color Ink = new("151921");
    private static readonly Color Gold = new("e2ad42");
    private static readonly Color GoldLight = new("ffe18a");
    private static readonly Color Wood = new("76502d");
    private static readonly Color WoodLight = new("b77b3b");
    private static readonly Color Stone = new("78818b");
    private static readonly Color StoneLight = new("b4bbc1");
    private static readonly Color Green = new("4f9a4b");
    private static readonly Color GreenLight = new("8bd66e");
    private static readonly Color Blue = new("2a76b8");
    private static readonly Color BlueLight = new("67d6ff");
    private static readonly Color Purple = new("7650bd");
    private static readonly Color PurpleLight = new("c28cff");
    private static readonly Color Red = new("b94735");
    private static readonly Color Orange = new("ef7b2d");

    private static Texture2D BuildIconsTexture()
    {
        Image image = NewImage(IconCell * 6, IconCell * 5);
        for (int index = 0; index < 30; index++)
            DrawIcon(image, index, (index % 6) * IconCell, (index / 6) * IconCell);
        return ImageTexture.CreateFromImage(image);
    }

    private static Texture2D BuildCharactersTexture()
    {
        Image image = NewImage(CharacterCell * 3, CharacterCell * 6);
        foreach (RaceKind race in Enum.GetValues<RaceKind>())
            for (int variant = 0; variant < 3; variant++)
                DrawCharacter(image, race, variant, variant * CharacterCell, (int)race * CharacterCell);
        return ImageTexture.CreateFromImage(image);
    }

    private static Texture2D BuildPortraitsTexture()
    {
        Image image = NewImage(PortraitCell * 4, PortraitCell * 3);
        for (int index = 0; index < 12; index++)
        {
            RaceKind race = (RaceKind)(index / 2);
            DrawPortrait(image, race, index % 2, (index % 4) * PortraitCell, (index / 4) * PortraitCell);
        }
        return ImageTexture.CreateFromImage(image);
    }

    private static Texture2D BuildBuildingsTexture()
    {
        Image image = NewImage(BuildingCell * 6, BuildingCell * 3);
        BuildingKind[] kinds =
        {
            BuildingKind.House, BuildingKind.Farm, BuildingKind.Market, BuildingKind.Lumberyard, BuildingKind.Quarry, BuildingKind.Mine,
            BuildingKind.Clinic, BuildingKind.Temple, BuildingKind.Barracks, BuildingKind.Smelter, BuildingKind.Wall, BuildingKind.Gate,
            BuildingKind.Keep, BuildingKind.Harbor, BuildingKind.Shipyard, BuildingKind.Workshop, BuildingKind.MageTower, BuildingKind.Monument,
        };
        for (int index = 0; index < kinds.Length; index++)
            DrawBuilding(image, kinds[index], (index % 6) * BuildingCell, (index / 6) * BuildingCell);
        return ImageTexture.CreateFromImage(image);
    }

    private static Texture2D BuildEffectsTexture()
    {
        Image image = NewImage(EffectCell * 5, EffectCell * 4);
        for (int index = 0; index < 20; index++)
            DrawEffect(image, (GameArtEffect)index, (index % 5) * EffectCell, (index / 5) * EffectCell);
        return ImageTexture.CreateFromImage(image);
    }

    private static Image NewImage(int width, int height)
    {
        Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(Colors.Transparent);
        return image;
    }

    private static void DrawIcon(Image image, int index, int ox, int oy)
    {
        int cx = ox + IconCell / 2;
        int cy = oy + IconCell / 2;
        switch ((GameIcon)index)
        {
            case GameIcon.Food:
                Circle(image, cx - 3, cy + 1, 11, Orange.Darkened(0.2f));
                Circle(image, cx - 5, cy - 2, 8, Orange);
                Line(image, cx + 4, cy + 5, cx + 13, cy + 12, StoneLight, 3);
                Circle(image, cx + 14, cy + 13, 3, StoneLight);
                break;
            case GameIcon.Wood:
                for (int row = 0; row < 2; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        Rect(image, cx - 15 + col * 10, cy - 8 + row * 11, 13, 8, Wood);
                        Circle(image, cx - 15 + col * 10, cy - 4 + row * 11, 4, WoodLight);
                        Circle(image, cx - 15 + col * 10, cy - 4 + row * 11, 2, Wood.Darkened(0.25f));
                    }
                break;
            case GameIcon.Stone:
            case GameIcon.Ore:
                Circle(image, cx - 9, cy + 5, 9, Stone);
                Circle(image, cx + 2, cy - 4, 11, Stone.Darkened(0.15f));
                Circle(image, cx + 11, cy + 7, 8, StoneLight.Darkened(0.15f));
                if ((GameIcon)index == GameIcon.Ore)
                {
                    Pixel(image, cx + 1, cy - 6, GoldLight);
                    Pixel(image, cx + 6, cy + 2, Gold);
                    Rect(image, cx - 8, cy + 4, 3, 3, Gold);
                }
                break;
            case GameIcon.Planks:
                for (int i = 0; i < 4; i++)
                    Rect(image, cx - 16 + i, cy - 12 + i * 6, 31 - i * 2, 5, i % 2 == 0 ? WoodLight : Wood);
                Line(image, cx - 9, cy - 14, cx - 6, cy + 12, Ink, 2);
                Line(image, cx + 9, cy - 14, cx + 6, cy + 12, Ink, 2);
                break;
            case GameIcon.Metal:
                Polygon(image, new[] { P(cx - 14, cy + 8), P(cx - 8, cy - 8), P(cx + 12, cy - 8), P(cx + 16, cy + 8) }, StoneLight);
                Line(image, cx - 8, cy - 7, cx + 11, cy - 7, Colors.White, 2);
                break;
            case GameIcon.Tools:
                Line(image, cx - 11, cy + 14, cx + 10, cy - 13, WoodLight, 4);
                Line(image, cx + 11, cy + 14, cx - 9, cy - 10, Wood, 4);
                Rect(image, cx + 3, cy - 16, 17, 6, StoneLight);
                Line(image, cx - 15, cy - 12, cx - 4, cy - 7, StoneLight, 5);
                break;
            case GameIcon.Weapons:
                Line(image, cx - 12, cy + 14, cx + 11, cy - 14, StoneLight, 4);
                Line(image, cx + 12, cy + 14, cx - 11, cy - 14, StoneLight, 4);
                Line(image, cx - 17, cy + 8, cx - 7, cy + 17, Gold, 3);
                Line(image, cx + 17, cy + 8, cx + 7, cy + 17, Gold, 3);
                break;
            case GameIcon.Gold:
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 4 - row; col++)
                        Circle(image, cx - 12 + col * 8 + row * 4, cy + 10 - row * 7, 5, row % 2 == 0 ? Gold : GoldLight);
                break;
            case GameIcon.Mana:
            case GameIcon.Water:
                Diamond(image, cx, cy, 15, (GameIcon)index == GameIcon.Mana ? BlueLight : Blue);
                Diamond(image, cx, cy - 2, 7, Colors.White.WithAlpha(0.55f));
                break;
            case GameIcon.Faith:
                Star(image, cx, cy, 17, GoldLight);
                Circle(image, cx, cy, 5, Colors.White);
                break;
            case GameIcon.Fear:
            case GameIcon.Disease:
                Skull(image, cx, cy, (GameIcon)index == GameIcon.Fear ? Purple : Green);
                for (int i = 0; i < 5; i++)
                    Circle(image, cx - 14 + i * 7, cy - 14 - i % 2 * 4, 2, ((GameIcon)index == GameIcon.Fear ? PurpleLight : GreenLight).WithAlpha(0.8f));
                break;
            case GameIcon.Happiness:
                Circle(image, cx, cy, 14, GoldLight);
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4;
                    Line(image, cx + (int)(Math.Cos(angle) * 17), cy + (int)(Math.Sin(angle) * 17), cx + (int)(Math.Cos(angle) * 21), cy + (int)(Math.Sin(angle) * 21), Gold, 2);
                }
                Pixel(image, cx - 5, cy - 2, Ink);
                Pixel(image, cx + 5, cy - 2, Ink);
                Line(image, cx - 5, cy + 5, cx + 5, cy + 5, Ink, 2);
                break;
            case GameIcon.Health:
                Heart(image, cx, cy, Red);
                Rect(image, cx - 2, cy - 6, 4, 13, Colors.White);
                Rect(image, cx - 7, cy - 1, 14, 4, Colors.White);
                break;
            case GameIcon.Fire:
                Flame(image, cx, cy + 5, Orange);
                break;
            case GameIcon.Lightning:
                Polygon(image, new[] { P(cx + 3, cy - 18), P(cx - 9, cy + 1), P(cx, cy + 1), P(cx - 5, cy + 18), P(cx + 13, cy - 5), P(cx + 4, cy - 5) }, BlueLight);
                break;
            case GameIcon.Forest:
                Tree(image, cx - 10, cy + 7, 12);
                Tree(image, cx + 8, cy + 10, 15);
                Tree(image, cx, cy - 1, 17);
                break;
            case GameIcon.Fertility:
                for (int i = -2; i <= 2; i++)
                {
                    Line(image, cx + i * 5, cy + 15, cx + i * 4, cy - 12, Gold, 2);
                    Circle(image, cx + i * 4 - 3, cy - 5, 3, GoldLight);
                    Circle(image, cx + i * 4 + 3, cy, 3, GoldLight);
                }
                break;
            case GameIcon.Population:
                House(image, cx, cy + 6, Blue);
                Circle(image, cx - 14, cy + 12, 5, GoldLight);
                Circle(image, cx + 14, cy + 12, 5, GoldLight.Darkened(0.12f));
                break;
            case GameIcon.Army:
                Shield(image, cx, cy + 2, Red);
                Line(image, cx - 15, cy + 16, cx + 12, cy - 13, StoneLight, 3);
                Line(image, cx + 15, cy + 16, cx - 12, cy - 13, StoneLight, 3);
                break;
            case GameIcon.Fleet:
                Ship(image, cx, cy + 6, Blue);
                break;
            case GameIcon.Trade:
                Line(image, cx, cy - 15, cx, cy + 14, Gold, 3);
                Line(image, cx - 14, cy - 8, cx + 14, cy - 8, Gold, 3);
                Line(image, cx - 12, cy - 7, cx - 16, cy + 5, Gold, 2);
                Line(image, cx + 12, cy - 7, cx + 16, cy + 5, Gold, 2);
                Circle(image, cx - 16, cy + 8, 7, GoldLight);
                Circle(image, cx + 16, cy + 8, 7, BlueLight);
                break;
            case GameIcon.Diplomacy:
                Rect(image, cx - 16, cy - 6, 13, 12, Blue);
                Rect(image, cx + 3, cy - 6, 13, 12, Red);
                Line(image, cx - 5, cy + 5, cx + 6, cy - 5, GoldLight, 5);
                Line(image, cx + 5, cy + 5, cx - 6, cy - 5, GoldLight, 5);
                break;
            case GameIcon.Magic:
                Rect(image, cx - 16, cy - 10, 14, 21, Purple.Darkened(0.25f));
                Rect(image, cx + 2, cy - 10, 14, 21, Purple);
                Line(image, cx, cy - 11, cx, cy + 12, Gold, 2);
                Diamond(image, cx, cy - 17, 7, PurpleLight);
                break;
            case GameIcon.Ruins:
                Rect(image, cx - 15, cy - 12, 7, 27, Stone);
                Rect(image, cx + 7, cy - 4, 8, 19, StoneLight.Darkened(0.2f));
                Rect(image, cx - 11, cy - 16, 26, 5, StoneLight);
                Line(image, cx - 16, cy + 15, cx + 16, cy + 15, Green, 3);
                break;
            case GameIcon.Relic:
                Rect(image, cx - 9, cy - 11, 18, 12, Gold);
                Line(image, cx, cy, cx, cy + 14, Gold, 4);
                Rect(image, cx - 10, cy + 13, 20, 4, GoldLight);
                Diamond(image, cx, cy - 9, 5, BlueLight);
                break;
            case GameIcon.Chronicle:
                Rect(image, cx - 16, cy - 15, 32, 30, Blue.Darkened(0.35f));
                Rect(image, cx - 12, cy - 11, 24, 22, new Color("e9d9a5"));
                Line(image, cx - 8, cy - 5, cx + 8, cy - 5, Wood, 2);
                Line(image, cx - 8, cy + 1, cx + 8, cy + 1, Wood, 2);
                Line(image, cx - 8, cy + 7, cx + 4, cy + 7, Wood, 2);
                break;
            case GameIcon.Settings:
                Gear(image, cx, cy, StoneLight);
                break;
        }
        OutlineCell(image, ox, oy, IconCell);
    }

    private static void DrawCharacter(Image image, RaceKind race, int variant, int ox, int oy)
    {
        int cx = ox + CharacterCell / 2;
        Color skin = RaceSkin(race);
        Color cloth = RaceColor(race);
        Color accent = RaceAccent(race);
        Circle(image, cx + 1, oy + 16, 8, skin);
        Rect(image, cx - 8, oy + 22, 17, 16, cloth);
        Rect(image, cx - 7, oy + 38, 6, 7, cloth.Darkened(0.35f));
        Rect(image, cx + 2, oy + 38, 6, 7, cloth.Darkened(0.35f));
        Rect(image, cx - 14, oy + 24, 6, 13, skin.Darkened(0.08f));
        Rect(image, cx + 9, oy + 24, 6, 13, skin.Darkened(0.08f));
        Pixel(image, cx - 3, oy + 15, Ink);
        Pixel(image, cx + 4, oy + 15, Ink);
        if (race == RaceKind.Sylvan)
        {
            Polygon(image, new[] { P(cx - 8, oy + 14), P(cx - 15, oy + 11), P(cx - 8, oy + 18) }, skin);
            Polygon(image, new[] { P(cx + 9, oy + 14), P(cx + 16, oy + 11), P(cx + 9, oy + 18) }, skin);
        }
        if (race == RaceKind.Orc)
        {
            Pixel(image, cx - 7, oy + 20, Colors.White);
            Pixel(image, cx + 8, oy + 20, Colors.White);
        }
        if (race == RaceKind.Tideborn)
        {
            Polygon(image, new[] { P(cx - 7, oy + 11), P(cx - 12, oy + 3), P(cx - 2, oy + 9) }, BlueLight);
            Polygon(image, new[] { P(cx + 8, oy + 11), P(cx + 13, oy + 3), P(cx + 3, oy + 9) }, BlueLight);
        }
        if (race == RaceKind.Arcane)
            Diamond(image, cx, oy + 4, 4, PurpleLight);

        switch (variant)
        {
            case 0:
                Rect(image, cx - 10, oy + 7, 21, 6, accent);
                Line(image, cx + 14, oy + 20, cx + 18, oy + 39, WoodLight, 3);
                Circle(image, cx + 18, oy + 18, 4, GreenLight);
                break;
            case 1:
                Rect(image, cx - 10, oy + 6, 21, 7, Stone);
                Line(image, cx + 13, oy + 24, cx + 19, oy + 10, Wood, 3);
                Rect(image, cx + 13, oy + 7, 10, 5, StoneLight);
                break;
            default:
                Rect(image, cx - 11, oy + 6, 23, 8, accent);
                Shield(image, cx - 13, oy + 30, cloth.Lightened(0.18f));
                Line(image, cx + 12, oy + 35, cx + 18, oy + 11, StoneLight, 3);
                break;
        }
    }

    private static void DrawPortrait(Image image, RaceKind race, int variant, int ox, int oy)
    {
        int cx = ox + PortraitCell / 2;
        Color skin = RaceSkin(race);
        Color cloth = RaceColor(race);
        Color accent = RaceAccent(race);
        Circle(image, cx, oy + 44, 25, skin);
        Rect(image, cx - 31, oy + 63, 63, 30, cloth.Darkened(0.15f));
        Rect(image, cx - 26, oy + 60, 52, 9, accent);
        Pixel(image, cx - 9, oy + 42, Ink);
        Pixel(image, cx + 9, oy + 42, Ink);
        Line(image, cx - 8, oy + 54, cx + 8, oy + 54, Ink, 2);
        if (variant == 0)
        {
            Crown(image, cx, oy + 18, accent);
            Rect(image, cx - 28, oy + 70, 8, 20, Gold);
            Rect(image, cx + 20, oy + 70, 8, 20, Gold);
        }
        else
        {
            Rect(image, cx - 28, oy + 18, 57, 8, cloth.Darkened(0.35f));
            Diamond(image, cx, oy + 17, 6, accent.Lightened(0.2f));
            Line(image, cx + 29, oy + 62, cx + 39, oy + 31, WoodLight, 4);
            Circle(image, cx + 40, oy + 28, 7, accent.Lightened(0.25f));
        }
        if (race == RaceKind.Sylvan)
        {
            Polygon(image, new[] { P(cx - 23, oy + 40), P(cx - 37, oy + 32), P(cx - 23, oy + 49) }, skin);
            Polygon(image, new[] { P(cx + 23, oy + 40), P(cx + 37, oy + 32), P(cx + 23, oy + 49) }, skin);
        }
        if (race == RaceKind.Dwarf || race == RaceKind.Orc)
            Polygon(image, new[] { P(cx - 18, oy + 53), P(cx, oy + 82), P(cx + 18, oy + 53) }, RaceHair(race));
        if (race == RaceKind.Tideborn)
            for (int i = 0; i < 4; i++)
                Line(image, cx - 20 + i * 12, oy + 21, cx - 24 + i * 15, oy + 7, BlueLight, 4);
        if (race == RaceKind.Arcane)
            Star(image, cx, oy + 14, 10, PurpleLight);
    }

    private static void DrawBuilding(Image image, BuildingKind kind, int ox, int oy)
    {
        int cx = ox + BuildingCell / 2;
        int groundY = oy + 68;
        Ellipse(image, cx, groundY, 29, 7, new Color("4f6842").WithAlpha(0.85f));
        switch (kind)
        {
            case BuildingKind.House:
            case BuildingKind.Clinic:
                BuildingBody(image, cx, oy + 51, 42, 30, StoneLight.Darkened(0.25f), kind == BuildingKind.Clinic ? Colors.White : WoodLight);
                Roof(image, cx, oy + 35, 48, kind == BuildingKind.Clinic ? Blue : new Color("315f9e"));
                Rect(image, cx - 5, oy + 51, 10, 17, Wood.Darkened(0.2f));
                if (kind == BuildingKind.Clinic)
                {
                    Rect(image, cx - 3, oy + 24, 6, 17, Red);
                    Rect(image, cx - 9, oy + 29, 18, 6, Red);
                }
                break;
            case BuildingKind.Farm:
                BuildingBody(image, cx - 10, oy + 50, 32, 25, WoodLight, new Color("d6a640"));
                Roof(image, cx - 10, oy + 38, 38, Gold);
                for (int row = 0; row < 4; row++)
                    Line(image, cx + 10, oy + 48 + row * 5, cx + 31, oy + 48 + row * 5, GreenLight, 2);
                break;
            case BuildingKind.Market:
                Rect(image, cx - 24, oy + 43, 48, 8, Blue);
                for (int i = 0; i < 5; i++)
                    Rect(image, cx - 24 + i * 10, oy + 43, 7, 8, i % 2 == 0 ? Blue : Colors.White);
                Line(image, cx - 21, oy + 51, cx - 21, oy + 67, Wood, 3);
                Line(image, cx + 21, oy + 51, cx + 21, oy + 67, Wood, 3);
                Circle(image, cx - 10, oy + 60, 5, Orange);
                Circle(image, cx + 4, oy + 61, 5, Gold);
                break;
            case BuildingKind.Lumberyard:
                for (int i = 0; i < 5; i++)
                {
                    Rect(image, cx - 23 + i * 9, oy + 50 + i % 2 * 6, 15, 7, Wood);
                    Circle(image, cx - 23 + i * 9, oy + 53 + i % 2 * 6, 4, WoodLight);
                }
                Tree(image, cx + 22, oy + 47, 15);
                break;
            case BuildingKind.Quarry:
            case BuildingKind.Mine:
                for (int i = 0; i < 5; i++)
                    Circle(image, cx - 20 + i * 10, oy + 56 - i % 2 * 8, 10, Stone.Darkened(i * 0.04f));
                if (kind == BuildingKind.Mine)
                {
                    Rect(image, cx - 13, oy + 47, 26, 22, Ink);
                    Line(image, cx - 15, oy + 47, cx - 15, oy + 69, WoodLight, 4);
                    Line(image, cx + 15, oy + 47, cx + 15, oy + 69, WoodLight, 4);
                    Line(image, cx - 16, oy + 47, cx + 16, oy + 47, WoodLight, 4);
                }
                break;
            case BuildingKind.Temple:
                Rect(image, cx - 24, oy + 47, 48, 21, StoneLight);
                for (int i = -2; i <= 2; i++)
                    Rect(image, cx + i * 10 - 3, oy + 35, 6, 33, Colors.White);
                Polygon(image, new[] { P(cx - 29, oy + 36), P(cx, oy + 20), P(cx + 29, oy + 36) }, StoneLight);
                Diamond(image, cx, oy + 18, 7, BlueLight);
                break;
            case BuildingKind.Barracks:
            case BuildingKind.Smelter:
            case BuildingKind.Workshop:
                BuildingBody(image, cx, oy + 47, 48, 32, Stone.Darkened(0.12f), Wood);
                Roof(image, cx, oy + 34, 53, kind == BuildingKind.Barracks ? Red : new Color("734a32"));
                if (kind == BuildingKind.Smelter)
                {
                    Rect(image, cx + 16, oy + 21, 9, 30, Stone);
                    Flame(image, cx - 9, oy + 62, Orange);
                }
                else if (kind == BuildingKind.Workshop)
                    Gear(image, cx, oy + 55, Gold);
                else
                    Shield(image, cx, oy + 55, Red.Lightened(0.1f));
                break;
            case BuildingKind.Wall:
            case BuildingKind.Gate:
                Rect(image, cx - 32, oy + 38, 64, 30, Stone);
                for (int i = 0; i < 6; i++)
                    Rect(image, cx - 32 + i * 12, oy + 31, 8, 10, StoneLight);
                if (kind == BuildingKind.Gate)
                    Rect(image, cx - 10, oy + 46, 20, 22, Wood.Darkened(0.25f));
                break;
            case BuildingKind.Keep:
                Rect(image, cx - 25, oy + 29, 50, 39, Stone);
                Rect(image, cx - 31, oy + 36, 14, 32, StoneLight.Darkened(0.15f));
                Rect(image, cx + 17, oy + 36, 14, 32, StoneLight.Darkened(0.15f));
                Rect(image, cx - 8, oy + 48, 16, 20, Wood);
                Flag(image, cx, oy + 28, Blue);
                break;
            case BuildingKind.Harbor:
            case BuildingKind.Shipyard:
                Rect(image, cx - 30, oy + 59, 60, 7, Wood);
                for (int i = -2; i <= 2; i++)
                    Line(image, cx + i * 12, oy + 60, cx + i * 12, oy + 72, Wood.Darkened(0.2f), 3);
                Ship(image, cx + (kind == BuildingKind.Shipyard ? 6 : -4), oy + 51, Blue);
                break;
            case BuildingKind.MageTower:
                Rect(image, cx - 15, oy + 25, 30, 43, Stone.Darkened(0.2f));
                Polygon(image, new[] { P(cx - 21, oy + 27), P(cx, oy + 8), P(cx + 21, oy + 27) }, Purple);
                Diamond(image, cx, oy + 8, 8, PurpleLight);
                for (int i = 0; i < 4; i++)
                    Pixel(image, cx - 18 + i * 12, oy + 18 + i % 2 * 8, PurpleLight);
                break;
            case BuildingKind.Monument:
                Rect(image, cx - 18, oy + 57, 36, 11, StoneLight);
                Rect(image, cx - 7, oy + 28, 14, 29, Gold);
                Circle(image, cx, oy + 20, 9, GoldLight);
                Line(image, cx - 6, oy + 34, cx - 21, oy + 21, Gold, 5);
                Line(image, cx + 6, oy + 34, cx + 21, oy + 21, Gold, 5);
                Diamond(image, cx, oy + 44, 6, BlueLight);
                break;
        }
    }

    private static void DrawEffect(Image image, GameArtEffect effect, int ox, int oy)
    {
        int cx = ox + EffectCell / 2;
        int cy = oy + EffectCell / 2;
        switch (effect)
        {
            case GameArtEffect.HealingAura:
                Rings(image, cx, cy + 9, GreenLight);
                Rect(image, cx - 3, cy - 16, 6, 28, Colors.White);
                Rect(image, cx - 13, cy - 6, 26, 7, Colors.White);
                break;
            case GameArtEffect.BlessingBeam:
                for (int i = 0; i < 7; i++)
                    Line(image, cx - 12 + i * 4, cy + 22, cx - 5 + i * 2, cy - 24, GoldLight.WithAlpha(0.35f + i * 0.05f), 3);
                Star(image, cx, cy - 17, 10, GoldLight);
                break;
            case GameArtEffect.CurseCloud:
            case GameArtEffect.PlagueCloud:
                for (int i = 0; i < 8; i++)
                    Circle(image, cx - 17 + i * 5, cy + (i % 3 - 1) * 6, 9, (effect == GameArtEffect.CurseCloud ? Purple : Green).WithAlpha(0.6f));
                Skull(image, cx, cy + 2, effect == GameArtEffect.CurseCloud ? PurpleLight : GreenLight);
                break;
            case GameArtEffect.LightningStrike:
                Polygon(image, new[] { P(cx + 5, cy - 26), P(cx - 8, cy - 2), P(cx, cy - 2), P(cx - 7, cy + 25), P(cx + 14, cy - 8), P(cx + 4, cy - 8) }, BlueLight);
                Rings(image, cx, cy + 23, Blue);
                break;
            case GameArtEffect.Meteor:
                Circle(image, cx - 4, cy + 9, 11, Orange);
                for (int i = 0; i < 5; i++)
                    Line(image, cx - 9 - i * 3, cy + 3 - i * 4, cx + 12 - i * 3, cy - 22 - i * 2, i % 2 == 0 ? Orange : GoldLight, 4);
                break;
            case GameArtEffect.FireBurst:
                Flame(image, cx - 8, cy + 16, Orange);
                Flame(image, cx + 8, cy + 18, Red);
                Flame(image, cx, cy + 8, GoldLight);
                break;
            case GameArtEffect.Smoke:
                for (int i = 0; i < 6; i++)
                    Circle(image, cx + (i % 2 == 0 ? -5 : 6), cy + 20 - i * 8, 8 + i, Stone.WithAlpha(0.25f + i * 0.06f));
                break;
            case GameArtEffect.HolySigil:
                Circle(image, cx, cy, 23, Gold.WithAlpha(0.35f));
                CircleOutline(image, cx, cy, 20, GoldLight, 2);
                Rect(image, cx - 3, cy - 16, 6, 32, GoldLight);
                Rect(image, cx - 13, cy - 5, 26, 7, GoldLight);
                break;
            case GameArtEffect.MagicCircle:
                CircleOutline(image, cx, cy, 23, PurpleLight, 2);
                CircleOutline(image, cx, cy, 14, BlueLight, 2);
                for (int i = 0; i < 6; i++)
                {
                    double angle = i * Math.PI / 3;
                    Star(image, cx + (int)(Math.Cos(angle) * 19), cy + (int)(Math.Sin(angle) * 19), 3, PurpleLight);
                }
                break;
            case GameArtEffect.ForestGrowth:
                Tree(image, cx, cy + 16, 24);
                for (int i = 0; i < 8; i++)
                    Circle(image, cx - 22 + i * 6, cy + 22 - i % 3 * 7, 2, GreenLight);
                break;
            case GameArtEffect.ShieldBubble:
                Circle(image, cx, cy, 25, Blue.WithAlpha(0.18f));
                CircleOutline(image, cx, cy, 25, BlueLight.WithAlpha(0.85f), 2);
                Star(image, cx, cy, 8, Colors.White.WithAlpha(0.6f));
                break;
            case GameArtEffect.ManaCrystal:
                Diamond(image, cx, cy, 22, BlueLight);
                Diamond(image, cx - 8, cy + 10, 9, Blue);
                Diamond(image, cx + 12, cy + 13, 8, PurpleLight);
                break;
            case GameArtEffect.RelicIdol:
                Rect(image, cx - 13, cy + 7, 26, 18, Gold);
                Circle(image, cx, cy - 5, 12, GoldLight);
                Rect(image, cx - 17, cy + 24, 34, 5, StoneLight);
                Diamond(image, cx, cy - 6, 5, Red);
                break;
            case GameArtEffect.AncientRuin:
                Rect(image, cx - 19, cy - 13, 9, 34, Stone);
                Rect(image, cx + 8, cy - 4, 10, 25, StoneLight.Darkened(0.2f));
                Rect(image, cx - 23, cy - 16, 43, 6, StoneLight);
                Line(image, cx - 22, cy + 22, cx + 22, cy + 22, Green, 3);
                Diamond(image, cx, cy - 20, 5, PurpleLight);
                break;
            case GameArtEffect.Crown:
                Crown(image, cx, cy, GoldLight);
                break;
            case GameArtEffect.TreasureChest:
                Rect(image, cx - 22, cy - 2, 44, 25, Wood);
                Rect(image, cx - 22, cy - 13, 44, 14, WoodLight);
                Rect(image, cx - 3, cy - 5, 6, 17, GoldLight);
                for (int i = 0; i < 5; i++)
                    Circle(image, cx - 15 + i * 8, cy - 15 - i % 2 * 3, 3, Gold);
                break;
            case GameArtEffect.BattleDust:
            case GameArtEffect.BattleDustLarge:
                int count = effect == GameArtEffect.BattleDust ? 7 : 12;
                for (int i = 0; i < count; i++)
                    Circle(image, cx - 24 + (i * 11) % 48, cy + 18 - (i * 13) % 37, 4 + i % 5, new Color("b69a70").WithAlpha(0.35f + i % 3 * 0.12f));
                break;
            case GameArtEffect.Anchor:
                CircleOutline(image, cx, cy - 13, 7, StoneLight, 3);
                Line(image, cx, cy - 6, cx, cy + 19, StoneLight, 4);
                Line(image, cx - 18, cy + 10, cx, cy + 23, StoneLight, 4);
                Line(image, cx + 18, cy + 10, cx, cy + 23, StoneLight, 4);
                Line(image, cx - 17, cy + 10, cx - 10, cy + 2, StoneLight, 3);
                Line(image, cx + 17, cy + 10, cx + 10, cy + 2, StoneLight, 3);
                break;
        }
    }

    private static Color RaceColor(RaceKind race) => race switch
    {
        RaceKind.Sylvan => new Color("477f43"),
        RaceKind.Dwarf => new Color("a85a2a"),
        RaceKind.Orc => new Color("7b3529"),
        RaceKind.Tideborn => new Color("197c9f"),
        RaceKind.Arcane => new Color("673d9e"),
        _ => new Color("315f9e"),
    };

    private static Color RaceAccent(RaceKind race) => race switch
    {
        RaceKind.Sylvan => GreenLight,
        RaceKind.Dwarf => Gold,
        RaceKind.Orc => Red,
        RaceKind.Tideborn => BlueLight,
        RaceKind.Arcane => PurpleLight,
        _ => GoldLight,
    };

    private static Color RaceSkin(RaceKind race) => race switch
    {
        RaceKind.Sylvan => new Color("b1d483"),
        RaceKind.Dwarf => new Color("d18b58"),
        RaceKind.Orc => new Color("6ca14b"),
        RaceKind.Tideborn => new Color("58b7cc"),
        RaceKind.Arcane => new Color("bd98dc"),
        _ => new Color("dba172"),
    };

    private static Color RaceHair(RaceKind race) => race switch
    {
        RaceKind.Sylvan => new Color("574324"),
        RaceKind.Dwarf => new Color("9d4824"),
        RaceKind.Orc => Ink,
        RaceKind.Tideborn => Blue,
        RaceKind.Arcane => new Color("e0d9ff"),
        _ => new Color("60402a"),
    };

    private static void OutlineCell(Image image, int ox, int oy, int cell)
    {
        Color glow = new Color(1, 1, 1, 0.06f);
        Line(image, ox + 5, oy + cell - 4, ox + cell - 5, oy + cell - 4, glow, 1);
    }

    private static void BuildingBody(Image image, int cx, int y, int width, int height, Color wall, Color trim)
    {
        Rect(image, cx - width / 2, y, width, height, wall);
        Rect(image, cx - width / 2, y, width, 4, trim);
        Rect(image, cx - width / 2 + 5, y + 10, 7, 8, BlueLight.Darkened(0.25f));
        Rect(image, cx + width / 2 - 12, y + 10, 7, 8, BlueLight.Darkened(0.25f));
    }

    private static void Roof(Image image, int cx, int y, int width, Color color)
        => Polygon(image, new[] { P(cx - width / 2, y + 15), P(cx, y), P(cx + width / 2, y + 15) }, color);

    private static void House(Image image, int cx, int cy, Color roof)
    {
        Rect(image, cx - 13, cy - 7, 26, 18, WoodLight);
        Polygon(image, new[] { P(cx - 16, cy - 7), P(cx, cy - 19), P(cx + 16, cy - 7) }, roof);
        Rect(image, cx - 4, cy + 1, 8, 10, Wood);
    }

    private static void Ship(Image image, int cx, int cy, Color sail)
    {
        Polygon(image, new[] { P(cx - 21, cy + 8), P(cx + 20, cy + 8), P(cx + 13, cy + 16), P(cx - 14, cy + 16) }, Wood);
        Line(image, cx, cy + 8, cx, cy - 21, WoodLight, 3);
        Polygon(image, new[] { P(cx + 1, cy - 19), P(cx + 1, cy + 5), P(cx + 18, cy + 1) }, sail.Lightened(0.25f));
        Line(image, cx - 20, cy + 18, cx + 20, cy + 18, BlueLight.WithAlpha(0.65f), 3);
    }

    private static void Tree(Image image, int cx, int cy, int radius)
    {
        Rect(image, cx - 3, cy, 6, radius, Wood);
        Circle(image, cx, cy - radius / 2, radius, Green.Darkened(0.1f));
        Circle(image, cx - radius / 2, cy - radius / 3, radius / 2 + 2, GreenLight.Darkened(0.1f));
        Circle(image, cx + radius / 2, cy - radius / 3, radius / 2 + 2, Green);
    }

    private static void Flame(Image image, int cx, int cy, Color color)
    {
        Polygon(image, new[] { P(cx, cy - 24), P(cx - 7, cy - 7), P(cx - 15, cy + 9), P(cx - 8, cy + 20), P(cx, cy + 24), P(cx + 11, cy + 16), P(cx + 14, cy + 3), P(cx + 6, cy - 8) }, color);
        Polygon(image, new[] { P(cx, cy - 10), P(cx - 5, cy + 5), P(cx, cy + 17), P(cx + 7, cy + 5) }, GoldLight);
    }

    private static void Shield(Image image, int cx, int cy, Color color)
    {
        Polygon(image, new[] { P(cx - 13, cy - 15), P(cx + 13, cy - 15), P(cx + 11, cy + 7), P(cx, cy + 18), P(cx - 11, cy + 7) }, color);
        Line(image, cx, cy - 12, cx, cy + 13, GoldLight, 2);
    }

    private static void Skull(Image image, int cx, int cy, Color color)
    {
        Circle(image, cx, cy - 3, 14, color);
        Rect(image, cx - 9, cy + 7, 18, 9, color.Darkened(0.15f));
        Circle(image, cx - 6, cy - 5, 4, Ink);
        Circle(image, cx + 6, cy - 5, 4, Ink);
        Rect(image, cx - 2, cy + 2, 4, 6, Ink);
        for (int i = -2; i <= 2; i++)
            Rect(image, cx + i * 4 - 1, cy + 11, 2, 5, Ink);
    }

    private static void Heart(Image image, int cx, int cy, Color color)
    {
        Circle(image, cx - 7, cy - 5, 9, color);
        Circle(image, cx + 7, cy - 5, 9, color);
        Polygon(image, new[] { P(cx - 15, cy - 3), P(cx + 15, cy - 3), P(cx, cy + 19) }, color);
    }

    private static void Crown(Image image, int cx, int cy, Color color)
    {
        Polygon(image, new[] { P(cx - 20, cy + 10), P(cx - 17, cy - 11), P(cx - 7, cy), P(cx, cy - 17), P(cx + 7, cy), P(cx + 17, cy - 11), P(cx + 20, cy + 10) }, color);
        Rect(image, cx - 20, cy + 8, 40, 8, Gold);
        Diamond(image, cx, cy + 9, 4, BlueLight);
    }

    private static void Flag(Image image, int cx, int cy, Color color)
    {
        Line(image, cx, cy - 18, cx, cy + 13, WoodLight, 2);
        Polygon(image, new[] { P(cx + 2, cy - 17), P(cx + 20, cy - 12), P(cx + 2, cy - 6) }, color);
    }

    private static void Gear(Image image, int cx, int cy, Color color)
    {
        Circle(image, cx, cy, 14, color);
        for (int i = 0; i < 8; i++)
        {
            double angle = i * Math.PI / 4;
            int x = cx + (int)(Math.Cos(angle) * 18);
            int y = cy + (int)(Math.Sin(angle) * 18);
            Rect(image, x - 3, y - 3, 7, 7, color);
        }
        Circle(image, cx, cy, 6, Ink);
        Circle(image, cx, cy, 3, Gold);
    }

    private static void Rings(Image image, int cx, int cy, Color color)
    {
        CircleOutline(image, cx, cy, 22, color.WithAlpha(0.35f), 2);
        CircleOutline(image, cx, cy, 14, color.WithAlpha(0.65f), 2);
        CircleOutline(image, cx, cy, 7, color, 2);
    }

    private static void Star(Image image, int cx, int cy, int radius, Color color)
    {
        Polygon(image, new[] { P(cx, cy - radius), P(cx + radius / 3, cy - radius / 3), P(cx + radius, cy), P(cx + radius / 3, cy + radius / 3), P(cx, cy + radius), P(cx - radius / 3, cy + radius / 3), P(cx - radius, cy), P(cx - radius / 3, cy - radius / 3) }, color);
    }

    private static void Diamond(Image image, int cx, int cy, int radius, Color color)
        => Polygon(image, new[] { P(cx, cy - radius), P(cx + radius, cy), P(cx, cy + radius), P(cx - radius, cy) }, color);

    private static void Ellipse(Image image, int cx, int cy, int rx, int ry, Color color)
    {
        for (int y = -ry; y <= ry; y++)
            for (int x = -rx; x <= rx; x++)
                if ((x * x) / (float)(rx * rx) + (y * y) / (float)(ry * ry) <= 1)
                    Pixel(image, cx + x, cy + y, color);
    }

    private static void Circle(Image image, int cx, int cy, int radius, Color color)
    {
        int rr = radius * radius;
        for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
                if (x * x + y * y <= rr)
                    Pixel(image, cx + x, cy + y, color);
    }

    private static void CircleOutline(Image image, int cx, int cy, int radius, Color color, int width)
    {
        int outer = radius * radius;
        int innerRadius = Math.Max(0, radius - width);
        int inner = innerRadius * innerRadius;
        for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                int d = x * x + y * y;
                if (d <= outer && d >= inner)
                    Pixel(image, cx + x, cy + y, color);
            }
    }

    private static void Rect(Image image, int x, int y, int width, int height, Color color)
    {
        for (int py = y; py < y + height; py++)
            for (int px = x; px < x + width; px++)
                Pixel(image, px, py, color);
    }

    private static void Line(Image image, int x0, int y0, int x1, int y1, Color color, int width)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            Rect(image, x0 - width / 2, y0 - width / 2, width, width, color);
            if (x0 == x1 && y0 == y1) break;
            int doubled = 2 * error;
            if (doubled >= dy) { error += dy; x0 += sx; }
            if (doubled <= dx) { error += dx; y0 += sy; }
        }
    }

    private static void Polygon(Image image, Vector2I[] points, Color color)
    {
        int minY = points.Min(p => p.Y);
        int maxY = points.Max(p => p.Y);
        for (int y = minY; y <= maxY; y++)
        {
            var intersections = new List<int>();
            for (int i = 0; i < points.Length; i++)
            {
                Vector2I a = points[i];
                Vector2I b = points[(i + 1) % points.Length];
                if ((a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y))
                {
                    float t = (y - a.Y) / (float)(b.Y - a.Y);
                    intersections.Add((int)MathF.Round(a.X + t * (b.X - a.X)));
                }
            }
            intersections.Sort();
            for (int i = 0; i + 1 < intersections.Count; i += 2)
                Rect(image, intersections[i], y, intersections[i + 1] - intersections[i] + 1, 1, color);
        }
    }

    private static Vector2I P(int x, int y) => new(x, y);

    private static void Pixel(Image image, int x, int y, Color color)
    {
        if (x >= 0 && y >= 0 && x < image.GetWidth() && y < image.GetHeight())
            image.SetPixel(x, y, color);
    }
}
