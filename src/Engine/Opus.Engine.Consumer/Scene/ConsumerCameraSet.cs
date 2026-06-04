using System;

namespace Opus.Engine.Consumer.Scene;

/// <summary>Main camera plus optional auxiliary views declared by a consumer scene.</summary>
public sealed record ConsumerCameraSet
{
    /// <summary>Creates a camera set.</summary>
    public ConsumerCameraSet(ConsumerCamera main, IReadOnlyList<ConsumerCamera> auxiliary)
    {
        Main = main;
        Auxiliary = ConsumerContractValidation.CopyRequiredList(auxiliary, nameof(auxiliary));
    }

    /// <summary>Main view rendered by the host.</summary>
    public ConsumerCamera Main { get; }

    /// <summary>Auxiliary views reserved for future host adapters.</summary>
    public IReadOnlyList<ConsumerCamera> Auxiliary { get; }

    /// <summary>Creates a camera set with only the main view.</summary>
    public static ConsumerCameraSet SingleMain(ConsumerCamera main) => new(main, Array.Empty<ConsumerCamera>());
}
