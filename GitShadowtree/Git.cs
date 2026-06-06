using System.Diagnostics;

namespace GitShadowtree;

/// <summary>Invokes git through the CLI (no LibGit2Sharp - the bare git-dir plus work-tree is handled natively).</summary>
internal static class Git
{
    public static int Run(string? workingDirectory, params string[] args)
    {
        var psi = new ProcessStartInfo("git") { UseShellExecute = false };
        if (workingDirectory is not null) psi.WorkingDirectory = workingDirectory;
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = Start(psi);
        process.WaitForExit();
        return process.ExitCode;
    }

    /// <summary>
    /// Like <see cref="Run"/> but swallows output and never throws - returns just the exit code,
    /// for best-effort calls where a non-matching pattern (exit 128) is expected.
    /// </summary>
    public static int TryRun(string? workingDirectory, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (workingDirectory is not null) psi.WorkingDirectory = workingDirectory;
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = Start(psi);
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
    }

    public static string Out(string? workingDirectory, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (workingDirectory is not null) psi.WorkingDirectory = workingDirectory;
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = Start(psi);
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new CommandException($"git {string.Join(' ', args)} failed: {standardError.Trim()}");
        return standardOutput.Trim();
    }

    static Process Start(ProcessStartInfo psi)
    {
        try
        {
            return Process.Start(psi) ?? throw new CommandException("git could not be started.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new CommandException("git not found - install Git and add it to PATH.");
        }
    }
}
