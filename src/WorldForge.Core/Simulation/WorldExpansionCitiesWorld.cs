using WorldForge.Core.World;

namespace WorldForge.Core.Simulation;

public sealed partial class WorldExpansionDirector
{
    private CityDistrictState EnsureCityDistrict(SettlementState city)
    {
        if (State.CityDistricts.TryGetValue(city.Id, out CityDistrictState? existing))
            return existing;
        var district = new CityDistrictState { SettlementId = city.Id };
        foreach (ResourceKind resource in Enum.GetValues<ResourceKind>())
            district.Stockpile[resource] = 0;
        State.CityDistricts[city.Id] = district;
        SeedInitialCity(district, city);
        return district;
    }

    private void SeedInitialCity(CityDistrictState district, SettlementState city)
    {
        int homes = Math.Max(2, city.Housing / 8);
        for (int i = 0; i < homes; i++) PlaceBuilding(district, city, BuildingKind.House, immediate: true);
        PlaceBuilding(district, city, BuildingKind.Farm, immediate: true);
        PlaceBuilding(district, city, BuildingKind.Market, immediate: true);
        if (city.Buildings.Contains("building.keep") || city.Stage == SettlementStage.Capital)
            PlaceBuilding(district, city, BuildingKind.Keep, immediate: true);
        if (city.Fortification > 0)
        {
            PlaceBuilding(district, city, BuildingKind.Wall, immediate: true);
            PlaceBuilding(district, city, BuildingKind.Gate, immediate: true);
        }
        if (IsCoastal(city.X, city.Y))
            PlaceBuilding(district, city, BuildingKind.Harbor, immediate: true);
        district.PopulationAtLastLayout = PopulationOfCity(city.Id);
        district.LastLayoutDay = _simulation.State.Day;
    }

    private PlacedBuilding PlaceBuilding(CityDistrictState district, SettlementState city, BuildingKind kind, bool immediate)
    {
        long id = district.NextBuildingId++;
        (int x, int y) = FindBuildingPosition(city, district, id, kind);
        var building = new PlacedBuilding
        {
            Id = id,
            SettlementId = city.Id,
            Kind = kind,
            X = x,
            Y = y,
            Progress = immediate ? 100 : 0,
            Health = 100,
            Status = immediate ? BuildingStatus.Active : BuildingStatus.Planned,
            StartedDay = _simulation.State.Day,
            CompletedDay = immediate ? _simulation.State.Day : 0,
        };
        district.Buildings.Add(building);
        AddRoadLine(district, city.X, city.Y, x, y);
        return building;
    }

    private (int X, int Y) FindBuildingPosition(SettlementState city, CityDistrictState district, long id, BuildingKind kind)
    {
        int baseRadius = kind switch
        {
            BuildingKind.Farm or BuildingKind.Lumberyard or BuildingKind.Quarry or BuildingKind.Mine => 8,
            BuildingKind.Wall or BuildingKind.Gate or BuildingKind.Watchtower => 10,
            BuildingKind.Harbor or BuildingKind.Shipyard => 12,
            _ => 5,
        };
        int hash = Math.Abs(StableHash((ulong)(city.Id * 1000UL + (ulong)id), State.Seed));
        for (int attempt = 0; attempt < 32; attempt++)
        {
            double angle = ((hash + attempt * 47) % 360) * Math.PI / 180.0;
            int radius = baseRadius + (hash / 17 + attempt) % 8;
            int x = city.X + (int)Math.Round(Math.Cos(angle) * radius);
            int y = city.Y + (int)Math.Round(Math.Sin(angle) * radius);
            if (!_world.IsInside(x, y)) continue;
            TerrainType terrain = _world.GetTerrain(x, y);
            bool needsWater = kind is BuildingKind.Harbor or BuildingKind.Shipyard;
            bool water = terrain is TerrainType.DeepOcean or TerrainType.ShallowWater;
            if (needsWater != water) continue;
            if (!needsWater && terrain == TerrainType.Mountain && kind is not BuildingKind.Mine and not BuildingKind.Quarry) continue;
            if (district.Buildings.Any(b => b.X == x && b.Y == y)) continue;
            return (x, y);
        }
        return (city.X + (int)(id % 5) - 2, city.Y + (int)((id / 5) % 5) - 2);
    }

    private void AddRoadLine(CityDistrictState district, int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            if (_world.IsInside(x0, y0)) district.RoadTiles.Add(y0 * _world.Width + x0);
            if (x0 == x1 && y0 == y1) break;
            int doubled = 2 * error;
            if (doubled >= dy) { error += dy; x0 += sx; }
            if (doubled <= dx) { error += dx; y0 += sy; }
        }
    }

    private void UpdateCityConstructionAndProduction()
    {
        foreach (SettlementState city in _simulation.State.Settlements.Values.OrderBy(c => c.Id))
        {
            CityDistrictState district = EnsureCityDistrict(city);
            int population = PopulationOfCity(city.Id);
            int builders = _living.State.Citizens.Values.Count(c => c.HomeSettlementId == city.Id && c.Job == CitizenJob.Builder);
            float speed = Math.Max(0.4f, 0.8f + builders * 0.18f) * State.ModRules.ConstructionSpeedMultiplier;
            foreach (PlacedBuilding building in district.Buildings.Where(b => b.Status is BuildingStatus.Planned or BuildingStatus.Building))
            {
                building.Status = BuildingStatus.Building;
                building.Workers = Math.Max(1, builders / Math.Max(1, district.Buildings.Count(b => b.Status == BuildingStatus.Building)));
                building.Progress = Math.Min(100, building.Progress + speed + building.Workers * 0.15f);
                if (building.Progress >= 100)
                {
                    building.Status = BuildingStatus.Active;
                    building.CompletedDay = _simulation.State.Day;
                    ApplyBuildingCompletion(city, building);
                    AddChronicle("city.building_complete", "สิ่งปลูกสร้างเสร็จสมบูรณ์", $"{city.Name} สร้าง {building.Kind} เสร็จแล้ว", building.X, building.Y, 1);
                }
            }

            if (_simulation.State.Day - district.LastProductionDay >= 7)
            {
                district.LastProductionDay = _simulation.State.Day;
                RunProductionChains(city, district, population);
            }
            if (_simulation.State.Day - district.LastLayoutDay >= 30)
            {
                district.LastLayoutDay = _simulation.State.Day;
                PlanAutomaticBuilding(city, district, population);
                district.PopulationAtLastLayout = population;
            }
            ApplyWarDamage(city, district);
        }
    }

    private void ApplyBuildingCompletion(SettlementState city, PlacedBuilding building)
    {
        switch (building.Kind)
        {
            case BuildingKind.House: city.Housing += 8 * building.Level; break;
            case BuildingKind.Wall: city.Fortification += 2 * building.Level; break;
            case BuildingKind.Watchtower: city.Fortification += building.Level; break;
            case BuildingKind.Keep: city.Fortification += 3 * building.Level; break;
            case BuildingKind.Market: city.Buildings.Add("building.market"); break;
            case BuildingKind.Barracks: city.Buildings.Add("building.barracks"); break;
            case BuildingKind.Temple: city.Buildings.Add("building.temple"); break;
            case BuildingKind.Clinic: city.Buildings.Add("building.clinic"); break;
            case BuildingKind.Harbor: city.Buildings.Add("building.harbor"); break;
            case BuildingKind.Shipyard: city.Buildings.Add("building.shipyard"); break;
            case BuildingKind.MageTower: city.Buildings.Add("building.mage_tower"); break;
        }
    }

    private void RunProductionChains(SettlementState city, CityDistrictState district, int population)
    {
        float raceProduction = RaceForKingdom(city.KingdomId.GetValueOrDefault()) switch
        {
            RaceKind.Dwarf => 1.18f,
            RaceKind.Sylvan => 1.12f,
            RaceKind.Orc => 1.08f,
            RaceKind.Tideborn => 1.1f,
            RaceKind.Arcane => 1.06f,
            _ => 1f,
        };
        int active(BuildingKind kind) => district.Buildings.Count(b => b.Kind == kind && b.Status == BuildingStatus.Active);
        district.Stockpile[ResourceKind.Food] += active(BuildingKind.Farm) * 14 * raceProduction;
        district.Stockpile[ResourceKind.Wood] += active(BuildingKind.Lumberyard) * 9 * raceProduction;
        district.Stockpile[ResourceKind.Stone] += active(BuildingKind.Quarry) * 8 * raceProduction;
        district.Stockpile[ResourceKind.Ore] += active(BuildingKind.Mine) * 6 * raceProduction;
        ConvertStock(district, ResourceKind.Wood, ResourceKind.Planks, active(BuildingKind.Sawmill) * 6, 0.85f);
        ConvertStock(district, ResourceKind.Ore, ResourceKind.Metal, active(BuildingKind.Smelter) * 5, 0.72f);
        int workshopCapacity = active(BuildingKind.Workshop) * 4;
        float craft = Math.Min(workshopCapacity, Math.Min(district.Stockpile[ResourceKind.Planks], district.Stockpile[ResourceKind.Metal]));
        if (craft > 0)
        {
            district.Stockpile[ResourceKind.Planks] -= craft;
            district.Stockpile[ResourceKind.Metal] -= craft;
            district.Stockpile[ResourceKind.Tools] += craft * 0.65f;
            district.Stockpile[ResourceKind.Weapons] += craft * 0.35f;
        }
        city.Food += district.Stockpile[ResourceKind.Food] * 0.45f;
        city.Wood += district.Stockpile[ResourceKind.Wood] * 0.35f + district.Stockpile[ResourceKind.Planks] * 0.25f;
        city.Stone += district.Stockpile[ResourceKind.Stone] * 0.35f;
        city.Gold += active(BuildingKind.Market) * Math.Max(1, population) * 0.015f;
        district.Stockpile[ResourceKind.Food] *= 0.5f;
        district.Stockpile[ResourceKind.Wood] *= 0.62f;
        district.Stockpile[ResourceKind.Stone] *= 0.62f;
        district.Stockpile[ResourceKind.Planks] *= 0.72f;
    }

    private static void ConvertStock(CityDistrictState district, ResourceKind input, ResourceKind output, float capacity, float efficiency)
    {
        float amount = Math.Min(capacity, district.Stockpile.GetValueOrDefault(input));
        district.Stockpile[input] -= amount;
        district.Stockpile[output] = district.Stockpile.GetValueOrDefault(output) + amount * efficiency;
    }

    private void PlanAutomaticBuilding(SettlementState city, CityDistrictState district, int population)
    {
        CityManagementPolicy? policy = _living.State.Cities.GetValueOrDefault(city.Id);
        if (policy?.AutoBuild == false || district.Buildings.Count(b => b.Status is BuildingStatus.Planned or BuildingStatus.Building) >= 3)
            return;
        BuildingKind? next = null;
        int active(BuildingKind kind) => district.Buildings.Count(b => b.Kind == kind && b.Status == BuildingStatus.Active);
        if (city.Housing < population + 8) next = BuildingKind.House;
        else if (city.Food < Math.Max(80, population * 2)) next = BuildingKind.Farm;
        else if (active(BuildingKind.Lumberyard) == 0) next = BuildingKind.Lumberyard;
        else if (active(BuildingKind.Quarry) == 0) next = BuildingKind.Quarry;
        else if (active(BuildingKind.Sawmill) == 0 && city.Wood > 40) next = BuildingKind.Sawmill;
        else if (active(BuildingKind.Market) < Math.Max(1, population / 80)) next = BuildingKind.Market;
        else if (policy?.Priority == CityPriority.Faith && active(BuildingKind.Temple) == 0) next = BuildingKind.Temple;
        else if (policy?.Priority == CityPriority.Knowledge && active(BuildingKind.MageTower) == 0) next = BuildingKind.MageTower;
        else if (policy?.Priority == CityPriority.Defense && active(BuildingKind.Watchtower) < 2) next = BuildingKind.Watchtower;
        else if (IsCoastal(city.X, city.Y) && active(BuildingKind.Harbor) > 0 && active(BuildingKind.Shipyard) == 0 && population >= 30) next = BuildingKind.Shipyard;
        else if (population >= 70 && active(BuildingKind.Clinic) == 0) next = BuildingKind.Clinic;
        else if (population >= 100 && active(BuildingKind.Barracks) == 0) next = BuildingKind.Barracks;
        if (next is not null) PlaceBuilding(district, city, next.Value, immediate: false);
    }

    private void ApplyWarDamage(SettlementState city, CityDistrictState district)
    {
        bool besieged = _simulation.State.Armies.Values.Any(a => a.IsActive && a.TargetSettlementId == city.Id && a.Status == ArmyStatus.Besieging);
        if (!besieged || _simulation.State.Day % 10 != 0) return;
        PlacedBuilding? target = district.Buildings.Where(b => b.Status == BuildingStatus.Active).OrderBy(_ => CreateDayRandom((int)city.Id).Next()).FirstOrDefault();
        if (target is null) return;
        target.Health -= 8;
        district.RuinDamage += 2;
        if (target.Health <= 0)
        {
            target.Health = 0;
            target.Status = BuildingStatus.Ruined;
        }
        else if (target.Health < 50) target.Status = BuildingStatus.Damaged;
    }

    public FleetState? CreateFleet(ulong settlementId, FleetMission mission)
    {
        if (!State.ModRules.EnableNavalWarfare || !_simulation.State.Settlements.TryGetValue(settlementId, out SettlementState? port) || port.KingdomId is null)
            return null;
        CityDistrictState district = EnsureCityDistrict(port);
        bool hasPort = district.Buildings.Any(b => b.Kind is BuildingKind.Harbor or BuildingKind.Shipyard && b.Status == BuildingStatus.Active);
        if (!hasPort || port.Wood < 25 || port.Gold < 10) return null;
        GridPoint? water = FindNearestWater(port.X, port.Y, 16);
        if (water is null) return null;
        port.Wood -= 25;
        port.Gold -= 10;
        var fleet = new FleetState
        {
            Id = State.NextFleetId++,
            Name = $"กองเรือ {State.NextFleetId - 1}",
            KingdomId = port.KingdomId.Value,
            OriginSettlementId = port.Id,
            X = water.Value.X,
            Y = water.Value.Y,
            Ships = district.Buildings.Any(b => b.Kind == BuildingKind.Shipyard && b.Status == BuildingStatus.Active) ? 5 : 3,
            Marines = district.Stockpile.GetValueOrDefault(ResourceKind.Weapons) >= 5 ? 35 : 20,
            Mission = mission,
        };
        State.Fleets[fleet.Id] = fleet;
        PlanFleetTarget(fleet);
        AddChronicle("naval.fleet_created", "กองเรือออกจากท่า", $"{fleet.Name} ออกจาก {port.Name} ภารกิจ {mission}", fleet.X, fleet.Y, 2);
        return fleet;
    }

    private void UpdateFleets()
    {
        if (!State.ModRules.EnableNavalWarfare) return;
        if (_simulation.State.Day - State.LastFleetPlanningDay >= 90)
        {
            State.LastFleetPlanningDay = _simulation.State.Day;
            foreach (SettlementState port in _simulation.State.Settlements.Values.Where(c => c.KingdomId is not null && IsCoastal(c.X, c.Y)).OrderBy(c => c.Id))
            {
                if (State.Fleets.Values.Count(f => f.IsActive && f.KingdomId == port.KingdomId) >= 3) continue;
                CityDistrictState district = EnsureCityDistrict(port);
                if (!district.Buildings.Any(b => b.Kind == BuildingKind.Shipyard && b.Status == BuildingStatus.Active)) continue;
                FleetMission mission = KingdomAtWar(port.KingdomId!.Value) ? FleetMission.Raid : FleetMission.Trade;
                CreateFleet(port.Id, mission);
            }
        }

        foreach (FleetState fleet in State.Fleets.Values.Where(f => f.IsActive).OrderBy(f => f.Id).ToArray())
        {
            if (_simulation.State.Day - fleet.LastMoveDay < Math.Max(1, (int)Math.Round(3 / State.ModRules.FleetSpeedMultiplier))) continue;
            fleet.LastMoveDay = _simulation.State.Day;
            fleet.Supply -= 0.8f;
            if (fleet.Supply <= 0 || fleet.Ships <= 0)
            {
                fleet.IsActive = false;
                continue;
            }
            if (fleet.Path.Count == 0 || fleet.PathIndex >= fleet.Path.Count)
            {
                if (fleet.TargetSettlementId is not null) ResolveFleetArrival(fleet);
                PlanFleetTarget(fleet);
                continue;
            }
            int steps = Math.Max(1, (int)MathF.Round(State.ModRules.FleetSpeedMultiplier));
            for (int i = 0; i < steps && fleet.PathIndex < fleet.Path.Count; i++)
            {
                GridPoint next = fleet.Path[fleet.PathIndex++];
                fleet.X = next.X;
                fleet.Y = next.Y;
            }
        }
    }

    private void PlanFleetTarget(FleetState fleet)
    {
        SettlementState? origin = _simulation.State.Settlements.GetValueOrDefault(fleet.OriginSettlementId);
        IEnumerable<SettlementState> ports = _simulation.State.Settlements.Values.Where(c => c.Id != fleet.OriginSettlementId && IsCoastal(c.X, c.Y));
        SettlementState? target = fleet.Mission switch
        {
            FleetMission.Raid or FleetMission.Invade => ports.Where(c => c.KingdomId is ulong kid && kid != fleet.KingdomId && RelationValue(fleet.KingdomId, kid) <= -50).OrderBy(c => DistanceSquared(fleet.X, fleet.Y, c.X, c.Y)).FirstOrDefault(),
            FleetMission.Trade => ports.Where(c => c.KingdomId is ulong kid && RelationValue(fleet.KingdomId, kid) >= 0).OrderBy(c => DistanceSquared(fleet.X, fleet.Y, c.X, c.Y)).FirstOrDefault(),
            _ => ports.OrderBy(c => DistanceSquared(fleet.X, fleet.Y, c.X, c.Y)).FirstOrDefault(),
        };
        target ??= origin;
        if (target is null) { fleet.Mission = FleetMission.Idle; return; }
        GridPoint? targetWater = FindNearestWater(target.X, target.Y, 16);
        if (targetWater is null) { fleet.Mission = FleetMission.Idle; return; }
        fleet.TargetSettlementId = target.Id;
        fleet.Path = FindWaterPath(new GridPoint(fleet.X, fleet.Y), targetWater.Value, 24000);
        fleet.PathIndex = fleet.Path.Count > 1 ? 1 : 0;
        if (fleet.Path.Count == 0) fleet.Mission = FleetMission.Idle;
    }

    private void ResolveFleetArrival(FleetState fleet)
    {
        if (fleet.TargetSettlementId is not ulong targetId || !_simulation.State.Settlements.TryGetValue(targetId, out SettlementState? target)) return;
        SettlementState? origin = _simulation.State.Settlements.GetValueOrDefault(fleet.OriginSettlementId);
        switch (fleet.Mission)
        {
            case FleetMission.Trade:
                float cargo = Math.Min(40, origin?.Food ?? 0);
                if (origin is not null) origin.Food -= cargo;
                target.Food += cargo;
                if (origin is not null) origin.Gold += 5 + cargo * 0.08f;
                target.Gold += 3;
                fleet.Supply = 100;
                fleet.Morale = Math.Min(100, fleet.Morale + 5);
                break;
            case FleetMission.Raid:
            case FleetMission.Invade:
                target.Food = Math.Max(0, target.Food - fleet.Marines * 0.7f);
                target.Gold = Math.Max(0, target.Gold - fleet.Marines * 0.2f);
                target.Happiness = Math.Max(0, target.Happiness - 12);
                target.Fortification = Math.Max(0, target.Fortification - Math.Max(1, fleet.Ships / 2));
                fleet.Morale -= target.Fortification * 2;
                if (fleet.Mission == FleetMission.Invade && fleet.Marines > 25 + target.Fortification * 8 && target.KingdomId != fleet.KingdomId)
                    CaptureCoastalCity(fleet, target);
                break;
            case FleetMission.Explore:
                RuinState? ruin = State.Ruins.Values.Where(r => !r.Explored && r.Type == RuinType.SunkenShrine).OrderBy(r => DistanceSquared(fleet.X, fleet.Y, r.X, r.Y)).FirstOrDefault();
                if (ruin is not null) ExploreRuin(ruin, null);
                break;
        }
        fleet.Mission = FleetMission.Return;
        fleet.TargetSettlementId = fleet.OriginSettlementId;
        PlanPathToSettlement(fleet, fleet.OriginSettlementId);
    }

    private void CaptureCoastalCity(FleetState fleet, SettlementState target)
    {
        if (target.KingdomId is ulong oldKingdom && _simulation.State.Kingdoms.TryGetValue(oldKingdom, out KingdomState? old))
            old.Settlements.Remove(target.Id);
        target.KingdomId = fleet.KingdomId;
        if (_simulation.State.Kingdoms.TryGetValue(fleet.KingdomId, out KingdomState? owner)) owner.Settlements.Add(target.Id);
        foreach (SimEntity citizen in _simulation.State.Entities.Values.Where(e => e.IsAlive && e.SettlementId == target.Id)) citizen.KingdomId = fleet.KingdomId;
        _simulation.State.TotalCitiesCaptured++;
        fleet.Marines = Math.Max(8, fleet.Marines - 12);
        AddChronicle("naval.capture", "เมืองชายฝั่งถูกยึด", $"{fleet.Name} ยึด {target.Name} จากทะเล", target.X, target.Y, 4);
    }

    private void PlanPathToSettlement(FleetState fleet, ulong settlementId)
    {
        SettlementState? target = _simulation.State.Settlements.GetValueOrDefault(settlementId);
        GridPoint? water = target is null ? null : FindNearestWater(target.X, target.Y, 16);
        if (water is null) { fleet.IsActive = false; return; }
        fleet.Path = FindWaterPath(new GridPoint(fleet.X, fleet.Y), water.Value, 24000);
        fleet.PathIndex = fleet.Path.Count > 1 ? 1 : 0;
    }

    private void UpdateNomads()
    {
        if (!State.ModRules.EnableNomads) return;
        int interval = Math.Max(180, (int)(720 / Math.Max(0.1f, State.ModRules.NomadFrequencyMultiplier)));
        if (_simulation.State.Day - State.LastNomadSpawnDay >= interval && State.Nomads.Values.Count(n => n.Active) < 8)
        {
            SpawnNomadBand();
            State.LastNomadSpawnDay = _simulation.State.Day;
        }
        foreach (NomadBandState band in State.Nomads.Values.Where(n => n.Active).OrderBy(n => n.Id).ToArray())
        {
            if (_simulation.State.Day - band.LastMoveDay < 5) continue;
            band.LastMoveDay = _simulation.State.Day;
            SettlementState? target = band.TargetSettlementId is ulong sid ? _simulation.State.Settlements.GetValueOrDefault(sid) : null;
            target ??= _simulation.State.Settlements.Values.OrderBy(c => DistanceSquared(band.X, band.Y, c.X, c.Y)).FirstOrDefault();
            band.TargetSettlementId = target?.Id;
            if (target is null) { WanderNomad(band); continue; }
            int distance = DistanceSquared(band.X, band.Y, target.X, target.Y);
            if (distance <= 9)
            {
                if (band.State == NomadStateKind.Trading)
                {
                    target.Food += band.Population * 0.4f;
                    target.Gold += band.Wealth * 0.15f;
                    band.Wealth += 4;
                    band.TargetSettlementId = null;
                }
                else if (band.State == NomadStateKind.Raiding)
                {
                    target.Food = Math.Max(0, target.Food - band.Population * 0.5f);
                    target.Happiness = Math.Max(0, target.Happiness - 8);
                    band.Wealth += 12;
                    band.TargetSettlementId = null;
                }
                else if (band.Wealth >= 45 && band.Population >= 20)
                {
                    SettleNomadBand(band, target);
                }
                else
                {
                    band.State = NomadStateKind.Trading;
                    band.TargetSettlementId = null;
                }
                continue;
            }
            MoveLandStepToward(band, target.X, target.Y);
        }
    }

    private void SpawnNomadBand()
    {
        var random = CreateDayRandom(4411 + (int)State.NextNomadId);
        for (int attempt = 0; attempt < 100; attempt++)
        {
            int x = random.Next(_world.Width);
            int y = random.Next(_world.Height);
            TerrainType terrain = _world.GetTerrain(x, y);
            if (terrain is TerrainType.DeepOcean or TerrainType.ShallowWater or TerrainType.Mountain) continue;
            var band = new NomadBandState
            {
                Id = State.NextNomadId++,
                Name = $"ชนเผ่าเร่ร่อน {State.NextNomadId - 1}",
                Race = State.ModRules.EnableFantasyRaces ? (RaceKind)random.Next(Enum.GetValues<RaceKind>().Length) : RaceKind.Human,
                X = x,
                Y = y,
                Population = random.Next(18, 46),
                Wealth = random.Next(12, 55),
                State = (NomadStateKind)random.Next(0, 3),
                LastMoveDay = _simulation.State.Day,
            };
            State.Nomads[band.Id] = band;
            AddChronicle("nomad.arrival", "ชนเผ่าเร่ร่อนปรากฏ", $"{band.Name} เดินทางเข้าสู่โลก", x, y, 2);
            return;
        }
    }

    private void WanderNomad(NomadBandState band)
    {
        var random = CreateDayRandom(7000 + (int)band.Id);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            int nx = Math.Clamp(band.X + random.Next(-2, 3), 0, _world.Width - 1);
            int ny = Math.Clamp(band.Y + random.Next(-2, 3), 0, _world.Height - 1);
            TerrainType terrain = _world.GetTerrain(nx, ny);
            if (terrain is TerrainType.DeepOcean or TerrainType.ShallowWater or TerrainType.Mountain) continue;
            band.X = nx; band.Y = ny; return;
        }
    }

    private void MoveLandStepToward(NomadBandState band, int tx, int ty)
    {
        int dx = Math.Sign(tx - band.X);
        int dy = Math.Sign(ty - band.Y);
        (int X, int Y)[] candidates = { (band.X + dx, band.Y + dy), (band.X + dx, band.Y), (band.X, band.Y + dy) };
        foreach ((int x, int y) in candidates)
        {
            if (!_world.IsInside(x, y)) continue;
            TerrainType terrain = _world.GetTerrain(x, y);
            if (terrain is TerrainType.DeepOcean or TerrainType.ShallowWater or TerrainType.Mountain) continue;
            band.X = x; band.Y = y; return;
        }
        WanderNomad(band);
    }

    private void SettleNomadBand(NomadBandState band, SettlementState nearCity)
    {
        var settlers = new List<ulong>();
        int count = Math.Clamp(band.Population / 3, 5, 16);
        int sx = Math.Clamp(band.X + 3, 0, _world.Width - 1);
        int sy = Math.Clamp(band.Y + 3, 0, _world.Height - 1);
        if (_world.GetTerrain(sx, sy) is TerrainType.DeepOcean or TerrainType.ShallowWater or TerrainType.Mountain) { sx = nearCity.X; sy = nearCity.Y; }
        for (int i = 0; i < count; i++)
        {
            SimEntity entity = _simulation.SpawnEntity(SpeciesKind.Settler, sx, sy, $"{band.Race} ผู้ตั้งถิ่นฐาน {i + 1}");
            entity.AgeDays = (18 + i % 24) * 360;
            settlers.Add(entity.Id);
            State.CitizenRaces[entity.Id] = band.Race;
        }
        SettlementState settlement = _simulation.FoundSettlement(settlers, $"นครแห่ง{band.Name}");
        KingdomState kingdom = _simulation.FoundKingdom(settlement.Id, $"รัฐ{band.Name}", GovernmentType.TribalConfederation);
        State.KingdomRaces[kingdom.Id] = band.Race;
        band.Active = false;
        band.State = NomadStateKind.Settling;
        AddChronicle("nomad.settled", "ชนเผ่าตั้งถิ่นฐาน", $"{band.Name} ก่อตั้ง {settlement.Name}", settlement.X, settlement.Y, 3);
        EnsureWorldRecords();
    }

    private void SeedRuinsIfNeeded()
    {
        if (State.Ruins.Count > 0 || State.ModRules.InitialRuinCount <= 0) return;
        var random = CreateDayRandom(9911);
        int target = Math.Min(State.ModRules.InitialRuinCount, Math.Max(1, _world.Width * _world.Height / 2048));
        int attempts = target * 80;
        while (State.Ruins.Count < target && attempts-- > 0)
        {
            int x = random.Next(_world.Width);
            int y = random.Next(_world.Height);
            TerrainType terrain = _world.GetTerrain(x, y);
            bool water = terrain is TerrainType.DeepOcean or TerrainType.ShallowWater;
            RuinType type = water ? RuinType.SunkenShrine : (RuinType)random.Next(Enum.GetValues<RuinType>().Length - 1);
            if (!water && terrain == TerrainType.Mountain && type != RuinType.MageVault) continue;
            if (State.Ruins.Values.Any(r => DistanceSquared(r.X, r.Y, x, y) < 18 * 18)) continue;
            ulong id = State.NextRuinId++;
            State.Ruins[id] = new RuinState
            {
                Id = id,
                Type = type,
                X = x,
                Y = y,
                Danger = random.Next(1, 11),
                Richness = random.Next(1, 11),
                RelicName = RelicName(type, id),
            };
        }
    }

    private void UpdateMagicAndRuins()
    {
        if (State.ModRules.EnableMagic)
        {
            foreach (MageProfile mage in State.Mages.Values.OrderBy(m => m.EntityId))
            {
                SimEntity? entity = _simulation.State.Entities.GetValueOrDefault(mage.EntityId);
                if (entity is null || !entity.IsAlive) continue;
                mage.Mana = Math.Min(100 + mage.Level * 15, mage.Mana + 0.7f + mage.Level * 0.05f);
                int interval = Math.Max(20, (int)(120 / Math.Max(0.1f, State.ModRules.MagicFrequencyMultiplier)));
                if (_simulation.State.Day - mage.LastCastDay >= interval && mage.Mana >= 18)
                    CastAutonomousSpell(mage, entity);
            }
        }

        if (_simulation.State.Day % 30 == 0)
        {
            foreach (RuinState ruin in State.Ruins.Values.Where(r => !r.Explored).OrderBy(r => r.Id))
            {
                SimEntity? explorer = _simulation.State.Entities.Values
                    .Where(e => e.IsAlive && e.Species == SpeciesKind.Settler)
                    .Where(e => _living.State.Citizens.GetValueOrDefault(e.Id)?.Job is CitizenJob.Scholar or CitizenJob.Trader)
                    .OrderBy(e => DistanceSquared(e.X, e.Y, ruin.X, ruin.Y))
                    .FirstOrDefault();
                if (explorer is null || DistanceSquared(explorer.X, explorer.Y, ruin.X, ruin.Y) > 80 * 80) continue;
                ruin.DiscoveredDay = ruin.DiscoveredDay < 0 ? _simulation.State.Day : ruin.DiscoveredDay;
                int chance = 12 + (int)Math.Clamp(explorer.Intelligence, 0, 30) + (State.Mages.ContainsKey(explorer.Id) ? 18 : 0) - ruin.Danger * 3;
                if (Math.Abs(StableHash(explorer.Id ^ ruin.Id, State.Seed + _simulation.State.Day)) % 100 < chance)
                    ExploreRuin(ruin, explorer.Id);
            }
        }
    }

    public bool CastSpell(ulong mageId, SpellKind spell, int x, int y)
    {
        if (!State.Mages.TryGetValue(mageId, out MageProfile? mage) || !mage.KnownSpells.Contains(spell) || mage.Mana < 20)
            return false;
        SimEntity? caster = _simulation.State.Entities.GetValueOrDefault(mageId);
        if (caster is null || !caster.IsAlive) return false;
        ApplySpell(mage, caster, spell, x, y);
        return true;
    }

    private void CastAutonomousSpell(MageProfile mage, SimEntity caster)
    {
        SpellKind spell = mage.KnownSpells.OrderBy(s => s).ElementAt(Math.Abs(StableHash(caster.Id, State.Seed + _simulation.State.Day)) % mage.KnownSpells.Count);
        ApplySpell(mage, caster, spell, caster.X, caster.Y);
    }

    private void ApplySpell(MageProfile mage, SimEntity caster, SpellKind spell, int x, int y)
    {
        mage.Mana -= 20;
        mage.LastCastDay = _simulation.State.Day;
        mage.Level = Math.Min(10, mage.Level + (Math.Abs(StableHash(caster.Id, _simulation.State.Day)) % 5 == 0 ? 1 : 0));
        switch (spell)
        {
            case SpellKind.Growth:
                for (int dy = -3; dy <= 3; dy++) for (int dx = -3; dx <= 3; dx++)
                {
                    int tx = x + dx; int ty = y + dy;
                    if (_world.IsInside(tx, ty) && _world.GetTerrain(tx, ty) == TerrainType.Grassland) _world.SetTerrain(tx, ty, TerrainType.Forest);
                }
                break;
            case SpellKind.Fireball:
                foreach (SimEntity target in _simulation.State.Entities.Values.Where(e => e.IsAlive && e.Id != caster.Id && DistanceSquared(e.X, e.Y, x, y) <= 16).Take(8)) target.Health -= 30 + mage.Level * 3;
                break;
            case SpellKind.Heal:
                foreach (SimEntity target in _simulation.State.Entities.Values.Where(e => e.IsAlive && e.KingdomId == caster.KingdomId && DistanceSquared(e.X, e.Y, x, y) <= 36).Take(16)) target.Health = Math.Min(100 * target.VitalityGene, target.Health + 18 + mage.Level * 2);
                break;
            case SpellKind.StormCall:
                _living.State.Weather = WeatherKind.Storm;
                _living.State.RainIntensity = 1;
                _living.State.WeatherDaysRemaining = 2;
                break;
            case SpellKind.Teleport:
                SettlementState? city = caster.KingdomId is ulong kid ? _simulation.State.Settlements.Values.Where(c => c.KingdomId == kid).OrderByDescending(c => DistanceSquared(c.X, c.Y, caster.X, caster.Y)).FirstOrDefault() : null;
                if (city is not null) { caster.X = city.X; caster.Y = city.Y; }
                break;
            case SpellKind.Ward:
                SettlementState? home = caster.SettlementId is ulong sid ? _simulation.State.Settlements.GetValueOrDefault(sid) : null;
                if (home is not null) home.Fortification += 1;
                break;
            case SpellKind.AnimateRuins:
                RuinState? ruin = State.Ruins.Values.Where(r => r.Explored).OrderBy(r => DistanceSquared(r.X, r.Y, x, y)).FirstOrDefault();
                if (ruin is not null && _world.GetTerrain(ruin.X, ruin.Y) is not TerrainType.DeepOcean and not TerrainType.ShallowWater)
                    _simulation.SpawnEntity(SpeciesKind.Monster, ruin.X, ruin.Y, "ผู้พิทักษ์ซากโบราณ");
                break;
        }
        AddChronicle("magic.spell", "เวทมนตร์ถูกใช้", $"{caster.Name} ใช้เวท {spell}", x, y, 2, caster.Id);
        LegendProfile legend = PromoteLegend(caster, LegendRole.Scholar, 18);
        legend.Fame += 2;
    }

    private void ExploreRuin(RuinState ruin, ulong? explorerId)
    {
        ruin.Explored = true;
        ruin.ExplorerId = explorerId;
        ruin.DiscoveredDay = ruin.DiscoveredDay < 0 ? _simulation.State.Day : ruin.DiscoveredDay;
        SettlementState? city = explorerId is ulong id ? _simulation.State.Entities.GetValueOrDefault(id)?.SettlementId is ulong sid ? _simulation.State.Settlements.GetValueOrDefault(sid) : null : null;
        if (city is not null)
        {
            CityDistrictState district = EnsureCityDistrict(city);
            district.Stockpile[ResourceKind.Relics] += ruin.Richness;
            district.Stockpile[ResourceKind.ManaCrystal] += ruin.Type == RuinType.MageVault ? ruin.Richness * 1.5f : ruin.Richness * 0.25f;
            city.Gold += ruin.Richness * 4;
            city.Technology += ruin.Richness * 0.6f;
        }
        if (explorerId is ulong explorer && _simulation.State.Entities.TryGetValue(explorer, out SimEntity? entity))
        {
            LegendProfile legend = PromoteLegend(entity, LegendRole.Explorer, 20);
            legend.Discoveries++;
            legend.Fame += 8 + ruin.Richness;
            AddMemory(legend, MemoryKind.Discovery, $"Discovered {ruin.RelicName} in {ruin.Type}.", ruin.X, ruin.Y, null, 7);
        }
        AddChronicle("ruin.explored", "ค้นพบซากโบราณ", $"มีการค้นพบ {ruin.RelicName} ภายใน {ruin.Type}", ruin.X, ruin.Y, 3, explorerId ?? 0);
    }

    private bool IsCoastal(int x, int y)
    {
        for (int dy = -3; dy <= 3; dy++) for (int dx = -3; dx <= 3; dx++)
        {
            int tx = x + dx; int ty = y + dy;
            if (!_world.IsInside(tx, ty)) continue;
            if (_world.GetTerrain(tx, ty) is TerrainType.DeepOcean or TerrainType.ShallowWater) return true;
        }
        return false;
    }

    private GridPoint? FindNearestWater(int x, int y, int radius)
    {
        GridPoint? best = null;
        int bestDistance = int.MaxValue;
        for (int dy = -radius; dy <= radius; dy++) for (int dx = -radius; dx <= radius; dx++)
        {
            int tx = x + dx; int ty = y + dy;
            if (!_world.IsInside(tx, ty)) continue;
            if (_world.GetTerrain(tx, ty) is not (TerrainType.DeepOcean or TerrainType.ShallowWater)) continue;
            int distance = dx * dx + dy * dy;
            if (distance >= bestDistance) continue;
            bestDistance = distance; best = new GridPoint(tx, ty);
        }
        return best;
    }

    private List<GridPoint> FindWaterPath(GridPoint start, GridPoint goal, int maxNodes)
    {
        if (start.Equals(goal)) return new List<GridPoint> { start };
        int startIndex = start.Y * _world.Width + start.X;
        int goalIndex = goal.Y * _world.Width + goal.X;
        var queue = new Queue<int>();
        var previous = new Dictionary<int, int> { [startIndex] = -1 };
        queue.Enqueue(startIndex);
        int expanded = 0;
        (int X, int Y)[] directions = { (1,0), (-1,0), (0,1), (0,-1), (1,1), (-1,1), (1,-1), (-1,-1) };
        while (queue.Count > 0 && expanded++ < maxNodes)
        {
            int current = queue.Dequeue();
            if (current == goalIndex) break;
            int cx = current % _world.Width; int cy = current / _world.Width;
            foreach ((int dx, int dy) in directions)
            {
                int nx = cx + dx; int ny = cy + dy;
                if (!_world.IsInside(nx, ny)) continue;
                int next = ny * _world.Width + nx;
                if (previous.ContainsKey(next)) continue;
                if (_world.GetTerrain(nx, ny) is not (TerrainType.DeepOcean or TerrainType.ShallowWater)) continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }
        if (!previous.ContainsKey(goalIndex)) return new List<GridPoint>();
        var path = new List<GridPoint>();
        for (int cursor = goalIndex; cursor >= 0; cursor = previous[cursor])
        {
            path.Add(new GridPoint(cursor % _world.Width, cursor / _world.Width));
            if (cursor == startIndex) break;
        }
        path.Reverse();
        return path;
    }

    private bool KingdomAtWar(ulong kingdomId)
        => _simulation.State.Kingdoms.GetValueOrDefault(kingdomId)?.Relations.Values.Any(value => value <= -60) == true;

    private int RelationValue(ulong a, ulong b)
        => _simulation.State.Kingdoms.GetValueOrDefault(a)?.Relations.GetValueOrDefault(b) ?? 0;

    private int PopulationOfCity(ulong settlementId)
        => _simulation.State.Entities.Values.Count(e => e.IsAlive && e.Species == SpeciesKind.Settler && e.SettlementId == settlementId);

    private static string RelicName(RuinType type, ulong id) => type switch
    {
        RuinType.AncientTemple => $"ศิลาจารึกแห่งรุ่งอรุณ #{id}",
        RuinType.FallenCity => $"มงกุฎแห่งนครที่สาบสูญ #{id}",
        RuinType.SunkenShrine => $"ไข่มุกแห่งทะเลลึก #{id}",
        RuinType.MageVault => $"ผลึกอาคมโบราณ #{id}",
        _ => $"ธงศึกของผู้ไร้นาม #{id}",
    };
}
