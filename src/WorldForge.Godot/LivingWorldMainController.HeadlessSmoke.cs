using Godot;
using WorldForge.Core.Simulation;

namespace WorldForge;

public sealed partial class LivingWorldMainController : Node2D
{
    public void RunHeadlessExpansionSmoke()
    {
        _setupWorldName.Text = "CI Expansion Smoke";
        _setupSeed.Text = "20260731";
        _setupWorldSize.Select(0);
        _setupKingdoms.Value = 2;
        _setupPopulationPerKingdom.Value = 8;
        _setupGrazers.Value = 12;
        _setupPredators.Value = 3;
        _setupMonsters.Value = 1;
        _setupFish.Value = 8;
        _setupPopulationCap.Value = 180;
        _setupWeather.ButtonPressed = true;
        _setupEvents.ButtonPressed = true;
        _setupAudio.ButtonPressed = false;
        _setupAutoPerformance.ButtonPressed = true;
        StartConfiguredWorld();
        EnsureExpansionRuntime();

        if (_world is null || _simulation is null || _director is null || _expansion is null)
            throw new InvalidOperationException("Expansion smoke world did not initialize.");
        if (_expansion.State.CityDistricts.Count == 0 || _expansion.State.Legends.Count == 0 || _expansion.State.Ruins.Count == 0)
            throw new InvalidOperationException("Expansion smoke state is incomplete.");

        var budget = SimulationBudgetOptions.ForProfile(SimulationPerformanceProfile.Economy, 180);
        budget.EnableAutomaticDiplomacy = false;
        budget.EnableArmies = false;
        for (int day = 0; day < 4; day++)
        {
            _simulation.AdvanceDayBudgeted(budget);
            _director.AdvanceDay();
            _expansion.AdvanceDay();
        }

        SettlementState city = _simulation.State.Settlements.Values.First();
        _expansion.State.Faith.Favor = 100;
        _expansion.UseMiracle(MiracleKind.BlessHarvest, city.Id);
        _expansion.PlanBuilding(city.Id, BuildingKind.Temple);
        _camera.Position = TileCenter(city.X, city.Y);
        _camera.Zoom = new Vector2(1.4f, 1.4f);
        UpdateExpansionCamera();
        _expansionRenderer.Refresh();
        _livingRenderer.Refresh();
        _renderDirty = true;

        GD.Print($"EXPANSION_SMOKE_OK population={_simulation.State.Entities.Count} legends={_expansion.State.Legends.Count} buildings={_expansion.State.CityDistricts.Values.Sum(d => d.Buildings.Count)} ruins={_expansion.State.Ruins.Count}");
    }
}
