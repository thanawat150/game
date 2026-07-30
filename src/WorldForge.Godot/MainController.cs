using Godot;
using WorldForge.Core.Editing;
using WorldForge.Core.Persistence;
using WorldForge.Core.Simulation;
using WorldForge.Core.World;
using WorldForge.Presentation;

namespace WorldForge;

public sealed partial class MainController : Node2D
{
    private readonly TerrainEditor _terrainEditor = new();
    private readonly WorldSaveService _saveService = new();
    private WorldMap? _world;
    private SimulationClock _clock = new();
    private WorldChunkRenderer _renderer = null!;
    private BrushOverlay _brushOverlay = null!;
    private Camera2D _camera = null!;
    private LineEdit _seedInput = null!;
    private OptionButton _terrainSelector = null!;
    private HSlider _brushSlider = null!;
    private Label _brushValue = null!;
    private Label _statusLabel = null!;
    private Label _debugLabel = null!;
    private Button _pauseButton = null!;
    private bool _isPanning;
    private int _ticksThisSecond;
    private int _measuredTps;
    private double _metricsAccumulator;
    private string _checksum = string.Empty;

    private TerrainType SelectedTerrain => (TerrainType)_terrainSelector.GetSelectedId();
    private int BrushRadius => Math.Max(0, (int)_brushSlider.Value - 1);
    private string SavePath => ProjectSettings.GlobalizePath("user://saves/slot_1.wfg.json");

    public override void _Ready()
    {
        DisplayServer.WindowSetTitle("WorldForge: Pixel Gods — Phase 1");
        _renderer = new WorldChunkRenderer { Name = "WorldRenderer" };
        AddChild(_renderer);
        _brushOverlay = new BrushOverlay { Name = "BrushOverlay", ZIndex = 50 };
        AddChild(_brushOverlay);
        _camera = new Camera2D { Name = "WorldCamera", Enabled = true, Position = new Vector2(512, 512) };
        AddChild(_camera);
        BuildInterface();
        GenerateWorld();
    }

    public override void _Process(double delta)
    {
        int steps = _clock.Advance(delta);
        _ticksThisSecond += steps;
        _metricsAccumulator += delta;
        if (_metricsAccumulator >= 1.0)
        {
            _measuredTps = _ticksThisSecond;
            _ticksThisSecond = 0;
            _metricsAccumulator -= 1.0;
        }
        UpdateDebugOverlay();
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
            if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
            {
                PaintAtMouse();
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
                if ((motion.ButtonMask & MouseButtonMask.Left) != 0)
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
            OffsetBottom = 54,
        };
        canvas.AddChild(topPanel);
        var top = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        top.AddThemeConstantOverride("separation", 8);
        topPanel.AddChild(top);
        top.AddChild(new Label { Text = "WorldForge: Pixel Gods  |  Seed" });
        _seedInput = new LineEdit { Text = "1502026", CustomMinimumSize = new Vector2(160, 0) };
        top.AddChild(_seedInput);
        top.AddChild(CreateButton("สร้างโลก", GenerateWorld));
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
            OffsetRight = 238,
            OffsetBottom = 346,
        };
        canvas.AddChild(toolsPanel);
        var tools = new VBoxContainer();
        tools.AddThemeConstantOverride("separation", 8);
        toolsPanel.AddChild(tools);
        tools.AddChild(new Label { Text = "เครื่องมือสร้างโลก" });
        _terrainSelector = new OptionButton();
        AddTerrainOption("มหาสมุทรลึก", TerrainType.DeepOcean);
        AddTerrainOption("น้ำตื้น", TerrainType.ShallowWater);
        AddTerrainOption("ชายหาด", TerrainType.Beach);
        AddTerrainOption("ทุ่งหญ้า", TerrainType.Grassland);
        AddTerrainOption("ป่า", TerrainType.Forest);
        AddTerrainOption("ภูเขา", TerrainType.Mountain);
        _terrainSelector.Select((int)TerrainType.Grassland);
        tools.AddChild(_terrainSelector);
        tools.AddChild(new Label { Text = "ขนาดแปรง" });
        var brushRow = new HBoxContainer();
        tools.AddChild(brushRow);
        _brushSlider = new HSlider
        {
            MinValue = 1,
            MaxValue = 12,
            Step = 1,
            Value = 3,
            CustomMinimumSize = new Vector2(160, 0),
        };
        _brushSlider.ValueChanged += value =>
        {
            _brushValue.Text = $"{(int)value}";
            UpdateBrushPreview();
        };
        brushRow.AddChild(_brushSlider);
        _brushValue = new Label { Text = "3" };
        brushRow.AddChild(_brushValue);
        tools.AddChild(CreateButton("ย้อนกลับการแก้ไข", UndoTerrain));
        tools.AddChild(new Label
        {
            Text = "ควบคุม\n• ลากเมาส์กลาง: เลื่อนกล้อง\n• ล้อเมาส์: ซูม\n• คลิกซ้าย: วาด Terrain",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        _debugLabel = new Label
        {
            AnchorLeft = 1,
            AnchorRight = 1,
            OffsetLeft = -330,
            OffsetTop = 64,
            OffsetRight = -8,
            OffsetBottom = 246,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        canvas.AddChild(_debugLabel);

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

    private void AddTerrainOption(string displayName, TerrainType terrain)
    {
        _terrainSelector.AddItem(displayName, (int)terrain);
    }

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
            _clock = new SimulationClock();
            _terrainEditor.ClearHistory();
            _renderer.Bind(_world);
            _checksum = WorldChecksum.Compute(_world);
            float worldPixels = _world.Width * _renderer.TilePixelSize;
            _camera.Position = new Vector2(worldPixels / 2f, worldPixels / 2f);
            _camera.Zoom = new Vector2(0.72f, 0.72f);
            _statusLabel.Text = $"สร้างโลก Seed {seed} แล้ว — {_world.TileCount:N0} tiles";
            UpdateBrushPreview();
        }
        catch (Exception exception)
        {
            ReportError("สร้างโลกไม่สำเร็จ", exception);
        }
    }

    private void PaintAtMouse()
    {
        if (_world is null)
            return;
        Vector2I tile = MouseTile();
        if (!_world.IsInside(tile.X, tile.Y))
            return;
        try
        {
            int changed = _terrainEditor.Paint(_world, tile.X, tile.Y, BrushRadius, SelectedTerrain);
            if (changed > 0)
            {
                _renderer.RefreshChunks(_terrainEditor.DrainDirtyChunks());
                _checksum = WorldChecksum.Compute(_world);
                _statusLabel.Text = $"แก้ไข Terrain {changed} tiles ที่ ({tile.X}, {tile.Y})";
            }
        }
        catch (Exception exception)
        {
            ReportError("แก้ไข Terrain ไม่สำเร็จ", exception);
        }
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

    private void SaveWorld()
    {
        if (_world is null)
            return;
        try
        {
            _saveService.Save(SavePath, _world, _clock);
            _statusLabel.Text = $"บันทึกโลกแล้ว: {SavePath}";
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
            _statusLabel.Text = $"โหลดโลกแล้ว — Tick {_clock.TickCount:N0}";
            UpdatePauseText();
            UpdateBrushPreview();
        }
        catch (Exception exception)
        {
            ReportError("โหลดโลกไม่สำเร็จ", exception);
        }
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
        if (_world is null)
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

    private void UpdateDebugOverlay()
    {
        if (_world is null)
        {
            _debugLabel.Text = "No world loaded";
            return;
        }
        Vector2I tile = MouseTile();
        string tileText = _world.IsInside(tile.X, tile.Y)
            ? $"Tile: {tile.X}, {tile.Y}  {_world.GetTerrain(tile.X, tile.Y)}"
            : "Tile: outside world";
        _debugLabel.Text =
            $"FPS: {Engine.GetFramesPerSecond()}\n" +
            $"Simulation TPS: {_measuredTps}\n" +
            $"Tick: {_clock.TickCount:N0}\n" +
            $"Speed: x{_clock.TimeScale:0}  Paused: {_clock.IsPaused}\n" +
            $"World: {_world.Width}×{_world.Height}\n" +
            $"Tiles: {_world.TileCount:N0}\n" +
            $"Chunks: {_renderer.RenderedChunkCount}\n" +
            $"Overrides: {_world.TerrainOverrides.Count:N0}\n" +
            $"Undo: {_terrainEditor.UndoCount}\n" +
            $"Checksum: {(_checksum.Length >= 12 ? _checksum[..12] : _checksum)}\n" +
            tileText;
    }

    private void ReportError(string message, Exception exception)
    {
        GD.PushError($"{message}: {exception}");
        _statusLabel.Text = $"{message}: {exception.Message}";
    }
}
