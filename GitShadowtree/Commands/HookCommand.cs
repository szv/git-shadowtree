using System.CommandLine;

namespace GitShadowtree.Commands;

/// <summary>
/// Entry point for the installed git hooks. Today it handles post-checkout: when <c>git worktree add</c>
/// creates a new linked worktree, it provisions a shadowtree there (clone from the primary worktree's
/// shadowtree, check out, sync exclude). Always returns 0 so it can never make the triggering git
/// command report failure - <c>git worktree add</c> propagates the hook's exit code.
/// </summary>
internal sealed class HookCommand : Command
{
    public HookCommand() : base("hook", "Internal git-hook handlers (invoked by the installed hooks).")
    {
        Subcommands.Add(BuildPostCheckout());
    }

    private static Command BuildPostCheckout()
    {
        // post-checkout args: $1 old HEAD, $2 new HEAD, $3 branch-checkout flag (1 = branch).
        var oldRef = new Argument<string>("old") { Description = "Ref of the previous HEAD (hook arg $1)." };
        var newRef = new Argument<string>("new") { Description = "Ref of the new HEAD (hook arg $2)." };
        var flag = new Argument<string>("flag") { Description = "Branch-checkout flag, 1 for a branch checkout (hook arg $3)." };

        var command = new Command("post-checkout", "post-checkout handler: provisions the shadowtree in new worktrees.");
        command.Arguments.Add(oldRef);
        command.Arguments.Add(newRef);
        command.Arguments.Add(flag);
        command.SetAction(parseResult => PostCheckout(
            parseResult.GetRequiredValue(oldRef),
            parseResult.GetRequiredValue(flag)));
        return command;
    }

    private static int PostCheckout(string oldRef, string flag)
    {
        try
        {
            // Act only on a fresh branch-level checkout (git worktree add), where the previous HEAD is
            // the null oid - not on ordinary branch switches. The null oid is all zeros (40 or 64).
            if (flag != "1" || oldRef.Length == 0 || oldRef.Any(c => c != '0'))
                return 0;

            var root = Shadowtree.Root();
            var gitDir = Shadowtree.GitDir(root);
            if (Directory.Exists(gitDir))
                return 0; // this work tree already has a shadowtree

            // Only linked worktrees need provisioning; in the primary worktree git-dir == git-common-dir.
            var gitDirPath = Path.GetFullPath(Git.Out(root, "rev-parse", "--git-dir"), root);
            var commonDir = Path.GetFullPath(Git.Out(root, "rev-parse", "--git-common-dir"), root);
            if (string.Equals(gitDirPath, commonDir, StringComparison.Ordinal))
                return 0;

            // Source = the primary worktree's shadowtree (the directory that holds the common .git).
            var primaryRoot = Path.GetDirectoryName(commonDir);
            if (primaryRoot is null) return 0;

            var srcGitDir = Shadowtree.GitDir(primaryRoot);
            if (!Directory.Exists(srcGitDir))
                return 0; // this repo doesn't use a shadowtree - nothing to do

            // Clone the primary shadowtree locally (full local history, offline). `clone --bare` points
            // origin at the local source, so re-point it at the real remote for a later push/pull.
            Git.Run(root, "clone", "--bare", srcGitDir, gitDir);
            try
            {
                var origin = Shadowtree.Out(srcGitDir, primaryRoot, "remote", "get-url", "origin");
                Shadowtree.Run(gitDir, root, "remote", "set-url", "origin", origin);
            }
            catch (CommandException) { /* primary has no upstream; keep origin -> local source */ }

            Shadowtree.Provision(gitDir, root);
            Console.WriteLine($"Shadowtree provisioned for worktree: {root}");
            return 0;
        }
        catch
        {
            return 0; // a hook must never block the git command that triggered it
        }
    }
}
