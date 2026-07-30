using WorldForge.Core.World;

namespace WorldForge.Core.Editing;

public sealed class TerrainEditor
{
    private readonly Stack<TerrainEditCommand> _undo = new();
    private readonly HashSet<ChunkCoord> _dirtyChunks = new();

    public int UndoCount => _undo.Count;

    public int Paint(WorldMap world, int centerX, int centerY, int radius, TerrainType terrain)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (radius < 0 || radius > 128)
            throw new ArgumentOutOfRangeException(nameof(radius));

        var changes = new List<TerrainChange>();
        int effectiveRadius = Math.Max(0, radius);
        for (int y = centerY - effectiveRadius; y <= centerY + effectiveRadius; y++)
        {
            for (int x = centerX - effectiveRadius; x <= centerX + effectiveRadius; x++)
            {
                if (!world.IsInside(x, y))
                    continue;
                int dx = x - centerX;
                int dy = y - centerY;
                if (effectiveRadius > 0 && dx * dx + dy * dy > effectiveRadius * effectiveRadius)
                    continue;

                int index = world.ToIndex(x, y);
                TerrainType before = world.GetTerrainByIndex(index);
                if (before == terrain)
                    continue;
                changes.Add(new TerrainChange(index, before, terrain));
            }
        }

        if (changes.Count == 0)
            return 0;

        var command = new TerrainEditCommand(changes);
        command.Apply(world);
        _undo.Push(command);
        MarkDirty(world, changes);
        return changes.Count;
    }

    public bool Undo(WorldMap world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (_undo.Count == 0)
            return false;
        TerrainEditCommand command = _undo.Pop();
        command.Undo(world);
        MarkDirty(world, command.Changes);
        return true;
    }

    public IReadOnlyCollection<ChunkCoord> DrainDirtyChunks()
    {
        ChunkCoord[] result = _dirtyChunks.ToArray();
        _dirtyChunks.Clear();
        return result;
    }

    public void ClearHistory()
    {
        _undo.Clear();
        _dirtyChunks.Clear();
    }

    private void MarkDirty(WorldMap world, IEnumerable<TerrainChange> changes)
    {
        foreach (TerrainChange change in changes)
        {
            int x = change.Index % world.Width;
            int y = change.Index / world.Width;
            _dirtyChunks.Add(world.GetChunkForTile(x, y));
        }
    }
}

public readonly record struct TerrainChange(int Index, TerrainType Before, TerrainType After);

public sealed class TerrainEditCommand
{
    public TerrainEditCommand(IReadOnlyList<TerrainChange> changes)
    {
        Changes = changes ?? throw new ArgumentNullException(nameof(changes));
    }

    public IReadOnlyList<TerrainChange> Changes { get; }

    public void Apply(WorldMap world)
    {
        foreach (TerrainChange change in Changes)
            world.SetTerrainByIndex(change.Index, change.After);
    }

    public void Undo(WorldMap world)
    {
        for (int i = Changes.Count - 1; i >= 0; i--)
        {
            TerrainChange change = Changes[i];
            world.SetTerrainByIndex(change.Index, change.Before);
        }
    }
}
