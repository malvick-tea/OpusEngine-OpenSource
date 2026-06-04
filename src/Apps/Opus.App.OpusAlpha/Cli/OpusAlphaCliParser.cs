using System;
using System.Globalization;
using Opus.Engine.AlphaHarness.Scenes;
using Opus.Engine.Diagnostics.Overlay;

namespace Opus.App.OpusAlpha.Cli;

/// <summary>
/// Parses an argv-style <see cref="string"/> array into a deterministic
/// <see cref="OpusAlphaArgs"/> record. Validation is loud at the boundary: unknown
/// options, missing values, and out-of-range numbers all fall through into a
/// <c>Mode = Help</c> result with a one-line reason so the dispatcher prints the
/// usage banner and exits non-zero. No exceptions cross the public surface.
/// </summary>
public static class OpusAlphaCliParser
{
    private const string OptionScene = "--scene";
    private const string OptionAsset = "--asset";
    private const string OptionDiagnostics = "--diagnostics-dir";
    private const string OptionInjectFailure = "--inject-failure";
    private const string OptionFrameBudget = "--frame-budget";
    private const string OptionAsyncLogging = "--async-logging";
    private const string OptionOverlayLevel = "--overlay-level";
    private const string OptionSettings = "--settings";
    private const string OptionConsumer = "--consumer";
    private const string OptionFrames = "--frames";
    private const string OptionScreenshotFrame = "--screenshot-frame";
    private const string OptionReportPath = "--report";
    private const string OptionPackage = "--package";
    private const string OptionMachineReference = "--reference";
    private const string OptionMachineSave = "--save";
    private const string OptionSoakPeers = "--peers";
    private const string OptionSoakPackets = "--packets";
    private const string OptionSoakPayload = "--payload";
    private const string OptionStressIterations = "--iterations";
    private const string OptionStressReportDirectory = "--stress-dir";
    private const string OptionKnownIssuesPath = "--known-issues";
    private const string OptionInjectLoss = "--inject-loss";
    private const string OptionInjectLatencyMs = "--inject-latency-ms";
    private const string OptionInjectSeed = "--inject-seed";
    private const string OptionInjectPeers = "--inject-peers";
    private const string OptionInjectPackets = "--inject-packets";
    private const string OptionInjectPayload = "--inject-payload";
    private const string OptionInjectDropTolerance = "--inject-drop-tolerance";
    private const string OptionInjectInboundLoss = "--inject-inbound-loss";
    private const string OptionInjectInboundLatencyMs = "--inject-inbound-latency-ms";
    private const string OptionInjectInboundSeed = "--inject-inbound-seed";
    private const string OptionInjectInboundDropTolerance = "--inject-inbound-drop-tolerance";
    private const string OptionKnownIssuesBase = "--base";
    private const string OptionKnownIssuesOverlay = "--overlay";
    private const string OptionKnownIssuesLeft = "--left";
    private const string OptionKnownIssuesRight = "--right";
    private const string OptionKnownIssuesOutput = "--output";
    private const string OptionKnownIssuesFormat = "--format";
    private const string OptionHelpShort = "-h";
    private const string OptionHelpLong = "--help";

    private const string CommandSmoke = "smoke";
    private const string CommandValidatePackage = "validate-package";
    private const string CommandCheckMachine = "check-machine";
    private const string CommandSoak = "soak";
    private const string CommandStress = "stress";
    private const string CommandKnownIssuesMerge = "known-issues-merge";
    private const string CommandKnownIssuesDiff = "known-issues-diff";

    private const string FormatText = "text";
    private const string FormatJson = "json";

    private const string FaultStartup = "startup";
    private const string FaultContent = "content";
    private const string FaultDeviceLost = "device-lost";

    /// <summary>Parses <paramref name="argv"/> into <see cref="OpusAlphaArgs"/>.</summary>
    public static OpusAlphaArgs Parse(string[] argv)
    {
        ArgumentNullException.ThrowIfNull(argv);
        if (argv.Length == 0)
        {
            return OpusAlphaArgs.WindowDefaults();
        }

        var first = argv[0];
        if (string.Equals(first, OptionHelpShort, StringComparison.Ordinal)
            || string.Equals(first, OptionHelpLong, StringComparison.Ordinal))
        {
            return OpusAlphaArgs.Help(string.Empty);
        }

        var (mode, startIndex) = ResolveMode(argv);
        return ApplyOptions(mode, argv, startIndex);
    }

    private static (OpusAlphaMode Mode, int StartIndex) ResolveMode(string[] argv)
    {
        return argv[0] switch
        {
            CommandSmoke => (OpusAlphaMode.Smoke, 1),
            CommandValidatePackage => (OpusAlphaMode.ValidatePackage, 1),
            CommandCheckMachine => (OpusAlphaMode.CheckMachine, 1),
            CommandSoak => (OpusAlphaMode.Soak, 1),
            CommandStress => (OpusAlphaMode.Stress, 1),
            CommandKnownIssuesMerge => (OpusAlphaMode.KnownIssuesMerge, 1),
            CommandKnownIssuesDiff => (OpusAlphaMode.KnownIssuesDiff, 1),
            _ => (OpusAlphaMode.Window, 0),
        };
    }

    private static OpusAlphaArgs ApplyOptions(OpusAlphaMode mode, string[] argv, int startIndex)
    {
        var current = OpusAlphaArgs.WindowDefaults() with { Mode = mode };
        for (var i = startIndex; i < argv.Length; i++)
        {
            var token = argv[i];
            if (IsValuelessHelp(token))
            {
                return OpusAlphaArgs.Help(string.Empty);
            }

            if (TryConsumeValuelessPositional(token, ref current))
            {
                continue;
            }

            if (TryConsumeValuelessOption(token, ref current))
            {
                continue;
            }

            if (!TryConsumeOptionValue(argv, ref i, token, out var value, out var help))
            {
                return help!;
            }

            current = ApplyOption(token, value, current);
            if (current.Mode == OpusAlphaMode.Help)
            {
                return current;
            }
        }

        return current;
    }

    private static bool IsValuelessHelp(string token) =>
        string.Equals(token, OptionHelpShort, StringComparison.Ordinal)
        || string.Equals(token, OptionHelpLong, StringComparison.Ordinal);

    private static bool TryConsumeValuelessPositional(string token, ref OpusAlphaArgs current)
    {
        if (token.StartsWith('-'))
        {
            return false;
        }

        if (current.Mode == OpusAlphaMode.Window && current.AssetPath is null)
        {
            current = current with { AssetPath = token };
            return true;
        }

        return false;
    }

    private static bool TryConsumeValuelessOption(string token, ref OpusAlphaArgs current)
    {
        if (string.Equals(token, OptionFrameBudget, StringComparison.Ordinal))
        {
            current = current with { EnableFrameBudget = true };
            return true;
        }

        if (string.Equals(token, OptionAsyncLogging, StringComparison.Ordinal))
        {
            current = current with { EnableAsyncLogging = true };
            return true;
        }

        return false;
    }

    private static bool TryConsumeOptionValue(
        string[] argv,
        ref int index,
        string optionName,
        out string value,
        out OpusAlphaArgs? help)
    {
        if (!optionName.StartsWith("--", StringComparison.Ordinal))
        {
            value = string.Empty;
            help = OpusAlphaArgs.Help($"Unknown or out-of-place token '{optionName}'.");
            return false;
        }

        if (index + 1 >= argv.Length)
        {
            value = string.Empty;
            help = OpusAlphaArgs.Help($"Option {optionName} is missing its value.");
            return false;
        }

        var next = argv[index + 1];
        if (next.StartsWith("--", StringComparison.Ordinal))
        {
            value = string.Empty;
            help = OpusAlphaArgs.Help($"Option {optionName} is missing its value.");
            return false;
        }

        index++;
        value = next;
        help = null;
        return true;
    }

    private static OpusAlphaArgs ApplyOption(string optionName, string value, OpusAlphaArgs current) => optionName switch
    {
        OptionScene => ApplyScene(current, value),
        OptionAsset => current with { AssetPath = value },
        OptionDiagnostics => current with { DiagnosticsDirectory = value },
        OptionInjectFailure => ApplyInjectFailure(current, value),
        OptionOverlayLevel => ApplyOverlayLevel(current, value),
        OptionSettings => current with { SettingsPath = value },
        OptionConsumer => current with { ConsumerAssemblyPath = value },
        OptionFrames => ApplyInt(current, OptionFrames, value, static (args, n) => args with { SmokeFrameCount = n }, minimum: 1),
        OptionScreenshotFrame => ApplyInt(current, OptionScreenshotFrame, value, static (args, n) => args with { SmokeScreenshotFrame = n }, minimum: 0),
        OptionReportPath => current with { SmokeReportPath = value },
        OptionPackage => current with { PackagePath = value },
        OptionMachineReference => current with { MachineReferencePath = value },
        OptionMachineSave => current with { MachineSavePath = value },
        OptionSoakPeers => ApplyInt(current, OptionSoakPeers, value, static (args, n) => args with { SoakPeers = n }, minimum: 1),
        OptionSoakPackets => ApplyInt(current, OptionSoakPackets, value, static (args, n) => args with { SoakPacketsPerPeer = n }, minimum: 1),
        OptionSoakPayload => ApplyInt(current, OptionSoakPayload, value, static (args, n) => args with { SoakPayloadBytes = n }, minimum: 1),
        OptionStressIterations => ApplyInt(current, OptionStressIterations, value, static (args, n) => args with { StressIterations = n }, minimum: 1),
        OptionStressReportDirectory => current with { StressReportDirectory = value },
        OptionKnownIssuesPath => current with { KnownIssuesPath = value },
        OptionInjectLoss => EnableInjection(ApplyFraction(current, OptionInjectLoss, value, static (args, x) => args with { StressInjectionLossRate = x })),
        OptionInjectLatencyMs => EnableInjection(ApplyInt(current, OptionInjectLatencyMs, value, static (args, n) => args with { StressInjectionLatencyMilliseconds = n }, minimum: 0)),
        OptionInjectSeed => EnableInjection(ApplyInt(current, OptionInjectSeed, value, static (args, n) => args with { StressInjectionSeed = n }, minimum: int.MinValue)),
        OptionInjectPeers => EnableInjection(ApplyInt(current, OptionInjectPeers, value, static (args, n) => args with { StressInjectionPeers = n }, minimum: 1)),
        OptionInjectPackets => EnableInjection(ApplyInt(current, OptionInjectPackets, value, static (args, n) => args with { StressInjectionPacketsPerPeer = n }, minimum: 1)),
        OptionInjectPayload => EnableInjection(ApplyInt(current, OptionInjectPayload, value, static (args, n) => args with { StressInjectionPayloadBytes = n }, minimum: 1)),
        OptionInjectDropTolerance => EnableInjection(ApplyFraction(current, OptionInjectDropTolerance, value, static (args, x) => args with { StressInjectionDropTolerance = x })),
        OptionInjectInboundLoss => EnableInjection(ApplyFraction(current, OptionInjectInboundLoss, value, static (args, x) => args with { StressInjectionInboundLossRate = x })),
        OptionInjectInboundLatencyMs => EnableInjection(ApplyInt(current, OptionInjectInboundLatencyMs, value, static (args, n) => args with { StressInjectionInboundLatencyMilliseconds = n }, minimum: 0)),
        OptionInjectInboundSeed => EnableInjection(ApplyInt(current, OptionInjectInboundSeed, value, static (args, n) => args with { StressInjectionInboundSeed = n }, minimum: int.MinValue)),
        OptionInjectInboundDropTolerance => EnableInjection(ApplyFraction(current, OptionInjectInboundDropTolerance, value, static (args, x) => args with { StressInjectionInboundDropTolerance = x })),
        OptionKnownIssuesBase => current with { KnownIssuesBasePath = value },
        OptionKnownIssuesOverlay => current with { KnownIssuesOverlayPath = value },
        OptionKnownIssuesLeft => current with { KnownIssuesLeftPath = value },
        OptionKnownIssuesRight => current with { KnownIssuesRightPath = value },
        OptionKnownIssuesOutput => current with { KnownIssuesOutputPath = value },
        OptionKnownIssuesFormat => ApplyDiffFormat(current, value),
        _ => OpusAlphaArgs.Help($"Unknown option '{optionName}'."),
    };

    private static OpusAlphaArgs ApplyDiffFormat(OpusAlphaArgs current, string value)
    {
        if (string.Equals(value, FormatText, StringComparison.OrdinalIgnoreCase))
        {
            return current with { KnownIssuesDiffFormat = KnownIssuesDiffFormat.Text };
        }

        if (string.Equals(value, FormatJson, StringComparison.OrdinalIgnoreCase))
        {
            return current with { KnownIssuesDiffFormat = KnownIssuesDiffFormat.Json };
        }

        return OpusAlphaArgs.Help($"--format expects 'text' or 'json'; received '{value}'.");
    }

    private static OpusAlphaArgs ApplyInjectFailure(OpusAlphaArgs current, string value)
    {
        if (string.Equals(value, FaultStartup, StringComparison.OrdinalIgnoreCase))
        {
            return current with { InjectFailure = AlphaFaultKind.Startup };
        }

        if (string.Equals(value, FaultContent, StringComparison.OrdinalIgnoreCase))
        {
            return current with { InjectFailure = AlphaFaultKind.Content };
        }

        if (string.Equals(value, FaultDeviceLost, StringComparison.OrdinalIgnoreCase))
        {
            return current with { InjectFailure = AlphaFaultKind.DeviceLost };
        }

        return OpusAlphaArgs.Help(
            $"--inject-failure expects 'startup', 'content', or 'device-lost'; received '{value}'.");
    }

    private static OpusAlphaArgs ApplyOverlayLevel(OpusAlphaArgs current, string value)
    {
        if (string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
        {
            return current with { OverlayLevel = DiagnosticOverlayLevel.Off };
        }

        if (string.Equals(value, "minimal", StringComparison.OrdinalIgnoreCase))
        {
            return current with { OverlayLevel = DiagnosticOverlayLevel.Minimal };
        }

        if (string.Equals(value, "full", StringComparison.OrdinalIgnoreCase))
        {
            return current with { OverlayLevel = DiagnosticOverlayLevel.Full };
        }

        return OpusAlphaArgs.Help($"--overlay-level expects 'off', 'minimal', or 'full'; received '{value}'.");
    }

    private static OpusAlphaArgs EnableInjection(OpusAlphaArgs args) =>
        args.Mode == OpusAlphaMode.Help
            ? args
            : args with { EnableStressNetworkInjection = true };

    private static OpusAlphaArgs ApplyScene(OpusAlphaArgs current, string value)
    {
        if (string.Equals(value, "small", StringComparison.OrdinalIgnoreCase))
        {
            return current with { SceneScale = AlphaSceneScale.Small };
        }

        if (string.Equals(value, "large", StringComparison.OrdinalIgnoreCase))
        {
            return current with { SceneScale = AlphaSceneScale.Large };
        }

        if (string.Equals(value, "massive", StringComparison.OrdinalIgnoreCase))
        {
            return current with { SceneScale = AlphaSceneScale.Massive };
        }

        return OpusAlphaArgs.Help($"--scene expects 'small', 'large', or 'massive'; received '{value}'.");
    }

    private static OpusAlphaArgs ApplyInt(
        OpusAlphaArgs current,
        string optionName,
        string raw,
        Func<OpusAlphaArgs, int, OpusAlphaArgs> projector,
        int minimum)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return OpusAlphaArgs.Help($"{optionName} expects an integer; received '{raw}'.");
        }

        if (value < minimum)
        {
            return OpusAlphaArgs.Help($"{optionName} must be >= {minimum.ToString(CultureInfo.InvariantCulture)}.");
        }

        return projector(current, value);
    }

    private static OpusAlphaArgs ApplyFraction(
        OpusAlphaArgs current,
        string optionName,
        string raw,
        Func<OpusAlphaArgs, double, OpusAlphaArgs> projector)
    {
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return OpusAlphaArgs.Help($"{optionName} expects a decimal in [0,1]; received '{raw}'.");
        }

        if (double.IsNaN(value) || value < 0.0 || value > 1.0)
        {
            return OpusAlphaArgs.Help($"{optionName} must be in the inclusive range [0,1].");
        }

        return projector(current, value);
    }
}
