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

        // Skip on a failed pull (e.g. merge conflict) so a conflicted .shadowtree is not acted on.
        if (code == 0)
        {
            // A pull can change or remove .shadowtree; keep it present and staged, then mirror the
            // exclude (which lives in the main repo, not the shadowtree) from the resulting patterns.
            var patterns = Shadowtree.LoadPatterns(root);
            Shadowtree.EnsurePatternsFileStaged(gitDir, root, patterns);
            Shadowtree.SyncExclude(root, patterns);
        }

        return code;
    }
}
