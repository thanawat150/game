using System.Text.Json;
using System.Text.Json.Serialization;
using WorldForge.Core.Simulation;
using WorldForge.Core.World;

namespace WorldForge.Core.Persistence;

public sealed class WorldSaveService
{
    public const string Magic = "WFG_SAVE";
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public void Save(string path, WorldMap world, SimulationClock clock)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Save path is required.", nameof(path));
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(clock);

        var document = new WorldSaveDocument
        {
            Header = new SaveHeader
            {
                Magic = Magic,
                SchemaVersion = CurrentSchemaVersion,
                GeneratorVersion = WorldGenerationConfig.CurrentGeneratorVersion,
                CreatedUtc = DateTimeOffset.UtcNow,
            },
            World = new WorldSaveState
            {
                Config = world.Config,
                SimulationTick = clock.TickCount,
                TimeScale = clock.TimeScale,
                IsPaused = clock.IsPaused,
                RandomState = world.Config.Seed,
                Checksum = WorldChecksum.Compute(world),
                TerrainEdits = world.EnumerateOverrides()
                    .Select(item => new TerrainEditSave { Index = item.Index, Terrain = item.Terrain })
                    .ToList(),
            },
        };

        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Unable to resolve the save directory.");
        Directory.CreateDirectory(directory);

        string tempPath = fullPath + ".tmp";
        string backupPath = fullPath + ".bak";
        string json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(tempPath, json);

        if (File.Exists(fullPath))
            File.Copy(fullPath, backupPath, overwrite: true);
        File.Move(tempPath, fullPath, overwrite: true);
    }

    public LoadedWorld LoadWithRecovery(string path)
    {
        try
        {
            return Load(path);
        }
        catch (Exception primaryException) when (primaryException is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            string backupPath = path + ".bak";
            if (!File.Exists(backupPath))
                throw;
            try
            {
                return Load(backupPath);
            }
            catch (Exception backupException)
            {
                throw new InvalidDataException("Both the primary save and backup save failed to load.", new AggregateException(primaryException, backupException));
            }
        }
    }

    public LoadedWorld Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("World save was not found.", path);

        string json = File.ReadAllText(path);
        WorldSaveDocument? document = JsonSerializer.Deserialize<WorldSaveDocument>(json, JsonOptions);
        if (document?.Header is null || document.World is null)
            throw new InvalidDataException("Save document is incomplete.");
        if (!string.Equals(document.Header.Magic, Magic, StringComparison.Ordinal))
            throw new InvalidDataException("Save magic header is invalid.");
        if (document.Header.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported save schema {document.Header.SchemaVersion}.");
        if (!string.Equals(document.Header.GeneratorVersion, WorldGenerationConfig.CurrentGeneratorVersion, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported generator version {document.Header.GeneratorVersion}.");

        document.World.Config.Validate();
        WorldMap world = WorldGenerator.Generate(document.World.Config);
        foreach (TerrainEditSave edit in document.World.TerrainEdits)
        {
            if ((uint)edit.Index >= (uint)world.TileCount)
                throw new InvalidDataException($"Terrain edit index {edit.Index} is outside the world.");
            world.SetTerrainByIndex(edit.Index, edit.Terrain);
        }

        string actualChecksum = WorldChecksum.Compute(world);
        if (!string.Equals(actualChecksum, document.World.Checksum, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("World checksum does not match the saved data.");

        var clock = new SimulationClock();
        clock.Restore(document.World.SimulationTick, document.World.TimeScale, document.World.IsPaused);
        return new LoadedWorld(world, clock, actualChecksum);
    }
}

public sealed class WorldSaveDocument
{
    public SaveHeader Header { get; set; } = new();
    public WorldSaveState World { get; set; } = new();
}

public sealed class SaveHeader
{
    public string Magic { get; set; } = WorldSaveService.Magic;
    public int SchemaVersion { get; set; } = WorldSaveService.CurrentSchemaVersion;
    public string GeneratorVersion { get; set; } = WorldGenerationConfig.CurrentGeneratorVersion;
    public DateTimeOffset CreatedUtc { get; set; }
}

public sealed class WorldSaveState
{
    public WorldGenerationConfig Config { get; set; } = new();
    public long SimulationTick { get; set; }
    public double TimeScale { get; set; } = 1;
    public bool IsPaused { get; set; }
    public long RandomState { get; set; }
    public string Checksum { get; set; } = string.Empty;
    public List<TerrainEditSave> TerrainEdits { get; set; } = new();
}

public sealed class TerrainEditSave
{
    public int Index { get; set; }
    public TerrainType Terrain { get; set; }
}

public sealed record LoadedWorld(WorldMap World, SimulationClock Clock, string Checksum);
