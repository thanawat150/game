using Godot;
using WorldForge.Core.World;

namespace WorldForge.Presentation;

public sealed partial class BrushOverlay : Node2D
{
    private int _radius;
    private int _tilePixelSize = 4;
    private TerrainType _terrain;

    public void SetBrush(Vector2I tile, int radius, TerrainType terrain, int tilePixelSize)
    {
        _radius = Math.Max(0, radius);
        _terrain = terrain;
        _tilePixelSize = tilePixelSize;
        Position = new Vector2(tile.X * tilePixelSize, tile.Y * tilePixelSize);
        Visible = true;
        QueueRedraw();
    }

    public override void _Draw()
    {
        int diameter = _radius * 2 + 1;
        float start = -_radius * _tilePixelSize;
        var rect = new Rect2(start, start, diameter * _tilePixelSize, diameter * _tilePixelSize);
        Color color = TerrainColor(_terrain);
        DrawRect(rect, new Color(color, 0.12f), filled: true);
        DrawRect(rect, new Color(color, 0.9f), filled: false, width: 1.5f);
    }

    private static Color TerrainColor(TerrainType terrain) => terrain switch
    {
        TerrainType.DeepOcean => Color.FromHtml("#102A56"),
        TerrainType.ShallowWater => Color.FromHtml("#236AA0"),
        TerrainType.Beach => Color.FromHtml("#D8C27A"),
        TerrainType.Grassland => Color.FromHtml("#74A84A"),
        TerrainType.Forest => Color.FromHtml("#285A35"),
        TerrainType.Mountain => Color.FromHtml("#A0A1A4"),
        _ => Colors.White,
    };
}
