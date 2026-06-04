namespace Opus.Tool.PackageValidator;

/// <summary>
/// Console entry point for the Opus content package validator.
/// </summary>
public static class Program
{
    /// <summary>Runs the package validator CLI.</summary>
    public static int Main(string[] args) =>
        PackageValidatorCommand.Run(args, Console.Out, Console.Error);
}
