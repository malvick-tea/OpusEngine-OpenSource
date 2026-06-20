using System;
using Opus.App.OpusAlpha.Cli;
using Opus.App.OpusAlpha.Run;
using Opus.App.OpusAlpha.Run.Consumer;
using Opus.Engine.Consumer.Integration;
using Opus.Foundation;

namespace Opus.App.OpusAlpha;

/// <summary>Entry point for the production D3D12 Opus 0.1 alpha host sample. M9
/// extends the M5.1 single-purpose Window mode with a small dispatch shell so the same
/// binary can drive a headless smoke, run the M9 alpha-package checklist, capture or
/// compare a machine profile, and drive the M8 loopback soak harness. The Window mode
/// remains the default — passing zero or one (positional asset) argument is equivalent
/// to the legacy invocation.</summary>
internal static class Program
{
    /// <summary>Exit code when a <c>--consumer</c> assembly cannot be loaded (startup config error).</summary>
    private const int ConsumerLoadFailureExitCode = 1;

    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var parsed = OpusAlphaCliParser.Parse(args);
        var consoleLog = new ConsoleLog(LogLevel.Information);
        return Dispatch(parsed, consoleLog);
    }

    private static int Dispatch(OpusAlphaArgs args, ConsoleLog consoleLog)
    {
        return args.Mode switch
        {
            OpusAlphaMode.Help => PrintHelp(args.HelpReason),
            OpusAlphaMode.Smoke => RunSmoke(args, consoleLog),
            OpusAlphaMode.ValidatePackage => OpusAlphaPackageRunner.Run(args, consoleLog),
            OpusAlphaMode.CheckMachine => OpusAlphaMachineRunner.Run(args, consoleLog),
            OpusAlphaMode.Soak => OpusAlphaSoakRunner.Run(args, consoleLog),
            OpusAlphaMode.Stress => OpusAlphaStressRunner.Run(args, consoleLog),
            OpusAlphaMode.KnownIssuesMerge => OpusAlphaKnownIssuesRunner.RunMerge(args, consoleLog),
            OpusAlphaMode.KnownIssuesDiff => OpusAlphaKnownIssuesRunner.RunDiff(args, consoleLog),
            _ => RunWindow(args, consoleLog),
        };
    }

    private static int RunWindow(OpusAlphaArgs args, ConsoleLog consoleLog)
    {
        if (!TryResolveConsumer(
                args,
                consoleLog,
                out var integration,
                out var consumerLifetime,
                out var failureExitCode))
        {
            return failureExitCode;
        }

        using (consumerLifetime)
        {
            return OpusAlphaWindowRunner.Run(args, consoleLog, integration);
        }
    }

    private static int RunSmoke(OpusAlphaArgs args, ConsoleLog consoleLog)
    {
        if (!TryResolveConsumer(
                args,
                consoleLog,
                out var integration,
                out var consumerLifetime,
                out var failureExitCode))
        {
            return failureExitCode;
        }

        using (consumerLifetime)
        {
            return OpusAlphaSmokeRunner.Run(args, consoleLog, integration);
        }
    }

    /// <summary>Resolves the optional <c>--consumer</c> integration shared by the window and smoke
    /// modes. Returns true when the run may proceed (no consumer requested, or one loaded), with
    /// <paramref name="integration"/> set accordingly. Returns false when a requested consumer could
    /// not be loaded: the reason is logged and <paramref name="failureExitCode"/> carries the
    /// startup-configuration exit code so the caller returns without opening the host.</summary>
    private static bool TryResolveConsumer(
        OpusAlphaArgs args,
        ConsoleLog consoleLog,
        out ConsumerIntegration? integration,
        out IDisposable? consumerLifetime,
        out int failureExitCode)
    {
        var resolution = ConsumerIntegrationStartup.Resolve(args.ConsumerAssemblyPath);
        integration = resolution.Integration;
        consumerLifetime = resolution.Lifetime;
        failureExitCode = ConsumerLoadFailureExitCode;
        if (!resolution.CanProceed)
        {
            consoleLog.Error($"Consumer integration could not be loaded: {resolution.FailureReason}");
            return false;
        }

        if (resolution.Integration is not null)
        {
            consoleLog.Info($"Consumer integration loaded from '{args.ConsumerAssemblyPath}'.");
        }

        return true;
    }

    private static int PrintHelp(string reason)
    {
        Console.Out.Write(OpusAlphaCliHelp.Render(reason));
        return string.IsNullOrWhiteSpace(reason) ? 0 : 1;
    }
}
