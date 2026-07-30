using WorldForge.Core.Simulation;
using WorldForge.Core.World;
using Xunit;

namespace WorldForge.Core.Tests;

public sealed class GrandSimulationTests
{
    [Fact]
    public void EcosystemPredatorCanKillNearbyPrey()
    {
        WorldMap world = CreateWorld();
        (int x, int y) = FindLand(world);
        var simulation = new GrandSimulation(world, 10);
        SimEntity prey = simulation.SpawnEntity(SpeciesKind.Grazer, x, y, "Prey");
        SimEntity predator = simulation.SpawnEntity(SpeciesKind.Predator, x, y, "Hunter");
        predator.Hunger = 10;

        simulation.AdvanceDays(4);

        Assert.False(simulation.State.Entities.ContainsKey(prey.Id));
        Assert.Contains(simulation.State.Chronicle, e => e.Type == "entity.killed");
    }

    [Fact]
    public void SettlersFoundSettlementAndKingdom()
    {
        WorldMap world = CreateWorld();
        (int x, int y) = FindLand(world);
        var simulation = new GrandSimulation(world, 20);
        var ids = new List<ulong>();
        for (int i = 0; i < 8; i++)
        {
            SimEntity settler = simulation.SpawnEntity(SpeciesKind.Settler, x, y, $"Settler {i}");
            settler.AgeDays = 20 * 360;
            ids.Add(settler.Id);
        }

        SettlementState settlement = simulation.FoundSettlement(ids, "First Hearth");
        KingdomState kingdom = simulation.FoundKingdom(settlement.Id, "Aster Compact", GovernmentType.Council);

        Assert.Equal(SettlementStage.Capital, settlement.Stage);
        Assert.Equal(kingdom.Id, settlement.KingdomId);
        Assert.All(ids, id => Assert.Equal(kingdom.Id, simulation.State.Entities[id].KingdomId));
        Assert.Contains(simulation.State.Chronicle, e => e.Type == "kingdom.founded");
    }

    [Fact]
    public void DiplomacyUsesSymmetricBoundedRelations()
    {
        WorldMap world = CreateWorld();
        var simulation = new GrandSimulation(world, 30);
        ulong first = CreateKingdom(simulation, world, "North", 0);
        ulong second = CreateKingdom(simulation, world, "South", 12);

        simulation.SetRelation(first, second, -90);

        Assert.Equal(-90, simulation.State.Kingdoms[first].Relations[second]);
        Assert.Equal(-90, simulation.State.Kingdoms[second].Relations[first]);
        Assert.Equal(RelationState.War, simulation.GetRelationState(first, second));
    }

    [Fact]
    public void GodPowersModifyRealWorldAndEntities()
    {
        WorldMap world = CreateWorld();
        (int x, int y) = FindTerrain(world, TerrainType.Grassland);
        var simulation = new GrandSimulation(world, 40);
        SimEntity entity = simulation.SpawnEntity(SpeciesKind.Settler, x, y);
        entity.Health = 50;

        simulation.ApplyPower(GodPowerType.Blessing, x, y, 1);
        simulation.ApplyPower(GodPowerType.CreateForest, x, y, 1);

        Assert.True(entity.Health > 50);
        Assert.Contains("trait.blessed", entity.Traits);
        Assert.Equal(TerrainType.Forest, world.GetTerrain(x, y));
        Assert.Contains(simulation.State.Chronicle, e => e.Type == "power.used");
    }

    [Fact]
    public void AdvancedSimulationSaveRoundTripPreservesState()
    {
        WorldMap world = CreateWorld();
        (int x, int y) = FindLand(world);
        var simulation = new GrandSimulation(world, 50);
        SimEntity entity = simulation.SpawnEntity(SpeciesKind.Settler, x, y, "Archivist");
        simulation.ApplyPower(GodPowerType.Knowledge, x, y);
        simulation.AdvanceDays(5);

        string json = simulation.SaveToJson();
        GrandSimulation loaded = GrandSimulation.LoadFromJson(world, json);

        Assert.Equal(simulation.State.Tick, loaded.State.Tick);
        Assert.Equal(entity.Name, loaded.State.Entities[entity.Id].Name);
        Assert.Equal(entity.Intelligence, loaded.State.Entities[entity.Id].Intelligence);
        Assert.Equal(simulation.State.Chronicle.Count, loaded.State.Chronicle.Count);
    }

    [Fact]
    public void DiseaseAndModValidationAreOperational()
    {
        WorldMap world = CreateWorld();
        (int x, int y) = FindLand(world);
        var simulation = new GrandSimulation(world, 60);
        SimEntity first = simulation.SpawnEntity(SpeciesKind.Settler, x, y);
        simulation.SpawnEntity(SpeciesKind.Settler, x, y);
        simulation.Infect(first.Id, new DiseaseState { Id = "disease.test", InfectionRate = 1, MortalityRate = 0, DurationDays = 2 });

        simulation.AdvanceDay();
        Assert.Single(simulation.State.Diseases);
        Assert.True(simulation.State.Diseases[0].InfectedDays.Count >= 1);

        var badManifest = new ModManifest("bad mod", "invalid", new[] { "missing.mod" }, new[] { "species", "unknown" });
        IReadOnlyList<string> errors = ModValidator.Validate(badManifest, Array.Empty<string>());
        Assert.Equal(4, errors.Count);
    }

    private static WorldMap CreateWorld() => WorldGenerator.Generate(new WorldGenerationConfig { Seed = 20260730, Width = 128, Height = 128, ChunkSize = 64 });

    private static (int X, int Y) FindLand(WorldMap world)
    {
        for (int y = 0; y < world.Height; y++)
            for (int x = 0; x < world.Width; x++)
                if (world.GetTerrain(x, y) is TerrainType.Grassland or TerrainType.Forest or TerrainType.Beach)
                    return (x, y);
        throw new InvalidOperationException("No land tile generated.");
    }

    private static (int X, int Y) FindTerrain(WorldMap world, TerrainType terrain)
    {
        for (int y = 0; y < world.Height; y++)
            for (int x = 0; x < world.Width; x++)
                if (world.GetTerrain(x, y) == terrain)
                    return (x, y);
        throw new InvalidOperationException($"No {terrain} tile generated.");
    }

    private static ulong CreateKingdom(GrandSimulation simulation, WorldMap world, string name, int offset)
    {
        (int x, int y) = FindLand(world);
        x = Math.Min(world.Width - 1, x + offset);
        while (world.GetTerrain(x, y) is TerrainType.DeepOcean or TerrainType.ShallowWater or TerrainType.Mountain)
            x = Math.Max(0, x - 1);
        var ids = new List<ulong>();
        for (int i = 0; i < 5; i++) ids.Add(simulation.SpawnEntity(SpeciesKind.Settler, x, y, $"{name}-{i}").Id);
        SettlementState settlement = simulation.FoundSettlement(ids, $"{name} City");
        return simulation.FoundKingdom(settlement.Id, $"{name} Realm", GovernmentType.Council).Id;
    }
}
