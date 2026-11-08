using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XdlSharp.Generator;

internal static partial class Clang
{
    public static string Executable { get; set; } = "clang";

    public static string Preprocess(string sourcePath, string includeDirectory)
    {
        (int exitCode, string output, string errors) = Run(["-std=c23", "-E", "-dD", "-I", includeDirectory, sourcePath]);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"clang -E failed:\n{errors}");
        }

        return output;
    }

    // The line numbers of sourcePath that an error, or a note of an error, points at.
    public static HashSet<int> ErrorLines(string sourcePath, string includeDirectory)
    {
        (int exitCode, _, string errors) = Run(["-std=c23", "-fsyntax-only", "-w", "-ferror-limit=0", "-fmacro-backtrace-limit=0", "-I", includeDirectory, sourcePath]);
        HashSet<int> lines = [];
        if (exitCode == 0)
        {
            return lines;
        }

        string fileName = Path.GetFileName(sourcePath);
        bool inError = false;
        foreach (string line in errors.Split('\n'))
        {
            Match match = DiagnosticPattern().Match(line);
            if (!match.Success)
            {
                continue;
            }

            string severity = match.Groups["severity"].Value;
            if (severity is "error" or "fatal error")
            {
                inError = true;
            }
            else if (severity == "warning")
            {
                inError = false;
            }

            if (inError && Path.GetFileName(match.Groups["file"].Value) == fileName)
            {
                lines.Add(int.Parse(match.Groups["line"].Value));
            }
        }

        if (lines.Count == 0)
        {
            throw new InvalidOperationException($"clang failed without an error in {fileName}:\n{errors}");
        }

        return lines;
    }

    public static JsonDocument DumpAst(string sourcePath, string includeDirectory)
    {
        ProcessStartInfo startInfo = StartInfo(["-std=c23", "-fsyntax-only", "-w", "-Xclang", "-ast-dump=json", "-I", includeDirectory, sourcePath]);
        using Process process = Process.Start(startInfo)!;
        Task<string> errors = process.StandardError.ReadToEndAsync();
        JsonDocument document = JsonDocument.Parse(process.StandardOutput.BaseStream, new JsonDocumentOptions { MaxDepth = 4096 });
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"clang -ast-dump=json failed:\n{errors.Result}");
        }

        return document;
    }

    private static (int ExitCode, string Output, string Errors) Run(IEnumerable<string> arguments)
    {
        using Process process = Process.Start(StartInfo(arguments))!;
        Task<string> errors = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, errors.Result);
    }

    private static ProcessStartInfo StartInfo(IEnumerable<string> arguments)
    {
        ProcessStartInfo startInfo = new(Executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    [GeneratedRegex(@"^(?<file>[^:\s][^:]*):(?<line>\d+):\d+: (?<severity>fatal error|error|warning|note):")]
    private static partial Regex DiagnosticPattern();
}
