using System.Reflection;
using System.Runtime.InteropServices;

namespace XdlSharp.Tests;

// A function that moves to another library, or leaves the release, fails only when it is called; this finds it at once.
public class ExportTests
{
    [Test]
    public void EveryImportResolves()
    {
        Assembly assembly = typeof(Xdl).Assembly;
        List<MethodInfo> imports = assembly.GetTypes().SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<DllImportAttribute>() != null).ToList();
        Dictionary<string, nint> libraries = [];
        List<string> missing = [];
        foreach (MethodInfo method in imports)
        {
            DllImportAttribute import = method.GetCustomAttribute<DllImportAttribute>()!;
            if (!libraries.TryGetValue(import.Value, out nint library))
            {
                library = NativeLibrary.Load(import.Value, assembly, null);
                libraries[import.Value] = library;
            }

            string entryPoint = import.EntryPoint ?? method.Name;
            if (!NativeLibrary.TryGetExport(library, entryPoint, out _))
            {
                missing.Add($"{import.Value}: {entryPoint}");
            }
        }

        Assert.That(imports, Is.Not.Empty);
        Assert.That(missing, Is.Empty);
    }
}
