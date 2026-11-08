using System.Runtime.CompilerServices;

// Every binding passes only blittable types, so the runtime never marshals and a non-blittable signature fails to compile.
[assembly: DisableRuntimeMarshalling]
