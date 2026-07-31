using System.Text.Json.Serialization;

namespace WorldForge.Core.Simulation;

public enum RaceKind { Human, Sylvan, Dwarf, Orc, Tideborn, Arcane }
public enum PersonalityTrait { Brave, Kind, Ambitious, Greedy, Loyal, Cruel, Curious, Pious, Cautious, Charismatic }
public enum LifeGoal { Family, Wealth, Knowledge, Power, Faith, Exploration, Revenge, Peace }
public enum MemoryKind { Birth, Marriage, ChildBorn, Battle, Loss, Rescue, Discovery, Miracle, Betrayal, Coronation, Exile, Achievement, Death }
public enum RelationshipKind { Family, Partner, Friend, Rival, Enemy, Mentor, Benefactor, BetrayedBy, DevotedTo }
public enum LegendRole { None, Hero, Ruler, General, Scholar, Healer, Priest, Explorer, Villain }
public enum BuildingKind { House, Farm, Lumberyard, Quarry, Mine, Sawmill, Smelter, Workshop, Market, Temple, Clinic, Barracks, Watchtower, Wall, Gate, Keep, Harbor, Shipyard, MageTower, Monument }
public enum BuildingStatus { Planned, Building, Active, Damaged, Ruined }
public enum ResourceKind { Food, Wood, Stone, Ore, Planks, Metal, Tools, Weapons, Relics, ManaCrystal }
public enum DeityPath { Mercy, Nature, War, Knowledge, Fear }
public enum FaithDoctrine { Charity, NatureBalance, Conquest, Scholarship, Sacrifice, Protection }
public enum MiracleKind { BlessHarvest, HealCity, Inspire, Smite, RaiseForest, RevealRuins, CalmSea }
public enum FleetMission { Idle, Trade, Explore, Patrol, Raid, Invade, Return }
public enum NomadStateKind { Wandering, Trading, Raiding, Settling }
public enum MagicSchool { Nature, Fire, Healing, Storm, Arcane, Necromancy }
public enum SpellKind { Growth, Fireball, Heal, StormCall, Teleport, Ward, AnimateRuins }
public enum RuinType { AncientTemple, FallenCity, SunkenShrine, MageVault, Battlefield }
public enum CampaignChapter { Awakening, FirstLegend, SacredCity, AgeOfSails, ArcaneDiscovery, ChronicleOfAges, Completed }

public sealed class LegendMemory
{
    public int Day { get; set; }
    public MemoryKind Kind { get; set; }
    public string Summary { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public ulong? OtherEntityId { get; set; }
    public int Weight { get; set; } = 1;
}

public sealed class LegendRelationship
{
    public ulong OtherEntityId { get; set; }
    public RelationshipKind Kind { get; set; }
    public int Strength { get; set; }
    public int LastChangedDay { get; set; }
}

public sealed class LegendProfile
{
    public ulong EntityId { get; set; }
    public RaceKind Race { get; set; } = RaceKind.Human;
    public LegendRole Role { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Epithet { get; set; } = string.Empty;
    public int Fame { get; set; }
    public int Legacy { get; set; }
    public int BirthDay { get; set; }
    public int? DeathDay { get; set; }
    public bool IsDead { get; set; }
    public LifeGoal Goal { get; set; }
    public List<PersonalityTrait> Traits { get; set; } = new();
    public List<LegendMemory> Memories { get; set; } = new();
    public Dictionary<ulong, LegendRelationship> Relationships { get; set; } = new();
    public int Battles { get; set; }
    public int LivesSaved { get; set; }
    public int Discoveries { get; set; }
    public int Monuments { get; set; }
    public int LastEvaluatedDay { get; set; }
    public int KnownChildren { get; set; }
}

public sealed class PlacedBuilding
{
    public long Id { get; set; }
    public ulong SettlementId { get; set; }
    public BuildingKind Kind { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Level { get; set; } = 1;
    public float Progress { get; set; }
    public float Health { get; set; } = 100;
    public BuildingStatus Status { get; set; } = BuildingStatus.Planned;
    public int Workers { get; set; }
    public int StartedDay { get; set; }
    public int CompletedDay { get; set; }
}

public sealed class CityDistrictState
{
    public ulong SettlementId { get; set; }
    public long NextBuildingId { get; set; } = 1;
    public List<PlacedBuilding> Buildings { get; set; } = new();
    public HashSet<int> RoadTiles { get; set; } = new();
    public Dictionary<ResourceKind, float> Stockpile { get; set; } = new();
    public int LastLayoutDay { get; set; } = -10000;
    public int LastProductionDay { get; set; } = -10000;
    public int PopulationAtLastLayout { get; set; }
    public float RuinDamage { get; set; }
}

public sealed class ProphecyState
{
    public long Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public int CreatedDay { get; set; }
    public int TargetDay { get; set; }
    public bool Fulfilled { get; set; }
    public bool Failed { get; set; }
}

public sealed class FaithProgressionState
{
    public DeityPath Path { get; set; } = DeityPath.Mercy;
    public float Faith { get; set; }
    public float Fear { get; set; }
    public float Favor { get; set; } = 25;
    public float MaxFavor { get; set; } = 100;
    public HashSet<FaithDoctrine> Doctrines { get; set; } = new();
    public HashSet<MiracleKind> UnlockedMiracles { get; set; } = new() { MiracleKind.BlessHarvest };
    public Dictionary<ulong, float> CityFaith { get; set; } = new();
    public Dictionary<ulong, FaithDoctrine> KingdomDoctrines { get; set; } = new();
    public List<ProphecyState> Prophecies { get; set; } = new();
    public int LastChronicleIndex { get; set; }
    public int LastProphecyDay { get; set; } = -10000;
}

public sealed class HistoryCitySnapshot
{
    public ulong Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public ulong? KingdomId { get; set; }
    public int Population { get; set; }
    public SettlementStage Stage { get; set; }
    public float Food { get; set; }
    public float Happiness { get; set; }
}

public sealed class HistoryKingdomSnapshot
{
    public ulong Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public RaceKind Race { get; set; }
    public int Settlements { get; set; }
    public int Population { get; set; }
    public float Stability { get; set; }
}

public sealed class WorldHistorySnapshot
{
    public int Day { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int Population { get; set; }
    public int Settlements { get; set; }
    public int Kingdoms { get; set; }
    public int Armies { get; set; }
    public int Fleets { get; set; }
    public long Births { get; set; }
    public long Battles { get; set; }
    public long Captures { get; set; }
    public float Faith { get; set; }
    public float Fear { get; set; }
    public List<HistoryCitySnapshot> Cities { get; set; } = new();
    public List<HistoryKingdomSnapshot> KingdomStates { get; set; } = new();
    public List<ulong> TopLegends { get; set; } = new();
}

public sealed class FleetState
{
    public ulong Id { get; set; }
    public string Name { get; set; } = "Fleet";
    public ulong KingdomId { get; set; }
    public ulong OriginSettlementId { get; set; }
    public ulong? TargetSettlementId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Ships { get; set; } = 3;
    public int Marines { get; set; } = 20;
    public float Supply { get; set; } = 100;
    public float Morale { get; set; } = 75;
    public FleetMission Mission { get; set; }
    public List<GridPoint> Path { get; set; } = new();
    public int PathIndex { get; set; }
    public int LastMoveDay { get; set; } = -10000;
    public bool IsActive { get; set; } = true;
}

public sealed class NomadBandState
{
    public ulong Id { get; set; }
    public string Name { get; set; } = "Nomad band";
    public RaceKind Race { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Population { get; set; } = 24;
    public float Wealth { get; set; } = 20;
    public NomadStateKind State { get; set; } = NomadStateKind.Wandering;
    public ulong? TargetSettlementId { get; set; }
    public int LastMoveDay { get; set; } = -10000;
    public bool Active { get; set; } = true;
}

public sealed class MageProfile
{
    public ulong EntityId { get; set; }
    public MagicSchool School { get; set; }
    public int Level { get; set; } = 1;
    public float Mana { get; set; } = 50;
    public int LastCastDay { get; set; } = -10000;
    public HashSet<SpellKind> KnownSpells { get; set; } = new();
}

public sealed class RuinState
{
    public ulong Id { get; set; }
    public RuinType Type { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Danger { get; set; }
    public int Richness { get; set; }
    public bool Explored { get; set; }
    public int DiscoveredDay { get; set; } = -1;
    public ulong? ExplorerId { get; set; }
    public string RelicName { get; set; } = string.Empty;
}

public sealed class AchievementState
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float Progress { get; set; }
    public float Target { get; set; } = 1;
    public bool Unlocked { get; set; }
    public int UnlockedDay { get; set; }
}

public sealed class CampaignProgressState
{
    public CampaignChapter Chapter { get; set; } = CampaignChapter.Awakening;
    public string Title { get; set; } = "เสียงเรียกแห่งโลก";
    public string Objective { get; set; } = "สร้างเมืองแรกและรักษาประชากรให้รอด";
    public float Progress { get; set; }
    public bool ChapterCompleted { get; set; }
}

public sealed class ExpansionModRules
{
    public string Name { get; set; } = "Default Expansion Rules";
    public float LegendPromotionMultiplier { get; set; } = 1f;
    public float ConstructionSpeedMultiplier { get; set; } = 1f;
    public float FaithGainMultiplier { get; set; } = 1f;
    public float FleetSpeedMultiplier { get; set; } = 1f;
    public float MagicFrequencyMultiplier { get; set; } = 1f;
    public float NomadFrequencyMultiplier { get; set; } = 1f;
    public int InitialRuinCount { get; set; } = 12;
    public bool EnableNavalWarfare { get; set; } = true;
    public bool EnableFantasyRaces { get; set; } = true;
    public bool EnableMagic { get; set; } = true;
    public bool EnableNomads { get; set; } = true;
}

public sealed class WorldExpansionState
{
    public const int CurrentSaveVersion = 1;
    public int SaveVersion { get; set; } = CurrentSaveVersion;
    public long Seed { get; set; }
    public ulong NextFleetId { get; set; } = 1;
    public ulong NextNomadId { get; set; } = 1;
    public ulong NextRuinId { get; set; } = 1;
    public long NextProphecyId { get; set; } = 1;
    public int LastAdvancedDay { get; set; } = -1;
    public int LastHistoryDay { get; set; } = -10000;
    public int LastNomadSpawnDay { get; set; } = -10000;
    public int LastFleetPlanningDay { get; set; } = -10000;
    public long LastBattles { get; set; }
    public long LastCaptures { get; set; }
    public Dictionary<ulong, RaceKind> KingdomRaces { get; set; } = new();
    public Dictionary<ulong, RaceKind> CitizenRaces { get; set; } = new();
    public Dictionary<ulong, LegendProfile> Legends { get; set; } = new();
    public Dictionary<ulong, CityDistrictState> CityDistricts { get; set; } = new();
    public FaithProgressionState Faith { get; set; } = new();
    public List<WorldHistorySnapshot> History { get; set; } = new();
    public Dictionary<ulong, FleetState> Fleets { get; set; } = new();
    public Dictionary<ulong, NomadBandState> Nomads { get; set; } = new();
    public Dictionary<ulong, MageProfile> Mages { get; set; } = new();
    public Dictionary<ulong, RuinState> Ruins { get; set; } = new();
    public Dictionary<string, AchievementState> Achievements { get; set; } = new(StringComparer.Ordinal);
    public CampaignProgressState Campaign { get; set; } = new();
    public ExpansionModRules ModRules { get; set; } = new();
    public List<string> WorldLegends { get; set; } = new();
}
