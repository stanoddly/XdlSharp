// The function-like macros that src/XdlSharp/Xdl.Macros.cs ports, compiled by C for the checks to compare against.

#include <SDL3/SDL.h>

#if defined(_WIN32)
#define XDLSHARP_EXPORT __declspec(dllexport)
#else
#define XDLSHARP_EXPORT __attribute__((visibility("default")))
#endif

XDLSHARP_EXPORT int xdlsharp_PIXELFLAG(Uint32 format) { return SDL_PIXELFLAG(format); }
XDLSHARP_EXPORT int xdlsharp_PIXELTYPE(Uint32 format) { return SDL_PIXELTYPE(format); }
XDLSHARP_EXPORT int xdlsharp_PIXELORDER(Uint32 format) { return SDL_PIXELORDER(format); }
XDLSHARP_EXPORT int xdlsharp_PIXELLAYOUT(Uint32 format) { return SDL_PIXELLAYOUT(format); }
XDLSHARP_EXPORT int xdlsharp_BITSPERPIXEL(Uint32 format) { return SDL_BITSPERPIXEL(format); }
XDLSHARP_EXPORT int xdlsharp_BYTESPERPIXEL(Uint32 format) { return SDL_BYTESPERPIXEL(format); }
XDLSHARP_EXPORT int xdlsharp_ISPIXELFORMAT_INDEXED(Uint32 format) { return SDL_ISPIXELFORMAT_INDEXED(format) ? 1 : 0; }
XDLSHARP_EXPORT int xdlsharp_ISPIXELFORMAT_PACKED(Uint32 format) { return SDL_ISPIXELFORMAT_PACKED(format) ? 1 : 0; }
XDLSHARP_EXPORT int xdlsharp_ISPIXELFORMAT_ARRAY(Uint32 format) { return SDL_ISPIXELFORMAT_ARRAY(format) ? 1 : 0; }
XDLSHARP_EXPORT int xdlsharp_ISPIXELFORMAT_10BIT(Uint32 format) { return SDL_ISPIXELFORMAT_10BIT(format) ? 1 : 0; }
XDLSHARP_EXPORT int xdlsharp_ISPIXELFORMAT_FLOAT(Uint32 format) { return SDL_ISPIXELFORMAT_FLOAT(format) ? 1 : 0; }
XDLSHARP_EXPORT int xdlsharp_ISPIXELFORMAT_ALPHA(Uint32 format) { return SDL_ISPIXELFORMAT_ALPHA(format) ? 1 : 0; }
XDLSHARP_EXPORT int xdlsharp_ISPIXELFORMAT_FOURCC(Uint32 format) { return SDL_ISPIXELFORMAT_FOURCC(format) ? 1 : 0; }
