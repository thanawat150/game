using Godot;

namespace WorldForge.Presentation;

internal sealed class PackedVector2Array
{
    public PackedVector2Array(Vector2[] points) => Points = points;
    public Vector2[] Points { get; }
}

public sealed partial class WorldExpansionRenderer : Node2D
{
    private void DrawTextureRectRegion(Rect2 destination, Texture2D texture, Rect2 source)
        => base.DrawTextureRectRegion(texture, destination, source);

    private void DrawColoredPolygon(PackedVector2Array points, Color color)
        => base.DrawColoredPolygon(points.Points, color);
}
