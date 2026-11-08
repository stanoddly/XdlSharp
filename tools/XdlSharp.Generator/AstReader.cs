using System.Globalization;
using System.Text.Json;

namespace XdlSharp.Generator;

// Reads clang's -ast-dump=json. The dump omits "file" from a location when it equals the last file it printed, in document
// order and across nesting, so the reader replays that order to know which header each declaration is in.
internal sealed class AstReader(string includeDirectory, string probePath)
{
    public const string ProbeHeader = "<probe>";
    public const string SystemHeader = "<system>";
    public const string TypePrefix = "xdlsharp_type_";
    public const string ValuePrefix = "xdlsharp_value_";

    private readonly string _includeDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(includeDirectory)) + "/";
    private readonly string _probePath = Path.GetFullPath(probePath);
    private readonly Dictionary<string, CType> _probeTypes = [];
    private readonly Dictionary<string, Int128> _probeIntegers = [];
    private readonly Dictionary<string, string> _probeStrings = [];
    private readonly Dictionary<string, string> _probeFloats = [];
    private string? _lastFile;
    private RecordInfo? _pendingAnonymousRecord;
    private EnumInfo? _pendingAnonymousEnum;

    public AstModel Read(JsonElement root)
    {
        AstModel model = new();
        foreach (JsonElement node in root.GetProperty("inner").EnumerateArray())
        {
            string header = HeaderOf(TrackDeclaration(node));
            string kind = node.GetProperty("kind").GetString()!;
            if (node.TryGetProperty("isImplicit", out JsonElement isImplicit) && isImplicit.GetBoolean())
            {
                continue;
            }

            switch (kind)
            {
                case "FunctionDecl":
                    ReadFunction(model, node, header);
                    break;
                case "RecordDecl":
                    ReadTopLevelRecord(model, node, header);
                    break;
                case "EnumDecl":
                    ReadEnum(model, node, header);
                    break;
                case "TypedefDecl":
                    ReadTypedef(model, node, header);
                    break;
                case "VarDecl":
                    ReadProbeVariable(node, header);
                    break;
            }
        }

        foreach ((string name, CType type) in _probeTypes)
        {
            Int128? integer = _probeIntegers.TryGetValue(name, out Int128 value) ? value : null;
            model.MacroValues[name] = new MacroValue(type, integer, _probeStrings.GetValueOrDefault(name), _probeFloats.GetValueOrDefault(name));
        }

        return model;
    }

    private string? TrackDeclaration(JsonElement node)
    {
        string? declarationFile = null;
        foreach (JsonProperty property in node.EnumerateObject())
        {
            if (property.NameEquals("loc"))
            {
                Track(property.Value);
                declarationFile = _lastFile;
            }
            else if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                Track(property.Value);
            }
        }

        return declarationFile;
    }

    private void Track(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.NameEquals("file") && property.Value.ValueKind == JsonValueKind.String)
                {
                    _lastFile = property.Value.GetString();
                }
                else if (property.NameEquals("includedFrom"))
                {
                }
                else if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    Track(property.Value);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                Track(item);
            }
        }
    }

    private string HeaderOf(string? file)
    {
        if (file == null)
        {
            return SystemHeader;
        }

        string fullPath = Path.GetFullPath(file);
        if (fullPath == _probePath)
        {
            return ProbeHeader;
        }

        return fullPath.StartsWith(_includeDirectory, StringComparison.Ordinal) ? fullPath[_includeDirectory.Length..] : SystemHeader;
    }

    private static void ReadFunction(AstModel model, JsonElement node, string header)
    {
        string name = node.GetProperty("name").GetString()!;
        if (model.Functions.Any(function => function.Name == name))
        {
            return;
        }

        string returnType = CTypeParser.ReturnTypeOfFunction(node.GetProperty("type").GetProperty("qualType").GetString()!);
        List<ParameterInfo> parameters = [];
        foreach (JsonElement child in Children(node))
        {
            if (Kind(child) == "ParmVarDecl")
            {
                string? parameterName = child.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() : null;
                parameters.Add(new ParameterInfo(parameterName, CTypeParser.Parse(QualType(child))));
            }
        }

        bool isVariadic = node.TryGetProperty("variadic", out JsonElement variadic) && variadic.GetBoolean();
        bool isInline = (node.TryGetProperty("inline", out JsonElement inline) && inline.GetBoolean())
            || (node.TryGetProperty("storageClass", out JsonElement storage) && storage.GetString() == "static");
        model.Functions.Add(new FunctionInfo(name, header, CTypeParser.Parse(returnType), parameters, isVariadic, isInline));
    }

    private void ReadTopLevelRecord(AstModel model, JsonElement node, string header)
    {
        string? tag = node.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() : null;
        bool isUnion = node.GetProperty("tagUsed").GetString() == "union";
        bool isComplete = node.TryGetProperty("completeDefinition", out JsonElement complete) && complete.GetBoolean();
        RecordInfo record;
        if (tag != null && model.RecordsByTag.TryGetValue(tag, out RecordInfo? existing))
        {
            record = existing;
        }
        else
        {
            record = new RecordInfo(tag, isUnion, header);
            model.Records.Add(record);
            if (tag != null)
            {
                model.RecordsByTag[tag] = record;
            }
            else
            {
                _pendingAnonymousRecord = record;
            }
        }

        if (isComplete)
        {
            record.Header = header;
            record.IsComplete = true;
            ReadFields(node, record);
        }
    }

    private static void ReadFields(JsonElement node, RecordInfo record)
    {
        RecordInfo? pendingNested = null;
        foreach (JsonElement child in Children(node))
        {
            string kind = Kind(child);
            if (kind == "RecordDecl")
            {
                if (child.TryGetProperty("name", out _))
                {
                    throw new NotSupportedException($"A named record nested in {record.Tag} is not supported.");
                }

                pendingNested = new RecordInfo(null, child.GetProperty("tagUsed").GetString() == "union", record.Header) { IsComplete = true, Parent = record };
                ReadFields(child, pendingNested);
            }
            else if (kind == "FieldDecl")
            {
                if (child.TryGetProperty("isBitfield", out JsonElement isBitfield) && isBitfield.GetBoolean())
                {
                    throw new NotSupportedException($"The bit-field {record.Tag}.{child.GetProperty("name")} is not supported.");
                }

                string? fieldName = child.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() : null;
                if (fieldName == null)
                {
                    throw new NotSupportedException($"An anonymous member of {record.Tag} is not supported.");
                }

                string type = QualType(child);
                if (type.Contains("(unnamed", StringComparison.Ordinal) || type.Contains("(anonymous", StringComparison.Ordinal))
                {
                    if (pendingNested == null || !type.StartsWith(pendingNested.IsUnion ? "union (" : "struct (", StringComparison.Ordinal) || !type.EndsWith(')'))
                    {
                        throw new NotSupportedException($"The field {record.Tag}.{fieldName} of type '{type}' is not supported.");
                    }

                    pendingNested.ParentField = fieldName;
                    record.Fields.Add(new FieldInfo(fieldName, null, pendingNested));
                    pendingNested = null;
                }
                else
                {
                    record.Fields.Add(new FieldInfo(fieldName, CTypeParser.Parse(type), null));
                }
            }
        }
    }

    private void ReadEnum(AstModel model, JsonElement node, string header)
    {
        string? tag = node.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() : null;
        EnumInfo info = new(tag, header);
        Int128 next = 0;
        foreach (JsonElement child in Children(node))
        {
            if (Kind(child) != "EnumConstantDecl")
            {
                continue;
            }

            string name = child.GetProperty("name").GetString()!;
            Int128 value = FindConstantValue(child) ?? next;
            next = value + 1;
            if (header == ProbeHeader && name.StartsWith(ValuePrefix, StringComparison.Ordinal))
            {
                _probeIntegers[name[ValuePrefix.Length..]] = value;
            }

            info.Constants.Add(new EnumConstant(name, value));
        }

        if (header == ProbeHeader)
        {
            return;
        }

        if (tag != null)
        {
            if (!model.EnumsByTag.ContainsKey(tag))
            {
                model.EnumsByTag[tag] = info;
                model.Enums.Add(info);
            }
        }
        else
        {
            model.Enums.Add(info);
            _pendingAnonymousEnum = info;
        }
    }

    private static Int128? FindConstantValue(JsonElement node)
    {
        foreach (JsonElement child in Children(node))
        {
            if (Kind(child) == "ConstantExpr" && child.TryGetProperty("value", out JsonElement value))
            {
                return Int128.Parse(value.GetString()!, CultureInfo.InvariantCulture);
            }

            Int128? nested = FindConstantValue(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void ReadTypedef(AstModel model, JsonElement node, string header)
    {
        string name = node.GetProperty("name").GetString()!;
        string type = QualType(node);
        TypedefInfo typedef;
        if (type.Contains("(unnamed", StringComparison.Ordinal) || type.Contains("(anonymous", StringComparison.Ordinal))
        {
            if (type.StartsWith("enum", StringComparison.Ordinal) && _pendingAnonymousEnum != null)
            {
                _pendingAnonymousEnum.CSharpName = name;
                typedef = new TypedefInfo(name, header, new CNamedType(name, false, false), null, _pendingAnonymousEnum);
                _pendingAnonymousEnum = null;
            }
            else if (_pendingAnonymousRecord != null)
            {
                _pendingAnonymousRecord.CSharpName = name;
                _pendingAnonymousRecord.CSpelling = name;
                typedef = new TypedefInfo(name, header, new CNamedType(name, false, false), _pendingAnonymousRecord, null);
                _pendingAnonymousRecord = null;
            }
            else
            {
                throw new NotSupportedException($"The typedef {name} names an anonymous type the reader did not see.");
            }
        }
        else
        {
            typedef = new TypedefInfo(name, header, CTypeParser.Parse(type), null, null);
        }

        if (model.TypedefsByName.TryAdd(name, typedef))
        {
            model.Typedefs.Add(typedef);
        }
    }

    private void ReadProbeVariable(JsonElement node, string header)
    {
        string name = node.GetProperty("name").GetString()!;
        if (header != ProbeHeader || !name.StartsWith(TypePrefix, StringComparison.Ordinal))
        {
            return;
        }

        string macro = name[TypePrefix.Length..];
        _probeTypes[macro] = CTypeParser.Parse(QualType(node));
        JsonElement? literal = FindLiteral(node);
        if (literal is JsonElement found)
        {
            string value = found.GetProperty("value").GetString()!;
            if (Kind(found) == "StringLiteral")
            {
                _probeStrings[macro] = value;
            }
            else
            {
                _probeFloats[macro] = value;
            }
        }
    }

    // A string or floating initializer, through parentheses and implicit casts only.
    private static JsonElement? FindLiteral(JsonElement node)
    {
        foreach (JsonElement child in Children(node))
        {
            string kind = Kind(child);
            if (kind is "StringLiteral" or "FloatingLiteral")
            {
                return child;
            }

            if (kind is "ParenExpr" or "ImplicitCastExpr")
            {
                return FindLiteral(child);
            }
        }

        return null;
    }

    private static IEnumerable<JsonElement> Children(JsonElement node)
    {
        return node.TryGetProperty("inner", out JsonElement inner) ? inner.EnumerateArray() : [];
    }

    private static string Kind(JsonElement node)
    {
        return node.TryGetProperty("kind", out JsonElement kind) ? kind.GetString()! : "";
    }

    private static string QualType(JsonElement node)
    {
        return node.GetProperty("type").GetProperty("qualType").GetString()!;
    }
}
