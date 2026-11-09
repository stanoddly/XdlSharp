namespace XdlSharp.Generator;

internal sealed record ParameterInfo(string? Name, CType Type);

internal sealed record FunctionInfo(string Name, string Header, CType Return, IReadOnlyList<ParameterInfo> Parameters, bool IsVariadic, bool IsInline);

internal sealed record FieldInfo(string Name, CType? Type, RecordInfo? AnonymousRecord);

internal sealed class RecordInfo(string? tag, bool isUnion, string header)
{
    public string? Tag { get; } = tag;
    public bool IsUnion { get; } = isUnion;
    public string Header { get; set; } = header;
    public bool IsComplete { get; set; }
    public List<FieldInfo> Fields { get; } = [];

    // Set from a typedef for an anonymous tag, or from the parent field for a nested anonymous record.
    public string CSharpName { get; set; } = tag ?? "";

    // The C spelling for sizeof and offsetof: a typedef name, "struct X", or a member access for nested records.
    public string? CSpelling { get; set; }
    public RecordInfo? Parent { get; set; }
    public string? ParentField { get; set; }
}

internal sealed record EnumConstant(string Name, Int128 Value);

internal sealed class EnumInfo(string? tag, string header)
{
    public string? Tag { get; } = tag;
    public string Header { get; } = header;
    public string CSharpName { get; set; } = tag ?? "";
    public List<EnumConstant> Constants { get; } = [];
}

internal sealed record TypedefInfo(string Name, string Header, CType Underlying, RecordInfo? AnonymousRecord, EnumInfo? AnonymousEnum);

// Body is the text after the name; a function-like macro's starts with its parameters, without the opening parenthesis.
internal sealed record MacroInfo(string Name, string Header, string Body, bool IsFunctionLike);

// What clang made of a macro in the probe: its C type and, for integers, strings and floats, its value.
internal sealed record MacroValue(CType Type, Int128? Integer, string? String, string? Floating);

internal sealed class AstModel
{
    public List<FunctionInfo> Functions { get; } = [];
    public List<RecordInfo> Records { get; } = [];
    public Dictionary<string, RecordInfo> RecordsByTag { get; } = [];
    public List<EnumInfo> Enums { get; } = [];
    public Dictionary<string, EnumInfo> EnumsByTag { get; } = [];
    public List<TypedefInfo> Typedefs { get; } = [];
    public Dictionary<string, TypedefInfo> TypedefsByName { get; } = [];
    public Dictionary<string, MacroValue> MacroValues { get; } = [];
}
