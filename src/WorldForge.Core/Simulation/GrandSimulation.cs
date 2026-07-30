using System.Text.Json;
using WorldForge.Core.World;

namespace WorldForge.Core.Simulation;

public enum SpeciesKind { Grazer, Predator, Settler, Monster, Fish }
public enum EntityAction { Idle, SearchFood, Eat, Hunt, Gather, Work, Build, Farm, Trade, Pray, Heal, Flee, Defend, Migrate }
public enum SettlementStage { Camp, Village, Town, City, Capital }
public enum GovernmentType { Monarchy, Council, TribalConfederation, MerchantRepublic, Theocracy, MilitaryState, MageCouncil }
public enum RelationState { War, Hostile, Neutral, Friendly, Alliance }
public enum GodPowerType { CreateForest, Knowledge, Peace, Lightning, Plague, Meteor, Blessing, Curse }
public enum WorldAge { Dawn, Growth, Kingdoms, Conflict, Ash, Frost, Wonders, Silence }

public sealed class SimEntity
{
    public ulong Id { get; init; }
    public string Name { get; set; } = "Unnamed";
    public SpeciesKind Species { get; init; }
    public int X { get; set; }
    public int Y { get; set; }
    public int AgeDays { get; set; }
    public float Health { get; set; } = 100;
    public float Hunger { get; set; }
    public float Energy { get; set; } = 100;
    public float Morale { get; set; } = 50;
    public float Intelligence { get; set; } = 10;
    public float Fertility { get; set; } = 0.02f;
    public EntityAction Action { get; set; }
    public ulong? SettlementId { get; set; }
    public ulong? KingdomId { get; set; }
    public List<string> Traits { get; init; } = new();
    public List<ulong> Parents { get; init; } = new();
    public bool IsAlive => Health > 0;
}

public sealed class SettlementState
{
    public ulong Id { get; init; }
    public string Name { get; set; } = "Settlement";
    public SettlementStage Stage { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public ulong? LeaderId { get; set; }
    public ulong? KingdomId { get; set; }
    public float Food { get; set; } = 100;
    public float Wood { get; set; } = 50;
    public float Stone { get; set; }
    public float Gold { get; set; }
    public int Housing { get; set; } = 12;
    public float Happiness { get; set; } = 50;
    public float Technology { get; set; }
    public HashSet<string> Buildings { get; init; } = new(StringComparer.Ordinal);
    public HashSet<int> Territory { get; init; } = new();
}

public sealed class KingdomState
{
    public ulong Id { get; init; }
    public string Name { get; set; } = "Kingdom";
    public ulong CapitalId { get; set; }
    public ulong? RulerId { get; set; }
    public GovernmentType Government { get; set; }
    public float Stability { get; set; } = 65;
    public float Economy { get; set; }
    public float ArmyStrength { get; set; }
    public string CultureId { get; set; } = "culture.common";
    public string ReligionId { get; set; } = "religion.none";
    public HashSet<ulong> Settlements { get; init; } = new();
    public Dictionary<ulong, int> Relations { get; init; } = new();
    public HashSet<string> Technologies { get; init; } = new(StringComparer.Ordinal);
}

public sealed class DiseaseState
{
    public string Id { get; init; } = "disease.unknown";
    public float InfectionRate { get; init; } = 0.05f;
    public float MortalityRate { get; init; } = 0.01f;
    public int DurationDays { get; init; } = 20;
    public Dictionary<ulong, int> InfectedDays { get; init; } = new();
}

public sealed record ChronicleEvent(long Tick, string Type, string Title, string Description, int X, int Y, int Severity, IReadOnlyList<ulong> Involved);

public sealed class GrandSimulationState
{
    public const int CurrentSaveVersion = 2;
    public int SaveVersion { get; set; } = CurrentSaveVersion;
    public long Seed { get; init; }
    public long Tick { get; set; }
    public int Day { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public WorldAge Age { get; set; } = WorldAge.Dawn;
    public ulong NextEntityId { get; set; } = 1;
    public ulong NextSettlementId { get; set; } = 1;
    public ulong NextKingdomId { get; set; } = 1;
    public Dictionary<ulong, SimEntity> Entities { get; init; } = new();
    public Dictionary<ulong, SettlementState> Settlements { get; init; } = new();
    public Dictionary<ulong, KingdomState> Kingdoms { get; init; } = new();
    public Dictionary<string, float> CultureInfluence { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, float> ReligionInfluence { get; init; } = new(StringComparer.Ordinal);
    public List<DiseaseState> Diseases { get; init; } = new();
    public List<ChronicleEvent> Chronicle { get; init; } = new();
}

public sealed class GrandSimulation
{
    private readonly WorldMap _world;
    private readonly DeterministicRandom _random;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public GrandSimulation(WorldMap world, long seed)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        State = new GrandSimulationState { Seed = seed };
        _random = new DeterministicRandom(seed);
    }

    private GrandSimulation(WorldMap world, GrandSimulationState state)
    {
        _world = world;
        State = state;
        _random = new DeterministicRandom(state.Seed ^ state.Tick);
    }

    public GrandSimulationState State { get; }

    public SimEntity SpawnEntity(SpeciesKind species, int x, int y, string? name = null)
    {
        if (!_world.IsInside(x, y)) throw new ArgumentOutOfRangeException(nameof(x));
        TerrainType terrain = _world.GetTerrain(x, y);
        bool water = terrain is TerrainType.DeepOcean or TerrainType.ShallowWater;
        if (species == SpeciesKind.Fish && !water) throw new InvalidOperationException("Fish must spawn in water.");
        if (species != SpeciesKind.Fish && water) throw new InvalidOperationException("Land entities cannot spawn in water.");

        ulong id = State.NextEntityId++;
        var entity = new SimEntity
        {
            Id = id,
            Name = name ?? $"{species}-{id}",
            Species = species,
            X = x,
            Y = y,
            Intelligence = species == SpeciesKind.Settler ? 15 : 5,
            Fertility = species == SpeciesKind.Settler ? 0.015f : 0.025f,
        };
        State.Entities[id] = entity;
        return entity;
    }

    public void AdvanceDays(int days)
    {
        if (days < 0) throw new ArgumentOutOfRangeException(nameof(days));
        for (int i = 0; i < days; i++) AdvanceDay();
    }

    public void AdvanceDay()
    {
        State.Tick++;
        State.Day++;
        UpdateEntities();
        UpdateDiseases();
        CleanupDead();
        if (State.Day % 30 == 0) AdvanceMonth();
    }

    private void UpdateEntities()
    {
        foreach (SimEntity entity in State.Entities.Values.OrderBy(e => e.Id).ToArray())
        {
            if (!entity.IsAlive) continue;
            entity.AgeDays++;
            entity.Hunger = MathF.Min(100, entity.Hunger + 5);
            entity.Energy = MathF.Max(0, entity.Energy - 1);

            if (entity.Hunger >= 75)
            {
                entity.Action = EntityAction.SearchFood;
                if (TryConsumeFood(entity)) entity.Action = EntityAction.Eat;
                else entity.Health -= 4;
            }
            else if (entity.Species == SpeciesKind.Predator)
            {
                entity.Action = EntityAction.Hunt;
                TryHunt(entity);
            }
            else if (entity.Species == SpeciesKind.Settler)
            {
                entity.Action = entity.SettlementId is null ? EntityAction.Gather : EntityAction.Work;
                GatherForSettlement(entity);
            }
            else entity.Action = EntityAction.Idle;
        }
    }

    private bool TryConsumeFood(SimEntity entity)
    {
        if (entity.SettlementId is ulong sid && State.Settlements.TryGetValue(sid, out SettlementState? settlement) && settlement.Food >= 2)
        {
            settlement.Food -= 2;
            entity.Hunger = MathF.Max(0, entity.Hunger - 65);
            entity.Health = MathF.Min(100, entity.Health + 2);
            return true;
        }

        TerrainType terrain = _world.GetTerrain(entity.X, entity.Y);
        if (entity.Species is SpeciesKind.Grazer or SpeciesKind.Settler && terrain is TerrainType.Grassland or TerrainType.Forest)
        {
            entity.Hunger = MathF.Max(0, entity.Hunger - 45);
            return true;
        }
        return false;
    }

    private void TryHunt(SimEntity predator)
    {
        SimEntity? prey = State.Entities.Values
            .Where(e => e.IsAlive && e.Species == SpeciesKind.Grazer && DistanceSquared(e, predator) <= 4)
            .OrderBy(e => e.Id)
            .FirstOrDefault();
        if (prey is null) return;
        prey.Health -= 35;
        predator.Hunger = MathF.Max(0, predator.Hunger - 50);
        if (!prey.IsAlive) AddEvent("entity.killed", "Predator kill", $"{predator.Name} killed {prey.Name}.", predator.X, predator.Y, 2, predator.Id, prey.Id);
    }

    private void GatherForSettlement(SimEntity entity)
    {
        if (entity.SettlementId is not ulong sid || !State.Settlements.TryGetValue(sid, out SettlementState? settlement)) return;
        TerrainType terrain = _world.GetTerrain(entity.X, entity.Y);
        settlement.Food += terrain is TerrainType.Forest or TerrainType.Grassland ? 1.5f : 0.2f;
        if (terrain == TerrainType.Forest) settlement.Wood += 1;
        if (terrain == TerrainType.Mountain) settlement.Stone += 0.7f;
    }

    public SettlementState FoundSettlement(IEnumerable<ulong> settlerIds, string name)
    {
        SimEntity[] settlers = settlerIds.Select(id => State.Entities[id]).Where(e => e.IsAlive && e.Species == SpeciesKind.Settler).ToArray();
        if (settlers.Length < 5) throw new InvalidOperationException("At least five living settlers are required.");
        int x = (int)Math.Round(settlers.Average(e => e.X));
        int y = (int)Math.Round(settlers.Average(e => e.Y));
        if (!IsSuitableSettlementTile(x, y)) throw new InvalidOperationException("Settlement location is unsuitable.");

        ulong id = State.NextSettlementId++;
        var settlement = new SettlementState { Id = id, Name = name, X = x, Y = y, Housing = Math.Max(12, settlers.Length + 2) };
        settlement.Buildings.Add("building.campfire");
        settlement.Territory.Add(_world.ToIndex(x, y));
        settlement.LeaderId = settlers.OrderByDescending(e => e.Intelligence + e.Morale).ThenBy(e => e.Id).First().Id;
        State.Settlements[id] = settlement;
        foreach (SimEntity settler in settlers) settler.SettlementId = id;
        AddEvent("settlement.founded", "Settlement founded", $"{name} was founded.", x, y, 3, settlers.Select(e => e.Id).ToArray());
        return settlement;
    }

    public KingdomState FoundKingdom(ulong capitalSettlementId, string name, GovernmentType government)
    {
        SettlementState capital = State.Settlements[capitalSettlementId];
        if (capital.KingdomId is not null) throw new InvalidOperationException("Settlement already belongs to a kingdom.");
        ulong id = State.NextKingdomId++;
        var kingdom = new KingdomState { Id = id, Name = name, CapitalId = capital.Id, RulerId = capital.LeaderId, Government = government };
        kingdom.Settlements.Add(capital.Id);
        capital.KingdomId = id;
        capital.Stage = SettlementStage.Capital;
        foreach (SimEntity entity in State.Entities.Values.Where(e => e.SettlementId == capital.Id)) entity.KingdomId = id;
        State.Kingdoms[id] = kingdom;
        AddEvent("kingdom.founded", "Kingdom founded", $"{name} emerged with {capital.Name} as its capital.", capital.X, capital.Y, 4);
        return kingdom;
    }

    public void SetRelation(ulong firstId, ulong secondId, int value)
    {
        if (firstId == secondId) throw new ArgumentException("A kingdom cannot have diplomacy with itself.");
        value = Math.Clamp(value, -100, 100);
        State.Kingdoms[firstId].Relations[secondId] = value;
        State.Kingdoms[secondId].Relations[firstId] = value;
    }

    public RelationState GetRelationState(ulong firstId, ulong secondId)
    {
        int value = State.Kingdoms[firstId].Relations.GetValueOrDefault(secondId);
        return value switch { <= -70 => RelationState.War, <= -25 => RelationState.Hostile, >= 70 => RelationState.Alliance, >= 25 => RelationState.Friendly, _ => RelationState.Neutral };
    }

    public void ApplyPower(GodPowerType power, int x, int y, int radius = 2)
    {
        if (!_world.IsInside(x, y)) throw new ArgumentOutOfRangeException(nameof(x));
        foreach (SimEntity entity in State.Entities.Values.Where(e => e.IsAlive && DistanceSquared(e.X, e.Y, x, y) <= radius * radius))
        {
            switch (power)
            {
                case GodPowerType.Blessing: entity.Health = MathF.Min(100, entity.Health + 30); if (!entity.Traits.Contains("trait.blessed")) entity.Traits.Add("trait.blessed"); break;
                case GodPowerType.Curse: entity.Health -= 20; if (!entity.Traits.Contains("trait.cursed")) entity.Traits.Add("trait.cursed"); break;
                case GodPowerType.Lightning: entity.Health -= 45; break;
                case GodPowerType.Meteor: entity.Health -= 100; break;
                case GodPowerType.Knowledge: entity.Intelligence += 3; break;
                case GodPowerType.Plague: Infect(entity.Id, new DiseaseState { Id = "disease.divine_plague", InfectionRate = 0.18f, MortalityRate = 0.08f, DurationDays = 18 }); break;
            }
        }

        if (power == GodPowerType.CreateForest)
        {
            for (int ty = Math.Max(0, y - radius); ty <= Math.Min(_world.Height - 1, y + radius); ty++)
                for (int tx = Math.Max(0, x - radius); tx <= Math.Min(_world.Width - 1, x + radius); tx++)
                    if (DistanceSquared(tx, ty, x, y) <= radius * radius && _world.GetTerrain(tx, ty) == TerrainType.Grassland)
                        _world.SetTerrain(tx, ty, TerrainType.Forest);
        }
        AddEvent("power.used", "Divine intervention", $"{power} affected the world.", x, y, power is GodPowerType.Meteor or GodPowerType.Plague ? 5 : 2);
        CleanupDead();
    }

    public void Infect(ulong entityId, DiseaseState disease)
    {
        DiseaseState? active = State.Diseases.FirstOrDefault(d => d.Id == disease.Id);
        if (active is null) { active = disease; State.Diseases.Add(active); }
        active.InfectedDays.TryAdd(entityId, 0);
    }

    private void UpdateDiseases()
    {
        foreach (DiseaseState disease in State.Diseases.ToArray())
        {
            foreach (ulong id in disease.InfectedDays.Keys.OrderBy(id => id).ToArray())
            {
                if (!State.Entities.TryGetValue(id, out SimEntity? entity) || !entity.IsAlive) { disease.InfectedDays.Remove(id); continue; }
                int elapsed = ++disease.InfectedDays[id];
                if (_random.NextFloat() < disease.MortalityRate) entity.Health -= 30;
                foreach (SimEntity nearby in State.Entities.Values.Where(e => e.IsAlive && !disease.InfectedDays.ContainsKey(e.Id) && DistanceSquared(e, entity) <= 2))
                    if (_random.NextFloat() < disease.InfectionRate) disease.InfectedDays[nearby.Id] = 0;
                if (elapsed >= disease.DurationDays) disease.InfectedDays.Remove(id);
            }
            if (disease.InfectedDays.Count == 0) State.Diseases.Remove(disease);
        }
    }

    private void AdvanceMonth()
    {
        State.Month++;
        foreach (SettlementState settlement in State.Settlements.Values)
        {
            SimEntity[] citizens = State.Entities.Values.Where(e => e.IsAlive && e.SettlementId == settlement.Id).ToArray();
            settlement.Food -= citizens.Length * 0.8f;
            if (settlement.Food < 0)
            {
                float shortage = -settlement.Food;
                settlement.Food = 0;
                settlement.Happiness = MathF.Max(0, settlement.Happiness - shortage * 0.3f);
                foreach (SimEntity citizen in citizens.Take((int)MathF.Ceiling(shortage / 3))) citizen.Health -= 12;
            }
            TryBuild(settlement, citizens.Length);
            UpgradeSettlement(settlement, citizens.Length);
        }

        foreach (KingdomState kingdom in State.Kingdoms.Values)
        {
            int population = State.Entities.Values.Count(e => e.IsAlive && e.KingdomId == kingdom.Id);
            kingdom.Economy = kingdom.Settlements.Sum(id => State.Settlements[id].Food + State.Settlements[id].Gold) / 10f;
            kingdom.ArmyStrength = population * 0.15f + kingdom.Technologies.Count * 5;
            kingdom.Stability = Math.Clamp(kingdom.Stability + (kingdom.Economy > population ? 1 : -1), 0, 100);
            TryResearch(kingdom, population);
        }

        if (State.Month % 12 == 0) AdvanceYear();
        CleanupDead();
    }

    private void AdvanceYear()
    {
        State.Year++;
        State.Age = State.Year switch { < 5 => WorldAge.Dawn, < 20 => WorldAge.Growth, < 50 => WorldAge.Kingdoms, < 80 => WorldAge.Conflict, _ => WorldAge.Wonders };
        foreach (string culture in State.Kingdoms.Values.Select(k => k.CultureId).Distinct()) State.CultureInfluence[culture] = State.CultureInfluence.GetValueOrDefault(culture) + 1;
        foreach (string religion in State.Kingdoms.Values.Select(k => k.ReligionId).Distinct()) State.ReligionInfluence[religion] = State.ReligionInfluence.GetValueOrDefault(religion) + 1;
    }

    private void TryBuild(SettlementState settlement, int population)
    {
        if (!settlement.Buildings.Contains("building.house") && settlement.Wood >= 10) { settlement.Wood -= 10; settlement.Housing += 8; settlement.Buildings.Add("building.house"); }
        if (population >= 10 && !settlement.Buildings.Contains("building.farm") && settlement.Wood >= 8) { settlement.Wood -= 8; settlement.Buildings.Add("building.farm"); settlement.Food += 20; }
        if (population >= 20 && !settlement.Buildings.Contains("building.market") && settlement.Wood >= 12 && settlement.Stone >= 5) { settlement.Wood -= 12; settlement.Stone -= 5; settlement.Buildings.Add("building.market"); }
    }

    private void TryResearch(KingdomState kingdom, int population)
    {
        string[] tree = { "tech.agriculture", "tech.construction", "tech.medicine", "tech.trade", "tech.metallurgy", "tech.navigation" };
        string? next = tree.FirstOrDefault(t => !kingdom.Technologies.Contains(t));
        if (next is null) return;
        double chance = Math.Min(0.8, 0.02 + population * 0.002 + kingdom.Economy * 0.001);
        if (_random.NextFloat() < chance)
        {
            kingdom.Technologies.Add(next);
            SettlementState capital = State.Settlements[kingdom.CapitalId];
            AddEvent("technology.discovered", "Technology discovered", $"{kingdom.Name} discovered {next}.", capital.X, capital.Y, 3);
        }
    }

    private static void UpgradeSettlement(SettlementState settlement, int population) => settlement.Stage = population switch
    {
        >= 100 => SettlementStage.City,
        >= 50 => SettlementStage.Town,
        >= 15 => SettlementStage.Village,
        _ => settlement.Stage,
    };

    private bool IsSuitableSettlementTile(int x, int y)
    {
        if (!_world.IsInside(x, y)) return false;
        TerrainType terrain = _world.GetTerrain(x, y);
        return terrain is TerrainType.Grassland or TerrainType.Forest or TerrainType.Beach;
    }

    private void CleanupDead()
    {
        foreach (SimEntity dead in State.Entities.Values.Where(e => !e.IsAlive).ToArray())
        {
            AddEvent("entity.died", "Death", $"{dead.Name} died.", dead.X, dead.Y, 2, dead.Id);
            State.Entities.Remove(dead.Id);
            foreach (DiseaseState disease in State.Diseases) disease.InfectedDays.Remove(dead.Id);
        }
    }

    private void AddEvent(string type, string title, string description, int x, int y, int severity, params ulong[] involved)
    {
        State.Chronicle.Add(new ChronicleEvent(State.Tick, type, title, description, x, y, severity, involved));
        if (State.Chronicle.Count > 10000) State.Chronicle.RemoveRange(0, 1000);
    }

    private static int DistanceSquared(SimEntity first, SimEntity second) => DistanceSquared(first.X, first.Y, second.X, second.Y);
    private static int DistanceSquared(int x1, int y1, int x2, int y2) { int dx = x1 - x2; int dy = y1 - y2; return dx * dx + dy * dy; }

    public string SaveToJson() => JsonSerializer.Serialize(State, JsonOptions);

    public static GrandSimulation LoadFromJson(WorldMap world, string json)
    {
        GrandSimulationState state = JsonSerializer.Deserialize<GrandSimulationState>(json, JsonOptions) ?? throw new InvalidDataException("Simulation save is empty.");
        if (state.SaveVersion != GrandSimulationState.CurrentSaveVersion) throw new InvalidDataException($"Unsupported simulation save version {state.SaveVersion}.");
        return new GrandSimulation(world, state);
    }
}

public sealed class DeterministicRandom
{
    private ulong _state;
    public DeterministicRandom(long seed) => _state = unchecked((ulong)seed) + 0x9E3779B97F4A7C15UL;
    public uint NextUInt()
    {
        ulong z = (_state += 0x9E3779B97F4A7C15UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return (uint)((z ^ (z >> 31)) >> 32);
    }
    public float NextFloat() => NextUInt() / (float)uint.MaxValue;
}

public sealed record ModManifest(string Id, string Version, IReadOnlyList<string> Dependencies, IReadOnlyList<string> ContentTypes);

public static class ModValidator
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        "species", "traits", "buildings", "technologies", "biomes", "disasters", "powers", "cultures", "religions", "items", "events", "sprites", "sounds"
    };

    public static IReadOnlyList<string> Validate(ModManifest manifest, IEnumerable<string> installedModIds)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(manifest.Id) || manifest.Id.Any(char.IsWhiteSpace)) errors.Add("Mod id must be non-empty and contain no whitespace.");
        if (!Version.TryParse(manifest.Version, out _)) errors.Add("Mod version must be valid.");
        HashSet<string> installed = installedModIds.ToHashSet(StringComparer.Ordinal);
        foreach (string dependency in manifest.Dependencies.Where(d => !installed.Contains(d))) errors.Add($"Missing dependency: {dependency}");
        foreach (string type in manifest.ContentTypes.Where(t => !Supported.Contains(t))) errors.Add($"Unsupported content type: {type}");
        return errors;
    }
}
