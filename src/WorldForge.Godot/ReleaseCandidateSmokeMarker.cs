using Godot;
using WorldForge.Presentation;

namespace WorldForge;

/// <summary>
/// Headless release smoke marker. It builds every runtime atlas and verifies its dimensions
/// before CI accepts the release candidate.
/// </summary>
public sealed partial class ReleaseCandidateSmokeMarker : Node
{
    public override void _Ready()
    {
        if (!DisplayServer.GetName().Contains("headless", StringComparison.OrdinalIgnoreCase))
            return;

        CallDeferred(MethodName.RunSmoke);
    }

    private void RunSmoke()
    {
        var art = new GeneratedGameArtAtlas();
        bool valid =
            art.IconsTexture.GetWidth() == GeneratedGameArtAtlas.IconCell * 6 &&
            art.IconsTexture.GetHeight() == GeneratedGameArtAtlas.IconCell * 5 &&
            art.CharactersTexture.GetWidth() == GeneratedGameArtAtlas.CharacterCell * 3 &&
            art.CharactersTexture.GetHeight() == GeneratedGameArtAtlas.CharacterCell * 6 &&
            art.PortraitsTexture.GetWidth() == GeneratedGameArtAtlas.PortraitCell * 4 &&
            art.PortraitsTexture.GetHeight() == GeneratedGameArtAtlas.PortraitCell * 3 &&
            art.BuildingsTexture.GetWidth() == GeneratedGameArtAtlas.BuildingCell * 6 &&
            art.BuildingsTexture.GetHeight() == GeneratedGameArtAtlas.BuildingCell * 3 &&
            art.EffectsTexture.GetWidth() == GeneratedGameArtAtlas.EffectCell * 5 &&
            art.EffectsTexture.GetHeight() == GeneratedGameArtAtlas.EffectCell * 4;

        if (!valid)
            throw new InvalidOperationException("Release candidate art atlas dimensions are invalid.");

        GD.Print("RELEASE_CANDIDATE_READY icons=30 characters=18 portraits=12 buildings=18 effects=20");
    }
}
