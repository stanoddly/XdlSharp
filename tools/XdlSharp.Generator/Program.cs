using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using XdlSharp.Generator;

// Generates the bindings from the headers of the XDL release in XDL_VERSION, which building this project downloads.
// usage: dotnet run --project tools/XdlSharp.Generator -- [--include <dir>] [--clang <path>] [--check]
// --check writes no checked-in file and fails when a generated file differs, so CI can tell that the checked-in code is current.

// The bindings follow the declarations that the headers make for Linux, and other targets see other declarations.
if (!OperatingSystem.IsLinux())
{
    Console.Error.WriteLine("The generator parses the headers for Linux, so it runs only on Linux.");
    return 1;
}

string root = FindRepositoryRoot();
string xdlVersion = File.ReadAllText(Path.Combine(root, "XDL_VERSION")).Trim();
string includeDirectory = Path.Combine(root, "artifacts", "xdl", xdlVersion, "include");
bool check = false;
for (int index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--include":
            includeDirectory = Path.GetFullPath(args[++index]);
            break;
        case "--clang":
            Clang.Executable = args[++index];
            break;
        case "--check":
            check = true;
            break;
        default:
            Console.Error.WriteLine($"Unknown argument {args[index]}.");
            return 2;
    }
}

GeneratorConfig config = GeneratorConfig.Load(Path.Combine(root, "tools", "XdlSharp.Generator", "generator.json"));
string workDirectory = Path.Combine(root, "artifacts", "generator");
Directory.CreateDirectory(workDirectory);
List<string> problems = [];
List<string> skipped = [];

string includes = string.Concat(config.Headers.Select(header => $"#include <{header}>\n"));
string entryPath = Path.Combine(workDirectory, "entry.c");
File.WriteAllText(entryPath, includes);
(List<MacroInfo> macros, HashSet<string> headers) = MacroScanner.Scan(Clang.Preprocess(entryPath, includeDirectory), includeDirectory);

foreach (string header in config.ExcludedHeaders.Where(header => !headers.Contains(header)))
{
    problems.Add($"The excluded header {header} is not included by the configured headers.");
}

bool IsBound(string header)
{
    return headers.Contains(header) && !config.ExcludedHeaders.Contains(header);
}

Dictionary<string, MacroInfo> macrosByName = macros.ToDictionary(macro => macro.Name);

// SDL_ASSERT_FILE expands to SDL_FILE, which expands to __FILE_NAME__: the probe would bind the name of its own file.
bool UsesBuiltin(MacroInfo macro, HashSet<string> visited)
{
    return macro.Body.Contains("__", StringComparison.Ordinal) || IdentifierPattern().Matches(macro.Body)
        .Any(match => macrosByName.TryGetValue(match.Value, out MacroInfo? used) && visited.Add(used.Name) && UsesBuiltin(used, visited));
}

foreach (string name in config.ExcludedMacros.Keys.Where(name => !macrosByName.TryGetValue(name, out MacroInfo? macro) || macro.IsFunctionLike || !IsBound(macro.Header)))
{
    problems.Add($"excludedMacros names {name}, which is not an object-like macro of a bound header.");
}

foreach (string name in config.PortedMacros.Where(name => !macrosByName.TryGetValue(name, out MacroInfo? macro) || !macro.IsFunctionLike || !IsBound(macro.Header)))
{
    problems.Add($"portedMacros names {name}, which is not a function-like macro of a bound header.");
}

// The probe evaluates every candidate macro twice: __auto_type keeps its C type, and an enum makes clang print its value.
// A macro whose expansion does not compile as an expression is dropped; clang names its line.
List<MacroInfo> candidates = [];
foreach (MacroInfo macro in macros.Where(macro => IsBound(macro.Header)))
{
    if (macro.IsFunctionLike)
    {
        if (!config.PortedMacros.Contains(macro.Name))
        {
            skipped.Add($"{macro.Header}: {macro.Name}: function-like macro");
        }

        continue;
    }

    if (macro.Body.Length == 0)
    {
        continue;
    }

    if (config.ExcludedMacros.TryGetValue(macro.Name, out string? reason))
    {
        skipped.Add($"{macro.Header}: {macro.Name}: {reason}");
        continue;
    }

    if (UsesBuiltin(macro, [macro.Name]))
    {
        skipped.Add($"{macro.Header}: {macro.Name}: uses a compiler builtin");
        continue;
    }

    candidates.Add(macro);
}

string probePath = Path.Combine(workDirectory, "probe.c");
int firstProbeLine = includes.Count(character => character == '\n') + 1;
for (int attempt = 0; ; attempt++)
{
    StringBuilder probe = new(includes);
    foreach (MacroInfo macro in candidates)
    {
        probe.Append("static const __auto_type ").Append(AstReader.TypePrefix).Append(macro.Name).Append(" = (").Append(macro.Name).Append("); ");
        probe.Append("enum { ").Append(AstReader.ValuePrefix).Append(macro.Name).Append(" = _Generic((").Append(macro.Name)
            .Append("), char *: 0, const char *: 0, float: 0, double: 0, void *: 0, default: (").Append(macro.Name).Append(")) };\n");
    }

    File.WriteAllText(probePath, probe.ToString());
    HashSet<int> errorLines = Clang.ErrorLines(probePath, includeDirectory);
    if (errorLines.Count == 0)
    {
        break;
    }

    if (attempt == 5 || errorLines.Any(line => line < firstProbeLine || line >= firstProbeLine + candidates.Count))
    {
        throw new InvalidOperationException($"The probe does not compile; see {probePath}.");
    }

    List<MacroInfo> failed = errorLines.Select(line => candidates[line - firstProbeLine]).ToList();
    foreach (MacroInfo macro in failed)
    {
        skipped.Add($"{macro.Header}: {macro.Name}: not a constant expression");
    }

    candidates = candidates.Except(failed).ToList();
}

AstModel model;
using (JsonDocument document = Clang.DumpAst(probePath, includeDirectory))
{
    model = new AstReader(includeDirectory, probePath).Read(document.RootElement);
}

// An integer typedef of a bound header becomes an enum of the same name, so SDL_WindowID and SDL_PropertiesID stay distinct types.
TypeMapper plainMapper = new(model, new HashSet<string>());
Dictionary<string, TypedefEnum> typedefEnums = [];
foreach (TypedefInfo typedef in model.Typedefs.Where(typedef => IsBound(typedef.Header) && !TypeMapper.IsPrimitiveName(typedef.Name)))
{
    string? integer = plainMapper.IntegerOf(typedef.Underlying);
    if (integer != null)
    {
        bool isFlags = config.TypedefConstants.TryGetValue(typedef.Name, out TypedefConstantsConfig? constantsConfig) && constantsConfig.Flags;
        typedefEnums[typedef.Name] = new TypedefEnum(typedef.Name, typedef.Header, integer, isFlags, []);
    }
}

TypeMapper mapper = new(model, typedefEnums.Keys.ToHashSet());

// Constants: a macro joins the typedef enum whose prefix it starts with, or else becomes a constant of Xdl.
List<MacroInfo> constants = [];
HashSet<string> usedExclusions = [];
foreach (MacroInfo macro in candidates)
{
    MacroValue value = model.MacroValues[macro.Name];
    MType type;
    try
    {
        type = mapper.Resolve(value.Type, allowMacroOnly: true);
    }
    catch (NotSupportedException exception)
    {
        skipped.Add($"{macro.Header}: {macro.Name}: {exception.Message}");
        continue;
    }

    bool isInteger = value.Integer != null && type is MEnum or MPrimitive { CSharp: not ("float" or "double" or "bool" or "void") };
    bool isString = value.String != null && type is MPointer { Pointee: MPrimitive { CName: "char" } };
    bool isFloat = value.Floating != null && type is MPrimitive { CSharp: "float" or "double" };
    if (!isInteger && !isString && !isFloat)
    {
        string reason = type is MPrimitive { CSharp: "float" or "double" } ? "a floating expression, not a literal" : $"a {TypeMapper.Describe(type)}, not an integer, string or floating constant";
        skipped.Add($"{macro.Header}: {macro.Name}: {reason}");
        continue;
    }

    string? owner = null;
    if (isInteger)
    {
        foreach ((string typedefName, TypedefConstantsConfig constantsConfig) in config.TypedefConstants)
        {
            if (!constantsConfig.Prefixes.Any(prefix => macro.Name.StartsWith(prefix, StringComparison.Ordinal)))
            {
                continue;
            }

            if (constantsConfig.Exclude.Contains(macro.Name))
            {
                usedExclusions.Add(macro.Name);
                continue;
            }

            if (owner != null)
            {
                problems.Add($"{macro.Name} matches the prefixes of both {owner} and {typedefName}.");
            }

            owner = typedefName;
        }
    }

    if (owner != null && typedefEnums.TryGetValue(owner, out TypedefEnum? typedefEnum))
    {
        typedefEnum.Members.Add(new EnumConstant(macro.Name, value.Integer!.Value));
    }
    else
    {
        constants.Add(macro);
    }
}

foreach ((string typedefName, TypedefConstantsConfig constantsConfig) in config.TypedefConstants)
{
    if (!typedefEnums.TryGetValue(typedefName, out TypedefEnum? typedefEnum))
    {
        problems.Add($"typedefConstants names {typedefName}, which is not an integer typedef of a bound header.");
        continue;
    }

    foreach (string prefix in constantsConfig.Prefixes.Where(prefix => !typedefEnum.Members.Any(member => member.Name.StartsWith(prefix, StringComparison.Ordinal))))
    {
        problems.Add($"The prefix {prefix} of {typedefName} matches no macro.");
    }

    foreach (string exclusion in constantsConfig.Exclude.Where(exclusion => !usedExclusions.Contains(exclusion)))
    {
        problems.Add($"The exclusion {exclusion} of {typedefName} matches no macro.");
    }
}

// Functions: exported ones of bound headers, and the few that the config keeps from excluded headers.
List<FunctionInfo> functions = [];
foreach (FunctionInfo function in model.Functions)
{
    bool isKept = config.ExcludedHeaderFunctions.Contains(function.Name);
    if (!IsBound(function.Header) && !isKept)
    {
        continue;
    }

    if (function.IsInline)
    {
        skipped.Add($"{function.Header}: {function.Name}: inline, not exported");
        continue;
    }

    if (function.IsVariadic)
    {
        skipped.Add($"{function.Header}: {function.Name}: variadic");
        continue;
    }

    List<MType> types = function.Parameters.Select(parameter => mapper.Resolve(parameter.Type)).Prepend(mapper.Resolve(function.Return)).ToList();
    string? unsupported = types.Select(TypeMapper.FindUnsupported).FirstOrDefault(name => name != null);
    if (unsupported != null)
    {
        skipped.Add($"{function.Header}: {function.Name}: takes a {unsupported}");
        continue;
    }

    if (BindingWriter.LibraryOf(config, function.Header) == null)
    {
        problems.Add($"libraries names no library for {function.Header}, which declares {function.Name}.");
    }

    functions.Add(function);
}

foreach (string directory in config.Libraries.Keys.Where(directory => !functions.Any(function => function.Header.StartsWith(directory + "/", StringComparison.Ordinal))))
{
    problems.Add($"libraries names {directory}, which declares no bound function.");
}

foreach (string name in config.ExcludedHeaderFunctions.Where(name => !functions.Any(function => function.Name == name && !IsBound(function.Header))))
{
    problems.Add($"excludedHeaderFunctions names {name}, which is not an exported function of an excluded header.");
}

foreach ((string functionName, string parameterName) in config.ReturnsParameter)
{
    FunctionInfo? function = functions.FirstOrDefault(function => function.Name == functionName);
    ParameterInfo? parameter = function?.Parameters.FirstOrDefault(parameter => parameter.Name == parameterName);
    bool isRetained = config.RetainedParameters.GetValueOrDefault(functionName, []).Contains(parameterName);
    if (function == null || parameter == null || !TypeMapper.IsConstCharPointer(mapper.Resolve(function.Return)) || !TypeMapper.IsConstCharPointer(mapper.Resolve(parameter.Type)) || isRetained)
    {
        problems.Add($"returnsParameter names {functionName}({parameterName}), which is not a const char * parameter, outside retainedParameters, of a bound function that returns const char *.");
    }
}

foreach ((string functionName, List<string> parameterNames) in config.RetainedParameters)
{
    FunctionInfo? function = functions.FirstOrDefault(function => function.Name == functionName);
    foreach (string parameterName in parameterNames)
    {
        ParameterInfo? parameter = function?.Parameters.FirstOrDefault(parameter => parameter.Name == parameterName);
        if (parameter == null || !TypeMapper.IsConstCharPointer(mapper.Resolve(parameter.Type)))
        {
            problems.Add($"retainedParameters names {functionName}({parameterName}), which is not a const char * parameter of a bound function.");
        }
    }
}

if (problems.Count > 0)
{
    foreach (string problem in problems)
    {
        Console.Error.WriteLine(problem);
    }

    return 1;
}

// One file per header, in the order the headers declare things.
string HeaderFile(string header)
{
    return Path.GetFileNameWithoutExtension(header) + ".g.cs";
}

Dictionary<string, HeaderContent> contents = [];
HeaderContent ContentOf(string header)
{
    if (!contents.TryGetValue(header, out HeaderContent? content))
    {
        content = new HeaderContent([], [], [], [], []);
        contents[header] = content;
    }

    return content;
}

List<(string CSharpName, string CSpelling)> layoutEnums = [];
foreach (EnumInfo enumInfo in model.Enums.Where(enumInfo => IsBound(enumInfo.Header)))
{
    if (enumInfo.CSharpName.Length == 0)
    {
        throw new NotSupportedException($"An anonymous enum without a typedef in {enumInfo.Header} is not supported.");
    }

    ContentOf(enumInfo.Header).Enums.Add(enumInfo);
    bool hasTypedef = model.TypedefsByName.TryGetValue(enumInfo.CSharpName, out TypedefInfo? typedef)
        && (typedef.AnonymousEnum == enumInfo || typedef.Underlying is CNamedType { IsTag: true } named && named.Name == enumInfo.Tag);
    layoutEnums.Add((enumInfo.CSharpName, hasTypedef ? enumInfo.CSharpName : "enum " + enumInfo.Tag));
}

foreach (TypedefEnum typedefEnum in typedefEnums.Values)
{
    ContentOf(typedefEnum.Header).TypedefEnums.Add(typedefEnum);
    layoutEnums.Add((typedefEnum.Name, typedefEnum.Name));
}

foreach (RecordInfo record in model.Records.Where(record => IsBound(record.Header)))
{
    ContentOf(record.Header).Records.Add(record);
}

foreach (MacroInfo constant in constants)
{
    ContentOf(constant.Header).Constants.Add(constant);
}

foreach (FunctionInfo function in functions)
{
    ContentOf(function.Header).Functions.Add(function);
}

BindingWriter writer = new(model, mapper, config);
Dictionary<string, string> outputs = [];
string generatedDirectory = Path.Combine(root, "src", "XdlSharp", "Generated");
foreach ((string header, HeaderContent content) in contents.OrderBy(pair => pair.Key, StringComparer.Ordinal))
{
    if (!outputs.TryAdd(Path.Combine(generatedDirectory, HeaderFile(header)), writer.Write(content)))
    {
        Console.Error.WriteLine($"{header} and another bound header would both generate {HeaderFile(header)}.");
        return 1;
    }
}

skipped.Sort(StringComparer.Ordinal);
outputs[Path.Combine(generatedDirectory, "skipped.txt")] = "# Declarations of the bound headers that have no binding, and why.\n" + string.Join('\n', skipped) + "\n";

(string layoutC, string layoutCSharp) = new LayoutWriter(model, mapper).Write(config.Headers, writer.WrittenRecords, layoutEnums);
outputs[Path.Combine(root, "tests", "native", "xdlsharp_layout.c")] = layoutC;
outputs[Path.Combine(root, "tests", "XdlSharp.Checks", "Generated", "ManagedLayout.g.cs")] = layoutCSharp;

HashSet<string> stale = Directory.GetFiles(generatedDirectory).Where(path => !outputs.ContainsKey(path)).ToHashSet();
List<string> differences = outputs.Where(pair => !File.Exists(pair.Key) || File.ReadAllText(pair.Key) != pair.Value).Select(pair => pair.Key).Concat(stale).ToList();
if (check)
{
    foreach (string path in differences)
    {
        Console.Error.WriteLine($"Out of date: {Path.GetRelativePath(root, path)}");
    }

    return differences.Count == 0 ? 0 : 1;
}

foreach (string path in stale)
{
    File.Delete(path);
}

foreach ((string path, string text) in outputs)
{
    File.WriteAllText(path, text);
}

Console.WriteLine($"{functions.Count} functions, {writer.WrittenRecords.Count} structs, {layoutEnums.Count} enums, {constants.Count} constants; {skipped.Count} skipped.");
return 0;

static string FindRepositoryRoot()
{
    string? directory = Directory.GetCurrentDirectory();
    while (directory != null && !File.Exists(Path.Combine(directory, "XdlSharp.slnx")))
    {
        directory = Path.GetDirectoryName(directory);
    }

    return directory ?? throw new InvalidOperationException("Run the generator inside the XdlSharp repository.");
}

internal partial class Program
{
    [GeneratedRegex(@"\b[A-Za-z_][A-Za-z_0-9]*\b")]
    private static partial Regex IdentifierPattern();
}
