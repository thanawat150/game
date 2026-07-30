using WorldForge.Core.World;

namespace WorldForge.Core.Simulation;

public sealed partial class GrandSimulation
{
    private const int GlobalEntityLimit = 6000;

    private void UpdateLongRangeMovement()
    {
        foreach (SimEntity entity in State.Entities.Values.OrderBy(e => e.Id).ToArray())
        {
            if (!entity.IsAlive || entity.PregnancyDaysRemaining > 0 && entity.Species == SpeciesKind.Settler && entity.Energy < 20)
                continue;

            GridPoint? target = ChooseMovementTarget(entity);
            if (target is null)
                continue;

            bool targetChanged = entity.DestinationX != target.Value.X || entity.DestinationY != target.Value.Y;
            bool needsPath = targetChanged ||
                             entity.Path.Count == 0 ||
                             entity.PathIndex >= entity.Path.Count ||
                             State.Day - entity.LastPathDay >= 12;

            if (needsPath)
            {
                IReadOnlyList<GridPoint> path = _pathfinder.FindPath(
                    new GridPoint(entity.X, entity.Y),
                    target.Value,
                    entity.Species,
                    maxExpandedNodes: 14000);
                entity.Path.Clear();
                entity.Path.AddRange(path);
                entity.PathIndex = path.Count > 1 ? 1 : 0;
                entity.DestinationX = target.Value.X;
                entity.DestinationY = target.Value.Y;
                entity.LastPathDay = State.Day;
            }

            if (entity.Path.Count == 0 || entity.PathIndex >= entity.Path.Count)
                continue;

            int steps = entity.SpeedGene >= 1.08f && ((State.Day + (int)entity.Id) % 3 == 0) ? 2 : 1;
            for (int step = 0; step < steps && entity.PathIndex < entity.Path.Count; step++)
            {
                GridPoint next = entity.Path[entity.PathIndex];
                if (!_pathfinder.CanTraverse(entity.Species, next.X, next.Y))
                {
                    entity.Path.Clear();
                    entity.PathIndex = 0;
                    break;
                }

                entity.X = next.X;
                entity.Y = next.Y;
                entity.PathIndex++;
                entity.Action = entity.Species switch
                {
                    SpeciesKind.Predator => EntityAction.Hunt,
                    SpeciesKind.Settler => EntityAction.Travel,
                    SpeciesKind.Monster => EntityAction.Migrate,
                    _ => EntityAction.Travel,
                };
                entity.Energy = MathF.Max(0, entity.Energy - 0.4f);
            }
        }
    }

    private GridPoint? ChooseMovementTarget(SimEntity entity)
    {
        if (entity.Species == SpeciesKind.Predator)
        {
            SimEntity? prey = State.Entities.Values
                .Where(e => e.IsAlive && e.Species == SpeciesKind.Grazer)
                .OrderBy(e => DistanceSquared(e, entity))
                .ThenBy(e => e.Id)
                .FirstOrDefault();
            if (prey is not null && DistanceSquared(prey, entity) <= 96 * 96)
                return new GridPoint(prey.X, prey.Y);
        }

        if (entity.Species == SpeciesKind.Monster)
        {
            SettlementState? settlement = State.Settlements.Values
                .OrderBy(s => DistanceSquared(entity.X, entity.Y, s.X, s.Y))
                .ThenBy(s => s.Id)
                .FirstOrDefault();
            if (settlement is not null && DistanceSquared(entity.X, entity.Y, settlement.X, settlement.Y) <= 120 * 120)
                return new GridPoint(settlement.X, settlement.Y);
        }

        if (entity.Species == SpeciesKind.Settler &&
            entity.SettlementId is ulong settlementId &&
            State.Settlements.TryGetValue(settlementId, out SettlementState? home))
        {
            int homeDistance = DistanceSquared(entity.X, entity.Y, home.X, home.Y);
            if (homeDistance > 12 * 12)
                return new GridPoint(home.X, home.Y);

            if ((State.Day / 6 + (int)entity.Id) % 3 == 0)
                return FindRandomPassableDestination(entity, home.X, home.Y, 14);
            return new GridPoint(home.X, home.Y);
        }

        bool pathFinished = entity.Path.Count == 0 || entity.PathIndex >= entity.Path.Count;
        if (!pathFinished && State.Day - entity.LastPathDay < 12)
            return entity.DestinationX is int x && entity.DestinationY is int y ? new GridPoint(x, y) : null;

        int radius = entity.Species switch
        {
            SpeciesKind.Fish => 36,
            SpeciesKind.Grazer => 28,
            SpeciesKind.Settler => 48,
            SpeciesKind.Monster => 50,
            _ => 40,
        };
        return FindRandomPassableDestination(entity, entity.X, entity.Y, radius);
    }

    private GridPoint? FindRandomPassableDestination(SimEntity entity, int centerX, int centerY, int radius)
    {
        for (int attempt = 0; attempt < 24; attempt++)
        {
            int x = Math.Clamp(centerX + _random.NextInt(-radius, radius + 1), 0, _world.Width - 1);
            int y = Math.Clamp(centerY + _random.NextInt(-radius, radius + 1), 0, _world.Height - 1);
            if (!_pathfinder.CanTraverse(entity.Species, x, y))
                continue;

            TerrainType terrain = _world.GetTerrain(x, y);
            if (entity.Species == SpeciesKind.Grazer &&
                entity.Hunger >= 45 &&
                terrain is not (TerrainType.Grassland or TerrainType.Forest))
                continue;
            return new GridPoint(x, y);
        }
        return null;
    }

    private void UpdateReproductionAndLifeCycle()
    {
        foreach (SimEntity entity in State.Entities.Values.OrderBy(e => e.Id).ToArray())
        {
            if (!entity.IsAlive)
                continue;

            ApplyNaturalAging(entity);
            if (entity.PregnancyDaysRemaining <= 0)
                continue;

            entity.PregnancyDaysRemaining--;
            entity.Action = EntityAction.Reproduce;
            if (entity.PregnancyDaysRemaining == 0)
                CompleteBirth(entity);
        }

        if (State.Day % 7 != 0 || State.Entities.Count >= GlobalEntityLimit)
            return;

        foreach (SimEntity female in State.Entities.Values
                     .Where(IsEligibleFemale)
                     .OrderBy(e => e.Id)
                     .ToArray())
        {
            if (!CanPopulationGrow(female))
                continue;

            SimEntity? male = State.Entities.Values
                .Where(e => IsEligibleMaleFor(e, female))
                .OrderBy(e => DistanceSquared(e, female))
                .ThenBy(e => e.Id)
                .FirstOrDefault();
            if (male is null)
                continue;

            float chance = Math.Clamp(
                0.1f + (female.Fertility * female.FertilityGene + male.Fertility * male.FertilityGene) * 2.5f,
                0.1f,
                0.95f);
            if (_random.NextFloat() > chance)
                continue;

            female.MateId = male.Id;
            male.MateId = female.Id;
            female.PregnancyDaysRemaining = GestationDays(female.Species);
            female.Action = EntityAction.Reproduce;
            male.Action = EntityAction.Reproduce;
            AddEvent("family.conceived", "New life conceived", $"{female.Name} and {male.Name} are expecting offspring.", female.X, female.Y, 1, female.Id, male.Id);
        }
    }

    private void ApplyNaturalAging(SimEntity entity)
    {
        int lifespan = LifespanDays(entity.Species);
        if (entity.AgeDays <= lifespan)
            return;

        float overAge = (entity.AgeDays - lifespan) / (float)Math.Max(1, lifespan);
        float dailyRisk = Math.Clamp(0.002f + overAge * 0.03f, 0.002f, 0.2f);
        if (_random.NextFloat() < dailyRisk)
            entity.Health -= 12 / MathF.Max(0.6f, entity.VitalityGene);
    }

    private bool IsEligibleFemale(SimEntity entity) =>
        entity.IsAlive &&
        entity.Sex == BiologicalSex.Female &&
        entity.PregnancyDaysRemaining == 0 &&
        IsAdult(entity) &&
        State.Day - entity.LastBirthDay >= ReproductionCooldown(entity.Species) &&
        entity.Health >= 55 &&
        entity.Hunger <= 70 &&
        entity.Energy >= 25;

    private bool IsEligibleMaleFor(SimEntity candidate, SimEntity female) =>
        candidate.IsAlive &&
        candidate.Id != female.Id &&
        candidate.Species == female.Species &&
        candidate.Sex == BiologicalSex.Male &&
        IsAdult(candidate) &&
        candidate.Health >= 50 &&
        candidate.Hunger <= 75 &&
        candidate.Energy >= 20 &&
        DistanceSquared(candidate, female) <= 12 * 12 &&
        !AreCloseRelatives(candidate, female);

    private static bool IsAdult(SimEntity entity) => entity.AgeDays >= AdultAgeDays(entity.Species);

    private bool CanPopulationGrow(SimEntity entity)
    {
        int speciesPopulation = State.Entities.Values.Count(e => e.IsAlive && e.Species == entity.Species);
        int speciesCapacity = entity.Species switch
        {
            SpeciesKind.Fish => Math.Max(100, _world.TileCount / 16),
            SpeciesKind.Grazer => Math.Max(80, _world.TileCount / 28),
            SpeciesKind.Predator => Math.Max(24, _world.TileCount / 120),
            SpeciesKind.Monster => Math.Max(8, _world.TileCount / 700),
            _ => Math.Max(80, _world.TileCount / 50),
        };
        if (speciesPopulation >= speciesCapacity)
            return false;

        if (entity.SettlementId is ulong settlementId &&
            State.Settlements.TryGetValue(settlementId, out SettlementState? settlement))
        {
            int population = State.Entities.Values.Count(e => e.IsAlive && e.SettlementId == settlementId);
            return population < settlement.Housing && settlement.Food >= Math.Max(12, population * 1.5f);
        }
        return true;
    }

    private void CompleteBirth(SimEntity mother)
    {
        if (mother.MateId is not ulong mateId ||
            !State.Entities.TryGetValue(mateId, out SimEntity? father) ||
            !father.IsAlive ||
            !CanPopulationGrow(mother))
        {
            mother.MateId = null;
            mother.LastBirthDay = State.Day;
            return;
        }

        int litter = LitterSize(mother.Species);
        var born = new List<ulong>();
        for (int i = 0; i < litter && State.Entities.Count < GlobalEntityLimit; i++)
        {
            GridPoint position = FindBirthPosition(mother);
            SimEntity child = CreateChild(mother, father, position);
            born.Add(child.Id);
        }

        mother.LastBirthDay = State.Day;
        father.LastBirthDay = State.Day;
        mother.MateId = null;
        father.MateId = null;
        mother.Energy = MathF.Max(10, mother.Energy - 24);
        mother.Hunger = MathF.Min(100, mother.Hunger + 18);
        State.TotalBirths += born.Count;

        if (born.Count > 0)
        {
            AddEvent(
                "family.birth",
                "Birth",
                $"{mother.Name} gave birth to {born.Count} offspring.",
                mother.X,
                mother.Y,
                2,
                new[] { mother.Id, father.Id }.Concat(born).ToArray());
        }
    }

    private SimEntity CreateChild(SimEntity mother, SimEntity father, GridPoint position)
    {
        ulong id = State.NextEntityId++;
        float mutation = 0.08f;
        var child = new SimEntity
        {
            Id = id,
            Name = $"{mother.Species}-{id}",
            Species = mother.Species,
            Sex = _random.NextFloat() < 0.5f ? BiologicalSex.Female : BiologicalSex.Male,
            X = position.X,
            Y = position.Y,
            AgeDays = 0,
            Generation = Math.Max(mother.Generation, father.Generation) + 1,
            Parents = { mother.Id, father.Id },
            SettlementId = mother.SettlementId ?? father.SettlementId,
            KingdomId = mother.KingdomId ?? father.KingdomId,
            Fertility = (mother.Fertility + father.Fertility) / 2f,
            SpeedGene = MutateGene((mother.SpeedGene + father.SpeedGene) / 2f, mutation),
            VitalityGene = MutateGene((mother.VitalityGene + father.VitalityGene) / 2f, mutation),
            FertilityGene = MutateGene((mother.FertilityGene + father.FertilityGene) / 2f, mutation),
            IntelligenceGene = MutateGene((mother.IntelligenceGene + father.IntelligenceGene) / 2f, mutation),
        };
        child.Intelligence = (mother.Intelligence + father.Intelligence) * 0.35f * child.IntelligenceGene;
        child.Health = 100 * child.VitalityGene;
        child.Energy = 75;
        ApplyGeneTraits(child);
        InheritTrait(child, mother, father);
        State.Entities[id] = child;
        mother.Children.Add(id);
        father.Children.Add(id);
        return child;
    }

    private float MutateGene(float average, float range) =>
        Math.Clamp(average + (_random.NextFloat() * 2f - 1f) * range, 0.65f, 1.35f);

    private void InheritTrait(SimEntity child, SimEntity mother, SimEntity father)
    {
        string[] candidates = mother.Traits.Concat(father.Traits)
            .Where(t => !t.StartsWith("trait.blessed", StringComparison.Ordinal) &&
                        !t.StartsWith("trait.cursed", StringComparison.Ordinal))
            .Distinct()
            .ToArray();
        foreach (string trait in candidates)
        {
            if (_random.NextFloat() < 0.35f && !child.Traits.Contains(trait))
                child.Traits.Add(trait);
        }
        if (_random.NextFloat() < 0.025f)
            child.Traits.Add("trait.mutant");
    }

    private GridPoint FindBirthPosition(SimEntity mother)
    {
        for (int radius = 0; radius <= 4; radius++)
        {
            for (int y = mother.Y - radius; y <= mother.Y + radius; y++)
            {
                for (int x = mother.X - radius; x <= mother.X + radius; x++)
                {
                    if (_pathfinder.CanTraverse(mother.Species, x, y))
                        return new GridPoint(x, y);
                }
            }
        }
        return new GridPoint(mother.X, mother.Y);
    }

    private static bool AreCloseRelatives(SimEntity first, SimEntity second)
    {
        if (first.Parents.Contains(second.Id) || second.Parents.Contains(first.Id))
            return true;
        return first.Parents.Intersect(second.Parents).Any();
    }

    private static int AdultAgeDays(SpeciesKind species) => species switch
    {
        SpeciesKind.Fish => 25,
        SpeciesKind.Grazer => 100,
        SpeciesKind.Predator => 160,
        SpeciesKind.Monster => 300,
        _ => 16 * 360,
    };

    private static int GestationDays(SpeciesKind species) => species switch
    {
        SpeciesKind.Fish => 18,
        SpeciesKind.Grazer => 45,
        SpeciesKind.Predator => 60,
        SpeciesKind.Monster => 120,
        _ => 90,
    };

    private static int ReproductionCooldown(SpeciesKind species) => species switch
    {
        SpeciesKind.Fish => 24,
        SpeciesKind.Grazer => 70,
        SpeciesKind.Predator => 100,
        SpeciesKind.Monster => 220,
        _ => 180,
    };

    private int LitterSize(SpeciesKind species) => species switch
    {
        SpeciesKind.Fish => _random.NextInt(3, 7),
        SpeciesKind.Grazer => _random.NextFloat() < 0.25f ? 2 : 1,
        SpeciesKind.Predator => _random.NextFloat() < 0.2f ? 2 : 1,
        _ => 1,
    };

    private static int LifespanDays(SpeciesKind species) => species switch
    {
        SpeciesKind.Fish => 6 * 360,
        SpeciesKind.Grazer => 14 * 360,
        SpeciesKind.Predator => 18 * 360,
        SpeciesKind.Monster => 80 * 360,
        _ => 72 * 360,
    };

    private void UpdateDiplomacyPressure()
    {
        if (State.Day == 0 || State.Day % 10 != 0)
            return;

        KingdomState[] kingdoms = State.Kingdoms.Values.OrderBy(k => k.Id).ToArray();
        for (int i = 0; i < kingdoms.Length; i++)
        {
            for (int j = i + 1; j < kingdoms.Length; j++)
            {
                int relation = kingdoms[i].Relations.GetValueOrDefault(kingdoms[j].Id);
                if (relation is > 0 and < 70)
                    relation += 2;
                else if (relation <= 0 && relation > -70)
                    relation -= 25;
                SetRelation(kingdoms[i].Id, kingdoms[j].Id, relation);
            }
        }
    }

    private void UpdateArmies()
    {
        MobilizeArmiesForActiveWars();

        foreach (ArmyState army in State.Armies.Values
                     .Where(a => a.IsActive)
                     .OrderBy(a => a.Id)
                     .ToArray())
        {
            if (!State.Kingdoms.ContainsKey(army.KingdomId))
            {
                army.Status = ArmyStatus.Disbanded;
                continue;
            }

            if (!TryResolveArmyTarget(army, out SettlementState? target))
            {
                army.Status = ArmyStatus.Retreating;
                target = State.Settlements.GetValueOrDefault(army.OriginSettlementId);
                army.TargetSettlementId = target?.Id;
            }
            if (target is null)
            {
                army.Status = ArmyStatus.Disbanded;
                continue;
            }

            bool targetChanged = army.Path.Count == 0 ||
                                 army.PathIndex >= army.Path.Count ||
                                 State.Day - army.LastPathDay >= 10 ||
                                 army.Path[^1] != new GridPoint(target.X, target.Y);
            if (targetChanged)
                PlanArmyPath(army, target);

            MoveArmy(army);
            army.Supply = MathF.Max(0, army.Supply - 0.6f);
            if (army.Supply <= 0 && State.Day % 5 == 0)
            {
                army.Units--;
                army.Morale = MathF.Max(0.25f, army.Morale - 0.05f);
            }
        }

        ResolveFieldBattles();
        ResolveSieges();
    }

    private void MobilizeArmiesForActiveWars()
    {
        KingdomState[] kingdoms = State.Kingdoms.Values.OrderBy(k => k.Id).ToArray();
        for (int i = 0; i < kingdoms.Length; i++)
        {
            for (int j = i + 1; j < kingdoms.Length; j++)
            {
                if (GetRelationState(kingdoms[i].Id, kingdoms[j].Id) != RelationState.War)
                    continue;
                EnsureArmyForWar(kingdoms[i].Id, kingdoms[j].Id);
                EnsureArmyForWar(kingdoms[j].Id, kingdoms[i].Id);
            }
        }
    }

    private void EnsureArmyForWar(ulong kingdomId, ulong enemyId)
    {
        if (!State.Kingdoms.TryGetValue(kingdomId, out KingdomState? kingdom) ||
            !State.Kingdoms.TryGetValue(enemyId, out KingdomState? enemy))
            return;

        bool alreadyActive = State.Armies.Values.Any(a =>
            a.IsActive &&
            a.KingdomId == kingdomId &&
            a.TargetSettlementId is ulong targetId &&
            State.Settlements.TryGetValue(targetId, out SettlementState? target) &&
            target.KingdomId == enemyId);
        if (alreadyActive)
            return;

        int population = State.Entities.Values.Count(e => e.IsAlive && e.KingdomId == kingdomId && e.Species == SpeciesKind.Settler);
        if (population < 5 || !State.Settlements.TryGetValue(kingdom.CapitalId, out SettlementState? capital))
            return;

        SettlementState? targetSettlement = enemy.Settlements
            .Where(State.Settlements.ContainsKey)
            .Select(id => State.Settlements[id])
            .OrderBy(s => DistanceSquared(capital.X, capital.Y, s.X, s.Y))
            .ThenBy(s => s.Id)
            .FirstOrDefault();
        if (targetSettlement is null)
            return;

        int units = Math.Clamp((int)MathF.Ceiling(population * 0.72f), 6, 36);
        ulong id = State.NextArmyId++;
        var army = new ArmyState
        {
            Id = id,
            Name = $"กองทัพ {kingdom.Name} #{id}",
            KingdomId = kingdomId,
            OriginSettlementId = capital.Id,
            TargetSettlementId = targetSettlement.Id,
            X = capital.X,
            Y = capital.Y,
            Units = units,
            Morale = 0.85f + kingdom.Stability / 500f,
            Supply = 100,
            Status = ArmyStatus.Mobilizing,
        };
        State.Armies[id] = army;
        PlanArmyPath(army, targetSettlement);
        AddEvent("army.mobilized", "Army mobilized", $"{army.Name} marched toward {targetSettlement.Name}.", army.X, army.Y, 4, army.Id, kingdomId, enemyId);
    }

    private bool TryResolveArmyTarget(ArmyState army, out SettlementState? target)
    {
        target = null;
        if (army.TargetSettlementId is not ulong targetId ||
            !State.Settlements.TryGetValue(targetId, out target))
            return false;

        if (target.KingdomId is not ulong targetKingdomId ||
            !State.Kingdoms.ContainsKey(targetKingdomId) ||
            GetRelationState(army.KingdomId, targetKingdomId) != RelationState.War)
            return false;
        return true;
    }

    private void PlanArmyPath(ArmyState army, SettlementState target)
    {
        IReadOnlyList<GridPoint> path = _pathfinder.FindPath(
            new GridPoint(army.X, army.Y),
            new GridPoint(target.X, target.Y),
            SpeciesKind.Settler,
            maxExpandedNodes: 30000);
        army.Path.Clear();
        army.Path.AddRange(path);
        army.PathIndex = path.Count > 1 ? 1 : 0;
        army.LastPathDay = State.Day;
        army.Status = path.Count > 1 ? ArmyStatus.Marching : ArmyStatus.Stalled;
    }

    private void MoveArmy(ArmyState army)
    {
        if (army.Status is ArmyStatus.Disbanded or ArmyStatus.Stalled ||
            army.Path.Count == 0 ||
            army.PathIndex >= army.Path.Count)
            return;

        int movement = State.Kingdoms.TryGetValue(army.KingdomId, out KingdomState? kingdom) &&
                       kingdom.Technologies.Contains("tech.logistics")
            ? 3
            : 2;
        for (int i = 0; i < movement && army.PathIndex < army.Path.Count; i++)
        {
            GridPoint next = army.Path[army.PathIndex++];
            if (!_pathfinder.CanTraverse(SpeciesKind.Settler, next.X, next.Y))
            {
                army.Status = ArmyStatus.Stalled;
                army.Path.Clear();
                break;
            }
            army.X = next.X;
            army.Y = next.Y;
            army.Status = ArmyStatus.Marching;
        }
    }

    private void ResolveFieldBattles()
    {
        ArmyState[] active = State.Armies.Values.Where(a => a.IsActive).OrderBy(a => a.Id).ToArray();
        var resolved = new HashSet<(ulong, ulong)>();
        for (int i = 0; i < active.Length; i++)
        {
            for (int j = i + 1; j < active.Length; j++)
            {
                ArmyState first = active[i];
                ArmyState second = active[j];
                if (!first.IsActive || !second.IsActive || first.KingdomId == second.KingdomId)
                    continue;
                if (!State.Kingdoms.ContainsKey(first.KingdomId) ||
                    !State.Kingdoms.ContainsKey(second.KingdomId) ||
                    GetRelationState(first.KingdomId, second.KingdomId) != RelationState.War)
                    continue;
                if (DistanceSquared(first.X, first.Y, second.X, second.Y) > 2)
                    continue;
                (ulong, ulong) key = first.Id < second.Id ? (first.Id, second.Id) : (second.Id, first.Id);
                if (!resolved.Add(key))
                    continue;
                ResolveArmyClash(first, second);
            }
        }
    }

    private void ResolveArmyClash(ArmyState first, ArmyState second)
    {
        float firstPower = ArmyCombatPower(first);
        float secondPower = ArmyCombatPower(second);
        float firstRoll = 0.82f + _random.NextFloat() * 0.36f;
        float secondRoll = 0.82f + _random.NextFloat() * 0.36f;
        bool firstWins = firstPower * firstRoll >= secondPower * secondRoll;
        ArmyState winner = firstWins ? first : second;
        ArmyState loser = firstWins ? second : first;

        int winnerLoss = Math.Max(1, (int)MathF.Round(loser.Units * (0.18f + _random.NextFloat() * 0.18f)));
        int loserLoss = Math.Max(1, (int)MathF.Round(loser.Units * (0.55f + _random.NextFloat() * 0.35f)));
        winner.Units = Math.Max(1, winner.Units - winnerLoss);
        loser.Units = Math.Max(0, loser.Units - loserLoss);
        winner.Morale = MathF.Min(1.3f, winner.Morale + 0.08f);
        loser.Morale = MathF.Max(0.2f, loser.Morale - 0.2f);
        winner.LastBattleDay = State.Day;
        loser.LastBattleDay = State.Day;
        State.TotalBattles++;

        InflictPopulationCasualties(loser.KingdomId, Math.Max(1, loserLoss / 3));
        if (loser.Units <= 1)
            loser.Status = ArmyStatus.Disbanded;
        else
            SendArmyHome(loser);

        AddEvent("battle.field", "Field battle", $"{winner.Name} defeated {loser.Name}.", winner.X, winner.Y, 5, winner.Id, loser.Id);
    }

    private float ArmyCombatPower(ArmyState army)
    {
        float technology = 1;
        if (State.Kingdoms.TryGetValue(army.KingdomId, out KingdomState? kingdom))
        {
            if (kingdom.Technologies.Contains("tech.metallurgy"))
                technology += 0.18f;
            if (kingdom.Technologies.Contains("tech.siegecraft"))
                technology += 0.12f;
        }
        return army.Units * army.Morale * technology * (0.65f + army.Supply / 285f);
    }

    private void ResolveSieges()
    {
        foreach (ArmyState army in State.Armies.Values.Where(a => a.IsActive).OrderBy(a => a.Id).ToArray())
        {
            if (State.Day - army.LastBattleDay < 4 ||
                army.TargetSettlementId is not ulong targetId ||
                !State.Settlements.TryGetValue(targetId, out SettlementState? target) ||
                target.KingdomId is not ulong defenderKingdomId ||
                defenderKingdomId == army.KingdomId ||
                DistanceSquared(army.X, army.Y, target.X, target.Y) > 2)
                continue;

            army.Status = ArmyStatus.Besieging;
            int defenders = State.Entities.Values.Count(e =>
                e.IsAlive &&
                e.Species == SpeciesKind.Settler &&
                e.SettlementId == target.Id);
            int nearbyArmyUnits = State.Armies.Values
                .Where(a => a.IsActive &&
                            a.KingdomId == defenderKingdomId &&
                            DistanceSquared(a.X, a.Y, target.X, target.Y) <= 4 * 4)
                .Sum(a => a.Units);

            float attack = ArmyCombatPower(army) * (0.9f + _random.NextFloat() * 0.3f);
            float defense = (defenders * 0.35f + nearbyArmyUnits * 0.75f + target.Fortification * 1.5f) *
                            (0.88f + _random.NextFloat() * 0.3f);
            army.LastBattleDay = State.Day;
            State.TotalBattles++;

            if (attack >= defense)
            {
                int losses = Math.Max(1, (int)MathF.Round(defense * 0.28f));
                army.Units = Math.Max(1, army.Units - losses);
                CaptureSettlement(army, target, defenderKingdomId);
            }
            else
            {
                int losses = Math.Max(2, (int)MathF.Round(army.Units * (0.3f + _random.NextFloat() * 0.3f)));
                army.Units = Math.Max(0, army.Units - losses);
                army.Morale = MathF.Max(0.2f, army.Morale - 0.25f);
                InflictPopulationCasualties(army.KingdomId, Math.Max(1, losses / 4));
                AddEvent("battle.siege_failed", "Siege repelled", $"{target.Name} repelled {army.Name}.", target.X, target.Y, 5, army.Id, target.Id);
                if (army.Units <= 2)
                    army.Status = ArmyStatus.Disbanded;
                else
                    SendArmyHome(army);
            }
        }
    }

    private void CaptureSettlement(ArmyState army, SettlementState target, ulong defenderKingdomId)
    {
        if (!State.Kingdoms.TryGetValue(army.KingdomId, out KingdomState? attacker) ||
            !State.Kingdoms.TryGetValue(defenderKingdomId, out KingdomState? defender))
            return;

        defender.Settlements.Remove(target.Id);
        attacker.Settlements.Add(target.Id);
        target.KingdomId = attacker.Id;
        target.Happiness = MathF.Max(10, target.Happiness - 25);
        target.Fortification = Math.Max(0, target.Fortification - 1);
        foreach (SimEntity citizen in State.Entities.Values.Where(e => e.SettlementId == target.Id))
        {
            citizen.KingdomId = attacker.Id;
            citizen.Morale = MathF.Max(10, citizen.Morale - 15);
        }
        State.TotalCitiesCaptured++;
        AddEvent("city.captured", "City captured", $"{attacker.Name} captured {target.Name} from {defender.Name}.", target.X, target.Y, 6, army.Id, target.Id, attacker.Id, defender.Id);

        if (defender.CapitalId == target.Id)
        {
            ulong replacement = defender.Settlements.FirstOrDefault();
            if (replacement != 0)
            {
                defender.CapitalId = replacement;
                State.Settlements[replacement].Stage = SettlementStage.Capital;
            }
        }

        army.TargetSettlementId = FindNearestEnemySettlement(attacker.Id, defender.Id, army.X, army.Y)?.Id;
        army.Path.Clear();
        army.PathIndex = 0;
        army.Supply = MathF.Min(100, army.Supply + 35);
        army.Morale = MathF.Min(1.3f, army.Morale + 0.15f);

        if (defender.Settlements.Count == 0)
            CollapseKingdom(defender.Id, attacker.Id);
    }

    private SettlementState? FindNearestEnemySettlement(ulong attackerId, ulong defenderId, int x, int y)
    {
        if (!State.Kingdoms.TryGetValue(defenderId, out KingdomState? defender))
            return null;
        return defender.Settlements
            .Where(State.Settlements.ContainsKey)
            .Select(id => State.Settlements[id])
            .OrderBy(s => DistanceSquared(x, y, s.X, s.Y))
            .ThenBy(s => s.Id)
            .FirstOrDefault();
    }

    private void CollapseKingdom(ulong collapsedId, ulong conquerorId)
    {
        if (!State.Kingdoms.TryGetValue(collapsedId, out KingdomState? collapsed))
            return;

        foreach (ArmyState army in State.Armies.Values.Where(a => a.KingdomId == collapsedId))
            army.Status = ArmyStatus.Disbanded;
        foreach (KingdomState kingdom in State.Kingdoms.Values)
            kingdom.Relations.Remove(collapsedId);
        State.Kingdoms.Remove(collapsedId);

        SettlementState? location = State.Kingdoms.TryGetValue(conquerorId, out KingdomState? conqueror) &&
                                    State.Settlements.TryGetValue(conqueror.CapitalId, out SettlementState? capital)
            ? capital
            : State.Settlements.Values.FirstOrDefault();
        AddEvent("kingdom.collapsed", "Kingdom collapsed", $"{collapsed.Name} ceased to exist.", location?.X ?? 0, location?.Y ?? 0, 6, collapsedId, conquerorId);
    }

    private void SendArmyHome(ArmyState army)
    {
        army.TargetSettlementId = army.OriginSettlementId;
        army.Path.Clear();
        army.PathIndex = 0;
        army.Status = ArmyStatus.Retreating;
    }

    private void InflictPopulationCasualties(ulong kingdomId, int count)
    {
        foreach (SimEntity victim in State.Entities.Values
                     .Where(e => e.IsAlive && e.KingdomId == kingdomId && e.Species == SpeciesKind.Settler)
                     .OrderBy(e => e.Health)
                     .ThenBy(e => e.Id)
                     .Take(count))
        {
            victim.Health -= 35;
        }
    }
}
