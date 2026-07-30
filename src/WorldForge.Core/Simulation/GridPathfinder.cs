using WorldForge.Core.World;

namespace WorldForge.Core.Simulation;

public readonly record struct GridPoint(int X, int Y);

/// <summary>
/// Deterministic A* over the world tile grid. The pathfinder is shared by creatures
/// and armies and never depends on Godot nodes, so it is testable in isolation.
/// </summary>
public sealed class GridPathfinder
{
    private static readonly (int X, int Y, float Cost)[] Directions =
    {
        (0, -1, 1f),
        (1, 0, 1f),
        (0, 1, 1f),
        (-1, 0, 1f),
        (1, -1, 1.41421356f),
        (1, 1, 1.41421356f),
        (-1, 1, 1.41421356f),
        (-1, -1, 1.41421356f),
    };

    private readonly WorldMap _world;

    public GridPathfinder(WorldMap world) =>
        _world = world ?? throw new ArgumentNullException(nameof(world));

    public IReadOnlyList<GridPoint> FindPath(
        GridPoint start,
        GridPoint goal,
        SpeciesKind species,
        int maxExpandedNodes = 20000)
    {
        if (maxExpandedNodes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExpandedNodes));
        if (!_world.IsInside(start.X, start.Y) || !_world.IsInside(goal.X, goal.Y))
            return Array.Empty<GridPoint>();
        if (!CanTraverse(species, _world.GetTerrain(start.X, start.Y)) ||
            !CanTraverse(species, _world.GetTerrain(goal.X, goal.Y)))
            return Array.Empty<GridPoint>();
        if (start == goal)
            return new[] { start };

        int count = _world.TileCount;
        int startIndex = _world.ToIndex(start.X, start.Y);
        int goalIndex = _world.ToIndex(goal.X, goal.Y);
        var cameFrom = new int[count];
        Array.Fill(cameFrom, -1);
        var gScore = new float[count];
        Array.Fill(gScore, float.PositiveInfinity);
        var closed = new bool[count];
        var open = new PriorityQueue<int, float>();

        gScore[startIndex] = 0;
        open.Enqueue(startIndex, Heuristic(start.X, start.Y, goal.X, goal.Y));
        int expanded = 0;

        while (open.TryDequeue(out int current, out _))
        {
            if (closed[current])
                continue;
            if (current == goalIndex)
                return Reconstruct(cameFrom, current);

            closed[current] = true;
            if (++expanded > maxExpandedNodes)
                return Array.Empty<GridPoint>();

            int currentX = current % _world.Width;
            int currentY = current / _world.Width;
            foreach ((int dx, int dy, float directionCost) in Directions)
            {
                int nx = currentX + dx;
                int ny = currentY + dy;
                if (!_world.IsInside(nx, ny))
                    continue;

                TerrainType terrain = _world.GetTerrain(nx, ny);
                if (!CanTraverse(species, terrain))
                    continue;

                // Prevent diagonal movement through two blocked orthogonal corners.
                if (dx != 0 && dy != 0 &&
                    (!CanTraverse(species, _world.GetTerrain(currentX + dx, currentY)) ||
                     !CanTraverse(species, _world.GetTerrain(currentX, currentY + dy))))
                    continue;

                int neighbor = _world.ToIndex(nx, ny);
                if (closed[neighbor])
                    continue;

                float tentative = gScore[current] + directionCost * TerrainCost(species, terrain);
                if (tentative >= gScore[neighbor])
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentative;
                float priority = tentative + Heuristic(nx, ny, goal.X, goal.Y);
                open.Enqueue(neighbor, priority);
            }
        }

        return Array.Empty<GridPoint>();
    }

    public bool CanTraverse(SpeciesKind species, int x, int y) =>
        _world.IsInside(x, y) && CanTraverse(species, _world.GetTerrain(x, y));

    public static bool CanTraverse(SpeciesKind species, TerrainType terrain)
    {
        bool water = terrain is TerrainType.DeepOcean or TerrainType.ShallowWater;
        return species == SpeciesKind.Fish ? water : !water;
    }

    private static float TerrainCost(SpeciesKind species, TerrainType terrain)
    {
        if (species == SpeciesKind.Fish)
            return terrain == TerrainType.DeepOcean ? 1f : 1.15f;

        return terrain switch
        {
            TerrainType.Beach => 1.25f,
            TerrainType.Grassland => 1f,
            TerrainType.Forest => species == SpeciesKind.Grazer ? 1.15f : 1.55f,
            TerrainType.Mountain => species == SpeciesKind.Monster ? 1.7f : 3.8f,
            _ => 1000f,
        };
    }

    private static float Heuristic(int x, int y, int goalX, int goalY)
    {
        int dx = Math.Abs(x - goalX);
        int dy = Math.Abs(y - goalY);
        int diagonal = Math.Min(dx, dy);
        int straight = Math.Max(dx, dy) - diagonal;
        return diagonal * 1.41421356f + straight;
    }

    private IReadOnlyList<GridPoint> Reconstruct(int[] cameFrom, int current)
    {
        var reversed = new List<GridPoint>();
        while (current >= 0)
        {
            reversed.Add(new GridPoint(current % _world.Width, current / _world.Width));
            current = cameFrom[current];
        }
        reversed.Reverse();
        return reversed;
    }
}
