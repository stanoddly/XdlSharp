using System.Text.RegularExpressions;

namespace XdlSharp.Generator;

// Reads the output of clang -E -dD, which keeps each #define in place between line markers that name its file.
internal static partial class MacroScanner
{
    public static (List<MacroInfo> ObjectLike, HashSet<string> Headers) Scan(string preprocessed, string includeDirectory)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(includeDirectory)) + "/";
        Dictionary<string, MacroInfo> macros = [];
        List<string> order = [];
        HashSet<string> headers = [];
        string? header = null;
        foreach (string line in preprocessed.Split('\n'))
        {
            Match marker = LineMarkerPattern().Match(line);
            if (marker.Success)
            {
                string file = marker.Groups["file"].Value;
                header = file.StartsWith('<') ? null : RelativeHeader(Path.GetFullPath(file), root);
                if (header != null)
                {
                    headers.Add(header);
                }

                continue;
            }

            Match undefine = UndefinePattern().Match(line);
            if (undefine.Success)
            {
                macros.Remove(undefine.Groups["name"].Value);
                continue;
            }

            Match define = DefinePattern().Match(line);
            if (!define.Success || header == null)
            {
                continue;
            }

            string name = define.Groups["name"].Value;
            if (define.Groups["function"].Success)
            {
                macros.Remove(name);
                continue;
            }

            if (!macros.ContainsKey(name))
            {
                order.Add(name);
            }

            macros[name] = new MacroInfo(name, header, define.Groups["body"].Value.Trim());
        }

        List<MacroInfo> objectLike = order.Distinct().Where(macros.ContainsKey).Select(name => macros[name]).ToList();
        return (objectLike, headers);
    }

    private static string? RelativeHeader(string fullPath, string root)
    {
        return fullPath.StartsWith(root, StringComparison.Ordinal) ? fullPath[root.Length..] : null;
    }

    [GeneratedRegex(@"^# \d+ ""(?<file>[^""]+)""")]
    private static partial Regex LineMarkerPattern();

    [GeneratedRegex(@"^#define (?<name>\w+)(?<function>\()?(?<body>.*)$")]
    private static partial Regex DefinePattern();

    [GeneratedRegex(@"^#undef (?<name>\w+)")]
    private static partial Regex UndefinePattern();
}
