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
    private void BuildSetupInterface()
    {
        _setupLayer = new CanvasLayer { Name = "WorldSetup", Layer = 50 };
        AddChild(_setupLayer);
        var shade = new ColorRect { AnchorRight = 1, AnchorBottom = 1, Color = new Color(0.02f, 0.025f, 0.04f, 0.97f) };
        _setupLayer.AddChild(shade);
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        _setupLayer.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(780, 650) };
        center.AddChild(panel);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        panel.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);
        root.AddChild(new Label { Text = "WorldForge: Pixel Gods — สร้างโลกที่มีชีวิต", HorizontalAlignment = HorizontalAlignment.Center });
        root.AddChild(new Label { Text = "กำหนดโลก ประชากร ระบบชีวิต สภาพอากาศ เหตุการณ์ และประสิทธิภาพก่อนเริ่ม", HorizontalAlignment = HorizontalAlignment.Center });

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 18);
        root.AddChild(columns);
        var left = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        columns.AddChild(left);
        columns.AddChild(right);

        left.AddChild(Section("โลก"));
        _setupWorldName = AddLineRow(left, "ชื่อโลก", "โลกแห่งชีวิต");
        _setupSeed = AddLineRow(left, "Seed", "1502026");
        _setupWorldSize = new OptionButton();
        foreach (int size in new[] { 128, 256, 384, 512 })
            _setupWorldSize.AddItem($"{size} × {size}", size);
        _setupWorldSize.Select(1);
        AddControlRow(left, "ขนาดโลก", _setupWorldSize);
        _setupSeaLevel = AddSliderRow(left, "ระดับน้ำทะเล", 0.35, 0.62, 0.01, 0.48);
        _setupSeaLevelLabel = new Label { Text = "0.48" };
        _setupSeaLevel.ValueChanged += value => _setupSeaLevelLabel.Text = value.ToString("0.00");
        left.AddChild(_setupSeaLevelLabel);

        left.AddChild(Section("ประชากรเริ่มต้น"));
        _setupKingdoms = AddSpinRow(left, "อาณาจักร", 1, 8, 1, 2);
        _setupPopulationPerKingdom = AddSpinRow(left, "คนต่ออาณาจักร", 5, 100, 1, 12);
        _setupGrazers = AddSpinRow(left, "สัตว์กินพืช", 0, 1000, 5, 45);
        _setupPredators = AddSpinRow(left, "นักล่า", 0, 300, 1, 8);
        _setupMonsters = AddSpinRow(left, "มอนสเตอร์", 0, 100, 1, 2);
        _setupFish = AddSpinRow(left, "ปลา", 0, 1000, 5, 30);
        _setupPopulationCap = AddSpinRow(left, "เพดานประชากรรวม", 25, 6000, 25, 1200);

        right.AddChild(Section("กฎและเป้าหมาย"));
        _setupRelation = new OptionButton();
        _setupRelation.AddItem("สันติ", (int)InitialRelation.Peaceful);
        _setupRelation.AddItem("เป็นกลาง", (int)InitialRelation.Neutral);
        _setupRelation.AddItem("เป็นศัตรู", (int)InitialRelation.Hostile);
        _setupRelation.AddItem("สงครามทันที", (int)InitialRelation.War);
        _setupRelation.Select(1);
        AddControlRow(right, "ความสัมพันธ์เริ่มต้น", _setupRelation);

        _setupScenario = new OptionButton();
        foreach (ScenarioKind scenario in Enum.GetValues<ScenarioKind>())
            _setupScenario.AddItem(ScenarioThai(scenario), (int)scenario);
        AddControlRow(right, "Scenario", _setupScenario);

        _setupReproduction = new CheckButton { Text = "การสืบพันธุ์", ButtonPressed = true };
        _setupAutomaticWar = new CheckButton { Text = "การทูตและสงครามอัตโนมัติ", ButtonPressed = true };
        _setupWeather = new CheckButton { Text = "ฤดูกาลและสภาพอากาศ", ButtonPressed = true };
        _setupEvents = new CheckButton { Text = "เหตุการณ์โลกและตัวเลือก", ButtonPressed = true };
        _setupAudio = new CheckButton { Text = "เสียงบรรยากาศ", ButtonPressed = true };
        _setupAutoPerformance = new CheckButton { Text = "Auto Performance Manager", ButtonPressed = true };
        right.AddChild(_setupReproduction);
        right.AddChild(_setupAutomaticWar);
        right.AddChild(_setupWeather);
        right.AddChild(_setupEvents);
        right.AddChild(_setupAudio);
        right.AddChild(_setupAutoPerformance);

        right.AddChild(Section("ประสิทธิภาพ"));
        _setupProfile = new OptionButton();
        _setupProfile.AddItem("ลื่นสุด", (int)SimulationPerformanceProfile.Economy);
        _setupProfile.AddItem("สมดุล", (int)SimulationPerformanceProfile.Balanced);
        _setupProfile.AddItem("ภาพละเอียด", (int)SimulationPerformanceProfile.Detailed);
        _setupProfile.Select(1);
        AddControlRow(right, "โปรไฟล์", _setupProfile);
        right.AddChild(new Label
        {
            Text = "แนะนำ: โลก 256×256, ประชากรไม่เกิน 1,200 และโปรไฟล์สมดุล\nโลก 512×512 ควรใช้โปรไฟล์ลื่นสุด",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        _setupError = new Label { Modulate = new Color(1f, 0.35f, 0.28f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        root.AddChild(_setupError);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddChild(CreateButton("สร้างโลกและเริ่มเล่น", StartConfiguredWorld));
        buttons.AddChild(CreateButton("ยกเลิก", HideSetup));
        root.AddChild(buttons);
    }
}
