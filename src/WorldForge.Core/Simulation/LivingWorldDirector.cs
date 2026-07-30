using System.Text.Json;
using WorldForge.Core.World;

namespace WorldForge.Core.Simulation;

public enum CitizenJob
{
    Child,
    Farmer,
    Woodcutter,
    Miner,
    Builder,
    Trader,
    Healer,
    Priest,
    Scholar,
    Guard,
    Soldier,
    Ruler,
}

public enum DailyActivity
{
    Sleeping,
    GoingToWork,
    Working,
    Eating,
    Socializing,
    ReturningHome,
    Patrolling,
    CaringForChild,
    Trading,
    Fleeing,
    Sheltering,
}

public enum SeasonKind { Hot, Rainy, Cool }
public enum WeatherKind { Clear, Cloudy, Rain, Storm, Fog, Drought, ColdSnap }
public enum ScenarioKind { Sandbox, Survive100Years, EcosystemBalance, StopPlague, AllianceOfThree, BuildMetropolis, RestoreAfterDisaster }
public enum WorldEventKind { Famine, Plague, GreatHarvest, Festival, Fire, Flood, Drought, MigrationWave, SuccessionCrisis, Rebellion, MonsterRaid, MarketBoom }
public enum CityPriority { Balanced, Food, Housing, Economy, Knowledge, Faith, Defense }
public enum BorderPolicy { Open, Controlled, Closed }
public enum LivingOverlayMode { None, Population, Food, Happiness, Disease, War, Kingdom, Trade, Migration, Weather, Performance }

public sealed class CitizenLifeProfile
{
    public ulong EntityId { get; set; }
    public ulong HouseholdId { get; set; }
    public CitizenJob Job { get; set; }
    public DailyActivity Activity { get; set; } = DailyActivity.Sleeping;
    public ulong? HomeSettlementId { get; set; }
    public ulong? PartnerId { get; set; }
    public int HomeX { get; set; }
    public int HomeY { get; set; }
    public int WorkX { get; set; }
    public int WorkY { get; set; }
    public int LastRoutineHour { get; set; } = -1;
    public int SocialNeed { get; set; }
    public int SafetyConcern { get; set; }
    public bool IsChild { get; set; }
}

public sealed class CityManagementPolicy
{
    public ulong SettlementId { get; set; }
    public CityPriority Priority { get; set; } = CityPriority.Balanced;
    public BorderPolicy BorderPolicy { get; set; } = BorderPolicy.Open;
    public float TaxRate { get; set; } = 0.12f;
    public float BirthPolicyMultiplier { get; set; } = 1f;
    public float FoodReserveTarget { get; set; } = 120f;
    public int PopulationLimit { get; set; } = 500;
    public bool AutoBuild { get; set; } = true;
    public bool Quarantine { get; set; }
    public bool Evacuate { get; set; }
    public int FestivalUntilDay { get; set; }
    public int LastConstructionDay { get; set; } = -10000;
}

public sealed class KingdomManagementPolicy
{
    public ulong KingdomId { get; set; }
    public float TaxModifier { get; set; } = 1f;
    public float MilitaryPriority { get; set; } = 0.5f;
    public float BirthPolicyMultiplier { get; set; } = 1f;
    public BorderPolicy BorderPolicy { get; set; } = BorderPolicy.Open;
    public int PopulationLimit { get; set; } = 2500;
    public bool PreferPeace { get; set; } = true;
}

public sealed class LivingPopulationPolicy
{
    public int GlobalPopulationLimit { get; set; } = 1200;
    public float BirthMultiplier { get; set; } = 1f;
    public float AgingMultiplier { get; set; } = 1f;
    public float MigrationMultiplier { get; set; } = 1f;
    public bool AutoBalanceEcosystem { get; set; } = true;
    public Dictionary<SpeciesKind, int> SpeciesCaps { get; set; } = new()
    {
        [SpeciesKind.Settler] = 800,
        [SpeciesKind.Grazer] = 260,
        [SpeciesKind.Predator] = 80,
        [SpeciesKind.Monster] = 24,
        [SpeciesKind.Fish] = 280,
    };
    public Dictionary<ulong, int> KingdomCaps { get; set; } = new();
}

public sealed class LivingWorldSettings
{
    public bool EnableDailyLife { get; set; } = true;
    public bool EnableJobs { get; set; } = true;
    public bool EnableWeather { get; set; } = true;
    public bool EnableEvents { get; set; } = true;
    public bool EnableTrade { get; set; } = true;
    public bool EnableMigration { get; set; } = true;
    public bool EnableCityAutomation { get; set; } = true;
    public bool EnableAmbientAnimation { get; set; } = true;
    public bool EnableAudio { get; set; } = true;
    public bool AutoPerformance { get; set; } = true;
}

public sealed class ScenarioProgress
{
    public ScenarioKind Scenario { get; set; } = ScenarioKind.Sandbox;
    public string Title { get; set; } = "Sandbox";
    public string Description { get; set; } = "Create and observe a living world.";
    public float Progress { get; set; }
    public bool Completed { get; set; }
    public bool Failed { get; set; }
    public int StartedDay { get; set; }
    public int TargetValue { get; set; }
}

public sealed class PendingWorldEvent
{
    public long Id { get; set; }
    public WorldEventKind Kind { get; set; }
    public string Title { get; set; } = "World event";
    public string Description { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int CreatedDay { get; set; }
    public ulong? SettlementId { get; set; }
    public ulong? KingdomId { get; set; }
    public List<string> Choices { get; set; } = new();
}

public sealed record PopulationHistoryPoint(
    int Day,
    int Settlers,
    int Grazers,
    int Predators,
    int Monsters,
    int Fish,
    int Births,
    int Deaths,
    int Migrants);

public sealed class LivingWorldState
{
    public const int CurrentSaveVersion = 1;
    public int SaveVersion { get; set; } = CurrentSaveVersion;
    public long Seed { get; set; }
    public string WorldName { get; set; } = "Living World";
    public float WorldHour { get; set; } = 7f;
    public SeasonKind Season { get; set; } = SeasonKind.Hot;
    public WeatherKind Weather { get; set; } = WeatherKind.Clear;
    public int WeatherDaysRemaining { get; set; } = 4;
    public float TemperatureC { get; set; } = 30f;
    public float RainIntensity { get; set; }
    public int NextEventDay { get; set; } = 25;
    public long NextEventId { get; set; } = 1;
    public int LastMigrationDay { get; set; } = -10000;
    public int LastTradeDay { get; set; } = -10000;
    public int TotalMigrants { get; set; }
    public int TotalEventChoices { get; set; }
    public LivingWorldSettings Settings { get; set; } = new();
    public LivingPopulationPolicy Population { get; set; } = new();
    public ScenarioProgress Scenario { get; set; } = new();
    public PendingWorldEvent? PendingEvent { get; set; }
    public Dictionary<ulong, CitizenLifeProfile> Citizens { get; set; } = new();
    public Dictionary<ulong, CityManagementPolicy> Cities { get; set; } = new();
    public Dictionary<ulong, KingdomManagementPolicy> Kingdoms { get; set; } = new();
    public Dictionary<int, int> TrafficByTile { get; set; } = new();
    public List<PopulationHistoryPoint> PopulationHistory { get; set; } = new();
    public List<string> TutorialFlags { get; set; } = new();
}

public sealed class LivingWorldDirector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly WorldMap _world;
    private readonly GrandSimulation _simulation;
    private readonly DeterministicRandom _random;
    private int _lastVisualHour = -1;
    private int _birthsAtLastHistory;
    private int _entityCountAtLastHistory;

    public LivingWorldDirector(WorldMap world, GrandSimulation simulation, long seed, string? worldName = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        _random = new DeterministicRandom(seed ^ 0x4C4956494E47L);
        State = new LivingWorldState
        {
            Seed = seed,
            WorldName = string.IsNullOrWhiteSpace(worldName) ? $"World {Math.Abs(seed % 10000):0000}" : worldName.Trim(),
        };
        EnsureWorldRecords();
        SelectScenario(ScenarioKind.Sandbox);
        _birthsAtLastHistory = (int)Math.Min(int.MaxValue, _simulation.State.TotalBirths);
        _entityCountAtLastHistory = _simulation.State.Entities.Count;
    }

    private LivingWorldDirector(WorldMap world, GrandSimulation simulation, LivingWorldState state)
    {
        _world = world;
        _simulation = simulation;
        State = state;
        _random = new DeterministicRandom(state.Seed ^ simulation.State.Tick ^ 0x4C4956494E47L);
        EnsureWorldRecords();
        _birthsAtLastHistory = (int)Math.Min(int.MaxValue, simulation.State.TotalBirths);
        _entityCountAtLastHistory = simulation.State.Entities.Count;
    }

    public LivingWorldState State { get; }

    public void EnsureWorldRecords()
    {
        foreach (SettlementState settlement in _simulation.State.Settlements.Values)
        {
            if (!State.Cities.ContainsKey(settlement.Id))
                State.Cities[settlement.Id] = new CityManagementPolicy { SettlementId = settlement.Id };
        }
        foreach (KingdomState kingdom in _simulation.State.Kingdoms.Values)
        {
            if (!State.Kingdoms.ContainsKey(kingdom.Id))
                State.Kingdoms[kingdom.Id] = new KingdomManagementPolicy { KingdomId = kingdom.Id };
            if (!State.Population.KingdomCaps.ContainsKey(kingdom.Id))
                State.Population.KingdomCaps[kingdom.Id] = 2500;
        }
        foreach (SimEntity entity in _simulation.State.Entities.Values.Where(e => e.IsAlive && e.Species == SpeciesKind.Settler))
            EnsureCitizen(entity);

        foreach (ulong id in State.Citizens.Keys.Where(id => !_simulation.State.Entities.ContainsKey(id)).ToArray())
            State.Citizens.Remove(id);
        foreach (ulong id in State.Cities.Keys.Where(id => !_simulation.State.Settlements.ContainsKey(id)).ToArray())
            State.Cities.Remove(id);
        foreach (ulong id in State.Kingdoms.Keys.Where(id => !_simulation.State.Kingdoms.ContainsKey(id)).ToArray())
            State.Kingdoms.Remove(id);
    }

    public CitizenLifeProfile EnsureCitizen(SimEntity entity)
    {
        if (State.Citizens.TryGetValue(entity.Id, out CitizenLifeProfile? existing))
        {
            existing.PartnerId = entity.MateId;
            existing.IsChild = entity.AgeDays < 16 * 360;
            return existing;
        }

        SettlementState? home = entity.SettlementId is ulong sid
            ? _simulation.State.Settlements.GetValueOrDefault(sid)
            : null;
        ulong household = entity.Parents.Count > 0
            ? entity.Parents.Min()
            : entity.SettlementId.GetValueOrDefault() * 100000UL + entity.Id / 4UL;
        CitizenJob job = ChooseJob(entity, home);
        (int hx, int hy) = home is null ? (entity.X, entity.Y) : OffsetAround(home.X, home.Y, entity.Id, 3, 7);
        (int wx, int wy) = WorkLocation(job, home, entity.Id, hx, hy);
        var profile = new CitizenLifeProfile
        {
            EntityId = entity.Id,
            HouseholdId = household,
            Job = entity.AgeDays < 16 * 360 ? CitizenJob.Child : job,
            Activity = DailyActivity.Sleeping,
            HomeSettlementId = entity.SettlementId,
            PartnerId = entity.MateId,
            HomeX = hx,
            HomeY = hy,
            WorkX = wx,
            WorkY = wy,
            IsChild = entity.AgeDays < 16 * 360,
        };
        State.Citizens[entity.Id] = profile;
        return profile;
    }

    public void AdvanceVisualTime(float hours)
    {
        if (!State.Settings.EnableDailyLife || hours <= 0)
            return;
        State.WorldHour = (State.WorldHour + hours) % 24f;
        int hour = (int)MathF.Floor(State.WorldHour);
        if (hour == _lastVisualHour)
            return;
        _lastVisualHour = hour;
        EnsureWorldRecords();
        ApplyHourlyRoutines(hour);
    }

    public void AdvanceDay()
    {
        EnsureWorldRecords();
        UpdateSeasonAndWeather();
        ApplyWeatherEffects();
        if (State.Settings.EnableCityAutomation)
            UpdateCityAutomation();
        if (State.Settings.EnableTrade && _simulation.State.Day - State.LastTradeDay >= 30)
            UpdateTrade();
        if (State.Settings.EnableMigration && _simulation.State.Day - State.LastMigrationDay >= 30)
            UpdateMigration();
        EnforcePopulationPolicies();
        if (State.Settings.EnableEvents)
            UpdateWorldEvents();
        EvaluateScenario();
        RecordPopulationHistory();
        DecayTraffic();
    }

    public void SelectScenario(ScenarioKind scenario)
    {
        State.Scenario = scenario switch
        {
            ScenarioKind.Survive100Years => new ScenarioProgress
            {
                Scenario = scenario,
                Title = "Century of Survival",
                Description = "Keep at least one kingdom and one settlement alive for 100 years.",
                StartedDay = _simulation.State.Day,
                TargetValue = 100,
            },
            ScenarioKind.EcosystemBalance => new ScenarioProgress
            {
                Scenario = scenario,
                Title = "Balanced Ecosystem",
                Description = "Maintain a healthy grazer-to-predator ratio for five years.",
                StartedDay = _simulation.State.Day,
                TargetValue = 5,
            },
            ScenarioKind.StopPlague => new ScenarioProgress
            {
                Scenario = scenario,
                Title = "Stop the Plague",
                Description = "End every active disease before half the settlers are lost.",
                StartedDay = _simulation.State.Day,
                TargetValue = 1,
            },
            ScenarioKind.AllianceOfThree => new ScenarioProgress
            {
                Scenario = scenario,
                Title = "Alliance of Three",
                Description = "Create an alliance among at least three kingdoms.",
                StartedDay = _simulation.State.Day,
                TargetValue = 3,
            },
            ScenarioKind.BuildMetropolis => new ScenarioProgress
            {
                Scenario = scenario,
                Title = "Build a Metropolis",
                Description = "Develop a settlement into a city with at least 100 citizens.",
                StartedDay = _simulation.State.Day,
                TargetValue = 100,
            },
            ScenarioKind.RestoreAfterDisaster => new ScenarioProgress
            {
                Scenario = scenario,
                Title = "Restore the World",
                Description = "Recover food, happiness and population after a major disaster.",
                StartedDay = _simulation.State.Day,
                TargetValue = 75,
            },
            _ => new ScenarioProgress
            {
                Scenario = ScenarioKind.Sandbox,
                Title = "Sandbox",
                Description = "Create, observe and manage a living world.",
                StartedDay = _simulation.State.Day,
            },
        };
    }

    public void ResolvePendingEvent(int choiceIndex)
    {
        PendingWorldEvent? worldEvent = State.PendingEvent;
        if (worldEvent is null)
            return;
        choiceIndex = Math.Clamp(choiceIndex, 0, Math.Max(0, worldEvent.Choices.Count - 1));
        SettlementState? city = worldEvent.SettlementId is ulong sid
            ? _simulation.State.Settlements.GetValueOrDefault(sid)
            : null;
        KingdomState? kingdom = worldEvent.KingdomId is ulong kid
            ? _simulation.State.Kingdoms.GetValueOrDefault(kid)
            : null;

        switch (worldEvent.Kind)
        {
            case WorldEventKind.Famine:
                if (choiceIndex == 0 && city is not null) { city.Food += 90; city.Gold = MathF.Max(0, city.Gold - 20); }
                else if (choiceIndex == 1 && city is not null) { city.Happiness = MathF.Max(0, city.Happiness - 12); SetCityEvacuation(city.Id, true); }
                else if (city is not null) city.Happiness = MathF.Max(0, city.Happiness - 24);
                break;
            case WorldEventKind.Plague:
                if (city is not null && State.Cities.TryGetValue(city.Id, out CityManagementPolicy? plaguePolicy))
                {
                    if (choiceIndex == 0) { plaguePolicy.Quarantine = true; city.Happiness -= 5; }
                    else if (choiceIndex == 1) { city.Gold = MathF.Max(0, city.Gold - 30); HealCity(city.Id, 24); }
                    else city.Happiness = MathF.Max(0, city.Happiness - 15);
                }
                break;
            case WorldEventKind.GreatHarvest:
                if (city is not null)
                {
                    if (choiceIndex == 0) city.Food += 120;
                    else if (choiceIndex == 1) { city.Food += 55; city.Gold += 40; }
                    else { city.Food += 30; city.Happiness = MathF.Min(100, city.Happiness + 12); }
                }
                break;
            case WorldEventKind.Festival:
                if (city is not null && State.Cities.TryGetValue(city.Id, out CityManagementPolicy? festivalPolicy))
                {
                    festivalPolicy.FestivalUntilDay = _simulation.State.Day + 12;
                    city.Happiness = MathF.Min(100, city.Happiness + (choiceIndex == 0 ? 18 : 10));
                    city.Gold = MathF.Max(0, city.Gold - (choiceIndex == 0 ? 24 : 8));
                }
                break;
            case WorldEventKind.Fire:
            case WorldEventKind.Flood:
            case WorldEventKind.Drought:
                if (city is not null)
                {
                    if (choiceIndex == 0) { city.Wood = MathF.Max(0, city.Wood - 18); city.Happiness -= 3; }
                    else if (choiceIndex == 1) { city.Gold = MathF.Max(0, city.Gold - 20); city.Happiness += 2; }
                    else { city.Food = MathF.Max(0, city.Food - 45); city.Happiness -= 14; }
                }
                break;
            case WorldEventKind.MigrationWave:
                if (city is not null)
                {
                    if (choiceIndex == 0) SpawnMigrants(city, 8);
                    else if (choiceIndex == 1) { SpawnMigrants(city, 3); city.Gold += 12; }
                    else city.Happiness = MathF.Max(0, city.Happiness - 4);
                }
                break;
            case WorldEventKind.SuccessionCrisis:
            case WorldEventKind.Rebellion:
                if (kingdom is not null)
                {
                    if (choiceIndex == 0) { kingdom.Stability = MathF.Min(100, kingdom.Stability + 12); kingdom.Economy = MathF.Max(0, kingdom.Economy - 10); }
                    else if (choiceIndex == 1) kingdom.Stability = MathF.Max(0, kingdom.Stability - 8);
                    else { kingdom.Stability = MathF.Max(0, kingdom.Stability - 18); kingdom.ArmyStrength *= 0.9f; }
                }
                break;
            case WorldEventKind.MonsterRaid:
                if (city is not null)
                {
                    if (choiceIndex == 0) city.Fortification += 2;
                    else if (choiceIndex == 1) _simulation.ApplyPower(GodPowerType.Lightning, worldEvent.X, worldEvent.Y, 4);
                    else city.Happiness = MathF.Max(0, city.Happiness - 20);
                }
                break;
            case WorldEventKind.MarketBoom:
                if (city is not null)
                {
                    if (choiceIndex == 0) city.Gold += 80;
                    else if (choiceIndex == 1) { city.Gold += 35; city.Technology += 4; }
                    else city.Happiness += 8;
                }
                break;
        }

        State.TotalEventChoices++;
        string chosen = worldEvent.Choices.Count > choiceIndex ? worldEvent.Choices[choiceIndex] : $"Choice {choiceIndex + 1}";
        AddChronicle("world.event.resolved", $"{worldEvent.Title} resolved", chosen, worldEvent.X, worldEvent.Y, 2);
        State.PendingEvent = null;
        State.NextEventDay = _simulation.State.Day + 28 + (int)(_random.NextUInt() % 38);
    }

    public void RenameWorld(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
            State.WorldName = name.Trim();
    }

    public bool RenameSettlement(ulong settlementId, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !_simulation.State.Settlements.TryGetValue(settlementId, out SettlementState? settlement))
            return false;
        settlement.Name = name.Trim();
        return true;
    }

    public bool RenameKingdom(ulong kingdomId, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !_simulation.State.Kingdoms.TryGetValue(kingdomId, out KingdomState? kingdom))
            return false;
        kingdom.Name = name.Trim();
        return true;
    }

    public void SetSpeciesCap(SpeciesKind species, int cap) => State.Population.SpeciesCaps[species] = Math.Clamp(cap, 0, 6000);
    public void SetKingdomCap(ulong kingdomId, int cap) => State.Population.KingdomCaps[kingdomId] = Math.Clamp(cap, 0, 6000);

    public CityManagementPolicy GetCityPolicy(ulong settlementId)
    {
        if (!State.Cities.TryGetValue(settlementId, out CityManagementPolicy? policy))
        {
            policy = new CityManagementPolicy { SettlementId = settlementId };
            State.Cities[settlementId] = policy;
        }
        return policy;
    }

    public KingdomManagementPolicy GetKingdomPolicy(ulong kingdomId)
    {
        if (!State.Kingdoms.TryGetValue(kingdomId, out KingdomManagementPolicy? policy))
        {
            policy = new KingdomManagementPolicy { KingdomId = kingdomId };
            State.Kingdoms[kingdomId] = policy;
        }
        return policy;
    }

    public void TriggerFestival(ulong settlementId)
    {
        if (!_simulation.State.Settlements.TryGetValue(settlementId, out SettlementState? city))
            return;
        CityManagementPolicy policy = GetCityPolicy(settlementId);
        policy.FestivalUntilDay = _simulation.State.Day + 10;
        city.Happiness = MathF.Min(100, city.Happiness + 12);
        city.Gold = MathF.Max(0, city.Gold - 12);
    }

    public void SetCityEvacuation(ulong settlementId, bool enabled)
    {
        GetCityPolicy(settlementId).Evacuate = enabled;
    }

    public void BuildNow(ulong settlementId)
    {
        if (!_simulation.State.Settlements.TryGetValue(settlementId, out SettlementState? city))
            return;
        CityManagementPolicy policy = GetCityPolicy(settlementId);
        TryConstruct(city, policy, force: true);
    }

    public void HealCity(ulong settlementId, float amount)
    {
        foreach (SimEntity entity in _simulation.State.Entities.Values.Where(e => e.IsAlive && e.SettlementId == settlementId))
            entity.Health = MathF.Min(100 * entity.VitalityGene, entity.Health + amount);
        foreach (DiseaseState disease in _simulation.State.Diseases)
            foreach (ulong id in disease.InfectedDays.Keys.Where(id => _simulation.State.Entities.GetValueOrDefault(id)?.SettlementId == settlementId).ToArray())
                disease.InfectedDays.Remove(id);
    }

    public string SaveToJson() => JsonSerializer.Serialize(State, JsonOptions);

    public static LivingWorldDirector LoadFromJson(WorldMap world, GrandSimulation simulation, string json)
    {
        LivingWorldState state = JsonSerializer.Deserialize<LivingWorldState>(json, JsonOptions)
            ?? throw new InvalidDataException("Living-world save is empty.");
        if (state.SaveVersion != LivingWorldState.CurrentSaveVersion)
            throw new InvalidDataException($"Unsupported living-world save version {state.SaveVersion}.");
        return new LivingWorldDirector(world, simulation, state);
    }

    private void ApplyHourlyRoutines(int hour)
    {
        bool danger = _simulation.State.Armies.Values.Any(a => a.IsActive && a.Status is ArmyStatus.Besieging or ArmyStatus.Marching);
        foreach (SimEntity entity in _simulation.State.Entities.Values.Where(e => e.IsAlive && e.Species == SpeciesKind.Settler).OrderBy(e => e.Id))
        {
            CitizenLifeProfile profile = EnsureCitizen(entity);
            if (profile.LastRoutineHour == hour)
                continue;
            profile.LastRoutineHour = hour;
            profile.PartnerId = entity.MateId;
            profile.IsChild = entity.AgeDays < 16 * 360;
            if (profile.IsChild)
                profile.Job = CitizenJob.Child;

            CityManagementPolicy? cityPolicy = profile.HomeSettlementId is ulong sid ? GetCityPolicy(sid) : null;
            bool shelter = danger && entity.SettlementId is not null;
            if (cityPolicy?.Evacuate == true)
            {
                profile.Activity = DailyActivity.Fleeing;
                entity.Action = EntityAction.Flee;
                MoveAwayFromSettlement(entity, profile);
                continue;
            }
            if (shelter || cityPolicy?.Quarantine == true)
            {
                profile.Activity = DailyActivity.Sheltering;
                entity.Action = EntityAction.Defend;
                MoveEntityToward(entity, profile.HomeX, profile.HomeY);
                continue;
            }

            DailyActivity activity = hour switch
            {
                < 6 => DailyActivity.Sleeping,
                < 8 => DailyActivity.GoingToWork,
                < 16 => profile.IsChild ? DailyActivity.Socializing : DailyActivity.Working,
                < 18 => DailyActivity.Socializing,
                < 21 => DailyActivity.ReturningHome,
                _ => DailyActivity.Sleeping,
            };
            if (profile.IsChild && entity.Parents.Any(id => _simulation.State.Entities.ContainsKey(id)) && hour is >= 8 and < 16)
                activity = DailyActivity.CaringForChild;
            profile.Activity = activity;

            switch (activity)
            {
                case DailyActivity.Sleeping:
                case DailyActivity.ReturningHome:
                case DailyActivity.CaringForChild:
                    entity.Action = EntityAction.Idle;
                    MoveEntityToward(entity, profile.HomeX, profile.HomeY);
                    break;
                case DailyActivity.GoingToWork:
                    entity.Action = EntityAction.Travel;
                    MoveEntityToward(entity, profile.WorkX, profile.WorkY);
                    break;
                case DailyActivity.Working:
                    entity.Action = ActionForJob(profile.Job);
                    MoveEntityToward(entity, profile.WorkX, profile.WorkY);
                    ApplyJobOutput(entity, profile);
                    break;
                case DailyActivity.Socializing:
                    entity.Action = EntityAction.Trade;
                    if (profile.HomeSettlementId is ulong homeId && _simulation.State.Settlements.TryGetValue(homeId, out SettlementState? home))
                        MoveEntityToward(entity, home.X, home.Y);
                    profile.SocialNeed = Math.Max(0, profile.SocialNeed - 3);
                    break;
            }
        }
    }

    private void ApplyJobOutput(SimEntity entity, CitizenLifeProfile profile)
    {
        if (profile.HomeSettlementId is not ulong sid || !_simulation.State.Settlements.TryGetValue(sid, out SettlementState? city))
            return;
        float productivity = Math.Clamp(0.5f + entity.Energy / 100f + entity.Intelligence / 80f, 0.5f, 2.2f);
        switch (profile.Job)
        {
            case CitizenJob.Farmer: city.Food += 0.35f * productivity; break;
            case CitizenJob.Woodcutter: city.Wood += 0.2f * productivity; break;
            case CitizenJob.Miner: city.Stone += 0.16f * productivity; city.Gold += 0.04f * productivity; break;
            case CitizenJob.Builder: city.Wood += 0.03f; city.Technology += 0.01f * productivity; break;
            case CitizenJob.Trader: city.Gold += 0.12f * productivity; break;
            case CitizenJob.Healer: city.Happiness = MathF.Min(100, city.Happiness + 0.02f * productivity); break;
            case CitizenJob.Priest: city.Happiness = MathF.Min(100, city.Happiness + 0.015f * productivity); break;
            case CitizenJob.Scholar: city.Technology += 0.03f * productivity; break;
            case CitizenJob.Guard:
            case CitizenJob.Soldier: city.Fortification = Math.Min(100, city.Fortification + (State.WorldHour % 6 < 1 ? 1 : 0)); break;
        }
    }

    private void UpdateSeasonAndWeather()
    {
        int yearDay = Math.Abs(_simulation.State.Day % 360);
        State.Season = yearDay switch
        {
            < 120 => SeasonKind.Hot,
            < 270 => SeasonKind.Rainy,
            _ => SeasonKind.Cool,
        };
        if (!State.Settings.EnableWeather)
        {
            State.Weather = WeatherKind.Clear;
            State.RainIntensity = 0;
            return;
        }
        State.WeatherDaysRemaining--;
        if (State.WeatherDaysRemaining > 0)
            return;

        float roll = _random.NextFloat();
        State.Weather = State.Season switch
        {
            SeasonKind.Rainy => roll switch
            {
                < 0.42f => WeatherKind.Rain,
                < 0.56f => WeatherKind.Storm,
                < 0.72f => WeatherKind.Cloudy,
                < 0.84f => WeatherKind.Fog,
                _ => WeatherKind.Clear,
            },
            SeasonKind.Cool => roll switch
            {
                < 0.18f => WeatherKind.ColdSnap,
                < 0.38f => WeatherKind.Fog,
                < 0.58f => WeatherKind.Cloudy,
                < 0.68f => WeatherKind.Rain,
                _ => WeatherKind.Clear,
            },
            _ => roll switch
            {
                < 0.18f => WeatherKind.Drought,
                < 0.34f => WeatherKind.Cloudy,
                < 0.43f => WeatherKind.Rain,
                _ => WeatherKind.Clear,
            },
        };
        State.WeatherDaysRemaining = 3 + (int)(_random.NextUInt() % 9);
        State.RainIntensity = State.Weather switch
        {
            WeatherKind.Rain => 0.65f,
            WeatherKind.Storm => 1f,
            _ => 0f,
        };
        State.TemperatureC = State.Season switch
        {
            SeasonKind.Hot => State.Weather == WeatherKind.Rain ? 28 : 35,
            SeasonKind.Rainy => 27,
            _ => State.Weather == WeatherKind.ColdSnap ? 13 : 22,
        };
    }

    private void ApplyWeatherEffects()
    {
        foreach (SettlementState city in _simulation.State.Settlements.Values)
        {
            switch (State.Weather)
            {
                case WeatherKind.Rain: city.Food += 0.8f; break;
                case WeatherKind.Storm: city.Food = MathF.Max(0, city.Food - 0.8f); city.Happiness -= 0.08f; break;
                case WeatherKind.Drought: city.Food = MathF.Max(0, city.Food - 1.4f); city.Happiness -= 0.12f; break;
                case WeatherKind.ColdSnap: city.Food = MathF.Max(0, city.Food - 0.5f); break;
                case WeatherKind.Fog: city.Happiness += 0.01f; break;
            }
            city.Happiness = Math.Clamp(city.Happiness, 0, 100);
        }
    }

    private void UpdateCityAutomation()
    {
        foreach (SettlementState city in _simulation.State.Settlements.Values.OrderBy(c => c.Id))
        {
            CityManagementPolicy policy = GetCityPolicy(city.Id);
            int population = _simulation.State.Entities.Values.Count(e => e.IsAlive && e.SettlementId == city.Id);
            float kingdomBirth = city.KingdomId is ulong kingdomId ? GetKingdomPolicy(kingdomId).BirthPolicyMultiplier : 1f;
            float fertility = Math.Clamp(0.025f * State.Population.BirthMultiplier * policy.BirthPolicyMultiplier * kingdomBirth, 0f, 0.2f);
            foreach (SimEntity citizen in _simulation.State.Entities.Values.Where(e => e.IsAlive && e.Species == SpeciesKind.Settler && e.SettlementId == city.Id))
                citizen.Fertility = fertility;
            city.Gold += MathF.Max(0, population * policy.TaxRate * 0.015f);
            city.Happiness = Math.Clamp(city.Happiness - policy.TaxRate * 0.015f, 0, 100);
            if (policy.FestivalUntilDay >= _simulation.State.Day)
                city.Happiness = MathF.Min(100, city.Happiness + 0.18f);
            if (policy.AutoBuild && _simulation.State.Day - policy.LastConstructionDay >= 30)
                TryConstruct(city, policy, force: false);
        }
    }

    private void TryConstruct(SettlementState city, CityManagementPolicy policy, bool force)
    {
        int population = _simulation.State.Entities.Values.Count(e => e.IsAlive && e.SettlementId == city.Id);
        string building = policy.Priority switch
        {
            CityPriority.Food => "building.farm",
            CityPriority.Housing => "building.house",
            CityPriority.Economy => "building.market",
            CityPriority.Knowledge => "building.school",
            CityPriority.Faith => "building.temple",
            CityPriority.Defense => city.Fortification < 12 ? "building.watchtower" : "building.barracks",
            _ => population >= city.Housing - 2 ? "building.house" : city.Food < policy.FoodReserveTarget ? "building.farm" : "building.workshop",
        };
        if (city.Buildings.Contains(building) && !force)
            return;
        float woodCost = building switch
        {
            "building.house" => 10,
            "building.farm" => 8,
            "building.market" => 12,
            "building.school" => 15,
            "building.temple" => 14,
            "building.watchtower" => 16,
            "building.barracks" => 22,
            _ => 12,
        };
        float stoneCost = building is "building.watchtower" or "building.barracks" ? 8 : building is "building.school" or "building.temple" ? 4 : 0;
        if (!force && (city.Wood < woodCost || city.Stone < stoneCost))
            return;
        city.Wood = MathF.Max(0, city.Wood - woodCost);
        city.Stone = MathF.Max(0, city.Stone - stoneCost);
        city.Buildings.Add(building);
        if (building == "building.house") city.Housing += 10;
        if (building == "building.farm") city.Food += 30;
        if (building is "building.watchtower" or "building.barracks") city.Fortification += 4;
        if (building == "building.school") city.Technology += 5;
        policy.LastConstructionDay = _simulation.State.Day;
    }

    private void UpdateTrade()
    {
        State.LastTradeDay = _simulation.State.Day;
        SettlementState[] cities = _simulation.State.Settlements.Values.OrderBy(c => c.Id).ToArray();
        for (int i = 0; i < cities.Length; i++)
        {
            SettlementState first = cities[i];
            SettlementState? second = cities
                .Where(c => c.Id != first.Id && CanTrade(first, c))
                .OrderBy(c => DistanceSquared(first.X, first.Y, c.X, c.Y))
                .ThenBy(c => c.Id)
                .FirstOrDefault();
            if (second is null)
                continue;
            float foodTransfer = Math.Clamp((first.Food - second.Food) * 0.1f, -18, 18);
            first.Food -= foodTransfer;
            second.Food += foodTransfer;
            first.Gold += 3;
            second.Gold += 3;
            int midpoint = _world.ToIndex((first.X + second.X) / 2, (first.Y + second.Y) / 2);
            State.TrafficByTile[midpoint] = State.TrafficByTile.GetValueOrDefault(midpoint) + 16;
        }
    }

    private bool CanTrade(SettlementState first, SettlementState second)
    {
        if (first.KingdomId is null || second.KingdomId is null)
            return true;
        if (first.KingdomId == second.KingdomId)
            return true;
        if (!_simulation.State.Kingdoms.TryGetValue(first.KingdomId.Value, out KingdomState? kingdom))
            return false;
        return kingdom.Relations.GetValueOrDefault(second.KingdomId.Value) >= 25;
    }

    private void UpdateMigration()
    {
        State.LastMigrationDay = _simulation.State.Day;
        foreach (SettlementState source in _simulation.State.Settlements.Values.OrderBy(c => c.Happiness).ThenBy(c => c.Id).ToArray())
        {
            CityManagementPolicy sourcePolicy = GetCityPolicy(source.Id);
            int population = _simulation.State.Entities.Values.Count(e => e.IsAlive && e.SettlementId == source.Id);
            bool pressure = sourcePolicy.Evacuate || source.Happiness < 30 || source.Food < population * 0.6f || population > sourcePolicy.PopulationLimit;
            if (!pressure)
                continue;
            SettlementState? destination = _simulation.State.Settlements.Values
                .Where(c => c.Id != source.Id && GetCityPolicy(c.Id).BorderPolicy != BorderPolicy.Closed)
                .OrderByDescending(c => c.Happiness + c.Food / 20f - DistanceSquared(source.X, source.Y, c.X, c.Y) / 2000f)
                .ThenBy(c => c.Id)
                .FirstOrDefault();
            if (destination is null)
                continue;
            SimEntity[] candidates = _simulation.State.Entities.Values
                .Where(e => e.IsAlive && e.Species == SpeciesKind.Settler && e.SettlementId == source.Id)
                .OrderBy(e => e.Id)
                .Take(Math.Max(1, (int)(4 * State.Population.MigrationMultiplier)))
                .ToArray();
            foreach (SimEntity migrant in candidates)
            {
                migrant.SettlementId = destination.Id;
                migrant.KingdomId = destination.KingdomId;
                CitizenLifeProfile profile = EnsureCitizen(migrant);
                profile.HomeSettlementId = destination.Id;
                (profile.HomeX, profile.HomeY) = OffsetAround(destination.X, destination.Y, migrant.Id, 3, 7);
                (profile.WorkX, profile.WorkY) = WorkLocation(profile.Job, destination, migrant.Id, profile.HomeX, profile.HomeY);
                migrant.Action = EntityAction.Migrate;
                State.TotalMigrants++;
            }
        }
    }

    private void EnforcePopulationPolicies()
    {
        State.Population.GlobalPopulationLimit = Math.Clamp(State.Population.GlobalPopulationLimit, 25, 6000);
        foreach ((SpeciesKind species, int cap) in State.Population.SpeciesCaps.ToArray())
        {
            SimEntity[] entities = _simulation.State.Entities.Values
                .Where(e => e.IsAlive && e.Species == species)
                .OrderBy(e => e.Health)
                .ThenByDescending(e => e.AgeDays)
                .ThenBy(e => e.Id)
                .ToArray();
            int overflow = Math.Max(0, entities.Length - Math.Max(0, cap));
            for (int i = 0; i < overflow; i++)
                RemoveForPopulationPolicy(entities[i], $"Species cap for {species}");
        }

        foreach ((ulong kingdomId, int cap) in State.Population.KingdomCaps.ToArray())
        {
            SimEntity[] citizens = _simulation.State.Entities.Values
                .Where(e => e.IsAlive && e.Species == SpeciesKind.Settler && e.KingdomId == kingdomId)
                .OrderBy(e => e.Health)
                .ThenByDescending(e => e.AgeDays)
                .ThenBy(e => e.Id)
                .ToArray();
            int overflow = Math.Max(0, citizens.Length - Math.Max(0, cap));
            for (int i = 0; i < overflow; i++)
                RemoveForPopulationPolicy(citizens[i], $"Kingdom cap for {kingdomId}");
        }
    }

    private void RemoveForPopulationPolicy(SimEntity entity, string reason)
    {
        if (!_simulation.State.Entities.Remove(entity.Id))
            return;
        foreach (DiseaseState disease in _simulation.State.Diseases)
            disease.InfectedDays.Remove(entity.Id);
        State.Citizens.Remove(entity.Id);
        AddChronicle("population.balanced", "Population adjusted", $"{entity.Name} left the simulated population. {reason}.", entity.X, entity.Y, 1);
    }

    private void UpdateWorldEvents()
    {
        if (State.PendingEvent is not null || _simulation.State.Day < State.NextEventDay)
            return;
        SettlementState? city = _simulation.State.Settlements.Values.OrderBy(_ => _random.NextUInt()).FirstOrDefault();
        KingdomState? kingdom = city?.KingdomId is ulong kid ? _simulation.State.Kingdoms.GetValueOrDefault(kid) : null;
        WorldEventKind kind;
        if (city is not null && city.Food < 25) kind = WorldEventKind.Famine;
        else if (_simulation.State.Diseases.Count > 0) kind = WorldEventKind.Plague;
        else if (State.Weather == WeatherKind.Drought) kind = WorldEventKind.Drought;
        else if (State.Weather == WeatherKind.Storm) kind = _random.NextFloat() < 0.5f ? WorldEventKind.Flood : WorldEventKind.Fire;
        else kind = (WorldEventKind)(_random.NextUInt() % (uint)Enum.GetValues<WorldEventKind>().Length);

        State.PendingEvent = CreateEvent(kind, city, kingdom);
        AddChronicle("world.event", State.PendingEvent.Title, State.PendingEvent.Description, State.PendingEvent.X, State.PendingEvent.Y, 3);
    }

    private PendingWorldEvent CreateEvent(WorldEventKind kind, SettlementState? city, KingdomState? kingdom)
    {
        (string title, string description, string[] choices) = kind switch
        {
            WorldEventKind.Famine => ("Famine", $"{city?.Name ?? "A settlement"} is running out of food.", new[] { "Buy emergency grain", "Evacuate families", "Let the city endure" }),
            WorldEventKind.Plague => ("Plague emergency", $"Disease is spreading near {city?.Name ?? "the population"}.", new[] { "Quarantine the city", "Fund healers", "Do nothing" }),
            WorldEventKind.GreatHarvest => ("Great harvest", $"{city?.Name ?? "The region"} produced an exceptional harvest.", new[] { "Store the surplus", "Export for profit", "Hold a feast" }),
            WorldEventKind.Festival => ("Festival request", $"Citizens of {city?.Name ?? "the realm"} request a celebration.", new[] { "Fund a grand festival", "Allow a modest festival", "Refuse" }),
            WorldEventKind.Fire => ("City fire", $"Fire threatens buildings in {city?.Name ?? "a settlement"}.", new[] { "Use stored timber", "Hire emergency crews", "Let it burn out" }),
            WorldEventKind.Flood => ("Flood", $"Floodwater threatens {city?.Name ?? "a settlement"}.", new[] { "Build barriers", "Fund recovery", "Accept the losses" }),
            WorldEventKind.Drought => ("Drought", $"Water and crops are failing around {city?.Name ?? "a settlement"}.", new[] { "Build irrigation", "Import supplies", "Ration food" }),
            WorldEventKind.MigrationWave => ("Migration wave", $"Families seek entry to {city?.Name ?? "the realm"}.", new[] { "Welcome everyone", "Accept skilled workers", "Close the border" }),
            WorldEventKind.SuccessionCrisis => ("Succession crisis", $"{kingdom?.Name ?? "A kingdom"} has no clear successor.", new[] { "Support a compromise", "Let the council decide", "Back a military claimant" }),
            WorldEventKind.Rebellion => ("Rebellion", $"Unrest is growing in {kingdom?.Name ?? "a kingdom"}.", new[] { "Offer reforms", "Negotiate", "Suppress the revolt" }),
            WorldEventKind.MonsterRaid => ("Monster raid", $"A monster threatens {city?.Name ?? "a settlement"}.", new[] { "Fortify the city", "Use divine lightning", "Abandon the outskirts" }),
            WorldEventKind.MarketBoom => ("Market boom", $"Trade is booming in {city?.Name ?? "a settlement"}.", new[] { "Collect taxes", "Invest in knowledge", "Share the prosperity" }),
            _ => ("World event", "Something unusual is happening.", new[] { "Respond", "Observe", "Ignore" }),
        };
        return new PendingWorldEvent
        {
            Id = State.NextEventId++,
            Kind = kind,
            Title = title,
            Description = description,
            X = city?.X ?? _world.Width / 2,
            Y = city?.Y ?? _world.Height / 2,
            CreatedDay = _simulation.State.Day,
            SettlementId = city?.Id,
            KingdomId = kingdom?.Id,
            Choices = choices.ToList(),
        };
    }

    private void EvaluateScenario()
    {
        ScenarioProgress scenario = State.Scenario;
        if (scenario.Scenario == ScenarioKind.Sandbox || scenario.Completed || scenario.Failed)
            return;
        int settlers = _simulation.State.Entities.Values.Count(e => e.IsAlive && e.Species == SpeciesKind.Settler);
        int grazers = _simulation.State.Entities.Values.Count(e => e.IsAlive && e.Species == SpeciesKind.Grazer);
        int predators = _simulation.State.Entities.Values.Count(e => e.IsAlive && e.Species == SpeciesKind.Predator);

        switch (scenario.Scenario)
        {
            case ScenarioKind.Survive100Years:
                scenario.Progress = Math.Clamp(_simulation.State.Year / 100f, 0, 1);
                scenario.Completed = _simulation.State.Year >= 100 && _simulation.State.Kingdoms.Count > 0 && _simulation.State.Settlements.Count > 0;
                scenario.Failed = settlers == 0 || _simulation.State.Settlements.Count == 0;
                break;
            case ScenarioKind.EcosystemBalance:
                bool balanced = grazers >= 20 && predators >= 2 && grazers / (float)Math.Max(1, predators) is >= 3 and <= 14;
                int years = Math.Max(0, (_simulation.State.Day - scenario.StartedDay) / 360);
                scenario.Progress = balanced ? Math.Clamp(years / 5f, 0, 1) : 0;
                scenario.Completed = balanced && years >= 5;
                scenario.Failed = grazers == 0;
                break;
            case ScenarioKind.StopPlague:
                scenario.Progress = _simulation.State.Diseases.Count == 0 ? 1 : Math.Clamp(1 - _simulation.State.Diseases.Sum(d => d.InfectedDays.Count) / (float)Math.Max(1, settlers), 0, 0.95f);
                scenario.Completed = _simulation.State.Day > scenario.StartedDay + 10 && _simulation.State.Diseases.Count == 0;
                scenario.Failed = settlers < Math.Max(1, State.Population.SpeciesCaps.GetValueOrDefault(SpeciesKind.Settler) / 4);
                break;
            case ScenarioKind.AllianceOfThree:
                int allied = _simulation.State.Kingdoms.Values.Count(k => k.Relations.Count(r => r.Value >= 70) >= 2);
                scenario.Progress = Math.Clamp(allied / 3f, 0, 1);
                scenario.Completed = allied >= 3;
                break;
            case ScenarioKind.BuildMetropolis:
                int maxPopulation = _simulation.State.Settlements.Keys.Select(id => _simulation.State.Entities.Values.Count(e => e.IsAlive && e.SettlementId == id)).DefaultIfEmpty().Max();
                scenario.Progress = Math.Clamp(maxPopulation / 100f, 0, 1);
                scenario.Completed = maxPopulation >= 100 && _simulation.State.Settlements.Values.Any(s => s.Stage is SettlementStage.City or SettlementStage.Capital);
                break;
            case ScenarioKind.RestoreAfterDisaster:
                float averageHappiness = _simulation.State.Settlements.Values.Select(s => s.Happiness).DefaultIfEmpty().Average();
                float averageFood = _simulation.State.Settlements.Values.Select(s => MathF.Min(100, s.Food)).DefaultIfEmpty().Average();
                scenario.Progress = Math.Clamp((averageHappiness + averageFood) / 150f, 0, 1);
                scenario.Completed = scenario.Progress >= 1 && settlers >= 50;
                break;
        }
    }

    private void RecordPopulationHistory()
    {
        if (_simulation.State.Day % 30 != 0)
            return;
        int births = (int)Math.Min(int.MaxValue, _simulation.State.TotalBirths);
        int currentEntities = _simulation.State.Entities.Count;
        int birthDelta = Math.Max(0, births - _birthsAtLastHistory);
        int deaths = Math.Max(0, _entityCountAtLastHistory + birthDelta - currentEntities);
        _birthsAtLastHistory = births;
        _entityCountAtLastHistory = currentEntities;
        State.PopulationHistory.Add(new PopulationHistoryPoint(
            _simulation.State.Day,
            CountSpecies(SpeciesKind.Settler),
            CountSpecies(SpeciesKind.Grazer),
            CountSpecies(SpeciesKind.Predator),
            CountSpecies(SpeciesKind.Monster),
            CountSpecies(SpeciesKind.Fish),
            birthDelta,
            deaths,
            State.TotalMigrants));
        if (State.PopulationHistory.Count > 240)
            State.PopulationHistory.RemoveRange(0, State.PopulationHistory.Count - 240);
    }

    private void DecayTraffic()
    {
        foreach (int tile in State.TrafficByTile.Keys.ToArray())
        {
            int next = State.TrafficByTile[tile] - 1;
            if (next <= 0) State.TrafficByTile.Remove(tile);
            else State.TrafficByTile[tile] = next;
        }
    }

    private void MoveEntityToward(SimEntity entity, int targetX, int targetY)
    {
        int dx = Math.Sign(targetX - entity.X);
        int dy = Math.Sign(targetY - entity.Y);
        (int x, int y)[] candidates =
        {
            (entity.X + dx, entity.Y + dy),
            (entity.X + dx, entity.Y),
            (entity.X, entity.Y + dy),
        };
        foreach ((int x, int y) in candidates)
        {
            if (!IsLandPassable(x, y))
                continue;
            entity.X = x;
            entity.Y = y;
            int tile = _world.ToIndex(x, y);
            State.TrafficByTile[tile] = State.TrafficByTile.GetValueOrDefault(tile) + 3;
            return;
        }
    }

    private void MoveAwayFromSettlement(SimEntity entity, CitizenLifeProfile profile)
    {
        if (profile.HomeSettlementId is not ulong sid || !_simulation.State.Settlements.TryGetValue(sid, out SettlementState? city))
            return;
        int dx = Math.Sign(entity.X - city.X);
        int dy = Math.Sign(entity.Y - city.Y);
        if (dx == 0 && dy == 0) dx = entity.Id % 2 == 0 ? 1 : -1;
        int x = entity.X + dx;
        int y = entity.Y + dy;
        if (IsLandPassable(x, y))
        {
            entity.X = x;
            entity.Y = y;
        }
    }

    private bool IsLandPassable(int x, int y)
    {
        if (!_world.IsInside(x, y))
            return false;
        TerrainType terrain = _world.GetTerrain(x, y);
        return terrain is not (TerrainType.DeepOcean or TerrainType.ShallowWater or TerrainType.Mountain);
    }

    private CitizenJob ChooseJob(SimEntity entity, SettlementState? home)
    {
        if (entity.AgeDays < 16 * 360)
            return CitizenJob.Child;
        if (home?.LeaderId == entity.Id)
            return CitizenJob.Ruler;
        CitizenJob[] jobs =
        {
            CitizenJob.Farmer, CitizenJob.Farmer, CitizenJob.Woodcutter, CitizenJob.Miner,
            CitizenJob.Builder, CitizenJob.Trader, CitizenJob.Healer, CitizenJob.Priest,
            CitizenJob.Scholar, CitizenJob.Guard, CitizenJob.Soldier,
        };
        return jobs[(int)(entity.Id % (ulong)jobs.Length)];
    }

    private static EntityAction ActionForJob(CitizenJob job) => job switch
    {
        CitizenJob.Farmer => EntityAction.Farm,
        CitizenJob.Woodcutter => EntityAction.Gather,
        CitizenJob.Miner => EntityAction.Gather,
        CitizenJob.Builder => EntityAction.Build,
        CitizenJob.Trader => EntityAction.Trade,
        CitizenJob.Healer => EntityAction.Heal,
        CitizenJob.Priest => EntityAction.Pray,
        CitizenJob.Scholar => EntityAction.Work,
        CitizenJob.Guard => EntityAction.Defend,
        CitizenJob.Soldier => EntityAction.Defend,
        CitizenJob.Ruler => EntityAction.Work,
        _ => EntityAction.Idle,
    };

    private (int X, int Y) WorkLocation(CitizenJob job, SettlementState? city, ulong id, int fallbackX, int fallbackY)
    {
        if (city is null)
            return (fallbackX, fallbackY);
        (int ox, int oy) = job switch
        {
            CitizenJob.Farmer => (6, 4),
            CitizenJob.Woodcutter => (-7, -4),
            CitizenJob.Miner => (9, -7),
            CitizenJob.Builder => (2, 1),
            CitizenJob.Trader => (1, 0),
            CitizenJob.Healer => (-2, 1),
            CitizenJob.Priest => (0, -2),
            CitizenJob.Scholar => (3, -2),
            CitizenJob.Guard => ((id % 2 == 0 ? 1 : -1) * 6, 0),
            CitizenJob.Soldier => (0, 6),
            CitizenJob.Ruler => (0, 0),
            _ => (0, 1),
        };
        int x = Math.Clamp(city.X + ox, 0, _world.Width - 1);
        int y = Math.Clamp(city.Y + oy, 0, _world.Height - 1);
        if (!IsLandPassable(x, y))
            return (city.X, city.Y);
        return (x, y);
    }

    private (int X, int Y) OffsetAround(int x, int y, ulong id, int minRadius, int maxRadius)
    {
        int radius = minRadius + (int)(id % (ulong)Math.Max(1, maxRadius - minRadius + 1));
        int direction = (int)(id % 8);
        (int dx, int dy) = direction switch
        {
            0 => (radius, 0), 1 => (radius, radius), 2 => (0, radius), 3 => (-radius, radius),
            4 => (-radius, 0), 5 => (-radius, -radius), 6 => (0, -radius), _ => (radius, -radius),
        };
        int tx = Math.Clamp(x + dx, 0, _world.Width - 1);
        int ty = Math.Clamp(y + dy, 0, _world.Height - 1);
        return IsLandPassable(tx, ty) ? (tx, ty) : (x, y);
    }

    private void SpawnMigrants(SettlementState city, int count)
    {
        for (int i = 0; i < count && _simulation.State.Entities.Count < State.Population.GlobalPopulationLimit; i++)
        {
            SimEntity settler = _simulation.SpawnEntity(SpeciesKind.Settler, city.X, city.Y, $"Migrant {_simulation.State.NextEntityId}");
            settler.AgeDays = (18 + i % 20) * 360;
            settler.SettlementId = city.Id;
            settler.KingdomId = city.KingdomId;
            EnsureCitizen(settler);
            State.TotalMigrants++;
        }
    }

    private int CountSpecies(SpeciesKind species) => _simulation.State.Entities.Values.Count(e => e.IsAlive && e.Species == species);

    private void AddChronicle(string type, string title, string description, int x, int y, int severity)
    {
        _simulation.State.Chronicle.Add(new ChronicleEvent(_simulation.State.Tick, type, title, description, x, y, severity, Array.Empty<ulong>()));
        if (_simulation.State.Chronicle.Count > 10000)
            _simulation.State.Chronicle.RemoveRange(0, 1000);
    }

    private static int DistanceSquared(int x1, int y1, int x2, int y2)
    {
        int dx = x1 - x2;
        int dy = y1 - y2;
        return dx * dx + dy * dy;
    }
}
