using Godot;
using WorldForge.Core.Editing;
using WorldForge.Core.Persistence;
using WorldForge.Core.Simulation;
using WorldForge.Core.World;
using WorldForge.Presentation;

namespace WorldForge;

public sealed partial class PerformanceMainController : Node2D
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

    private enum InitialRelation
    {
        Peaceful,
        Neutral,
        Hostile,
        War,
    }

    private readonly TerrainEditor _terrainEditor = new();
    private readonly WorldSaveService _saveService = new();
    private WorldMap? _world;
    private GrandSimulation? _simulation;
    private SimulationClock _clock = new();
    private SimulationBudgetOptions _budget = SimulationBudgetOptions.ForProfile(SimulationPerformanceProfile.Balanced, 1200);
    private WorldChunkRenderer _worldRenderer = null!;
    private SimulationRenderer _simulationRenderer = null!;
    private BrushOverlay _brushOverlay = null!;
    private Camera2D _camera = null!;
    private CanvasLayer _setupLayer = null!;
    private Label _setupError = null!;
    private LineEdit _setupSeed = null!;
    private OptionButton _setupWorldSize = null!;
    private HSlider _setupSeaLevel = null!;
    private Label _setupSeaLevelValue = null!;
    private SpinBox _setupKingdoms = null!;
    private SpinBox _setupPopulationPerKingdom = null!;
    private SpinBox _setupGrazers = null!;
    private SpinBox _setupPredators = null!;
    private SpinBox _setupMonsters = null!;
    private SpinBox _setupFish = null!;
    private SpinBox _setupPopulationCap = null!;
    private OptionButton _setupProfile = null!;
    private OptionButton _setupRelation = null!;
    private CheckButton _setupReproduction = null!;
    private CheckButton _setupAutomaticWar = null!;
    private SpinBox _setupAiBudget = null!;
    private SpinBox _setupPathBudget = null!;
    private SpinBox _setupRenderHz = null!;
    private SpinBox _setupMaxDaysPerFrame = null!;

    private LineEdit _seedInput = null!;
    private OptionButton _toolSelector = null!;
    private OptionButton _terrainSelector = null!;
    private OptionButton _kingdomASelector = null!;
    private OptionButton _kingdomBSelector = null!;
    private OptionButton _runtimeProfile = null!;
    private HSlider _brushSlider = null!;
    private Label _brushValue = null!;
    private Label _toolHelp = null!;
    private Label _status = null!;
    private Label _debug = null!;
    private Label _inspector = null!;
    private Label _chronicle = null!;
    private Button _pauseButton = null!;
    private SpinBox _runtimePopulationCap = null!;
    private SpinBox _runtimeAiBudget = null!;
    private SpinBox _runtimePathBudget = null!;
    private SpinBox _runtimeRenderHz = null!;

    private bool _isPanning;
    private bool _renderDirty;
    private int _pendingSimulationTicks;
    private int _queuedManualDays;
    private int _maxDaysPerFrame = 2;
    private double _renderHz = 7;
    private double _renderAccumulator;
    private double _metricsAccumulator;
    private int _ticksThisSecond;
    private int _measuredTps;
    private string _checksum = string.Empty;

    private InteractionTool SelectedTool => (InteractionTool)_toolSelector.GetSelectedId();
    private TerrainType SelectedTerrain => (TerrainType)_terrainSelector.GetSelectedId();
    private int BrushRadius => Math.Max(0, (int)_brushSlider.Value - 1);
    private string SavePath => ProjectSettings.GlobalizePath("user://saves/slot_1.wfg.json");
    private string SimulationSavePath => ProjectSettings.GlobalizePath("user://saves/slot_1.sim.json");

    public override void _Ready()
    {
        DisplayServer.WindowSetTitle("WorldForge: Pixel Gods — Performance Build");
        _worldRenderer = new WorldChunkRenderer { Name = "WorldRenderer", ZIndex = 0 };
        AddChild(_worldRenderer);
        _simulationRenderer = new SimulationRenderer { Name = "SimulationRenderer", ZIndex = 25 };
        AddChild(_simulationRenderer);
        _brushOverlay = new BrushOverlay { Name = "BrushOverlay", ZIndex = 50, Visible = false };
        AddChild(_brushOverlay);
        _camera = new Camera2D { Name = "WorldCamera", Enabled = true, Position = new Vector2(512, 512) };
        AddChild(_camera);
        BuildGameInterface();
        BuildSetupInterface();
        ShowSetup();
    }

    public override void _Process(double delta)
    {
        if (_simulation is null)
            return;

        int steps = _clock.Advance(delta);
        _pendingSimulationTicks += steps;
        _ticksThisSecond += steps;
        _metricsAccumulator += delta;
        _renderAccumulator += delta;

        int daysThisFrame = 0;
        while ((_pendingSimulationTicks >= 10 || _queuedManualDays > 0) && daysThisFrame < _maxDaysPerFrame)
        {
            if (_queuedManualDays > 0)
                _queuedManualDays--;
            else
                _pendingSimulationTicks -= 10;
            _simulation.AdvanceDayBudgeted(_budget);
            daysThisFrame++;
            _renderDirty = true;
        }
        _pendingSimulationTicks = Math.Min(_pendingSimulationTicks, 30);

        if (_renderDirty && _renderAccumulator >= 1.0 / Math.Max(1, _renderHz))
        {
            _renderAccumulator = 0;
            _renderDirty = false;
            _simulationRenderer.Refresh();
            RefreshUi();
        }

        if (_metricsAccumulator >= 1.0)
        {
            _measuredTps = _ticksThisSecond;
            _ticksThisSecond = 0;
            _metricsAccumulator -= 1.0;
            UpdateDebug();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_world is null || _setupLayer.Visible)
            return;
        if (@event is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp && button.Pressed)
            {
                ZoomCamera(1.18f);
                GetViewport().SetInputAsHandled();
                return;
            }
            if (button.ButtonIndex == MouseButton.WheelDown && button.Pressed)
            {
                ZoomCamera(1f / 1.18f);
                GetViewport().SetInputAsHandled();
                return;
            }
            if (button.ButtonIndex == MouseButton.Middle)
            {
                _isPanning = button.Pressed;
                GetViewport().SetInputAsHandled();
                return;
            }
            if (button.ButtonIndex == MouseButton.Right && button.Pressed)
            {
                InspectAt(MouseTile());
                GetViewport().SetInputAsHandled();
                return;
            }
            if (button.ButtonIndex == MouseButton.Left && button.Pressed)
            {
                UseSelectedTool(MouseTile());
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
                    PaintAt(MouseTile());
            }
        }
    }

    private void BuildGameInterface()
    {
        var canvas = new CanvasLayer { Name = "GameInterface", Layer = 10 };
        AddChild(canvas);

        var topPanel = new PanelContainer { AnchorRight = 1, OffsetBottom = 56 };
        canvas.AddChild(topPanel);
        var top = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        top.AddThemeConstantOverride("separation", 7);
        topPanel.AddChild(top);
        top.AddChild(new Label { Text = "WorldForge | Seed" });
        _seedInput = new LineEdit { Text = "1502026", CustomMinimumSize = new Vector2(115, 0), Editable = false };
        top.AddChild(_seedInput);
        top.AddChild(CreateButton("ตั้งค่าโลกใหม่", ShowSetup));
        top.AddChild(CreateButton("บันทึก", SaveWorld));
        top.AddChild(CreateButton("โหลด", LoadWorld));
        _pauseButton = CreateButton("หยุดเวลา", TogglePause);
        top.AddChild(_pauseButton);
        top.AddChild(CreateButton("x1", () => SetSpeed(1)));
        top.AddChild(CreateButton("x2", () => SetSpeed(2)));
        top.AddChild(CreateButton("x4", () => SetSpeed(4)));
        top.AddChild(CreateButton("x8", () => SetSpeed(8)));
        top.AddChild(CreateButton("MAX", () => SetSpeed(32)));

        var leftPanel = new PanelContainer
        {
            OffsetLeft = 8,
            OffsetTop = 64,
            OffsetRight = 314,
            AnchorBottom = 1,
            OffsetBottom = -42,
        };
        canvas.AddChild(leftPanel);
        var leftScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        leftPanel.AddChild(leftScroll);
        var tools = new VBoxContainer { CustomMinimumSize = new Vector2(286, 0) };
        tools.AddThemeConstantOverride("separation", 6);
        leftScroll.AddChild(tools);
        tools.AddChild(Section("เครื่องมือโลก"));
        _toolSelector = new OptionButton();
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
        _toolSelector.ItemSelected += _ => { UpdateToolHelp(); UpdateBrushPreview(); };
        tools.AddChild(_toolSelector);

        _terrainSelector = new OptionButton();
        AddTerrainOption("มหาสมุทรลึก", TerrainType.DeepOcean);
        AddTerrainOption("น้ำตื้น", TerrainType.ShallowWater);
        AddTerrainOption("ชายหาด", TerrainType.Beach);
        AddTerrainOption("ทุ่งหญ้า", TerrainType.Grassland);
        AddTerrainOption("ป่า", TerrainType.Forest);
        AddTerrainOption("ภูเขา", TerrainType.Mountain);
        _terrainSelector.Select((int)TerrainType.Grassland);
        tools.AddChild(_terrainSelector);
        var brushRow = new HBoxContainer();
        _brushSlider = new HSlider { MinValue = 1, MaxValue = 12, Step = 1, Value = 3, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _brushSlider.ValueChanged += value => { _brushValue.Text = ((int)value).ToString(); UpdateBrushPreview(); };
        brushRow.AddChild(_brushSlider);
        _brushValue = new Label { Text = "3" };
        brushRow.AddChild(_brushValue);
        tools.AddChild(brushRow);
        tools.AddChild(CreateButton("ย้อนกลับ Terrain", UndoTerrain));
        tools.AddChild(CreateButton("จำลองเพิ่ม 30 วันแบบแบ่งรอบ", () => _queuedManualDays += 30));
        _toolHelp = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(280, 96) };
        tools.AddChild(_toolHelp);
        UpdateToolHelp();

        tools.AddChild(new HSeparator());
        tools.AddChild(Section("ประสิทธิภาพระหว่างเล่น"));
        _runtimeProfile = new OptionButton();
        _runtimeProfile.AddItem("ประหยัด / ลื่นสุด", (int)SimulationPerformanceProfile.Economy);
        _runtimeProfile.AddItem("สมดุล", (int)SimulationPerformanceProfile.Balanced);
        _runtimeProfile.AddItem("ละเอียด", (int)SimulationPerformanceProfile.Detailed);
        _runtimeProfile.Select(1);
        _runtimeProfile.ItemSelected += _ => ApplyRuntimeProfile((SimulationPerformanceProfile)_runtimeProfile.GetSelectedId());
        tools.AddChild(LabeledControl("โปรไฟล์", _runtimeProfile));
        _runtimePopulationCap = CreateSpin(25, 6000, 25, 1200);
        _runtimePopulationCap.ValueChanged += value => _budget.MaxPopulation = (int)value;
        tools.AddChild(LabeledControl("เพดานประชากร", _runtimePopulationCap));
        _runtimeAiBudget = CreateSpin(10, 2000, 10, 120);
        _runtimeAiBudget.ValueChanged += value => { _budget.EntityAiUpdatesPerDay = (int)value; _budget.Profile = SimulationPerformanceProfile.Custom; };
        tools.AddChild(LabeledControl("AI ที่อัปเดต/วัน", _runtimeAiBudget));
        _runtimePathBudget = CreateSpin(0, 200, 1, 12);
        _runtimePathBudget.ValueChanged += value => { _budget.PathRequestsPerDay = (int)value; _budget.Profile = SimulationPerformanceProfile.Custom; };
        tools.AddChild(LabeledControl("A* ที่คำนวณ/วัน", _runtimePathBudget));
        _runtimeRenderHz = CreateSpin(1, 20, 1, 7);
        _runtimeRenderHz.ValueChanged += value => _renderHz = value;
        tools.AddChild(LabeledControl("รีเฟรชภาพ/วินาที", _runtimeRenderHz));

        tools.AddChild(new HSeparator());
        tools.AddChild(Section("การทูต"));
        _kingdomASelector = new OptionButton();
        _kingdomBSelector = new OptionButton();
        tools.AddChild(_kingdomASelector);
        tools.AddChild(_kingdomBSelector);
        var relationRow = new HBoxContainer();
        relationRow.AddChild(CreateButton("สงคราม", () => ApplyDiplomacy(-90)));
        relationRow.AddChild(CreateButton("กลาง", () => ApplyDiplomacy(0)));
        relationRow.AddChild(CreateButton("พันธมิตร", () => ApplyDiplomacy(90)));
        tools.AddChild(relationRow);

        var rightPanel = new PanelContainer
        {
            AnchorLeft = 1,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = -390,
            OffsetTop = 64,
            OffsetRight = -8,
            OffsetBottom = -42,
        };
        canvas.AddChild(rightPanel);
        var right = new VBoxContainer();
        right.AddThemeConstantOverride("separation", 6);
        rightPanel.AddChild(right);
        right.AddChild(Section("สถานะและ Performance"));
        _debug = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        right.AddChild(_debug);
        right.AddChild(new HSeparator());
        right.AddChild(Section("Inspector"));
        _inspector = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(360, 180) };
        right.AddChild(_inspector);
        right.AddChild(new HSeparator());
        right.AddChild(Section("Chronicle"));
        var chronicleScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(360, 180) };
        right.AddChild(chronicleScroll);
        _chronicle = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(345, 0) };
        chronicleScroll.AddChild(_chronicle);

        _status = new Label
        {
            AnchorTop = 1,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 8,
            OffsetTop = -34,
            OffsetRight = -8,
            OffsetBottom = -6,
            Text = "กำลังรอตั้งค่าโลก",
        };
        canvas.AddChild(_status);
    }

    private void BuildSetupInterface()
    {
        _setupLayer = new CanvasLayer { Name = "WorldSetup", Layer = 100 };
        AddChild(_setupLayer);
        var shade = new ColorRect
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            Color = new Color(0.025f, 0.035f, 0.06f, 0.96f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _setupLayer.AddChild(shade);
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        _setupLayer.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(780, 690) };
        center.AddChild(panel);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        panel.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);
        var title = new Label { Text = "ตั้งค่าโลกก่อนเริ่ม", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 26);
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "เริ่มน้อยจะลื่นกว่า คุณสามารถเพิ่มประชากรและงบ AI ระหว่างเล่นได้",
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 24);
        root.AddChild(columns);
        var basic = new VBoxContainer { CustomMinimumSize = new Vector2(350, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var advanced = new VBoxContainer { CustomMinimumSize = new Vector2(350, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        columns.AddChild(basic);
        columns.AddChild(advanced);

        basic.AddChild(Section("โลกและอารยธรรม"));
        _setupSeed = new LineEdit { Text = "1502026" };
        basic.AddChild(LabeledControl("Seed", _setupSeed));
        _setupWorldSize = new OptionButton();
        _setupWorldSize.AddItem("128 × 128 — เบามาก", 128);
        _setupWorldSize.AddItem("256 × 256 — แนะนำ", 256);
        _setupWorldSize.AddItem("384 × 384 — ใหญ่", 384);
        _setupWorldSize.AddItem("512 × 512 — หนัก", 512);
        _setupWorldSize.Select(1);
        basic.AddChild(LabeledControl("ขนาดโลก", _setupWorldSize));
        _setupSeaLevel = new HSlider { MinValue = 0.34, MaxValue = 0.66, Step = 0.01, Value = 0.48, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _setupSeaLevelValue = new Label { Text = "0.48" };
        _setupSeaLevel.ValueChanged += value => _setupSeaLevelValue.Text = value.ToString("0.00");
        var seaRow = new HBoxContainer();
        seaRow.AddChild(_setupSeaLevel);
        seaRow.AddChild(_setupSeaLevelValue);
        basic.AddChild(LabeledControl("ระดับน้ำทะเล", seaRow));
        _setupKingdoms = CreateSpin(0, 8, 1, 2);
        basic.AddChild(LabeledControl("จำนวนอาณาจักร", _setupKingdoms));
        _setupPopulationPerKingdom = CreateSpin(5, 120, 1, 12);
        basic.AddChild(LabeledControl("ประชากร/อาณาจักร", _setupPopulationPerKingdom));
        _setupRelation = new OptionButton();
        _setupRelation.AddItem("สันติ", (int)InitialRelation.Peaceful);
        _setupRelation.AddItem("เป็นกลาง", (int)InitialRelation.Neutral);
        _setupRelation.AddItem("เป็นศัตรู", (int)InitialRelation.Hostile);
        _setupRelation.AddItem("เริ่มสงครามทันที", (int)InitialRelation.War);
        _setupRelation.Select(1);
        basic.AddChild(LabeledControl("ความสัมพันธ์เริ่มต้น", _setupRelation));
        basic.AddChild(Section("สัตว์เริ่มต้น"));
        _setupGrazers = CreateSpin(0, 1500, 5, 45);
        _setupPredators = CreateSpin(0, 500, 1, 10);
        _setupMonsters = CreateSpin(0, 150, 1, 2);
        _setupFish = CreateSpin(0, 1500, 5, 25);
        basic.AddChild(LabeledControl("สัตว์กินพืช", _setupGrazers));
        basic.AddChild(LabeledControl("นักล่า", _setupPredators));
        basic.AddChild(LabeledControl("มอนสเตอร์", _setupMonsters));
        basic.AddChild(LabeledControl("ปลา", _setupFish));

        advanced.AddChild(Section("ประสิทธิภาพและระบบจำลอง"));
        _setupProfile = new OptionButton();
        _setupProfile.AddItem("ประหยัด / ลื่นสุด", (int)SimulationPerformanceProfile.Economy);
        _setupProfile.AddItem("สมดุล — แนะนำ", (int)SimulationPerformanceProfile.Balanced);
        _setupProfile.AddItem("ละเอียด — ใช้เครื่องแรง", (int)SimulationPerformanceProfile.Detailed);
        _setupProfile.AddItem("กำหนดเอง", (int)SimulationPerformanceProfile.Custom);
        _setupProfile.Select(1);
        _setupProfile.ItemSelected += _ => ApplySetupProfilePreset();
        advanced.AddChild(LabeledControl("โปรไฟล์", _setupProfile));
        _setupPopulationCap = CreateSpin(25, 6000, 25, 1200);
        advanced.AddChild(LabeledControl("เพดานประชากรรวม", _setupPopulationCap));
        _setupAiBudget = CreateSpin(10, 2000, 10, 120);
        advanced.AddChild(LabeledControl("AI ที่อัปเดต/วัน", _setupAiBudget));
        _setupPathBudget = CreateSpin(0, 200, 1, 12);
        advanced.AddChild(LabeledControl("A* ที่คำนวณ/วัน", _setupPathBudget));
        _setupRenderHz = CreateSpin(1, 20, 1, 7);
        advanced.AddChild(LabeledControl("รีเฟรชภาพ/วินาที", _setupRenderHz));
        _setupMaxDaysPerFrame = CreateSpin(1, 8, 1, 2);
        advanced.AddChild(LabeledControl("วันจำลองสูงสุด/เฟรม", _setupMaxDaysPerFrame));
        _setupReproduction = new CheckButton { Text = "เปิดการสืบพันธุ์", ButtonPressed = true };
        _setupAutomaticWar = new CheckButton { Text = "เปิดการทูตและสงครามอัตโนมัติ", ButtonPressed = true };
        advanced.AddChild(_setupReproduction);
        advanced.AddChild(_setupAutomaticWar);
        advanced.AddChild(new Label
        {
            Text = "แนะนำสำหรับเครื่องทั่วไป:\n• โลก 256×256\n• ประชากรเริ่มต้นไม่เกิน 120\n• AI 80–150/วัน\n• A* 5–15/วัน\n• Render 5–8 Hz",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        _setupError = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _setupError.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.35f));
        root.AddChild(_setupError);
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        actions.AddThemeConstantOverride("separation", 12);
        root.AddChild(actions);
        actions.AddChild(CreateButton("เริ่มสร้างโลก", StartConfiguredWorld));
        var cancel = CreateButton("กลับไปเล่นโลกเดิม", HideSetup);
        actions.AddChild(cancel);
    }

    private void ShowSetup()
    {
        _setupLayer.Visible = true;
        _clock.SetPaused(true);
        UpdatePauseText();
        _setupError.Text = string.Empty;
    }

    private void HideSetup()
    {
        if (_world is null)
        {
            _setupError.Text = "กรุณาสร้างโลกก่อน";
            return;
        }
        _setupLayer.Visible = false;
    }

    private void ApplySetupProfilePreset()
    {
        SimulationPerformanceProfile profile = (SimulationPerformanceProfile)_setupProfile.GetSelectedId();
        if (profile == SimulationPerformanceProfile.Custom)
            return;
        SimulationBudgetOptions preset = SimulationBudgetOptions.ForProfile(profile, (int)_setupPopulationCap.Value);
        _setupAiBudget.Value = preset.EntityAiUpdatesPerDay;
        _setupPathBudget.Value = preset.PathRequestsPerDay;
        _setupRenderHz.Value = profile switch
        {
            SimulationPerformanceProfile.Economy => 4,
            SimulationPerformanceProfile.Detailed => 12,
            _ => 7,
        };
        _setupMaxDaysPerFrame.Value = profile switch
        {
            SimulationPerformanceProfile.Economy => 1,
            SimulationPerformanceProfile.Detailed => 3,
            _ => 2,
        };
    }

    private void StartConfiguredWorld()
    {
        try
        {
            int kingdoms = (int)_setupKingdoms.Value;
            int peoplePerKingdom = (int)_setupPopulationPerKingdom.Value;
            int startingPopulation = kingdoms * peoplePerKingdom +
                                     (int)_setupGrazers.Value +
                                     (int)_setupPredators.Value +
                                     (int)_setupMonsters.Value +
                                     (int)_setupFish.Value;
            int cap = (int)_setupPopulationCap.Value;
            if (startingPopulation > cap)
            {
                _setupError.Text = $"จำนวนเริ่มต้น {startingPopulation:N0} มากกว่าเพดาน {cap:N0}";
                return;
            }

            SimulationPerformanceProfile profile = (SimulationPerformanceProfile)_setupProfile.GetSelectedId();
            _budget = SimulationBudgetOptions.ForProfile(
                profile == SimulationPerformanceProfile.Custom ? SimulationPerformanceProfile.Balanced : profile,
                cap);
            _budget.Profile = profile;
            _budget.EntityAiUpdatesPerDay = (int)_setupAiBudget.Value;
            _budget.PathRequestsPerDay = (int)_setupPathBudget.Value;
            _budget.EnableReproduction = _setupReproduction.ButtonPressed;
            _budget.EnableAutomaticDiplomacy = _setupAutomaticWar.ButtonPressed;
            _budget.EnableArmies = _setupAutomaticWar.ButtonPressed;
            _renderHz = _setupRenderHz.Value;
            _maxDaysPerFrame = (int)_setupMaxDaysPerFrame.Value;

            long seed = SeedUtility.ParseOrHash(_setupSeed.Text);
            int size = checked((int)_setupWorldSize.GetSelectedId());
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
            _worldRenderer.Bind(_world);
            SeedConfiguredWorld(
                seed,
                kingdoms,
                peoplePerKingdom,
                (int)_setupGrazers.Value,
                (int)_setupPredators.Value,
                (int)_setupMonsters.Value,
                (int)_setupFish.Value,
                (InitialRelation)_setupRelation.GetSelectedId());
            _simulationRenderer.Bind(_simulation);
            _checksum = WorldChecksum.Compute(_world);
            float pixels = _world.Width * _worldRenderer.TilePixelSize;
            _camera.Position = new Vector2(pixels / 2f, pixels / 2f);
            _camera.Zoom = new Vector2(size >= 384 ? 0.46f : 0.72f, size >= 384 ? 0.46f : 0.72f);
            _seedInput.Text = seed.ToString();
            SyncRuntimeControls();
            PopulateKingdomSelectors();
            _pendingSimulationTicks = 0;
            _queuedManualDays = 0;
            _renderDirty = true;
            _setupLayer.Visible = false;
            _clock.SetPaused(false);
            UpdatePauseText();
            _status.Text = $"สร้างโลกแล้ว: {startingPopulation:N0} สิ่งมีชีวิต | {kingdoms} อาณาจักร | โปรไฟล์ {profile}";
            RefreshUi();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            _setupError.Text = exception.Message;
        }
    }

    private void SeedConfiguredWorld(long seed, int kingdomCount, int peoplePerKingdom, int grazers, int predators, int monsters, int fish, InitialRelation initialRelation)
    {
        if (_world is null || _simulation is null)
            return;
        List<Vector2I> land = CollectTiles(t => t is TerrainType.Grassland or TerrainType.Forest or TerrainType.Beach);
        List<Vector2I> water = CollectTiles(t => t is TerrainType.DeepOcean or TerrainType.ShallowWater);
        if (land.Count < Math.Max(5, kingdomCount))
            throw new InvalidOperationException("พื้นที่บกไม่เพียงพอ กรุณาลดระดับน้ำทะเล");

        var kingdoms = new List<KingdomState>();
        for (int i = 0; i < kingdomCount; i++)
        {
            int targetX = (i + 1) * _world.Width / (kingdomCount + 1);
            int targetY = _world.Height / 2 + ((i % 2 == 0 ? -1 : 1) * _world.Height / 7);
            Vector2I tile = land.OrderBy(p => SquaredDistance(p.X, p.Y, targetX, targetY)).First();
            kingdoms.Add(CreateCivilization(tile, peoplePerKingdom, $"อาณาจักร {i + 1}"));
        }

        int relationValue = initialRelation switch
        {
            InitialRelation.Peaceful => 45,
            InitialRelation.Hostile => -40,
            InitialRelation.War => -90,
            _ => 0,
        };
        for (int i = 0; i < kingdoms.Count; i++)
            for (int j = i + 1; j < kingdoms.Count; j++)
                _simulation.SetRelation(kingdoms[i].Id, kingdoms[j].Id, relationValue);

        var random = new Random(unchecked((int)(seed ^ (seed >> 32))));
        SpawnMany(SpeciesKind.Grazer, land, grazers, random);
        SpawnMany(SpeciesKind.Predator, land, predators, random);
        SpawnMany(SpeciesKind.Monster, land, monsters, random);
        if (water.Count > 0)
            SpawnMany(SpeciesKind.Fish, water, fish, random);
    }

    private KingdomState CreateCivilization(Vector2I tile, int population, string name)
    {
        if (_simulation is null)
            throw new InvalidOperationException("Simulation not ready");
        var settlers = new List<ulong>();
        for (int i = 0; i < Math.Max(5, population); i++)
        {
            SimEntity settler = _simulation.SpawnEntity(SpeciesKind.Settler, tile.X, tile.Y, $"ประชากร {_simulation.State.NextEntityId}");
            settler.AgeDays = (18 + i % 30) * 360;
            settler.Morale = 50 + i % 25;
            settlers.Add(settler.Id);
        }
        SettlementState settlement = _simulation.FoundSettlement(settlers, $"นคร {_simulation.State.NextSettlementId}");
        return _simulation.FoundKingdom(settlement.Id, name, GovernmentType.Council);
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

    private void SpawnMany(SpeciesKind species, IReadOnlyList<Vector2I> candidates, int count, Random random)
    {
        if (_simulation is null || candidates.Count == 0)
            return;
        for (int i = 0; i < count; i++)
        {
            Vector2I tile = candidates[random.Next(candidates.Count)];
            SimEntity entity = _simulation.SpawnEntity(species, tile.X, tile.Y);
            entity.AgeDays = species switch
            {
                SpeciesKind.Fish => random.Next(30, 900),
                SpeciesKind.Grazer => random.Next(120, 1800),
                SpeciesKind.Predator => random.Next(180, 2200),
                SpeciesKind.Monster => random.Next(400, 8000),
                _ => entity.AgeDays,
            };
        }
    }

    private void ApplyRuntimeProfile(SimulationPerformanceProfile profile)
    {
        int cap = _budget.MaxPopulation;
        bool reproduction = _budget.EnableReproduction;
        bool diplomacy = _budget.EnableAutomaticDiplomacy;
        bool armies = _budget.EnableArmies;
        _budget = SimulationBudgetOptions.ForProfile(profile, cap);
        _budget.EnableReproduction = reproduction;
        _budget.EnableAutomaticDiplomacy = diplomacy;
        _budget.EnableArmies = armies;
        _renderHz = profile switch
        {
            SimulationPerformanceProfile.Economy => 4,
            SimulationPerformanceProfile.Detailed => 12,
            _ => 7,
        };
        _maxDaysPerFrame = profile switch
        {
            SimulationPerformanceProfile.Economy => 1,
            SimulationPerformanceProfile.Detailed => 3,
            _ => 2,
        };
        SyncRuntimeControls();
        _status.Text = $"เปลี่ยนโปรไฟล์เป็น {profile}";
    }

    private void SyncRuntimeControls()
    {
        _runtimePopulationCap.Value = _budget.MaxPopulation;
        _runtimeAiBudget.Value = _budget.EntityAiUpdatesPerDay;
        _runtimePathBudget.Value = _budget.PathRequestsPerDay;
        _runtimeRenderHz.Value = _renderHz;
        int profileIndex = _budget.Profile switch
        {
            SimulationPerformanceProfile.Economy => 0,
            SimulationPerformanceProfile.Detailed => 2,
            _ => 1,
        };
        _runtimeProfile.Select(profileIndex);
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
                    if (_simulation.State.Entities.Count + 8 > _budget.MaxPopulation)
                        throw new InvalidOperationException("ถึงเพดานประชากรแล้ว");
                    KingdomState kingdom = CreateCivilization(tile, 8, $"อาณาจักร {_simulation.State.NextKingdomId}");
                    foreach (KingdomState other in _simulation.State.Kingdoms.Values.Where(k => k.Id != kingdom.Id))
                        _simulation.SetRelation(kingdom.Id, other.Id, 0);
                    PopulateKingdomSelectors();
                    _simulationRenderer.SelectKingdom(kingdom.Id);
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
            ReportError("ใช้เครื่องมือไม่สำเร็จ", exception);
        }
    }

    private void SpawnAt(SpeciesKind species, Vector2I tile)
    {
        if (_simulation is null)
            return;
        if (_simulation.State.Entities.Count >= _budget.MaxPopulation)
            throw new InvalidOperationException("ถึงเพดานประชากรแล้ว เพิ่มเพดานในแถบซ้ายก่อน");
        SimEntity entity = _simulation.SpawnEntity(species, tile.X, tile.Y);
        _simulationRenderer.SelectEntity(entity.Id);
        _status.Text = $"วาง {species} ที่ ({tile.X}, {tile.Y})";
    }

    private void ApplyPower(GodPowerType power, Vector2I tile)
    {
        if (_simulation is null || _world is null)
            return;
        _simulation.ApplyPower(power, tile.X, tile.Y, Math.Max(1, BrushRadius));
        if (power == GodPowerType.CreateForest)
        {
            _worldRenderer.Bind(_world);
            _checksum = WorldChecksum.Compute(_world);
        }
        _status.Text = $"ใช้พลัง {power}";
    }

    private void InspectAt(Vector2I tile)
    {
        if (_simulation is null)
            return;
        SimEntity? entity = _simulation.State.Entities.Values
            .Where(e => SquaredDistance(e.X, e.Y, tile.X, tile.Y) <= 16)
            .OrderBy(e => SquaredDistance(e.X, e.Y, tile.X, tile.Y))
            .ThenBy(e => e.Id)
            .FirstOrDefault();
        if (entity is not null)
        {
            _simulationRenderer.SelectEntity(entity.Id);
            UpdateInspector();
            return;
        }
        SettlementState? settlement = _simulation.State.Settlements.Values
            .Where(s => SquaredDistance(s.X, s.Y, tile.X, tile.Y) <= 196)
            .OrderBy(s => SquaredDistance(s.X, s.Y, tile.X, tile.Y))
            .FirstOrDefault();
        if (settlement is not null)
            _simulationRenderer.SelectSettlement(settlement.Id);
        else
            _simulationRenderer.ClearSelection();
        UpdateInspector();
    }

    private void PaintAt(Vector2I tile)
    {
        if (_world is null || !_world.IsInside(tile.X, tile.Y))
            return;
        int changed = _terrainEditor.Paint(_world, tile.X, tile.Y, BrushRadius, SelectedTerrain);
        if (changed <= 0)
            return;
        _worldRenderer.RefreshChunks(_terrainEditor.DrainDirtyChunks());
        _checksum = WorldChecksum.Compute(_world);
        _status.Text = $"แก้ Terrain {changed} tiles";
    }

    private void UndoTerrain()
    {
        if (_world is null || !_terrainEditor.Undo(_world))
        {
            _status.Text = "ไม่มี Terrain ให้ย้อนกลับ";
            return;
        }
        _worldRenderer.RefreshChunks(_terrainEditor.DrainDirtyChunks());
        _checksum = WorldChecksum.Compute(_world);
        _status.Text = "ย้อนกลับ Terrain แล้ว";
    }

    private void ApplyDiplomacy(int value)
    {
        if (_simulation is null || _kingdomASelector.ItemCount < 2 || _kingdomBSelector.ItemCount < 2)
            return;
        ulong first = (ulong)_kingdomASelector.GetSelectedId();
        ulong second = (ulong)_kingdomBSelector.GetSelectedId();
        if (first == second)
        {
            _status.Text = "กรุณาเลือกคนละอาณาจักร";
            return;
        }
        _simulation.SetRelation(first, second, value);
        _status.Text = $"ความสัมพันธ์: {_simulation.GetRelationState(first, second)}";
        _renderDirty = true;
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

    private void SaveWorld()
    {
        if (_world is null || _simulation is null)
        {
            _status.Text = "ยังไม่มีโลกให้บันทึก";
            return;
        }
        try
        {
            _saveService.Save(SavePath, _world, _clock);
            WriteAtomic(SimulationSavePath, _simulation.SaveToJson());
            _status.Text = "บันทึกโลกแล้ว";
        }
        catch (Exception exception)
        {
            ReportError("บันทึกไม่สำเร็จ", exception);
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
            _worldRenderer.Bind(_world);
            _checksum = loaded.Checksum;
            _seedInput.Text = _world.Config.Seed.ToString();
            _simulation = File.Exists(SimulationSavePath)
                ? GrandSimulation.LoadFromJson(_world, File.ReadAllText(SimulationSavePath))
                : new GrandSimulation(_world, _world.Config.Seed ^ 0x5A17_2026L);
            _simulationRenderer.Bind(_simulation);
            PopulateKingdomSelectors();
            _setupLayer.Visible = false;
            _renderDirty = true;
            _status.Text = $"โหลดโลกแล้ว — {_simulation.State.Entities.Count:N0} สิ่งมีชีวิต";
            RefreshUi();
        }
        catch (Exception exception)
        {
            ReportError("โหลดไม่สำเร็จ", exception);
        }
    }

    private static void WriteAtomic(string path, string content)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        string temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, true);
    }

    private void TogglePause()
    {
        if (_simulation is null)
            return;
        _clock.TogglePaused();
        UpdatePauseText();
        _status.Text = _clock.IsPaused ? "หยุด Simulation แล้ว" : "Simulation ทำงานต่อ";
    }

    private void SetSpeed(double speed)
    {
        try
        {
            _clock.SetTimeScale(speed);
            _status.Text = $"ความเร็ว x{speed:0}";
        }
        catch (Exception exception)
        {
            ReportError("เปลี่ยนความเร็วไม่สำเร็จ", exception);
        }
    }

    private void UpdatePauseText() => _pauseButton.Text = _clock.IsPaused ? "เล่นต่อ" : "หยุดเวลา";

    private void RefreshUi()
    {
        UpdateDebug();
        UpdateInspector();
        UpdateChronicle();
    }

    private void UpdateDebug()
    {
        if (_world is null || _simulation is null)
        {
            _debug.Text = "ยังไม่ได้สร้างโลก";
            return;
        }
        SimulationBudgetMetrics metrics = _simulation.LastBudgetMetrics;
        int infected = _simulation.State.Diseases.Sum(d => d.InfectedDays.Count);
        int activeArmies = _simulation.State.Armies.Values.Count(a => a.IsActive);
        _debug.Text =
            $"FPS: {Engine.GetFramesPerSecond()} | Clock TPS: {_measuredTps}\n" +
            $"วัน {_simulation.State.Day} | เดือน {_simulation.State.Month} | ปี {_simulation.State.Year}\n" +
            $"ประชากร {_simulation.State.Entities.Count:N0}/{_budget.MaxPopulation:N0} | เมือง {_simulation.State.Settlements.Count} | อาณาจักร {_simulation.State.Kingdoms.Count}\n" +
            $"กองทัพ {activeArmies} | ผู้ติดเชื้อ {infected} | Births {_simulation.State.TotalBirths:N0}\n" +
            $"AI/วัน {metrics.AiEntitiesUpdated}/{_budget.EntityAiUpdatesPerDay} | A* {metrics.PathRequestsUsed}/{_budget.PathRequestsPerDay}\n" +
            $"Render {_renderHz:0} Hz | วัน/เฟรมสูงสุด {_maxDaysPerFrame} | Profile {_budget.Profile}\n" +
            $"โลก {_world.Width}×{_world.Height} | Chunks {_worldRenderer.RenderedChunkCount}\n" +
            $"Checksum {(_checksum.Length >= 12 ? _checksum[..12] : _checksum)}";
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
            _inspector.Text =
                $"{entity.Name} #{entity.Id} | {entity.Species} {entity.Sex}\n" +
                $"Action {entity.Action} | HP {entity.Health:0} | Hunger {entity.Hunger:0} | Energy {entity.Energy:0}\n" +
                $"อายุ {entity.AgeDays / 360f:0.0} ปี | รุ่น {entity.Generation} | ลูก {entity.Children.Count}\n" +
                $"เมือง {settlement} | อาณาจักร {kingdom}\n" +
                $"โรค {disease} | ท้อง {entity.PregnancyDaysRemaining} วัน\n" +
                $"ยีน Speed {entity.SpeedGene:0.00} Vitality {entity.VitalityGene:0.00} Fertility {entity.FertilityGene:0.00}";
            return;
        }
        if (_simulationRenderer.SelectedSettlementId is ulong settlementId && _simulation.State.Settlements.TryGetValue(settlementId, out SettlementState? city))
        {
            int population = _simulation.State.Entities.Values.Count(e => e.SettlementId == settlementId);
            _inspector.Text =
                $"{city.Name} #{city.Id} | {city.Stage}\n" +
                $"ประชากร {population}/{city.Housing} | Happiness {city.Happiness:0}\n" +
                $"Food {city.Food:0} | Wood {city.Wood:0} | Stone {city.Stone:0} | Gold {city.Gold:0}\n" +
                $"Fortification {city.Fortification} | Buildings {string.Join(", ", city.Buildings)}";
            return;
        }
        _inspector.Text = "เลือกเครื่องมือตรวจสอบ แล้วคลิกสิ่งมีชีวิตหรือเมือง\nคลิกขวาใช้ตรวจสอบได้ตลอดเวลา";
    }

    private void UpdateChronicle()
    {
        if (_simulation is null)
            return;
        ChronicleEvent[] events = _simulation.State.Chronicle.TakeLast(16).Reverse().ToArray();
        _chronicle.Text = events.Length == 0
            ? "ยังไม่มีเหตุการณ์"
            : string.Join("\n\n", events.Select(e => $"Day {e.Tick} • {e.Title}\n{e.Description}"));
    }

    private void UpdateToolHelp()
    {
        if (_toolHelp is null)
            return;
        _toolHelp.Text = SelectedTool switch
        {
            InteractionTool.Inspect => "คลิกซ้ายหรือขวาเพื่อตรวจสอบสิ่งมีชีวิตและเมือง",
            InteractionTool.PaintTerrain => "คลิกหรือลากซ้ายเพื่อวาด Terrain",
            InteractionTool.CreateCivilization => "สร้างประชากร 8 คน เมืองหลวง และอาณาจักรใหม่",
            InteractionTool.PowerPlague => "ปล่อยโรคในรัศมีที่กำหนด",
            InteractionTool.PowerLightning or InteractionTool.PowerMeteor => "สร้างความเสียหายจริงและบันทึกใน Chronicle",
            _ => "คลิกบนแผนที่เพื่อใช้เครื่องมือ • เมาส์กลางเลื่อน • ล้อเมาส์ซูม",
        };
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
        _brushOverlay.SetBrush(tile, BrushRadius, SelectedTerrain, _worldRenderer.TilePixelSize);
    }

    private Vector2I MouseTile()
    {
        Vector2 position = GetGlobalMousePosition();
        return new Vector2I(
            Mathf.FloorToInt(position.X / _worldRenderer.TilePixelSize),
            Mathf.FloorToInt(position.Y / _worldRenderer.TilePixelSize));
    }

    private void ZoomCamera(float factor)
    {
        float next = Math.Clamp(_camera.Zoom.X * factor, 0.12f, 4f);
        _camera.Zoom = new Vector2(next, next);
        _renderDirty = true;
    }

    private static int SquaredDistance(int x1, int y1, int x2, int y2)
    {
        int dx = x1 - x2;
        int dy = y1 - y2;
        return dx * dx + dy * dy;
    }

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        return button;
    }

    private static SpinBox CreateSpin(double min, double max, double step, double value) => new()
    {
        MinValue = min,
        MaxValue = max,
        Step = step,
        Value = value,
        UpdateOnTextChanged = true,
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    };

    private static Label Section(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 18);
        return label;
    }

    private static Control LabeledControl(string labelText, Control control)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(155, 0) });
        control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(control);
        return row;
    }

    private void AddToolOption(string text, InteractionTool tool) => _toolSelector.AddItem(text, (int)tool);
    private void AddTerrainOption(string text, TerrainType terrain) => _terrainSelector.AddItem(text, (int)terrain);

    private void ReportError(string message, Exception exception)
    {
        GD.PushError($"{message}: {exception}");
        _status.Text = $"{message}: {exception.Message}";
    }
}
