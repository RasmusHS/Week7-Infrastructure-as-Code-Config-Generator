namespace HomelabCompose.Core.Diff;

public enum DiffLineType
{
    Context,
    Added,
    Removed,
}

public record DiffLine(DiffLineType Type, string Content);

public enum DiffStatus
{
    New,
    Unchanged,
    Modified,
}

public class FileDiffResult
{
    public required string FileName { get; init; }
    public DiffStatus Status { get; init; }
    public List<DiffLine> Lines { get; init; } = [];

    public void PrintToConsole()
    {
        switch (Status)
        {
            case DiffStatus.New:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  + NEW: {FileName}");
                Console.ResetColor();
                return;

            case DiffStatus.Unchanged:
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  = UNCHANGED: {FileName}");
                Console.ResetColor();
                return;

            case DiffStatus.Modified:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  ~ MODIFIED: {FileName}");
                Console.ResetColor();
                break;
        }

        Console.WriteLine();
        foreach (var line in Lines)
        {
            switch (line.Type)
            {
                case DiffLineType.Removed:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  - {line.Content}");
                    break;
                case DiffLineType.Added:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  + {line.Content}");
                    break;
                case DiffLineType.Context:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"    {line.Content}");
                    break;
            }
        }

        Console.ResetColor();
        Console.WriteLine();
    }
}

public class DiffService
{
    private const int ContextLines = 3;

    public List<FileDiffResult> ComputeDiffs(
        string outputDir, Dictionary<string, string> generatedFiles)
    {
        var results = new List<FileDiffResult>();

        foreach (var (fileName, newContent) in generatedFiles)
        {
            var filePath = Path.Combine(outputDir, fileName);

            if (!File.Exists(filePath))
            {
                results.Add(new FileDiffResult
                {
                    FileName = fileName,
                    Status = DiffStatus.New,
                });
                continue;
            }

            var oldContent = File.ReadAllText(filePath);

            if (oldContent == newContent)
            {
                results.Add(new FileDiffResult
                {
                    FileName = fileName,
                    Status = DiffStatus.Unchanged,
                });
                continue;
            }

            var oldLines = SplitLines(oldContent);
            var newLines = SplitLines(newContent);
            var diffLines = BuildDiffWithContext(oldLines, newLines);

            results.Add(new FileDiffResult
            {
                FileName = fileName,
                Status = DiffStatus.Modified,
                Lines = diffLines,
            });
        }

        return results;
    }

    private List<DiffLine> BuildDiffWithContext(string[] oldLines, string[] newLines)
    {
        var rawDiff = ComputeLcsDiff(oldLines, newLines);
        return AddContext(rawDiff);
    }

    /// <summary>
    /// LCS-based diff: finds the longest common subsequence of lines,
    /// then marks everything else as added or removed.
    /// </summary>
    private List<DiffLine> ComputeLcsDiff(string[] oldLines, string[] newLines)
    {
        var m = oldLines.Length;
        var n = newLines.Length;

        // Build LCS table
        var dp = new int[m + 1, n + 1];
        for (var i = 1; i <= m; i++)
            for (var j = 1; j <= n; j++)
                dp[i, j] = oldLines[i - 1] == newLines[j - 1]
                    ? dp[i - 1, j - 1] + 1
                    : Math.Max(dp[i - 1, j], dp[i, j - 1]);

        // Backtrack to produce diff
        var result = new List<DiffLine>();
        var oi = m;
        var ni = n;

        while (oi > 0 || ni > 0)
        {
            if (oi > 0 && ni > 0 && oldLines[oi - 1] == newLines[ni - 1])
            {
                result.Add(new DiffLine(DiffLineType.Context, oldLines[oi - 1]));
                oi--;
                ni--;
            }
            else if (ni > 0 && (oi == 0 || dp[oi, ni - 1] >= dp[oi - 1, ni]))
            {
                result.Add(new DiffLine(DiffLineType.Added, newLines[ni - 1]));
                ni--;
            }
            else
            {
                result.Add(new DiffLine(DiffLineType.Removed, oldLines[oi - 1]));
                oi--;
            }
        }

        result.Reverse();
        return result;
    }

    /// <summary>
    /// Filters to only show changed lines plus surrounding context,
    /// inserting separator markers between non-contiguous hunks.
    /// </summary>
    private List<DiffLine> AddContext(List<DiffLine> fullDiff)
    {
        // Find which lines are changes
        var changeIndices = new HashSet<int>();
        for (var i = 0; i < fullDiff.Count; i++)
        {
            if (fullDiff[i].Type != DiffLineType.Context)
                changeIndices.Add(i);
        }

        if (changeIndices.Count == 0)
            return [];

        // Mark which lines to include (changes + context window)
        var includeIndices = new HashSet<int>();
        foreach (var idx in changeIndices)
        {
            for (var c = -ContextLines; c <= ContextLines; c++)
            {
                var target = idx + c;
                if (target >= 0 && target < fullDiff.Count)
                    includeIndices.Add(target);
            }
        }

        // Build output with hunk separators
        var result = new List<DiffLine>();
        var lastIncluded = -2;

        for (var i = 0; i < fullDiff.Count; i++)
        {
            if (!includeIndices.Contains(i))
                continue;

            if (lastIncluded >= 0 && i - lastIncluded > 1)
                result.Add(new DiffLine(DiffLineType.Context, "───"));

            result.Add(fullDiff[i]);
            lastIncluded = i;
        }

        return result;
    }

    private static string[] SplitLines(string content)
    {
        return content.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .ToArray();
    }
}
