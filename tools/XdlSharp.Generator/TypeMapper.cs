namespace XdlSharp.Generator;

// A C type with typedefs resolved to what the binding needs.
internal abstract record MType;

// CName is the C spelling (char, bool, void, ...), CSharp the C# keyword.
internal sealed record MPrimitive(string CName, string CSharp, bool IsConst) : MType;

internal sealed record MEnum(string CSharpName) : MType;

internal sealed record MRecord(RecordInfo Record) : MType;

internal sealed record MPointer(MType Pointee) : MType;

internal sealed record MArray(MType Element, int Length) : MType;

internal sealed record MFunction(MType Return, IReadOnlyList<MType> Parameters, bool IsVariadic) : MType;

// wchar_t and va_list: their size differs between platforms, so the binding only passes them behind a pointer or skips them.
internal sealed record MUnsupported(string CName) : MType;

internal enum Usage
{
    // A struct field: pointers are Pointer<T>, bool is bool.
    Field,
    // A public method: like a field.
    Public,
    // A DllImport: pointers are T*, bool is byte.
    Raw,
    // A function pointer parameter or return: like Raw.
    Callback,
}

internal sealed class TypeMapper(AstModel model, IReadOnlySet<string> typedefEnums)
{
    private static readonly Dictionary<string, string> Primitives = new()
    {
        ["void"] = "void",
        ["bool"] = "bool",
        ["_Bool"] = "bool",
        ["char"] = "byte",
        ["signed char"] = "sbyte",
        ["unsigned char"] = "byte",
        ["short"] = "short",
        ["unsigned short"] = "ushort",
        ["int"] = "int",
        ["unsigned int"] = "uint",
        ["long long"] = "long",
        ["unsigned long long"] = "ulong",
        ["float"] = "float",
        ["double"] = "double",
        ["Uint8"] = "byte",
        ["Sint8"] = "sbyte",
        ["Uint16"] = "ushort",
        ["Sint16"] = "short",
        ["Uint32"] = "uint",
        ["Sint32"] = "int",
        ["Uint64"] = "ulong",
        ["Sint64"] = "long",
        ["int8_t"] = "sbyte",
        ["uint8_t"] = "byte",
        ["int16_t"] = "short",
        ["uint16_t"] = "ushort",
        ["int32_t"] = "int",
        ["uint32_t"] = "uint",
        ["int64_t"] = "long",
        ["uint64_t"] = "ulong",
        ["size_t"] = "nuint",
        ["intptr_t"] = "nint",
        ["uintptr_t"] = "nuint",
        ["ptrdiff_t"] = "nint",
    };

    // long is 32 bits on Windows and 64 bits elsewhere, so it is allowed only in macro values, which are evaluated once.
    private static readonly Dictionary<string, string> MacroOnlyPrimitives = new()
    {
        ["long"] = "long",
        ["unsigned long"] = "ulong",
    };

    public static bool IsPrimitiveName(string name)
    {
        return Primitives.ContainsKey(name) || MacroOnlyPrimitives.ContainsKey(name);
    }

    public MType Resolve(CType type, bool allowMacroOnly = false)
    {
        switch (type)
        {
            case CPointerType pointer:
                return new MPointer(Resolve(pointer.Pointee, allowMacroOnly));
            case CArrayType array:
                return new MArray(Resolve(array.Element, allowMacroOnly), array.Length);
            case CFunctionType function:
                return new MFunction(Resolve(function.Return), function.Parameters.Select(parameter => Resolve(parameter)).ToList(), function.IsVariadic);
            case CNamedType named:
                return ResolveNamed(named, allowMacroOnly);
            default:
                throw new NotSupportedException($"Unknown C type {type}.");
        }
    }

    private MType ResolveNamed(CNamedType named, bool allowMacroOnly)
    {
        if (named.Name is "wchar_t")
        {
            return new MUnsupported(named.Name);
        }

        if (named.Name is "va_list" or "__builtin_va_list" or "__va_list_tag")
        {
            return new MUnsupported("va_list");
        }

        if (named.IsTag)
        {
            if (model.RecordsByTag.TryGetValue(named.Name, out RecordInfo? record))
            {
                return new MRecord(record);
            }

            if (model.EnumsByTag.TryGetValue(named.Name, out EnumInfo? enumInfo))
            {
                return new MEnum(enumInfo.CSharpName);
            }

            throw new NotSupportedException($"Unknown tag {named.Name}.");
        }

        if (Primitives.TryGetValue(named.Name, out string? primitive))
        {
            return new MPrimitive(named.Name, primitive, named.IsConst);
        }

        if (allowMacroOnly && MacroOnlyPrimitives.TryGetValue(named.Name, out string? macroOnly))
        {
            return new MPrimitive(named.Name, macroOnly, named.IsConst);
        }

        if (typedefEnums.Contains(named.Name))
        {
            return new MEnum(named.Name);
        }

        if (model.TypedefsByName.TryGetValue(named.Name, out TypedefInfo? typedef))
        {
            if (typedef.AnonymousRecord != null)
            {
                return new MRecord(typedef.AnonymousRecord);
            }

            if (typedef.AnonymousEnum != null)
            {
                return new MEnum(typedef.AnonymousEnum.CSharpName);
            }

            MType underlying = Resolve(typedef.Underlying, allowMacroOnly);
            return named.IsConst && underlying is MPrimitive underlyingPrimitive ? underlyingPrimitive with { IsConst = true } : underlying;
        }

        throw new NotSupportedException($"Unknown C type name {named.Name}.");
    }

    // The integer that a typedef finally names, for typedefs that become enums.
    public string? IntegerOf(CType type)
    {
        MType resolved;
        try
        {
            resolved = Resolve(type);
        }
        catch (NotSupportedException)
        {
            return null;
        }

        return resolved is MPrimitive { CSharp: "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" } primitive ? primitive.CSharp : null;
    }

    public string Render(MType type, Usage usage)
    {
        switch (type)
        {
            case MPrimitive { CSharp: "bool" } when usage is Usage.Raw or Usage.Callback:
                return "byte";
            case MPrimitive primitive:
                return primitive.CSharp;
            case MEnum enumType:
                return enumType.CSharpName;
            case MRecord record:
                return record.Record.CSharpName;
            case MPointer pointer:
                return RenderPointer(pointer, usage);
            default:
                throw new NotSupportedException($"{Describe(type)} cannot be rendered as a {usage} type.");
        }
    }

    // A pointee keeps bool as bool: it is memory that C reads as one byte, not a value passed in a register.
    private string RenderPointee(MType pointee, Usage usage)
    {
        if (pointee is MPrimitive { CSharp: "bool" })
        {
            return "bool";
        }

        return Render(pointee, usage);
    }

    private string RenderPointer(MPointer pointer, Usage usage)
    {
        MType pointee = pointer.Pointee;
        if (pointee is MPrimitive { CSharp: "void" } or MUnsupported { CName: "wchar_t" })
        {
            return "nint";
        }

        // Mono's wasm interpreter cannot call a P/Invoke whose signature has a function pointer type, so one crosses the native
        // boundary as nint and keeps its delegate* type in public signatures and fields.
        if (pointee is MFunction function)
        {
            return usage is Usage.Raw or Usage.Callback ? "nint" : RenderFunctionPointer(function);
        }

        if (pointee is MUnsupported unsupported)
        {
            throw new NotSupportedException($"A pointer to {unsupported.CName} is not supported.");
        }

        // A function pointer cannot be a type argument, so a pointer to one stays a C# pointer in every usage.
        if (usage is Usage.Raw or Usage.Callback || pointee is MPointer { Pointee: MFunction })
        {
            return RenderPointee(pointee, usage) + "*";
        }

        return $"Pointer<{RenderPointee(pointee, usage)}>";
    }

    public string RenderFunctionPointer(MFunction function)
    {
        if (function.IsVariadic)
        {
            throw new NotSupportedException("A variadic function pointer is not supported.");
        }

        IEnumerable<string> parts = function.Parameters.Select(parameter => Render(parameter, Usage.Callback)).Append(Render(function.Return, Usage.Callback));
        return $"delegate* unmanaged[Cdecl]<{string.Join(", ", parts)}>";
    }

    public static string Describe(MType type)
    {
        return type switch
        {
            MPrimitive primitive => primitive.CName,
            MEnum enumType => enumType.CSharpName,
            MRecord record => record.Record.CSharpName,
            MPointer pointer => Describe(pointer.Pointee) + " *",
            MArray array => $"{Describe(array.Element)}[{array.Length}]",
            MFunction => "function",
            MUnsupported unsupported => unsupported.CName,
            _ => type.ToString(),
        };
    }

    // A C array of these becomes a fixed buffer; of anything else, an [InlineArray] struct.
    public static bool IsFixedBufferElement(MType element)
    {
        return element is MPrimitive { CSharp: "bool" or "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or "float" or "double" };
    }

    public static bool IsConstCharPointer(MType type)
    {
        return type is MPointer { Pointee: MPrimitive { CName: "char", IsConst: true } };
    }

    // The C name of the first type in type that the binding cannot pass, or null.
    public static string? FindUnsupported(MType type)
    {
        return type switch
        {
            MUnsupported unsupported => unsupported.CName,
            MPointer { Pointee: MUnsupported { CName: "wchar_t" } } => null,
            MPointer pointer => FindUnsupported(pointer.Pointee),
            MArray array => FindUnsupported(array.Element),
            MFunction function => function.Parameters.Prepend(function.Return).Select(FindUnsupported).FirstOrDefault(name => name != null),
            _ => null,
        };
    }
}
