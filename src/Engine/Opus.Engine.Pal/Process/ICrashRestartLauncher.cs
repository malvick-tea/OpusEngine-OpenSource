namespace Opus.Engine.Pal.Process;

/// <summary>Spawns a fresh process of the current executable so the player lands back in
/// the game after a crash. Concrete impls live in <c>Engine.Pal.{platform}</c> — Windows
/// uses <c>Process.Start(Environment.ProcessPath)</c>, mobile platforms route through an
/// activity restart intent. Implementations must NOT terminate the calling process —
/// returning lets <see cref="CrashReportNotifier"/> finish its bookkeeping; the CLR then
/// kills the process as part of the unhandled-exception path.</summary>
public interface ICrashRestartLauncher
{
    void RelaunchProcess();
}
