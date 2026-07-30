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
    private void BuildTutorial()
    {
        _tutorialLayer = new CanvasLayer { Name = "Tutorial", Layer = 80 };
        AddChild(_tutorialLayer);
        var shade = new ColorRect { AnchorRight = 1, AnchorBottom = 1, Color = new Color(0, 0, 0, 0.62f) };
        _tutorialLayer.AddChild(shade);
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        _tutorialLayer.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(560, 300) };
        center.AddChild(panel);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        panel.AddChild(root);
        root.AddChild(new Label { Text = "ยินดีต้อนรับสู่โลกที่มีชีวิต", HorizontalAlignment = HorizontalAlignment.Center });
        _tutorialText = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(520, 150) };
        root.AddChild(_tutorialText);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddChild(CreateButton("ถัดไป", NextTutorial));
        row.AddChild(CreateButton("ปิด Tutorial", CloseTutorial));
        root.AddChild(row);
        _tutorialLayer.Visible = false;
    }
}
