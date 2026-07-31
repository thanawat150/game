using Godot;
using WorldForge.Core.Simulation;

namespace WorldForge;

public sealed partial class LivingWorldMainController : Node2D
{
    public WorldExpansionDirector? GetExpansionRuntime() => _expansion;

    public void NotifyExpansionRulesChanged()
    {
        _expansion?.EnsureWorldRecords();
        RefreshExpansionUi(force: true);
        _renderDirty = true;
    }
}
