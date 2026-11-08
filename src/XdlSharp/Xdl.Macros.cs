using System.Runtime.CompilerServices;

namespace XdlSharp;

// Function-like macros of the headers, which a generator cannot bind: ports of their C definitions.
public static unsafe partial class Xdl
{
    // SDL_pixels.h

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SDL_PIXELFLAG(SDL_PixelFormat format)
    {
        return (int)(((uint)format >> 28) & 0x0F);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SDL_PixelType SDL_PIXELTYPE(SDL_PixelFormat format)
    {
        return (SDL_PixelType)(((uint)format >> 24) & 0x0F);
    }

    // The value is an SDL_BitmapOrder, SDL_PackedOrder or SDL_ArrayOrder, depending on SDL_PIXELTYPE.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SDL_PIXELORDER(SDL_PixelFormat format)
    {
        return (int)(((uint)format >> 20) & 0x0F);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SDL_PackedLayout SDL_PIXELLAYOUT(SDL_PixelFormat format)
    {
        return (SDL_PackedLayout)(((uint)format >> 16) & 0x0F);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SDL_BITSPERPIXEL(SDL_PixelFormat format)
    {
        return SDL_ISPIXELFORMAT_FOURCC(format) ? 0 : (int)(((uint)format >> 8) & 0xFF);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SDL_BYTESPERPIXEL(SDL_PixelFormat format)
    {
        if (SDL_ISPIXELFORMAT_FOURCC(format))
        {
            bool twoBytes = format is SDL_PixelFormat.SDL_PIXELFORMAT_YUY2 or SDL_PixelFormat.SDL_PIXELFORMAT_UYVY or SDL_PixelFormat.SDL_PIXELFORMAT_YVYU
                or SDL_PixelFormat.SDL_PIXELFORMAT_P010;
            return twoBytes ? 2 : 1;
        }

        return (int)((uint)format & 0xFF);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SDL_ISPIXELFORMAT_INDEXED(SDL_PixelFormat format)
    {
        return !SDL_ISPIXELFORMAT_FOURCC(format) && SDL_PIXELTYPE(format) is SDL_PixelType.SDL_PIXELTYPE_INDEX1 or SDL_PixelType.SDL_PIXELTYPE_INDEX2
            or SDL_PixelType.SDL_PIXELTYPE_INDEX4 or SDL_PixelType.SDL_PIXELTYPE_INDEX8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SDL_ISPIXELFORMAT_PACKED(SDL_PixelFormat format)
    {
        return !SDL_ISPIXELFORMAT_FOURCC(format) && SDL_PIXELTYPE(format) is SDL_PixelType.SDL_PIXELTYPE_PACKED8 or SDL_PixelType.SDL_PIXELTYPE_PACKED16
            or SDL_PixelType.SDL_PIXELTYPE_PACKED32;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SDL_ISPIXELFORMAT_ARRAY(SDL_PixelFormat format)
    {
        return !SDL_ISPIXELFORMAT_FOURCC(format) && SDL_PIXELTYPE(format) is SDL_PixelType.SDL_PIXELTYPE_ARRAYU8 or SDL_PixelType.SDL_PIXELTYPE_ARRAYU16
            or SDL_PixelType.SDL_PIXELTYPE_ARRAYU32 or SDL_PixelType.SDL_PIXELTYPE_ARRAYF16 or SDL_PixelType.SDL_PIXELTYPE_ARRAYF32;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SDL_ISPIXELFORMAT_10BIT(SDL_PixelFormat format)
    {
        return !SDL_ISPIXELFORMAT_FOURCC(format) && SDL_PIXELTYPE(format) == SDL_PixelType.SDL_PIXELTYPE_PACKED32
            && SDL_PIXELLAYOUT(format) == SDL_PackedLayout.SDL_PACKEDLAYOUT_2101010;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SDL_ISPIXELFORMAT_FLOAT(SDL_PixelFormat format)
    {
        return !SDL_ISPIXELFORMAT_FOURCC(format) && SDL_PIXELTYPE(format) is SDL_PixelType.SDL_PIXELTYPE_ARRAYF16 or SDL_PixelType.SDL_PIXELTYPE_ARRAYF32;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SDL_ISPIXELFORMAT_ALPHA(SDL_PixelFormat format)
    {
        if (SDL_ISPIXELFORMAT_PACKED(format))
        {
            return (SDL_PackedOrder)SDL_PIXELORDER(format) is SDL_PackedOrder.SDL_PACKEDORDER_ARGB or SDL_PackedOrder.SDL_PACKEDORDER_RGBA
                or SDL_PackedOrder.SDL_PACKEDORDER_ABGR or SDL_PackedOrder.SDL_PACKEDORDER_BGRA;
        }

        return SDL_ISPIXELFORMAT_ARRAY(format) && (SDL_ArrayOrder)SDL_PIXELORDER(format) is SDL_ArrayOrder.SDL_ARRAYORDER_ARGB
            or SDL_ArrayOrder.SDL_ARRAYORDER_RGBA or SDL_ArrayOrder.SDL_ARRAYORDER_ABGR or SDL_ArrayOrder.SDL_ARRAYORDER_BGRA;
    }

    // The flag is 1 for the formats SDL defines and the first character of a FourCC code otherwise.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SDL_ISPIXELFORMAT_FOURCC(SDL_PixelFormat format)
    {
        return format != 0 && SDL_PIXELFLAG(format) != 1;
    }

    // SDL_stdinc.h, for the structs that SDL documents as interfaces

    public static void SDL_INIT_INTERFACE(Pointer<SDL_IOStreamInterface> iface)
    {
        SDL_IOStreamInterface* pointer = iface;
        *pointer = default;
        pointer->version = (uint)sizeof(SDL_IOStreamInterface);
    }

    public static void SDL_INIT_INTERFACE(Pointer<SDL_StorageInterface> iface)
    {
        SDL_StorageInterface* pointer = iface;
        *pointer = default;
        pointer->version = (uint)sizeof(SDL_StorageInterface);
    }

    public static void SDL_INIT_INTERFACE(Pointer<SDL_VirtualJoystickDesc> iface)
    {
        SDL_VirtualJoystickDesc* pointer = iface;
        *pointer = default;
        pointer->version = (uint)sizeof(SDL_VirtualJoystickDesc);
    }
}
