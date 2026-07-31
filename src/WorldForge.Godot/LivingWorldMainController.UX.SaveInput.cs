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
    private void SaveWorld(bool showStatus = true)
    {
        if (_world is null || _simulation is null || _director is null)
            return;
        try
        {
            EnsureExpansionRuntime();
            _saveService.Save(WorldSavePath(CurrentSlot), _world, _clock);
            WriteAtomic(SimulationSavePath(CurrentSlot), _simulation.SaveToJson());
            WriteAtomic(LivingSavePath(CurrentSlot), _director.SaveToJson());
            if (_expansion is not null)
                WriteAtomic(ExpansionSavePath(CurrentSlot), _expansion.SaveToJson());
            WriteAtomic(MetaSavePath(CurrentSlot), JsonSerializer.Serialize(new
            {
                _director.State.WorldName,
                Day = _simulation.State.Day,
                Year = _simulation.State.Year,
                Population = _simulation.State.Entities.Count,
                Legends = _expansion?.State.Legends.Count ?? 0,
                Faith = _expansion?.State.Faith.Faith ?? 0,
                Fleets = _expansion?.State.Fleets.Values.Count(f => f.IsActive) ?? 0,
                Campaign = _expansion?.State.Campaign.Chapter.ToString() ?? "LivingWorld",
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
            _expansion = File.Exists(ExpansionSavePath(CurrentSlot))
                ? WorldExpansionDirector.LoadFromJson(_world, _simulation, _director, File.ReadAllText(ExpansionSavePath(CurrentSlot)))
                : new WorldExpansionDirector(_world, _simulation, _director, _world.Config.Seed ^ 0x4C4547454E44L);
            _expansionBoundSimulation = _simulation;
            _budget.MaxPopulation = _director.State.Population.GlobalPopulationLimit;
            _livingRenderer.Bind(_world, _simulation, _director);
            _expansionRenderer.Bind(_world, _simulation, _director, _expansion);
            _miniMap.Bind(_world, _simulation, _director);
            float worldPixels = _world.Width * _terrainRenderer.TilePixelSize;
            _camera.Position = new Vector2(worldPixels / 2f, worldPixels / 2f);
            _setupLayer.Visible = false;
            _gameLayer.Visible = true;
            _tutorialLayer.Visible = false;
            SyncRuntimeControls();
            RebuildChronicle();
            RefreshExpansionUi(force: true);
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
    private string ExpansionSavePath(int slot) => ProjectSettings.GlobalizePath($"user://saves/slot_{slot}.expansion.json");
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
}
