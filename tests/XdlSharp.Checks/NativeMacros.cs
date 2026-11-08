using System.Runtime.InteropServices;

namespace XdlSharp.Checks;

internal static class NativeChecks
{
    // tests/native, built by CMake for the desktop and by Emscripten in XdlSharp.Browser.Tests for the browser.
    public const string LibraryName = "xdlsharp_native";
}

// The C macros that Xdl.Macros.cs ports, from tests/native/xdlsharp_macros.c.
internal static class NativeMacros
{
    [DllImport(NativeChecks.LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int xdlsharp_PIXELFLAG(uint format);

    [DllImport(NativeChecks.LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int xdlsharp_PIXELTYPE(uint format);

    [DllImport(NativeChecks.LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int xdlsharp_PIXELORDER(uint format);

    [DllImport(NativeChecks.LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int xdlsharp_PIXELLAYOUT(uint format);

    [DllImport(NativeChecks.LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int xdlsharp_BITSPERPIXEL(uint format);

    [DllImport(NativeChecks.LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int xdlsharp_BYTESPERPIXEL(uint format);

    [DllImport(NativeChecks.LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int xdlsharp_ISPIXELFORMAT_INDEXED(uint format);

    [DllImport(NativeChecks.LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int xdlsharp_ISPIXELFORMAT_PACKED(uint format);

    [DllImport(NativeChecks.LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int xdlsharp_ISPIXELFORMAT_ARRAY(uint format);

    [DllImport(NativeChecks.LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int xdlsharp_ISPIXELFORMAT_10BIT(uint format);

    [DllImport(NativeChecks.LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int xdlsharp_ISPIXELFORMAT_FLOAT(uint format);

    [DllImport(NativeChecks.LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int xdlsharp_ISPIXELFORMAT_ALPHA(uint format);

    [DllImport(NativeChecks.LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int xdlsharp_ISPIXELFORMAT_FOURCC(uint format);
}
