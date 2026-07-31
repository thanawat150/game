using System.Text.Json;
using Godot;
using WorldForge.Core.Simulation;

namespace WorldForge;

public sealed partial class ExpansionModEditorOverlay : Node
{
    private LivingWorldMainController? _controller;
    private PanelContainer _panel = null!;
    private CheckButton _naval = null!;
    private CheckButton _fantasy = null!;
    private CheckButton _magic = null!;
    private CheckButton _nomads = null!;
    private SpinBox _legendRate = null!;
    private SpinBox _constructionRate = null!;
    private SpinBox _fleetRate = null!;
    private SpinBox _magicRate = null!;
    private SpinBox _nomadRate = null!;
    private SpinBox _ruinCount = null!;
    private Label _status = null!;

    public override void _Ready()
    {
        _controller = GetParentOrNull<LivingWorldMainController>();
        BuildInterface();
    }

    private void BuildInterface()
    {
        var layer = new CanvasLayer { Layer = 24 };
        AddChild(layer);
        var open = new Button
        {
            Text = "MOD",
            AnchorLeft = 1,
            AnchorRight = 1,
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetLeft = -68,
            OffsetRight = -10,
            OffsetTop = -72,
            OffsetBottom = -38,
            TooltipText = "เปิด Mod Editor สำหรับระบบ Expansion",
        };
        open.Pressed += TogglePanel;
        layer.AddChild(open);

        _panel = new PanelContainer
        {
            AnchorLeft = 1,
            AnchorRight = 1,
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetLeft = -410,
            OffsetRight = -76,
            OffsetTop = -620,
            OffsetBottom = -40,
            Visible = false,
        };
        layer.AddChild(_panel);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _panel.AddChild(scroll);
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(305, 0) };
        root.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(root);

        var title = new HBoxContainer();
        title.AddChild(new Label { Text = "EXPANSION MOD EDITOR", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var close = new Button { Text = "×" };
        close.Pressed += TogglePanel;
        title.AddChild(close);
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "ปรับกฎของโลกปัจจุบันได้ทันที หรือ Export เป็นไฟล์ Mod เพื่อแชร์กับผู้อื่น",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        _naval = AddToggle(root, "เปิดกองเรือและสงครามทางทะเล");
        _fantasy = AddToggle(root, "เปิดเผ่าพันธุ์แฟนตาซี");
        _magic = AddToggle(root, "เปิดเวทมนตร์และนักเวท");
        _nomads = AddToggle(root, "เปิดชนเผ่าเร่ร่อน");
        root.AddChild(new HSeparator());
        _legendRate = AddNumber(root, "อัตราเกิดตำนาน", 0.1, 5, 0.1, 1);
        _constructionRate = AddNumber(root, "ความเร็วการก่อสร้าง", 0.1, 5, 0.1, 1);
        _fleetRate = AddNumber(root, "ความเร็วเรือ", 0.25, 4, 0.25, 1);
        _magicRate = AddNumber(root, "ความถี่การใช้เวท", 0, 5, 0.1, 1);
        _nomadRate = AddNumber(root, "ความถี่ชนเผ่าเร่ร่อน", 0, 5, 0.1, 1);
        _ruinCount = AddNumber(root, "จำนวนซากโบราณเป้าหมาย", 0, 100, 1, 12);

        var actions = new HBoxContainer();
        actions.AddChild(CreateButton("ใช้ค่า", ApplyRules));
        actions.AddChild(CreateButton("คืนค่าเริ่มต้น", ResetDefaults));
        root.AddChild(actions);
        var fileActions = new HBoxContainer();
        fileActions.AddChild(CreateButton("Export Mod", ExportRules));
        fileActions.AddChild(CreateButton("Import Mod", ImportRules));
        root.AddChild(fileActions);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        root.AddChild(_status);
    }

    private void TogglePanel()
    {
        _panel.Visible = !_panel.Visible;
        if (_panel.Visible) LoadFromRuntime();
    }

    private void LoadFromRuntime()
    {
        WorldExpansionDirector? expansion = _controller?.GetExpansionRuntime();
        if (expansion is null)
        {
            _status.Text = "สร้างหรือโหลดโลกก่อนจึงจะใช้ Mod Editor ได้";
            return;
        }
        ExpansionModRules rules = expansion.State.ModRules;
        _naval.ButtonPressed = rules.EnableNavalWarfare;
        _fantasy.ButtonPressed = rules.EnableFantasyRaces;
        _magic.ButtonPressed = rules.EnableMagic;
        _nomads.ButtonPressed = rules.EnableNomads;
        _legendRate.Value = rules.LegendPromotionMultiplier;
        _constructionRate.Value = rules.ConstructionSpeedMultiplier;
        _fleetRate.Value = rules.FleetSpeedMultiplier;
        _magicRate.Value = rules.MagicFrequencyMultiplier;
        _nomadRate.Value = rules.NomadFrequencyMultiplier;
        _ruinCount.Value = rules.InitialRuinCount;
        _status.Text = $"กำลังแก้ Mod: {rules.Name}";
    }

    private void ApplyRules()
    {
        WorldExpansionDirector? expansion = _controller?.GetExpansionRuntime();
        if (expansion is null)
        {
            _status.Text = "ยังไม่มีโลกที่กำลังเล่น";
            return;
        }
        ExpansionModRules rules = expansion.State.ModRules;
        rules.EnableNavalWarfare = _naval.ButtonPressed;
        rules.EnableFantasyRaces = _fantasy.ButtonPressed;
        rules.EnableMagic = _magic.ButtonPressed;
        rules.EnableNomads = _nomads.ButtonPressed;
        rules.LegendPromotionMultiplier = (float)_legendRate.Value;
        rules.ConstructionSpeedMultiplier = (float)_constructionRate.Value;
        rules.FleetSpeedMultiplier = (float)_fleetRate.Value;
        rules.MagicFrequencyMultiplier = (float)_magicRate.Value;
        rules.NomadFrequencyMultiplier = (float)_nomadRate.Value;
        rules.InitialRuinCount = (int)_ruinCount.Value;
        expansion.ImportModPack(expansion.ExportModPack());
        _controller?.NotifyExpansionRulesChanged();
        _status.Text = "ใช้กฎใหม่กับโลกปัจจุบันแล้ว";
    }

    private void ResetDefaults()
    {
        WorldExpansionDirector? expansion = _controller?.GetExpansionRuntime();
        if (expansion is null) return;
        expansion.State.ModRules = new ExpansionModRules();
        LoadFromRuntime();
        _controller?.NotifyExpansionRulesChanged();
        _status.Text = "คืนค่า Mod เริ่มต้นแล้ว";
    }

    private void ExportRules()
    {
        WorldExpansionDirector? expansion = _controller?.GetExpansionRuntime();
        if (expansion is null) return;
        string path = ProjectSettings.GlobalizePath("user://mods/worldforge_expansion_mod.json");
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, expansion.ExportModPack());
        _status.Text = $"Export แล้ว: {path}";
    }

    private void ImportRules()
    {
        WorldExpansionDirector? expansion = _controller?.GetExpansionRuntime();
        if (expansion is null) return;
        string path = ProjectSettings.GlobalizePath("user://mods/worldforge_expansion_mod.json");
        if (!File.Exists(path))
        {
            _status.Text = "ไม่พบไฟล์ Mod กด Export ก่อนเพื่อสร้าง Template";
            return;
        }
        try
        {
            expansion.ImportModPack(File.ReadAllText(path));
            LoadFromRuntime();
            _controller?.NotifyExpansionRulesChanged();
            _status.Text = "Import Mod สำเร็จ";
        }
        catch (Exception exception)
        {
            _status.Text = $"Import ไม่สำเร็จ: {exception.Message}";
        }
    }

    private static CheckButton AddToggle(VBoxContainer root, string text)
    {
        var control = new CheckButton { Text = text };
        root.AddChild(control);
        return control;
    }

    private static SpinBox AddNumber(VBoxContainer root, string label, double min, double max, double step, double value)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var control = new SpinBox { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(90, 0) };
        row.AddChild(control);
        root.AddChild(row);
        return control;
    }

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Text = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        button.Pressed += action;
        return button;
    }
}
