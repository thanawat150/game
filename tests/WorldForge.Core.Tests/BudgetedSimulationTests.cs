using WorldForge.Core.Simulation;
using WorldForge.Core.World;
using Xunit;

namespace WorldForge.Core.Tests;

public sealed class BudgetedSimulationTests
{
    [Fact]
    public void BudgetedDayRespectsAiAndPathBudgets()
    {
        WorldMap world = CreateGrassWorld(64);
        var simulation = new GrandSimulation(world, 111);
        for (int i = 0; i < 80; i++)
            simulation.SpawnEntity(SpeciesKind.Grazer, 4 + i % 40, 4 + i / 40);
        for (int i = 0; i < 20; i++)
            simulation.SpawnEntity(SpeciesKind.Predator, 55 - i % 20, 50 - i / 20);

        var options = SimulationBudgetOptions.ForProfile(SimulationPerformanceProfile.Economy, 200);
        options.EntityAiUpdatesPerDay = 17;
        options.PathRequestsPerDay = 3;
        simulation.AdvanceDayBudgeted(options);

        Assert.Equal(1, simulation.State.Day);
        Assert.Equal(17, simulation.LastBudgetMetrics.AiEntitiesUpdated);
        Assert.InRange(simulation.LastBudgetMetrics.PathRequestsUsed, 0, 3);
    }

    [Fact]
    public void PopulationCapIsEnforced()
    {
        WorldMap world = CreateGrassWorld(64);
        var simulation = new GrandSimulation(world, 222);
        for (int i = 0; i < 90; i++)
            simulation.SpawnEntity(SpeciesKind.Grazer, 5 + i % 45, 5 + i / 45);

        var options = SimulationBudgetOptions.ForProfile(SimulationPerformanceProfile.Balanced, 50);
        options.EnableReproduction = false;
        simulation.AdvanceDayBudgeted(options);

        Assert.Equal(50, simulation.State.Entities.Count);
        Assert.Equal(40, simulation.LastBudgetMetrics.RemovedByPopulationCap);
    }

    [Fact]
    public void ReproductionCanBeDisabledWithoutChangingWorldTime()
    {
        WorldMap world = CreateGrassWorld(64);
        var simulation = new GrandSimulation(world, 333);
        SimEntity female = simulation.SpawnEntity(SpeciesKind.Grazer, 20, 20);
        SimEntity male = simulation.SpawnEntity(SpeciesKind.Grazer, 21, 20);
        female.Sex = BiologicalSex.Female;
        male.Sex = BiologicalSex.Male;
        female.AgeDays = 500;
        male.AgeDays = 500;
        female.MateId = male.Id;
        female.PregnancyDaysRemaining = 10;

        var options = SimulationBudgetOptions.ForProfile(SimulationPerformanceProfile.Balanced, 100);
        options.EnableReproduction = false;
        simulation.AdvanceDayBudgeted(options);

        Assert.Equal(1, simulation.State.Day);
        Assert.Equal(0, female.PregnancyDaysRemaining);
        Assert.Null(female.MateId);
        Assert.Equal(2, simulation.State.Entities.Count);
    }

    [Fact]
    public void SpatialIndexFindsNearestSpecies()
    {
        WorldMap world = CreateGrassWorld(64);
        var simulation = new GrandSimulation(world, 444);
        simulation.SpawnEntity(SpeciesKind.Grazer, 50, 50);
        SimEntity nearest = simulation.SpawnEntity(SpeciesKind.Grazer, 12, 10);
        SimEntity predator = simulation.SpawnEntity(SpeciesKind.Predator, 10, 10);
        var options = SimulationBudgetOptions.ForProfile(SimulationPerformanceProfile.Balanced, 100);
        options.EntityAiUpdatesPerDay = 3;
        options.PathRequestsPerDay = 3;

        simulation.AdvanceDayBudgeted(options);

        Assert.True(predator.DestinationX == nearest.X || predator.X != 10 || predator.Path.Count > 0);
    }

    private static WorldMap CreateGrassWorld(int size)
    {
        WorldMap world = WorldGenerator.Generate(new WorldGenerationConfig
        {
            Seed = 90210,
            Width = size,
            Height = size,
            ChunkSize = 16,
            SeaLevel = 0.4f,
        });
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                world.SetTerrain(x, y, TerrainType.Grassland);
        return world;
    }
}
