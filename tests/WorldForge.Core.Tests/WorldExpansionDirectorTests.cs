using WorldForge.Core.Simulation;
using WorldForge.Core.World;
using Xunit;

namespace WorldForge.Core.Tests;

public sealed class WorldExpansionDirectorTests
{
    [Fact]
    public void ExpansionInitializesRacesDistrictsLegendsRuinsAndHistory()
    {
        (WorldMap world, GrandSimulation simulation, LivingWorldDirector living, WorldExpansionDirector expansion, ulong kingdomId, _) = CreateExpansion(10101, 16);

        Assert.True(expansion.State.KingdomRaces.ContainsKey(kingdomId));
        Assert.NotEmpty(expansion.State.CityDistricts);
        Assert.Contains(expansion.State.CityDistricts.Values.SelectMany(d => d.Buildings), b => b.Kind == BuildingKind.House);
        Assert.NotEmpty(expansion.State.Legends);
        Assert.NotEmpty(expansion.State.Ruins);
        Assert.Single(expansion.State.History);
        Assert.All(simulation.State.Entities.Values.Where(e => e.Species == SpeciesKind.Settler), e => Assert.True(expansion.State.CitizenRaces.ContainsKey(e.Id)));
    }

    [Fact]
    public void PlannedBuildingCompletesAndChangesPhysicalCity()
    {
        (_, GrandSimulation simulation, LivingWorldDirector living, WorldExpansionDirector expansion, _, ulong cityId) = CreateExpansion(11111, 20);
        Assert.True(expansion.PlanBuilding(cityId, BuildingKind.Temple));

        AdvanceDays(simulation, living, expansion, 170);

        CityDistrictState district = expansion.State.CityDistricts[cityId];
        Assert.Contains(district.Buildings, b => b.Kind == BuildingKind.Temple && b.Status == BuildingStatus.Active);
        Assert.Contains("building.temple", simulation.State.Settlements[cityId].Buildings);
        Assert.NotEmpty(district.RoadTiles);
    }

    [Fact]
    public void FaithUnlocksAndMiracleChangesCityState()
    {
        (_, GrandSimulation simulation, LivingWorldDirector living, WorldExpansionDirector expansion, _, ulong cityId) = CreateExpansion(12121, 14);
        expansion.State.Faith.Faith = 100;
        expansion.State.Faith.Favor = 100;
        simulation.State.Settlements[cityId].Food = 5;

        AdvanceDays(simulation, living, expansion, 1);
        float before = simulation.State.Settlements[cityId].Food;
        bool used = expansion.UseMiracle(MiracleKind.BlessHarvest, cityId);

        Assert.True(used);
        Assert.True(simulation.State.Settlements[cityId].Food > before);
        Assert.Contains(MiracleKind.HealCity, expansion.State.Faith.UnlockedMiracles);
        Assert.Contains(simulation.State.Chronicle, e => e.Type == "faith.miracle");
    }

    [Fact]
    public void CoastalCityCreatesFleetWithWaterPathState()
    {
        (WorldMap world, GrandSimulation simulation, _, WorldExpansionDirector expansion, _, ulong cityId) = CreateExpansion(13131, 18);
        simulation.State.Settlements[cityId].Wood = 200;
        simulation.State.Settlements[cityId].Gold = 100;

        FleetState? fleet = expansion.CreateFleet(cityId, FleetMission.Explore);

        Assert.NotNull(fleet);
        Assert.True(fleet!.IsActive);
        Assert.True(world.GetTerrain(fleet.X, fleet.Y) is TerrainType.DeepOcean or TerrainType.ShallowWater);
        Assert.Contains(expansion.State.CityDistricts[cityId].Buildings, b => b.Kind == BuildingKind.Harbor);
    }

    [Fact]
    public void MageSpellUsesManaAndChangesWorld()
    {
        (WorldMap world, GrandSimulation simulation, _, WorldExpansionDirector expansion, ulong kingdomId, _) = CreateExpansion(14141, 12, 22);
        SimEntity mageEntity = simulation.State.Entities.Values.First(e => e.KingdomId == kingdomId);
        expansion.State.Mages[mageEntity.Id] = new MageProfile
        {
            EntityId = mageEntity.Id,
            School = MagicSchool.Nature,
            Mana = 100,
            KnownSpells = new HashSet<SpellKind> { SpellKind.Growth },
        };
        world.SetTerrain(mageEntity.X, mageEntity.Y, TerrainType.Grassland);

        bool cast = expansion.CastSpell(mageEntity.Id, SpellKind.Growth, mageEntity.X, mageEntity.Y);

        Assert.True(cast);
        Assert.Equal(TerrainType.Forest, world.GetTerrain(mageEntity.X, mageEntity.Y));
        Assert.True(expansion.State.Mages[mageEntity.Id].Mana < 100);
        Assert.Contains(simulation.State.Chronicle, e => e.Type == "magic.spell");
    }

    [Fact]
    public void ExpansionSaveRoundTripPreservesAdvancedState()
    {
        (WorldMap world, GrandSimulation simulation, LivingWorldDirector living, WorldExpansionDirector expansion, _, ulong cityId) = CreateExpansion(15151, 16);
        expansion.State.Faith.Faith = 123;
        expansion.State.Faith.Favor = 77;
        expansion.PlanBuilding(cityId, BuildingKind.MageTower);
        AdvanceDays(simulation, living, expansion, 40);
        float faithBeforeSave = expansion.State.Faith.Faith;

        string json = expansion.SaveToJson();
        WorldExpansionDirector loaded = WorldExpansionDirector.LoadFromJson(world, simulation, living, json);

        Assert.Equal(WorldExpansionState.CurrentSaveVersion, loaded.State.SaveVersion);
        Assert.Equal(expansion.State.Legends.Count, loaded.State.Legends.Count);
        Assert.Equal(expansion.State.CityDistricts.Count, loaded.State.CityDistricts.Count);
        Assert.Equal(expansion.State.Ruins.Count, loaded.State.Ruins.Count);
        Assert.Equal(expansion.State.History.Count, loaded.State.History.Count);
        Assert.Equal(faithBeforeSave, loaded.State.Faith.Faith);
        Assert.Equal(expansion.State.CityDistricts[cityId].Buildings.Count, loaded.State.CityDistricts[cityId].Buildings.Count);
    }

    [Fact]
    public void HistoryAchievementsCampaignAndReportAdvance()
    {
        (_, GrandSimulation simulation, LivingWorldDirector living, WorldExpansionDirector expansion, _, _) = CreateExpansion(16161, 24);

        AdvanceDays(simulation, living, expansion, 65);
        string report = expansion.GenerateHistoryReport();

        Assert.True(expansion.State.History.Count >= 3);
        Assert.True(expansion.State.Campaign.Chapter >= CampaignChapter.FirstLegend);
        Assert.True(expansion.State.Achievements["first_legend"].Unlocked);
        Assert.Contains("พงศาวดารโลก", report);
        Assert.Contains("ตำนานสำคัญ", report);
    }

    [Fact]
    public void ModPackRoundTripAppliesValidatedRules()
    {
        (WorldMap world, GrandSimulation simulation, LivingWorldDirector living, WorldExpansionDirector expansion, _, _) = CreateExpansion(17171, 10, 18, 48);
        expansion.State.ModRules.ConstructionSpeedMultiplier = 2.5f;
        expansion.State.ModRules.InitialRuinCount = 20;

        string json = expansion.ExportModPack();
        var other = new WorldExpansionDirector(world, simulation, living, 40404);
        other.ImportModPack(json);

        Assert.Equal(2.5f, other.State.ModRules.ConstructionSpeedMultiplier);
        Assert.Equal(20, other.State.ModRules.InitialRuinCount);
        Assert.True(other.State.ModRules.EnableMagic);
    }

    private static (WorldMap World, GrandSimulation Simulation, LivingWorldDirector Living, WorldExpansionDirector Expansion, ulong KingdomId, ulong CityId) CreateExpansion(
        long seed,
        int population,
        int x = 18,
        int size = 64)
    {
        WorldMap world = CreateCoastalWorld(size);
        var simulation = new GrandSimulation(world, seed);
        ulong kingdomId = CreateKingdom(simulation, x, size / 2, $"Realm{seed}", population);
        ulong cityId = simulation.State.Kingdoms[kingdomId].CapitalId;
        var living = new LivingWorldDirector(world, simulation, seed + 1000, $"World {seed}");
        var expansion = new WorldExpansionDirector(world, simulation, living, seed + 2000);
        return (world, simulation, living, expansion, kingdomId, cityId);
    }

    private static void AdvanceDays(GrandSimulation simulation, LivingWorldDirector living, WorldExpansionDirector expansion, int days)
    {
        var budget = SimulationBudgetOptions.ForProfile(SimulationPerformanceProfile.Economy, 800);
        budget.EnableAutomaticDiplomacy = false;
        budget.EnableArmies = false;
        budget.EnableReproduction = false;
        for (int i = 0; i < days; i++)
        {
            simulation.AdvanceDayBudgeted(budget);
            living.AdvanceDay();
            expansion.AdvanceDay();
        }
    }

    private static WorldMap CreateCoastalWorld(int size)
    {
        WorldMap world = WorldGenerator.Generate(new WorldGenerationConfig
        {
            Seed = 20260731,
            Width = size,
            Height = size,
            ChunkSize = 16,
            SeaLevel = 0.4f,
        });
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                world.SetTerrain(x, y, x < 16 ? TerrainType.ShallowWater : TerrainType.Grassland);
        return world;
    }

    private static ulong CreateKingdom(GrandSimulation simulation, int x, int y, string name, int population)
    {
        var settlers = new List<ulong>();
        for (int i = 0; i < population; i++)
        {
            SimEntity settler = simulation.SpawnEntity(SpeciesKind.Settler, x, y, $"{name}-{i}");
            settler.AgeDays = (18 + i % 30) * 360;
            settlers.Add(settler.Id);
        }
        SettlementState settlement = simulation.FoundSettlement(settlers, $"{name} City");
        settlement.Wood = 200;
        settlement.Stone = 150;
        settlement.Gold = 80;
        return simulation.FoundKingdom(settlement.Id, $"{name} Realm", GovernmentType.Council).Id;
    }
}
