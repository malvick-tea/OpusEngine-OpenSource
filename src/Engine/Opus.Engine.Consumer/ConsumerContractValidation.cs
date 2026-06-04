using System;

namespace Opus.Engine.Consumer;

internal static class ConsumerContractValidation
{
    public static T[] CopyRequiredList<T>(IReadOnlyList<T> source, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(source, parameterName);
        if (source.Count == 0)
        {
            return Array.Empty<T>();
        }

        var copy = new T[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            var item = source[i];
            if (item is null)
            {
                throw new ArgumentException("List entries must not be null.", parameterName);
            }

            copy[i] = item;
        }

        return copy;
    }

    public static string[] CopyRequiredStringList(IReadOnlyList<string> source, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(source, parameterName);
        if (source.Count == 0)
        {
            return Array.Empty<string>();
        }

        var copy = new string[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            var item = source[i];
            if (string.IsNullOrWhiteSpace(item))
            {
                throw new ArgumentException("String entries must not be empty.", parameterName);
            }

            copy[i] = item;
        }

        return copy;
    }
}
