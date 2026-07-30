using Godot;
using WorldForge.Core.Simulation;

namespace WorldForge.Presentation;

/// <summary>
/// Draws the complete living simulation in one canvas node. This deliberately avoids
/// one Godot Node per creature so the renderer can scale to thousands of entities.
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

        DrawDiplomacyLinks(_simulation.State);
        DrawTerritories(_simulation.State);
        DrawSettlements(_simulation.State);
        DrawEntities(_simulation.State);
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
                    RelationState.War => new Color(0.95f, 0.14f, 0.12f, 0.75f),
                    RelationState.Hostile => new Color(0.95f, 0.45f, 0.16f, 0.55f),
                    RelationState.Friendly => new Color(0.35f, 0.85f, 0.45f, 0.5f),
                    RelationState.Alliance => new Color(0.2f, 0.9f, 0.95f, 0.75f),
                    _ => new Color(0.75f, 0.75f, 0.8f, 0.22f),
                };
                DrawLine(first, second, color, relation is RelationState.War or RelationState.Alliance ? 2.4f : 1.0f, true);
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
                float radius = (selected ? 11f : 8f) * TilePixelSize;
                DrawCircle(center, radius, new Color(color.R, color.G, color.B, selected ? 0.2f : 0.11f));
                DrawCircle(center, radius, new Color(color.R, color.G, color.B, selected ? 0.9f : 0.45f), false, selected ? 2.5f : 1.0f, true);
            }
        }
    }

    private void DrawSettlements(GrandSimulationState state)
    {
        foreach (SettlementState settlement in state.Settlements.Values.OrderBy(s => s.Id))
        {
            Color color = settlement.KingdomId is ulong kingdomId ? KingdomColor(kingdomId) : new Color(0.92f, 0.82f, 0.4f);
            float size = settlement.Stage switch
            {
                SettlementStage.Capital => 15f,
                SettlementStage.City => 13f,
                SettlementStage.Town => 11f,
                SettlementStage.Village => 9f,
                _ => 7f,
            };
            Vector2 center = TileCenter(settlement.X, settlement.Y);
            Rect2 body = new(center - new Vector2(size / 2f, size / 2f), new Vector2(size, size));
            DrawRect(body, new Color(0.08f, 0.08f, 0.1f, 0.9f));
            DrawRect(new Rect2(body.Position + Vector2.One * 1.5f, body.Size - Vector2.One * 3f), color);
            DrawLine(center + new Vector2(-size / 2f, 0), center + new Vector2(size / 2f, 0), Colors.White, 1f);

            if (SelectedSettlementId == settlement.Id)
                DrawCircle(center, size, Colors.White, false, 2.2f, true);
        }
    }

    private void DrawEntities(GrandSimulationState state)
    {
        HashSet<ulong> infected = state.Diseases.SelectMany(d => d.InfectedDays.Keys).ToHashSet();
        foreach (SimEntity entity in state.Entities.Values.OrderBy(e => e.Id))
        {
            Vector2 center = TileCenter(entity.X, entity.Y);
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

            if (infected.Contains(entity.Id))
                DrawCircle(center, radius + 3.5f, new Color(1f, 0.55f, 0.08f, 0.95f), false, 1.4f, true);
            if (entity.Traits.Contains("trait.blessed"))
                DrawCircle(center, radius + 5.5f, new Color(1f, 0.95f, 0.3f, 0.75f), false, 1.2f, true);
            if (SelectedEntityId == entity.Id)
                DrawCircle(center, radius + 7f, Colors.White, false, 2.2f, true);
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

    private Vector2 TileCenter(int x, int y) => new((x + 0.5f) * TilePixelSize, (y + 0.5f) * TilePixelSize);

    private static Color KingdomColor(ulong id)
    {
        float hue = (float)((id * 0.1732050807) % 1.0);
        return Color.FromHsv(hue, 0.72f, 0.96f);
    }

    private static RelationState RelationFromValue(int value) => value switch
    {
        <= -70 => RelationState.War,
        <= -25 => RelationState.Hostile,
        >= 70 => RelationState.Alliance,
        >= 25 => RelationState.Friendly,
        _ => RelationState.Neutral,
    };
}
