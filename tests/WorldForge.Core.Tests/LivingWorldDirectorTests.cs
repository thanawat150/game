using WorldForge.Core.Simulation;
using WorldForge.Core.World;
using Xunit;

namespace WorldForge.Core.Tests;

public sealed class LivingWorldDirectorTests
{
    [Fact]
    public void DirectorCreatesDailyLifeProfilesAndJobs()
    {
        (WorldMap world, GrandSimulation simulation, SettlementState city, KingdomState kingdom) = CreateCivilization();
        var director = new LivingWorldDirector(world, simulation, 1234, "Test World");

        director.EnsureWorldRecords();
        director.AdvanceVisualTime(9);

        Assert.Equal(simulation.State.Entities.Values.Count(e => e.Species == SpeciesKind.Settler), director.State.Citizens.Count);
        Assert.All(director.State.Citizens.Values, profile => Assert.NotEqual(0UL, profile.HouseholdId));
        Assert.Contains(director.State.Citizens.Values, profile => profile.Job != CitizenJob.Child);
        Assert.Equal("Test World", director.State.WorldName);
    }

    [Fact]
    public void CityPoliciesChangeFertilityAndCanConstruct()
    {
        (WorldMap world, GrandSimulation simulation, SettlementState city, KingdomState kingdom) = CreateCivilization();
        city.Wood = 200;
        city.Stone = 100;
        var director = new LivingWorldDirector(world, simulation, 2222);
        CityManagementPolicy policy = director.GetCityPolicy(city.Id);
        policy.Priority = CityPriority.Defense;
        policy.BirthPolicyMultiplier = 0.25f;
        director.State.Population.BirthMultiplier = 0.5f;

        director.BuildNow(city.Id);
        director.AdvanceDay();

        Assert.Contains(city.Buildings, building => building is "building.watchtower" or "building.barracks");
        Assert.All(simulation.State.Entities.Values.Where(e => e.SettlementId == city.Id), citizen => Assert.InRange(citizen.Fertility, 0f, 0.01f));
    }

    [Fact]
    public void WeatherAndSeasonsAdvance()
    {
        (WorldMap world, GrandSimulation simulation, SettlementState city, KingdomState kingdom) = CreateCivilization();
        var director = new LivingWorldDirector(world, simulation, 3333);
        director.State.WeatherDaysRemaining = 0;
        simulation.State.Day = 150;

        director.AdvanceDay();

        Assert.Equal(SeasonKind.Rainy, director.State.Season);
        Assert.InRange(director.State.WeatherDaysRemaining, 3, 11);
        Assert.InRange(director.State.TemperatureC, 10, 40);
    }

    [Fact]
    public void TradeMovesResourcesBetweenFriendlyCities()
    {
        WorldMap world = CreateGrassWorld(96);
        var simulation = new GrandSimulation(world, 4444);
        (SettlementState first, KingdomState firstKingdom) = CreateCity(simulation, 20, 20, "First");
        (SettlementState second, KingdomState secondKingdom) = CreateCity(simulation, 65, 65, "Second");
        simulation.SetRelation(firstKingdom.Id, secondKingdom.Id, 80);
        first.Food = 300;
        second.Food = 20;
        var director = new LivingWorldDirector(world, simulation, 4444);
        simulation.State.Day = 30;

        director.AdvanceDay();

        Assert.True(first.Food < 300);
        Assert.True(second.Food > 20);
        Assert.True(first.Gold > 0);
        Assert.NotEmpty(director.State.TrafficByTile);
    }

    [Fact]
    public void MigrationMovesFamiliesAwayFromFailingCity()
    {
        WorldMap world = CreateGrassWorld(96);
        var simulation = new GrandSimulation(world, 5555);
        (SettlementState source, KingdomState sourceKingdom) = CreateCity(simulation, 20, 20, "Source");
        (SettlementState destination, KingdomState destinationKingdom) = CreateCity(simulation, 70, 70, "Destination");
        source.Happiness = 5;
        source.Food = 0;
        destination.Happiness = 90;
        destination.Food = 400;
        var director = new LivingWorldDirector(world, simulation, 5555);
        simulation.State.Day = 30;
        int before = simulation.State.Entities.Values.Count(e => e.SettlementId == destination.Id);

        director.AdvanceDay();

        int after = simulation.State.Entities.Values.Count(e => e.SettlementId == destination.Id);
        Assert.True(after > before);
        Assert.True(director.State.TotalMigrants > 0);
    }

    [Fact]
    public void SpeciesCapsAreEnforced()
    {
        WorldMap world = CreateGrassWorld(64);
        var simulation = new GrandSimulation(world, 6666);
        for (int i = 0; i < 30; i++)
            simulation.SpawnEntity(SpeciesKind.Grazer, 5 + i, 10);
        var director = new LivingWorldDirector(world, simulation, 6666);
        director.SetSpeciesCap(SpeciesKind.Grazer, 12);

        director.AdvanceDay();

        Assert.Equal(12, simulation.State.Entities.Values.Count(e => e.IsAlive && e.Species == SpeciesKind.Grazer));
    }

    [Fact]
    public void WorldEventChoiceChangesCityAndChronicle()
    {
        (WorldMap world, GrandSimulation simulation, SettlementState city, KingdomState kingdom) = CreateCivilization();
        var director = new LivingWorldDirector(world, simulation, 7777);
        city.Food = 0;
        director.State.NextEventDay = 0;

        director.AdvanceDay();
        PendingWorldEvent? pending = director.State.PendingEvent;

        Assert.NotNull(pending);
        float before = city.Food;
        director.ResolvePendingEvent(0);

        Assert.Null(director.State.PendingEvent);
        Assert.True(city.Food >= before);
        Assert.Contains(simulation.State.Chronicle, e => e.Type == "world.event.resolved");
    }

    [Fact]
    public void ScenarioCanCompleteAllianceOfThree()
    {
        WorldMap world = CreateGrassWorld(128);
        var simulation = new GrandSimulation(world, 8888);
        (_, KingdomState first) = CreateCity(simulation, 20, 20, "A");
        (_, KingdomState second) = CreateCity(simulation, 60, 20, "B");
        (_, KingdomState third) = CreateCity(simulation, 100, 20, "C");
        simulation.SetRelation(first.Id, second.Id, 90);
        simulation.SetRelation(first.Id, third.Id, 90);
        simulation.SetRelation(second.Id, third.Id, 90);
        var director = new LivingWorldDirector(world, simulation, 8888);
        director.SelectScenario(ScenarioKind.AllianceOfThree);

        director.AdvanceDay();

        Assert.True(director.State.Scenario.Completed);
        Assert.Equal(1f, director.State.Scenario.Progress);
    }

    [Fact]
    public void LivingWorldStateRoundTrips()
    {
        (WorldMap world, GrandSimulation simulation, SettlementState city, KingdomState kingdom) = CreateCivilization();
        var director = new LivingWorldDirector(world, simulation, 9999, "Round Trip");
        director.State.Settings.EnableAudio = false;
        director.GetCityPolicy(city.Id).Priority = CityPriority.Knowledge;
        director.SelectScenario(ScenarioKind.BuildMetropolis);
        director.AdvanceVisualTime(5);

        string json = director.SaveToJson();
        LivingWorldDirector loaded = LivingWorldDirector.LoadFromJson(world, simulation, json);

        Assert.Equal("Round Trip", loaded.State.WorldName);
        Assert.False(loaded.State.Settings.EnableAudio);
        Assert.Equal(CityPriority.Knowledge, loaded.GetCityPolicy(city.Id).Priority);
        Assert.Equal(ScenarioKind.BuildMetropolis, loaded.State.Scenario.Scenario);
        Assert.NotEmpty(loaded.State.Citizens);
    }

    private static (WorldMap World, GrandSimulation Simulation, SettlementState City, KingdomState Kingdom) CreateCivilization()
    {
        WorldMap world = CreateGrassWorld(80);
        var simulation = new GrandSimulation(world, 123);
        (SettlementState city, KingdomState kingdom) = CreateCity(simulation, 40, 40, "Capital");
        return (world, simulation, city, kingdom);
    }

    private static (SettlementState City, KingdomState Kingdom) CreateCity(GrandSimulation simulation, int x, int y, string name)
    {
        var ids = new List<ulong>();
        for (int i = 0; i < 12; i++)
        {
            SimEntity citizen = simulation.SpawnEntity(SpeciesKind.Settler, x, y, $"{name}-{i}");
            citizen.AgeDays = (20 + i) * 360;
            ids.Add(citizen.Id);
        }
        SettlementState city = simulation.FoundSettlement(ids, name);
        KingdomState kingdom = simulation.FoundKingdom(city.Id, $"{name} Kingdom", GovernmentType.Council);
        return (city, kingdom);
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
