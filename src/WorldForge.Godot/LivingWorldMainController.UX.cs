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
    private void RefreshUi()
    {
        if (_world is null || _simulation is null || _director is null)
            return;
        _worldTitle.Text = $"{_director.State.WorldName} • {_director.State.Season} • {_director.State.Weather} • {_director.State.WorldHour:00}:00";
        UpdateInspector();
        UpdateScenario();
        UpdateEventPanel();
        UpdateDebug();
        if (_simulation.State.Chronicle.Count != _lastChronicleCount)
            RebuildChronicle();
    }

    private void UpdateInspector()
    {
        if (_simulation is null || _director is null)
            return;
        if (_livingRenderer.SelectedEntityId is ulong entityId && _simulation.State.Entities.TryGetValue(entityId, out SimEntity? entity))
        {
            CitizenLifeProfile? life = _director.State.Citizens.GetValueOrDefault(entity.Id);
            string city = entity.SettlementId is ulong sid ? _simulation.State.Settlements.GetValueOrDefault(sid)?.Name ?? "ไม่มี" : "ไม่มี";
            string kingdom = entity.KingdomId is ulong kid ? _simulation.State.Kingdoms.GetValueOrDefault(kid)?.Name ?? "ไม่มี" : "ไม่มี";
            string family = $"พ่อแม่ {entity.Parents.Count} • ลูก {entity.Children.Count} • คู่ {(entity.MateId is null ? "ไม่มี" : $"#{entity.MateId}")}";
            _inspectorLabel.Text =
                $"{entity.Name} #{entity.Id}\n{entity.Species} • {entity.Sex} • อายุ {entity.AgeDays / 360f:0.0} ปี\n" +
                $"HP {entity.Health:0} • หิว {entity.Hunger:0} • พลังงาน {entity.Energy:0} • ขวัญ {entity.Morale:0}\n" +
                $"Action: {entity.Action} • อาชีพ: {life?.Job.ToString() ?? "-"} • กิจกรรม: {life?.Activity.ToString() ?? "-"}\n" +
                $"บ้าน: {city} • อาณาจักร: {kingdom}\n{family}\nTraits: {(entity.Traits.Count == 0 ? "ไม่มี" : string.Join(", ", entity.Traits))}";
            return;
        }
        if (_livingRenderer.SelectedSettlementId is ulong cityId && _simulation.State.Settlements.TryGetValue(cityId, out SettlementState? settlement))
        {
            int population = _simulation.State.Entities.Values.Count(e => e.IsAlive && e.SettlementId == cityId);
            CityManagementPolicy policy = _director.GetCityPolicy(cityId);
            int infected = _simulation.State.Diseases.Sum(d => d.InfectedDays.Keys.Count(id => _simulation.State.Entities.GetValueOrDefault(id)?.SettlementId == cityId));
            _inspectorLabel.Text =
                $"{settlement.Name} #{settlement.Id} • {settlement.Stage}\nประชากร {population}/{settlement.Housing} • ติดเชื้อ {infected}\n" +
                $"อาหาร {settlement.Food:0} • ไม้ {settlement.Wood:0} • หิน {settlement.Stone:0} • ทอง {settlement.Gold:0}\n" +
                $"ความสุข {settlement.Happiness:0} • เทคโนโลยี {settlement.Technology:0} • ป้อม {settlement.Fortification}\n" +
                $"นโยบาย {policy.Priority} • ภาษี {policy.TaxRate:P0} • พรมแดน {policy.BorderPolicy}\n" +
                $"อาคาร: {(settlement.Buildings.Count == 0 ? "ไม่มี" : string.Join(", ", settlement.Buildings))}";
            return;
        }
        if (SelectedKingdomId() is ulong kingdomId && _simulation.State.Kingdoms.TryGetValue(kingdomId, out KingdomState? kingdom))
        {
            int population = _simulation.State.Entities.Values.Count(e => e.IsAlive && e.KingdomId == kingdomId);
            KingdomManagementPolicy policy = _director.GetKingdomPolicy(kingdomId);
            _inspectorLabel.Text =
                $"{kingdom.Name} #{kingdom.Id} • {kingdom.Government}\nประชากร {population} • เมือง {kingdom.Settlements.Count}\n" +
                $"เศรษฐกิจ {kingdom.Economy:0} • กองทัพ {kingdom.ArmyStrength:0} • เสถียรภาพ {kingdom.Stability:0}\n" +
                $"พรมแดน {policy.BorderPolicy} • สันติ {policy.PreferPeace} • ความสำคัญกองทัพ {policy.MilitaryPriority:P0}\n" +
                $"เทคโนโลยี: {(kingdom.Technologies.Count == 0 ? "ไม่มี" : string.Join(", ", kingdom.Technologies))}";
            return;
        }
        _inspectorLabel.Text = "ใช้เครื่องมือ ‘ตรวจสอบ’ แล้วคลิกสิ่งมีชีวิตหรือเมือง\nคลิก Chronicle เพื่อกระโดดไปยังเหตุการณ์\nคลิก Minimap เพื่อย้ายกล้อง";
    }

    private void UpdateScenario()
    {
        if (_director is null)
            return;
        ScenarioProgress scenario = _director.State.Scenario;
        _scenarioLabel.Text = $"{scenario.Title}\n{scenario.Description}\n" +
                              (scenario.Completed ? "สถานะ: สำเร็จ" : scenario.Failed ? "สถานะ: ล้มเหลว" : "สถานะ: กำลังดำเนินการ");
        _scenarioProgress.Value = scenario.Progress * 100;
    }

    private void UpdateEventPanel()
    {
        if (_director?.State.PendingEvent is not PendingWorldEvent worldEvent)
        {
            _eventPanel.Visible = false;
            return;
        }
        _eventPanel.Visible = true;
        _eventTitle.Text = $"เหตุการณ์: {worldEvent.Title}";
        _eventDescription.Text = worldEvent.Description;
        for (int i = 0; i < _eventChoiceButtons.Length; i++)
        {
            _eventChoiceButtons[i].Visible = i < worldEvent.Choices.Count;
            if (i < worldEvent.Choices.Count) _eventChoiceButtons[i].Text = worldEvent.Choices[i];
        }
        if (_lastEventId != worldEvent.Id)
        {
            _lastEventId = (int)Math.Min(int.MaxValue, worldEvent.Id);
            PlayEventSound();
        }
    }

    private void UpdateDebug()
    {
        if (_world is null || _simulation is null || _director is null)
            return;
        int infected = _simulation.State.Diseases.Sum(d => d.InfectedDays.Count);
        _debugLabel.Text =
            $"วัน {_simulation.State.Day} • เดือน {_simulation.State.Month} • ปี {_simulation.State.Year}\n" +
            $"คน/สัตว์ {_simulation.State.Entities.Count:N0} • เมือง {_simulation.State.Settlements.Count} • อาณาจักร {_simulation.State.Kingdoms.Count}\n" +
            $"กองทัพ {_simulation.State.Armies.Values.Count(a => a.IsActive)} • โรค {_simulation.State.Diseases.Count} • ติดเชื้อ {infected}\n" +
            $"เกิด {_simulation.State.TotalBirths:N0} • อพยพ {_director.State.TotalMigrants:N0} • ยึดเมือง {_simulation.State.TotalCitiesCaptured:N0}\n" +
            $"Checksum {(_checksum.Length > 12 ? _checksum[..12] : _checksum)}";
    }

    private void UpdatePerformanceLabel()
    {
        if (_simulation is null)
            return;
        SimulationBudgetMetrics metrics = _simulation.LastBudgetMetrics;
        _performanceLabel.Text =
            $"FPS {Engine.GetFramesPerSecond()} • TPS {_measuredTps}\n" +
            $"Simulation {_lastSimulationMs:0.00} ms • เฉลี่ย {_averageSimulationMs:0.00} ms\n" +
            $"AI {metrics.AiEntitiesUpdated}/{_budget.EntityAiUpdatesPerDay} • A* {metrics.PathRequestsUsed}/{_budget.PathRequestsPerDay}\n" +
            $"วาดคน {_livingRenderer.DrawnEntities:N0} • กลุ่ม {_livingRenderer.AggregatedCells:N0} • เมือง {_livingRenderer.DrawnCities} • กองทัพ {_livingRenderer.DrawnArmies}\n" +
            $"Render {_renderHz:0.0} Hz • วัน/เฟรม {_maxDaysPerFrame} • Auto {_director?.State.Settings.AutoPerformance}";
    }

    private void UpdateScreenTint()
    {
        if (_director is null)
            return;
        float hour = _director.State.WorldHour;
        float alpha = hour switch
        {
            < 5 => 0.25f,
            < 7 => 0.25f - (hour - 5) * 0.1f,
            < 18 => 0,
            < 21 => (hour - 18) * 0.07f,
            _ => 0.22f,
        };
        _screenTint.Color = new Color(0.04f, 0.08f, 0.18f, Math.Clamp(alpha, 0, 0.3f));
    }

    private void UpdateAudio()
    {
        if (_director is null)
            return;
        if (!_director.State.Settings.EnableAudio)
        {
            _ambientAudio.SetMode(ProceduralAmbientMode.Silent, -80);
            _weatherAudio.SetMode(ProceduralAmbientMode.Silent, -80);
            return;
        }

        _ambientAudio.SetMode(IsCameraNearCity() ? ProceduralAmbientMode.Market : ProceduralAmbientMode.Forest, -20);
        ProceduralAmbientMode weatherMode = _director.State.Weather switch
        {
            WeatherKind.Rain => ProceduralAmbientMode.Rain,
            WeatherKind.Storm => ProceduralAmbientMode.Storm,
            _ => ProceduralAmbientMode.Silent,
        };
        _weatherAudio.SetMode(weatherMode, weatherMode == ProceduralAmbientMode.Storm ? -17 : -23);
    }

    private bool IsCameraNearCity()
    {
        if (_simulation is null)
            return false;
        Vector2I tile = new((int)(_camera.Position.X / _terrainRenderer.TilePixelSize), (int)(_camera.Position.Y / _terrainRenderer.TilePixelSize));
        return _simulation.State.Settlements.Values.Any(c => DistanceSquared(c.X, c.Y, tile.X, tile.Y) < 35 * 35);
    }

    private void PlayEventSound()
    {
        if (_director is not null && !_director.State.Settings.EnableAudio)
            return;
        _eventAudio.TriggerEvent();
    }

    private void SaveWorld(bool showStatus = true)
    {
        if (_world is null || _simulation is null || _director is null)
            return;
        try
        {
            _saveService.Save(WorldSavePath(CurrentSlot), _world, _clock);
            WriteAtomic(SimulationSavePath(CurrentSlot), _simulation.SaveToJson());
            WriteAtomic(LivingSavePath(CurrentSlot), _director.SaveToJson());
            WriteAtomic(MetaSavePath(CurrentSlot), JsonSerializer.Serialize(new
            {
                _director.State.WorldName,
                Day = _simulation.State.Day,
                Year = _simulation.State.Year,
                Population = _simulation.State.Entities.Count,
                SavedAt = DateTimeOffset.UtcNow,
            }, new JsonSerializerOptions { WriteIndented = true }));
            if (showStatus) _statusLabel.Text = $"บันทึกช่อง {CurrentSlot} แล้ว";
        }
        catch (Exception exception)
        {
            _statusLabel.Text = $"บันทึกไม่สำเร็จ: {exception.Message}";
        }
    }

    private void LoadWorld()
    {
        try
        {
            LoadedWorld loaded = _saveService.LoadWithRecovery(WorldSavePath(CurrentSlot));
            _world = loaded.World;
            _clock = loaded.Clock;
            _terrainEditor.ClearHistory();
            _terrainRenderer.Bind(_world);
            _checksum = loaded.Checksum;
            _simulation = File.Exists(SimulationSavePath(CurrentSlot))
                ? GrandSimulation.LoadFromJson(_world, File.ReadAllText(SimulationSavePath(CurrentSlot)))
                : new GrandSimulation(_world, _world.Config.Seed ^ 0x5A17_2026L);
            _director = File.Exists(LivingSavePath(CurrentSlot))
                ? LivingWorldDirector.LoadFromJson(_world, _simulation, File.ReadAllText(LivingSavePath(CurrentSlot)))
                : new LivingWorldDirector(_world, _simulation, _world.Config.Seed);
            _budget.MaxPopulation = _director.State.Population.GlobalPopulationLimit;
            _livingRenderer.Bind(_world, _simulation, _director);
            _miniMap.Bind(_world, _simulation, _director);
            float worldPixels = _world.Width * _terrainRenderer.TilePixelSize;
            _camera.Position = new Vector2(worldPixels / 2f, worldPixels / 2f);
            _setupLayer.Visible = false;
            _gameLayer.Visible = true;
            _tutorialLayer.Visible = false;
            SyncRuntimeControls();
            RebuildChronicle();
            _statusLabel.Text = $"โหลดช่อง {CurrentSlot}: {_director.State.WorldName}";
            _renderDirty = true;
            RefreshUi();
        }
        catch (Exception exception)
        {
            _statusLabel.Text = $"โหลดไม่สำเร็จ: {exception.Message}";
        }
    }

    private static void WriteAtomic(string path, string content)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        string temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }

    private string WorldSavePath(int slot) => ProjectSettings.GlobalizePath($"user://saves/slot_{slot}.wfg.json");
    private string SimulationSavePath(int slot) => ProjectSettings.GlobalizePath($"user://saves/slot_{slot}.sim.json");
    private string LivingSavePath(int slot) => ProjectSettings.GlobalizePath($"user://saves/slot_{slot}.living.json");
    private string MetaSavePath(int slot) => ProjectSettings.GlobalizePath($"user://saves/slot_{slot}.meta.json");

    private void TogglePause()
    {
        _clock.TogglePaused();
        _pauseButton.Text = _clock.IsPaused ? "เล่นต่อ" : "หยุดเวลา";
    }

    private void SetSpeed(double speed)
    {
        _clock.SetTimeScale(speed);
        _statusLabel.Text = $"ความเร็ว x{speed:0}";
    }

    private void ZoomCamera(float factor)
    {
        float zoom = Math.Clamp(_camera.Zoom.X * factor, 0.12f, 4f);
        _camera.Zoom = new Vector2(zoom, zoom);
        _renderDirty = true;
    }

    private Vector2I MouseTile()
    {
        Vector2 worldPosition = GetGlobalMousePosition();
        return new Vector2I(
            Mathf.FloorToInt(worldPosition.X / _terrainRenderer.TilePixelSize),
            Mathf.FloorToInt(worldPosition.Y / _terrainRenderer.TilePixelSize));
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
        _brushOverlay.SetBrush(tile, BrushRadius, SelectedTerrain, _terrainRenderer.TilePixelSize);
    }

    private Vector2 TileCenter(int x, int y) => new((x + 0.5f) * _terrainRenderer.TilePixelSize, (y + 0.5f) * _terrainRenderer.TilePixelSize);

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
