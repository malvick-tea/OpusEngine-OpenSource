namespace Opus.Engine.Physics.Ground;

/// <summary>
/// Physical configuration of a powered ground vehicle. The model is agnostic to the
/// caller's domain: tracked carriers, wheeled vehicles, and differential robots can
/// all describe their driven contact patch through the same force budget.
/// </summary>
public sealed record GroundVehicleProperties
{
    public GroundVehicleProperties(
        float massKg,
        float frontalAreaSquareMeters,
        float aerodynamicDragCoefficient,
        float rollingResistanceCoefficient,
        float tractionScale,
        float lateralGripScale,
        float maximumBrakeForceNewtons,
        float yawInertiaKgSquareMeters,
        float maximumSteeringTorqueNewtonMeters,
        float angularDampingNewtonMeterSeconds,
        PowertrainProperties powertrain,
        float engineBrakingCoefficientNsPerM = 0f,
        float turningResistanceCoefficientSeconds = 0f,
        float terrainSlopeSampleDistanceMeters = 2f)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(massKg);
        ArgumentOutOfRangeException.ThrowIfNegative(frontalAreaSquareMeters);
        ArgumentOutOfRangeException.ThrowIfNegative(aerodynamicDragCoefficient);
        ArgumentOutOfRangeException.ThrowIfNegative(rollingResistanceCoefficient);
        ArgumentOutOfRangeException.ThrowIfNegative(tractionScale);
        ArgumentOutOfRangeException.ThrowIfNegative(lateralGripScale);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBrakeForceNewtons);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(yawInertiaKgSquareMeters);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumSteeringTorqueNewtonMeters);
        ArgumentOutOfRangeException.ThrowIfNegative(angularDampingNewtonMeterSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(engineBrakingCoefficientNsPerM);
        ArgumentOutOfRangeException.ThrowIfNegative(turningResistanceCoefficientSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(terrainSlopeSampleDistanceMeters);
        ArgumentNullException.ThrowIfNull(powertrain);

        MassKg = massKg;
        FrontalAreaSquareMeters = frontalAreaSquareMeters;
        AerodynamicDragCoefficient = aerodynamicDragCoefficient;
        RollingResistanceCoefficient = rollingResistanceCoefficient;
        TractionScale = tractionScale;
        LateralGripScale = lateralGripScale;
        MaximumBrakeForceNewtons = maximumBrakeForceNewtons;
        YawInertiaKgSquareMeters = yawInertiaKgSquareMeters;
        MaximumSteeringTorqueNewtonMeters = maximumSteeringTorqueNewtonMeters;
        AngularDampingNewtonMeterSeconds = angularDampingNewtonMeterSeconds;
        Powertrain = powertrain;
        EngineBrakingCoefficientNsPerM = engineBrakingCoefficientNsPerM;
        TurningResistanceCoefficientSeconds = turningResistanceCoefficientSeconds;
        TerrainSlopeSampleDistanceMeters = terrainSlopeSampleDistanceMeters;
    }

    public float MassKg { get; }

    public float FrontalAreaSquareMeters { get; }

    public float AerodynamicDragCoefficient { get; }

    public float RollingResistanceCoefficient { get; }

    public float TractionScale { get; }

    public float LateralGripScale { get; }

    public float MaximumBrakeForceNewtons { get; }

    public float YawInertiaKgSquareMeters { get; }

    public float MaximumSteeringTorqueNewtonMeters { get; }

    public float AngularDampingNewtonMeterSeconds { get; }

    public PowertrainProperties Powertrain { get; }

    /// <summary>Off-throttle drivetrain drag, in newton-seconds per metre: the resistive force
    /// per unit forward speed the engine + transmission + tracks impose when the driver lifts
    /// off the throttle (engine braking). Zero (the default) models a free-wheeling coast; a
    /// tracked vehicle wants a large value so it sheds speed promptly instead of gliding. The
    /// force is viscous (proportional to forward speed) so it fades smoothly toward a stop,
    /// where the constant rolling resistance takes over.</summary>
    public float EngineBrakingCoefficientNsPerM { get; }

    /// <summary>Skid-steer scrub coefficient, in seconds: the fraction of normal force spent
    /// resisting planar travel per radian/second of hull rotation. Zero (the default) models a
    /// contact patch with no additional turning loss; tracked vehicles want a positive value
    /// because their tracks must scrub over the ground while the hull yaws.</summary>
    public float TurningResistanceCoefficientSeconds { get; }

    /// <summary>Half-spacing, in metres, used to sample terrain height around the contact
    /// patch. Callers choose a value that matches the vehicle footprint so a short robot,
    /// a wheeled car, and a long tracked carrier do not bridge the same terrain scale.</summary>
    public float TerrainSlopeSampleDistanceMeters { get; }
}
