using System;
using System.Collections.Generic;

namespace Opus.Engine.Diagnostics.Reports;

/// <summary>
/// Flattened exception entry captured in a failure report. The flattener walks
/// <see cref="Exception.InnerException"/> and expands every
/// <see cref="AggregateException.InnerExceptions"/> chain, with a fixed depth cap to
/// guarantee bounded report size even against pathological exception graphs.
/// </summary>
public sealed record FailureReportExceptionInfo(
    string Type,
    string Message,
    string? StackTrace)
{
    /// <summary>Maximum number of entries kept in a single failure-report exception
    /// chain. Pathological inputs (cyclic InnerException references, deeply nested
    /// AggregateException trees) cannot grow the report beyond this cap.</summary>
    public const int MaxChainDepth = 32;

    /// <summary>Flattens an exception and its inner chain into report entries. Traversal
    /// order and depth bound are shared with <see cref="FailureReportClassifier"/> via
    /// <see cref="FailureReportExceptionChain"/>.</summary>
    public static IReadOnlyList<FailureReportExceptionInfo> From(Exception? exception)
    {
        List<FailureReportExceptionInfo>? entries = null;
        foreach (var current in FailureReportExceptionChain.Flatten(exception, MaxChainDepth))
        {
            entries ??= new List<FailureReportExceptionInfo>();
            entries.Add(new FailureReportExceptionInfo(
                current.GetType().FullName ?? current.GetType().Name,
                current.Message,
                current.StackTrace));
        }

        return entries ?? (IReadOnlyList<FailureReportExceptionInfo>)Array.Empty<FailureReportExceptionInfo>();
    }
}
