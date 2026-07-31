using Godot;
using WorldForge.Core.Simulation;
using WorldForge.Presentation;

namespace WorldForge;

/// <summary>
/// Release-candidate presentation shell. It adds the generated art atlases, polished HUD,
/// legend portraits, pause menu and consistent fantasy UI without changing simulation rules.
/// </summary>
public sealed partial class ReleaseCandidateBootstrap : Node
{
    private const string ReleaseVersion = "1.0 RC";

    private LivingWorldMainController? _controller;
    private GeneratedGameArtAtlas? _art;
    private ReleaseArtOverlay? _overlay;
    private GrandSimulation? _boundSimulation;

    private PanelContainer? _statsPanel;
    private readonly Dictionary<GameIcon, Label> _statLabels = new();
    private PanelContainer? _portraitPanel;
    private TextureRect? _portrait;
    private Label? _portraitName;
    private CanvasLayer? _pauseLayer;

    private double _renderAccumulator;
    private double _uiAccumulator;
    private double _themeAccumulator;
    private bool _initialized;
    private bool _pausedByPauseMenu;
    private bool _pausedForSetup;
    private bool _lastSetupVisible;

    public override void _Ready()
    {
        _controller = GetParent() as LivingWorldMainController;
        SetProcess(true);
        SetProcessUnhandledInput(true);
    }

    public override void _Process(double delta)
    {
        if (_controller is null)
            return;

        if (!_initialized)
        {
            if (!_controller.ReleaseUiReady)
                return;
            InitializeReleasePresentation();
        }

        EnsureOverlayBinding();
        UpdateOverlayCamera();

        _overlay?.AdvanceAnimation(delta);
        _renderAccumulator += delta;
        _uiAccumulator += delta;
        _themeAccumulator += delta;

        if (_renderAccumulator >= 1.0 / 8.0)
        {
            _renderAccumulator = 0;
            _overlay?.Refresh();
        }

        if (_uiAccumulator >= 0.25)
        {
            _uiAccumulator = 0;
            RefreshHud();
            RefreshLegendPortrait();
            HandleSetupPauseTransition();
        }

        if (_themeAccumulator >= 2)
        {
            _themeAccumulator = 0;
            ApplyThemeToConnectedLayers();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_initialized || _controller is null)
            return;
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            TogglePauseMenu();
            GetViewport().SetInputAsHandled();
        }
    }

    private void InitializeReleasePresentation()
    {
        if (_controller is null)
            return;

        _art = new GeneratedGameArtAtlas();
        _overlay = new ReleaseArtOverlay
        {
            Name = "GeneratedReleaseArt",
            ZIndex = 42,
            TilePixelSize = _controller.ReleaseTilePixelSize,
        };
        _overlay.SetArt(_art);
        _controller.AddChild(_overlay);

        BuildStatsPanel();
        BuildLegendPortrait();
        BuildPauseMenu();
        AddReleaseBadge();
        ApplyThemeToConnectedLayers();

        _lastSetupVisible = _controller.ReleaseSetupLayer.Visible;
        _initialized = true;
    }

    private void BuildStatsPanel()
    {
        if (_controller is null || _art is null)
            return;

        _statsPanel = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            OffsetLeft = -255,
            OffsetRight = 255,
            OffsetTop = 61,
            OffsetBottom = 99,
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        _controller.ReleaseGameLayer.AddChild(_statsPanel);

        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", 10);
        _statsPanel.AddChild(row);

        AddStat(row, GameIcon.Population, "ประชากร");
        AddStat(row, GameIcon.Diplomacy, "อาณาจักร");
        AddStat(row, GameIcon.Food, "อาหาร");
        AddStat(row, GameIcon.Gold, "ทอง");
        AddStat(row, GameIcon.Faith, "ศรัทธา");
        AddStat(row, GameIcon.Mana, "พลังเทพ");

        var version = new Label
        {
            Text = ReleaseVersion,
            TooltipText = "Playable Release Candidate",
            VerticalAlignment = VerticalAlignment.Center,
        };
        version.AddThemeColorOverride("font_color", new Color(0.95f, 0.72f, 0.28f));
        row.AddChild(version);
    }

    private void AddStat(HBoxContainer row, GameIcon icon, string tooltip)
    {
        if (_art is null)
            return;
        var box = new HBoxContainer { TooltipText = tooltip };
        box.AddThemeConstantOverride("separation", 3);
        box.AddChild(new TextureRect
        {
            Texture = _art.Icon(icon),
            CustomMinimumSize = new Vector2(22, 22),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        var label = new Label
        {
            Text = "—",
            CustomMinimumSize = new Vector2(45, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        box.AddChild(label);
        row.AddChild(box);
        _statLabels[icon] = label;
    }

    private void BuildLegendPortrait()
    {
        if (_controller is null)
            return;
        _portraitPanel = new PanelContainer
        {
            AnchorLeft = 1,
            AnchorRight = 1,
            OffsetLeft = -760,
            OffsetRight = -628,
            OffsetTop = 108,
            OffsetBottom = 292,
            Visible = false,
        };
        _controller.ReleaseExpansionLayer.AddChild(_portraitPanel);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 4);
        _portraitPanel.AddChild(root);
        _portrait = new TextureRect
        {
            CustomMinimumSize = new Vector2(128, 128),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        root.AddChild(_portrait);
        _portraitName = new Label
        {
            Text = "ยังไม่มีตำนาน",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        root.AddChild(_portraitName);
    }

    private void BuildPauseMenu()
    {
        if (_controller is null)
            return;

        _pauseLayer = new CanvasLayer { Name = "ReleasePauseMenu", Layer = 90, Visible = false };
        _controller.AddChild(_pauseLayer);

        _pauseLayer.AddChild(new ColorRect
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            Color = new Color(0.01f, 0.015f, 0.03f, 0.8f),
        });
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        _pauseLayer.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(420, 360) };
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);
        var root = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var title = new Label { Text = "WORLD FORGE", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", new Color(0.96f, 0.74f, 0.3f));
        root.AddChild(title);
        root.AddChild(new Label { Text = "พักเกม", HorizontalAlignment = HorizontalAlignment.Center });
        root.AddChild(ActionButton("เล่นต่อ", ClosePauseMenu));
        root.AddChild(ActionButton("บันทึกเกม [F5]", () => _controller.ReleaseSaveWorld()));
        root.AddChild(ActionButton("กลับหน้าสร้างโลก", ReturnToSetup));
        root.AddChild(ActionButton("ออกจากเกม", () => GetTree().Quit()));
        root.AddChild(new Label
        {
            Text = $"WorldForge: Pixel Gods • {ReleaseVersion}",
            HorizontalAlignment = HorizontalAlignment.Center,
        });
    }

    private void AddReleaseBadge()
    {
        if (_controller is null)
            return;
        var version = new Label
        {
            Text = $"WorldForge: Pixel Gods • {ReleaseVersion}",
            AnchorLeft = 1,
            AnchorRight = 1,
            OffsetLeft = -260,
            OffsetRight = -14,
            OffsetTop = 12,
            OffsetBottom = 34,
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        version.AddThemeColorOverride("font_color", new Color(0.94f, 0.72f, 0.3f));
        _controller.ReleaseSetupLayer.AddChild(version);
    }

    private Button ActionButton(string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        DecorateButton(button);
        return button;
    }

    private void EnsureOverlayBinding()
    {
        if (_controller is null || _overlay is null)
            return;
        GrandSimulation? simulation = _controller.ReleaseSimulation;
        if (simulation is null ||
            _controller.ReleaseWorld is null ||
            _controller.ReleaseLiving is null ||
            _controller.ReleaseExpansion is null)
            return;

        if (!ReferenceEquals(_boundSimulation, simulation))
        {
            _overlay.Bind(
                _controller.ReleaseWorld,
                simulation,
                _controller.ReleaseLiving,
                _controller.ReleaseExpansion);
            _boundSimulation = simulation;
        }
    }

    private void UpdateOverlayCamera()
    {
        if (_controller is null || _overlay is null)
            return;
        _overlay.CameraPosition = _controller.ReleaseCamera.Position;
        _overlay.CameraZoom = _controller.ReleaseCamera.Zoom;
        _overlay.ViewportSize = GetViewport().GetVisibleRect().Size;
        _overlay.TilePixelSize = _controller.ReleaseTilePixelSize;
    }

    private void RefreshHud()
    {
        if (_controller?.ReleaseSimulation is not GrandSimulation simulation)
            return;

        int population = simulation.State.Entities.Values.Count(e => e.IsAlive && e.Species == SpeciesKind.Settler);
        int kingdoms = simulation.State.Kingdoms.Count;
        float food = simulation.State.Settlements.Values.Sum(c => c.Food);
        float gold = simulation.State.Settlements.Values.Sum(c => c.Gold);
        float faith = _controller.ReleaseExpansion?.State.Faith.Faith ?? 0;
        float favor = _controller.ReleaseExpansion?.State.Faith.Favor ?? 0;

        SetStat(GameIcon.Population, population.ToString("N0"));
        SetStat(GameIcon.Diplomacy, kingdoms.ToString("N0"));
        SetStat(GameIcon.Food, CompactNumber(food));
        SetStat(GameIcon.Gold, CompactNumber(gold));
        SetStat(GameIcon.Faith, CompactNumber(faith));
        SetStat(GameIcon.Mana, CompactNumber(favor));
    }

    private void RefreshLegendPortrait()
    {
        if (_controller is null || _art is null || _portraitPanel is null || _portrait is null || _portraitName is null)
            return;

        IReadOnlyList<ulong> ids = _controller.ReleaseVisibleLegendIds;
        bool show = _controller.ReleaseExpansionPanel.Visible &&
                    _controller.ReleaseExpansion is not null &&
                    ids.Count > 0;
        _portraitPanel.Visible = show;
        if (!show || _controller.ReleaseExpansion is not WorldExpansionDirector expansion)
            return;

        int index = Math.Clamp(_controller.ReleaseSelectedLegendIndex, 0, ids.Count - 1);
        if (!expansion.State.Legends.TryGetValue(ids[index], out LegendProfile? legend))
            return;

        _portrait.Texture = _art.Portrait(legend.Race, legend.Role);
        _portraitName.Text = $"{expansion.DisplayLegendName(legend)}\n{legend.Role} • Fame {legend.Fame}";
    }

    private void SetStat(GameIcon icon, string value)
    {
        if (_statLabels.TryGetValue(icon, out Label? label))
            label.Text = value;
    }

    private static string CompactNumber(double value)
    {
        if (value >= 1_000_000) return $"{value / 1_000_000:0.0}M";
        if (value >= 1_000) return $"{value / 1_000:0.0}K";
        return $"{value:0}";
    }

    private void TogglePauseMenu()
    {
        if (_controller is null || _pauseLayer is null)
            return;

        if (_controller.ReleaseSetupLayer.Visible)
        {
            if (_controller.ReleaseWorld is not null)
                _controller.ReleaseHideSetup();
            return;
        }

        if (_pauseLayer.Visible)
            ClosePauseMenu();
        else
            OpenPauseMenu();
    }

    private void OpenPauseMenu()
    {
        if (_controller is null || _pauseLayer is null)
            return;
        _pauseLayer.Visible = true;
        _pausedByPauseMenu = !_controller.ReleaseClockPaused;
        if (_pausedByPauseMenu)
            _controller.ReleaseTogglePause();
    }

    private void ClosePauseMenu()
    {
        if (_controller is null || _pauseLayer is null)
            return;
        _pauseLayer.Visible = false;
        if (_pausedByPauseMenu && _controller.ReleaseClockPaused)
            _controller.ReleaseTogglePause();
        _pausedByPauseMenu = false;
    }

    private void ReturnToSetup()
    {
        if (_controller is null || _pauseLayer is null)
            return;
        _pauseLayer.Visible = false;
        _pausedByPauseMenu = false;
        if (!_controller.ReleaseClockPaused)
            _controller.ReleaseTogglePause();
        _pausedForSetup = true;
        _controller.ReleaseShowSetup();
    }

    private void HandleSetupPauseTransition()
    {
        if (_controller is null)
            return;
        bool visible = _controller.ReleaseSetupLayer.Visible;
        if (_pausedForSetup && _lastSetupVisible && !visible)
        {
            if (_controller.ReleaseClockPaused)
                _controller.ReleaseTogglePause();
            _pausedForSetup = false;
        }
        _lastSetupVisible = visible;
    }

    private void ApplyThemeToConnectedLayers()
    {
        if (_controller is null)
            return;
        ApplyThemeRecursive(_controller.ReleaseGameLayer);
        ApplyThemeRecursive(_controller.ReleaseSetupLayer);
        ApplyThemeRecursive(_controller.ReleaseExpansionLayer);
        if (_pauseLayer is not null)
            ApplyThemeRecursive(_pauseLayer);
    }

    private void ApplyThemeRecursive(Node node)
    {
        if (!node.HasMeta("release_styled"))
        {
            if (node is PanelContainer panel)
            {
                panel.AddThemeStyleboxOverride(
                    "panel",
                    PanelStyle(new Color(0.035f, 0.055f, 0.09f, 0.95f), new Color(0.62f, 0.42f, 0.17f, 0.9f), 1));
            }
            else if (node is Button button)
            {
                DecorateButton(button);
                button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color(0.07f, 0.095f, 0.14f, 0.96f), new Color(0.45f, 0.32f, 0.16f)));
                button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color(0.12f, 0.16f, 0.23f, 0.98f), new Color(0.9f, 0.65f, 0.24f)));
                button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color(0.18f, 0.13f, 0.07f, 0.98f), new Color(1f, 0.76f, 0.3f)));
                button.AddThemeColorOverride("font_color", new Color(0.93f, 0.91f, 0.84f));
                button.AddThemeColorOverride("font_hover_color", Colors.White);
            }
            else if (node is Label label)
            {
                label.AddThemeColorOverride("font_color", new Color(0.88f, 0.89f, 0.9f));
                if (label.Text.Contains('•') || IsUppercaseHeading(label.Text))
                    label.AddThemeColorOverride("font_color", new Color(0.94f, 0.72f, 0.3f));
            }
            node.SetMeta("release_styled", true);
        }

        foreach (Node child in node.GetChildren())
            ApplyThemeRecursive(child);
    }

    private void DecorateButton(Button button)
    {
        if (_art is null)
            return;
        button.Icon = _art.Icon(ButtonIcon(button.Text));
        button.ExpandIcon = true;
        button.TooltipText = string.IsNullOrWhiteSpace(button.TooltipText) ? button.Text : button.TooltipText;
    }

    private static bool IsUppercaseHeading(string text)
    {
        bool foundLetter = false;
        foreach (char character in text)
        {
            if (!char.IsLetter(character))
                continue;
            foundLetter = true;
            if (char.IsLower(character))
                return false;
        }
        return foundLetter;
    }

    private static GameIcon ButtonIcon(string text)
    {
        string value = text.ToLowerInvariant();
        if (value.Contains("บันทึก") || value.Contains("export")) return GameIcon.Chronicle;
        if (value.Contains("โหลด") || value.Contains("import")) return GameIcon.Relic;
        if (value.Contains("โลกใหม่") || value.Contains("สร้างโลก")) return GameIcon.Forest;
        if (value.Contains("เล่นต่อ") || value.Contains("เริ่มเล่น")) return GameIcon.Happiness;
        if (value.Contains("หยุด") || value.Contains("ออกจากเกม") || value.Contains("ปิด")) return GameIcon.Settings;
        if (value.Contains("ตำนาน") || value.Contains("พงศาวดาร")) return GameIcon.Chronicle;
        if (value.Contains("ศรัทธา") || value.Contains("ปาฏิหาริย์")) return GameIcon.Faith;
        if (value.Contains("ค้นหา") || value.Contains("สำรวจ")) return GameIcon.Ruins;
        if (value.Contains("อาคาร") || value.Contains("ก่อสร้าง") || value.Contains("ซ่อม")) return GameIcon.Tools;
        if (value.Contains("รักษา")) return GameIcon.Health;
        if (value.Contains("เทศกาล")) return GameIcon.Happiness;
        if (value.Contains("กองเรือ")) return GameIcon.Fleet;
        if (value.Contains("เวท") || value.Contains("mod")) return GameIcon.Magic;
        if (value.Contains("อนุสาวรีย์")) return GameIcon.Relic;
        if (value.Contains("ย้อนกลับ") || value.Contains("ยกเลิก")) return GameIcon.Settings;
        if (value.Contains("จำลอง")) return GameIcon.Chronicle;
        return GameIcon.Settings;
    }

    private static StyleBoxFlat PanelStyle(Color background, Color border, int width)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = width,
            BorderWidthTop = width,
            BorderWidthRight = width,
            BorderWidthBottom = width,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        };
    }

    private static StyleBoxFlat ButtonStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        };
    }
}
