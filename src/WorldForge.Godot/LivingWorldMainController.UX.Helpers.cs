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
    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        return button;
    }

    private static Label Section(string text) => new() { Text = text };

    private void AddTool(string text, InteractionTool tool) => _toolSelector.AddItem(text, (int)tool);

    private static SpinBox CreateSpin(double min, double max, double step, double value, float width = 90)
    {
        return new SpinBox { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(width, 0) };
    }

    private static SpinBox AddSpinRow(VBoxContainer parent, string label, double min, double max, double step, double value)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        SpinBox spin = CreateSpin(min, max, step, value);
        row.AddChild(spin);
        parent.AddChild(row);
        return spin;
    }

    private static HSlider AddSliderRow(VBoxContainer parent, string label, double min, double max, double step, double value)
    {
        var box = new VBoxContainer();
        box.AddChild(new Label { Text = label });
        var row = new HBoxContainer();
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var valueLabel = new Label { Text = value.ToString("0.00"), CustomMinimumSize = new Vector2(48, 0) };
        slider.ValueChanged += v => valueLabel.Text = v.ToString("0.00");
        row.AddChild(slider);
        row.AddChild(valueLabel);
        box.AddChild(row);
        parent.AddChild(box);
        return slider;
    }

    private static LineEdit AddLineRow(VBoxContainer parent, string label, string value)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(120, 0) });
        var line = new LineEdit { Text = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(line);
        parent.AddChild(row);
        return line;
    }

    private static void AddControlRow(VBoxContainer parent, string label, Control control)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        control.CustomMinimumSize = new Vector2(150, 0);
        row.AddChild(control);
        parent.AddChild(row);
    }

    private static OptionButton AddEnumOption<T>(VBoxContainer parent, Func<T, string> label) where T : struct, Enum
    {
        var option = new OptionButton();
        foreach (T value in Enum.GetValues<T>())
            option.AddItem(label(value), Convert.ToInt32(value));
        parent.AddChild(option);
        return option;
    }

    private static string TerrainThai(TerrainType terrain) => terrain switch
    {
        TerrainType.DeepOcean => "มหาสมุทรลึก",
        TerrainType.ShallowWater => "น้ำตื้น",
        TerrainType.Beach => "ชายหาด",
        TerrainType.Grassland => "ทุ่งหญ้า",
        TerrainType.Forest => "ป่า",
        TerrainType.Mountain => "ภูเขา",
        _ => terrain.ToString(),
    };

    private static string OverlayThai(LivingOverlayMode mode) => mode switch
    {
        LivingOverlayMode.None => "ปกติ",
        LivingOverlayMode.Population => "ความหนาแน่นประชากร",
        LivingOverlayMode.Food => "อาหาร",
        LivingOverlayMode.Happiness => "ความสุข",
        LivingOverlayMode.Disease => "โรคระบาด",
        LivingOverlayMode.War => "สงคราม",
        LivingOverlayMode.Kingdom => "อาณาเขต",
        LivingOverlayMode.Trade => "การค้า",
        LivingOverlayMode.Migration => "การอพยพ",
        LivingOverlayMode.Weather => "สภาพอากาศ",
        LivingOverlayMode.Performance => "Performance Hotspot",
        _ => mode.ToString(),
    };

    private static string PriorityThai(CityPriority priority) => priority switch
    {
        CityPriority.Balanced => "สมดุล",
        CityPriority.Food => "อาหาร",
        CityPriority.Housing => "ที่อยู่อาศัย",
        CityPriority.Economy => "เศรษฐกิจ",
        CityPriority.Knowledge => "ความรู้",
        CityPriority.Faith => "ศรัทธา",
        CityPriority.Defense => "ป้องกัน",
        _ => priority.ToString(),
    };

    private static string BorderThai(BorderPolicy policy) => policy switch
    {
        BorderPolicy.Open => "เปิด",
        BorderPolicy.Controlled => "ควบคุม",
        BorderPolicy.Closed => "ปิด",
        _ => policy.ToString(),
    };

    private static string ScenarioThai(ScenarioKind scenario) => scenario switch
    {
        ScenarioKind.Sandbox => "Sandbox",
        ScenarioKind.Survive100Years => "เอาตัวรอด 100 ปี",
        ScenarioKind.EcosystemBalance => "รักษาสมดุลระบบนิเวศ",
        ScenarioKind.StopPlague => "หยุดโรคระบาด",
        ScenarioKind.AllianceOfThree => "พันธมิตร 3 อาณาจักร",
        ScenarioKind.BuildMetropolis => "สร้างมหานคร",
        ScenarioKind.RestoreAfterDisaster => "ฟื้นฟูหลังภัยพิบัติ",
        _ => scenario.ToString(),
    };

    private static string FilterThai(ChronicleFilter filter) => filter switch
    {
        ChronicleFilter.All => "ทั้งหมด",
        ChronicleFilter.Life => "ชีวิต",
        ChronicleFilter.City => "เมือง",
        ChronicleFilter.Kingdom => "อาณาจักร",
        ChronicleFilter.War => "สงคราม",
        ChronicleFilter.Disease => "โรค",
        ChronicleFilter.Power => "พลังเทพ",
        ChronicleFilter.Event => "เหตุการณ์",
        _ => filter.ToString(),
    };

    private static int DistanceSquared(int x1, int y1, int x2, int y2)
    {
        int dx = x1 - x2;
        int dy = y1 - y2;
        return dx * dx + dy * dy;
    }
}
