using WorldForge.Core.World;

namespace WorldForge.Core.Simulation;

public sealed partial class WorldExpansionDirector
{
    private void PromoteInitialLegends()
    {
        foreach (KingdomState kingdom in _simulation.State.Kingdoms.Values.OrderBy(k => k.Id))
        {
            if (kingdom.RulerId is ulong rulerId && _simulation.State.Entities.TryGetValue(rulerId, out SimEntity? ruler))
                PromoteLegend(ruler, LegendRole.Ruler, 35);
        }

        int targetCount = Math.Max(2, _simulation.State.Kingdoms.Count * 3);
        foreach (SimEntity entity in _simulation.State.Entities.Values
                     .Where(e => e.IsAlive && e.Species == SpeciesKind.Settler && e.AgeDays >= 18 * 360)
                     .OrderByDescending(e => e.Intelligence + e.Morale + e.Health * 0.2f)
                     .ThenBy(e => e.Id)
                     .Take(targetCount))
        {
            int threshold = Math.Clamp((int)(18 * State.ModRules.LegendPromotionMultiplier), 5, 80);
            if (Math.Abs(StableHash(entity.Id, State.Seed) % 100) >= threshold)
                continue;
            CitizenLifeProfile? life = _living.State.Citizens.GetValueOrDefault(entity.Id);
            LegendRole role = life?.Job switch
            {
                CitizenJob.Scholar => LegendRole.Scholar,
                CitizenJob.Healer => LegendRole.Healer,
                CitizenJob.Priest => LegendRole.Priest,
                CitizenJob.Soldier or CitizenJob.Guard => LegendRole.General,
                CitizenJob.Ruler => LegendRole.Ruler,
                _ => entity.Intelligence >= 18 ? LegendRole.Explorer : LegendRole.Hero,
            };
            PromoteLegend(entity, role, 12);
        }
    }

    private LegendProfile PromoteLegend(SimEntity entity, LegendRole role, int startingFame)
    {
        if (State.Legends.TryGetValue(entity.Id, out LegendProfile? existing))
        {
            if (existing.Role == LegendRole.None) existing.Role = role;
            existing.Fame = Math.Max(existing.Fame, startingFame);
            UpdateLegendTitle(existing);
            return existing;
        }

        var random = CreateDayRandom(unchecked((int)entity.Id));
        PersonalityTrait[] traits = Enum.GetValues<PersonalityTrait>();
        var selectedTraits = new HashSet<PersonalityTrait>();
        while (selectedTraits.Count < 3)
            selectedTraits.Add(traits[random.Next(traits.Length)]);
        LifeGoal[] goals = Enum.GetValues<LifeGoal>();
        var legend = new LegendProfile
        {
            EntityId = entity.Id,
            Race = RaceForEntity(entity.Id),
            Role = role,
            Fame = startingFame,
            BirthDay = _simulation.State.Day - entity.AgeDays,
            Goal = goals[Math.Abs(StableHash(entity.Id + 17, State.Seed)) % goals.Length],
            Traits = selectedTraits.ToList(),
            KnownChildren = entity.Children.Count,
        };
        State.Legends[entity.Id] = legend;
        AddMemory(legend, MemoryKind.Birth, $"Born into {legend.Race} culture.", entity.X, entity.Y, null, 1);
        foreach (ulong parent in entity.Parents)
            SetRelationship(legend, parent, RelationshipKind.Family, 85);
        foreach (ulong child in entity.Children)
            SetRelationship(legend, child, RelationshipKind.Family, 90);
        if (entity.MateId is ulong mate)
            SetRelationship(legend, mate, RelationshipKind.Partner, 95);
        UpdateLegendTitle(legend);
        AddChronicle("legend.rises", "ตำนานถือกำเนิด", $"{entity.Name} เริ่มเป็นที่กล่าวขานในฐานะ {legend.Title}", entity.X, entity.Y, 2, entity.Id);
        return legend;
    }

    private void UpdateLegendLives()
    {
        foreach (LegendProfile legend in State.Legends.Values.OrderBy(l => l.EntityId).ToArray())
        {
            SimEntity? entity = _simulation.State.Entities.GetValueOrDefault(legend.EntityId);
            if (entity is null || !entity.IsAlive)
            {
                HandleLegendDeath(legend, entity);
                continue;
            }
            if (_simulation.State.Day - legend.LastEvaluatedDay < 15)
                continue;

            legend.LastEvaluatedDay = _simulation.State.Day;
            CitizenLifeProfile? life = _living.State.Citizens.GetValueOrDefault(entity.Id);
            int fameGain = 0;
            if (life?.Activity is DailyActivity.Working or DailyActivity.Patrolling or DailyActivity.Trading) fameGain++;
            if (entity.Morale > 80) fameGain++;
            if (entity.Health < 35 && legend.Traits.Contains(PersonalityTrait.Brave)) fameGain++;
            if (life?.Job is CitizenJob.Ruler or CitizenJob.Scholar or CitizenJob.Priest) fameGain++;
            legend.Fame = Math.Min(1000, legend.Fame + fameGain);

            if (entity.Children.Count > legend.KnownChildren)
            {
                foreach (ulong child in entity.Children.Skip(legend.KnownChildren))
                {
                    SetRelationship(legend, child, RelationshipKind.Family, 92);
                    AddMemory(legend, MemoryKind.ChildBorn, "A child joined the family line.", entity.X, entity.Y, child, 3);
                }
                legend.KnownChildren = entity.Children.Count;
            }
            if (entity.MateId is ulong mateId && !legend.Relationships.ContainsKey(mateId))
            {
                SetRelationship(legend, mateId, RelationshipKind.Partner, 90);
                AddMemory(legend, MemoryKind.Marriage, "Formed a lifelong partnership.", entity.X, entity.Y, mateId, 4);
            }
            BuildSocialRelationships(legend, entity);
            UpdateLegendTitle(legend);
        }

        if (_simulation.State.TotalBattles <= State.LastBattles)
            return;
        int newBattles = (int)Math.Min(20, _simulation.State.TotalBattles - State.LastBattles);
        State.LastBattles = _simulation.State.TotalBattles;
        foreach (LegendProfile warrior in State.Legends.Values
                     .Where(l => !l.IsDead && l.Role is LegendRole.General or LegendRole.Hero)
                     .OrderByDescending(l => l.Fame)
                     .Take(3))
        {
            warrior.Battles += newBattles;
            warrior.Fame += newBattles * 3;
            SimEntity? entity = _simulation.State.Entities.GetValueOrDefault(warrior.EntityId);
            AddMemory(warrior, MemoryKind.Battle, $"Survived {newBattles} major battle reports.", entity?.X ?? 0, entity?.Y ?? 0, null, 5);
        }
    }

    private void HandleLegendDeath(LegendProfile legend, SimEntity? entity)
    {
        if (legend.IsDead) return;
        legend.IsDead = true;
        legend.DeathDay = _simulation.State.Day;
        legend.Legacy += Math.Max(5, legend.Fame / 3);
        AddMemory(legend, MemoryKind.Death, "Their life ended, but their story remained.", entity?.X ?? 0, entity?.Y ?? 0, null, 8);
        string record = $"ปี {_simulation.State.Year}: {DisplayLegendName(legend)} สิ้นชีวิต ทิ้งมรดก {legend.Legacy}";
        State.WorldLegends.Add(record);
        AddChronicle("legend.dies", "การจากไปของบุคคลสำคัญ", record, entity?.X ?? 0, entity?.Y ?? 0, 3, legend.EntityId);
        TryCreateMemorialForLegend(legend, entity);
    }

    private void BuildSocialRelationships(LegendProfile legend, SimEntity entity)
    {
        foreach (SimEntity other in _simulation.State.Entities.Values
                     .Where(other => other.IsAlive && other.Id != entity.Id && other.Species == SpeciesKind.Settler && other.SettlementId == entity.SettlementId)
                     .OrderBy(other => DistanceSquared(entity.X, entity.Y, other.X, other.Y))
                     .ThenBy(other => other.Id)
                     .Take(3))
        {
            if (legend.Relationships.ContainsKey(other.Id)) continue;
            int hash = Math.Abs(StableHash(entity.Id ^ other.Id, State.Seed));
            int roll = hash % 10;
            RelationshipKind kind = roll switch
            {
                0 => RelationshipKind.Rival,
                1 when legend.Traits.Contains(PersonalityTrait.Cruel) => RelationshipKind.Enemy,
                2 when other.Intelligence > entity.Intelligence => RelationshipKind.Mentor,
                _ => RelationshipKind.Friend,
            };
            int strength = kind is RelationshipKind.Enemy or RelationshipKind.Rival
                ? -35 - hash % 35
                : 35 + hash % 45;
            SetRelationship(legend, other.Id, kind, strength);
        }
    }

    private void SetRelationship(LegendProfile legend, ulong otherId, RelationshipKind kind, int strength)
    {
        legend.Relationships[otherId] = new LegendRelationship
        {
            OtherEntityId = otherId,
            Kind = kind,
            Strength = strength,
            LastChangedDay = _simulation.State.Day,
        };
    }

    private void TryCreateMemorialForLegend(LegendProfile legend, SimEntity? entity)
    {
        if (entity?.SettlementId is not ulong cityId || !_simulation.State.Settlements.TryGetValue(cityId, out SettlementState? city))
            return;
        if (legend.Fame < 50 || city.Stone < 12) return;
        CityDistrictState district = EnsureCityDistrict(city);
        PlaceBuilding(district, city, BuildingKind.Monument, immediate: legend.Fame >= 150);
        legend.Monuments++;
    }

    private void UpdateLegendTitle(LegendProfile legend)
    {
        legend.Title = legend.Role switch
        {
            LegendRole.Ruler => "ผู้ครองบัลลังก์",
            LegendRole.General => "แม่ทัพ",
            LegendRole.Scholar => "นักปราชญ์",
            LegendRole.Healer => "ผู้เยียวยา",
            LegendRole.Priest => "ผู้นำศรัทธา",
            LegendRole.Explorer => "นักสำรวจ",
            LegendRole.Villain => "ผู้สร้างความหวาดกลัว",
            _ => "วีรชน",
        };
        legend.Epithet = legend.Fame switch
        {
            >= 400 => "ผู้เป็นอมตะในหน้าประวัติศาสตร์",
            >= 220 => "ผู้ยิ่งใหญ่",
            >= 100 => "ผู้เลื่องชื่อ",
            >= 50 => "ผู้เป็นที่กล่าวขาน",
            _ => string.Empty,
        };
        if (legend.Traits.Contains(PersonalityTrait.Kind) && legend.LivesSaved > 0) legend.Epithet = "ผู้เปี่ยมเมตตา";
        if (legend.Battles >= 5) legend.Epithet = "ผู้ผ่านศึกนับครั้งไม่ถ้วน";
        if (legend.Discoveries >= 3) legend.Epithet = "ผู้เปิดม่านแห่งความลับ";
    }

    public string DisplayLegendName(LegendProfile legend)
    {
        string name = _simulation.State.Entities.GetValueOrDefault(legend.EntityId)?.Name ?? $"บุคคล #{legend.EntityId}";
        return string.IsNullOrEmpty(legend.Epithet) ? $"{name} — {legend.Title}" : $"{name} — {legend.Epithet}";
    }

    private void AddMemory(LegendProfile legend, MemoryKind kind, string summary, int x, int y, ulong? other, int weight)
    {
        legend.Memories.Add(new LegendMemory
        {
            Day = _simulation.State.Day,
            Kind = kind,
            Summary = summary,
            X = x,
            Y = y,
            OtherEntityId = other,
            Weight = weight,
        });
        legend.Legacy += Math.Max(0, weight - 1);
    }

    public void SetDeityPath(DeityPath path)
    {
        State.Faith.Path = path;
        State.Faith.Doctrines.Add(path switch
        {
            DeityPath.Mercy => FaithDoctrine.Charity,
            DeityPath.Nature => FaithDoctrine.NatureBalance,
            DeityPath.War => FaithDoctrine.Conquest,
            DeityPath.Knowledge => FaithDoctrine.Scholarship,
            _ => FaithDoctrine.Sacrifice,
        });
    }

    private void UpdateFaithProgression()
    {
        ProcessGodPowerChronicle();
        float dailyFaith = 0;
        foreach (SettlementState city in _simulation.State.Settlements.Values)
        {
            CityDistrictState district = EnsureCityDistrict(city);
            int temples = district.Buildings.Count(b => b.Kind == BuildingKind.Temple && b.Status == BuildingStatus.Active);
            int priests = _living.State.Citizens.Values.Count(c => c.HomeSettlementId == city.Id && c.Job == CitizenJob.Priest);
            float cityFaith = State.Faith.CityFaith.GetValueOrDefault(city.Id);
            cityFaith += (temples * 0.08f + priests * 0.015f) * State.ModRules.FaithGainMultiplier;
            if (city.Happiness < 25) cityFaith -= 0.15f;
            State.Faith.CityFaith[city.Id] = Math.Clamp(cityFaith, 0, 1000);
            dailyFaith += temples * 0.04f + priests * 0.01f;
        }
        State.Faith.Faith = Math.Clamp(State.Faith.Faith + dailyFaith * State.ModRules.FaithGainMultiplier, 0, 10000);
        State.Faith.Favor = Math.Clamp(State.Faith.Favor + dailyFaith * 0.35f + State.Faith.Faith / 50000f, 0, State.Faith.MaxFavor);
        State.Faith.Fear = Math.Max(0, State.Faith.Fear - 0.01f);
        UnlockMiracles();
        if (_simulation.State.Day - State.Faith.LastProphecyDay >= 360 && State.Faith.Faith >= 80)
            GenerateProphecy();
        EvaluateProphecies();
    }

    private void ProcessGodPowerChronicle()
    {
        int start = Math.Clamp(State.Faith.LastChronicleIndex, 0, _simulation.State.Chronicle.Count);
        for (int i = start; i < _simulation.State.Chronicle.Count; i++)
        {
            string type = _simulation.State.Chronicle[i].Type.ToLowerInvariant();
            if (type.Contains("bless") || type.Contains("forest") || type.Contains("peace") || type.Contains("heal"))
            {
                State.Faith.Faith += 2.5f * State.ModRules.FaithGainMultiplier;
                State.Faith.Favor = Math.Min(State.Faith.MaxFavor, State.Faith.Favor + 1.2f);
            }
            if (type.Contains("meteor") || type.Contains("lightning") || type.Contains("curse") || type.Contains("plague"))
            {
                State.Faith.Fear += 3.5f;
                State.Faith.Faith = State.Faith.Path == DeityPath.Fear
                    ? State.Faith.Faith + 1.5f
                    : Math.Max(0, State.Faith.Faith - 0.5f);
            }
        }
        State.Faith.LastChronicleIndex = _simulation.State.Chronicle.Count;
        _lastKnownChronicleCount = State.Faith.LastChronicleIndex;
    }

    private void UnlockMiracles()
    {
        if (State.Faith.Faith >= 30) State.Faith.UnlockedMiracles.Add(MiracleKind.HealCity);
        if (State.Faith.Faith >= 75) State.Faith.UnlockedMiracles.Add(MiracleKind.Inspire);
        if (State.Faith.Faith >= 130) State.Faith.UnlockedMiracles.Add(MiracleKind.RaiseForest);
        if (State.Faith.Faith >= 190) State.Faith.UnlockedMiracles.Add(MiracleKind.RevealRuins);
        if (State.Faith.Faith >= 260) State.Faith.UnlockedMiracles.Add(MiracleKind.CalmSea);
        if (State.Faith.Faith >= 340 || State.Faith.Fear >= 120) State.Faith.UnlockedMiracles.Add(MiracleKind.Smite);
        State.Faith.MaxFavor = Math.Clamp(100 + State.Faith.Faith / 10f, 100, 500);
    }

    public bool UseMiracle(MiracleKind miracle, ulong? settlementId = null)
    {
        if (!State.Faith.UnlockedMiracles.Contains(miracle)) return false;
        float cost = miracle switch
        {
            MiracleKind.BlessHarvest => 12,
            MiracleKind.HealCity => 18,
            MiracleKind.Inspire => 20,
            MiracleKind.RaiseForest => 25,
            MiracleKind.RevealRuins => 28,
            MiracleKind.CalmSea => 32,
            MiracleKind.Smite => 35,
            _ => 20,
        };
        if (State.Faith.Favor < cost) return false;
        SettlementState? city = settlementId is ulong id
            ? _simulation.State.Settlements.GetValueOrDefault(id)
            : _simulation.State.Settlements.Values.OrderByDescending(c => c.Happiness).FirstOrDefault();
        int x = city?.X ?? _world.Width / 2;
        int y = city?.Y ?? _world.Height / 2;

        switch (miracle)
        {
            case MiracleKind.BlessHarvest:
                if (city is null) return false;
                city.Food += 180;
                city.Happiness = Math.Min(100, city.Happiness + 8);
                break;
            case MiracleKind.HealCity:
                if (city is null) return false;
                foreach (SimEntity entity in _simulation.State.Entities.Values.Where(e => e.IsAlive && e.SettlementId == city.Id))
                    entity.Health = Math.Min(100 * entity.VitalityGene, entity.Health + 35);
                foreach (DiseaseState disease in _simulation.State.Diseases)
                    foreach (ulong infectedId in disease.InfectedDays.Keys.Where(id2 => _simulation.State.Entities.GetValueOrDefault(id2)?.SettlementId == city.Id).ToArray())
                        disease.InfectedDays.Remove(infectedId);
                break;
            case MiracleKind.Inspire:
                if (city is null) return false;
                city.Happiness = Math.Min(100, city.Happiness + 18);
                foreach (SimEntity entity in _simulation.State.Entities.Values.Where(e => e.IsAlive && e.SettlementId == city.Id))
                    entity.Morale = Math.Min(100, entity.Morale + 25);
                break;
            case MiracleKind.Smite:
                foreach (SimEntity target in _simulation.State.Entities.Values
                             .Where(e => e.IsAlive && DistanceSquared(e.X, e.Y, x, y) <= 100)
                             .Where(e => e.Species == SpeciesKind.Monster || (city is not null && e.KingdomId != city.KingdomId))
                             .Take(30))
                    target.Health -= 60;
                State.Faith.Fear += 8;
                break;
            case MiracleKind.RaiseForest:
                for (int dy = -8; dy <= 8; dy++)
                    for (int dx = -8; dx <= 8; dx++)
                    {
                        int tx = x + dx;
                        int ty = y + dy;
                        if (!_world.IsInside(tx, ty) || dx * dx + dy * dy > 64) continue;
                        if (_world.GetTerrain(tx, ty) is TerrainType.Grassland or TerrainType.Beach)
                            _world.SetTerrain(tx, ty, TerrainType.Forest);
                    }
                break;
            case MiracleKind.RevealRuins:
                foreach (RuinState ruin in State.Ruins.Values.OrderBy(r => DistanceSquared(r.X, r.Y, x, y)).Take(4))
                    if (ruin.DiscoveredDay < 0) ruin.DiscoveredDay = _simulation.State.Day;
                break;
            case MiracleKind.CalmSea:
                _living.State.Weather = WeatherKind.Clear;
                _living.State.RainIntensity = 0;
                _living.State.WeatherDaysRemaining = 5;
                foreach (FleetState fleet in State.Fleets.Values) fleet.Morale = Math.Min(100, fleet.Morale + 10);
                break;
        }

        State.Faith.Favor -= cost;
        State.Faith.Faith += 1.5f;
        AddChronicle("faith.miracle", "ปาฏิหาริย์", $"ผู้เล่นใช้ปาฏิหาริย์ {miracle}", x, y, 3);
        LegendProfile? priest = State.Legends.Values.Where(l => !l.IsDead && l.Role == LegendRole.Priest).OrderByDescending(l => l.Fame).FirstOrDefault();
        if (priest is not null)
        {
            priest.Fame += 3;
            AddMemory(priest, MemoryKind.Miracle, $"Witnessed the miracle {miracle}.", x, y, null, 5);
        }
        return true;
    }

    private void GenerateProphecy()
    {
        string[] subjects =
        {
            "เมืองที่รุ่งเรืองจะผ่านบททดสอบครั้งใหญ่",
            "ผู้เดินทางจากซากโบราณจะนำความเปลี่ยนแปลงมา",
            "กองเรือจะเปิดประตูสู่ยุคใหม่",
            "ตำนานผู้หนึ่งจะเปลี่ยนชะตาของอาณาจักร",
            "ศรัทธาและความหวาดกลัวจะต้องถูกเลือก",
        };
        var random = CreateDayRandom(7811);
        State.Faith.Prophecies.Add(new ProphecyState
        {
            Id = State.NextProphecyId++,
            Text = subjects[random.Next(subjects.Length)],
            CreatedDay = _simulation.State.Day,
            TargetDay = _simulation.State.Day + 360 + random.Next(360),
        });
        State.Faith.LastProphecyDay = _simulation.State.Day;
        AddChronicle("faith.prophecy", "คำพยากรณ์ใหม่", State.Faith.Prophecies[^1].Text, _world.Width / 2, _world.Height / 2, 2);
    }

    private void EvaluateProphecies()
    {
        foreach (ProphecyState prophecy in State.Faith.Prophecies.Where(p => !p.Fulfilled && !p.Failed && _simulation.State.Day >= p.TargetDay))
        {
            bool success = State.Legends.Values.Any(l => l.Fame >= 100) || State.Fleets.Values.Any(f => f.IsActive) || State.Ruins.Values.Any(r => r.Explored);
            prophecy.Fulfilled = success;
            prophecy.Failed = !success;
            State.Faith.Faith = Math.Max(0, State.Faith.Faith + (success ? 20 : -10));
            AddChronicle("faith.prophecy_resolved", success ? "คำพยากรณ์เป็นจริง" : "คำพยากรณ์ล้มเหลว", prophecy.Text, _world.Width / 2, _world.Height / 2, success ? 3 : 2);
        }
    }

    private MageProfile CreateMage(ulong entityId, MagicSchool school)
    {
        var mage = new MageProfile { EntityId = entityId, School = school };
        mage.KnownSpells.Add(school switch
        {
            MagicSchool.Nature => SpellKind.Growth,
            MagicSchool.Fire => SpellKind.Fireball,
            MagicSchool.Healing => SpellKind.Heal,
            MagicSchool.Storm => SpellKind.StormCall,
            MagicSchool.Necromancy => SpellKind.AnimateRuins,
            _ => SpellKind.Ward,
        });
        if (school == MagicSchool.Arcane) mage.KnownSpells.Add(SpellKind.Teleport);
        return mage;
    }

    private static int DistanceSquared(int x1, int y1, int x2, int y2)
    {
        int dx = x1 - x2;
        int dy = y1 - y2;
        return dx * dx + dy * dy;
    }
}
