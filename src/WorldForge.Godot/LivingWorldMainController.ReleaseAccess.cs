using Godot;
using WorldForge.Core.Simulation;
using WorldForge.Core.World;
using WorldForge.Presentation;

namespace WorldForge;

public sealed partial class LivingWorldMainController : Node2D
{
    public bool ReleaseUiReady =>
        _gameLayer is not null &&
        _setupLayer is not null &&
        _expansionLayer is not null &&
        _expansionPanel is not null &&
        _camera is not null &&
        _terrainRenderer is not null;

    public WorldMap? ReleaseWorld => _world;
    public GrandSimulation? ReleaseSimulation => _simulation;
    public LivingWorldDirector? ReleaseLiving => _director;
    public WorldExpansionDirector? ReleaseExpansion => _expansion;
    public Camera2D ReleaseCamera => _camera;
    public CanvasLayer ReleaseGameLayer => _gameLayer;
    public CanvasLayer ReleaseSetupLayer => _setupLayer;
    public CanvasLayer ReleaseExpansionLayer => _expansionLayer;
    public PanelContainer ReleaseExpansionPanel => _expansionPanel;
    public IReadOnlyList<ulong> ReleaseVisibleLegendIds => _visibleLegendIds;
    public int ReleaseSelectedLegendIndex => _selectedLegendIndex;
    public int ReleaseTilePixelSize => _terrainRenderer.TilePixelSize;
    public bool ReleaseClockPaused => _clock.IsPaused;
    public string ReleaseWorldName => _director?.State.WorldName ?? "WorldForge";

    public void ReleaseSaveWorld() => SaveWorld();
    public void ReleaseTogglePause() => TogglePause();
    public void ReleaseShowSetup() => ShowSetup();
    public void ReleaseHideSetup() => HideSetup();
}
