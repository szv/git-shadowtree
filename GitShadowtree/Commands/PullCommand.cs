using System.CommandLine;

namespace GitShadowtree.Commands;

/// <summary>
/// Pulls shadowtree changes, then re-mirrors info/exclude from the pulled patterns (the exclude
/// lives in the main repo, not the shadowtree). Extra tokens are forwarded to <c>git pull</c>.
/// </summary>
internal sealed class PullCommand : Command
{
    public PullCommand() : base("pull", "Pulls shadowtree changes, then syncs the exclude from .shadowtree.")
    {
        // Let git pull flags such as --rebase / --ff-only pass through to the forwarded command.
        TreatUnmatchedTokensAsErrors = false;
        SetAction(Run);
    }

    private int Run(ParseResult parseResult)
    {
        var root = Shadowtree.Root();
        var gitDir = Shadowtree.GitDir(root);

        string[] tokens = [.. parseResult.UnmatchedTokens];
        string[] pullArgs = tokens.Length > 0 ? ["pull", .. tokens] : ["pull", "origin", "main"];
        var code = Shadowtree.Run(gitDir, root, pullArgs);

        // The exclude is not tracked by the shadowtree, so mirror it from the just-pulled patterns.
        // Skip on a failed pull (e.g. merge conflict) so a conflicted .shadowtree is not mirrored.
        if (code == 0)
            Shadowtree.SyncExclude(root, Shadowtree.LoadPatterns(root));

        return code;
    }
}
