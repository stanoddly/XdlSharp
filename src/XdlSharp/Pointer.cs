using System;

namespace XdlSharp;

public readonly struct Pointer<TValue> : IEquatable<Pointer<TValue>> where TValue : unmanaged
{
    public static readonly Pointer<TValue> Null = default;

    private readonly unsafe TValue* _rawPointer;
    
    public unsafe Pointer(TValue* rawPointer) => _rawPointer = rawPointer;
    
    public static unsafe implicit operator TValue*(Pointer<TValue> pointer)
    {
        return pointer._rawPointer;
    }
    
    public static unsafe implicit operator Pointer<TValue>(TValue* rawPointer)
    {
        return new Pointer<TValue>(rawPointer);
    }
    
    public static unsafe explicit operator IntPtr(Pointer<TValue> pointer)
    {
        return (IntPtr)pointer._rawPointer;
    }

    public ref TValue Value
    {
        get
        {
            unsafe
            {
                if (_rawPointer == null) throw new InvalidOperationException("Pointer is null.");
                return ref *_rawPointer;
            }
        }
    }

    public bool IsNull
    {
        get { unsafe { return _rawPointer == null; } }
    }

    public void ThrowIfNull()
    {
        unsafe
        {
            if (_rawPointer == null) throw new InvalidOperationException("Pointer is null.");
        }
    }

    public void ThrowIfNull(string message)
    {
        unsafe
        {
            if (_rawPointer == null) throw new InvalidOperationException(message);
        }
    }

    public bool Equals(Pointer<TValue> other)
    {
        unsafe { return _rawPointer == other._rawPointer; }
    }

    public override bool Equals(object? obj)
    {
        return obj is Pointer<TValue> other && Equals(other);
    }

    public override int GetHashCode()
    {
        unsafe { return ((nint)_rawPointer).GetHashCode(); }
    }

    public static bool operator ==(Pointer<TValue> left, Pointer<TValue> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Pointer<TValue> left, Pointer<TValue> right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        unsafe
        {
            if (_rawPointer == null)
            {
                return "null";
            }

            return $"0x{(nint)_rawPointer:X}";
        }
    }
}
