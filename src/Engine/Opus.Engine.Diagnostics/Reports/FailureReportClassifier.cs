using System;
using Opus.Foundation;

namespace Opus.Engine.Diagnostics.Reports;

/// <summary>
/// Maps a captured exception to a <see cref="FailureReportKind"/> by inspecting the engine
/// failure taxonomy (<see cref="EngineFailureException"/> subtypes) across the exception's
/// inner / aggregate chain. Pure and deterministic: no IO, no clock, no environment reads,
/// so it is safe to call from a crash handler.
/// </summary>
/// <remarks>
/// Priority is <see cref="FailureReportKind.DeviceLost"/> &gt;
/// <see cref="FailureReportKind.ContentFailure"/> &gt; <see cref="FailureReportKind.Crash"/>.
/// Device loss wins because it is the most severe and most actionable signal — the GPU is
/// gone — so a content error wrapped inside a device-loss teardown is still reported as a
/// device loss. An exception that carries no engine-taxonomy member is reported as a generic
/// <see cref="FailureReportKind.Crash"/>: the engine throws the typed exceptions at its own
/// content and device boundaries, so the classifier never has to guess a category from BCL
/// exception types.
/// </remarks>
public static class FailureReportClassifier
{
    /// <summary>Classifies the failure represented by <paramref name="exception"/>. Returns
    /// <see cref="FailureReportKind.Crash"/> when the exception is null or carries no
    /// recognised engine failure type anywhere in its chain.</summary>
    public static FailureReportKind Classify(Exception? exception)
    {
        var sawContentFailure = false;
        foreach (var current in FailureReportExceptionChain.Flatten(exception, FailureReportExceptionInfo.MaxChainDepth))
        {
            switch (current)
            {
                case EngineDeviceLostException:
                    return FailureReportKind.DeviceLost;
                case EngineContentException:
                    sawContentFailure = true;
                    break;
            }
        }

        return sawContentFailure ? FailureReportKind.ContentFailure : FailureReportKind.Crash;
    }
}
