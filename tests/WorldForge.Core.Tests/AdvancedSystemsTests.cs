using WorldForge.Core.Simulation;
using WorldForge.Core.World;
using Xunit;

namespace WorldForge.Core.Tests;

public sealed class AdvancedSystemsTests
{
    [Fact]
    public void AStarFindsLongRouteThroughOnlyAvailableGap()
    {
        WorldMap world = CreateFlatWorld(64, TerrainType.Grassland);
        for (int y = 0; y < world.Height; y++)
        {
            if (y != 40)
                world.SetTerrain(30, y, TerrainType.DeepOcean);
        }

        var pathfinder = new GridPathfinder(world);
        IReadOnlyList<GridPoint> path = pathfinder.FindPath(
            new GridPoint(5, 8),
            new GridPoint(55, 8),
            SpeciesKind.Settler);

        Assert.NotEmpty(path);
        Assert.Equal(new GridPoint(5, 8), path[0]);
        Assert.Equal(new GridPoint(55, 8), path[^1]);
        Assert.Contains(path, point => point.X == 30 && point.Y == 40);
        Assert.DoesNotContain(path, point => point.X == 30 && point.Y != 40);
    }

    [Fact]
    public void PredatorUsesPathfindingToApproachDistantPrey()
    {
        WorldMap world = CreateFlatWorld(64, TerrainType.Grassland);
        var simulation = new GrandSimulation(world, 5001);
        SimEntity predator = simulation.SpawnEntity(SpeciesKind.Predator, 5, 20, "Path Hunter");
        simulation.SpawnEntity(SpeciesKind.Grazer, 35, 20, "Distant Prey");
        int before = DistanceSquared(predator.X, predator.Y, 35, 20);

        simulation.AdvanceDays(8);

        int after = DistanceSquared(predator.X, predator.Y, 35, 20);
        Assert.True(after < before, $"Predator did not approach prey. Before={before}, after={after}");
        Assert.NotEmpty(predator.Path);
        Assert.Equal(EntityAction.Hunt, predator.Action);
    }

    [Fact]
    public void ReproductionCreatesChildWithParentsGenerationAndMutatedGenes()
    {
        WorldMap world = CreateFlatWorld(64, TerrainType.Grassland);
        var simulation = new GrandSimulation(world, 6002);
        SimEntity female = simulation.SpawnEntity(SpeciesKind.Grazer, 20, 20, "Mother");
        SimEntity male = simulation.SpawnEntity(SpeciesKind.Grazer, 20, 20, "Father");
        female.Sex = BiologicalSex.Female;
        male.Sex = BiologicalSex.Male;
        female.AgeDays = 300;
        male.AgeDays = 300;
        female.Fertility = 1;
        male.Fertility = 1;
        female.FertilityGene = 1.3f;
        male.FertilityGene = 1.3f;

        simulation.AdvanceDays(70);

        SimEntity[] children = simulation.State.Entities.Values
            .Where(e => e.Id != female.Id && e.Id != male.Id)
            .OrderBy(e => e.Id)
            .ToArray();
        Assert.NotEmpty(children);
        SimEntity child = children[0];
        Assert.Equal(1, child.Generation);
        Assert.Contains(female.Id, child.Parents);
        Assert.Contains(male.Id, child.Parents);
        Assert.Contains(child.Id, female.Children);
        Assert.Contains(child.Id, male.Children);
        Assert.InRange(child.SpeedGene, 0.65f, 1.35f);
        Assert.InRange(child.VitalityGene, 0.65f, 1.35f);
        Assert.True(simulation.State.TotalBirths >= 1);
        Assert.Contains(simulation.State.Chronicle, e => e.Type == "family.birth");
    }

    [Fact]
    public void WarAutomaticallyMobilizesMovesBattlesAndCapturesCity()
    {
        WorldMap world = CreateFlatWorld(64, TerrainType.Grassland);
        var simulation = new GrandSimulation(world, 7003);
        ulong first = CreateKingdom(simulation, 8, 24, "Aurora");
        ulong second = CreateKingdom(simulation, 54, 24, "Verdant");
        ulong secondCapital = simulation.State.Kingdoms[second].CapitalId;
        simulation.SetRelation(first, second, -90);

        simulation.AdvanceDay();

        ArmyState attacker = simulation.State.Armies.Values.First(a => a.KingdomId == first);
        ArmyState defender = simulation.State.Armies.Values.First(a => a.KingdomId == second);
        attacker.Units = 100;
        attacker.Morale = 1.3f;
        attacker.Supply = 100;
        defender.Units = 1;
        int startX = attacker.X;

        simulation.AdvanceDays(45);

        Assert.True(attacker.X != startX || attacker.Status == ArmyStatus.Disbanded);
        Assert.True(simulation.State.TotalBattles > 0);
        Assert.True(
            simulation.State.TotalCitiesCaptured > 0 ||
            !simulation.State.Kingdoms.ContainsKey(second) ||
            simulation.State.Settlements[secondCapital].KingdomId == first);
        Assert.Contains(simulation.State.Chronicle, e =>
            e.Type is "battle.field" or "city.captured" or "kingdom.collapsed");
    }

    [Fact]
    public void AdvancedSaveRoundTripPreservesPathsFamiliesAndArmies()
    {
        WorldMap world = CreateFlatWorld(64, TerrainType.Grassland);
        var simulation = new GrandSimulation(world, 8004);
        SimEntity first = simulation.SpawnEntity(SpeciesKind.Grazer, 8, 8);
        SimEntity second = simulation.SpawnEntity(SpeciesKind.Grazer, 8, 8);
        first.Sex = BiologicalSex.Female;
        second.Sex = BiologicalSex.Male;
        first.AgeDays = second.AgeDays = 300;
        first.Fertility = second.Fertility = 1;
        ulong kingdomA = CreateKingdom(simulation, 10, 30, "East");
        ulong kingdomB = CreateKingdom(simulation, 50, 30, "West");
        simulation.SetRelation(kingdomA, kingdomB, -90);
        simulation.AdvanceDays(12);

        string json = simulation.SaveToJson();
        GrandSimulation loaded = GrandSimulation.LoadFromJson(world, json);

        Assert.Equal(GrandSimulationState.CurrentSaveVersion, loaded.State.SaveVersion);
        Assert.Equal(simulation.State.Entities.Count, loaded.State.Entities.Count);
        Assert.Equal(simulation.State.Armies.Count, loaded.State.Armies.Count);
        Assert.Equal(simulation.State.TotalBirths, loaded.State.TotalBirths);
        Assert.All(loaded.State.Armies.Values, army => Assert.NotNull(army.Path));
        Assert.All(loaded.State.Entities.Values, entity => Assert.NotNull(entity.Path));
    }

    private static WorldMap CreateFlatWorld(int size, TerrainType terrain)
    {
        WorldMap world = WorldGenerator.Generate(new WorldGenerationConfig
        {
            Seed = 20260731,
            Width = size,
            Height = size,
            ChunkSize = 32,
        });
        for (int i = 0; i < world.TileCount; i++)
            world.SetTerrainByIndex(i, terrain);
        return world;
    }

    private static ulong CreateKingdom(GrandSimulation simulation, int x, int y, string name)
    {
        var settlers = new List<ulong>();
        for (int i = 0; i < 12; i++)
        {
            SimEntity settler = simulation.SpawnEntity(SpeciesKind.Settler, x, y, $"{name}-{i}");
            settler.AgeDays = 20 * 360;
            settlers.Add(settler.Id);
        }
        SettlementState settlement = simulation.FoundSettlement(settlers, $"{name} City");
        return simulation.FoundKingdom(settlement.Id, $"{name} Realm", GovernmentType.Council).Id;
    }

    private static int DistanceSquared(int x1, int y1, int x2, int y2)
    {
        int dx = x1 - x2;
        int dy = y1 - y2;
        return dx * dx + dy * dy;
    }
}
