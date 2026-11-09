using System.Text.Json;
using System.Text.Json.Serialization;

namespace XdlSharp.Generator;

internal sealed class GeneratorConfig
{
    // The headers the probe includes; everything they include is bound unless excluded.
    public required List<string> Headers { get; init; }

    // The P/Invoke module of the functions declared under each include directory: SDL3 to XDL3.
    public required Dictionary<string, string> Libraries { get; init; }

    public required List<string> ExcludedHeaders { get; init; }

    // Functions bound from an excluded header anyway.
    public required List<string> ExcludedHeaderFunctions { get; init; }

    // Constant macros of bound headers that have no meaning in C#: macro to the reason that skipped.txt gives.
    public required Dictionary<string, string> ExcludedMacros { get; init; }

    // Function-like macros that src/XdlSharp/Xdl.Macros.cs ports by hand, so skipped.txt does not list them.
    public required List<string> PortedMacros { get; init; }

    // An integer typedef becomes a C# enum; these macros become its members.
    public required Dictionary<string, TypedefConstantsConfig> TypedefConstants { get; init; }

    // Functions that may return one of their string parameters, which the binding then returns as a span: function to parameter.
    public required Dictionary<string, string> ReturnsParameter { get; init; }

    // String parameters that SDL keeps after the call returns: Pointer<byte> instead of a span pinned only for the call.
    public required Dictionary<string, List<string>> RetainedParameters { get; init; }

    public static GeneratorConfig Load(string path)
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };
        return JsonSerializer.Deserialize<GeneratorConfig>(File.ReadAllText(path), options)!;
    }
}

internal sealed class TypedefConstantsConfig
{
    public required List<string> Prefixes { get; init; }

    public List<string> Exclude { get; init; } = [];

    public bool Flags { get; init; }
}
