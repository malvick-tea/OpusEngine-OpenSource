namespace Opus.Engine.Physics.Destruction;

/// <summary>
/// Energy a moving body must deliver to defeat an upright, rooted obstacle — a tree, a post, a
/// sign — by failing the base of its standing member. A slender stake yields to a nudge; a thick
/// trunk needs a charging hull.
/// </summary>
/// <remarks>
/// The base of an upright member of diameter <c>d</c> fails when the applied bending moment
/// reaches <c>M_max = σ · Z</c>, where <c>σ</c> is the material's modulus of rupture (Pa) and
/// <c>Z = π·d³/32</c> is the section modulus of a solid circular section (m³). Carrying the base
/// to failure does work <c>M_max · φ</c>, where <c>φ</c> is the angular deflection the material
/// sustains before the base lets go. So the energy an impactor must carry is
/// <c>E = σ · (π·d³/32) · φ</c>. Every term is a physical property of the obstacle — base
/// diameter, material rupture stress, material failure angle — so resistance follows from
/// catalogue numbers with no per-object tuning constant. The cubic in <c>d</c> is what makes a
/// twice-thicker trunk eight times harder to fell.
/// </remarks>
public static class PropResistance
{
    /// <summary>Section modulus of a solid circular section of the given diameter (m³).</summary>
    public static float CircularSectionModulus(float diameterMeters)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(diameterMeters);
        return MathF.PI * diameterMeters * diameterMeters * diameterMeters / 32f;
    }

    /// <summary>
    /// Kinetic energy (J) an impactor must carry to fail the base of an upright member: the
    /// rupture moment <c>σ·Z</c> carried through the material's failure deflection <c>φ</c>.
    /// Below it the impactor only loads the member elastically and it springs back; at or above
    /// it the base lets go and the member topples (a trunk, a post) or shatters (a brittle sign).
    /// </summary>
    public static float ToppleEnergyJoules(
        float baseDiameterMeters,
        float modulusOfRupturePa,
        float failureDeflectionRadians)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseDiameterMeters);
        ArgumentOutOfRangeException.ThrowIfNegative(modulusOfRupturePa);
        ArgumentOutOfRangeException.ThrowIfNegative(failureDeflectionRadians);
        var ruptureMoment = modulusOfRupturePa * CircularSectionModulus(baseDiameterMeters);
        return ruptureMoment * failureDeflectionRadians;
    }

    /// <summary>Translational kinetic energy (J) of a body of the given mass and speed —
    /// the impactor side of the comparison, <c>½·m·v²</c>.</summary>
    public static float KineticEnergyJoules(float massKg, float speedMps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(massKg);
        return 0.5f * massKg * speedMps * speedMps;
    }
}
