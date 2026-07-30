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
    private void BuildLeftPanel()
    {
        var panel = new PanelContainer
        {
            OffsetLeft = 8,
            OffsetTop = 66,
            OffsetRight = 326,
            AnchorBottom = 1,
            OffsetBottom = -40,
        };
        _gameLayer.AddChild(panel);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        panel.AddChild(scroll);
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(296, 0) };
        root.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(root);

        root.AddChild(Section("เครื่องมือโลก"));
        _toolSelector = new OptionButton();
        AddTool("ตรวจสอบ", InteractionTool.Inspect);
        AddTool("วาด Terrain", InteractionTool.PaintTerrain);
        AddTool("วางสัตว์กินพืช", InteractionTool.SpawnGrazer);
        AddTool("วางนักล่า", InteractionTool.SpawnPredator);
        AddTool("วางประชากร", InteractionTool.SpawnSettler);
        AddTool("วางสัตว์ประหลาด", InteractionTool.SpawnMonster);
        AddTool("วางปลา", InteractionTool.SpawnFish);
        AddTool("สร้างเมืองและอาณาจักร", InteractionTool.CreateCivilization);
        AddTool("พลัง: สร้างป่า", InteractionTool.PowerCreateForest);
        AddTool("พลัง: อวยพร", InteractionTool.PowerBlessing);
        AddTool("พลัง: สาป", InteractionTool.PowerCurse);
        AddTool("พลัง: สายฟ้า", InteractionTool.PowerLightning);
        AddTool("พลัง: โรคระบาด", InteractionTool.PowerPlague);
        AddTool("พลัง: อุกกาบาต", InteractionTool.PowerMeteor);
        _toolSelector.Select(0);
        _toolSelector.ItemSelected += _ => UpdateBrushPreview();
        root.AddChild(_toolSelector);

        _terrainSelector = new OptionButton();
        foreach (TerrainType terrain in Enum.GetValues<TerrainType>())
            _terrainSelector.AddItem(TerrainThai(terrain), (int)terrain);
        _terrainSelector.Select((int)TerrainType.Grassland);
        root.AddChild(_terrainSelector);

        var brushRow = new HBoxContainer();
        _brushSize = new HSlider { MinValue = 1, MaxValue = 12, Step = 1, Value = 3, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _brushSize.ValueChanged += value => { _brushSizeLabel.Text = ((int)value).ToString(); UpdateBrushPreview(); };
        brushRow.AddChild(_brushSize);
        _brushSizeLabel = new Label { Text = "3" };
        brushRow.AddChild(_brushSizeLabel);
        root.AddChild(brushRow);
        root.AddChild(CreateButton("ย้อนกลับ Terrain", UndoTerrain));
        root.AddChild(CreateButton("จำลองเพิ่ม 30 วัน", () => _queuedManualDays += 30));

        root.AddChild(new HSeparator());
        root.AddChild(Section("Minimap และ Overlay"));
        _overlaySelector = new OptionButton();
        foreach (LivingOverlayMode mode in Enum.GetValues<LivingOverlayMode>())
            _overlaySelector.AddItem(OverlayThai(mode), (int)mode);
        _overlaySelector.ItemSelected += id =>
        {
            if (_livingRenderer is null || _miniMap is null) return;
            LivingOverlayMode mode = (LivingOverlayMode)(int)id;
            _livingRenderer.OverlayMode = mode;
            _miniMap.OverlayMode = mode;
            _renderDirty = true;
        };
        root.AddChild(_overlaySelector);

        _miniMap = new LivingMiniMap { CustomMinimumSize = new Vector2(288, 170), MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        _miniMap.TileRequested += tile => _camera.Position = TileCenter(tile.X, tile.Y);
        root.AddChild(_miniMap);

        root.AddChild(new HSeparator());
        root.AddChild(Section("Auto Performance Manager"));
        _performanceProfile = new OptionButton();
        _performanceProfile.AddItem("ลื่นสุด", (int)SimulationPerformanceProfile.Economy);
        _performanceProfile.AddItem("สมดุล", (int)SimulationPerformanceProfile.Balanced);
        _performanceProfile.AddItem("ภาพละเอียด", (int)SimulationPerformanceProfile.Detailed);
        _performanceProfile.AddItem("กำหนดเอง", (int)SimulationPerformanceProfile.Custom);
        _performanceProfile.Select(1);
        _performanceProfile.ItemSelected += id => ApplyPerformanceProfile((SimulationPerformanceProfile)(int)id);
        root.AddChild(_performanceProfile);
        _autoPerformance = new CheckButton { Text = "ปรับตาม FPS อัตโนมัติ", ButtonPressed = true };
        _autoPerformance.Toggled += enabled => { if (_director is not null) _director.State.Settings.AutoPerformance = enabled; };
        root.AddChild(_autoPerformance);
        _populationCap = AddSpinRow(root, "ประชากรรวมสูงสุด", 25, 6000, 25, 1200);
        _aiBudget = AddSpinRow(root, "AI ต่อวัน", 10, 2000, 10, 120);
        _pathBudget = AddSpinRow(root, "A* ต่อวัน", 0, 200, 1, 12);
        _renderHzControl = AddSpinRow(root, "Render Hz", 1, 30, 1, 7);
        _maxDaysPerFrameControl = AddSpinRow(root, "วันสูงสุด/เฟรม", 1, 8, 1, 2);
        foreach (SpinBox control in new[] { _populationCap, _aiBudget, _pathBudget, _renderHzControl, _maxDaysPerFrameControl })
            control.ValueChanged += _ => ApplyRuntimePerformanceControls();
        _performanceLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        root.AddChild(_performanceLabel);

        root.AddChild(new HSeparator());
        root.AddChild(Section("ควบคุมประชากร"));
        _settlerCap = AddSpinRow(root, "ประชากรมนุษย์", 0, 6000, 10, 800);
        _grazerCap = AddSpinRow(root, "สัตว์กินพืช", 0, 6000, 10, 260);
        _predatorCap = AddSpinRow(root, "นักล่า", 0, 1000, 5, 80);
        _monsterCap = AddSpinRow(root, "มอนสเตอร์", 0, 500, 1, 24);
        _fishCap = AddSpinRow(root, "ปลา", 0, 6000, 10, 280);
        foreach (SpinBox control in new[] { _settlerCap, _grazerCap, _predatorCap, _monsterCap, _fishCap })
            control.ValueChanged += _ => ApplyPopulationControls();
        _birthMultiplier = AddSliderRow(root, "อัตราเกิด", 0, 2, 0.05, 1);
        _migrationMultiplier = AddSliderRow(root, "การอพยพ", 0, 3, 0.1, 1);
        _birthMultiplier.ValueChanged += _ => ApplyPopulationControls();
        _migrationMultiplier.ValueChanged += _ => ApplyPopulationControls();

        _debugLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        root.AddChild(_debugLabel);
    }
}
