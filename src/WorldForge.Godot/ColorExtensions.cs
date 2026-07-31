using Godot;

namespace WorldForge.Presentation;

internal static class ColorExtensions
{
    /// <summary>Returns the same RGB color with an explicit alpha value.</summary>
    public static Color WithAlpha(this Color color, float alpha)
        => new(color.R, color.G, color.B, Math.Clamp(alpha, 0f, 1f));
}
