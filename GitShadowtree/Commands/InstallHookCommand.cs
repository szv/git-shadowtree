using System.CommandLine;

namespace GitShadowtree.Commands;

/// <summary>
/// Installs (or refreshes) the post-checkout hook that provisions the shadowtree in new worktrees.
/// init/clone do this automatically; this command retrofits an existing setup.
/// </summary>
internal sealed class InstallHookCommand : Command
{
    private readonly Option<bool> _force = new("--force", "-f")
    {
        Description = "Overwrite an existing non-managed post-checkout hook."
    };

    public InstallHookCommand()
        : base("install-hook", "Installs the post-checkout hook that sets up the shadowtree in new git worktrees.")
    {
        Options.Add(_force);
        SetAction(Run);
    }

    private int Run(ParseResult parseResult)
    {
        var root = Shadowtree.Root();

        if (Shadowtree.InstallHook(root, parseResult.GetValue(_force)))
        {
            Console.WriteLine($"Installed post-checkout hook: {Path.Combine(Shadowtree.HooksDir(root), "post-checkout")}");
            return 0;
        }

        Console.Error.WriteLine(Shadowtree.ForeignHookNotice);
        Console.Error.WriteLine("Re-run with --force to overwrite it.");
        return 1;
    }
}
