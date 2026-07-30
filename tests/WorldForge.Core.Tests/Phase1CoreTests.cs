using Xunit;
using WorldForge.Core.Editing;
using WorldForge.Core.Persistence;
using WorldForge.Core.Simulation;
using WorldForge.Core.World;

namespace WorldForge.Core.Tests;

public sealed class Phase1CoreTests
{
    [Fact]
    public void SameSeedAndConfigProduceSameWorldChecksum()
    {
        var config = new WorldGenerationConfig { Seed = 123456, Width = 128, Height = 128, ChunkSize = 64 };
        WorldMap first = WorldGenerator.Generate(config);
        WorldMap second = WorldGenerator.Generate(config);
        Assert.Equal(WorldChecksum.Compute(first), WorldChecksum.Compute(second));
    }

    [Fact]
    public void DifferentSeedsProduceDifferentWorldChecksums()
    {
        WorldMap first = WorldGenerator.Generate(new WorldGenerationConfig { Seed = 100, Width = 128, Height = 128, ChunkSize = 64 });
        WorldMap second = WorldGenerator.Generate(new WorldGenerationConfig { Seed = 101, Width = 128, Height = 128, ChunkSize = 64 });
        Assert.NotEqual(WorldChecksum.Compute(first), WorldChecksum.Compute(second));
    }

    [Fact]
    public void TerrainPaintAndUndoRestoreOriginalChecksum()
    {
        WorldMap world = WorldGenerator.Generate(new WorldGenerationConfig { Seed = 99, Width = 128, Height = 128, ChunkSize = 64 });
        string original = WorldChecksum.Compute(world);
        var editor = new TerrainEditor();
        int changed = editor.Paint(world, 50, 50, 4, TerrainType.Mountain);
        Assert.True(changed > 0);
        Assert.NotEqual(original, WorldChecksum.Compute(world));
        Assert.True(editor.Undo(world));
        Assert.Equal(original, WorldChecksum.Compute(world));
    }

    [Fact]
    public void PausedClockDoesNotAdvance()
    {
        var clock = new SimulationClock();
        clock.SetPaused(true);
        int steps = clock.Advance(10);
        Assert.Equal(0, steps);
        Assert.Equal(0, clock.TickCount);
    }

    [Fact]
    public void TimeScaleChangesTickRateWithoutChangingFixedTickSize()
    {
        var clock = new SimulationClock();
        clock.SetTimeScale(4);
        int steps = clock.Advance(0.25);
        Assert.Equal(10, steps);
        Assert.Equal(10, clock.TickCount);
    }

    [Fact]
    public void SaveLoadRoundTripPreservesWorldAndClock()
    {
        string directory = Path.Combine(Path.GetTempPath(), "worldforge-tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "slot.wfg.json");
        try
        {
            WorldMap world = WorldGenerator.Generate(new WorldGenerationConfig { Seed = 20260730, Width = 128, Height = 128, ChunkSize = 64 });
            var editor = new TerrainEditor();
            editor.Paint(world, 10, 10, 3, TerrainType.Forest);
            editor.Paint(world, 80, 60, 6, TerrainType.ShallowWater);
            var clock = new SimulationClock();
            clock.Restore(12345, 8, paused: true);
            string before = WorldChecksum.Compute(world);

            var service = new WorldSaveService();
            service.Save(path, world, clock);
            LoadedWorld loaded = service.Load(path);

            Assert.Equal(before, loaded.Checksum);
            Assert.Equal(before, WorldChecksum.Compute(loaded.World));
            Assert.Equal(12345, loaded.Clock.TickCount);
            Assert.Equal(8, loaded.Clock.TimeScale);
            Assert.True(loaded.Clock.IsPaused);
            Assert.Equal(world.TerrainOverrides.Count, loaded.World.TerrainOverrides.Count);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MediumWorldGeneratesWithoutThrowing()
    {
        WorldMap world = WorldGenerator.Generate(new WorldGenerationConfig
        {
            Seed = 777,
            Width = 512,
            Height = 512,
            ChunkSize = 64,
        });
        Assert.Equal(262144, world.TileCount);
        Assert.Equal(64, world.ChunkColumns * world.ChunkRows);
        Assert.False(string.IsNullOrWhiteSpace(WorldChecksum.Compute(world)));
    }
}
