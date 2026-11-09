using System.Runtime.InteropServices;

namespace XdlSharp.Checks;

// The table of tests/native/xdlsharp_layout.c, as the C compiler of the platform measured it.
internal static unsafe class NativeLayout
{
    // Each entry is { const char *name; size_t value; }: two pointer-sized words.
    [DllImport(NativeChecks.LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern nuint* xdlsharp_layouts(int* count);

    public static List<(string Name, nuint Value)> Get()
    {
        int count;
        nuint* entries = xdlsharp_layouts(&count);
        List<(string Name, nuint Value)> result = [];
        for (int index = 0; index < count; index++)
        {
            result.Add((Marshal.PtrToStringUTF8((nint)entries[2 * index])!, entries[2 * index + 1]));
        }

        return result;
    }
}
