using Godot;
using WorldForge.Core.Simulation;

namespace WorldForge.Presentation;

/// <summary>
/// Generates one compact in-memory sprite atlas. Every settler animation reuses this texture;
/// no Sprite2D or AnimatedSprite2D node is created per citizen.
/// </summary>
public sealed class ProceduralPixelAtlas
{
    public const int FrameWidth = 10;
    public const int FrameHeight = 14;
    public const int FrameCount = 4;
    public const int RaceCount = 6;

    public Texture2D Texture { get; }

    public ProceduralPixelAtlas()
    {
        Image image = Image.CreateEmpty(FrameWidth * FrameCount, FrameHeight * RaceCount, false, Image.Format.Rgba8);
        image.Fill(Colors.Transparent);
        foreach (RaceKind race in Enum.GetValues<RaceKind>())
            for (int frame = 0; frame < FrameCount; frame++)
                DrawFrame(image, race, frame);
        Texture = ImageTexture.CreateFromImage(image);
    }

    public Rect2 SourceRect(RaceKind race, int frame)
    {
        frame = Math.Abs(frame) % FrameCount;
        return new Rect2(frame * FrameWidth, (int)race * FrameHeight, FrameWidth, FrameHeight);
    }

    private static void DrawFrame(Image image, RaceKind race, int frame)
    {
        int ox = frame * FrameWidth;
        int oy = (int)race * FrameHeight;
        Color skin = race switch
        {
            RaceKind.Sylvan => new Color(0.75f, 0.9f, 0.62f),
            RaceKind.Dwarf => new Color(0.82f, 0.58f, 0.38f),
            RaceKind.Orc => new Color(0.42f, 0.68f, 0.32f),
            RaceKind.Tideborn => new Color(0.42f, 0.78f, 0.88f),
            RaceKind.Arcane => new Color(0.78f, 0.58f, 0.94f),
            _ => new Color(0.9f, 0.7f, 0.52f),
        };
        Color cloth = race switch
        {
            RaceKind.Sylvan => new Color(0.18f, 0.56f, 0.26f),
            RaceKind.Dwarf => new Color(0.5f, 0.31f, 0.18f),
            RaceKind.Orc => new Color(0.42f, 0.22f, 0.16f),
            RaceKind.Tideborn => new Color(0.12f, 0.4f, 0.65f),
            RaceKind.Arcane => new Color(0.36f, 0.16f, 0.58f),
            _ => new Color(0.22f, 0.36f, 0.62f),
        };
        Color dark = cloth.Darkened(0.42f);
        Color hair = race switch
        {
            RaceKind.Sylvan => new Color(0.32f, 0.22f, 0.1f),
            RaceKind.Dwarf => new Color(0.5f, 0.22f, 0.08f),
            RaceKind.Orc => new Color(0.12f, 0.1f, 0.08f),
            RaceKind.Tideborn => new Color(0.08f, 0.34f, 0.48f),
            RaceKind.Arcane => new Color(0.85f, 0.82f, 1f),
            _ => new Color(0.2f, 0.12f, 0.08f),
        };

        int bounce = frame is 1 or 3 ? 1 : 0;
        int legShift = frame switch { 1 => -1, 3 => 1, _ => 0 };
        Fill(image, ox + 3, oy + 1 + bounce, 4, 2, hair);
        Fill(image, ox + 3, oy + 3 + bounce, 4, 3, skin);
        Pixel(image, ox + 6, oy + 4 + bounce, Colors.Black);
        Fill(image, ox + 2, oy + 6 + bounce, 6, 4, cloth);
        Fill(image, ox + 1, oy + 7 + bounce, 1, 3, skin);
        Fill(image, ox + 8, oy + 7 + bounce, 1, 3, skin);
        Fill(image, ox + 3 + legShift, oy + 10, 2, 3, dark);
        Fill(image, ox + 5 - legShift, oy + 10, 2, 3, dark);

        if (race == RaceKind.Sylvan)
        {
            Pixel(image, ox + 2, oy + 3 + bounce, skin);
            Pixel(image, ox + 7, oy + 3 + bounce, skin);
        }
        if (race == RaceKind.Dwarf)
            Fill(image, ox + 3, oy + 6 + bounce, 4, 2, hair);
        if (race == RaceKind.Tideborn)
        {
            Pixel(image, ox + 2, oy + 2 + bounce, skin.Lightened(0.2f));
            Pixel(image, ox + 7, oy + 2 + bounce, skin.Lightened(0.2f));
        }
        if (race == RaceKind.Arcane)
            Pixel(image, ox + 4, oy, new Color(0.8f, 0.45f, 1f));
    }

    private static void Fill(Image image, int x, int y, int width, int height, Color color)
    {
        for (int py = y; py < y + height; py++)
            for (int px = x; px < x + width; px++)
                Pixel(image, px, py, color);
    }

    private static void Pixel(Image image, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= image.GetWidth() || y >= image.GetHeight()) return;
        image.SetPixel(x, y, color);
    }
}
