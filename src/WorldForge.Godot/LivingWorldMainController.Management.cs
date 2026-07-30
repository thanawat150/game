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
    private void ApplyRuntimePerformanceControls()
    {
        _budget.MaxPopulation = (int)_populationCap.Value;
        _budget.EntityAiUpdatesPerDay = (int)_aiBudget.Value;
        _budget.PathRequestsPerDay = (int)_pathBudget.Value;
        _renderHz = _renderHzControl.Value;
        _maxDaysPerFrame = (int)_maxDaysPerFrameControl.Value;
        if (_director is not null)
            _director.State.Population.GlobalPopulationLimit = _budget.MaxPopulation;
    }

    private void ApplyPerformanceProfile(SimulationPerformanceProfile profile)
    {
        if (profile == SimulationPerformanceProfile.Custom)
            return;
        int cap = (int)_populationCap.Value;
        bool reproduction = _budget.EnableReproduction;
        bool diplomacy = _budget.EnableAutomaticDiplomacy;
        bool armies = _budget.EnableArmies;
        _budget = SimulationBudgetOptions.ForProfile(profile, cap);
        _budget.EnableReproduction = reproduction;
        _budget.EnableAutomaticDiplomacy = diplomacy;
        _budget.EnableArmies = armies;
        _renderHz = profile switch { SimulationPerformanceProfile.Economy => 4, SimulationPerformanceProfile.Detailed => 12, _ => 7 };
        _maxDaysPerFrame = profile == SimulationPerformanceProfile.Economy ? 1 : profile == SimulationPerformanceProfile.Detailed ? 3 : 2;
        SyncRuntimeControls();
    }

    private void ApplyAutoPerformance()
    {
        if (_director is null || !_director.State.Settings.AutoPerformance)
            return;
        double fps = Engine.GetFramesPerSecond();
        if (fps < 28)
        {
            _renderHz = Math.Max(2, _renderHz - 1);
            _budget.EntityAiUpdatesPerDay = Math.Max(30, (int)(_budget.EntityAiUpdatesPerDay * 0.82));
            _budget.PathRequestsPerDay = Math.Max(2, _budget.PathRequestsPerDay - 2);
            _maxDaysPerFrame = 1;
        }
        else if (fps < 42)
        {
            _renderHz = Math.Max(3, _renderHz - 0.5);
            _budget.EntityAiUpdatesPerDay = Math.Max(45, (int)(_budget.EntityAiUpdatesPerDay * 0.92));
            _budget.PathRequestsPerDay = Math.Max(3, _budget.PathRequestsPerDay - 1);
        }
        else if (fps > 57 && _averageSimulationMs < 8)
        {
            SimulationPerformanceProfile profile = (SimulationPerformanceProfile)_performanceProfile.GetSelectedId();
            SimulationBudgetOptions target = SimulationBudgetOptions.ForProfile(profile == SimulationPerformanceProfile.Custom ? SimulationPerformanceProfile.Balanced : profile, _budget.MaxPopulation);
            _renderHz = Math.Min(profile == SimulationPerformanceProfile.Detailed ? 12 : profile == SimulationPerformanceProfile.Economy ? 5 : 8, _renderHz + 0.5);
            _budget.EntityAiUpdatesPerDay = Math.Min(target.EntityAiUpdatesPerDay, _budget.EntityAiUpdatesPerDay + 10);
            _budget.PathRequestsPerDay = Math.Min(target.PathRequestsPerDay, _budget.PathRequestsPerDay + 1);
        }
        SyncRuntimeControls();
    }

    private void ApplyPopulationControls()
    {
        if (_director is null)
            return;
        _director.State.Population.GlobalPopulationLimit = (int)_populationCap.Value;
        _director.SetSpeciesCap(SpeciesKind.Settler, (int)_settlerCap.Value);
        _director.SetSpeciesCap(SpeciesKind.Grazer, (int)_grazerCap.Value);
        _director.SetSpeciesCap(SpeciesKind.Predator, (int)_predatorCap.Value);
        _director.SetSpeciesCap(SpeciesKind.Monster, (int)_monsterCap.Value);
        _director.SetSpeciesCap(SpeciesKind.Fish, (int)_fishCap.Value);
        _director.State.Population.BirthMultiplier = (float)_birthMultiplier.Value;
        _director.State.Population.MigrationMultiplier = (float)_migrationMultiplier.Value;
        int baseChecks = _budget.Profile switch
        {
            SimulationPerformanceProfile.Economy => 8,
            SimulationPerformanceProfile.Detailed => 36,
            _ => 18,
        };
        _budget.ReproductionChecksPerCycle = Math.Clamp((int)Math.Round(baseChecks * _birthMultiplier.Value), 0, 500);
    }

    private void SyncRuntimeControls()
    {
        _populationCap.SetValueNoSignal(_budget.MaxPopulation);
        _aiBudget.SetValueNoSignal(_budget.EntityAiUpdatesPerDay);
        _pathBudget.SetValueNoSignal(_budget.PathRequestsPerDay);
        _renderHzControl.SetValueNoSignal(_renderHz);
        _maxDaysPerFrameControl.SetValueNoSignal(_maxDaysPerFrame);
        if (_director is null) return;
        _autoPerformance.SetPressedNoSignal(_director.State.Settings.AutoPerformance);
        _settlerCap.SetValueNoSignal(_director.State.Population.SpeciesCaps.GetValueOrDefault(SpeciesKind.Settler));
        _grazerCap.SetValueNoSignal(_director.State.Population.SpeciesCaps.GetValueOrDefault(SpeciesKind.Grazer));
        _predatorCap.SetValueNoSignal(_director.State.Population.SpeciesCaps.GetValueOrDefault(SpeciesKind.Predator));
        _monsterCap.SetValueNoSignal(_director.State.Population.SpeciesCaps.GetValueOrDefault(SpeciesKind.Monster));
        _fishCap.SetValueNoSignal(_director.State.Population.SpeciesCaps.GetValueOrDefault(SpeciesKind.Fish));
        _birthMultiplier.SetValueNoSignal(_director.State.Population.BirthMultiplier);
        _migrationMultiplier.SetValueNoSignal(_director.State.Population.MigrationMultiplier);
        _scenarioSelector.Select(_scenarioSelector.GetItemIndex((int)_director.State.Scenario.Scenario));
    }

    private void LoadManagementControlsFromSelection()
    {
        if (_director is null)
            return;
        if (_livingRenderer.SelectedSettlementId is ulong cityId)
        {
            CityManagementPolicy policy = _director.GetCityPolicy(cityId);
            _cityPriority.Select(_cityPriority.GetItemIndex((int)policy.Priority));
            _cityBorder.Select(_cityBorder.GetItemIndex((int)policy.BorderPolicy));
            _cityTax.SetValueNoSignal(policy.TaxRate);
            _cityBirth.SetValueNoSignal(policy.BirthPolicyMultiplier);
            _cityFoodReserve.SetValueNoSignal(policy.FoodReserveTarget);
            _cityPopulationLimit.SetValueNoSignal(policy.PopulationLimit);
            _cityAutoBuild.SetPressedNoSignal(policy.AutoBuild);
            _cityQuarantine.SetPressedNoSignal(policy.Quarantine);
            _cityEvacuate.SetPressedNoSignal(policy.Evacuate);
        }
        ulong? kingdomId = SelectedKingdomId();
        if (kingdomId is ulong kid)
        {
            KingdomManagementPolicy policy = _director.GetKingdomPolicy(kid);
            _kingdomBorder.Select(_kingdomBorder.GetItemIndex((int)policy.BorderPolicy));
            _kingdomTax.SetValueNoSignal(policy.TaxModifier);
            _kingdomBirth.SetValueNoSignal(policy.BirthPolicyMultiplier);
            _kingdomMilitary.SetValueNoSignal(policy.MilitaryPriority);
            _kingdomPopulationLimit.SetValueNoSignal(policy.PopulationLimit);
            _kingdomPreferPeace.SetPressedNoSignal(policy.PreferPeace);
        }
    }

    private void ApplySelectedCityPolicy()
    {
        if (_director is null || _livingRenderer.SelectedSettlementId is not ulong cityId)
            return;
        CityManagementPolicy policy = _director.GetCityPolicy(cityId);
        policy.Priority = (CityPriority)_cityPriority.GetSelectedId();
        policy.BorderPolicy = (BorderPolicy)_cityBorder.GetSelectedId();
        policy.TaxRate = (float)_cityTax.Value;
        policy.BirthPolicyMultiplier = (float)_cityBirth.Value;
        policy.FoodReserveTarget = (float)_cityFoodReserve.Value;
        policy.PopulationLimit = (int)_cityPopulationLimit.Value;
        policy.AutoBuild = _cityAutoBuild.ButtonPressed;
        policy.Quarantine = _cityQuarantine.ButtonPressed;
        policy.Evacuate = _cityEvacuate.ButtonPressed;
    }

    private void ApplySelectedKingdomPolicy()
    {
        if (_director is null || SelectedKingdomId() is not ulong kingdomId)
            return;
        KingdomManagementPolicy policy = _director.GetKingdomPolicy(kingdomId);
        policy.BorderPolicy = (BorderPolicy)_kingdomBorder.GetSelectedId();
        policy.TaxModifier = (float)_kingdomTax.Value;
        policy.BirthPolicyMultiplier = (float)_kingdomBirth.Value;
        policy.MilitaryPriority = (float)_kingdomMilitary.Value;
        policy.PopulationLimit = (int)_kingdomPopulationLimit.Value;
        policy.PreferPeace = _kingdomPreferPeace.ButtonPressed;
        _director.SetKingdomCap(kingdomId, policy.PopulationLimit);
    }

    private void BuildSelectedCity()
    {
        if (_director is null || _livingRenderer.SelectedSettlementId is not ulong cityId) return;
        _director.BuildNow(cityId);
        _renderDirty = true;
    }

    private void FestivalSelectedCity()
    {
        if (_director is null || _livingRenderer.SelectedSettlementId is not ulong cityId) return;
        _director.TriggerFestival(cityId);
        PlayEventSound();
        _renderDirty = true;
    }

    private void HealSelectedCity()
    {
        if (_director is null || _livingRenderer.SelectedSettlementId is not ulong cityId) return;
        _director.HealCity(cityId, 35);
        _renderDirty = true;
    }

    private ulong? SelectedKingdomId()
    {
        if (_livingRenderer.SelectedKingdomId is ulong selected) return selected;
        if (_livingRenderer.SelectedSettlementId is ulong cityId && _simulation?.State.Settlements.GetValueOrDefault(cityId)?.KingdomId is ulong cityKingdom) return cityKingdom;
        if (_livingRenderer.SelectedEntityId is ulong entityId && _simulation?.State.Entities.GetValueOrDefault(entityId)?.KingdomId is ulong entityKingdom) return entityKingdom;
        return null;
    }

    private void RenameSelected()
    {
        if (_director is null || string.IsNullOrWhiteSpace(_renameInput.Text))
            return;
        bool changed = false;
        if (_livingRenderer.SelectedSettlementId is ulong cityId)
            changed = _director.RenameSettlement(cityId, _renameInput.Text);
        else if (SelectedKingdomId() is ulong kingdomId)
            changed = _director.RenameKingdom(kingdomId, _renameInput.Text);
        else
            _director.RenameWorld(_renameInput.Text);
        _renameInput.Clear();
        _statusLabel.Text = changed ? "เปลี่ยนชื่อแล้ว" : "เปลี่ยนชื่อโลกแล้ว";
        RefreshUi();
    }

    private void ResolveEvent(int choice)
    {
        if (_director?.State.PendingEvent is null)
            return;
        _camera.Position = TileCenter(_director.State.PendingEvent.X, _director.State.PendingEvent.Y);
        _director.ResolvePendingEvent(choice);
        PlayEventSound();
        _eventPanel.Visible = false;
        _renderDirty = true;
        RefreshUi();
    }

    private void SearchAndFocus()
    {
        if (_simulation is null)
            return;
        string query = _searchInput.Text.Trim();
        if (query.Length == 0)
            return;
        SimEntity? entity = _simulation.State.Entities.Values.FirstOrDefault(e => e.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (entity is not null)
        {
            _camera.Position = TileCenter(entity.X, entity.Y);
            _livingRenderer.SelectEntity(entity.Id);
            LoadManagementControlsFromSelection();
            return;
        }
        SettlementState? city = _simulation.State.Settlements.Values.FirstOrDefault(e => e.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (city is not null)
        {
            _camera.Position = TileCenter(city.X, city.Y);
            _livingRenderer.SelectSettlement(city.Id);
            LoadManagementControlsFromSelection();
            return;
        }
        KingdomState? kingdom = _simulation.State.Kingdoms.Values.FirstOrDefault(e => e.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (kingdom is not null && _simulation.State.Settlements.TryGetValue(kingdom.CapitalId, out SettlementState? capital))
        {
            _camera.Position = TileCenter(capital.X, capital.Y);
            _livingRenderer.SelectKingdom(kingdom.Id);
            LoadManagementControlsFromSelection();
            return;
        }
        _statusLabel.Text = $"ไม่พบ “{query}”";
    }

    private void RebuildChronicle()
    {
        if (_simulation is null)
            return;
        _chronicleList.Clear();
        _visibleChronicle.Clear();
        ChronicleFilter filter = (ChronicleFilter)_chronicleFilter.GetSelectedId();
        string query = _chronicleSearch.Text.Trim();
        foreach (ChronicleEvent item in _simulation.State.Chronicle.AsEnumerable().Reverse().Where(e => MatchesFilter(e, filter)).Where(e => query.Length == 0 || e.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || e.Description.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(150))
        {
            _visibleChronicle.Add(item);
            _chronicleList.AddItem($"D{item.Tick} • {item.Title}\n{item.Description}");
        }
        _lastChronicleCount = _simulation.State.Chronicle.Count;
    }

    private static bool MatchesFilter(ChronicleEvent e, ChronicleFilter filter) => filter switch
    {
        ChronicleFilter.All => true,
        ChronicleFilter.Life => e.Type.Contains("entity") || e.Type.Contains("family"),
        ChronicleFilter.City => e.Type.Contains("settlement") || e.Type.Contains("city"),
        ChronicleFilter.Kingdom => e.Type.Contains("kingdom") || e.Type.Contains("technology"),
        ChronicleFilter.War => e.Type.Contains("war") || e.Type.Contains("army") || e.Type.Contains("battle") || e.Type.Contains("siege"),
        ChronicleFilter.Disease => e.Type.Contains("disease") || e.Type.Contains("plague"),
        ChronicleFilter.Power => e.Type.Contains("power"),
        ChronicleFilter.Event => e.Type.Contains("event"),
        _ => true,
    };

    private void FocusChronicle(int index)
    {
        if (index < 0 || index >= _visibleChronicle.Count)
            return;
        ChronicleEvent item = _visibleChronicle[index];
        _camera.Position = TileCenter(item.X, item.Y);
        _statusLabel.Text = $"ไปยังเหตุการณ์: {item.Title}";
    }

    private void UpdateCameraAwareRendering()
    {
        Vector2 viewportSize = GetViewportRect().Size;
        _livingRenderer.CameraPosition = _camera.Position;
        _livingRenderer.CameraZoom = _camera.Zoom;
        _livingRenderer.ViewportSize = viewportSize;
        _miniMap.CameraWorldPosition = _camera.Position;
        _miniMap.CameraWorldSize = viewportSize / Math.Max(0.05f, _camera.Zoom.X);
    }
}
