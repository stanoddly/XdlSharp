# XdlSharp

Low-level C# bindings for [XDL](https://github.com/stanoddly/XDL), the altered SDL 3.4.20 that builds as `XDL3`, and for SDL_image, SDL_ttf and SDL_mixer built against it as `XDL3_image`, `XDL3_ttf` and `XDL3_mixer`. They are meant for libraries such as Pixely, so they mirror the C API: the same names and parameters, no exceptions and no checks. The exception is `Pointer<T>`: `Value` and `ThrowIfNull` throw `InvalidOperationException` for a null pointer. No runtime has to stop a null dereference in unsafe code, and on wasm address 0 is readable memory.

```csharp
using XdlSharp;
using static XdlSharp.Xdl;

SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO);
Pointer<SDL_Window> window = SDL_CreateWindow("Title"u8, 640, 480, SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
```

## Mapping

- Functions, constants, hints and property names of all four libraries are static members of `XdlSharp.Xdl`; types are in `XdlSharp`. Each function loads from its own library: `IMG_` functions from `XDL3_image`, and so on.
- `T*` is `Pointer<T>`, `void*` is `nint`, and a function pointer is `delegate* unmanaged[Cdecl]<...>`. The signature of a function pointer keeps `T*`, and `bool` in it is `byte`.
- `const char*` parameters are `ReadOnlySpan<byte>`, pinned for the call; a `u8` literal is terminated. `default` passes `NULL`.
- Returned strings are `Pointer<byte>`, neither decoded nor freed.
- `SDL_GetStringProperty` returns `ReadOnlySpan<byte>`: SDL returns `default_value` itself for a missing property, so the binding returns the caller's span instead of a pointer that would dangle after the pin.
- `SDL_SetScancodeName` keeps `name` after it returns, so that parameter is `Pointer<byte>`: its lifetime is the caller's.
- Integer typedefs are enums of the same name (`SDL_WindowID`, `SDL_PropertiesID`); flag macros are their members (`SDL_WindowFlags.SDL_WINDOW_HIDDEN`).
- `bool` crosses the native boundary as `byte`, because a `bool` passed by value arrives with stale upper bytes on Mono in the browser.
- Function pointers cross the native boundary as `nint`, because Mono's wasm interpreter cannot call a P/Invoke that has a function pointer type in its signature.
- Struct fields keep `bool`, `Pointer<T>` and `delegate*` types; fixed arrays are `fixed` buffers or `[InlineArray]` structs.

`src/XdlSharp/Generated/skipped.txt` lists what has no binding: variadic and `va_list` functions, inline functions, function-like macros, and macros that are not constants. `SDL_stdinc.h` is left out except `SDL_malloc`, `SDL_calloc`, `SDL_realloc` and `SDL_free`. `src/XdlSharp/Xdl.Macros.cs` ports the pixel format macros and `SDL_INIT_INTERFACE` by hand.

## XDL release

`XDL_VERSION` pins the XDL release. Building the generator or a test project runs `tools/XdlRelease.proj`, which downloads that release's headers, the runtime for this machine and the browser archives into `artifacts/xdl/<version>`.

## Generating

The bindings are generated from the release headers with clang, and the output is checked in.

```shell
dotnet run --project tools/XdlSharp.Generator
dotnet run --project tools/XdlSharp.Generator -- --check
```

`--include <dir>` names other headers and `--clang <path>` another clang. `--check` writes no checked-in file and fails when a generated file is out of date. `tools/XdlSharp.Generator/generator.json` holds the choices the headers do not state, and the generator fails when an entry no longer matches the headers.

## Testing

The checks in `tests/XdlSharp.Checks` compare every struct size, field offset and enum size with what the C compiler measures from the same headers, and call into XDL for booleans, strings, structs by value, callbacks and the hand-ported macros, and into each satellite library.

On the desktop they run under NUnit, against the release runtime and a library CMake builds from `tests/native`:

```shell
dotnet test tests/XdlSharp.Tests
```

In the browser runtime they run on Mono under Node. The release archives and `tests/native`, compiled by the Emscripten of the wasm-tools workload, are linked into the runtime:

```shell
dotnet publish tests/XdlSharp.Browser.Tests
cd tests/XdlSharp.Browser.Tests/bin/Release/net11.0/publish/wwwroot && node main.mjs
```

## Limits

- The headers are parsed for Linux, so declarations that only other platforms see, such as `SDL_SetWindowsMessageHook`, are missing. The generator runs only on Linux.
- `SDL_main.h` is not bound: `SDL.h` does not include it.
- `SDL_vulkan.h` is not bound: `SDL.h` does not include it, and `VkSurfaceKHR` is a pointer on 64-bit targets but a `uint64_t` on wasm32.
- `wchar_t*` is `nint`, because `wchar_t` is 2 bytes on Windows and 4 elsewhere.
