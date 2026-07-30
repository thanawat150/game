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
            string kingdomName = entity.KingdomId is ulong kid ? _simulation.State.Kingdoms.GetValueOrDefault(kid)?.Name ?? "ไม่มี" : "ไม่มี";
            string family = $"พ่อแม่ {entity.Parents.Count} • ลูก {entity.Children.Count} • คู่ {(entity.MateId is null ? "ไม่มี" : $"#{entity.MateId}")}";
            _inspectorLabel.Text =
                $"{entity.Name} #{entity.Id}\n{entity.Species} • {entity.Sex} • อายุ {entity.AgeDays / 360f:0.0} ปี\n" +
                $"HP {entity.Health:0} • หิว {entity.Hunger:0} • พลังงาน {entity.Energy:0} • ขวัญ {entity.Morale:0}\n" +
                $"Action: {entity.Action} • อาชีพ: {life?.Job.ToString() ?? "-"} • กิจกรรม: {life?.Activity.ToString() ?? "-"}\n" +
                $"บ้าน: {city} • อาณาจักร: {kingdomName}\n{family}\nTraits: {(entity.Traits.Count == 0 ? "ไม่มี" : string.Join(", ", entity.Traits))}";
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
        _inspectorLabel.Text = "ใช้เครื่องมือ “ตรวจสอบ” แล้วคลิกสิ่งมีชีวิตหรือเมือง\nคลิก Chronicle เพื่อกระโดดไปยังเหตุการณ์\nคลิก Minimap เพื่อย้ายกล้อง";
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

}
