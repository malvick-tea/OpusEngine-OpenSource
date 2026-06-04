namespace Opus.Engine.Consumer.Scene;

/// <summary>Engine-neutral local-light kind declared by a consumer lighting snapshot.</summary>
public enum ConsumerLocalLightKind : byte
{
    /// <summary>Omnidirectional point light.</summary>
    Point = 0,

    /// <summary>Cone-restricted spot light.</summary>
    Spot = 1,

    /// <summary>Area light source.</summary>
    Area = 2,
}
