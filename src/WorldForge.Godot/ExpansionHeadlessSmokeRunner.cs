using Godot;

namespace WorldForge;

public sealed partial class ExpansionHeadlessSmokeRunner : Node
{
    private bool _checked;

    public override void _Process(double delta)
    {
        if (_checked) return;
        _checked = true;
        if (!DisplayServer.GetName().Equals("headless", StringComparison.OrdinalIgnoreCase))
        {
            QueueFree();
            return;
        }

        LivingWorldMainController? controller = GetParentOrNull<LivingWorldMainController>();
        controller?.RunHeadlessExpansionSmoke();
        QueueFree();
    }
}
