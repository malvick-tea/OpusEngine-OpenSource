using System;
using FluentAssertions;
using Opus.Engine.AlphaStress.Stress;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Stress;

public sealed class AlphaStressIssueCodeStringsTests
{
    [Theory]
    [InlineData(AlphaStressIssueCode.HostUnavailable, AlphaStressDiagnosticCodes.StressHostUnavailable)]
    [InlineData(AlphaStressIssueCode.BudgetExceeded, AlphaStressDiagnosticCodes.StressBudgetExceeded)]
    [InlineData(AlphaStressIssueCode.IterationFailed, AlphaStressDiagnosticCodes.StressIterationFailed)]
    [InlineData(AlphaStressIssueCode.FramePacingDegraded, AlphaStressDiagnosticCodes.StressFramePacingDegraded)]
    [InlineData(AlphaStressIssueCode.MemoryGrowthExceeded, AlphaStressDiagnosticCodes.StressMemoryGrowthExceeded)]
    [InlineData(AlphaStressIssueCode.FaultInjectionDegraded, AlphaStressDiagnosticCodes.StressFaultInjectionDegraded)]
    [InlineData(AlphaStressIssueCode.IterationUnhandledException, AlphaStressDiagnosticCodes.StressIterationUnhandledException)]
    [InlineData(AlphaStressIssueCode.KnownIssueBlockerOpen, AlphaStressDiagnosticCodes.KnownIssueBlockerOpen)]
    [InlineData(AlphaStressIssueCode.KnownIssueMustFixOpen, AlphaStressDiagnosticCodes.KnownIssueMustFixOpen)]
    public void ToDiagnosticCode_maps_every_known_value(AlphaStressIssueCode code, string expected)
    {
        AlphaStressIssueCodeStrings.ToDiagnosticCode(code).Should().Be(expected);
    }

    [Fact]
    public void ToDiagnosticCode_throws_on_unknown_value()
    {
        var act = () => AlphaStressIssueCodeStrings.ToDiagnosticCode((AlphaStressIssueCode)9999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
