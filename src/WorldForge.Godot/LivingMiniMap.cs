using Godot;
using WorldForge.Core.Simulation;
using WorldForge.Core.World;

namespace WorldForge.Presentation;

public sealed partial class LivingMiniMap : Control
{
    private WorldMap? _world;
    private GrandSimulation? _simulation;
    private LivingWorldDirector? _director;

    public LivingOverlayMode OverlayMode { get; set; }
    public Vector2 CameraWorldPosition { get; set; }
    public Vector2 CameraWorldSize { get; set; } = new(400, 240);
    public int TilePixelSize { get; set; } = 4;
    public event Action<Vector2I>? TileRequested;

    public void Bind(WorldMap world, GrandSimulation simulation, LivingWorldDirector director)
    {
        _world = world;
        _simulation = simulation;
        _director = director;
        QueueRedraw();
    }

    public void Refresh() => QueueRedraw();

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton button || !button.Pressed || button.ButtonIndex != MouseButton.Left || _world is null)
            return;
        Vector2 local = GetLocalMousePosition();
        int x = Math.Clamp((int)(local.X / Math.Max(1, Size.X) * _world.Width), 0, _world.Width - 1);
        int y = Math.Clamp((int)(local.Y / Math.Max(1, Size.Y) * _world.Height), 0, _world.Height - 1);
        TileRequested?.Invoke(new Vector2I(x, y));
        AcceptEvent();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.025f, 0.03f, 0.045f, 0.96f));
        if (_world is null || _simulation is null || _director is null || Size.X <= 2 || Size.Y <= 2)
            return;

        int sampleX = Math.Max(1, _world.Width / 64);
        int sampleY = Math.Max(1, _world.Height / 42);
        float sx = Size.X / _world.Width;
        float sy = Size.Y / _world.Height;

        for (int y = 0; y < _world.Height; y += sampleY)
        {
            for (int x = 0; x < _world.Width; x += sampleX)
            {
                Color color = TerrainColor(_world.GetTerrain(x, y));
                DrawRect(new Rect2(new Vector2(x * sx, y * sy), new Vector2(Math.Max(1, sampleX * sx + 0.5f), Math.Max(1, sampleY * sy + 0.5f))), color);
            }
        }

        DrawOverlay(sx, sy);

        foreach (SettlementState city in _simulation.State.Settlements.Values)
        {
            Color color = city.KingdomId is ulong kingdomId ? KingdomColor(kingdomId) : Colors.Gold;
            Vector2 point = new(city.X * sx, city.Y * sy);
            float radius = city.Stage switch
            {
                SettlementStage.Capital => 4,
                SettlementStage.City => 3.5f,
                SettlementStage.Town => 3,
                _ => 2.4f,
            };
            DrawCircle(point, radius + 1, new Color(0.02f, 0.02f, 0.03f, 0.9f));
            DrawCircle(point, radius, color);
        }

        foreach (ArmyState army in _simulation.State.Armies.Values.Where(a => a.IsActive))
            DrawCircle(new Vector2(army.X * sx, army.Y * sy), 2.4f, new Color(1f, 0.15f, 0.08f));

        float worldWidthPixels = _world.Width * TilePixelSize;
        float worldHeightPixels = _world.Height * TilePixelSize;
        Vector2 viewportTopLeft = CameraWorldPosition - CameraWorldSize / 2f;
        var cameraRect = new Rect2(
            new Vector2(viewportTopLeft.X / worldWidthPixels * Size.X, viewportTopLeft.Y / worldHeightPixels * Size.Y),
            new Vector2(CameraWorldSize.X / worldWidthPixels * Size.X, CameraWorldSize.Y / worldHeightPixels * Size.Y));
        DrawRect(cameraRect, new Color(1f, 1f, 1f, 0.8f), false, 1.4f);
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(1f, 1f, 1f, 0.28f), false, 1f);
    }

    private void DrawOverlay(float sx, float sy)
    {
        if (_simulation is null || _director is null || OverlayMode == LivingOverlayMode.None)
            return;
        GrandSimulationState simulation = _simulation.State;
        switch (OverlayMode)
        {
            case LivingOverlayMode.Population:
                foreach (IGrouping<(int X, int Y), SimEntity> group in simulation.Entities.Values.Where(e => e.IsAlive).GroupBy(e => (e.X / 12, e.Y / 12)))
                {
                    int count = group.Count();
                    Vector2 p = new((group.Key.X * 12 + 6) * sx, (group.Key.Y * 12 + 6) * sy);
                    DrawCircle(p, MathF.Min(6, 1.2f + MathF.Sqrt(count) * 0.5f), new Color(0.15f, 1f, 0.48f, 0.5f));
                }
                break;
            case LivingOverlayMode.Disease:
                HashSet<ulong> infected = simulation.Diseases.SelectMany(d => d.InfectedDays.Keys).ToHashSet();
                foreach (SimEntity entity in simulation.Entities.Values.Where(e => infected.Contains(e.Id)))
                    DrawCircle(new Vector2(entity.X * sx, entity.Y * sy), 2, new Color(1f, 0.3f, 0.05f, 0.75f));
                break;
            case LivingOverlayMode.War:
                foreach (ArmyState army in simulation.Armies.Values.Where(a => a.IsActive))
                    DrawCircle(new Vector2(army.X * sx, army.Y * sy), 4, new Color(1f, 0.08f, 0.05f, 0.45f));
                break;
            case LivingOverlayMode.Kingdom:
                foreach (SettlementState city in simulation.Settlements.Values.Where(c => c.KingdomId is not null))
                    DrawCircle(new Vector2(city.X * sx, city.Y * sy), 9, WithAlpha(KingdomColor(city.KingdomId!.Value), 0.28f));
                break;
            case LivingOverlayMode.Food:
            case LivingOverlayMode.Happiness:
            case LivingOverlayMode.Performance:
                foreach (SettlementState city in simulation.Settlements.Values)
                {
                    float value = OverlayMode switch
                    {
                        LivingOverlayMode.Food => Math.Clamp(city.Food / 180f, 0, 1),
                        LivingOverlayMode.Happiness => Math.Clamp(city.Happiness / 100f, 0, 1),
                        _ => Math.Clamp(simulation.Entities.Values.Count(e => e.IsAlive && DistanceSquared(e.X, e.Y, city.X, city.Y) < 400) / 100f, 0, 1),
                    };
                    DrawCircle(new Vector2(city.X * sx, city.Y * sy), 5 + value * 5, WithAlpha(HeatColor(value), 0.45f));
                }
                break;
            case LivingOverlayMode.Weather:
                Color color = _director.State.Weather switch
                {
                    WeatherKind.Rain => new Color(0.18f, 0.48f, 0.95f, 0.2f),
                    WeatherKind.Storm => new Color(0.18f, 0.18f, 0.35f, 0.32f),
                    WeatherKind.Fog => new Color(0.85f, 0.88f, 0.9f, 0.25f),
                    WeatherKind.Drought => new Color(0.95f, 0.62f, 0.12f, 0.22f),
                    _ => Colors.Transparent,
                };
                DrawRect(new Rect2(Vector2.Zero, Size), color);
                break;
        }
    }

    private static Color TerrainColor(TerrainType terrain) => terrain switch
    {
        TerrainType.DeepOcean => new Color(0.04f, 0.16f, 0.34f),
        TerrainType.ShallowWater => new Color(0.08f, 0.34f, 0.58f),
        TerrainType.Beach => new Color(0.78f, 0.7f, 0.46f),
        TerrainType.Grassland => new Color(0.24f, 0.52f, 0.24f),
        TerrainType.Forest => new Color(0.09f, 0.32f, 0.16f),
        TerrainType.Mountain => new Color(0.38f, 0.4f, 0.43f),
        _ => Colors.Magenta,
    };

    private static Color KingdomColor(ulong id)
    {
        float hue = (float)((id * 0.1732050807) % 1.0);
        return Color.FromHsv(hue, 0.7f, 0.95f);
    }

    private static Color WithAlpha(Color color, float alpha) => new(color.R, color.G, color.B, alpha);
    private static Color HeatColor(float value) => value < 0.5f
        ? new Color(0.95f, 0.2f + value, 0.1f)
        : new Color(1f - (value - 0.5f) * 1.5f, 0.75f + (value - 0.5f) * 0.4f, 0.12f);

    private static int DistanceSquared(int x1, int y1, int x2, int y2)
    {
        int dx = x1 - x2;
        int dy = y1 - y2;
        return dx * dx + dy * dy;
    }
}
