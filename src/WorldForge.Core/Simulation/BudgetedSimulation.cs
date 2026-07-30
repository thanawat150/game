using WorldForge.Core.World;

namespace WorldForge.Core.Simulation;

public enum SimulationPerformanceProfile
{
    Economy,
    Balanced,
    Detailed,
    Custom,
}

public sealed class SimulationBudgetOptions
{
    public SimulationPerformanceProfile Profile { get; set; } = SimulationPerformanceProfile.Balanced;
    public int MaxPopulation { get; set; } = 1200;
    public int EntityAiUpdatesPerDay { get; set; } = 120;
    public int PathRequestsPerDay { get; set; } = 12;
    public int MaxExpandedNodesPerPath { get; set; } = 5000;
    public int ReproductionIntervalDays { get; set; } = 14;
    public int ReproductionChecksPerCycle { get; set; } = 18;
    public int DiseaseIntervalDays { get; set; } = 2;
    public int ArmyIntervalDays { get; set; } = 2;
    public bool EnableReproduction { get; set; } = true;
    public bool EnableAutomaticDiplomacy { get; set; } = true;
    public bool EnableArmies { get; set; } = true;

    public static SimulationBudgetOptions ForProfile(SimulationPerformanceProfile profile, int maxPopulation)
    {
        maxPopulation = Math.Clamp(maxPopulation, 25, 6000);
        return profile switch
        {
            SimulationPerformanceProfile.Economy => new SimulationBudgetOptions
            {
                Profile = profile,
                MaxPopulation = maxPopulation,
                EntityAiUpdatesPerDay = 55,
                PathRequestsPerDay = 5,
                MaxExpandedNodesPerPath = 2600,
                ReproductionIntervalDays = 24,
                ReproductionChecksPerCycle = 8,
                DiseaseIntervalDays = 4,
                ArmyIntervalDays = 3,
            },
            SimulationPerformanceProfile.Detailed => new SimulationBudgetOptions
            {
                Profile = profile,
                MaxPopulation = maxPopulation,
                EntityAiUpdatesPerDay = 260,
                PathRequestsPerDay = 28,
                MaxExpandedNodesPerPath = 9000,
                ReproductionIntervalDays = 7,
                ReproductionChecksPerCycle = 36,
                DiseaseIntervalDays = 1,
                ArmyIntervalDays = 1,
            },
            _ => new SimulationBudgetOptions
            {
                Profile = SimulationPerformanceProfile.Balanced,
                MaxPopulation = maxPopulation,
                EntityAiUpdatesPerDay = 120,
                PathRequestsPerDay = 12,
                MaxExpandedNodesPerPath = 5000,
                ReproductionIntervalDays = 14,
                ReproductionChecksPerCycle = 18,
                DiseaseIntervalDays = 2,
                ArmyIntervalDays = 2,
            },
        };
    }
}

public sealed record SimulationBudgetMetrics(
    int LivingEntities,
    int AiEntitiesUpdated,
    int PathRequestsUsed,
    int BirthsThisDay,
    int RemovedByPopulationCap);

public sealed partial class GrandSimulation
{
    private int _budgetEntityCursor;
    private int _budgetReproductionCursor;

    public SimulationBudgetMetrics LastBudgetMetrics { get; private set; } = new(0, 0, 0, 0, 0);

    public void AdvanceDayBudgeted(SimulationBudgetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MaxPopulation = Math.Clamp(options.MaxPopulation, 25, 6000);
        options.EntityAiUpdatesPerDay = Math.Clamp(options.EntityAiUpdatesPerDay, 1, 2000);
        options.PathRequestsPerDay = Math.Clamp(options.PathRequestsPerDay, 0, 200);
        options.MaxExpandedNodesPerPath = Math.Clamp(options.MaxExpandedNodesPerPath, 500, 30000);
        options.ReproductionIntervalDays = Math.Clamp(options.ReproductionIntervalDays, 1, 365);
        options.ReproductionChecksPerCycle = Math.Clamp(options.ReproductionChecksPerCycle, 0, 500);
        options.DiseaseIntervalDays = Math.Clamp(options.DiseaseIntervalDays, 1, 30);
        options.ArmyIntervalDays = Math.Clamp(options.ArmyIntervalDays, 1, 30);

        State.Tick++;
        State.Day++;

        SimEntity[] living = State.Entities.Values.Where(e => e.IsAlive).OrderBy(e => e.Id).ToArray();
        AdvanceLightweightLifeState(living);
        var spatial = new EntitySpatialIndex(living, 16);
        int pathRequests = 0;
        int aiUpdated = AdvanceBudgetedEntityAi(living, spatial, options, ref pathRequests);
        int birthsBefore = checked((int)Math.Min(int.MaxValue, State.TotalBirths));

        if (options.EnableReproduction)
            AdvanceBudgetedReproduction(living, spatial, options);
        else
            CancelPregnancies(living);

        if (State.Day % options.DiseaseIntervalDays == 0)
            UpdateDiseases();
        if (options.EnableAutomaticDiplomacy && State.Day % 10 == 0)
            UpdateDiplomacyPressure();
        if (options.EnableArmies && State.Day % options.ArmyIntervalDays == 0)
            UpdateArmies();

        CleanupDead();
        int removed = EnforcePopulationLimit(options.MaxPopulation);
        if (State.Day % 30 == 0)
            AdvanceMonth();

        int birthsAfter = checked((int)Math.Min(int.MaxValue, State.TotalBirths));
        LastBudgetMetrics = new SimulationBudgetMetrics(
            State.Entities.Values.Count(e => e.IsAlive),
            aiUpdated,
            pathRequests,
            Math.Max(0, birthsAfter - birthsBefore),
            removed);
    }

    private void AdvanceLightweightLifeState(IEnumerable<SimEntity> living)
    {
        foreach (SimEntity entity in living)
        {
            entity.AgeDays++;
            entity.Hunger = MathF.Min(100, entity.Hunger + HungerPerDay(entity.Species));
            entity.Energy = MathF.Min(100, MathF.Max(0, entity.Energy - 0.35f) + 0.55f);
            ApplyNaturalAging(entity);
            if (entity.PregnancyDaysRemaining <= 0)
                continue;
            entity.PregnancyDaysRemaining--;
            entity.Action = EntityAction.Reproduce;
        }
    }

    private int AdvanceBudgetedEntityAi(SimEntity[] living, EntitySpatialIndex spatial, SimulationBudgetOptions options, ref int pathRequests)
    {
        if (living.Length == 0)
            return 0;
        int count = Math.Min(living.Length, options.EntityAiUpdatesPerDay);
        for (int offset = 0; offset < count; offset++)
        {
            int index = (_budgetEntityCursor + offset) % living.Length;
            SimEntity entity = living[index];
            if (!entity.IsAlive)
                continue;

            if (entity.Hunger >= 75 && TryConsumeFood(entity))
            {
                entity.Action = EntityAction.Eat;
                continue;
            }

            if (entity.Species == SpeciesKind.Predator)
            {
                SimEntity? closePrey = spatial.FindNearest(SpeciesKind.Grazer, entity.X, entity.Y, 2, candidate => candidate.IsAlive);
                if (closePrey is not null)
                {
                    closePrey.Health -= 35 * MathF.Max(0.8f, entity.VitalityGene);
                    entity.Hunger = MathF.Max(0, entity.Hunger - 50);
                    entity.Action = EntityAction.Hunt;
                    continue;
                }
            }

            GridPoint? target = ChooseBudgetedTarget(entity, spatial);
            if (target is null)
                continue;

            bool targetChanged = entity.DestinationX != target.Value.X || entity.DestinationY != target.Value.Y;
            bool pathExpired = State.Day - entity.LastPathDay >= 30;
            bool pathMissing = entity.Path.Count == 0 || entity.PathIndex >= entity.Path.Count;
            if ((targetChanged || pathExpired || pathMissing) && pathRequests < options.PathRequestsPerDay)
            {
                IReadOnlyList<GridPoint> path = _pathfinder.FindPath(
                    new GridPoint(entity.X, entity.Y),
                    target.Value,
                    entity.Species,
                    options.MaxExpandedNodesPerPath);
                entity.Path.Clear();
                entity.Path.AddRange(path);
                entity.PathIndex = path.Count > 1 ? 1 : 0;
                entity.DestinationX = target.Value.X;
                entity.DestinationY = target.Value.Y;
                entity.LastPathDay = State.Day;
                pathRequests++;
            }

            MoveEntityAlongCachedPath(entity);
            if (entity.Species == SpeciesKind.Settler)
                GatherForSettlement(entity);
        }
        _budgetEntityCursor = (_budgetEntityCursor + count) % living.Length;
        return count;
    }

    private GridPoint? ChooseBudgetedTarget(SimEntity entity, EntitySpatialIndex spatial)
    {
        if (entity.Species == SpeciesKind.Predator)
        {
            SimEntity? prey = spatial.FindNearest(SpeciesKind.Grazer, entity.X, entity.Y, 96, candidate => candidate.IsAlive);
            if (prey is not null)
                return new GridPoint(prey.X, prey.Y);
        }

        if (entity.Species == SpeciesKind.Monster)
        {
            SettlementState? city = State.Settlements.Values
                .OrderBy(s => DistanceSquared(entity.X, entity.Y, s.X, s.Y))
                .ThenBy(s => s.Id)
                .FirstOrDefault();
            if (city is not null && DistanceSquared(entity.X, entity.Y, city.X, city.Y) <= 120 * 120)
                return new GridPoint(city.X, city.Y);
        }

        if (entity.Species == SpeciesKind.Settler && entity.SettlementId is ulong settlementId && State.Settlements.TryGetValue(settlementId, out SettlementState? home))
        {
            if (DistanceSquared(entity.X, entity.Y, home.X, home.Y) > 14 * 14)
                return new GridPoint(home.X, home.Y);
            if ((State.Day + (int)(entity.Id % 17)) % 12 == 0)
                return FindRandomPassableDestination(entity, home.X, home.Y, 10);
            return new GridPoint(home.X, home.Y);
        }

        if (entity.Path.Count > 0 && entity.PathIndex < entity.Path.Count && entity.DestinationX is int currentX && entity.DestinationY is int currentY)
            return new GridPoint(currentX, currentY);

        int radius = entity.Species switch
        {
            SpeciesKind.Fish => 22,
            SpeciesKind.Grazer => 18,
            SpeciesKind.Monster => 32,
            _ => 24,
        };
        return FindRandomPassableDestination(entity, entity.X, entity.Y, radius);
    }

    private void MoveEntityAlongCachedPath(SimEntity entity)
    {
        if (entity.Path.Count == 0 || entity.PathIndex >= entity.Path.Count)
            return;
        int steps = entity.SpeedGene >= 1.12f && (State.Day + (int)(entity.Id % 7)) % 4 == 0 ? 2 : 1;
        for (int i = 0; i < steps && entity.PathIndex < entity.Path.Count; i++)
        {
            GridPoint next = entity.Path[entity.PathIndex];
            if (!_pathfinder.CanTraverse(entity.Species, next.X, next.Y))
            {
                entity.Path.Clear();
                entity.PathIndex = 0;
                return;
            }
            entity.X = next.X;
            entity.Y = next.Y;
            entity.PathIndex++;
            entity.Action = entity.Species == SpeciesKind.Predator ? EntityAction.Hunt : EntityAction.Travel;
        }
    }

    private void AdvanceBudgetedReproduction(SimEntity[] living, EntitySpatialIndex spatial, SimulationBudgetOptions options)
    {
        foreach (SimEntity mother in living.Where(e => e.PregnancyDaysRemaining == 0 && e.MateId is not null).ToArray())
        {
            if (State.Entities.Count >= options.MaxPopulation)
            {
                mother.MateId = null;
                mother.LastBirthDay = State.Day;
                continue;
            }
            CompleteBirth(mother);
        }

        if (State.Day % options.ReproductionIntervalDays != 0 || State.Entities.Count >= options.MaxPopulation || options.ReproductionChecksPerCycle <= 0)
            return;

        SimEntity[] females = living.Where(IsEligibleFemale).OrderBy(e => e.Id).ToArray();
        if (females.Length == 0)
            return;
        int checks = Math.Min(females.Length, options.ReproductionChecksPerCycle);
        for (int offset = 0; offset < checks && State.Entities.Count < options.MaxPopulation; offset++)
        {
            SimEntity female = females[(_budgetReproductionCursor + offset) % females.Length];
            SimEntity? male = spatial.FindNearest(female.Species, female.X, female.Y, 12, candidate => IsEligibleMaleFor(candidate, female));
            if (male is null)
                continue;
            float chance = Math.Clamp(
                0.08f + (female.Fertility * female.FertilityGene + male.Fertility * male.FertilityGene) * 2f,
                0.08f,
                0.75f);
            if (_random.NextFloat() > chance)
                continue;
            female.MateId = male.Id;
            male.MateId = female.Id;
            female.PregnancyDaysRemaining = GestationDays(female.Species);
            female.Action = EntityAction.Reproduce;
            male.Action = EntityAction.Reproduce;
            AddEvent("family.conceived", "New life conceived", $"{female.Name} and {male.Name} are expecting offspring.", female.X, female.Y, 1, female.Id, male.Id);
        }
        _budgetReproductionCursor = (_budgetReproductionCursor + checks) % females.Length;
    }

    private static void CancelPregnancies(IEnumerable<SimEntity> living)
    {
        foreach (SimEntity entity in living)
        {
            entity.PregnancyDaysRemaining = 0;
            entity.MateId = null;
        }
    }

    private int EnforcePopulationLimit(int maxPopulation)
    {
        int overflow = State.Entities.Count - maxPopulation;
        if (overflow <= 0)
            return 0;

        SimEntity[] candidates = State.Entities.Values
            .Where(e => e.Species != SpeciesKind.Settler || e.AgeDays < AdultAgeDays(SpeciesKind.Settler))
            .OrderBy(e => e.AgeDays)
            .ThenByDescending(e => e.Id)
            .Take(overflow)
            .ToArray();
        if (candidates.Length < overflow)
        {
            candidates = State.Entities.Values
                .OrderBy(e => e.AgeDays)
                .ThenByDescending(e => e.Id)
                .Take(overflow)
                .ToArray();
        }
        foreach (SimEntity entity in candidates)
        {
            State.Entities.Remove(entity.Id);
            foreach (DiseaseState disease in State.Diseases)
                disease.InfectedDays.Remove(entity.Id);
        }
        return candidates.Length;
    }
}

internal sealed class EntitySpatialIndex
{
    private readonly int _cellSize;
    private readonly Dictionary<(SpeciesKind Species, int X, int Y), List<SimEntity>> _cells = new();

    public EntitySpatialIndex(IEnumerable<SimEntity> entities, int cellSize)
    {
        _cellSize = Math.Max(4, cellSize);
        foreach (SimEntity entity in entities)
        {
            var key = (entity.Species, FloorCell(entity.X), FloorCell(entity.Y));
            if (!_cells.TryGetValue(key, out List<SimEntity>? bucket))
            {
                bucket = new List<SimEntity>();
                _cells[key] = bucket;
            }
            bucket.Add(entity);
        }
    }

    public SimEntity? FindNearest(SpeciesKind species, int x, int y, int maxDistance, Func<SimEntity, bool>? predicate = null)
    {
        int centerX = FloorCell(x);
        int centerY = FloorCell(y);
        int cellRadius = (int)Math.Ceiling(maxDistance / (double)_cellSize);
        int maxDistanceSquared = maxDistance * maxDistance;
        SimEntity? best = null;
        int bestDistance = int.MaxValue;

        for (int cellY = centerY - cellRadius; cellY <= centerY + cellRadius; cellY++)
        {
            for (int cellX = centerX - cellRadius; cellX <= centerX + cellRadius; cellX++)
            {
                if (!_cells.TryGetValue((species, cellX, cellY), out List<SimEntity>? bucket))
                    continue;
                foreach (SimEntity candidate in bucket)
                {
                    if (predicate is not null && !predicate(candidate))
                        continue;
                    int dx = candidate.X - x;
                    int dy = candidate.Y - y;
                    int distance = dx * dx + dy * dy;
                    if (distance > maxDistanceSquared || distance >= bestDistance)
                        continue;
                    best = candidate;
                    bestDistance = distance;
                }
            }
        }
        return best;
    }

    private int FloorCell(int value) => value >= 0 ? value / _cellSize : (value - _cellSize + 1) / _cellSize;
}
