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
    private void BuildAudio()
    {
        _ambientAudio = new ProceduralAmbientAudio { Name = "AmbientAudio" };
        _weatherAudio = new ProceduralAmbientAudio { Name = "WeatherAudio" };
        _eventAudio = new ProceduralAmbientAudio { Name = "EventAudio" };
        AddChild(_ambientAudio);
        AddChild(_weatherAudio);
        AddChild(_eventAudio);
    }

    private void BuildGameInterface()
    {
        _gameLayer = new CanvasLayer { Name = "GameInterface", Layer = 10 };
        AddChild(_gameLayer);

        _screenTint = new ColorRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorRight = 1,
            AnchorBottom = 1,
            Color = Colors.Transparent,
        };
        _gameLayer.AddChild(_screenTint);

        var topPanel = new PanelContainer { AnchorRight = 1, OffsetBottom = 58 };
        _gameLayer.AddChild(topPanel);
        var top = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        top.AddThemeConstantOverride("separation", 6);
        topPanel.AddChild(top);

        _worldTitle = new Label { Text = "WorldForge", CustomMinimumSize = new Vector2(160, 0) };
        top.AddChild(_worldTitle);
        top.AddChild(CreateButton("โลกใหม่", ShowSetup));

        _saveSlot = new OptionButton();
        for (int i = 1; i <= 6; i++) _saveSlot.AddItem($"ช่อง {i}", i);
        _saveSlot.Select(0);
        top.AddChild(_saveSlot);
        top.AddChild(CreateButton("บันทึก [F5]", () => SaveWorld()));
        top.AddChild(CreateButton("โหลด [F9]", LoadWorld));

        _autosaveEnabled = new CheckButton { Text = "Autosave", ButtonPressed = true };
        top.AddChild(_autosaveEnabled);
        _autosaveMinutes = CreateSpin(1, 30, 1, 3, 58);
        top.AddChild(_autosaveMinutes);

        _pauseButton = CreateButton("หยุดเวลา", TogglePause);
        top.AddChild(_pauseButton);
        foreach ((string text, double speed) in new[] { ("x1", 1d), ("x2", 2d), ("x4", 4d), ("x8", 8d), ("MAX", 32d) })
            top.AddChild(CreateButton(text, () => SetSpeed(speed)));

        _searchInput = new LineEdit { PlaceholderText = "ค้นหาคน/เมือง/อาณาจักร", CustomMinimumSize = new Vector2(190, 0) };
        _searchInput.TextSubmitted += _ => SearchAndFocus();
        top.AddChild(_searchInput);
        top.AddChild(CreateButton("ค้นหา", SearchAndFocus));

        BuildLeftPanel();
        BuildRightPanel();

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
        _gameLayer.AddChild(_statusLabel);
    }
}
