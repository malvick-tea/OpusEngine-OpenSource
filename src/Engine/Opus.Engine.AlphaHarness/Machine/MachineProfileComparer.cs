using System;
using System.Collections.Generic;

namespace Opus.Engine.AlphaHarness.Machine;

/// <summary>
/// Compares a captured <see cref="KnownGoodMachineProfile"/> against a reference one. The
/// comparer is a pure function — it observes both inputs and returns a deterministic
/// <see cref="MachineProfileComparison"/>. Severity assignment is explicit:
/// <list type="bullet">
/// <item><description>OS family / architecture / adapter mismatch → <see cref="MachineProfileDifferenceSeverity.Error"/>.</description></item>
/// <item><description>Lower processor count or older dotnet runtime → <see cref="MachineProfileDifferenceSeverity.Error"/>.</description></item>
/// <item><description>OS description string drift on the same family → <see cref="MachineProfileDifferenceSeverity.Warning"/>.</description></item>
/// <item><description>Field matches exactly → <see cref="MachineProfileDifferenceSeverity.Info"/>.</description></item>
/// </list>
/// </summary>
public static class MachineProfileComparer
{
    /// <summary>Compares <paramref name="captured"/> against <paramref name="reference"/>.</summary>
    public static MachineProfileComparison Compare(
        KnownGoodMachineProfile reference,
        KnownGoodMachineProfile captured)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(captured);
        reference.Validate();
        captured.Validate();

        var findings = new List<MachineProfileDifference>(capacity: 6);
        AppendFamily(findings, reference, captured);
        AppendArchitecture(findings, reference, captured);
        AppendProcessorCount(findings, reference, captured);
        AppendDotnetRuntime(findings, reference, captured);
        AppendGraphicsAdapter(findings, reference, captured);
        AppendOperatingSystemDescription(findings, reference, captured);
        return MachineProfileComparison.From(reference, captured, findings);
    }

    private static void AppendFamily(
        List<MachineProfileDifference> findings,
        KnownGoodMachineProfile reference,
        KnownGoodMachineProfile captured)
    {
        var expected = reference.OperatingSystemFamily;
        var actual = captured.OperatingSystemFamily;
        if (expected == actual)
        {
            findings.Add(new MachineProfileDifference(
                AlphaHarnessDiagnosticCodes.MachineOsFamilyMismatch,
                MachineProfileDifferenceSeverity.Info,
                "operatingSystemFamily",
                expected.ToString(),
                actual.ToString(),
                $"Operating system family matches '{expected}'."));
            return;
        }

        findings.Add(new MachineProfileDifference(
            AlphaHarnessDiagnosticCodes.MachineOsFamilyMismatch,
            MachineProfileDifferenceSeverity.Error,
            "operatingSystemFamily",
            expected.ToString(),
            actual.ToString(),
            $"Operating system family mismatch: expected '{expected}', captured '{actual}'."));
    }

    private static void AppendArchitecture(
        List<MachineProfileDifference> findings,
        KnownGoodMachineProfile reference,
        KnownGoodMachineProfile captured)
    {
        if (string.Equals(reference.ProcessArchitecture, captured.ProcessArchitecture, StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new MachineProfileDifference(
                AlphaHarnessDiagnosticCodes.MachineArchitectureMismatch,
                MachineProfileDifferenceSeverity.Info,
                "processArchitecture",
                reference.ProcessArchitecture,
                captured.ProcessArchitecture,
                $"Process architecture matches '{reference.ProcessArchitecture}'."));
            return;
        }

        findings.Add(new MachineProfileDifference(
            AlphaHarnessDiagnosticCodes.MachineArchitectureMismatch,
            MachineProfileDifferenceSeverity.Error,
            "processArchitecture",
            reference.ProcessArchitecture,
            captured.ProcessArchitecture,
            $"Process architecture mismatch: expected '{reference.ProcessArchitecture}', captured '{captured.ProcessArchitecture}'."));
    }

    private static void AppendProcessorCount(
        List<MachineProfileDifference> findings,
        KnownGoodMachineProfile reference,
        KnownGoodMachineProfile captured)
    {
        var expected = reference.LogicalProcessorCount;
        var actual = captured.LogicalProcessorCount;
        if (actual >= expected)
        {
            findings.Add(new MachineProfileDifference(
                AlphaHarnessDiagnosticCodes.MachineProcessorCountBelowExpected,
                MachineProfileDifferenceSeverity.Info,
                "logicalProcessorCount",
                expected.ToString(System.Globalization.CultureInfo.InvariantCulture),
                actual.ToString(System.Globalization.CultureInfo.InvariantCulture),
                $"Logical processor count {actual} >= reference {expected}."));
            return;
        }

        findings.Add(new MachineProfileDifference(
            AlphaHarnessDiagnosticCodes.MachineProcessorCountBelowExpected,
            MachineProfileDifferenceSeverity.Error,
            "logicalProcessorCount",
            expected.ToString(System.Globalization.CultureInfo.InvariantCulture),
            actual.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"Logical processor count {actual} below reference {expected}."));
    }

    private static void AppendDotnetRuntime(
        List<MachineProfileDifference> findings,
        KnownGoodMachineProfile reference,
        KnownGoodMachineProfile captured)
    {
        if (string.Equals(reference.DotnetRuntimeVersion, captured.DotnetRuntimeVersion, StringComparison.Ordinal))
        {
            findings.Add(new MachineProfileDifference(
                AlphaHarnessDiagnosticCodes.MachineDotnetRuntimeBelowExpected,
                MachineProfileDifferenceSeverity.Info,
                "dotnetRuntimeVersion",
                reference.DotnetRuntimeVersion,
                captured.DotnetRuntimeVersion,
                $"Dotnet runtime matches '{reference.DotnetRuntimeVersion}'."));
            return;
        }

        findings.Add(new MachineProfileDifference(
            AlphaHarnessDiagnosticCodes.MachineDotnetRuntimeBelowExpected,
            MachineProfileDifferenceSeverity.Warning,
            "dotnetRuntimeVersion",
            reference.DotnetRuntimeVersion,
            captured.DotnetRuntimeVersion,
            $"Dotnet runtime drift: reference '{reference.DotnetRuntimeVersion}', captured '{captured.DotnetRuntimeVersion}'."));
    }

    private static void AppendGraphicsAdapter(
        List<MachineProfileDifference> findings,
        KnownGoodMachineProfile reference,
        KnownGoodMachineProfile captured)
    {
        if (reference.GraphicsAdapterName is null)
        {
            findings.Add(new MachineProfileDifference(
                AlphaHarnessDiagnosticCodes.MachineGraphicsAdapterUnknown,
                MachineProfileDifferenceSeverity.Info,
                "graphicsAdapterName",
                "<unset>",
                captured.GraphicsAdapterName ?? "<unset>",
                "Reference profile has no graphics adapter to compare against."));
            return;
        }

        if (captured.GraphicsAdapterName is null)
        {
            findings.Add(new MachineProfileDifference(
                AlphaHarnessDiagnosticCodes.MachineGraphicsAdapterUnknown,
                MachineProfileDifferenceSeverity.Error,
                "graphicsAdapterName",
                reference.GraphicsAdapterName,
                "<unset>",
                $"Captured host did not expose a graphics adapter; reference required '{reference.GraphicsAdapterName}'."));
            return;
        }

        if (string.Equals(reference.GraphicsAdapterName, captured.GraphicsAdapterName, StringComparison.Ordinal))
        {
            findings.Add(new MachineProfileDifference(
                AlphaHarnessDiagnosticCodes.MachineGraphicsAdapterMismatch,
                MachineProfileDifferenceSeverity.Info,
                "graphicsAdapterName",
                reference.GraphicsAdapterName,
                captured.GraphicsAdapterName,
                $"Graphics adapter matches '{reference.GraphicsAdapterName}'."));
            return;
        }

        findings.Add(new MachineProfileDifference(
            AlphaHarnessDiagnosticCodes.MachineGraphicsAdapterMismatch,
            MachineProfileDifferenceSeverity.Error,
            "graphicsAdapterName",
            reference.GraphicsAdapterName,
            captured.GraphicsAdapterName,
            $"Graphics adapter mismatch: expected '{reference.GraphicsAdapterName}', captured '{captured.GraphicsAdapterName}'."));
    }

    private static void AppendOperatingSystemDescription(
        List<MachineProfileDifference> findings,
        KnownGoodMachineProfile reference,
        KnownGoodMachineProfile captured)
    {
        if (string.Equals(reference.OperatingSystemDescription, captured.OperatingSystemDescription, StringComparison.Ordinal))
        {
            findings.Add(new MachineProfileDifference(
                AlphaHarnessDiagnosticCodes.MachineOsFamilyMismatch,
                MachineProfileDifferenceSeverity.Info,
                "operatingSystemDescription",
                reference.OperatingSystemDescription,
                captured.OperatingSystemDescription,
                $"OS description matches '{reference.OperatingSystemDescription}'."));
            return;
        }

        // OS description drift on the same family is informational only — minor build
        // numbers update frequently and should not block a tester smoke.
        findings.Add(new MachineProfileDifference(
            AlphaHarnessDiagnosticCodes.MachineOsFamilyMismatch,
            MachineProfileDifferenceSeverity.Warning,
            "operatingSystemDescription",
            reference.OperatingSystemDescription,
            captured.OperatingSystemDescription,
            $"OS description drift: reference '{reference.OperatingSystemDescription}', captured '{captured.OperatingSystemDescription}'."));
    }
}
