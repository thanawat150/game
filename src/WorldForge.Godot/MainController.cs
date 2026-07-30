using Godot;
using WorldForge.Core.Editing;
using WorldForge.Core.Persistence;
using WorldForge.Core.Simulation;
using WorldForge.Core.World;
using WorldForge.Presentation;

namespace WorldForge;

public sealed partial class MainController : Node2D
{
    private enum InteractionTool
    {
        Inspect = 0,
        PaintTerrain = 1,
        SpawnGrazer = 10,
        SpawnPredator = 11,
        SpawnSettler = 12,
        SpawnMonster = 13,
        SpawnFish = 14,
        CreateCivilization = 20,
        PowerCreateForest = 30,
        PowerBlessing = 31,
        PowerCurse = 32,
        PowerLightning = 33,
        PowerPlague = 34,
        PowerMeteor = 35,
    }

    private readonly TerrainEditor _terrainEditor = new();
    private readonly WorldSaveService _saveService = new();
    private WorldMap? _world;
    private GrandSimulation? _simulation;
    private SimulationClock _clock = new();
    private WorldChunkRenderer _renderer = null!;
    private SimulationRenderer _simulationRenderer = null!;
    private BrushOverlay _brushOverlay = null!;
    private Camera2D _camera = null!;
    private LineEdit _seedInput = null!;
    private OptionButton _toolSelector = null!;
    private OptionButton _terrainSelector = null!;
    private OptionButton _kingdomASelector = null!;
    private OptionButton _kingdomBSelector = null!;
    private HSlider _brushSlider = null!;
    private Label _brushValue = null!;
    private Label _toolHelpLabel = null!;
    private Label _statusLabel = null!;
    private Label _debugLabel = null!;
    private Label _inspectorLabel = null!;
    private Label _chronicleLabel = null!;
    private Button _pauseButton = null!;
    private bool _isPanning;
    private int _ticksThisSecond;
    private int _measuredTps;
    private double _metricsAccumulator;
    private double _uiAccumulator;
    private string _checksum = string.Empty;

    private InteractionTool SelectedTool => (InteractionTool)_toolSelector.GetSelectedId();
    private TerrainType SelectedTerrain => (TerrainType)_terrainSelector.GetSelectedId();
    private int BrushRadius => Math.Max(0, (int)_brushSlider.Value - 1);
    private string SavePath => ProjectSettings.GlobalizePath("user://saves/slot_1.wfg.json");
    private string SimulationSavePath => ProjectSettings.GlobalizePath("user://saves/slot_1.sim.json");

    public override void _Ready()
    {
        DisplayServer.WindowSetTitle("WorldForge: Pixel Gods — Living World Playtest");

        _renderer = new WorldChunkRenderer { Name = "WorldRenderer", ZIndex = 0 };
        AddChild(_renderer);
        _simulationRenderer = new SimulationRenderer { Name = "SimulationRenderer", ZIndex = 25 };
        AddChild(_simulationRenderer);
        _brushOverlay = new BrushOverlay { Name = "BrushOverlay", ZIndex = 50 };
        AddChild(_brushOverlay);
        _camera = new Camera2D { Name = "WorldCamera", Enabled = true, Position = new Vector2(512, 512) };
        AddChild(_camera);

        BuildInterface();
        GenerateWorld();
    }

    public override void _Process(double delta)
    {
        int steps = _clock.Advance(delta, tick =>
        {
            if (tick % 10 == 0 && _simulation is not null)
            {
                _simulation.AdvanceDay();
                _simulationRenderer.Refresh();
            }
        });
        _ticksThisSecond += steps;
        _metricsAccumulator += delta;
        _uiAccumulator += delta;

        if (_metricsAccumulator >= 1.0)
        {
            _measuredTps = _ticksThisSecond;
            _ticksThisSecond = 0;
            _metricsAccumulator -= 1.0;
        }

        if (_uiAccumulator >= 0.2)
        {
            _uiAccumulator = 0;
            UpdateDebugOverlay();
            UpdateInspector();
            UpdateChronicle();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
            {
                ZoomCamera(1.18f);
                GetViewport().SetInputAsHandled();
                return;
            }
            if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
            {
                ZoomCamera(1f / 1.18f);
                GetViewport().SetInputAsHandled();
                return;
            }
            if (mouseButton.ButtonIndex == MouseButton.Middle)
            {
                _isPanning = mouseButton.Pressed;
                GetViewport().SetInputAsHandled();
                return;
            }
            if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
            {
                InspectAtMouse();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
            {
                UseSelectedToolAtMouse();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event is InputEventMouseMotion motion)
        {
            if (_isPanning)
            {
                _camera.Position -= motion.Relative / _camera.Zoom;
                GetViewport().SetInputAsHandled();
            }
            else
            {
                UpdateBrushPreview();
                if ((motion.ButtonMask & MouseButtonMask.Left) != 0 && SelectedTool == InteractionTool.PaintTerrain)
                    PaintAtMouse();
            }
        }
    }

    private void BuildInterface()
    {
        var canvas = new CanvasLayer { Name = "Interface" };
        AddChild(canvas);

        var topPanel = new PanelContainer
        {
            AnchorRight = 1,
            OffsetBottom = 56,
        };
        canvas.AddChild(topPanel);
        var top = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        top.AddThemeConstantOverride("separation", 7);
        topPanel.AddChild(top);
        top.AddChild(new Label { Text = "WorldForge | Seed" });
        _seedInput = new LineEdit { Text = "1502026", CustomMinimumSize = new Vector2(130, 0) };
        top.AddChild(_seedInput);
        top.AddChild(CreateButton("สร้างโลกมีชีวิต", GenerateWorld));
        top.AddChild(CreateButton("บันทึก", SaveWorld));
        top.AddChild(CreateButton("โหลด", LoadWorld));
        _pauseButton = CreateButton("หยุดเวลา", TogglePause);
        top.AddChild(_pauseButton);
        top.AddChild(CreateButton("x1", () => SetSpeed(1)));
        top.AddChild(CreateButton("x2", () => SetSpeed(2)));
        top.AddChild(CreateButton("x4", () => SetSpeed(4)));
        top.AddChild(CreateButton("x8", () => SetSpeed(8)));
        top.AddChild(CreateButton("MAX", () => SetSpeed(32)));

        var toolsPanel = new PanelContainer
        {
            OffsetLeft = 8,
            OffsetTop = 64,
            OffsetRight = 292,
            AnchorBottom = 1,
            OffsetBottom = -42,
        };
        canvas.AddChild(toolsPanel);
        var tools = new VBoxContainer();
        tools.AddThemeConstantOverride("separation", 7);
        toolsPanel.AddChild(tools);
        tools.AddChild(new Label { Text = "เครื่องมือโลกมีชีวิต" });

        _toolSelector = new OptionButton { CustomMinimumSize = new Vector2(264, 0) };
        AddToolOption("ตรวจสอบสิ่งมีชีวิต/เมือง", InteractionTool.Inspect);
        AddToolOption("วาด Terrain", InteractionTool.PaintTerrain);
        AddToolOption("วางสัตว์กินพืช", InteractionTool.SpawnGrazer);
        AddToolOption("วางนักล่า", InteractionTool.SpawnPredator);
        AddToolOption("วางประชากร", InteractionTool.SpawnSettler);
        AddToolOption("วางสัตว์ประหลาด", InteractionTool.SpawnMonster);
        AddToolOption("วางปลา", InteractionTool.SpawnFish);
        AddToolOption("สร้างเมืองและอาณาจักร", InteractionTool.CreateCivilization);
        AddToolOption("พลังเทพ: สร้างป่า", InteractionTool.PowerCreateForest);
        AddToolOption("พลังเทพ: อวยพร", InteractionTool.PowerBlessing);
        AddToolOption("พลังเทพ: สาป", InteractionTool.PowerCurse);
        AddToolOption("พลังเทพ: สายฟ้า", InteractionTool.PowerLightning);
        AddToolOption("พลังเทพ: โรคระบาด", InteractionTool.PowerPlague);
        AddToolOption("พลังเทพ: อุกกาบาต", InteractionTool.PowerMeteor);
        _toolSelector.Select(0);
        _toolSelector.ItemSelected += _ =>
        {
            UpdateToolHelp();
            UpdateBrushPreview();
        };
        tools.AddChild(_toolSelector);

        tools.AddChild(new Label { Text = "ชนิด Terrain" });
        _terrainSelector = new OptionButton();
        AddTerrainOption("มหาสมุทรลึก", TerrainType.DeepOcean);
        AddTerrainOption("น้ำตื้น", TerrainType.ShallowWater);
        AddTerrainOption("ชายหาด", TerrainType.Beach);
        AddTerrainOption("ทุ่งหญ้า", TerrainType.Grassland);
        AddTerrainOption("ป่า", TerrainType.Forest);
        AddTerrainOption("ภูเขา", TerrainType.Mountain);
        _terrainSelector.Select((int)TerrainType.Grassland);
        tools.AddChild(_terrainSelector);

        tools.AddChild(new Label { Text = "ขนาดแปรง/รัศมีพลัง" });
        var brushRow = new HBoxContainer();
        tools.AddChild(brushRow);
        _brushSlider = new HSlider
        {
            MinValue = 1,
            MaxValue = 12,
            Step = 1,
            Value = 3,
            CustomMinimumSize = new Vector2(215, 0),
        };
        _brushSlider.ValueChanged += value =>
        {
            _brushValue.Text = $"{(int)value}";
            UpdateBrushPreview();
        };
        brushRow.AddChild(_brushSlider);
        _brushValue = new Label { Text = "3" };
        brushRow.AddChild(_brushValue);

        tools.AddChild(CreateButton("ย้อนกลับ Terrain", UndoTerrain));
        tools.AddChild(CreateButton("จำลองเพิ่ม 30 วัน", AdvanceThirtyDays));
        _toolHelpLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(260, 120),
        };
        tools.AddChild(_toolHelpLabel);
        UpdateToolHelp();

        var diplomacyPanel = new PanelContainer();
        diplomacyPanel.CustomMinimumSize = new Vector2(260, 190);
        tools.AddChild(diplomacyPanel);
        var diplomacy = new VBoxContainer();
        diplomacy.AddThemeConstantOverride("separation", 5);
        diplomacyPanel.AddChild(diplomacy);
        diplomacy.AddChild(new Label { Text = "การทูตระหว่างอาณาจักร" });
        _kingdomASelector = new OptionButton();
        _kingdomBSelector = new OptionButton();
        diplomacy.AddChild(_kingdomASelector);
        diplomacy.AddChild(_kingdomBSelector);
        var relationRow = new HBoxContainer();
        relationRow.AddChild(CreateButton("สงคราม", () => ApplyDiplomacy(-90)));
        relationRow.AddChild(CreateButton("เป็นกลาง", () => ApplyDiplomacy(0)));
        relationRow.AddChild(CreateButton("พันธมิตร", () => ApplyDiplomacy(90)));
        diplomacy.AddChild(relationRow);

        var rightPanel = new PanelContainer
        {
            AnchorLeft = 1,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = -372,
            OffsetTop = 64,
            OffsetRight = -8,
            OffsetBottom = -42,
        };
        canvas.AddChild(rightPanel);
        var right = new VBoxContainer();
        right.AddThemeConstantOverride("separation", 7);
        rightPanel.AddChild(right);
        right.AddChild(new Label { Text = "สถานะโลก" });
        _debugLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        right.AddChild(_debugLabel);
        right.AddChild(new HSeparator());
        right.AddChild(new Label { Text = "Inspector" });
        _inspectorLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(340, 190),
        };
        right.AddChild(_inspectorLabel);
        right.AddChild(new HSeparator());
        right.AddChild(new Label { Text = "Chronicle เหตุการณ์ล่าสุด" });
        var chronicleScroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(340, 180),
        };
        right.AddChild(chronicleScroll);
        _chronicleLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(325, 0),
        };
        chronicleScroll.AddChild(_chronicleLabel);

        _statusLabel = new Label
        {
            AnchorTop = 1,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 8,
            OffsetTop = -34,
            OffsetRight = -8,
            OffsetBottom = -6,
            Text = "พร้อม",
        };
        canvas.AddChild(_statusLabel);
    }

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        return button;
    }

    private void AddToolOption(string displayName, InteractionTool tool) => _toolSelector.AddItem(displayName, (int)tool);
    private void AddTerrainOption(string displayName, TerrainType terrain) => _terrainSelector.AddItem(displayName, (int)terrain);

    private void GenerateWorld()
    {
        try
        {
            long seed = SeedUtility.ParseOrHash(_seedInput.Text);
            var config = new WorldGenerationConfig
            {
                Seed = seed,
                Width = 256,
                Height = 256,
                ChunkSize = 64,
                SeaLevel = 0.48f,
            };
            _world = WorldGenerator.Generate(config);
            _simulation = new GrandSimulation(_world, seed ^ 0x5A17_2026L);
            _clock = new SimulationClock();
            _terrainEditor.ClearHistory();
            _renderer.Bind(_world);
            SeedLivingWorld(seed);
            _simulationRenderer.Bind(_simulation);
            _checksum = WorldChecksum.Compute(_world);
            float worldPixels = _world.Width * _renderer.TilePixelSize;
            _camera.Position = new Vector2(worldPixels / 2f, worldPixels / 2f);
            _camera.Zoom = new Vector2(0.72f, 0.72f);
            PopulateKingdomSelectors();
            _statusLabel.Text = $"สร้างโลกมีชีวิต Seed {seed} แล้ว — {_simulation.State.Entities.Count:N0} สิ่งมีชีวิต, {_simulation.State.Kingdoms.Count} อาณาจักร";
            UpdatePauseText();
            UpdateBrushPreview();
            RefreshUiNow();
        }
        catch (Exception exception)
        {
            ReportError("สร้างโลกไม่สำเร็จ", exception);
        }
    }

    private void SeedLivingWorld(long seed)
    {
        if (_world is null || _simulation is null)
            return;

        List<Vector2I> land = CollectTiles(t => t is TerrainType.Grassland or TerrainType.Forest or TerrainType.Beach);
        List<Vector2I> water = CollectTiles(t => t is TerrainType.DeepOcean or TerrainType.ShallowWater);
        if (land.Count < 2)
            throw new InvalidOperationException("โลกไม่มีพื้นที่บกเพียงพอสำหรับสิ่งมีชีวิต");

        Vector2I first = FindSuitableNear(_world.Width / 3, _world.Height / 2, land);
        Vector2I second = FindSuitableNear(_world.Width * 2 / 3, _world.Height / 2, land);
        KingdomState firstKingdom = CreateCivilizationAt(first, "อาณาจักรอรุณ", GovernmentType.Council);
        KingdomState secondKingdom = CreateCivilizationAt(second, "อาณาจักรพฤกษา", GovernmentType.Monarchy);
        _simulation.SetRelation(firstKingdom.Id, secondKingdom.Id, -10);

        var random = new Random(unchecked((int)(seed ^ (seed >> 32))));
        SpawnMany(SpeciesKind.Grazer, land, 75, random);
        SpawnMany(SpeciesKind.Predator, land, 18, random);
        SpawnMany(SpeciesKind.Monster, land, 6, random);
        if (water.Count > 0)
            SpawnMany(SpeciesKind.Fish, water, 45, random);
    }

    private List<Vector2I> CollectTiles(Func<TerrainType, bool> predicate)
    {
        var result = new List<Vector2I>();
        if (_world is null)
            return result;
        for (int y = 0; y < _world.Height; y++)
            for (int x = 0; x < _world.Width; x++)
                if (predicate(_world.GetTerrain(x, y)))
                    result.Add(new Vector2I(x, y));
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

    private KingdomState CreateCivilizationAt(Vector2I tile, string? kingdomName = null, GovernmentType government = GovernmentType.Council)
    {
        if (_world is null || _simulation is null)
            throw new InvalidOperationException("Simulation is not ready.");
        TerrainType terrain = _world.GetTerrain(tile.X, tile.Y);
        if (terrain is not (TerrainType.Grassland or TerrainType.Forest or TerrainType.Beach))
            throw new InvalidOperationException("เมืองต้องสร้างบนทุ่งหญ้า ป่า หรือชายหาด");

        var settlers = new List<ulong>();
        for (int i = 0; i < 8; i++)
        {
            SimEntity settler = _simulation.SpawnEntity(SpeciesKind.Settler, tile.X, tile.Y, $"ประชากร {_simulation.State.NextEntityId}");
            settler.AgeDays = (18 + i) * 360;
            settler.Morale = 55 + i;
            settlers.Add(settler.Id);
        }
        string cityName = $"นคร {_simulation.State.NextSettlementId}";
        SettlementState settlement = _simulation.FoundSettlement(settlers, cityName);
        string realmName = kingdomName ?? $"อาณาจักร {_simulation.State.NextKingdomId}";
        KingdomState kingdom = _simulation.FoundKingdom(settlement.Id, realmName, government);

        foreach (KingdomState other in _simulation.State.Kingdoms.Values.Where(k => k.Id != kingdom.Id))
            _simulation.SetRelation(kingdom.Id, other.Id, 0);
        return kingdom;
    }

    private void UseSelectedToolAtMouse()
    {
        if (_world is null || _simulation is null)
            return;
        Vector2I tile = MouseTile();
        if (!_world.IsInside(tile.X, tile.Y))
            return;

        try
        {
            switch (SelectedTool)
            {
                case InteractionTool.Inspect:
                    InspectAt(tile);
                    break;
                case InteractionTool.PaintTerrain:
                    PaintAt(tile);
                    break;
                case InteractionTool.SpawnGrazer:
                    SpawnAt(SpeciesKind.Grazer, tile);
                    break;
                case InteractionTool.SpawnPredator:
                    SpawnAt(SpeciesKind.Predator, tile);
                    break;
                case InteractionTool.SpawnSettler:
                    SpawnAt(SpeciesKind.Settler, tile);
                    break;
                case InteractionTool.SpawnMonster:
                    SpawnAt(SpeciesKind.Monster, tile);
                    break;
                case InteractionTool.SpawnFish:
                    SpawnAt(SpeciesKind.Fish, tile);
                    break;
                case InteractionTool.CreateCivilization:
                    KingdomState kingdom = CreateCivilizationAt(tile);
                    PopulateKingdomSelectors();
                    _simulationRenderer.SelectKingdom(kingdom.Id);
                    _statusLabel.Text = $"สร้าง {kingdom.Name} แล้ว";
                    break;
                case InteractionTool.PowerCreateForest:
                    ApplyPowerAt(GodPowerType.CreateForest, tile);
                    break;
                case InteractionTool.PowerBlessing:
                    ApplyPowerAt(GodPowerType.Blessing, tile);
                    break;
                case InteractionTool.PowerCurse:
                    ApplyPowerAt(GodPowerType.Curse, tile);
                    break;
                case InteractionTool.PowerLightning:
                    ApplyPowerAt(GodPowerType.Lightning, tile);
                    break;
                case InteractionTool.PowerPlague:
                    ApplyPowerAt(GodPowerType.Plague, tile);
                    break;
                case InteractionTool.PowerMeteor:
                    ApplyPowerAt(GodPowerType.Meteor, tile);
                    break;
            }
            _simulationRenderer.Refresh();
            RefreshUiNow();
        }
        catch (Exception exception)
        {
            ReportError("ใช้เครื่องมือไม่สำเร็จ", exception);
        }
    }

    private void SpawnAt(SpeciesKind species, Vector2I tile)
    {
        if (_simulation is null)
            return;
        SimEntity entity = _simulation.SpawnEntity(species, tile.X, tile.Y);
        _simulationRenderer.SelectEntity(entity.Id);
        _statusLabel.Text = $"วาง {species} ที่ ({tile.X}, {tile.Y})";
    }

    private void ApplyPowerAt(GodPowerType power, Vector2I tile)
    {
        if (_simulation is null || _world is null)
            return;
        _simulation.ApplyPower(power, tile.X, tile.Y, Math.Max(1, BrushRadius));
        if (power == GodPowerType.CreateForest)
        {
            _renderer.Bind(_world);
            _checksum = WorldChecksum.Compute(_world);
        }
        _statusLabel.Text = $"ใช้พลัง {power} ที่ ({tile.X}, {tile.Y})";
    }

    private void PaintAtMouse() => PaintAt(MouseTile());

    private void PaintAt(Vector2I tile)
    {
        if (_world is null || !_world.IsInside(tile.X, tile.Y))
            return;
        int changed = _terrainEditor.Paint(_world, tile.X, tile.Y, BrushRadius, SelectedTerrain);
        if (changed <= 0)
            return;
        _renderer.RefreshChunks(_terrainEditor.DrainDirtyChunks());
        _checksum = WorldChecksum.Compute(_world);
        _statusLabel.Text = $"แก้ไข Terrain {changed} tiles ที่ ({tile.X}, {tile.Y})";
    }

    private void UndoTerrain()
    {
        if (_world is null)
            return;
        if (!_terrainEditor.Undo(_world))
        {
            _statusLabel.Text = "ไม่มีคำสั่ง Terrain ให้ย้อนกลับ";
            return;
        }
        _renderer.RefreshChunks(_terrainEditor.DrainDirtyChunks());
        _checksum = WorldChecksum.Compute(_world);
        _statusLabel.Text = "ย้อนกลับการแก้ไข Terrain แล้ว";
    }

    private void InspectAtMouse() => InspectAt(MouseTile());

    private void InspectAt(Vector2I tile)
    {
        if (_simulation is null)
            return;

        SimEntity? entity = _simulation.State.Entities.Values
            .Where(e => DistanceSquared(e.X, e.Y, tile.X, tile.Y) <= 6)
            .OrderBy(e => DistanceSquared(e.X, e.Y, tile.X, tile.Y))
            .ThenBy(e => e.Id)
            .FirstOrDefault();
        if (entity is not null)
        {
            _simulationRenderer.SelectEntity(entity.Id);
            _statusLabel.Text = $"เลือก {entity.Name}";
            UpdateInspector();
            return;
        }

        SettlementState? settlement = _simulation.State.Settlements.Values
            .Where(s => DistanceSquared(s.X, s.Y, tile.X, tile.Y) <= 20)
            .OrderBy(s => DistanceSquared(s.X, s.Y, tile.X, tile.Y))
            .FirstOrDefault();
        if (settlement is not null)
        {
            _simulationRenderer.SelectSettlement(settlement.Id);
            _statusLabel.Text = $"เลือก {settlement.Name}";
            UpdateInspector();
            return;
        }

        _simulationRenderer.ClearSelection();
        _statusLabel.Text = $"ไม่มีสิ่งที่ตรวจสอบใกล้ ({tile.X}, {tile.Y})";
        UpdateInspector();
    }

    private void ApplyDiplomacy(int value)
    {
        if (_simulation is null || _kingdomASelector.ItemCount == 0 || _kingdomBSelector.ItemCount == 0)
            return;
        ulong first = (ulong)_kingdomASelector.GetSelectedId();
        ulong second = (ulong)_kingdomBSelector.GetSelectedId();
        if (first == second)
        {
            _statusLabel.Text = "กรุณาเลือกคนละอาณาจักร";
            return;
        }
        _simulation.SetRelation(first, second, value);
        RelationState state = _simulation.GetRelationState(first, second);
        _statusLabel.Text = $"ปรับความสัมพันธ์เป็น {state}";
        _simulationRenderer.Refresh();
        RefreshUiNow();
    }

    private void PopulateKingdomSelectors()
    {
        _kingdomASelector.Clear();
        _kingdomBSelector.Clear();
        if (_simulation is null)
            return;
        foreach (KingdomState kingdom in _simulation.State.Kingdoms.Values.OrderBy(k => k.Id))
        {
            int id = checked((int)kingdom.Id);
            _kingdomASelector.AddItem(kingdom.Name, id);
            _kingdomBSelector.AddItem(kingdom.Name, id);
        }
        if (_kingdomASelector.ItemCount > 0)
            _kingdomASelector.Select(0);
        if (_kingdomBSelector.ItemCount > 1)
            _kingdomBSelector.Select(1);
    }

    private void AdvanceThirtyDays()
    {
        if (_simulation is null)
            return;
        _simulation.AdvanceDays(30);
        _simulationRenderer.Refresh();
        _statusLabel.Text = "จำลองเพิ่ม 30 วันแล้ว";
        RefreshUiNow();
    }

    private void SaveWorld()
    {
        if (_world is null || _simulation is null)
            return;
        try
        {
            _saveService.Save(SavePath, _world, _clock);
            WriteAtomic(SimulationSavePath, _simulation.SaveToJson());
            _statusLabel.Text = $"บันทึกโลกและสิ่งมีชีวิตแล้ว: {SavePath}";
        }
        catch (Exception exception)
        {
            ReportError("บันทึกโลกไม่สำเร็จ", exception);
        }
    }

    private void LoadWorld()
    {
        try
        {
            LoadedWorld loaded = _saveService.LoadWithRecovery(SavePath);
            _world = loaded.World;
            _clock = loaded.Clock;
            _terrainEditor.ClearHistory();
            _renderer.Bind(_world);
            _checksum = loaded.Checksum;
            _seedInput.Text = _world.Config.Seed.ToString();

            if (File.Exists(SimulationSavePath))
                _simulation = GrandSimulation.LoadFromJson(_world, File.ReadAllText(SimulationSavePath));
            else
            {
                _simulation = new GrandSimulation(_world, _world.Config.Seed ^ 0x5A17_2026L);
                SeedLivingWorld(_world.Config.Seed);
            }

            _simulationRenderer.Bind(_simulation);
            PopulateKingdomSelectors();
            _statusLabel.Text = $"โหลดโลกแล้ว — {_simulation.State.Entities.Count:N0} สิ่งมีชีวิต";
            UpdatePauseText();
            UpdateBrushPreview();
            RefreshUiNow();
        }
        catch (Exception exception)
        {
            ReportError("โหลดโลกไม่สำเร็จ", exception);
        }
    }

    private static void WriteAtomic(string path, string content)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        string temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }

    private void TogglePause()
    {
        _clock.TogglePaused();
        UpdatePauseText();
        _statusLabel.Text = _clock.IsPaused ? "Simulation หยุดแล้ว" : "Simulation ทำงานต่อแล้ว";
    }

    private void UpdatePauseText() => _pauseButton.Text = _clock.IsPaused ? "เล่นต่อ" : "หยุดเวลา";

    private void SetSpeed(double speed)
    {
        try
        {
            _clock.SetTimeScale(speed);
            _statusLabel.Text = $"ความเร็ว Simulation x{speed:0}";
        }
        catch (Exception exception)
        {
            ReportError("เปลี่ยนความเร็วไม่สำเร็จ", exception);
        }
    }

    private void ZoomCamera(float factor)
    {
        float next = Math.Clamp(_camera.Zoom.X * factor, 0.18f, 4f);
        _camera.Zoom = new Vector2(next, next);
    }

    private Vector2I MouseTile()
    {
        Vector2 worldPosition = GetGlobalMousePosition();
        return new Vector2I(
            Mathf.FloorToInt(worldPosition.X / _renderer.TilePixelSize),
            Mathf.FloorToInt(worldPosition.Y / _renderer.TilePixelSize));
    }

    private void UpdateBrushPreview()
    {
        if (_world is null || SelectedTool != InteractionTool.PaintTerrain)
        {
            _brushOverlay.Visible = false;
            return;
        }
        Vector2I tile = MouseTile();
        if (!_world.IsInside(tile.X, tile.Y))
        {
            _brushOverlay.Visible = false;
            return;
        }
        _brushOverlay.SetBrush(tile, BrushRadius, SelectedTerrain, _renderer.TilePixelSize);
    }

    private void UpdateToolHelp()
    {
        if (_toolHelpLabel is null)
            return;
        _toolHelpLabel.Text = SelectedTool switch
        {
            InteractionTool.Inspect => "คลิกซ้ายหรือคลิกขวาใกล้ตัวละคร/เมือง เพื่อดูสุขภาพ อาหาร การกระทำ อาณาจักร และโรค",
            InteractionTool.PaintTerrain => "คลิกหรือลากซ้ายเพื่อวาด Terrain ตามชนิดและขนาดแปรง",
            InteractionTool.CreateCivilization => "คลิกบนทุ่งหญ้า ป่า หรือชายหาด เพื่อสร้างประชากร 8 คน เมืองหลวง และอาณาจักรใหม่",
            InteractionTool.PowerPlague => "คลิกบริเวณที่มีสิ่งมีชีวิตเพื่อปล่อยโรค ซึ่งแพร่และลดสุขภาพตามวันจำลอง",
            InteractionTool.PowerLightning or InteractionTool.PowerMeteor => "คลิกเพื่อสร้างความเสียหายจริง สิ่งมีชีวิตที่สุขภาพหมดจะตายและถูกบันทึกใน Chronicle",
            InteractionTool.PowerBlessing or InteractionTool.PowerCurse => "คลิกเพื่อเพิ่ม Trait และเปลี่ยนสุขภาพของสิ่งมีชีวิตในรัศมี",
            InteractionTool.PowerCreateForest => "คลิกบนทุ่งหญ้าเพื่อเปลี่ยนเป็นป่าจริงตามรัศมี",
            _ => "คลิกบนแผนที่เพื่อวางสิ่งมีชีวิตชนิดที่เลือก • เมาส์กลางเลื่อนกล้อง • ล้อเมาส์ซูม",
        };
    }

    private void RefreshUiNow()
    {
        UpdateDebugOverlay();
        UpdateInspector();
        UpdateChronicle();
    }

    private void UpdateDebugOverlay()
    {
        if (_world is null || _simulation is null)
        {
            _debugLabel.Text = "No world loaded";
            return;
        }
        Vector2I tile = MouseTile();
        string tileText = _world.IsInside(tile.X, tile.Y)
            ? $"Tile: {tile.X}, {tile.Y}  {_world.GetTerrain(tile.X, tile.Y)}"
            : "Tile: outside world";
        int infected = _simulation.State.Diseases.Sum(d => d.InfectedDays.Count);
        _debugLabel.Text =
            $"FPS: {Engine.GetFramesPerSecond()} | TPS: {_measuredTps}\n" +
            $"วัน: {_simulation.State.Day}  เดือน: {_simulation.State.Month}  ปี: {_simulation.State.Year}  ยุค: {_simulation.State.Age}\n" +
            $"สิ่งมีชีวิต: {_simulation.State.Entities.Count:N0}  เมือง: {_simulation.State.Settlements.Count}  อาณาจักร: {_simulation.State.Kingdoms.Count}\n" +
            $"โรคที่กำลังระบาด: {_simulation.State.Diseases.Count}  ผู้ติดเชื้อ: {infected}\n" +
            $"Speed: x{_clock.TimeScale:0}  Paused: {_clock.IsPaused}\n" +
            $"World: {_world.Width}×{_world.Height}  Chunks: {_renderer.RenderedChunkCount}\n" +
            $"Checksum: {(_checksum.Length >= 12 ? _checksum[..12] : _checksum)}\n" + tileText;
    }

    private void UpdateInspector()
    {
        if (_simulation is null)
            return;

        if (_simulationRenderer.SelectedEntityId is ulong entityId && _simulation.State.Entities.TryGetValue(entityId, out SimEntity? entity))
        {
            string disease = _simulation.State.Diseases.FirstOrDefault(d => d.InfectedDays.ContainsKey(entity.Id))?.Id ?? "ไม่มี";
            string settlement = entity.SettlementId is ulong sid && _simulation.State.Settlements.TryGetValue(sid, out SettlementState? s) ? s.Name : "ไม่มี";
            string kingdom = entity.KingdomId is ulong kid && _simulation.State.Kingdoms.TryGetValue(kid, out KingdomState? k) ? k.Name : "ไม่มี";
            _inspectorLabel.Text =
                $"{entity.Name} (#{entity.Id})\nชนิด: {entity.Species} | Action: {entity.Action}\n" +
                $"HP: {entity.Health:0}  Hunger: {entity.Hunger:0}  Energy: {entity.Energy:0}\n" +
                $"อายุ: {entity.AgeDays / 360f:0.0} ปี  Intelligence: {entity.Intelligence:0}\n" +
                $"เมือง: {settlement}\nอาณาจักร: {kingdom}\nโรค: {disease}\n" +
                $"Traits: {(entity.Traits.Count == 0 ? "ไม่มี" : string.Join(", ", entity.Traits))}";
            return;
        }

        if (_simulationRenderer.SelectedSettlementId is ulong settlementId && _simulation.State.Settlements.TryGetValue(settlementId, out SettlementState? settlementState))
        {
            int population = _simulation.State.Entities.Values.Count(e => e.SettlementId == settlementId);
            string kingdom = settlementState.KingdomId is ulong kid && _simulation.State.Kingdoms.TryGetValue(kid, out KingdomState? k) ? k.Name : "อิสระ";
            _inspectorLabel.Text =
                $"{settlementState.Name} (#{settlementState.Id})\nระดับ: {settlementState.Stage} | อาณาจักร: {kingdom}\n" +
                $"ประชากร: {population}/{settlementState.Housing}  Happiness: {settlementState.Happiness:0}\n" +
                $"Food: {settlementState.Food:0}  Wood: {settlementState.Wood:0}  Stone: {settlementState.Stone:0}  Gold: {settlementState.Gold:0}\n" +
                $"สิ่งปลูกสร้าง: {string.Join(", ", settlementState.Buildings)}";
            return;
        }

        if (_simulationRenderer.SelectedKingdomId is ulong kingdomId && _simulation.State.Kingdoms.TryGetValue(kingdomId, out KingdomState? kingdomState))
        {
            int population = _simulation.State.Entities.Values.Count(e => e.KingdomId == kingdomId);
            string relations = string.Join("\n", kingdomState.Relations.OrderBy(p => p.Key).Select(p =>
            {
                string name = _simulation.State.Kingdoms.GetValueOrDefault(p.Key)?.Name ?? p.Key.ToString();
                return $"• {name}: {p.Value} ({_simulation.GetRelationState(kingdomId, p.Key)})";
            }));
            _inspectorLabel.Text =
                $"{kingdomState.Name} (#{kingdomState.Id})\nรัฐบาล: {kingdomState.Government}\n" +
                $"ประชากร: {population}  เมือง: {kingdomState.Settlements.Count}\n" +
                $"เศรษฐกิจ: {kingdomState.Economy:0.0}  กองทัพ: {kingdomState.ArmyStrength:0.0}  เสถียรภาพ: {kingdomState.Stability:0}\n" +
                $"เทคโนโลยี: {(kingdomState.Technologies.Count == 0 ? "ยังไม่มี" : string.Join(", ", kingdomState.Technologies))}\n" +
                $"การทูต:\n{relations}";
            return;
        }

        _inspectorLabel.Text = "เลือกเครื่องมือ ‘ตรวจสอบ’ แล้วคลิกสิ่งมีชีวิตหรือเมือง\n\nจุดสีเขียว = สัตว์กินพืช\nแดง = นักล่า\nเหลือง/สีอาณาจักร = ประชากร\nม่วง = สัตว์ประหลาด\nฟ้า = ปลา\nสี่เหลี่ยม = เมือง\nวงสีส้ม = ติดโรค";
    }

    private void UpdateChronicle()
    {
        if (_simulation is null)
            return;
        ChronicleEvent[] latest = _simulation.State.Chronicle.TakeLast(10).Reverse().ToArray();
        _chronicleLabel.Text = latest.Length == 0
            ? "ยังไม่มีเหตุการณ์"
            : string.Join("\n\n", latest.Select(e => $"[วัน {e.Tick}] {e.Title}\n{e.Description}"));
    }

    private static int DistanceSquared(int x1, int y1, int x2, int y2)
    {
        int dx = x1 - x2;
        int dy = y1 - y2;
        return dx * dx + dy * dy;
    }

    private void ReportError(string message, Exception exception)
    {
        GD.PushError($"{message}: {exception}");
        _statusLabel.Text = $"{message}: {exception.Message}";
    }
}
