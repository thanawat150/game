using Godot;
using WorldForge.Core.Simulation;

namespace WorldForge.Presentation;

/// <summary>
/// Draws creatures, armies, diplomacy and detailed procedural pixel-art settlements
/// in a single canvas node. No Godot node is created per simulated object.
/// </summary>
public sealed partial class SimulationRenderer : Node2D
{
    private GrandSimulation? _simulation;

    public int TilePixelSize { get; set; } = 4;
    public ulong? SelectedEntityId { get; private set; }
    public ulong? SelectedSettlementId { get; private set; }
    public ulong? SelectedKingdomId { get; private set; }

    public void Bind(GrandSimulation simulation)
    {
        _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        ClearSelection();
        QueueRedraw();
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
        if (_simulation is null)
            return;

        GrandSimulationState state = _simulation.State;
        DrawDiplomacyLinks(state);
        DrawTerritories(state);
        DrawArmyRoutes(state);
        DrawSettlements(state);
        DrawArmies(state);
        DrawEntities(state);
    }

    private void DrawDiplomacyLinks(GrandSimulationState state)
    {
        KingdomState[] kingdoms = state.Kingdoms.Values.OrderBy(k => k.Id).ToArray();
        for (int i = 0; i < kingdoms.Length; i++)
        {
            if (!TryCapitalPosition(state, kingdoms[i], out Vector2 first))
                continue;

            for (int j = i + 1; j < kingdoms.Length; j++)
            {
                if (!TryCapitalPosition(state, kingdoms[j], out Vector2 second))
                    continue;

                RelationState relation = RelationFromValue(kingdoms[i].Relations.GetValueOrDefault(kingdoms[j].Id));
                Color color = relation switch
                {
                    RelationState.War => new Color(0.95f, 0.14f, 0.12f, 0.65f),
                    RelationState.Hostile => new Color(0.95f, 0.45f, 0.16f, 0.45f),
                    RelationState.Friendly => new Color(0.35f, 0.85f, 0.45f, 0.42f),
                    RelationState.Alliance => new Color(0.2f, 0.9f, 0.95f, 0.66f),
                    _ => new Color(0.75f, 0.75f, 0.8f, 0.16f),
                };
                DrawLine(first, second, color, relation is RelationState.War or RelationState.Alliance ? 2.4f : 1f, false);
            }
        }
    }

    private void DrawTerritories(GrandSimulationState state)
    {
        foreach (KingdomState kingdom in state.Kingdoms.Values.OrderBy(k => k.Id))
        {
            Color color = KingdomColor(kingdom.Id);
            bool selected = SelectedKingdomId == kingdom.Id;
            foreach (ulong settlementId in kingdom.Settlements)
            {
                if (!state.Settlements.TryGetValue(settlementId, out SettlementState? settlement))
                    continue;

                Vector2 center = TileCenter(settlement.X, settlement.Y);
                float radius = settlement.Stage switch
                {
                    SettlementStage.Capital => 15f * TilePixelSize,
                    SettlementStage.City => 13f * TilePixelSize,
                    SettlementStage.Town => 11f * TilePixelSize,
                    SettlementStage.Village => 9f * TilePixelSize,
                    _ => 7f * TilePixelSize,
                };
                DrawCircle(center, radius, WithAlpha(color, selected ? 0.18f : 0.08f));
                DrawCircle(center, radius, WithAlpha(color, selected ? 0.85f : 0.35f), false, selected ? 2.5f : 1f, false);
            }
        }
    }

    private void DrawArmyRoutes(GrandSimulationState state)
    {
        foreach (ArmyState army in state.Armies.Values.Where(a => a.IsActive).OrderBy(a => a.Id))
        {
            if (army.Path.Count <= 1 || army.PathIndex >= army.Path.Count)
                continue;

            Color color = WithAlpha(KingdomColor(army.KingdomId), 0.5f);
            Vector2 previous = TileCenter(army.X, army.Y);
            int end = Math.Min(army.Path.Count, army.PathIndex + 80);
            for (int i = army.PathIndex; i < end; i++)
            {
                Vector2 next = TileCenter(army.Path[i].X, army.Path[i].Y);
                DrawLine(previous, next, color, 1.2f, false);
                previous = next;
            }
        }
    }

    private void DrawSettlements(GrandSimulationState state)
    {
        foreach (SettlementState settlement in state.Settlements.Values.OrderBy(s => s.Y).ThenBy(s => s.Id))
        {
            Color kingdomColor = settlement.KingdomId is ulong kingdomId
                ? KingdomColor(kingdomId)
                : new Color(0.92f, 0.82f, 0.4f);
            DrawPixelCity(settlement, kingdomColor);

            if (SelectedSettlementId == settlement.Id)
            {
                float radius = settlement.Stage switch
                {
                    SettlementStage.Capital => 42,
                    SettlementStage.City => 36,
                    SettlementStage.Town => 30,
                    SettlementStage.Village => 25,
                    _ => 20,
                };
                DrawCircle(TileCenter(settlement.X, settlement.Y), radius, Colors.White, false, 2.2f, false);
            }
        }
    }

    private void DrawPixelCity(SettlementState settlement, Color kingdomColor)
    {
        Vector2 center = PixelSnap(TileCenter(settlement.X, settlement.Y));
        int scale = settlement.Stage switch
        {
            SettlementStage.Capital => 3,
            SettlementStage.City => 3,
            SettlementStage.Town => 2,
            SettlementStage.Village => 2,
            _ => 1,
        };
        int width = settlement.Stage switch
        {
            SettlementStage.Capital => 72,
            SettlementStage.City => 62,
            SettlementStage.Town => 48,
            SettlementStage.Village => 38,
            _ => 28,
        };
        int height = settlement.Stage switch
        {
            SettlementStage.Capital => 58,
            SettlementStage.City => 52,
            SettlementStage.Town => 42,
            SettlementStage.Village => 34,
            _ => 24,
        };

        Vector2 origin = PixelSnap(center - new Vector2(width / 2f, height / 2f));
        Color ground = new(0.22f, 0.18f, 0.13f, 0.9f);
        Color road = new(0.56f, 0.48f, 0.34f, 0.95f);
        Color stone = new(0.55f, 0.58f, 0.62f);
        Color darkStone = Shade(stone, 0.56f);
        Color roof = Shade(kingdomColor, 0.58f);
        Color roofLight = Shade(kingdomColor, 0.82f);
        Color plaster = new(0.79f, 0.68f, 0.5f);
        Color window = new(1f, 0.78f, 0.25f);
        Color shadow = new(0.03f, 0.03f, 0.04f, 0.55f);

        DrawRect(new Rect2(origin + new Vector2(4, 6), new Vector2(width, height)), shadow);
        DrawRect(new Rect2(origin, new Vector2(width, height)), ground);

        DrawFarm(origin + new Vector2(-14, 5), 12, 18, kingdomColor, settlement.Id);
        DrawFarm(origin + new Vector2(width + 2, height - 22), 12, 18, kingdomColor, settlement.Id + 13);
        if (settlement.Stage >= SettlementStage.Town)
        {
            DrawTree(origin + new Vector2(-8, height - 8), 2);
            DrawTree(origin + new Vector2(width + 6, 8), 2);
            DrawTree(origin + new Vector2(width + 10, 18), 2);
        }

        DrawRect(new Rect2(origin + new Vector2(width / 2f - 2, 2), new Vector2(4, height - 4)), road);
        DrawRect(new Rect2(origin + new Vector2(2, height / 2f - 2), new Vector2(width - 4, 4)), road);
        DrawRect(new Rect2(origin + new Vector2(width / 2f - 1, 2), new Vector2(1, height - 4)), Shade(road, 1.15f));
        DrawRect(new Rect2(origin + new Vector2(2, height / 2f - 1), new Vector2(width - 4, 1)), Shade(road, 1.15f));

        int houseCount = settlement.Stage switch
        {
            SettlementStage.Capital => 12,
            SettlementStage.City => 10,
            SettlementStage.Town => 7,
            SettlementStage.Village => 5,
            _ => 2,
        };
        for (int i = 0; i < houseCount; i++)
        {
            int hash = StableHash(settlement.Id, i);
            int quadrant = i % 4;
            int houseWidth = 7 + Math.Abs(hash % 3) * scale / 2;
            int houseHeight = 6 + Math.Abs((hash / 7) % 3) * scale / 2;
            float x = quadrant switch
            {
                0 or 2 => 5 + Math.Abs(hash % Math.Max(1, width / 2 - houseWidth - 7)),
                _ => width / 2f + 5 + Math.Abs(hash % Math.Max(1, width / 2 - houseWidth - 9)),
            };
            float y = quadrant switch
            {
                0 or 1 => 5 + Math.Abs((hash / 11) % Math.Max(1, height / 2 - houseHeight - 7)),
                _ => height / 2f + 5 + Math.Abs((hash / 13) % Math.Max(1, height / 2 - houseHeight - 9)),
            };
            DrawPixelBuilding(
                PixelSnap(origin + new Vector2(x, y)),
                houseWidth,
                houseHeight,
                plaster,
                i % 2 == 0 ? roof : roofLight,
                window,
                hash);
        }

        if (settlement.Buildings.Contains("building.market") || settlement.Stage >= SettlementStage.Town)
        {
            Vector2 market = PixelSnap(center + new Vector2(5, 4));
            DrawRect(new Rect2(market, new Vector2(12, 8)), Shade(plaster, 0.8f));
            for (int i = 0; i < 4; i++)
            {
                Color stripe = i % 2 == 0 ? kingdomColor : new Color(0.94f, 0.9f, 0.75f);
                DrawRect(new Rect2(market + new Vector2(i * 3, -3), new Vector2(3, 4)), stripe);
            }
            DrawRect(new Rect2(market + new Vector2(2, 3), new Vector2(2, 2)), window);
            DrawRect(new Rect2(market + new Vector2(8, 3), new Vector2(2, 2)), window);
        }

        int keepWidth = settlement.Stage == SettlementStage.Capital ? 18 : settlement.Stage == SettlementStage.City ? 15 : 12;
        int keepHeight = settlement.Stage == SettlementStage.Capital ? 20 : settlement.Stage == SettlementStage.City ? 17 : 13;
        Vector2 keepOrigin = PixelSnap(center - new Vector2(keepWidth / 2f, keepHeight / 2f + 3));
        DrawRect(new Rect2(keepOrigin + new Vector2(2, 4), new Vector2(keepWidth, keepHeight)), shadow);
        DrawRect(new Rect2(keepOrigin, new Vector2(keepWidth, keepHeight)), stone);
        DrawRect(new Rect2(keepOrigin, new Vector2(keepWidth, 3)), darkStone);
        DrawRect(new Rect2(keepOrigin + new Vector2(2, -3), new Vector2(keepWidth - 4, 3)), roof);
        DrawRect(new Rect2(keepOrigin + new Vector2(4, -5), new Vector2(keepWidth - 8, 2)), roofLight);
        DrawRect(new Rect2(keepOrigin + new Vector2(keepWidth / 2f - 2, keepHeight - 6), new Vector2(4, 6)), Shade(darkStone, 0.65f));
        for (int y = 5; y < keepHeight - 7; y += 5)
        {
            DrawRect(new Rect2(keepOrigin + new Vector2(3, y), new Vector2(2, 2)), window);
            DrawRect(new Rect2(keepOrigin + new Vector2(keepWidth - 5, y), new Vector2(2, 2)), window);
        }

        if (settlement.Fortification > 0 || settlement.Stage >= SettlementStage.Town)
            DrawCityWall(origin, width, height, stone, darkStone, settlement.Fortification);

        Vector2 bannerBase = keepOrigin + new Vector2(keepWidth / 2f, -6);
        DrawLine(bannerBase, bannerBase + new Vector2(0, -12), darkStone, 2, false);
        DrawRect(new Rect2(bannerBase + new Vector2(1, -11), new Vector2(8, 5)), kingdomColor);
        DrawRect(new Rect2(bannerBase + new Vector2(1, -10), new Vector2(5, 1)), Shade(kingdomColor, 1.25f));
    }

    private void DrawCityWall(Vector2 origin, int width, int height, Color stone, Color darkStone, int fortification)
    {
        int wall = Math.Clamp(2 + fortification / 2, 2, 5);
        DrawRect(new Rect2(origin, new Vector2(width, wall)), stone);
        DrawRect(new Rect2(origin + new Vector2(0, height - wall), new Vector2(width, wall)), stone);
        DrawRect(new Rect2(origin, new Vector2(wall, height)), stone);
        DrawRect(new Rect2(origin + new Vector2(width - wall, 0), new Vector2(wall, height)), stone);

        for (int x = 1; x < width - 2; x += 6)
        {
            DrawRect(new Rect2(origin + new Vector2(x, -2), new Vector2(3, 2)), darkStone);
            DrawRect(new Rect2(origin + new Vector2(x, height), new Vector2(3, 2)), darkStone);
        }
        for (int y = 1; y < height - 2; y += 6)
        {
            DrawRect(new Rect2(origin + new Vector2(-2, y), new Vector2(2, 3)), darkStone);
            DrawRect(new Rect2(origin + new Vector2(width, y), new Vector2(2, 3)), darkStone);
        }

        int tower = 8 + Math.Min(3, fortification);
        Vector2[] towers =
        {
            origin - new Vector2(2, 2),
            origin + new Vector2(width - tower + 2, -2),
            origin + new Vector2(-2, height - tower + 2),
            origin + new Vector2(width - tower + 2, height - tower + 2),
        };
        foreach (Vector2 position in towers)
        {
            DrawRect(new Rect2(position + new Vector2(2, 3), new Vector2(tower, tower)), new Color(0.02f, 0.02f, 0.03f, 0.4f));
            DrawRect(new Rect2(position, new Vector2(tower, tower)), stone);
            DrawRect(new Rect2(position, new Vector2(tower, 2)), darkStone);
            DrawRect(new Rect2(position + new Vector2(2, 3), new Vector2(2, 2)), new Color(0.18f, 0.2f, 0.23f));
        }
    }

    private void DrawPixelBuilding(Vector2 origin, int width, int height, Color body, Color roof, Color window, int variant)
    {
        Color shadow = new(0.02f, 0.02f, 0.03f, 0.48f);
        DrawRect(new Rect2(origin + new Vector2(2, 3), new Vector2(width, height)), shadow);
        DrawRect(new Rect2(origin, new Vector2(width, height)), variant % 3 == 0 ? Shade(body, 0.9f) : body);

        int roofHeight = Math.Max(3, height / 3);
        for (int row = 0; row < roofHeight; row++)
        {
            int inset = row;
            int rowWidth = Math.Max(2, width - inset * 2);
            DrawRect(new Rect2(origin + new Vector2(inset, -roofHeight + row), new Vector2(rowWidth, 1)), row % 2 == 0 ? roof : Shade(roof, 1.12f));
        }

        DrawRect(new Rect2(origin + new Vector2(width / 2f - 1, height - 4), new Vector2(3, 4)), Shade(body, 0.45f));
        if (width >= 7)
        {
            DrawRect(new Rect2(origin + new Vector2(1, 2), new Vector2(2, 2)), window);
            DrawRect(new Rect2(origin + new Vector2(width - 3, 2), new Vector2(2, 2)), window);
        }
        if (variant % 4 == 0)
        {
            DrawRect(new Rect2(origin + new Vector2(width - 2, -roofHeight - 2), new Vector2(2, 4)), Shade(body, 0.55f));
            DrawRect(new Rect2(origin + new Vector2(width - 3, -roofHeight - 3), new Vector2(3, 1)), new Color(0.28f, 0.26f, 0.25f, 0.6f));
        }
    }

    private void DrawFarm(Vector2 origin, int width, int height, Color kingdomColor, ulong seed)
    {
        Color soil = new(0.32f, 0.2f, 0.11f, 0.9f);
        Color cropA = new(0.55f, 0.72f, 0.22f);
        Color cropB = Shade(kingdomColor, 0.9f);
        DrawRect(new Rect2(origin, new Vector2(width, height)), soil);
        for (int x = 2; x < width; x += 3)
        {
            Color crop = ((x + (int)(seed % 3)) / 3) % 2 == 0 ? cropA : cropB;
            DrawRect(new Rect2(origin + new Vector2(x, 1), new Vector2(1, height - 2)), crop);
        }
        DrawRect(new Rect2(origin, new Vector2(width, 1)), new Color(0.62f, 0.47f, 0.25f));
        DrawRect(new Rect2(origin + new Vector2(0, height - 1), new Vector2(width, 1)), new Color(0.62f, 0.47f, 0.25f));
    }

    private void DrawTree(Vector2 basePosition, int scale)
    {
        Color trunk = new(0.3f, 0.18f, 0.09f);
        Color dark = new(0.08f, 0.3f, 0.13f);
        Color light = new(0.2f, 0.55f, 0.2f);
        DrawRect(new Rect2(basePosition, new Vector2(scale, scale * 3)), trunk);
        DrawRect(new Rect2(basePosition + new Vector2(-scale * 2, -scale * 3), new Vector2(scale * 5, scale * 4)), dark);
        DrawRect(new Rect2(basePosition + new Vector2(-scale, -scale * 4), new Vector2(scale * 3, scale * 3)), light);
    }

    private void DrawArmies(GrandSimulationState state)
    {
        foreach (ArmyState army in state.Armies.Values.Where(a => a.IsActive).OrderBy(a => a.Y).ThenBy(a => a.Id))
        {
            Vector2 center = PixelSnap(TileCenter(army.X, army.Y));
            Color color = KingdomColor(army.KingdomId);
            Color dark = Shade(color, 0.42f);
            int rows = Math.Clamp((int)MathF.Ceiling(army.Units / 4f), 1, 4);
            int columns = Math.Clamp(army.Units, 1, 4);
            DrawCircle(center + new Vector2(1, 3), 9, new Color(0, 0, 0, 0.35f));
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int index = row * columns + column;
                    if (index >= army.Units)
                        break;
                    Vector2 unit = center + new Vector2(column * 4 - 6, row * 4 - 5);
                    DrawRect(new Rect2(unit, new Vector2(3, 3)), dark);
                    DrawRect(new Rect2(unit + new Vector2(1, 0), new Vector2(2, 2)), color);
                    DrawRect(new Rect2(unit + new Vector2(1, -1), new Vector2(1, 1)), new Color(0.75f, 0.72f, 0.64f));
                }
            }

            DrawLine(center + new Vector2(0, 3), center + new Vector2(0, -14), dark, 2, false);
            DrawRect(new Rect2(center + new Vector2(1, -14), new Vector2(10, 6)), color);
            DrawRect(new Rect2(center + new Vector2(2, -13), new Vector2(6, 1)), Shade(color, 1.25f));

            Color status = army.Status switch
            {
                ArmyStatus.Besieging => new Color(1f, 0.24f, 0.12f),
                ArmyStatus.Retreating => new Color(1f, 0.72f, 0.15f),
                ArmyStatus.Stalled => new Color(0.65f, 0.65f, 0.68f),
                _ => Colors.White,
            };
            DrawCircle(center, 10, status, false, 1.2f, false);
        }
    }

    private void DrawEntities(GrandSimulationState state)
    {
        HashSet<ulong> infected = state.Diseases.SelectMany(d => d.InfectedDays.Keys).ToHashSet();
        foreach (SimEntity entity in state.Entities.Values.OrderBy(e => e.Y).ThenBy(e => e.Id))
        {
            Vector2 center = PixelSnap(TileCenter(entity.X, entity.Y));
            Color color = entity.Species switch
            {
                SpeciesKind.Grazer => new Color(0.75f, 0.95f, 0.35f),
                SpeciesKind.Predator => new Color(0.95f, 0.24f, 0.18f),
                SpeciesKind.Settler => entity.KingdomId is ulong kingdomId ? KingdomColor(kingdomId) : new Color(1f, 0.85f, 0.25f),
                SpeciesKind.Monster => new Color(0.8f, 0.2f, 0.9f),
                SpeciesKind.Fish => new Color(0.2f, 0.88f, 1f),
                _ => Colors.White,
            };
            float radius = entity.Species == SpeciesKind.Monster ? 4.2f : entity.Species == SpeciesKind.Settler ? 3.2f : 2.6f;
            DrawCircle(center, radius + 1.2f, new Color(0.04f, 0.04f, 0.05f, 0.9f));
            DrawCircle(center, radius, color);

            if (entity.PathIndex < entity.Path.Count)
            {
                GridPoint next = entity.Path[entity.PathIndex];
                Vector2 direction = new Vector2(next.X - entity.X, next.Y - entity.Y).Normalized();
                DrawRect(new Rect2(PixelSnap(center + direction * (radius + 2)) - Vector2.One, new Vector2(2, 2)), Colors.White);
            }

            if (entity.AgeDays < 30)
                DrawCircle(center, radius + 2, new Color(1f, 0.85f, 0.9f, 0.8f), false, 1, false);
            if (entity.PregnancyDaysRemaining > 0)
                DrawCircle(center, radius + 3, new Color(1f, 0.45f, 0.72f, 0.9f), false, 1.1f, false);
            if (infected.Contains(entity.Id))
                DrawCircle(center, radius + 4.5f, new Color(1f, 0.55f, 0.08f, 0.95f), false, 1.4f, false);
            if (entity.Traits.Contains("trait.blessed"))
                DrawCircle(center, radius + 6f, new Color(1f, 0.95f, 0.3f, 0.75f), false, 1.2f, false);
            if (SelectedEntityId == entity.Id)
            {
                DrawCircle(center, radius + 8f, Colors.White, false, 2.2f, false);
                DrawSelectedEntityPath(entity);
            }
        }
    }

    private void DrawSelectedEntityPath(SimEntity entity)
    {
        if (entity.Path.Count <= 1 || entity.PathIndex >= entity.Path.Count)
            return;

        Vector2 previous = TileCenter(entity.X, entity.Y);
        int end = Math.Min(entity.Path.Count, entity.PathIndex + 80);
        for (int i = entity.PathIndex; i < end; i++)
        {
            Vector2 next = TileCenter(entity.Path[i].X, entity.Path[i].Y);
            DrawLine(previous, next, new Color(1f, 1f, 1f, 0.65f), 1f, false);
            previous = next;
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

    private Vector2 TileCenter(int x, int y) =>
        new((x + 0.5f) * TilePixelSize, (y + 0.5f) * TilePixelSize);

    private static Vector2 PixelSnap(Vector2 value) =>
        new(Mathf.Round(value.X), Mathf.Round(value.Y));

    private static int StableHash(ulong seed, int index)
    {
        unchecked
        {
            uint value = (uint)(seed ^ (seed >> 32));
            value ^= (uint)index * 0x9E3779B9u;
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (int)(value & 0x7FFFFFFF);
        }
    }

    private static Color KingdomColor(ulong id)
    {
        float hue = (float)((id * 0.1732050807) % 1.0);
        return Color.FromHsv(hue, 0.72f, 0.96f);
    }

    private static Color Shade(Color color, float factor) =>
        new(
            Math.Clamp(color.R * factor, 0, 1),
            Math.Clamp(color.G * factor, 0, 1),
            Math.Clamp(color.B * factor, 0, 1),
            color.A);

    private static Color WithAlpha(Color color, float alpha) =>
        new(color.R, color.G, color.B, alpha);

    private static RelationState RelationFromValue(int value) => value switch
    {
        <= -70 => RelationState.War,
        <= -25 => RelationState.Hostile,
        >= 70 => RelationState.Alliance,
        >= 25 => RelationState.Friendly,
        _ => RelationState.Neutral,
    };
}
