using Godot;
using WorldForge.Core.Simulation;
using WorldForge.Core.World;

namespace WorldForge.Presentation;

/// <summary>
/// Draws physical districts, animated atlas citizens, legends, fleets, nomads, ruins,
/// magic and historical replay without allocating one node per simulation object.
/// </summary>
public sealed partial class WorldExpansionRenderer : Node2D
{
    private WorldMap? _world;
    private GrandSimulation? _simulation;
    private LivingWorldDirector? _living;
    private WorldExpansionDirector? _expansion;
    private ProceduralPixelAtlas? _atlas;
    private double _animationTime;

    public int TilePixelSize { get; set; } = 4;
    public Vector2 CameraPosition { get; set; }
    public Vector2 CameraZoom { get; set; } = Vector2.One;
    public Vector2 ViewportSize { get; set; } = new(1280, 720);
    public int ReplaySnapshotIndex { get; set; } = -1;
    public bool ShowRoads { get; set; } = true;
    public bool ShowFaith { get; set; } = true;
    public bool ShowMagic { get; set; } = true;
    public int DrawnAnimatedCitizens { get; private set; }
    public int DrawnBuildings { get; private set; }

    public void Bind(WorldMap world, GrandSimulation simulation, LivingWorldDirector living, WorldExpansionDirector expansion)
    {
        _world = world;
        _simulation = simulation;
        _living = living;
        _expansion = expansion;
        _atlas ??= new ProceduralPixelAtlas();
        QueueRedraw();
    }

    public void AdvanceAnimation(double delta) => _animationTime += delta;
    public void Refresh() => QueueRedraw();

    public override void _Draw()
    {
        if (_world is null || _simulation is null || _living is null || _expansion is null || _atlas is null)
            return;
        DrawnAnimatedCitizens = 0;
        DrawnBuildings = 0;
        Rect2 visible = VisibleWorldRect(100);
        float zoom = Math.Max(0.05f, CameraZoom.X);

        DrawHistoryReplay(_expansion.State, visible);
        DrawDistricts(_simulation.State, _expansion.State, visible, zoom);
        DrawRuins(_expansion.State, visible, zoom);
        DrawFleets(_expansion.State, visible, zoom);
        DrawNomads(_expansion.State, visible, zoom);
        DrawAnimatedCitizens(_simulation.State, _living.State, _expansion.State, visible, zoom);
        DrawLegends(_simulation.State, _expansion.State, visible, zoom);
        DrawMages(_simulation.State, _expansion.State, visible, zoom);
        DrawFaith(_simulation.State, _expansion.State, visible, zoom);
    }

    private Rect2 VisibleWorldRect(float margin)
    {
        float zoom = Math.Max(0.05f, CameraZoom.X);
        Vector2 half = ViewportSize / (2f * zoom);
        return new Rect2(CameraPosition - half - Vector2.One * margin, half * 2 + Vector2.One * margin * 2);
    }

    private void DrawHistoryReplay(WorldExpansionState state, Rect2 visible)
    {
        if (ReplaySnapshotIndex < 0 || state.History.Count == 0) return;
        WorldHistorySnapshot snapshot = state.History[Math.Clamp(ReplaySnapshotIndex, 0, state.History.Count - 1)];
        foreach (HistoryCitySnapshot city in snapshot.Cities)
        {
            Vector2 center = TileCenter(city.X, city.Y);
            if (!visible.HasPoint(center)) continue;
            Color color = city.KingdomId is ulong id ? RaceColor(snapshot.KingdomStates.FirstOrDefault(k => k.Id == id)?.Race ?? RaceKind.Human) : Colors.White;
            float radius = city.Stage switch
            {
                SettlementStage.Capital => 28,
                SettlementStage.City => 23,
                SettlementStage.Town => 18,
                SettlementStage.Village => 14,
                _ => 10,
            };
            DrawCircle(center, radius, WithAlpha(color, 0.14f));
            DrawCircle(center, radius, WithAlpha(color, 0.7f), false, 2, false);
            DrawLine(center - new Vector2(radius, 0), center + new Vector2(radius, 0), WithAlpha(Colors.White, 0.35f), 1, false);
        }
    }

    private void DrawDistricts(GrandSimulationState simulation, WorldExpansionState state, Rect2 visible, float zoom)
    {
        foreach (CityDistrictState district in state.CityDistricts.Values.OrderBy(d => d.SettlementId))
        {
            if (!simulation.Settlements.TryGetValue(district.SettlementId, out SettlementState? city)) continue;
            Vector2 cityCenter = TileCenter(city.X, city.Y);
            if (!visible.Grow(180).HasPoint(cityCenter)) continue;
            RaceKind race = city.KingdomId is ulong kingdomId ? state.KingdomRaces.GetValueOrDefault(kingdomId) : RaceKind.Human;
            Color culture = RaceColor(race);
            if (ShowRoads && zoom >= 0.28f)
            {
                foreach (int tileIndex in district.RoadTiles)
                {
                    int x = tileIndex % _world!.Width;
                    int y = tileIndex / _world.Width;
                    Vector2 p = TileCenter(x, y);
                    if (!visible.HasPoint(p)) continue;
                    DrawRect(new Rect2(p - new Vector2(2.2f, 2.2f), new Vector2(4.4f, 4.4f)), new Color(0.42f, 0.34f, 0.24f, 0.72f));
                }
            }
            foreach (PlacedBuilding building in district.Buildings.OrderBy(b => b.Y).ThenBy(b => b.Id))
            {
                Vector2 p = TileCenter(building.X, building.Y);
                if (!visible.HasPoint(p)) continue;
                DrawBuilding(building, culture, zoom);
                DrawnBuildings++;
            }
        }
    }

    private void DrawBuilding(PlacedBuilding building, Color culture, float zoom)
    {
        Vector2 p = PixelSnap(TileCenter(building.X, building.Y));
        float scale = zoom >= 1.25f ? 1.35f : zoom >= 0.55f ? 1f : 0.7f;
        Color wall = building.Status switch
        {
            BuildingStatus.Ruined => new Color(0.25f, 0.24f, 0.23f),
            BuildingStatus.Damaged => new Color(0.46f, 0.39f, 0.34f),
            BuildingStatus.Building or BuildingStatus.Planned => new Color(0.58f, 0.48f, 0.32f),
            _ => new Color(0.76f, 0.68f, 0.53f),
        };
        Color roof = culture.Darkened(0.25f);
        Vector2 size = building.Kind switch
        {
            BuildingKind.Keep => new Vector2(18, 18),
            BuildingKind.Wall => new Vector2(18, 5),
            BuildingKind.Gate => new Vector2(14, 10),
            BuildingKind.Harbor or BuildingKind.Shipyard => new Vector2(18, 11),
            BuildingKind.Monument => new Vector2(8, 17),
            BuildingKind.Farm => new Vector2(16, 10),
            _ => new Vector2(12, 11),
        } * scale;
        Rect2 baseRect = new(p - size / 2, size);
        DrawRect(new Rect2(baseRect.Position + new Vector2(2, 3), baseRect.Size), new Color(0.02f, 0.02f, 0.03f, 0.35f));

        if (building.Kind == BuildingKind.Farm)
        {
            DrawRect(baseRect, new Color(0.45f, 0.38f, 0.14f));
            for (int i = 2; i < size.X; i += 4)
                DrawLine(baseRect.Position + new Vector2(i, 1), baseRect.Position + new Vector2(i, size.Y - 1), new Color(0.65f, 0.72f, 0.22f), 1, false);
        }
        else if (building.Kind == BuildingKind.Monument)
        {
            DrawRect(new Rect2(p + new Vector2(-4, 4), new Vector2(8, 5)), new Color(0.46f, 0.48f, 0.53f));
            DrawRect(new Rect2(p + new Vector2(-2, -8), new Vector2(4, 13)), new Color(0.65f, 0.68f, 0.74f));
            DrawCircle(p + new Vector2(0, -9), 3, culture);
        }
        else if (building.Kind is BuildingKind.Harbor or BuildingKind.Shipyard)
        {
            DrawRect(baseRect, new Color(0.36f, 0.25f, 0.15f));
            DrawLine(p + new Vector2(-size.X / 2, 0), p + new Vector2(size.X / 2, 0), new Color(0.75f, 0.58f, 0.3f), 2, false);
            DrawRect(new Rect2(p + new Vector2(-4, -6), new Vector2(8, 7)), wall);
            DrawRect(new Rect2(p + new Vector2(-5, -8), new Vector2(10, 3)), roof);
        }
        else if (building.Kind == BuildingKind.Wall)
        {
            DrawRect(baseRect, new Color(0.48f, 0.5f, 0.54f));
            for (int i = 0; i < size.X; i += 5)
                DrawRect(new Rect2(baseRect.Position + new Vector2(i, -2), new Vector2(3, 3)), new Color(0.62f, 0.64f, 0.67f));
        }
        else
        {
            DrawRect(baseRect, wall);
            DrawRect(new Rect2(baseRect.Position + new Vector2(-1, -4), new Vector2(size.X + 2, 5)), roof);
            DrawRect(new Rect2(p + new Vector2(-1.5f, size.Y / 2 - 5), new Vector2(3, 5)), new Color(0.22f, 0.14f, 0.09f));
            if (building.Status == BuildingStatus.Active)
                DrawRect(new Rect2(p + new Vector2(2, -1), new Vector2(2, 2)), new Color(1f, 0.78f, 0.24f));
        }

        if (building.Status is BuildingStatus.Planned or BuildingStatus.Building)
        {
            float width = size.X * Math.Clamp(building.Progress / 100f, 0, 1);
            DrawRect(new Rect2(baseRect.Position + new Vector2(0, size.Y + 2), new Vector2(size.X, 2)), new Color(0.1f, 0.1f, 0.12f, 0.8f));
            DrawRect(new Rect2(baseRect.Position + new Vector2(0, size.Y + 2), new Vector2(width, 2)), new Color(0.25f, 0.9f, 0.45f));
            DrawLine(baseRect.Position - new Vector2(2, 2), baseRect.End + new Vector2(2, 2), new Color(0.78f, 0.56f, 0.25f), 1, false);
        }
        if (building.Health < 60)
        {
            int smoke = 2 + (int)((_animationTime * 3 + building.Id) % 3);
            for (int i = 0; i < smoke; i++)
                DrawCircle(p + new Vector2(i * 2 - 2, -size.Y / 2 - 5 - i * 3), 2 + i, new Color(0.24f, 0.23f, 0.25f, 0.4f));
        }
    }

    private void DrawAnimatedCitizens(GrandSimulationState simulation, LivingWorldState living, WorldExpansionState expansion, Rect2 visible, float zoom)
    {
        if (zoom < 0.62f || ReplaySnapshotIndex >= 0) return;
        int frameBase = (int)(_animationTime * 7) % ProceduralPixelAtlas.FrameCount;
        int limit = zoom >= 1.2f ? 900 : 450;
        foreach (SimEntity entity in simulation.Entities.Values.Where(e => e.IsAlive && e.Species == SpeciesKind.Settler).OrderBy(e => e.Id))
        {
            Vector2 p = TileCenter(entity.X, entity.Y);
            if (!visible.HasPoint(p)) continue;
            if (DrawnAnimatedCitizens >= limit) break;
            RaceKind race = expansion.CitizenRaces.GetValueOrDefault(entity.Id, RaceKind.Human);
            CitizenLifeProfile? life = living.Citizens.GetValueOrDefault(entity.Id);
            bool moving = life?.Activity is DailyActivity.GoingToWork or DailyActivity.ReturningHome or DailyActivity.Trading or DailyActivity.Fleeing or DailyActivity.Patrolling;
            int frame = moving ? (frameBase + (int)(entity.Id % 4)) % 4 : ((int)(_animationTime * 2 + entity.Id) % 2) * 2;
            float scale = zoom >= 1.5f ? 1.5f : 1.15f;
            Rect2 destination = new(PixelSnap(p - new Vector2(ProceduralPixelAtlas.FrameWidth, ProceduralPixelAtlas.FrameHeight) * scale / 2), new Vector2(ProceduralPixelAtlas.FrameWidth, ProceduralPixelAtlas.FrameHeight) * scale);
            DrawTextureRectRegion(destination, _atlas!.Texture, _atlas.SourceRect(race, frame));
            DrawJobTool(p, life?.Job ?? CitizenJob.Farmer, scale, entity.Id);
            DrawnAnimatedCitizens++;
        }
    }

    private void DrawJobTool(Vector2 p, CitizenJob job, float scale, ulong id)
    {
        Color tool = new Color(0.72f, 0.72f, 0.76f);
        float sway = MathF.Sin((float)_animationTime * 5 + id) * 1.2f;
        switch (job)
        {
            case CitizenJob.Farmer:
                DrawLine(p + new Vector2(5, -1), p + new Vector2(8 + sway, 6), new Color(0.48f, 0.3f, 0.12f), 1.2f, false);
                break;
            case CitizenJob.Woodcutter:
                DrawLine(p + new Vector2(5, -1), p + new Vector2(8 + sway, 5), tool, 1.5f, false);
                DrawRect(new Rect2(p + new Vector2(7 + sway, 4), new Vector2(4, 2)), tool);
                break;
            case CitizenJob.Miner:
                DrawLine(p + new Vector2(4, -4), p + new Vector2(9 + sway, 0), tool, 1.3f, false);
                break;
            case CitizenJob.Trader:
                DrawRect(new Rect2(p + new Vector2(5, 2), new Vector2(5, 4)), new Color(0.7f, 0.42f, 0.15f));
                break;
            case CitizenJob.Guard or CitizenJob.Soldier:
                DrawLine(p + new Vector2(5, -5), p + new Vector2(5, 7), tool, 1.5f, false);
                break;
            case CitizenJob.Scholar or CitizenJob.Priest:
                DrawCircle(p + new Vector2(0, -10), 2 + MathF.Sin((float)_animationTime * 3 + id) * 0.4f, new Color(0.72f, 0.45f, 1f, 0.75f));
                break;
        }
    }

    private void DrawLegends(GrandSimulationState simulation, WorldExpansionState state, Rect2 visible, float zoom)
    {
        foreach (LegendProfile legend in state.Legends.Values.Where(l => !l.IsDead).OrderByDescending(l => l.Fame).Take(80))
        {
            SimEntity? entity = simulation.Entities.GetValueOrDefault(legend.EntityId);
            if (entity is null || !entity.IsAlive) continue;
            Vector2 p = TileCenter(entity.X, entity.Y);
            if (!visible.HasPoint(p)) continue;
            float pulse = 8 + MathF.Sin((float)_animationTime * 2 + legend.EntityId) * 2 + MathF.Min(8, legend.Fame / 50f);
            Color color = legend.Role switch
            {
                LegendRole.Ruler => new Color(1f, 0.82f, 0.2f),
                LegendRole.General => new Color(0.95f, 0.25f, 0.18f),
                LegendRole.Scholar => new Color(0.5f, 0.7f, 1f),
                LegendRole.Healer => new Color(0.35f, 1f, 0.65f),
                LegendRole.Priest => new Color(0.85f, 0.55f, 1f),
                _ => new Color(1f, 0.9f, 0.4f),
            };
            DrawCircle(p, pulse, WithAlpha(color, 0.17f));
            DrawCircle(p, pulse, WithAlpha(color, 0.8f), false, 1.4f, false);
            DrawRect(new Rect2(p + new Vector2(-3, -15), new Vector2(6, 3)), color);
        }
    }

    private void DrawFleets(WorldExpansionState state, Rect2 visible, float zoom)
    {
        foreach (FleetState fleet in state.Fleets.Values.Where(f => f.IsActive).OrderBy(f => f.Id))
        {
            Vector2 p = TileCenter(fleet.X, fleet.Y);
            if (!visible.HasPoint(p)) continue;
            float bob = MathF.Sin((float)_animationTime * 3 + fleet.Id) * 1.5f;
            Color color = RaceColor(state.KingdomRaces.GetValueOrDefault(fleet.KingdomId, RaceKind.Human));
            DrawRect(new Rect2(p + new Vector2(-9, 2 + bob), new Vector2(18, 5)), new Color(0.38f, 0.22f, 0.12f));
            DrawLine(p + new Vector2(0, -9 + bob), p + new Vector2(0, 4 + bob), new Color(0.52f, 0.36f, 0.2f), 2, false);
            var sail = new PackedVector2Array(new[] { p + new Vector2(1, -8 + bob), p + new Vector2(9, -2 + bob), p + new Vector2(1, -1 + bob) });
            DrawColoredPolygon(sail, WithAlpha(color, 0.9f));
            if (zoom >= 0.7f && fleet.Path.Count > fleet.PathIndex)
            {
                Vector2 previous = p;
                for (int i = fleet.PathIndex; i < Math.Min(fleet.Path.Count, fleet.PathIndex + 45); i += 3)
                {
                    Vector2 next = TileCenter(fleet.Path[i].X, fleet.Path[i].Y);
                    DrawLine(previous, next, WithAlpha(color, 0.3f), 1, false);
                    previous = next;
                }
            }
        }
    }

    private void DrawNomads(WorldExpansionState state, Rect2 visible, float zoom)
    {
        foreach (NomadBandState band in state.Nomads.Values.Where(n => n.Active))
        {
            Vector2 p = TileCenter(band.X, band.Y);
            if (!visible.HasPoint(p)) continue;
            Color color = RaceColor(band.Race);
            int tents = Math.Clamp(band.Population / 12, 1, 4);
            for (int i = 0; i < tents; i++)
            {
                Vector2 tp = p + new Vector2(i * 7 - tents * 3, MathF.Sin((float)_animationTime + i) * 1.2f);
                var tent = new PackedVector2Array(new[] { tp + new Vector2(-4, 4), tp + new Vector2(0, -5), tp + new Vector2(4, 4) });
                DrawColoredPolygon(tent, WithAlpha(color, 0.8f));
            }
            DrawCircle(p, 10, WithAlpha(color, 0.2f), false, 1, false);
        }
    }

    private void DrawRuins(WorldExpansionState state, Rect2 visible, float zoom)
    {
        foreach (RuinState ruin in state.Ruins.Values)
        {
            Vector2 p = TileCenter(ruin.X, ruin.Y);
            if (!visible.HasPoint(p)) continue;
            Color color = ruin.Explored ? new Color(0.35f, 0.8f, 0.65f) : ruin.DiscoveredDay >= 0 ? new Color(0.85f, 0.62f, 0.22f) : new Color(0.38f, 0.35f, 0.42f, 0.45f);
            DrawRect(new Rect2(p + new Vector2(-7, -4), new Vector2(5, 10)), color.Darkened(0.25f));
            DrawRect(new Rect2(p + new Vector2(1, -8), new Vector2(6, 14)), color);
            DrawLine(p + new Vector2(-9, 6), p + new Vector2(10, 6), WithAlpha(color, 0.8f), 2, false);
            if (!ruin.Explored && ruin.DiscoveredDay >= 0)
                DrawCircle(p, 13 + MathF.Sin((float)_animationTime * 2 + ruin.Id) * 2, WithAlpha(color, 0.25f), false, 1.5f, false);
        }
    }

    private void DrawMages(GrandSimulationState simulation, WorldExpansionState state, Rect2 visible, float zoom)
    {
        if (!ShowMagic || zoom < 0.55f) return;
        foreach (MageProfile mage in state.Mages.Values)
        {
            SimEntity? entity = simulation.Entities.GetValueOrDefault(mage.EntityId);
            if (entity is null || !entity.IsAlive) continue;
            Vector2 p = TileCenter(entity.X, entity.Y);
            if (!visible.HasPoint(p)) continue;
            Color color = mage.School switch
            {
                MagicSchool.Nature => new Color(0.3f, 1f, 0.45f),
                MagicSchool.Fire => new Color(1f, 0.3f, 0.12f),
                MagicSchool.Healing => new Color(0.5f, 1f, 0.8f),
                MagicSchool.Storm => new Color(0.4f, 0.7f, 1f),
                MagicSchool.Necromancy => new Color(0.55f, 0.9f, 0.3f),
                _ => new Color(0.75f, 0.4f, 1f),
            };
            int motes = Math.Min(5, 1 + mage.Level / 2);
            for (int i = 0; i < motes; i++)
            {
                float angle = (float)_animationTime * (1.2f + i * 0.08f) + i * MathF.Tau / motes;
                Vector2 mote = p + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (8 + i);
                DrawCircle(mote, 1.4f, WithAlpha(color, 0.85f));
            }
        }
    }

    private void DrawFaith(GrandSimulationState simulation, WorldExpansionState state, Rect2 visible, float zoom)
    {
        if (!ShowFaith) return;
        foreach ((ulong cityId, float faith) in state.Faith.CityFaith)
        {
            if (!simulation.Settlements.TryGetValue(cityId, out SettlementState? city)) continue;
            Vector2 p = TileCenter(city.X, city.Y);
            if (!visible.HasPoint(p) || faith < 10) continue;
            Color color = state.Faith.Path switch
            {
                DeityPath.Mercy => new Color(0.5f, 1f, 0.75f),
                DeityPath.Nature => new Color(0.25f, 0.9f, 0.35f),
                DeityPath.War => new Color(1f, 0.25f, 0.15f),
                DeityPath.Knowledge => new Color(0.4f, 0.65f, 1f),
                _ => new Color(0.72f, 0.28f, 0.85f),
            };
            float radius = 16 + MathF.Min(30, faith * 0.05f) + MathF.Sin((float)_animationTime + cityId) * 2;
            DrawCircle(p, radius, WithAlpha(color, 0.05f));
            DrawCircle(p, radius, WithAlpha(color, 0.2f), false, 1, false);
        }
    }

    private Vector2 TileCenter(int x, int y) => new((x + 0.5f) * TilePixelSize, (y + 0.5f) * TilePixelSize);
    private static Vector2 PixelSnap(Vector2 value) => new(MathF.Round(value.X), MathF.Round(value.Y));
    private static Color WithAlpha(Color color, float alpha) => new(color.R, color.G, color.B, alpha);

    private static Color RaceColor(RaceKind race) => race switch
    {
        RaceKind.Sylvan => new Color(0.28f, 0.75f, 0.36f),
        RaceKind.Dwarf => new Color(0.72f, 0.42f, 0.2f),
        RaceKind.Orc => new Color(0.52f, 0.67f, 0.24f),
        RaceKind.Tideborn => new Color(0.2f, 0.68f, 0.88f),
        RaceKind.Arcane => new Color(0.68f, 0.4f, 0.92f),
        _ => new Color(0.32f, 0.55f, 0.88f),
    };
}
