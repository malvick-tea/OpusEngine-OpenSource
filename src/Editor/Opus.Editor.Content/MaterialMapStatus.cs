namespace Opus.Editor.Content;

/// <summary>
/// One authored map's presence in a material set: which map it is, the path it is expected at (relative to
/// the textures root, for display), and whether the file is present. An absent map is surfaced — not as an
/// error, but so the author knows which channel the runtime will render with a neutral fallback.
/// </summary>
/// <param name="Kind">The authored map this status describes.</param>
/// <param name="RelativePath">The map's expected path relative to the textures root.</param>
/// <param name="Present">True when the file exists on disk.</param>
public sealed record MaterialMapStatus(MaterialMapKind Kind, string RelativePath, bool Present);
