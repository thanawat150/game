using System.Text.Json;
using Godot;
using WorldForge.Core.World;

namespace WorldForge.Presentation;

public sealed partial class WorldChunkRenderer : Node2D
{
    private readonly Dictionary<ChunkCoord, Sprite2D> _sprites = new();
    private Dictionary<TerrainType, Color> _palette = CreateFallbackPalette();
    private WorldMap? _world;

    public int TilePixelSize { get; set; } = 4;
    public int RenderedChunkCount => _sprites.Count;

    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        _palette = LoadPaletteOrFallback("res://data/biomes/phase1_biomes.json");
    }

    public void Bind(WorldMap world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        foreach (Sprite2D sprite in _sprites.Values)
            sprite.QueueFree();
        _sprites.Clear();

        for (int chunkY = 0; chunkY < world.ChunkRows; chunkY++)
        {
            for (int chunkX = 0; chunkX < world.ChunkColumns; chunkX++)
                RebuildChunk(new ChunkCoord(chunkX, chunkY));
        }
    }

    public void RefreshChunks(IEnumerable<ChunkCoord> chunks)
    {
        if (_world is null)
            return;
        foreach (ChunkCoord chunk in chunks.Distinct())
            RebuildChunk(chunk);
    }

    private void RebuildChunk(ChunkCoord chunk)
    {
        if (_world is null)
            return;
        if (chunk.X < 0 || chunk.Y < 0 || chunk.X >= _world.ChunkColumns || chunk.Y >= _world.ChunkRows)
            return;

        int size = _world.ChunkSize;
        Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        int startX = chunk.X * size;
        int startY = chunk.Y * size;
        for (int localY = 0; localY < size; localY++)
        {
            for (int localX = 0; localX < size; localX++)
            {
                TerrainType terrain = _world.GetTerrain(startX + localX, startY + localY);
                image.SetPixel(localX, localY, _palette.GetValueOrDefault(terrain, Colors.Magenta));
            }
        }

        ImageTexture texture = ImageTexture.CreateFromImage(image);
        if (!_sprites.TryGetValue(chunk, out Sprite2D? sprite))
        {
            sprite = new Sprite2D
            {
                Centered = false,
                TextureFilter = TextureFilterEnum.Nearest,
                Scale = new Vector2(TilePixelSize, TilePixelSize),
                Position = new Vector2(startX * TilePixelSize, startY * TilePixelSize),
                Name = $"Chunk_{chunk.X}_{chunk.Y}",
            };
            AddChild(sprite);
            _sprites[chunk] = sprite;
        }
        sprite.Texture = texture;
    }

    private static Dictionary<TerrainType, Color> LoadPaletteOrFallback(string resourcePath)
    {
        try
        {
            string fullPath = ProjectSettings.GlobalizePath(resourcePath);
            string json = File.ReadAllText(fullPath);
            BiomeDefinition[]? definitions = JsonSerializer.Deserialize<BiomeDefinition[]>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            if (definitions is null || definitions.Length == 0)
                return CreateFallbackPalette();

            var result = new Dictionary<TerrainType, Color>();
            foreach (BiomeDefinition definition in definitions)
            {
                if (Enum.TryParse(definition.TerrainType, ignoreCase: true, out TerrainType terrain))
                    result[terrain] = Color.FromHtml(definition.Color);
            }
            return result.Count > 0 ? result : CreateFallbackPalette();
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Biome palette could not be loaded; fallback palette is active. {exception.Message}");
            return CreateFallbackPalette();
        }
    }

    private static Dictionary<TerrainType, Color> CreateFallbackPalette() => new()
    {
        [TerrainType.DeepOcean] = Color.FromHtml("#102A56"),
        [TerrainType.ShallowWater] = Color.FromHtml("#236AA0"),
        [TerrainType.Beach] = Color.FromHtml("#D8C27A"),
        [TerrainType.Grassland] = Color.FromHtml("#74A84A"),
        [TerrainType.Forest] = Color.FromHtml("#285A35"),
        [TerrainType.Mountain] = Color.FromHtml("#76777A"),
    };

    private sealed class BiomeDefinition
    {
        public string TerrainType { get; set; } = string.Empty;
        public string Color { get; set; } = "#FF00FF";
    }
}
