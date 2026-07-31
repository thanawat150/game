using Godot;
using WorldForge.Core.Simulation;
using WorldForge.Core.World;

namespace WorldForge.Presentation;

/// <summary>
/// Draws the generated production art over the low-cost procedural simulation renderer.
/// It remains a single canvas node, uses camera culling, and caps detailed citizens by zoom.
/// </summary>
public sealed partial class ReleaseArtOverlay : Node2D
{
    private GeneratedGameArtAtlas? _art;
    private WorldMap? _world;
    private GrandSimulation? _simulation;
    private LivingWorldDirector? _living;
    private WorldExpansionDirector? _expansion;
    private double _animationTime;

    public int TilePixelSize { get; set; } = 4;
    public Vector2 CameraPosition { get; set; }
    public Vector2 CameraZoom { get; set; } = Vector2.One;
    public Vector2 ViewportSize { get; set; } = new(1280, 720);
    public int DrawnCitizens { get; private set; }
    public int DrawnBuildings { get; private set; }

    public void SetArt(GeneratedGameArtAtlas art)
    {
        _art = art;
        QueueRedraw();
    }

    public void Bind(WorldMap world, GrandSimulation simulation, LivingWorldDirector living, WorldExpansionDirector expansion)
    {
        _world = world;
        _simulation = simulation;
        _living = living;
        _expansion = expansion;
        QueueRedraw();
    }

    public void AdvanceAnimation(double delta) => _animationTime += delta;
    public void Refresh() => QueueRedraw();

    public override void _Draw()
    {
        if (_art is null || _world is null || _simulation is null || _living is null || _expansion is null)
            return;

        DrawnCitizens = 0;
        DrawnBuildings = 0;
        Rect2 visible = VisibleWorldRect(120);
        float zoom = Math.Max(0.05f, CameraZoom.X);

        DrawBuildings(_simulation.State, _expansion.State, visible, zoom);
        DrawWorldObjects(_simulation.State, _expansion.State, visible, zoom);
        DrawCitizens(_simulation.State, _living.State, _expansion.State, visible, zoom);
        DrawLegendMarkers(_simulation.State, _expansion.State, visible, zoom);
    }

    private void DrawBuildings(GrandSimulationState simulation, WorldExpansionState expansion, Rect2 visible, float zoom)
    {
        foreach (CityDistrictState district in expansion.CityDistricts.Values.OrderBy(d => d.SettlementId))
        {
            if (!simulation.Settlements.TryGetValue(district.SettlementId, out SettlementState? city))
                continue;
            Vector2 cityCenter = TileCenter(city.X, city.Y);
            if (!visible.Grow(200).HasPoint(cityCenter))
                continue;

            foreach (PlacedBuilding building in district.Buildings.OrderBy(b => b.Y).ThenBy(b => b.Id))
            {
                Vector2 center = TileCenter(building.X, building.Y);
                if (!visible.HasPoint(center))
                    continue;

                float size = building.Kind switch
                {
                    BuildingKind.Keep => 52,
                    BuildingKind.Harbor or BuildingKind.Shipyard => 48,
                    BuildingKind.Wall or BuildingKind.Gate or BuildingKind.Watchtower => 40,
                    BuildingKind.Monument or BuildingKind.MageTower => 46,
                    _ => 38,
                };
                size *= zoom >= 1.35f ? 1.15f : zoom < 0.48f ? 0.75f : 1f;
                Rect2 destination = new(
                    PixelSnap(center - new Vector2(size / 2f, size * 0.72f)),
                    new Vector2(size, size));

                DrawTextureRectRegion(_art!.BuildingsTexture, destination, _art.BuildingRegion(building.Kind));
                DrawnBuildings++;

                if (building.Status is BuildingStatus.Planned or BuildingStatus.Building)
                {
                    DrawRect(destination, new Color(0.05f, 0.07f, 0.1f, 0.28f));
                    float progress = Math.Clamp(building.Progress / 100f, 0, 1);
                    Rect2 bar = new(destination.Position + new Vector2(2, destination.Size.Y - 4), new Vector2(destination.Size.X - 4, 3));
                    DrawRect(bar, new Color(0.04f, 0.04f, 0.05f, 0.9f));
                    DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * progress, bar.Size.Y)), new Color(0.25f, 0.92f, 0.48f));
                }
                else if (building.Status is BuildingStatus.Damaged or BuildingStatus.Ruined)
                {
                    DrawRect(destination, new Color(0.12f, 0.02f, 0.02f, building.Status == BuildingStatus.Ruined ? 0.48f : 0.25f));
                    int motes = building.Status == BuildingStatus.Ruined ? 4 : 2;
                    for (int i = 0; i < motes; i++)
                    {
                        float drift = (float)((_animationTime * 8 + building.Id + i * 5) % 12);
                        DrawCircle(center + new Vector2(i * 3 - 4, -16 - drift), 2 + i * 0.4f, new Color(0.25f, 0.24f, 0.27f, 0.42f));
                    }
                }
            }
        }
    }

    private void DrawCitizens(
        GrandSimulationState simulation,
        LivingWorldState living,
        WorldExpansionState expansion,
        Rect2 visible,
        float zoom)
    {
        if (zoom < 0.78f)
            return;

        int limit = zoom >= 1.55f ? 900 : zoom >= 1.1f ? 600 : 320;
        foreach (SimEntity entity in simulation.Entities.Values
                     .Where(e => e.IsAlive && e.Species == SpeciesKind.Settler)
                     .OrderBy(e => e.Id))
        {
            Vector2 center = TileCenter(entity.X, entity.Y);
            if (!visible.HasPoint(center))
                continue;
            if (DrawnCitizens >= limit)
                break;

            RaceKind race = expansion.CitizenRaces.GetValueOrDefault(entity.Id, RaceKind.Human);
            CitizenLifeProfile? profile = living.Citizens.GetValueOrDefault(entity.Id);
            CitizenJob job = profile?.Job ?? CitizenJob.Farmer;
            bool moving = profile?.Activity is DailyActivity.GoingToWork
                or DailyActivity.ReturningHome
                or DailyActivity.Trading
                or DailyActivity.Fleeing
                or DailyActivity.Patrolling;

            float bob = moving ? MathF.Sin((float)_animationTime * 8 + entity.Id) * 1.6f : MathF.Sin((float)_animationTime * 2 + entity.Id) * 0.45f;
            float size = zoom >= 1.6f ? 38 : 31;
            Rect2 destination = new(
                PixelSnap(center + new Vector2(-size / 2f, -size * 0.72f + bob)),
                new Vector2(size, size));
            DrawTextureRectRegion(_art!.CharactersTexture, destination, _art.CharacterRegion(race, job));
            DrawnCitizens++;
        }
    }

    private void DrawWorldObjects(GrandSimulationState simulation, WorldExpansionState expansion, Rect2 visible, float zoom)
    {
        foreach (FleetState fleet in expansion.Fleets.Values.Where(f => f.IsActive).OrderBy(f => f.Id))
        {
            Vector2 center = TileCenter(fleet.X, fleet.Y);
            if (!visible.HasPoint(center))
                continue;
            float bob = MathF.Sin((float)_animationTime * 3 + fleet.Id) * 1.4f;
            DrawIcon(GameIcon.Fleet, center + new Vector2(0, bob), zoom >= 1 ? 36 : 28);
        }

        foreach (RuinState ruin in expansion.Ruins.Values)
        {
            Vector2 center = TileCenter(ruin.X, ruin.Y);
            if (!visible.HasPoint(center))
                continue;
            DrawEffect(GameArtEffect.AncientRuin, center, ruin.Explored ? 30 : 35);
            if (!ruin.Explored && ruin.DiscoveredDay >= 0)
                DrawCircle(center, 18 + MathF.Sin((float)_animationTime * 2 + ruin.Id) * 2, new Color(0.95f, 0.66f, 0.22f, 0.3f), false, 1.5f, false);
        }

        if (zoom >= 0.65f)
        {
            foreach (MageProfile mage in expansion.Mages.Values)
            {
                SimEntity? entity = simulation.Entities.GetValueOrDefault(mage.EntityId);
                if (entity is null || !entity.IsAlive)
                    continue;
                Vector2 center = TileCenter(entity.X, entity.Y);
                if (!visible.HasPoint(center))
                    continue;
                DrawEffect(GameArtEffect.MagicCircle, center + new Vector2(0, 3), 28 + Math.Min(10, mage.Level));
            }
        }

        foreach ((ulong cityId, float faith) in expansion.Faith.CityFaith)
        {
            if (faith < 35 || !simulation.Settlements.TryGetValue(cityId, out SettlementState? city))
                continue;
            Vector2 center = TileCenter(city.X, city.Y);
            if (!visible.HasPoint(center))
                continue;
            float pulse = 34 + MathF.Sin((float)_animationTime * 1.5f + cityId) * 3;
            GameArtEffect effect = expansion.Faith.Path switch
            {
                DeityPath.Mercy => GameArtEffect.HealingAura,
                DeityPath.Nature => GameArtEffect.ForestGrowth,
                DeityPath.War => GameArtEffect.FireBurst,
                DeityPath.Knowledge => GameArtEffect.MagicCircle,
                _ => GameArtEffect.CurseCloud,
            };
            DrawEffect(effect, center, pulse);
        }
    }

    private void DrawLegendMarkers(GrandSimulationState simulation, WorldExpansionState expansion, Rect2 visible, float zoom)
    {
        if (zoom < 0.7f)
            return;
        foreach (LegendProfile legend in expansion.Legends.Values.Where(l => !l.IsDead).OrderByDescending(l => l.Fame).Take(60))
        {
            SimEntity? entity = simulation.Entities.GetValueOrDefault(legend.EntityId);
            if (entity is null || !entity.IsAlive)
                continue;
            Vector2 center = TileCenter(entity.X, entity.Y);
            if (!visible.HasPoint(center))
                continue;
            float size = 15 + MathF.Sin((float)_animationTime * 2 + legend.EntityId) * 1.5f;
            DrawEffect(GameArtEffect.Crown, center + new Vector2(0, -18), size);
        }
    }

    private void DrawIcon(GameIcon icon, Vector2 center, float size)
    {
        Rect2 destination = new(PixelSnap(center - Vector2.One * size / 2f), Vector2.One * size);
        DrawTextureRectRegion(_art!.IconsTexture, destination, _art.IconRegion(icon));
    }

    private void DrawEffect(GameArtEffect effect, Vector2 center, float size)
    {
        Rect2 destination = new(PixelSnap(center - Vector2.One * size / 2f), Vector2.One * size);
        DrawTextureRectRegion(_art!.EffectsTexture, destination, _art.EffectRegion(effect));
    }

    private Rect2 VisibleWorldRect(float margin)
    {
        float zoom = Math.Max(0.05f, CameraZoom.X);
        Vector2 half = ViewportSize / (2f * zoom);
        return new Rect2(CameraPosition - half - Vector2.One * margin, half * 2 + Vector2.One * margin * 2);
    }

    private Vector2 TileCenter(int x, int y) => new((x + 0.5f) * TilePixelSize, (y + 0.5f) * TilePixelSize);
    private static Vector2 PixelSnap(Vector2 value) => new(MathF.Round(value.X), MathF.Round(value.Y));
}
