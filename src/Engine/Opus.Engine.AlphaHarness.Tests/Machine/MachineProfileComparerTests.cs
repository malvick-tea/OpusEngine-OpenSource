using System;
using System.Linq;
using FluentAssertions;
using Opus.Engine.AlphaHarness;
using Opus.Engine.AlphaHarness.Machine;
using Xunit;

namespace Opus.Engine.AlphaHarness.Tests.Machine;

public sealed class MachineProfileComparerTests
{
    private static readonly DateTimeOffset FixedTimestamp =
        new(2026, 5, 27, 9, 0, 0, TimeSpan.Zero);

    private static KnownGoodMachineProfile Reference() => new(
        ProfileName: "reference",
        OperatingSystemFamily: MachineOperatingSystemFamily.Windows,
        OperatingSystemDescription: "Microsoft Windows 11 Pro",
        ProcessArchitecture: "X64",
        LogicalProcessorCount: 16,
        DotnetRuntimeVersion: ".NET 8.0.10",
        GraphicsAdapterName: "NVIDIA GeForce RTX 4090",
        CapturedAtUtc: FixedTimestamp,
        Notes: null);

    [Fact]
    public void Identical_profiles_report_no_errors()
    {
        var reference = Reference();
        var captured = reference with { ProfileName = "captured" };

        var comparison = MachineProfileComparer.Compare(reference, captured);

        comparison.ErrorCount.Should().Be(0);
        comparison.WarningCount.Should().Be(0);
        comparison.IsCompatible.Should().BeTrue();
    }

    [Fact]
    public void Os_family_mismatch_is_error()
    {
        var reference = Reference();
        var captured = reference with { OperatingSystemFamily = MachineOperatingSystemFamily.Linux };

        var comparison = MachineProfileComparer.Compare(reference, captured);

        comparison.IsCompatible.Should().BeFalse();
        comparison.Differences.Should().Contain(diff =>
            diff.DiagnosticCode == AlphaHarnessDiagnosticCodes.MachineOsFamilyMismatch
            && diff.Severity == MachineProfileDifferenceSeverity.Error);
    }

    [Fact]
    public void Architecture_mismatch_is_error()
    {
        var reference = Reference();
        var captured = reference with { ProcessArchitecture = "Arm64" };

        var comparison = MachineProfileComparer.Compare(reference, captured);

        comparison.IsCompatible.Should().BeFalse();
        comparison.Differences.Should().Contain(diff =>
            diff.DiagnosticCode == AlphaHarnessDiagnosticCodes.MachineArchitectureMismatch
            && diff.Severity == MachineProfileDifferenceSeverity.Error);
    }

    [Fact]
    public void Processor_count_below_reference_is_error()
    {
        var reference = Reference();
        var captured = reference with { LogicalProcessorCount = 4 };

        var comparison = MachineProfileComparer.Compare(reference, captured);

        comparison.IsCompatible.Should().BeFalse();
        comparison.Differences.Should().Contain(diff =>
            diff.DiagnosticCode == AlphaHarnessDiagnosticCodes.MachineProcessorCountBelowExpected
            && diff.Severity == MachineProfileDifferenceSeverity.Error);
    }

    [Fact]
    public void Processor_count_above_reference_is_info()
    {
        var reference = Reference();
        var captured = reference with { LogicalProcessorCount = reference.LogicalProcessorCount * 2 };

        var comparison = MachineProfileComparer.Compare(reference, captured);

        comparison.IsCompatible.Should().BeTrue();
        comparison.Differences.Should().Contain(diff =>
            diff.DiagnosticCode == AlphaHarnessDiagnosticCodes.MachineProcessorCountBelowExpected
            && diff.Severity == MachineProfileDifferenceSeverity.Info);
    }

    [Fact]
    public void Dotnet_runtime_drift_is_warning_only()
    {
        var reference = Reference();
        var captured = reference with { DotnetRuntimeVersion = ".NET 8.0.11" };

        var comparison = MachineProfileComparer.Compare(reference, captured);

        comparison.IsCompatible.Should().BeTrue();
        comparison.WarningCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Captured_host_without_adapter_against_reference_with_adapter_is_error()
    {
        var reference = Reference();
        var captured = reference with { GraphicsAdapterName = null };

        var comparison = MachineProfileComparer.Compare(reference, captured);

        comparison.IsCompatible.Should().BeFalse();
        comparison.Differences.Should().Contain(diff =>
            diff.DiagnosticCode == AlphaHarnessDiagnosticCodes.MachineGraphicsAdapterUnknown
            && diff.Severity == MachineProfileDifferenceSeverity.Error);
    }

    [Fact]
    public void Reference_without_adapter_is_informational()
    {
        var reference = Reference() with { GraphicsAdapterName = null };
        var captured = Reference() with { ProfileName = "captured" };

        var comparison = MachineProfileComparer.Compare(reference, captured);

        comparison.IsCompatible.Should().BeTrue();
        comparison.Differences.Should().Contain(diff =>
            diff.DiagnosticCode == AlphaHarnessDiagnosticCodes.MachineGraphicsAdapterUnknown);
    }

    [Fact]
    public void Adapter_mismatch_is_error()
    {
        var reference = Reference();
        var captured = reference with { GraphicsAdapterName = "AMD Radeon RX 7900 XTX" };

        var comparison = MachineProfileComparer.Compare(reference, captured);

        comparison.IsCompatible.Should().BeFalse();
        comparison.Differences.Should().Contain(diff =>
            diff.DiagnosticCode == AlphaHarnessDiagnosticCodes.MachineGraphicsAdapterMismatch
            && diff.Severity == MachineProfileDifferenceSeverity.Error);
    }

    [Fact]
    public void Os_description_drift_on_same_family_is_warning()
    {
        var reference = Reference();
        var captured = reference with { OperatingSystemDescription = "Microsoft Windows 11 Pro 26200" };

        var comparison = MachineProfileComparer.Compare(reference, captured);

        comparison.IsCompatible.Should().BeTrue();
        comparison.Differences.Should().Contain(diff =>
            diff.Field == "operatingSystemDescription"
            && diff.Severity == MachineProfileDifferenceSeverity.Warning);
    }

    [Fact]
    public void Differences_are_sorted_severity_then_code()
    {
        var reference = Reference();
        var captured = reference with
        {
            OperatingSystemFamily = MachineOperatingSystemFamily.Linux,
            ProcessArchitecture = "Arm64",
            LogicalProcessorCount = 2,
        };

        var comparison = MachineProfileComparer.Compare(reference, captured);
        var severities = comparison.Differences.Select(d => (int)d.Severity).ToArray();

        for (var i = 1; i < severities.Length; i++)
        {
            severities[i - 1].Should().BeGreaterThanOrEqualTo(severities[i]);
        }
    }
}
