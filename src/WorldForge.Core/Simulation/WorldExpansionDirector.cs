using System.Text.Json;
using System.Text.Json.Serialization;
using WorldForge.Core.World;

namespace WorldForge.Core.Simulation;

public sealed partial class WorldExpansionDirector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly WorldMap _world;
    private readonly GrandSimulation _simulation;
    private readonly LivingWorldDirector _living;
    private int _lastKnownChronicleCount;

    public WorldExpansionDirector(WorldMap world, GrandSimulation simulation, LivingWorldDirector living, long seed)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        _living = living ?? throw new ArgumentNullException(nameof(living));
        State = new WorldExpansionState { Seed = seed };
        InitializeAchievements();
        EnsureWorldRecords();
        RecordHistory(force: true);
    }

    private WorldExpansionDirector(WorldMap world, GrandSimulation simulation, LivingWorldDirector living, WorldExpansionState state)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        _living = living ?? throw new ArgumentNullException(nameof(living));
        State = state ?? throw new ArgumentNullException(nameof(state));
        if (State.SaveVersion > WorldExpansionState.CurrentSaveVersion)
            throw new InvalidDataException($"Unsupported expansion save version {State.SaveVersion}.");
        State.SaveVersion = WorldExpansionState.CurrentSaveVersion;
        InitializeAchievements();
        EnsureWorldRecords();
        _lastKnownChronicleCount = Math.Min(State.Faith.LastChronicleIndex, _simulation.State.Chronicle.Count);
    }

    public WorldExpansionState State { get; }
    public GrandSimulation Simulation => _simulation;
    public LivingWorldDirector Living => _living;
    public WorldMap World => _world;

    public string SaveToJson() => JsonSerializer.Serialize(State, JsonOptions);

    public static WorldExpansionDirector LoadFromJson(
        WorldMap world,
        GrandSimulation simulation,
        LivingWorldDirector living,
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        WorldExpansionState state = JsonSerializer.Deserialize<WorldExpansionState>(json, JsonOptions)
            ?? throw new InvalidDataException("Expansion save payload is empty.");
        return new WorldExpansionDirector(world, simulation, living, state);
    }

    public string ExportModPack() => JsonSerializer.Serialize(State.ModRules, JsonOptions);

    public void ImportModPack(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ExpansionModRules rules = JsonSerializer.Deserialize<ExpansionModRules>(json, JsonOptions)
            ?? throw new InvalidDataException("Mod pack is empty.");
        rules.LegendPromotionMultiplier = Math.Clamp(rules.LegendPromotionMultiplier, 0.1f, 5f);
        rules.ConstructionSpeedMultiplier = Math.Clamp(rules.ConstructionSpeedMultiplier, 0.1f, 5f);
        rules.FaithGainMultiplier = Math.Clamp(rules.FaithGainMultiplier, 0.1f, 5f);
        rules.FleetSpeedMultiplier = Math.Clamp(rules.FleetSpeedMultiplier, 0.25f, 4f);
        rules.MagicFrequencyMultiplier = Math.Clamp(rules.MagicFrequencyMultiplier, 0f, 5f);
        rules.NomadFrequencyMultiplier = Math.Clamp(rules.NomadFrequencyMultiplier, 0f, 5f);
        rules.InitialRuinCount = Math.Clamp(rules.InitialRuinCount, 0, 100);
        State.ModRules = rules;
        EnsureWorldRecords();
    }

    public void EnsureWorldRecords()
    {
        AssignRaces();
        foreach (SettlementState settlement in _simulation.State.Settlements.Values.OrderBy(s => s.Id))
            EnsureCityDistrict(settlement);
        foreach (SimEntity entity in _simulation.State.Entities.Values.Where(e => e.IsAlive && e.Species == SpeciesKind.Settler).OrderBy(e => e.Id))
            EnsureCitizenExpansion(entity);
        SeedRuinsIfNeeded();
        PromoteInitialLegends();
        CleanupMissingRecords();
    }

    public void AdvanceDay()
    {
        int day = _simulation.State.Day;
        if (State.LastAdvancedDay == day)
            return;
        State.LastAdvancedDay = day;
        EnsureWorldRecords();
        UpdateLegendLives();
        UpdateCityConstructionAndProduction();
        UpdateFaithProgression();
        UpdateFleets();
        UpdateNomads();
        UpdateMagicAndRuins();
        UpdateAchievementsAndCampaign();
        RecordHistory(force: false);
        TrimState();
    }

    public RaceKind RaceForEntity(ulong entityId)
        => State.CitizenRaces.GetValueOrDefault(entityId, RaceKind.Human);

    public RaceKind RaceForKingdom(ulong kingdomId)
        => State.KingdomRaces.GetValueOrDefault(kingdomId, RaceKind.Human);

    public WorldHistorySnapshot? SnapshotAtIndex(int index)
    {
        if (State.History.Count == 0)
            return null;
        return State.History[Math.Clamp(index, 0, State.History.Count - 1)];
    }

    public int LatestHistoryIndex => Math.Max(0, State.History.Count - 1);

    public LegendProfile? MostFamousLivingLegend()
    {
        return State.Legends.Values
            .Where(l => !l.IsDead && _simulation.State.Entities.GetValueOrDefault(l.EntityId)?.IsAlive == true)
            .OrderByDescending(l => l.Fame)
            .ThenBy(l => l.EntityId)
            .FirstOrDefault();
    }

    public bool CommissionMonument(ulong settlementId, ulong legendId)
    {
        if (!_simulation.State.Settlements.TryGetValue(settlementId, out SettlementState? city) ||
            !State.Legends.TryGetValue(legendId, out LegendProfile? legend))
            return false;
        CityDistrictState district = EnsureCityDistrict(city);
        if (city.Stone < 20 || city.Gold < 8)
            return false;
        city.Stone -= 20;
        city.Gold -= 8;
        PlaceBuilding(district, city, BuildingKind.Monument, immediate: false);
        legend.Monuments++;
        legend.Legacy += 12;
        AddMemory(legend, MemoryKind.Achievement, $"A monument was commissioned in {city.Name}.", city.X, city.Y, null, 5);
        AddChronicle("legend.monument", "อนุสาวรีย์แห่งตำนาน", $"{city.Name} เริ่มสร้างอนุสาวรีย์ให้ {DisplayLegendName(legend)}", city.X, city.Y, 2, legend.EntityId);
        return true;
    }

    public bool PlanBuilding(ulong settlementId, BuildingKind kind)
    {
        if (!_simulation.State.Settlements.TryGetValue(settlementId, out SettlementState? city))
            return false;
        CityDistrictState district = EnsureCityDistrict(city);
        if (district.Buildings.Count(b => b.Status is BuildingStatus.Planned or BuildingStatus.Building) >= 4)
            return false;
        PlaceBuilding(district, city, kind, immediate: false);
        return true;
    }

    public bool RepairCity(ulong settlementId)
    {
        if (!_simulation.State.Settlements.TryGetValue(settlementId, out SettlementState? city) ||
            !State.CityDistricts.TryGetValue(settlementId, out CityDistrictState? district))
            return false;
        PlacedBuilding? target = district.Buildings
            .Where(b => b.Status is BuildingStatus.Damaged or BuildingStatus.Ruined || b.Health < 100)
            .OrderBy(b => b.Health)
            .FirstOrDefault();
        if (target is null || city.Wood < 8 || city.Stone < 5)
            return false;
        city.Wood -= 8;
        city.Stone -= 5;
        target.Status = BuildingStatus.Building;
        target.Progress = Math.Max(35, target.Progress);
        target.Health = Math.Max(30, target.Health);
        district.RuinDamage = Math.Max(0, district.RuinDamage - 10);
        return true;
    }

    private void AssignRaces()
    {
        foreach (KingdomState kingdom in _simulation.State.Kingdoms.Values.OrderBy(k => k.Id))
        {
            if (!State.KingdomRaces.ContainsKey(kingdom.Id))
                State.KingdomRaces[kingdom.Id] = State.ModRules.EnableFantasyRaces
                    ? (RaceKind)((kingdom.Id - 1) % (ulong)Enum.GetValues<RaceKind>().Length)
                    : RaceKind.Human;
        }
    }

    private void EnsureCitizenExpansion(SimEntity entity)
    {
        RaceKind race = entity.KingdomId is ulong kingdomId
            ? RaceForKingdom(kingdomId)
            : RaceKind.Human;
        State.CitizenRaces[entity.Id] = race;

        if (State.ModRules.EnableMagic && !State.Mages.ContainsKey(entity.Id))
        {
            CitizenLifeProfile? life = _living.State.Citizens.GetValueOrDefault(entity.Id);
            bool suitable = life?.Job is CitizenJob.Scholar or CitizenJob.Priest or CitizenJob.Healer;
            int hash = StableHash(entity.Id, State.Seed);
            if (suitable && Math.Abs(hash % 100) < 18)
            {
                MagicSchool school = race switch
                {
                    RaceKind.Sylvan => MagicSchool.Nature,
                    RaceKind.Dwarf => MagicSchool.Fire,
                    RaceKind.Tideborn => MagicSchool.Storm,
                    RaceKind.Arcane => MagicSchool.Arcane,
                    _ when life?.Job == CitizenJob.Healer => MagicSchool.Healing,
                    _ => MagicSchool.Arcane,
                };
                State.Mages[entity.Id] = CreateMage(entity.Id, school);
            }
        }
    }

    private void CleanupMissingRecords()
    {
        HashSet<ulong> entityIds = _simulation.State.Entities.Keys.ToHashSet();
        foreach (ulong id in State.CitizenRaces.Keys.Where(id => !entityIds.Contains(id)).ToArray())
            State.CitizenRaces.Remove(id);
        foreach (ulong id in State.Mages.Keys.Where(id => !entityIds.Contains(id)).ToArray())
            State.Mages.Remove(id);
        foreach (ulong id in State.CityDistricts.Keys.Where(id => !_simulation.State.Settlements.ContainsKey(id)).ToArray())
            State.CityDistricts.Remove(id);
        foreach (ulong id in State.Fleets.Keys.Where(id => !id.Equals(id) || !State.Fleets[id].IsActive).ToArray())
            State.Fleets.Remove(id);
    }

    private void TrimState()
    {
        if (State.History.Count > 360)
            State.History.RemoveRange(0, State.History.Count - 360);
        foreach (LegendProfile legend in State.Legends.Values)
        {
            if (legend.Memories.Count > 80)
                legend.Memories.RemoveRange(0, legend.Memories.Count - 80);
            if (legend.Relationships.Count > 40)
            {
                foreach (ulong id in legend.Relationships.Values.OrderBy(r => Math.Abs(r.Strength)).Take(legend.Relationships.Count - 40).Select(r => r.OtherEntityId).ToArray())
                    legend.Relationships.Remove(id);
            }
        }
        if (State.WorldLegends.Count > 200)
            State.WorldLegends.RemoveRange(0, State.WorldLegends.Count - 200);
    }

    private Random CreateDayRandom(int salt)
    {
        long mixed = State.Seed ^ ((long)_simulation.State.Day << 21) ^ ((long)salt * 0x9E3779B9L);
        return new Random(unchecked((int)(mixed ^ (mixed >> 32))));
    }

    private static int StableHash(ulong id, long seed)
    {
        ulong value = id ^ (ulong)seed;
        value ^= value >> 33;
        value *= 0xff51afd7ed558ccdUL;
        value ^= value >> 33;
        return unchecked((int)value);
    }

    private void AddChronicle(string type, string title, string description, int x, int y, int severity, params ulong[] involved)
    {
        _simulation.State.Chronicle.Add(new ChronicleEvent(
            _simulation.State.Tick,
            type,
            title,
            description,
            x,
            y,
            severity,
            involved));
        if (_simulation.State.Chronicle.Count > 1200)
            _simulation.State.Chronicle.RemoveRange(0, _simulation.State.Chronicle.Count - 1200);
    }
}
