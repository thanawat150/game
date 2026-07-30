using System.Security.Cryptography;
using System.Text;

namespace WorldForge.Core.World;

public enum TerrainType : byte
{
    DeepOcean = 0,
    ShallowWater = 1,
    Beach = 2,
    Grassland = 3,
    Forest = 4,
    Mountain = 5,
}

public enum BiomeType : byte
{
    DeepOcean = 0,
    ShallowWater = 1,
    Beach = 2,
    Grassland = 3,
    Forest = 4,
    Mountain = 5,
}

public readonly record struct ChunkCoord(int X, int Y);

public readonly record struct TileSnapshot(
    int X,
    int Y,
    TerrainType Terrain,
    BiomeType Biome,
    float Elevation,
    float Moisture,
    float Temperature);

public sealed record WorldGenerationConfig
{
    public const string CurrentGeneratorVersion = "phase1-1.0.0";

    public long Seed { get; init; } = 1502026;
    public int Width { get; init; } = 256;
    public int Height { get; init; } = 256;
    public int ChunkSize { get; init; } = 64;
    public float SeaLevel { get; init; } = 0.48f;
    public int Octaves { get; init; } = 5;
    public float Frequency { get; init; } = 0.0125f;
    public float Persistence { get; init; } = 0.52f;

    public void Validate()
    {
        if (Width <= 0 || Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(Width), "World dimensions must be positive.");
        if (Width > 4096 || Height > 4096)
            throw new ArgumentOutOfRangeException(nameof(Width), "Phase 1 protects against worlds larger than 4096×4096.");
        if (ChunkSize <= 0 || Width % ChunkSize != 0 || Height % ChunkSize != 0)
            throw new ArgumentException("Chunk size must divide both world dimensions exactly.");
        if (SeaLevel is < 0.1f or > 0.9f)
            throw new ArgumentOutOfRangeException(nameof(SeaLevel));
        if (Octaves is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(Octaves));
    }
}

public static class SeedUtility
{
    public static long ParseOrHash(string? value)
    {
        if (long.TryParse(value, out long numeric))
            return numeric;

        string text = string.IsNullOrWhiteSpace(value) ? "worldforge" : value.Trim();
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        foreach (byte b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= prime;
        }
        return unchecked((long)hash);
    }
}

public sealed class WorldMap
{
    private readonly TerrainType[] _generatedTerrain;
    private readonly TerrainType[] _terrain;
    private readonly BiomeType[] _biome;
    private readonly float[] _elevation;
    private readonly float[] _moisture;
    private readonly float[] _temperature;
    private readonly Dictionary<int, TerrainType> _terrainOverrides = new();

    internal WorldMap(
        WorldGenerationConfig config,
        TerrainType[] terrain,
        BiomeType[] biome,
        float[] elevation,
        float[] moisture,
        float[] temperature)
    {
        Config = config;
        _generatedTerrain = (TerrainType[])terrain.Clone();
        _terrain = terrain;
        _biome = biome;
        _elevation = elevation;
        _moisture = moisture;
        _temperature = temperature;
    }

    public WorldGenerationConfig Config { get; }
    public int Width => Config.Width;
    public int Height => Config.Height;
    public int ChunkSize => Config.ChunkSize;
    public int TileCount => _terrain.Length;
    public int ChunkColumns => Width / ChunkSize;
    public int ChunkRows => Height / ChunkSize;
    public IReadOnlyDictionary<int, TerrainType> TerrainOverrides => _terrainOverrides;

    public bool IsInside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public int ToIndex(int x, int y)
    {
        if (!IsInside(x, y))
            throw new ArgumentOutOfRangeException($"Tile ({x}, {y}) is outside the world.");
        return y * Width + x;
    }

    public TileSnapshot GetTile(int x, int y)
    {
        int index = ToIndex(x, y);
        return new TileSnapshot(x, y, _terrain[index], _biome[index], _elevation[index], _moisture[index], _temperature[index]);
    }

    public TerrainType GetTerrain(int x, int y) => _terrain[ToIndex(x, y)];
    public TerrainType GetTerrainByIndex(int index) => _terrain[index];
    public BiomeType GetBiomeByIndex(int index) => _biome[index];
    public float GetElevationByIndex(int index) => _elevation[index];
    public float GetMoistureByIndex(int index) => _moisture[index];
    public float GetTemperatureByIndex(int index) => _temperature[index];

    public void SetTerrain(int x, int y, TerrainType terrain) => SetTerrainByIndex(ToIndex(x, y), terrain);

    public void SetTerrainByIndex(int index, TerrainType terrain)
    {
        if ((uint)index >= (uint)_terrain.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        _terrain[index] = terrain;
        _biome[index] = (BiomeType)terrain;
        if (_generatedTerrain[index] == terrain)
            _terrainOverrides.Remove(index);
        else
            _terrainOverrides[index] = terrain;
    }

    public ChunkCoord GetChunkForTile(int x, int y) => new(x / ChunkSize, y / ChunkSize);

    public IEnumerable<(int Index, TerrainType Terrain)> EnumerateOverrides()
    {
        foreach (KeyValuePair<int, TerrainType> pair in _terrainOverrides.OrderBy(pair => pair.Key))
            yield return (pair.Key, pair.Value);
    }
}

public static class WorldGenerator
{
    public static WorldMap Generate(WorldGenerationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();

        int count = checked(config.Width * config.Height);
        var terrain = new TerrainType[count];
        var biome = new BiomeType[count];
        var elevation = new float[count];
        var moisture = new float[count];
        var temperature = new float[count];

        var elevationNoise = new DeterministicNoise(config.Seed ^ 0x45A1_3D92_7B11_08C1L);
        var moistureNoise = new DeterministicNoise(config.Seed ^ 0x1E77_5A9C_33D4_6F21L);
        var temperatureNoise = new DeterministicNoise(config.Seed ^ 0x61C2_0B85_14EA_39D7L);

        for (int y = 0; y < config.Height; y++)
        {
            float normalizedY = config.Height == 1 ? 0f : y / (float)(config.Height - 1);
            float latitude = MathF.Abs(normalizedY * 2f - 1f);
            for (int x = 0; x < config.Width; x++)
            {
                int index = y * config.Width + x;
                float normalizedX = config.Width == 1 ? 0f : x / (float)(config.Width - 1);
                float edge = MathF.Max(MathF.Abs(normalizedX * 2f - 1f), MathF.Abs(normalizedY * 2f - 1f));
                float islandFalloff = SmoothStep(0.52f, 1f, edge) * 0.34f;

                float e = elevationNoise.Fbm(x * config.Frequency, y * config.Frequency, config.Octaves, config.Persistence);
                e = Math.Clamp(e - islandFalloff + 0.08f, 0f, 1f);
                float m = moistureNoise.Fbm(x * config.Frequency * 1.35f, y * config.Frequency * 1.35f, 4, 0.55f);
                float t = Math.Clamp(1f - latitude * 0.84f + (temperatureNoise.Fbm(x * 0.008f, y * 0.008f, 3, 0.5f) - 0.5f) * 0.26f - e * 0.18f, 0f, 1f);

                TerrainType terrainType = Classify(e, m, t, config.SeaLevel);
                terrain[index] = terrainType;
                biome[index] = (BiomeType)terrainType;
                elevation[index] = e;
                moisture[index] = m;
                temperature[index] = t;
            }
        }

        return new WorldMap(config, terrain, biome, elevation, moisture, temperature);
    }

    private static TerrainType Classify(float elevation, float moisture, float temperature, float seaLevel)
    {
        if (elevation < seaLevel - 0.09f)
            return TerrainType.DeepOcean;
        if (elevation < seaLevel)
            return TerrainType.ShallowWater;
        if (elevation < seaLevel + 0.025f)
            return TerrainType.Beach;
        if (elevation > 0.78f || (elevation > 0.70f && temperature < 0.25f))
            return TerrainType.Mountain;
        if (moisture > 0.53f && temperature > 0.16f)
            return TerrainType.Forest;
        return TerrainType.Grassland;
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}

internal sealed class DeterministicNoise
{
    private readonly long _seed;

    public DeterministicNoise(long seed) => _seed = seed;

    public float Fbm(float x, float y, int octaves, float persistence)
    {
        float total = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue = 0f;
        for (int i = 0; i < octaves; i++)
        {
            total += ValueNoise(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= 2f;
        }
        return total / maxValue;
    }

    private float ValueNoise(float x, float y)
    {
        int x0 = (int)MathF.Floor(x);
        int y0 = (int)MathF.Floor(y);
        int x1 = x0 + 1;
        int y1 = y0 + 1;
        float sx = Fade(x - x0);
        float sy = Fade(y - y0);
        float n0 = Lerp(Hash01(x0, y0), Hash01(x1, y0), sx);
        float n1 = Lerp(Hash01(x0, y1), Hash01(x1, y1), sx);
        return Lerp(n0, n1, sy);
    }

    private float Hash01(int x, int y)
    {
        unchecked
        {
            ulong h = (ulong)_seed;
            h ^= (ulong)(uint)x * 0x9E3779B185EBCA87UL;
            h ^= (ulong)(uint)y * 0xC2B2AE3D27D4EB4FUL;
            h ^= h >> 30;
            h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 27;
            h *= 0x94D049BB133111EBUL;
            h ^= h >> 31;
            return (h & 0x00FF_FFFFUL) / 16777215f;
        }
    }

    private static float Fade(float t) => t * t * (3f - 2f * t);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}

public static class WorldChecksum
{
    public static string Compute(WorldMap world)
    {
        ArgumentNullException.ThrowIfNull(world);
        using var stream = new MemoryStream(capacity: world.TileCount * 2 + 128);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(WorldGenerationConfig.CurrentGeneratorVersion);
        writer.Write(world.Config.Seed);
        writer.Write(world.Width);
        writer.Write(world.Height);
        writer.Write(world.ChunkSize);
        writer.Write(world.Config.SeaLevel);
        for (int i = 0; i < world.TileCount; i++)
        {
            writer.Write((byte)world.GetTerrainByIndex(i));
            writer.Write((byte)world.GetBiomeByIndex(i));
        }
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length))));
    }
}
