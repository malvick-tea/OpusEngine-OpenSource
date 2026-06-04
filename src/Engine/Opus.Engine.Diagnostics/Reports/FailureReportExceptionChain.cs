using System;
using System.Collections.Generic;

namespace Opus.Engine.Diagnostics.Reports;

/// <summary>
/// Breadth-first flattener over an exception and its inner / aggregate chain, with a fixed
/// depth cap and a reference-cycle guard so a pathological graph (cyclic
/// <see cref="Exception.InnerException"/>, deeply nested <see cref="AggregateException"/>)
/// cannot produce an unbounded or non-terminating walk. Shared by
/// <see cref="FailureReportExceptionInfo"/> (which builds report entries) and
/// <see cref="FailureReportClassifier"/> (which inspects types) so both observe the same
/// traversal order and bound.
/// </summary>
internal static class FailureReportExceptionChain
{
    /// <summary>Enumerates the exception graph in breadth-first order, expanding every
    /// <see cref="AggregateException.InnerExceptions"/> and otherwise following
    /// <see cref="Exception.InnerException"/>, yielding at most <paramref name="maxDepth"/>
    /// distinct exceptions.</summary>
    public static IEnumerable<Exception> Flatten(Exception? root, int maxDepth)
    {
        if (root is null || maxDepth < 1)
        {
            yield break;
        }

        var queue = new Queue<Exception>();
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        queue.Enqueue(root);
        var emitted = 0;
        while (queue.Count > 0 && emitted < maxDepth)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            yield return current;
            emitted++;

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    if (inner is not null)
                    {
                        queue.Enqueue(inner);
                    }
                }

                continue;
            }

            if (current.InnerException is not null)
            {
                queue.Enqueue(current.InnerException);
            }
        }
    }
}
