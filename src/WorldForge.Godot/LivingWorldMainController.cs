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
    private enum InteractionTool
    {
        Inspect,
        PaintTerrain,
        SpawnGrazer,
        SpawnPredator,
        SpawnSettler,
        SpawnMonster,
        SpawnFish,
        CreateCivilization,
        PowerCreateForest,
        PowerBlessing,
        PowerCurse,
        PowerLightning,
        PowerPlague,
        PowerMeteor,
    }

    private enum InitialRelation { Peaceful, Neutral, Hostile, War }
    private enum ChronicleFilter { All, Life, City, Kingdom, War, Disease, Power, Event }

    private readonly TerrainEditor _terrainEditor = new();
    private readonly WorldSaveService _saveService = new();
    private readonly Stopwatch _simulationTimer = new();

    private WorldMap? _world;
    private GrandSimulation? _simulation;
    private LivingWorldDirector? _director;
    private SimulationClock _clock = new();
    private SimulationBudgetOptions _budget = SimulationBudgetOptions.ForProfile(SimulationPerformanceProfile.Balanced, 1200);

    private WorldChunkRenderer _terrainRenderer = null!;
    private LivingWorldRenderer _livingRenderer = null!;
    private BrushOverlay _brushOverlay = null!;
    private Camera2D _camera = null!;
    private CanvasLayer _gameLayer = null!;
    private CanvasLayer _setupLayer = null!;
    private CanvasLayer _tutorialLayer = null!;
    private ColorRect _screenTint = null!;
    private LivingMiniMap _miniMap = null!;
    private ProceduralAmbientAudio _ambientAudio = null!;
    private ProceduralAmbientAudio _weatherAudio = null!;
    private ProceduralAmbientAudio _eventAudio = null!;

    private LineEdit _setupWorldName = null!;
    private LineEdit _setupSeed = null!;
    private OptionButton _setupWorldSize = null!;
    private HSlider _setupSeaLevel = null!;
    private Label _setupSeaLevelLabel = null!;
    private SpinBox _setupKingdoms = null!;
    private SpinBox _setupPopulationPerKingdom = null!;
    private SpinBox _setupGrazers = null!;
    private SpinBox _setupPredators = null!;
    private SpinBox _setupMonsters = null!;
    private SpinBox _setupFish = null!;
    private SpinBox _setupPopulationCap = null!;
    private OptionButton _setupProfile = null!;
    private OptionButton _setupRelation = null!;
    private OptionButton _setupScenario = null!;
    private CheckButton _setupReproduction = null!;
    private CheckButton _setupAutomaticWar = null!;
    private CheckButton _setupWeather = null!;
    private CheckButton _setupEvents = null!;
    private CheckButton _setupAudio = null!;
    private CheckButton _setupAutoPerformance = null!;
    private Label _setupError = null!;

    private Label _worldTitle = null!;
    private OptionButton _saveSlot = null!;
    private CheckButton _autosaveEnabled = null!;
    private SpinBox _autosaveMinutes = null!;
    private Button _pauseButton = null!;
    private LineEdit _searchInput = null!;
    private OptionButton _toolSelector = null!;
    private OptionButton _terrainSelector = null!;
    private HSlider _brushSize = null!;
    private Label _brushSizeLabel = null!;
    private OptionButton _overlaySelector = null!;
    private OptionButton _performanceProfile = null!;
    private CheckButton _autoPerformance = null!;
    private SpinBox _populationCap = null!;
    private SpinBox _aiBudget = null!;
    private SpinBox _pathBudget = null!;
    private SpinBox _renderHzControl = null!;
    private SpinBox _maxDaysPerFrameControl = null!;
    private SpinBox _settlerCap = null!;
    private SpinBox _grazerCap = null!;
    private SpinBox _predatorCap = null!;
    private SpinBox _monsterCap = null!;
    private SpinBox _fishCap = null!;
    private HSlider _birthMultiplier = null!;
    private HSlider _migrationMultiplier = null!;
    private Label _performanceLabel = null!;
    private Label _debugLabel = null!;
    private Label _statusLabel = null!;
    private Label _inspectorLabel = null!;
    private Label _scenarioLabel = null!;
    private ProgressBar _scenarioProgress = null!;
    private OptionButton _scenarioSelector = null!;

    private LineEdit _renameInput = null!;
    private OptionButton _cityPriority = null!;
    private OptionButton _cityBorder = null!;
    private HSlider _cityTax = null!;
    private HSlider _cityBirth = null!;
    private SpinBox _cityFoodReserve = null!;
    private SpinBox _cityPopulationLimit = null!;
    private CheckButton _cityAutoBuild = null!;
    private CheckButton _cityQuarantine = null!;
    private CheckButton _cityEvacuate = null!;

    private OptionButton _kingdomBorder = null!;
    private HSlider _kingdomTax = null!;
    private HSlider _kingdomBirth = null!;
    private HSlider _kingdomMilitary = null!;
    private SpinBox _kingdomPopulationLimit = null!;
    private CheckButton _kingdomPreferPeace = null!;

    private OptionButton _chronicleFilter = null!;
    private LineEdit _chronicleSearch = null!;
    private ItemList _chronicleList = null!;
    private readonly List<ChronicleEvent> _visibleChronicle = new();

    private PanelContainer _eventPanel = null!;
    private Label _eventTitle = null!;
    private Label _eventDescription = null!;
    private readonly Button[] _eventChoiceButtons = new Button[3];

    private Label _tutorialText = null!;
    private int _tutorialStep;

    private bool _isPanning;
    private bool _renderDirty;
    private int _pendingSimulationTicks;
    private int _queuedManualDays;
    private int _maxDaysPerFrame = 2;
    private double _renderHz = 7;
    private double _renderAccumulator;
    private double _uiAccumulator;
    private double _metricsAccumulator;
    private double _autosaveAccumulator;
    private double _performanceAccumulator;
    private int _ticksThisSecond;
    private int _measuredTps;
    private double _lastSimulationMs;
    private double _averageSimulationMs;
    private string _checksum = string.Empty;
    private int _lastChronicleCount;
    private int _lastEventId;

    private InteractionTool SelectedTool => (InteractionTool)_toolSelector.GetSelectedId();
    private TerrainType SelectedTerrain => (TerrainType)_terrainSelector.GetSelectedId();
    private int BrushRadius => Math.Max(0, (int)_brushSize.Value - 1);
    private int CurrentSlot => Math.Max(1, (int)_saveSlot.GetSelectedId());

    public override void _Ready()
    {
        DisplayServer.WindowSetTitle("WorldForge: Pixel Gods — Living World & Management");
        _terrainRenderer = new WorldChunkRenderer { Name = "TerrainRenderer", ZIndex = 0 };
        AddChild(_terrainRenderer);
        _livingRenderer = new LivingWorldRenderer { Name = "LivingWorldRenderer", ZIndex = 25 };
        AddChild(_livingRenderer);
        _brushOverlay = new BrushOverlay { Name = "BrushOverlay", ZIndex = 60, Visible = false };
        AddChild(_brushOverlay);
        _camera = new Camera2D { Name = "WorldCamera", Enabled = true, Position = new Vector2(512, 512) };
        AddChild(_camera);

        BuildAudio();
        BuildGameInterface();
        BuildSetupInterface();
        BuildTutorial();
        ShowSetup();
    }

    public override void _Process(double delta)
    {
        if (_simulation is null || _director is null || _world is null)
            return;

        _livingRenderer.AdvanceAnimation(delta);
        float visualHours = (float)(delta * Math.Max(0.5, _clock.TimeScale) * 0.8);
        _director.AdvanceVisualTime(visualHours);

        int steps = _clock.Advance(delta);
        _pendingSimulationTicks += steps;
        _ticksThisSecond += steps;
        _renderAccumulator += delta;
        _uiAccumulator += delta;
        _metricsAccumulator += delta;
        _autosaveAccumulator += delta;
        _performanceAccumulator += delta;

        int daysThisFrame = 0;
        while ((_pendingSimulationTicks >= 10 || _queuedManualDays > 0) && daysThisFrame < _maxDaysPerFrame)
        {
            if (_queuedManualDays > 0) _queuedManualDays--;
            else _pendingSimulationTicks -= 10;

            _simulationTimer.Restart();
            _simulation.AdvanceDayBudgeted(_budget);
            _director.AdvanceDay();
            _simulationTimer.Stop();
            _lastSimulationMs = _simulationTimer.Elapsed.TotalMilliseconds;
            _averageSimulationMs = _averageSimulationMs <= 0 ? _lastSimulationMs : _averageSimulationMs * 0.88 + _lastSimulationMs * 0.12;
            daysThisFrame++;
            _renderDirty = true;
        }
        _pendingSimulationTicks = Math.Min(_pendingSimulationTicks, 30);

        UpdateCameraAwareRendering();
        if (_renderDirty && _renderAccumulator >= 1.0 / Math.Max(1, _renderHz))
        {
            _renderAccumulator = 0;
            _renderDirty = false;
            _livingRenderer.Refresh();
            _miniMap.Refresh();
        }

        if (_uiAccumulator >= 0.25)
        {
            _uiAccumulator = 0;
            RefreshUi();
            UpdateAudio();
            UpdateScreenTint();
        }

        if (_metricsAccumulator >= 1)
        {
            _measuredTps = _ticksThisSecond;
            _ticksThisSecond = 0;
            _metricsAccumulator -= 1;
            UpdatePerformanceLabel();
        }

        if (_performanceAccumulator >= 2)
        {
            _performanceAccumulator = 0;
            ApplyAutoPerformance();
        }

        if (_autosaveEnabled.ButtonPressed && _autosaveAccumulator >= Math.Max(30, _autosaveMinutes.Value * 60))
        {
            _autosaveAccumulator = 0;
            SaveWorld(showStatus: false);
            _statusLabel.Text = $"Autosave ช่อง {CurrentSlot} สำเร็จ";
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_setupLayer.Visible)
            return;

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.F5)
            {
                SaveWorld();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (key.Keycode == Key.F9)
            {
                LoadWorld();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (key.Keycode == Key.Space)
            {
                TogglePause();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (_world is null)
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
}
