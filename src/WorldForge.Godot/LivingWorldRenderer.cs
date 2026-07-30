using Godot;
using WorldForge.Core.Simulation;
using WorldForge.Core.World;

namespace WorldForge.Presentation;

/// <summary>
/// Camera-aware living-world renderer. It culls off-screen objects, switches to aggregate
/// population rendering when zoomed out, and layers daily-life, weather, trade, migration,
/// city-state and performance overlays without one Godot node per simulated object.
/// </summary>
public sealed partial class LivingWorldRenderer : Node2D
{
    private WorldMap? _world;
    private GrandSimulation? _simulation;
    private LivingWorldDirector? _director;
    private double _animationTime;

    public int TilePixelSize { get; set; } = 4;
    public Vector2 CameraPosition { get; set; }
    public Vector2 CameraZoom { get; set; } = Vector2.One;
    public Vector2 ViewportSize { get; set; } = new(1280, 720);
    public LivingOverlayMode OverlayMode { get; set; }
    public ulong? SelectedEntityId { get; private set; }
    public ulong? SelectedSettlementId { get; private set; }
    public ulong? SelectedKingdomId { get; private set; }
    public int DrawnEntities { get; private set; }
    public int DrawnCities { get; private set; }
    public int DrawnArmies { get; private set; }
    public int AggregatedCells { get; private set; }

    public void Bind(WorldMap world, GrandSimulation simulation, LivingWorldDirector director)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        _director = director ?? throw new ArgumentNullException(nameof(director));
        ClearSelection();
        QueueRedraw();
    }

    public void AdvanceAnimation(double delta)
    {
        _animationTime += delta;
    }

    public void Refresh() => QueueRedraw();

    public void SelectEntity(ulong? id)
    {
        SelectedEntityId = id;
        SelectedSettlementId = null;
        SelectedKingdomId = null;
        QueueRedraw();
    }

    public void SelectSettlement(ulong? id)
    {
        SelectedEntityId = null;
        SelectedSettlementId = id;
        SelectedKingdomId = null;
        QueueRedraw();
    }

    public void SelectKingdom(ulong? id)
    {
        SelectedEntityId = null;
        SelectedSettlementId = null;
        SelectedKingdomId = id;
        QueueRedraw();
    }

    public void ClearSelection()
    {
        SelectedEntityId = null;
        SelectedSettlementId = null;
        SelectedKingdomId = null;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_world is null || _simulation is null || _director is null)
            return;

        DrawnEntities = 0;
        DrawnCities = 0;
        DrawnArmies = 0;
        AggregatedCells = 0;
        Rect2 visible = VisibleWorldRect(80);
        float zoom = CameraZoom.X;

        DrawOverlay(_simulation.State, _director.State, visible);
        DrawTrafficAndTrade(_simulation.State, _director.State, visible);
        DrawDiplomacy(_simulation.State, visible);
        DrawTerritories(_simulation.State, visible);
        DrawArmies(_simulation.State, visible, zoom);
        DrawSettlements(_simulation.State, _director.State, visible, zoom);

        if (zoom >= 0.42f)
            DrawEntitiesDetailed(_simulation.State, _director.State, visible, zoom);
        else
            DrawEntitiesAggregated(_simulation.State, visible);

        DrawWeather(_director.State, visible, zoom);
        DrawDayNight(_simulation.State, _director.State, visible);
    }

    private Rect2 VisibleWorldRect(float marginPixels)
    {
        float zoom = MathF.Max(0.05f, CameraZoom.X);
        Vector2 half = ViewportSize / (2f * zoom);
        return new Rect2(CameraPosition - half - Vector2.One * marginPixels, half * 2f + Vector2.One * marginPixels * 2f);
    }

    private bool IsVisible(int x, int y, Rect2 rect)
    {
        Vector2 p = TileCenter(x, y);
        return rect.HasPoint(p);
    }

    private void DrawOverlay(GrandSimulationState simulation, LivingWorldState living, Rect2 visible)
    {
        if (OverlayMode == LivingOverlayMode.None)
            return;

        switch (OverlayMode)
        {
            case LivingOverlayMode.Population:
                foreach (IGrouping<(int X, int Y), SimEntity> group in simulation.Entities.Values
                             .Where(e => e.IsAlive && IsVisible(e.X, e.Y, visible))
                             .GroupBy(e => (e.X / 8, e.Y / 8)))
                {
                    int count = group.Count();
                    Vector2 center = TileCenter(group.Key.X * 8 + 4, group.Key.Y * 8 + 4);
                    float radius = MathF.Min(34, 5 + MathF.Sqrt(count) * 4);
                    DrawCircle(center, radius, new Color(0.15f, 0.9f, 0.55f, MathF.Min(0.5f, 0.08f + count * 0.02f)));
                }
                break;
            case LivingOverlayMode.Food:
            case LivingOverlayMode.Happiness:
            case LivingOverlayMode.Performance:
                foreach (SettlementState city in simulation.Settlements.Values.Where(c => IsVisible(c.X, c.Y, visible)))
                {
                    float value = OverlayMode switch
                    {
                        LivingOverlayMode.Food => Math.Clamp(city.Food / 180f, 0, 1),
                        LivingOverlayMode.Happiness => Math.Clamp(city.Happiness / 100f, 0, 1),
                        _ => Math.Clamp(simulation.Entities.Values.Count(e => e.IsAlive && DistanceSquared(e.X, e.Y, city.X, city.Y) <= 20 * 20) / 100f, 0, 1),
                    };
                    Color color = HeatColor(value);
                    DrawCircle(TileCenter(city.X, city.Y), 24 + value * 42, WithAlpha(color, 0.22f));
                }
                break;
            case LivingOverlayMode.Disease:
                HashSet<ulong> infected = simulation.Diseases.SelectMany(d => d.InfectedDays.Keys).ToHashSet();
                foreach (SimEntity entity in simulation.Entities.Values.Where(e => infected.Contains(e.Id) && IsVisible(e.X, e.Y, visible)))
                    DrawCircle(TileCenter(entity.X, entity.Y), 10, new Color(0.96f, 0.35f, 0.08f, 0.28f));
                break;
            case LivingOverlayMode.War:
                foreach (ArmyState army in simulation.Armies.Values.Where(a => a.IsActive && IsVisible(a.X, a.Y, visible)))
                    DrawCircle(TileCenter(army.X, army.Y), 22, new Color(0.95f, 0.12f, 0.1f, 0.3f));
                break;
            case LivingOverlayMode.Kingdom:
                foreach (SettlementState city in simulation.Settlements.Values.Where(c => c.KingdomId is not null && IsVisible(c.X, c.Y, visible)))
                    DrawCircle(TileCenter(city.X, city.Y), 45, WithAlpha(KingdomColor(city.KingdomId!.Value), 0.22f));
                break;
            case LivingOverlayMode.Migration:
                foreach (CitizenLifeProfile profile in living.Citizens.Values.Where(p => simulation.Entities.GetValueOrDefault(p.EntityId)?.Action == EntityAction.Migrate))
                    if (simulation.Entities.TryGetValue(profile.EntityId, out SimEntity? migrant) && IsVisible(migrant.X, migrant.Y, visible))
                        DrawCircle(TileCenter(migrant.X, migrant.Y), 7, new Color(0.65f, 0.4f, 1f, 0.5f));
                break;
            case LivingOverlayMode.Weather:
                Color weather = living.Weather switch
                {
                    WeatherKind.Rain => new Color(0.2f, 0.5f, 0.95f, 0.18f),
                    WeatherKind.Storm => new Color(0.24f, 0.25f, 0.42f, 0.3f),
                    WeatherKind.Fog => new Color(0.85f, 0.88f, 0.9f, 0.25f),
                    WeatherKind.Drought => new Color(0.95f, 0.65f, 0.16f, 0.2f),
                    WeatherKind.ColdSnap => new Color(0.65f, 0.88f, 1f, 0.2f),
                    _ => Colors.Transparent,
                };
                DrawRect(visible, weather);
                break;
        }
    }

    private void DrawTrafficAndTrade(GrandSimulationState simulation, LivingWorldState living, Rect2 visible)
    {
        if (OverlayMode is LivingOverlayMode.Trade or LivingOverlayMode.Migration || CameraZoom.X >= 0.85f)
        {
            foreach ((int index, int amount) in living.TrafficByTile.Where(p => p.Value > 2))
            {
                int x = index % (_world?.Width ?? 1);
                int y = index / (_world?.Width ?? 1);
                if (!IsVisible(x, y, visible))
                    continue;
                float alpha = Math.Clamp(amount / 80f, 0.05f, 0.45f);
                DrawRect(new Rect2(TileCenter(x, y) - new Vector2(2, 1), new Vector2(4, 2)), new Color(0.95f, 0.75f, 0.28f, alpha));
            }
        }

        if (OverlayMode != LivingOverlayMode.Trade)
            return;

        SettlementState[] cities = simulation.Settlements.Values.OrderBy(c => c.Id).ToArray();
        for (int i = 0; i < cities.Length; i++)
        {
            SettlementState first = cities[i];
            SettlementState? second = cities.Skip(i + 1)
                .Where(c => first.KingdomId == c.KingdomId || Friendly(simulation, first.KingdomId, c.KingdomId))
                .OrderBy(c => DistanceSquared(first.X, first.Y, c.X, c.Y))
                .FirstOrDefault();
            if (second is null)
                continue;
            DrawDashedLine(TileCenter(first.X, first.Y), TileCenter(second.X, second.Y), new Color(0.95f, 0.78f, 0.25f, 0.65f), 8);
        }
    }

    private void DrawDiplomacy(GrandSimulationState state, Rect2 visible)
    {
        if (CameraZoom.X < 0.3f && OverlayMode != LivingOverlayMode.War)
            return;
        KingdomState[] kingdoms = state.Kingdoms.Values.OrderBy(k => k.Id).ToArray();
        for (int i = 0; i < kingdoms.Length; i++)
        {
            if (!TryCapitalPosition(state, kingdoms[i], out Vector2 first))
                continue;
            for (int j = i + 1; j < kingdoms.Length; j++)
            {
                if (!TryCapitalPosition(state, kingdoms[j], out Vector2 second))
                    continue;
                if (!visible.Intersects(new Rect2(
                    new Vector2(MathF.Min(first.X, second.X), MathF.Min(first.Y, second.Y)),
                    new Vector2(MathF.Abs(second.X - first.X), MathF.Abs(second.Y - first.Y)))))
                    continue;
                int relation = kingdoms[i].Relations.GetValueOrDefault(kingdoms[j].Id);
                Color color = relation switch
                {
                    <= -70 => new Color(0.96f, 0.13f, 0.12f, 0.55f),
                    <= -25 => new Color(0.95f, 0.45f, 0.14f, 0.35f),
                    >= 70 => new Color(0.16f, 0.9f, 0.95f, 0.55f),
                    >= 25 => new Color(0.3f, 0.88f, 0.42f, 0.35f),
                    _ => new Color(0.7f, 0.72f, 0.78f, 0.12f),
                };
                DrawLine(first, second, color, relation is <= -70 or >= 70 ? 2.2f : 1f);
            }
        }
    }

    private void DrawTerritories(GrandSimulationState state, Rect2 visible)
    {
        foreach (SettlementState city in state.Settlements.Values.Where(c => c.KingdomId is not null && IsVisible(c.X, c.Y, visible)))
        {
            Color color = KingdomColor(city.KingdomId!.Value);
            float radius = city.Stage switch
            {
                SettlementStage.Capital => 62,
                SettlementStage.City => 52,
                SettlementStage.Town => 42,
                SettlementStage.Village => 34,
                _ => 27,
            };
            DrawCircle(TileCenter(city.X, city.Y), radius, WithAlpha(color, SelectedKingdomId == city.KingdomId ? 0.18f : 0.07f));
            DrawCircle(TileCenter(city.X, city.Y), radius, WithAlpha(color, 0.32f), false, 1f);
        }
    }

    private void DrawArmies(GrandSimulationState state, Rect2 visible, float zoom)
    {
        foreach (ArmyState army in state.Armies.Values.Where(a => a.IsActive && IsVisible(a.X, a.Y, visible)).OrderBy(a => a.Id))
        {
            DrawnArmies++;
            Vector2 center = TileCenter(army.X, army.Y);
            Color color = KingdomColor(army.KingdomId);
            if (zoom >= 0.55f && army.Path.Count > army.PathIndex + 1)
            {
                Vector2 previous = center;
                int end = Math.Min(army.Path.Count, army.PathIndex + 50);
                for (int i = army.PathIndex; i < end; i++)
                {
                    Vector2 next = TileCenter(army.Path[i].X, army.Path[i].Y);
                    DrawLine(previous, next, WithAlpha(color, 0.35f), 1.2f);
                    previous = next;
                }
            }
            float bob = MathF.Sin((float)_animationTime * 5f + (float)(army.Id % 10000)) * 1.5f;
            DrawCircle(center + new Vector2(0, bob), 7, new Color(0.04f, 0.04f, 0.05f, 0.9f));
            DrawCircle(center + new Vector2(0, bob), 5.5f, color);
            DrawLine(center + new Vector2(0, bob - 3), center + new Vector2(0, bob - 14), Colors.White, 1.2f);
            DrawFlag(center + new Vector2(0, bob - 14), color, army.Id);
            if (army.Status == ArmyStatus.Besieging)
                DrawCircle(center, 12, new Color(1f, 0.25f, 0.08f, 0.65f), false, 2f);
        }
    }

    private void DrawSettlements(GrandSimulationState simulation, LivingWorldState living, Rect2 visible, float zoom)
    {
        HashSet<ulong> diseasedCities = simulation.Diseases
            .SelectMany(d => d.InfectedDays.Keys)
            .Select(id => simulation.Entities.GetValueOrDefault(id)?.SettlementId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToHashSet();
        HashSet<ulong> besiegedCities = simulation.Armies.Values
            .Where(a => a.IsActive && a.Status == ArmyStatus.Besieging && a.TargetSettlementId is not null)
            .Select(a => a.TargetSettlementId!.Value)
            .ToHashSet();

        foreach (SettlementState city in simulation.Settlements.Values.Where(c => IsVisible(c.X, c.Y, visible)).OrderBy(c => c.Y).ThenBy(c => c.Id))
        {
            DrawnCities++;
            CityManagementPolicy policy = living.Cities.GetValueOrDefault(city.Id) ?? new CityManagementPolicy { SettlementId = city.Id };
            bool detailed = zoom >= 0.48f;
            bool veryDetailed = zoom >= 0.9f;
            DrawLivingCity(city, policy, diseasedCities.Contains(city.Id), besiegedCities.Contains(city.Id), detailed, veryDetailed);
            if (SelectedSettlementId == city.Id)
                DrawCircle(TileCenter(city.X, city.Y), 34, Colors.White, false, 2.4f);
        }
    }

    private void DrawLivingCity(SettlementState city, CityManagementPolicy policy, bool diseased, bool besieged, bool detailed, bool veryDetailed)
    {
        Vector2 center = PixelSnap(TileCenter(city.X, city.Y));
        Color banner = city.KingdomId is ulong kid ? KingdomColor(kid) : new Color(0.95f, 0.78f, 0.3f);
        int size = city.Stage switch
        {
            SettlementStage.Capital => 28,
            SettlementStage.City => 24,
            SettlementStage.Town => 20,
            SettlementStage.Village => 16,
            _ => 12,
        };

        if (!detailed)
        {
            DrawRect(new Rect2(center - new Vector2(size / 2f, size / 2f), new Vector2(size, size)), new Color(0.06f, 0.06f, 0.08f, 0.9f));
            DrawRect(new Rect2(center - new Vector2(size / 2f - 2, size / 2f - 2), new Vector2(size - 4, size - 4)), banner);
            DrawFlag(center + new Vector2(0, -size / 2f), banner, city.Id);
            return;
        }

        int width = size * 2 + 12;
        int height = size + 22;
        Vector2 origin = PixelSnap(center - new Vector2(width / 2f, height / 2f));
        Color ground = city.Food < 25 ? new Color(0.34f, 0.25f, 0.15f) : new Color(0.25f, 0.29f, 0.18f);
        Color wall = city.Fortification > 4 ? new Color(0.48f, 0.5f, 0.53f) : new Color(0.38f, 0.3f, 0.2f);
        Color roof = Shade(banner, 0.66f);
        Color plaster = city.Happiness < 30 ? new Color(0.52f, 0.48f, 0.4f) : new Color(0.78f, 0.69f, 0.53f);
        Color window = IsNight() ? new Color(1f, 0.76f, 0.25f) : new Color(0.25f, 0.38f, 0.48f);

        DrawRect(new Rect2(origin + new Vector2(3, 4), new Vector2(width, height)), new Color(0.02f, 0.02f, 0.03f, 0.55f));
        DrawRect(new Rect2(origin, new Vector2(width, height)), ground);
        DrawRect(new Rect2(origin + new Vector2(width / 2f - 2, 0), new Vector2(4, height)), new Color(0.53f, 0.43f, 0.3f));
        DrawRect(new Rect2(origin + new Vector2(0, height / 2f - 2), new Vector2(width, 4)), new Color(0.53f, 0.43f, 0.3f));

        int houses = city.Stage switch
        {
            SettlementStage.Capital => 12,
            SettlementStage.City => 10,
            SettlementStage.Town => 7,
            SettlementStage.Village => 5,
            _ => 3,
        };
        for (int i = 0; i < houses; i++)
        {
            int hash = StableHash(city.Id, i);
            int px = 4 + Math.Abs(hash % Math.Max(4, width - 12));
            int py = 4 + Math.Abs((hash / 11) % Math.Max(4, height - 12));
            if (Math.Abs(px - width / 2) < 5 || Math.Abs(py - height / 2) < 5)
                px = Math.Max(3, px - 7);
            DrawHouse(origin + new Vector2(px, py), plaster, i % 2 == 0 ? roof : Shade(roof, 1.18f), window, hash, veryDetailed);
        }

        DrawKeep(center + new Vector2(0, -4), wall, roof, window, city.Stage);
        if (city.Fortification > 3 || city.Stage >= SettlementStage.City)
            DrawWalls(origin, width, height, wall);
        DrawFarm(origin + new Vector2(-13, height - 18), city.Food >= 25, city.Id);
        DrawFarm(origin + new Vector2(width + 2, 4), city.Food >= 25, city.Id + 17);

        float smoke = MathF.Sin((float)_animationTime * 1.4f + (float)(city.Id % 10000)) * 2f;
        if (city.Food > 20 && !policy.Evacuate)
        {
            Vector2 chimney = center + new Vector2(-9, -12);
            for (int i = 0; i < 3; i++)
                DrawCircle(chimney + new Vector2(smoke + i * 1.5f, -i * 5 - (float)(_animationTime % 1) * 3), 2.2f + i, new Color(0.75f, 0.76f, 0.78f, 0.22f - i * 0.04f));
        }

        DrawFlag(center + new Vector2(0, -height / 2f - 5), banner, city.Id);
        if (policy.FestivalUntilDay >= (_simulation?.State.Day ?? 0))
        {
            for (int i = -2; i <= 2; i++)
                DrawCircle(center + new Vector2(i * 7, -height / 2f - 1 + MathF.Sin((float)_animationTime * 4 + i) * 2), 2, i % 2 == 0 ? Colors.Yellow : banner);
        }
        if (diseased)
        {
            DrawCircle(center, width * 0.45f, new Color(0.95f, 0.45f, 0.08f, 0.25f));
            DrawCircle(center, width * 0.45f, new Color(1f, 0.55f, 0.08f, 0.8f), false, 2f);
        }
        if (besieged)
        {
            for (int i = 0; i < 4; i++)
            {
                float angle = (float)_animationTime + i * Mathf.Tau / 4f;
                Vector2 fire = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (width * 0.45f);
                DrawCircle(fire, 3 + MathF.Sin((float)_animationTime * 8 + i), new Color(1f, 0.25f, 0.04f, 0.85f));
            }
        }
        if (policy.Quarantine)
            DrawRect(new Rect2(center + new Vector2(-5, -height / 2f - 13), new Vector2(10, 6)), new Color(0.9f, 0.75f, 0.12f));
    }

    private void DrawEntitiesDetailed(GrandSimulationState simulation, LivingWorldState living, Rect2 visible, float zoom)
    {
        HashSet<ulong> infected = simulation.Diseases.SelectMany(d => d.InfectedDays.Keys).ToHashSet();
        foreach (SimEntity entity in simulation.Entities.Values.Where(e => e.IsAlive && IsVisible(e.X, e.Y, visible)).OrderBy(e => e.Y).ThenBy(e => e.Id))
        {
            DrawnEntities++;
            Vector2 center = TileCenter(entity.X, entity.Y);
            float bob = MathF.Sin((float)_animationTime * (entity.Action == EntityAction.Travel ? 8 : 3) + (float)(entity.Id % 10000) * 0.7f) * (entity.Action == EntityAction.Idle ? 0.5f : 1.2f);
            center.Y += bob;
            Color color = EntityColor(entity);
            float radius = entity.Species switch
            {
                SpeciesKind.Monster => 4.5f,
                SpeciesKind.Settler => 3.2f,
                SpeciesKind.Predator => 3f,
                _ => 2.6f,
            };
            DrawCircle(center + new Vector2(1, 2), radius + 1, new Color(0.02f, 0.02f, 0.03f, 0.55f));
            DrawCircle(center, radius, color);

            if (entity.Species == SpeciesKind.Settler && living.Citizens.TryGetValue(entity.Id, out CitizenLifeProfile? life) && zoom >= 0.82f)
            {
                DrawRect(new Rect2(center + new Vector2(-2, -radius - 4), new Vector2(4, 2)), JobColor(life.Job));
                DrawActivityBubble(center, life.Activity, zoom);
            }
            if (entity.AgeDays < 3 * 360)
                DrawCircle(center, radius + 2, new Color(0.5f, 0.9f, 1f, 0.65f), false, 1f);
            if (entity.PregnancyDaysRemaining > 0)
                DrawCircle(center, radius + 3.5f, new Color(1f, 0.4f, 0.72f, 0.75f), false, 1.2f);
            if (infected.Contains(entity.Id))
                DrawCircle(center, radius + 5, new Color(1f, 0.55f, 0.08f, 0.85f), false, 1.4f);
            if (SelectedEntityId == entity.Id)
            {
                DrawCircle(center, radius + 7, Colors.White, false, 2f);
                DrawSelectedPath(entity);
            }
        }
    }

    private void DrawEntitiesAggregated(GrandSimulationState state, Rect2 visible)
    {
        const int cellSize = 12;
        foreach (IGrouping<(int X, int Y, SpeciesKind Species), SimEntity> group in state.Entities.Values
                     .Where(e => e.IsAlive && IsVisible(e.X, e.Y, visible))
                     .GroupBy(e => (e.X / cellSize, e.Y / cellSize, e.Species)))
        {
            AggregatedCells++;
            Vector2 center = TileCenter(group.Key.X * cellSize + cellSize / 2, group.Key.Y * cellSize + cellSize / 2);
            int count = group.Count();
            Color color = EntityColor(group.First());
            float radius = MathF.Min(8, 2.5f + MathF.Sqrt(count));
            DrawCircle(center, radius + 1, new Color(0.02f, 0.02f, 0.03f, 0.75f));
            DrawCircle(center, radius, WithAlpha(color, 0.8f));
        }
    }

    private void DrawSelectedPath(SimEntity entity)
    {
        if (entity.Path.Count <= entity.PathIndex)
            return;
        Vector2 previous = TileCenter(entity.X, entity.Y);
        int end = Math.Min(entity.Path.Count, entity.PathIndex + 60);
        for (int i = entity.PathIndex; i < end; i++)
        {
            Vector2 next = TileCenter(entity.Path[i].X, entity.Path[i].Y);
            DrawLine(previous, next, new Color(1f, 1f, 1f, 0.55f), 1f);
            previous = next;
        }
    }

    private void DrawWeather(LivingWorldState living, Rect2 visible, float zoom)
    {
        if (!living.Settings.EnableAmbientAnimation)
            return;
        int particles = zoom >= 0.7f ? 90 : 45;
        if (living.Weather is WeatherKind.Rain or WeatherKind.Storm)
        {
            float speed = living.Weather == WeatherKind.Storm ? 240 : 150;
            for (int i = 0; i < particles; i++)
            {
                float x = visible.Position.X + PositiveMod(i * 97.3f + (float)_animationTime * 23f, visible.Size.X);
                float y = visible.Position.Y + PositiveMod(i * 53.7f + (float)_animationTime * speed, visible.Size.Y);
                float length = living.Weather == WeatherKind.Storm ? 13 : 8;
                DrawLine(new Vector2(x, y), new Vector2(x - 3, y + length), new Color(0.55f, 0.78f, 1f, living.Weather == WeatherKind.Storm ? 0.62f : 0.42f), 1f);
            }
        }
        else if (living.Weather == WeatherKind.Fog)
        {
            for (int i = 0; i < 12; i++)
            {
                float x = visible.Position.X + PositiveMod(i * 131f + (float)_animationTime * 9f, visible.Size.X);
                float y = visible.Position.Y + PositiveMod(i * 71f, visible.Size.Y);
                DrawCircle(new Vector2(x, y), 55 + i % 3 * 18, new Color(0.82f, 0.85f, 0.88f, 0.045f));
            }
        }
        else if (living.Weather == WeatherKind.ColdSnap)
        {
            for (int i = 0; i < particles / 2; i++)
            {
                float x = visible.Position.X + PositiveMod(i * 83f + MathF.Sin((float)_animationTime + i) * 20, visible.Size.X);
                float y = visible.Position.Y + PositiveMod(i * 47f + (float)_animationTime * 35f, visible.Size.Y);
                DrawCircle(new Vector2(x, y), 1.4f, new Color(0.9f, 0.96f, 1f, 0.7f));
            }
        }
    }

    private void DrawDayNight(GrandSimulationState simulation, LivingWorldState living, Rect2 visible)
    {
        float hour = living.WorldHour;
        float darkness = hour switch
        {
            < 5 => 0.48f,
            < 7 => 0.48f - (hour - 5) * 0.18f,
            < 18 => 0.08f,
            < 21 => 0.08f + (hour - 18) * 0.13f,
            _ => 0.47f,
        };
        if (living.Weather == WeatherKind.Storm)
            darkness += 0.12f;
        if (darkness <= 0.09f)
            return;
        DrawRect(visible, new Color(0.03f, 0.06f, 0.16f, Math.Clamp(darkness, 0, 0.62f)));
        foreach (SettlementState city in simulation.Settlements.Values.Where(c => IsVisible(c.X, c.Y, visible)))
        {
            Vector2 center = TileCenter(city.X, city.Y);
            float radius = city.Stage switch
            {
                SettlementStage.Capital => 34,
                SettlementStage.City => 29,
                SettlementStage.Town => 24,
                _ => 18,
            };
            DrawCircle(center, radius, new Color(1f, 0.65f, 0.16f, 0.09f));
            DrawCircle(center, radius * 0.5f, new Color(1f, 0.78f, 0.28f, 0.12f));
        }
    }

    private void DrawActivityBubble(Vector2 center, DailyActivity activity, float zoom)
    {
        if (zoom < 1f)
            return;
        string symbol = activity switch
        {
            DailyActivity.Sleeping => "Z",
            DailyActivity.Working => "!",
            DailyActivity.Eating => "•",
            DailyActivity.Socializing => "♥",
            DailyActivity.Fleeing => "!",
            DailyActivity.Sheltering => "⌂",
            _ => string.Empty,
        };
        if (string.IsNullOrEmpty(symbol))
            return;
        Vector2 bubble = center + new Vector2(5, -8);
        DrawCircle(bubble, 4, new Color(1f, 1f, 1f, 0.82f));
        DrawString(ThemeDB.FallbackFont, bubble + new Vector2(-2.8f, 2.8f), symbol, HorizontalAlignment.Left, -1, 7, new Color(0.1f, 0.1f, 0.13f));
    }

    private void DrawHouse(Vector2 origin, Color body, Color roof, Color window, int hash, bool detailed)
    {
        int width = 7 + Math.Abs(hash % 4);
        int height = 6 + Math.Abs((hash / 7) % 3);
        origin = PixelSnap(origin);
        DrawRect(new Rect2(origin + new Vector2(1, 2), new Vector2(width, height)), new Color(0.02f, 0.02f, 0.03f, 0.45f));
        DrawRect(new Rect2(origin, new Vector2(width, height)), body);
        DrawRect(new Rect2(origin + new Vector2(-1, -3), new Vector2(width + 2, 3)), roof);
        DrawRect(new Rect2(origin + new Vector2(1, -5), new Vector2(width - 2, 2)), Shade(roof, 1.15f));
        DrawRect(new Rect2(origin + new Vector2(width / 2f - 1, height - 3), new Vector2(2, 3)), Shade(body, 0.55f));
        DrawRect(new Rect2(origin + new Vector2(1, 2), new Vector2(2, 2)), window);
        if (detailed && width > 8)
            DrawRect(new Rect2(origin + new Vector2(width - 3, 2), new Vector2(2, 2)), window);
    }

    private void DrawKeep(Vector2 center, Color stone, Color roof, Color window, SettlementStage stage)
    {
        int width = stage >= SettlementStage.City ? 15 : 11;
        int height = stage == SettlementStage.Capital ? 18 : stage >= SettlementStage.Town ? 14 : 10;
        Vector2 origin = PixelSnap(center - new Vector2(width / 2f, height / 2f));
        DrawRect(new Rect2(origin + new Vector2(2, 3), new Vector2(width, height)), new Color(0.02f, 0.02f, 0.03f, 0.5f));
        DrawRect(new Rect2(origin, new Vector2(width, height)), stone);
        DrawRect(new Rect2(origin + new Vector2(-1, -3), new Vector2(width + 2, 3)), roof);
        DrawRect(new Rect2(origin + new Vector2(width / 2f - 2, height - 5), new Vector2(4, 5)), Shade(stone, 0.5f));
        DrawRect(new Rect2(origin + new Vector2(2, 4), new Vector2(2, 2)), window);
        DrawRect(new Rect2(origin + new Vector2(width - 4, 4), new Vector2(2, 2)), window);
    }

    private void DrawWalls(Vector2 origin, int width, int height, Color stone)
    {
        DrawRect(new Rect2(origin, new Vector2(width, 2)), stone);
        DrawRect(new Rect2(origin + new Vector2(0, height - 2), new Vector2(width, 2)), stone);
        DrawRect(new Rect2(origin, new Vector2(2, height)), stone);
        DrawRect(new Rect2(origin + new Vector2(width - 2, 0), new Vector2(2, height)), stone);
        DrawTower(origin + new Vector2(-2, -2), stone);
        DrawTower(origin + new Vector2(width - 4, -2), stone);
        DrawTower(origin + new Vector2(-2, height - 4), stone);
        DrawTower(origin + new Vector2(width - 4, height - 4), stone);
    }

    private void DrawTower(Vector2 origin, Color stone)
    {
        DrawRect(new Rect2(origin, new Vector2(6, 6)), Shade(stone, 0.7f));
        DrawRect(new Rect2(origin + new Vector2(1, 1), new Vector2(4, 4)), stone);
    }

    private void DrawFarm(Vector2 origin, bool healthy, ulong seed)
    {
        Color soil = healthy ? new Color(0.42f, 0.31f, 0.16f) : new Color(0.32f, 0.27f, 0.2f);
        Color crop = healthy ? new Color(0.66f, 0.82f, 0.23f) : new Color(0.55f, 0.42f, 0.18f);
        DrawRect(new Rect2(origin, new Vector2(11, 15)), soil);
        for (int x = 2; x < 10; x += 3)
            DrawLine(origin + new Vector2(x, 1), origin + new Vector2(x, 14), crop, 1f);
        float sway = MathF.Sin((float)_animationTime * 2 + (float)(seed % 10000)) * 0.6f;
        DrawLine(origin + new Vector2(5, 2), origin + new Vector2(5 + sway, -2), crop, 1f);
    }

    private void DrawFlag(Vector2 top, Color color, ulong seed)
    {
        float wave = MathF.Sin((float)_animationTime * 5f + (float)(seed % 10000)) * 2f;
        DrawLine(top, top + new Vector2(0, 10), new Color(0.78f, 0.8f, 0.84f), 1.2f);
        Vector2[] points =
        {
            top,
            top + new Vector2(8 + wave, 2),
            top + new Vector2(1, 5),
        };
        DrawColoredPolygon(points, color);
    }

    private void DrawDashedLine(Vector2 from, Vector2 to, Color color, int dashes)
    {
        for (int i = 0; i < dashes; i += 2)
        {
            float a = i / (float)dashes;
            float b = Math.Min(1, (i + 1) / (float)dashes);
            DrawLine(from.Lerp(to, a), from.Lerp(to, b), color, 1.4f);
        }
    }

    private bool TryCapitalPosition(GrandSimulationState state, KingdomState kingdom, out Vector2 position)
    {
        if (state.Settlements.TryGetValue(kingdom.CapitalId, out SettlementState? capital))
        {
            position = TileCenter(capital.X, capital.Y);
            return true;
        }
        position = Vector2.Zero;
        return false;
    }

    private bool IsNight()
    {
        float hour = _director?.State.WorldHour ?? 12;
        return hour < 6 || hour >= 19;
    }

    private Vector2 TileCenter(int x, int y) => new((x + 0.5f) * TilePixelSize, (y + 0.5f) * TilePixelSize);
    private static Vector2 PixelSnap(Vector2 value) => new(MathF.Round(value.X), MathF.Round(value.Y));
    private static Color WithAlpha(Color color, float alpha) => new(color.R, color.G, color.B, alpha);
    private static Color Shade(Color color, float factor) => new(Math.Clamp(color.R * factor, 0, 1), Math.Clamp(color.G * factor, 0, 1), Math.Clamp(color.B * factor, 0, 1), color.A);

    private static Color KingdomColor(ulong id)
    {
        float hue = (float)((id * 0.1732050807) % 1.0);
        return Color.FromHsv(hue, 0.7f, 0.95f);
    }

    private static Color EntityColor(SimEntity entity) => entity.Species switch
    {
        SpeciesKind.Grazer => new Color(0.72f, 0.94f, 0.28f),
        SpeciesKind.Predator => new Color(0.96f, 0.22f, 0.16f),
        SpeciesKind.Settler => entity.KingdomId is ulong kingdomId ? KingdomColor(kingdomId) : new Color(1f, 0.82f, 0.23f),
        SpeciesKind.Monster => new Color(0.78f, 0.18f, 0.9f),
        SpeciesKind.Fish => new Color(0.18f, 0.82f, 1f),
        _ => Colors.White,
    };

    private static Color JobColor(CitizenJob job) => job switch
    {
        CitizenJob.Farmer => new Color(0.55f, 0.9f, 0.24f),
        CitizenJob.Woodcutter => new Color(0.35f, 0.65f, 0.22f),
        CitizenJob.Miner => new Color(0.65f, 0.68f, 0.72f),
        CitizenJob.Builder => new Color(0.94f, 0.58f, 0.16f),
        CitizenJob.Trader => new Color(0.98f, 0.82f, 0.22f),
        CitizenJob.Healer => new Color(0.25f, 0.95f, 0.75f),
        CitizenJob.Priest => new Color(0.76f, 0.55f, 1f),
        CitizenJob.Scholar => new Color(0.3f, 0.68f, 1f),
        CitizenJob.Guard or CitizenJob.Soldier => new Color(0.95f, 0.28f, 0.2f),
        CitizenJob.Ruler => new Color(1f, 0.85f, 0.35f),
        _ => Colors.White,
    };

    private static Color HeatColor(float value)
    {
        value = Math.Clamp(value, 0, 1);
        return value < 0.5f
            ? new Color(0.95f, 0.2f + value, 0.1f)
            : new Color(1f - (value - 0.5f) * 1.5f, 0.75f + (value - 0.5f) * 0.4f, 0.12f);
    }

    private static bool Friendly(GrandSimulationState state, ulong? firstId, ulong? secondId)
    {
        if (firstId is null || secondId is null || firstId == secondId)
            return true;
        return state.Kingdoms.GetValueOrDefault(firstId.Value)?.Relations.GetValueOrDefault(secondId.Value) >= 25;
    }

    private static int StableHash(ulong id, int salt)
    {
        unchecked
        {
            ulong value = id * 11400714819323198485UL + (ulong)(salt + 1) * 14029467366897019727UL;
            value ^= value >> 33;
            value *= 0xff51afd7ed558ccdUL;
            value ^= value >> 33;
            return (int)(value & 0x7fffffff);
        }
    }

    private static int DistanceSquared(int x1, int y1, int x2, int y2)
    {
        int dx = x1 - x2;
        int dy = y1 - y2;
        return dx * dx + dy * dy;
    }

    private static float PositiveMod(float value, float divisor)
    {
        if (divisor <= 0)
            return 0;
        float result = value % divisor;
        return result < 0 ? result + divisor : result;
    }
}
