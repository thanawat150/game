using Godot;
using WorldForge.Core.Simulation;

namespace WorldForge.Presentation;

public enum GameIcon
{
    Food,
    Wood,
    Stone,
    Ore,
    Planks,
    Metal,
    Tools,
    Weapons,
    Gold,
    Mana,
    Faith,
    Fear,
    Happiness,
    Health,
    Disease,
    Fire,
    Lightning,
    Water,
    Forest,
    Fertility,
    Population,
    Army,
    Fleet,
    Trade,
    Diplomacy,
    Magic,
    Ruins,
    Relic,
    Chronicle,
    Settings,
}

public enum GameArtEffect
{
    HealingAura,
    BlessingBeam,
    CurseCloud,
    PlagueCloud,
    LightningStrike,
    Meteor,
    FireBurst,
    Smoke,
    HolySigil,
    MagicCircle,
    ForestGrowth,
    ShieldBubble,
    ManaCrystal,
    RelicIdol,
    AncientRuin,
    Crown,
    TreasureChest,
    BattleDust,
    BattleDustLarge,
    Anchor,
}

public sealed class GeneratedGameArtAtlas
{
    public const int IconCell = 64;
    public const int CharacterCell = 64;
    public const int PortraitCell = 128;
    public const int BuildingCell = 128;
    public const int EffectCell = 96;

    public Texture2D IconsTexture { get; }
    public Texture2D CharactersTexture { get; }
    public Texture2D PortraitsTexture { get; }
    public Texture2D BuildingsTexture { get; }
    public Texture2D EffectsTexture { get; }

    public GeneratedGameArtAtlas()
    {
        IconsTexture = LoadPng(GeneratedGameArtData.IconsPngBase64, "icons");
        CharactersTexture = LoadPng(GeneratedGameArtData.CharactersPngBase64, "characters");
        PortraitsTexture = LoadPng(GeneratedGameArtData.PortraitsPngBase64, "portraits");
        BuildingsTexture = LoadPng(GeneratedGameArtData.BuildingsPngBase64, "buildings");
        EffectsTexture = LoadPng(GeneratedGameArtData.EffectsPngBase64, "effects");
    }

    public AtlasTexture Icon(GameIcon icon) => Slice(IconsTexture, IconRegion(icon));
    public AtlasTexture Portrait(RaceKind race, LegendRole role) => Slice(PortraitsTexture, PortraitRegion(race, role));

    public Rect2 IconRegion(GameIcon icon)
    {
        int index = Math.Clamp((int)icon, 0, 29);
        return GridRegion(index, 6, IconCell);
    }

    public Rect2 CharacterRegion(RaceKind race, CitizenJob job)
    {
        int row = Math.Clamp((int)race, 0, 5);
        int column = CharacterColumn(race, job);
        return new Rect2(column * CharacterCell, row * CharacterCell, CharacterCell, CharacterCell);
    }

    public Rect2 PortraitRegion(RaceKind race, LegendRole role)
    {
        int baseIndex = race switch
        {
            RaceKind.Human => 0,
            RaceKind.Sylvan => 2,
            RaceKind.Dwarf => 4,
            RaceKind.Orc => 6,
            RaceKind.Tideborn => 8,
            RaceKind.Arcane => 10,
            _ => 0,
        };
        bool secondary = role is LegendRole.Scholar or LegendRole.Healer or LegendRole.Priest or LegendRole.Explorer;
        return GridRegion(baseIndex + (secondary ? 1 : 0), 4, PortraitCell);
    }

    public Rect2 BuildingRegion(BuildingKind kind)
    {
        int index = kind switch
        {
            BuildingKind.House => 0,
            BuildingKind.Farm => 1,
            BuildingKind.Market => 2,
            BuildingKind.Lumberyard => 3,
            BuildingKind.Quarry => 4,
            BuildingKind.Mine => 5,
            BuildingKind.Clinic => 6,
            BuildingKind.Temple => 7,
            BuildingKind.Barracks => 8,
            BuildingKind.Sawmill => 3,
            BuildingKind.Smelter => 9,
            BuildingKind.Workshop => 9,
            BuildingKind.Watchtower => 10,
            BuildingKind.Wall => 10,
            BuildingKind.Gate => 11,
            BuildingKind.Keep => 12,
            BuildingKind.Harbor => 13,
            BuildingKind.Shipyard => 14,
            BuildingKind.MageTower => 16,
            BuildingKind.Monument => 17,
            _ => 0,
        };
        return GridRegion(index, 6, BuildingCell);
    }

    public Rect2 EffectRegion(GameArtEffect effect)
    {
        int index = Math.Clamp((int)effect, 0, 19);
        return GridRegion(index, 5, EffectCell);
    }

    private static int CharacterColumn(RaceKind race, CitizenJob job)
    {
        return race switch
        {
            RaceKind.Human => job switch
            {
                CitizenJob.Farmer or CitizenJob.Child or CitizenJob.Trader => 0,
                CitizenJob.Builder or CitizenJob.Woodcutter or CitizenJob.Miner => 1,
                _ => 2,
            },
            RaceKind.Sylvan => job switch
            {
                CitizenJob.Healer or CitizenJob.Priest or CitizenJob.Scholar => 1,
                CitizenJob.Ruler => 2,
                _ => 0,
            },
            RaceKind.Dwarf => job switch
            {
                CitizenJob.Miner => 0,
                CitizenJob.Builder or CitizenJob.Woodcutter or CitizenJob.Trader => 1,
                _ => 2,
            },
            RaceKind.Orc => job switch
            {
                CitizenJob.Priest or CitizenJob.Healer or CitizenJob.Scholar => 2,
                CitizenJob.Trader or CitizenJob.Woodcutter or CitizenJob.Builder => 1,
                _ => 0,
            },
            RaceKind.Tideborn => job switch
            {
                CitizenJob.Healer or CitizenJob.Priest or CitizenJob.Scholar => 1,
                CitizenJob.Trader or CitizenJob.Builder => 2,
                _ => 0,
            },
            RaceKind.Arcane => job switch
            {
                CitizenJob.Scholar or CitizenJob.Priest or CitizenJob.Healer => 1,
                CitizenJob.Guard or CitizenJob.Soldier => 2,
                _ => 0,
            },
            _ => 0,
        };
    }

    private static Rect2 GridRegion(int index, int columns, int cell)
        => new((index % columns) * cell, (index / columns) * cell, cell, cell);

    private static AtlasTexture Slice(Texture2D atlas, Rect2 region)
        => new() { Atlas = atlas, Region = region };

    private static ImageTexture LoadPng(string base64, string label)
    {
        byte[] bytes = Convert.FromBase64String(base64);
        var image = new Image();
        Error error = image.LoadPngFromBuffer(bytes);
        if (error != Error.Ok)
            throw new InvalidOperationException($"Unable to load generated {label} atlas: {error}");
        return ImageTexture.CreateFromImage(image);
    }
}
