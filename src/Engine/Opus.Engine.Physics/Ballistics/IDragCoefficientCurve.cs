namespace Opus.Engine.Physics.Ballistics;

/// <summary>Returns aerodynamic drag coefficient for a Mach number.</summary>
public interface IDragCoefficientCurve
{
    float CoefficientAtMach(float mach);
}
