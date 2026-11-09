using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static XdlSharp.Xdl;

namespace XdlSharp.Checks;

public sealed class CheckFailedException(string message) : Exception(message);

// Each check throws CheckFailedException when the bindings and XDL disagree. They run under NUnit on CoreCLR and from
// XdlSharp.Browser.Tests on Mono in the browser, where the one-byte bool once arrived with stale upper bytes.
public static unsafe class Checks
{
    public static IReadOnlyList<(string Name, Action Run)> All { get; } =
    [
        (nameof(LayoutsMatchC), LayoutsMatchC),
        (nameof(BooleansCrossBothWays), BooleansCrossBothWays),
        (nameof(FloatsCrossBothWays), FloatsCrossBothWays),
        (nameof(StringParametersArriveTerminated), StringParametersArriveTerminated),
        (nameof(StringPropertyReturnsNativeValue), StringPropertyReturnsNativeValue),
        (nameof(StringPropertyReturnsCallerDefault), StringPropertyReturnsCallerDefault),
        (nameof(StringPropertyDefaultSurvivesCompaction), StringPropertyDefaultSurvivesCompaction),
        (nameof(DefaultSpanPassesNull), DefaultSpanPassesNull),
        (nameof(ErrorIsReadable), ErrorIsReadable),
        (nameof(StructsCrossByValue), StructsCrossByValue),
        (nameof(CallbacksReachManagedCode), CallbacksReachManagedCode),
        (nameof(PixelFormatMacrosMatchC), PixelFormatMacrosMatchC),
        (nameof(InitInterfaceSetsVersion), InitInterfaceSetsVersion),
        (nameof(SatellitesMatchTheirHeaders), SatellitesMatchTheirHeaders),
    ];

    public static void LayoutsMatchC()
    {
        Dictionary<string, nuint> native = NativeLayout.Get().ToDictionary(entry => entry.Name, entry => entry.Value);
        Dictionary<string, nuint> managed = ManagedLayout.Get().ToDictionary(entry => entry.Name, entry => entry.Value);
        List<string> differences = [];
        foreach ((string name, nuint value) in native)
        {
            if (!managed.TryGetValue(name, out nuint managedValue))
            {
                differences.Add($"{name}: C {value}, missing in C#");
            }
            else if (managedValue != value)
            {
                differences.Add($"{name}: C {value}, C# {managedValue}");
            }
        }

        differences.AddRange(managed.Keys.Except(native.Keys).Select(name => $"{name}: missing in C"));
        Expect(differences.Count == 0, $"{differences.Count} of {native.Count} sizes and offsets differ:\n{string.Join('\n', differences)}");
    }

    public static void BooleansCrossBothWays()
    {
        SDL_PropertiesID props = SDL_CreateProperties();
        try
        {
            Expect(SDL_SetBooleanProperty(props, "on"u8, true), "SDL_SetBooleanProperty returned false");
            Expect(SDL_SetBooleanProperty(props, "off"u8, false), "SDL_SetBooleanProperty returned false");
            for (int index = 0; index < 1000; index++)
            {
                Expect(SDL_GetBooleanProperty(props, "on"u8, false), "a true property read as false");
                Expect(!SDL_GetBooleanProperty(props, "off"u8, true), "a false property read as true");
                Expect(!SDL_GetBooleanProperty(props, "missing"u8, false), "the false default read as true");
                Expect(SDL_GetBooleanProperty(props, "missing"u8, true), "the true default read as false");
                Expect(!SDL_HasProperty(props, "missing"u8), "SDL_HasProperty returned true for a missing property");
            }
        }
        finally
        {
            SDL_DestroyProperties(props);
        }
    }

    public static void FloatsCrossBothWays()
    {
        SDL_PropertiesID props = SDL_CreateProperties();
        try
        {
            Expect(SDL_SetFloatProperty(props, "value"u8, 1.5f), "SDL_SetFloatProperty returned false");
            Expect(SDL_GetFloatProperty(props, "value"u8, 0) == 1.5f, "the float property read back as another value");
            Expect(SDL_GetFloatProperty(props, "missing"u8, -2.25f) == -2.25f, "the float default read back as another value");
        }
        finally
        {
            SDL_DestroyProperties(props);
        }
    }

    // A u8 literal carries a terminator after its last byte, and stackalloc memory can be terminated by hand.
    public static void StringParametersArriveTerminated()
    {
        SDL_PropertiesID props = SDL_CreateProperties();
        try
        {
            Span<byte> name = stackalloc byte[] { (byte)'k', (byte)'e', (byte)'y', 0 };
            Expect(SDL_SetNumberProperty(props, name, 42), "SDL_SetNumberProperty returned false");
            Expect(SDL_GetNumberProperty(props, "key"u8, 0) == 42, "the property set through a stackalloc name is not found by a literal");
        }
        finally
        {
            SDL_DestroyProperties(props);
        }
    }

    public static void StringPropertyReturnsNativeValue()
    {
        SDL_PropertiesID props = SDL_CreateProperties();
        try
        {
            Expect(SDL_SetStringProperty(props, "name"u8, "native value"u8), "SDL_SetStringProperty returned false");
            ReadOnlySpan<byte> value = SDL_GetStringProperty(props, "name"u8, "fallback"u8);
            Expect(value.SequenceEqual("native value"u8), $"read '{Encoding.UTF8.GetString(value)}'");
        }
        finally
        {
            SDL_DestroyProperties(props);
        }
    }

    // SDL returns default_value itself for a missing property; the binding hands back the caller's span, cut at its terminator.
    public static void StringPropertyReturnsCallerDefault()
    {
        SDL_PropertiesID props = SDL_CreateProperties();
        try
        {
            byte[] fallback = "fallback\0"u8.ToArray();
            ReadOnlySpan<byte> value = SDL_GetStringProperty(props, "missing"u8, fallback);
            Expect(Unsafe.AreSame(ref MemoryMarshal.GetReference(value), ref fallback[0]), "the result is not the caller's array");
            Expect(value.SequenceEqual("fallback"u8), $"read '{Encoding.UTF8.GetString(value)}'");
        }
        finally
        {
            SDL_DestroyProperties(props);
        }
    }

    // The span keeps tracking the array when a compacting collection moves it; a raw pointer would be left behind.
    public static void StringPropertyDefaultSurvivesCompaction()
    {
        SDL_PropertiesID props = SDL_CreateProperties();
        try
        {
            List<byte[]> garbage = [];
            for (int index = 0; index < 1000; index++)
            {
                garbage.Add(new byte[64]);
            }

            byte[] fallback = "fallback\0"u8.ToArray();
            garbage.Clear();
            ReadOnlySpan<byte> value = SDL_GetStringProperty(props, "missing"u8, fallback);
            for (int index = 0; index < 3; index++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            }

            Expect(Unsafe.AreSame(ref MemoryMarshal.GetReference(value), ref fallback[0]), "the result does not follow the caller's array");
            Expect(value.SequenceEqual("fallback"u8), $"read '{Encoding.UTF8.GetString(value)}'");
        }
        finally
        {
            SDL_DestroyProperties(props);
        }
    }

    public static void DefaultSpanPassesNull()
    {
        SDL_PropertiesID props = SDL_CreateProperties();
        try
        {
            ReadOnlySpan<byte> value = SDL_GetStringProperty(props, "missing"u8, default);
            Expect(value.IsEmpty && Unsafe.IsNullRef(ref MemoryMarshal.GetReference(value)), "a default span did not pass NULL");
            ReadOnlySpan<byte> empty = SDL_GetStringProperty(props, "missing"u8, ""u8);
            Expect(empty.IsEmpty && !Unsafe.IsNullRef(ref MemoryMarshal.GetReference(empty)), "an empty literal passed NULL");
        }
        finally
        {
            SDL_DestroyProperties(props);
        }
    }

    public static void ErrorIsReadable()
    {
        SDL_ClearError();
        Expect(MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)SDL_GetError()).IsEmpty, "SDL_ClearError left an error");
        Expect(!SDL_SetBooleanProperty(0, "name"u8, true), "SDL_SetBooleanProperty accepted the invalid properties 0");
        ReadOnlySpan<byte> error = MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)SDL_GetError());
        Expect(!error.IsEmpty, "SDL_GetError is empty after a failure");
    }

    public static void StructsCrossByValue()
    {
        ReadOnlySpan<byte> text = "030000005e0400008e02000014010000"u8;
        SDL_GUID guid = SDL_StringToGUID(text);
        Expect(guid.data[0] == 0x03 && guid.data[4] == 0x5e && guid.data[15] == 0x00, "SDL_StringToGUID returned other bytes");
        byte* buffer = stackalloc byte[33];
        SDL_GUIDToString(guid, buffer, 33);
        ReadOnlySpan<byte> roundTrip = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(buffer);
        Expect(roundTrip.SequenceEqual(text), $"SDL_GUIDToString returned '{Encoding.UTF8.GetString(roundTrip)}'");
    }

    public static void CallbacksReachManagedCode()
    {
        SDL_PropertiesID props = SDL_CreateProperties();
        try
        {
            SDL_SetNumberProperty(props, "a"u8, 1);
            SDL_SetNumberProperty(props, "b"u8, 2);
            int count = 0;
            Expect(SDL_EnumerateProperties(props, &CountProperty, (nint)(&count)), "SDL_EnumerateProperties returned false");
            Expect(count == 2, $"the callback ran {count} times");
        }
        finally
        {
            SDL_DestroyProperties(props);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void CountProperty(nint userdata, SDL_PropertiesID props, byte* name)
    {
        (*(int*)userdata)++;
    }

    // Every named format plus FourCC codes and values that no format uses, through the C# ports and the C macros.
    public static void PixelFormatMacrosMatchC()
    {
        List<uint> formats = Enum.GetValues<SDL_PixelFormat>().Select(format => (uint)format).ToList();
        formats.AddRange([0x32315659, 0x56555949, 0x10000000, 0x1FFFFFFF, 0xFFFFFFFF, 1]);
        List<string> differences = [];
        foreach (uint value in formats)
        {
            SDL_PixelFormat format = (SDL_PixelFormat)value;
            (string Name, int CSharp, int C)[] results =
            [
                ("PIXELFLAG", SDL_PIXELFLAG(format), NativeMacros.xdlsharp_PIXELFLAG(value)),
                ("PIXELTYPE", (int)SDL_PIXELTYPE(format), NativeMacros.xdlsharp_PIXELTYPE(value)),
                ("PIXELORDER", SDL_PIXELORDER(format), NativeMacros.xdlsharp_PIXELORDER(value)),
                ("PIXELLAYOUT", (int)SDL_PIXELLAYOUT(format), NativeMacros.xdlsharp_PIXELLAYOUT(value)),
                ("BITSPERPIXEL", SDL_BITSPERPIXEL(format), NativeMacros.xdlsharp_BITSPERPIXEL(value)),
                ("BYTESPERPIXEL", SDL_BYTESPERPIXEL(format), NativeMacros.xdlsharp_BYTESPERPIXEL(value)),
                ("ISPIXELFORMAT_INDEXED", SDL_ISPIXELFORMAT_INDEXED(format) ? 1 : 0, NativeMacros.xdlsharp_ISPIXELFORMAT_INDEXED(value)),
                ("ISPIXELFORMAT_PACKED", SDL_ISPIXELFORMAT_PACKED(format) ? 1 : 0, NativeMacros.xdlsharp_ISPIXELFORMAT_PACKED(value)),
                ("ISPIXELFORMAT_ARRAY", SDL_ISPIXELFORMAT_ARRAY(format) ? 1 : 0, NativeMacros.xdlsharp_ISPIXELFORMAT_ARRAY(value)),
                ("ISPIXELFORMAT_10BIT", SDL_ISPIXELFORMAT_10BIT(format) ? 1 : 0, NativeMacros.xdlsharp_ISPIXELFORMAT_10BIT(value)),
                ("ISPIXELFORMAT_FLOAT", SDL_ISPIXELFORMAT_FLOAT(format) ? 1 : 0, NativeMacros.xdlsharp_ISPIXELFORMAT_FLOAT(value)),
                ("ISPIXELFORMAT_ALPHA", SDL_ISPIXELFORMAT_ALPHA(format) ? 1 : 0, NativeMacros.xdlsharp_ISPIXELFORMAT_ALPHA(value)),
                ("ISPIXELFORMAT_FOURCC", SDL_ISPIXELFORMAT_FOURCC(format) ? 1 : 0, NativeMacros.xdlsharp_ISPIXELFORMAT_FOURCC(value)),
            ];
            foreach ((string name, int csharp, int c) in results.Where(result => result.CSharp != result.C))
            {
                differences.Add($"SDL_{name}(0x{value:X8}): C# {csharp}, C {c}");
            }
        }

        Expect(differences.Count == 0, string.Join('\n', differences));
    }

    public static void InitInterfaceSetsVersion()
    {
        SDL_IOStreamInterface iface = default;
        iface.size = (delegate* unmanaged[Cdecl]<nint, long>)1;
        SDL_INIT_INTERFACE(&iface);
        Expect(iface.version == (uint)sizeof(SDL_IOStreamInterface) && iface.size == null, "SDL_INIT_INTERFACE did not zero the struct and set its size");
    }

    // Each satellite is its own P/Invoke module, so a call to each shows that its library is found and linked.
    public static void SatellitesMatchTheirHeaders()
    {
        Expect(IMG_Version() == SDL_IMAGE_VERSION, $"IMG_Version() is {IMG_Version()}, the header says {SDL_IMAGE_VERSION}");
        Expect(TTF_Version() == SDL_TTF_VERSION, $"TTF_Version() is {TTF_Version()}, the header says {SDL_TTF_VERSION}");
        Expect(MIX_Version() == SDL_MIXER_VERSION, $"MIX_Version() is {MIX_Version()}, the header says {SDL_MIXER_VERSION}");
        Expect(TTF_Init(), $"TTF_Init failed: {Error()}");
        TTF_Quit();
        Expect(MIX_Init(), $"MIX_Init failed: {Error()}");
        MIX_Quit();
    }

    private static string Error()
    {
        return Encoding.UTF8.GetString(MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)SDL_GetError()));
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition)
        {
            throw new CheckFailedException(message);
        }
    }
}
