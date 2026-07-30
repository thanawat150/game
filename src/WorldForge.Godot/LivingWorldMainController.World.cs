using System.Diagnostics;
using System.Text.Json;
using Godot;
using WorldForge.Core.Editing;
using WorldForge.Core.Persistence;
using WorldForge.Core.Simulation;
using WorldForge.Core.World;
using WorldForge.Presentation;

namespace WorldForge;

public sealed partial class LivingWorldMainController : Node2D
{
    private void StartConfiguredWorld()
    {
        try
        {
            long seed = SeedUtility.ParseOrHash(_setupSeed.Text);
            int size = (int)_setupWorldSize.GetSelectedId();
            int kingdomCount = (int)_setupKingdoms.Value;
            int populationPerKingdom = (int)_setupPopulationPerKingdom.Value;
            int initialPopulation = kingdomCount * populationPerKingdom + (int)_setupGrazers.Value + (int)_setupPredators.Value + (int)_setupMonsters.Value + (int)_setupFish.Value;
            int cap = (int)_setupPopulationCap.Value;
            if (initialPopulation > cap)
                throw new InvalidOperationException($"ประชากรเริ่มต้น {initialPopulation:N0} มากกว่าเพดาน {cap:N0}");

            var config = new WorldGenerationConfig
            {
                Seed = seed,
                Width = size,
                Height = size,
                ChunkSize = 64,
                SeaLevel = (float)_setupSeaLevel.Value,
            };
            _world = WorldGenerator.Generate(config);
            _simulation = new GrandSimulation(_world, seed ^ 0x5A17_2026L);
            _clock = new SimulationClock();
            _terrainEditor.ClearHistory();
            _terrainRenderer.Bind(_world);

            List<Vector2I> land = CollectTiles(t => t is TerrainType.Grassland or TerrainType.Forest or TerrainType.Beach);
            List<Vector2I> water = CollectTiles(t => t is TerrainType.DeepOcean or TerrainType.ShallowWater);
            if (land.Count < kingdomCount)
                throw new InvalidOperationException("พื้นที่บกไม่เพียงพอสำหรับจำนวนอาณาจักรที่เลือก");

            var random = new Random(unchecked((int)(seed ^ (seed >> 32))));
            var kingdoms = new List<KingdomState>();
            for (int i = 0; i < kingdomCount; i++)
            {
                int targetX = (int)((i + 1) / (float)(kingdomCount + 1) * _world.Width);
                int targetY = i % 2 == 0 ? _world.Height / 3 : _world.Height * 2 / 3;
                Vector2I tile = FindSuitableNear(targetX, targetY, land);
                kingdoms.Add(CreateCivilizationAt(tile, populationPerKingdom, $"อาณาจักร {i + 1}", (GovernmentType)(i % Enum.GetValues<GovernmentType>().Length)));
            }
            int relationValue = (InitialRelation)_setupRelation.GetSelectedId() switch
            {
                InitialRelation.Peaceful => 55,
                InitialRelation.Neutral => 0,
                InitialRelation.Hostile => -45,
                InitialRelation.War => -90,
                _ => 0,
            };
            for (int i = 0; i < kingdoms.Count; i++)
                for (int j = i + 1; j < kingdoms.Count; j++)
                    _simulation.SetRelation(kingdoms[i].Id, kingdoms[j].Id, relationValue);

            SpawnMany(SpeciesKind.Grazer, land, (int)_setupGrazers.Value, random);
            SpawnMany(SpeciesKind.Predator, land, (int)_setupPredators.Value, random);
            SpawnMany(SpeciesKind.Monster, land, (int)_setupMonsters.Value, random);
            SpawnMany(SpeciesKind.Fish, water, (int)_setupFish.Value, random);

            _director = new LivingWorldDirector(_world, _simulation, seed, _setupWorldName.Text);
            _director.State.Settings.EnableWeather = _setupWeather.ButtonPressed;
            _director.State.Settings.EnableEvents = _setupEvents.ButtonPressed;
            _director.State.Settings.EnableAudio = _setupAudio.ButtonPressed;
            _director.State.Settings.AutoPerformance = _setupAutoPerformance.ButtonPressed;
            _director.SelectScenario((ScenarioKind)_setupScenario.GetSelectedId());

            SimulationPerformanceProfile profile = (SimulationPerformanceProfile)_setupProfile.GetSelectedId();
            _budget = SimulationBudgetOptions.ForProfile(profile, cap);
            _budget.EnableReproduction = _setupReproduction.ButtonPressed;
            _budget.EnableAutomaticDiplomacy = _setupAutomaticWar.ButtonPressed;
            _budget.EnableArmies = _setupAutomaticWar.ButtonPressed;
            _director.State.Population.GlobalPopulationLimit = cap;
            _director.State.Population.SpeciesCaps[SpeciesKind.Settler] = Math.Max(kingdomCount * populationPerKingdom + 100, (int)(cap * 0.7));
            _director.State.Population.SpeciesCaps[SpeciesKind.Grazer] = Math.Max((int)_setupGrazers.Value * 4, 50);
            _director.State.Population.SpeciesCaps[SpeciesKind.Predator] = Math.Max((int)_setupPredators.Value * 4, 20);
            _director.State.Population.SpeciesCaps[SpeciesKind.Monster] = Math.Max((int)_setupMonsters.Value * 3, 6);
            _director.State.Population.SpeciesCaps[SpeciesKind.Fish] = Math.Max((int)_setupFish.Value * 4, 50);

            _livingRenderer.Bind(_world, _simulation, _director);
            _miniMap.Bind(_world, _simulation, _director);
            _checksum = WorldChecksum.Compute(_world);
            float worldPixels = _world.Width * _terrainRenderer.TilePixelSize;
            _camera.Position = new Vector2(worldPixels / 2f, worldPixels / 2f);
            _camera.Zoom = size >= 384 ? new Vector2(0.48f, 0.48f) : new Vector2(0.72f, 0.72f);
            SyncRuntimeControls();
            _setupLayer.Visible = false;
            _gameLayer.Visible = true;
            _tutorialStep = 0;
            ShowTutorial();
            _statusLabel.Text = $"สร้าง {_director.State.WorldName} แล้ว — {_simulation.State.Entities.Count:N0} สิ่งมีชีวิต";
            _renderDirty = true;
            RefreshUi();
        }
        catch (Exception exception)
        {
            _setupError.Text = $"สร้างโลกไม่สำเร็จ: {exception.Message}";
        }
    }

    private void ShowSetup()
    {
        _setupLayer.Visible = true;
        _gameLayer.Visible = _world is not null;
        _tutorialLayer.Visible = false;
        _setupError.Text = string.Empty;
    }

    private void HideSetup()
    {
        if (_world is null)
            return;
        _setupLayer.Visible = false;
        _gameLayer.Visible = true;
    }

    private void ShowTutorial()
    {
        _tutorialLayer.Visible = true;
        UpdateTutorial();
    }

    private void NextTutorial()
    {
        _tutorialStep++;
        if (_tutorialStep >= 5) CloseTutorial();
        else UpdateTutorial();
    }

    private void CloseTutorial()
    {
        _tutorialLayer.Visible = false;
        if (_director is not null && !_director.State.TutorialFlags.Contains("completed"))
            _director.State.TutorialFlags.Add("completed");
    }

    private void UpdateTutorial()
    {
        _tutorialText.Text = _tutorialStep switch
        {
            0 => "1/5 โลกดำเนินชีวิตเอง: ประชาชนมีบ้าน อาชีพ ตารางชีวิต ครอบครัว การอพยพ และปฏิกิริยาต่อภัย",
            1 => "2/5 ใช้ Minimap และ Overlay ด้านซ้ายเพื่อดูประชากร อาหาร ความสุข โรค สงคราม การค้า และจุดที่ใช้ทรัพยากรสูง",
            2 => "3/5 คลิกขวาที่คน เมือง หรืออาณาจักร แล้วใช้แผงด้านขวาเพื่อเปลี่ยนชื่อ ภาษี นโยบายการเกิด อาคาร กักกัน และการอพยพ",
            3 => "4/5 เหตุการณ์โลกจะให้ทางเลือกซึ่งเปลี่ยนอาหาร เศรษฐกิจ ความสุข โรค และเสถียรภาพจริง กด Chronicle เพื่อกระโดดไปยังจุดเกิดเหตุ",
            _ => "5/5 Auto Performance จะลด AI, A* และ Render Hz เมื่อ FPS ต่ำ คุณปรับเองได้ และใช้ F5/F9 สำหรับ Quick Save/Load",
        };
    }

    private void UseSelectedTool(Vector2I tile)
    {
        if (_world is null || _simulation is null || !_world.IsInside(tile.X, tile.Y))
            return;
        try
        {
            switch (SelectedTool)
            {
                case InteractionTool.Inspect: InspectAt(tile); break;
                case InteractionTool.PaintTerrain: PaintAt(tile); break;
                case InteractionTool.SpawnGrazer: SpawnAt(SpeciesKind.Grazer, tile); break;
                case InteractionTool.SpawnPredator: SpawnAt(SpeciesKind.Predator, tile); break;
                case InteractionTool.SpawnSettler: SpawnAt(SpeciesKind.Settler, tile); break;
                case InteractionTool.SpawnMonster: SpawnAt(SpeciesKind.Monster, tile); break;
                case InteractionTool.SpawnFish: SpawnAt(SpeciesKind.Fish, tile); break;
                case InteractionTool.CreateCivilization:
                    CreateCivilizationAt(tile, 8, null, GovernmentType.Council);
                    _director?.EnsureWorldRecords();
                    break;
                case InteractionTool.PowerCreateForest: ApplyPower(GodPowerType.CreateForest, tile); break;
                case InteractionTool.PowerBlessing: ApplyPower(GodPowerType.Blessing, tile); break;
                case InteractionTool.PowerCurse: ApplyPower(GodPowerType.Curse, tile); break;
                case InteractionTool.PowerLightning: ApplyPower(GodPowerType.Lightning, tile); break;
                case InteractionTool.PowerPlague: ApplyPower(GodPowerType.Plague, tile); break;
                case InteractionTool.PowerMeteor: ApplyPower(GodPowerType.Meteor, tile); break;
            }
            _renderDirty = true;
            RefreshUi();
        }
        catch (Exception exception)
        {
            _statusLabel.Text = $"ใช้เครื่องมือไม่สำเร็จ: {exception.Message}";
        }
    }

    private void InspectAt(Vector2I tile)
    {
        if (_simulation is null)
            return;
        SimEntity? entity = _simulation.State.Entities.Values
            .Where(e => e.IsAlive)
            .OrderBy(e => DistanceSquared(e.X, e.Y, tile.X, tile.Y))
            .ThenBy(e => e.Id)
            .FirstOrDefault();
        SettlementState? city = _simulation.State.Settlements.Values
            .OrderBy(s => DistanceSquared(s.X, s.Y, tile.X, tile.Y))
            .ThenBy(s => s.Id)
            .FirstOrDefault();

        int entityDistance = entity is null ? int.MaxValue : DistanceSquared(entity.X, entity.Y, tile.X, tile.Y);
        int cityDistance = city is null ? int.MaxValue : DistanceSquared(city.X, city.Y, tile.X, tile.Y);
        if (entityDistance <= 9 && entityDistance <= cityDistance)
            _livingRenderer.SelectEntity(entity!.Id);
        else if (cityDistance <= 144)
            _livingRenderer.SelectSettlement(city!.Id);
        else
            _livingRenderer.ClearSelection();
        LoadManagementControlsFromSelection();
        RefreshUi();
    }

    private void SpawnAt(SpeciesKind species, Vector2I tile)
    {
        if (_simulation is null || _director is null)
            return;
        SimEntity entity = _simulation.SpawnEntity(species, tile.X, tile.Y);
        if (species == SpeciesKind.Settler)
        {
            SettlementState? nearest = _simulation.State.Settlements.Values.OrderBy(s => DistanceSquared(s.X, s.Y, tile.X, tile.Y)).FirstOrDefault();
            if (nearest is not null)
            {
                entity.SettlementId = nearest.Id;
                entity.KingdomId = nearest.KingdomId;
                entity.AgeDays = 20 * 360;
            }
            _director.EnsureCitizen(entity);
        }
        _livingRenderer.SelectEntity(entity.Id);
        _statusLabel.Text = $"วาง {species} ที่ {tile.X},{tile.Y}";
    }

    private void ApplyPower(GodPowerType power, Vector2I tile)
    {
        if (_simulation is null || _world is null)
            return;
        _simulation.ApplyPower(power, tile.X, tile.Y, Math.Max(1, BrushRadius));
        if (power == GodPowerType.CreateForest)
        {
            _terrainRenderer.Bind(_world);
            _checksum = WorldChecksum.Compute(_world);
        }
        PlayEventSound();
        _statusLabel.Text = $"ใช้พลัง {power}";
    }

    private void PaintAt(Vector2I tile)
    {
        if (_world is null || !_world.IsInside(tile.X, tile.Y))
            return;
        int changed = _terrainEditor.Paint(_world, tile.X, tile.Y, BrushRadius, SelectedTerrain);
        if (changed <= 0)
            return;
        _terrainRenderer.RefreshChunks(_terrainEditor.DrainDirtyChunks());
        _checksum = WorldChecksum.Compute(_world);
        _renderDirty = true;
    }

    private void UndoTerrain()
    {
        if (_world is null || !_terrainEditor.Undo(_world))
        {
            _statusLabel.Text = "ไม่มี Terrain ให้ย้อนกลับ";
            return;
        }
        _terrainRenderer.Bind(_world);
        _checksum = WorldChecksum.Compute(_world);
        _renderDirty = true;
    }

    private KingdomState CreateCivilizationAt(Vector2I tile, int population, string? kingdomName, GovernmentType government)
    {
        if (_world is null || _simulation is null)
            throw new InvalidOperationException("Simulation is not ready.");
        TerrainType terrain = _world.GetTerrain(tile.X, tile.Y);
        if (terrain is not (TerrainType.Grassland or TerrainType.Forest or TerrainType.Beach))
            throw new InvalidOperationException("เมืองต้องอยู่บนทุ่งหญ้า ป่า หรือชายหาด");

        var settlers = new List<ulong>();
        for (int i = 0; i < Math.Max(5, population); i++)
        {
            SimEntity settler = _simulation.SpawnEntity(SpeciesKind.Settler, tile.X, tile.Y, $"ประชากร {_simulation.State.NextEntityId}");
            settler.AgeDays = (18 + i % 35) * 360;
            settler.Morale = 50 + i % 20;
            settlers.Add(settler.Id);
        }
        SettlementState settlement = _simulation.FoundSettlement(settlers, $"นคร {_simulation.State.NextSettlementId}");
        KingdomState kingdom = _simulation.FoundKingdom(settlement.Id, kingdomName ?? $"อาณาจักร {_simulation.State.NextKingdomId}", government);
        foreach (KingdomState other in _simulation.State.Kingdoms.Values.Where(k => k.Id != kingdom.Id))
            _simulation.SetRelation(kingdom.Id, other.Id, 0);
        _director?.EnsureWorldRecords();
        _livingRenderer.SelectKingdom(kingdom.Id);
        return kingdom;
    }

    private List<Vector2I> CollectTiles(Func<TerrainType, bool> predicate)
    {
        var result = new List<Vector2I>();
        if (_world is null) return result;
        for (int y = 0; y < _world.Height; y++)
            for (int x = 0; x < _world.Width; x++)
                if (predicate(_world.GetTerrain(x, y))) result.Add(new Vector2I(x, y));
        return result;
    }

    private static Vector2I FindSuitableNear(int targetX, int targetY, IReadOnlyList<Vector2I> candidates) =>
        candidates.OrderBy(p => (p.X - targetX) * (p.X - targetX) + (p.Y - targetY) * (p.Y - targetY)).First();

    private void SpawnMany(SpeciesKind species, IReadOnlyList<Vector2I> candidates, int count, Random random)
    {
        if (_simulation is null || candidates.Count == 0)
            return;
        for (int i = 0; i < count; i++)
        {
            Vector2I tile = candidates[random.Next(candidates.Count)];
            _simulation.SpawnEntity(species, tile.X, tile.Y);
        }
    }
}
