// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public unsafe struct HStringMarshaller1
{
    IntPtr _native;

    public void FromManagedValue(string str)
    {
        fixed (char* ptr = str)
        {
            void* alloc;
            Platform.WindowsCreateString(ptr, (uint)str.Length, &alloc);
            _native = (IntPtr)alloc;
        }
    }

    public IntPtr ToNativeValue()
        => _native;

    public void FromNativeValue(IntPtr value)
    {
        if (_native != IntPtr.Zero)
            Platform.WindowsDeleteString((void*)_native);
        _native = value;
    }

    public string ToManaged()
        => new string(Platform.WindowsGetStringRawBuffer((void*)_native));

    public void FreeNative()
        => Platform.WindowsDeleteString((void*)_native);
}

public unsafe struct HStringMarshaller2
{
    public struct State
    {
        public IntPtr Native;
    }

    public static IntPtr InitializeToNativeValue(string str, ref State state)
    {
        fixed (char* ptr = str)
        {
            void* alloc;
            Platform.WindowsCreateString(ptr, (uint)str.Length, &alloc);
            state.Native = (IntPtr)alloc;
        }

        return state.Native;
    }

    public static string ToManaged(IntPtr ptr)
    {
        var res = new string(Platform.WindowsGetStringRawBuffer((void*)ptr));
        Platform.WindowsDeleteString((void*)ptr);
        return res;
    }

    public static void FreeNative(ref State state)
        => Platform.WindowsDeleteString((void*)state.Native);
}

public unsafe struct HStringMarshaller3
{
    public static IntPtr ConvertToNativeValue(string str)
    {
        fixed (char* ptr = str)
        {
            void* alloc;
            Platform.WindowsCreateString(ptr, (uint)str.Length, &alloc);
            return (IntPtr)alloc;
        }
    }

    public static string ConvertToManaged(IntPtr ptr)
    {
        var res = new string(Platform.WindowsGetStringRawBuffer((void*)ptr));
        Platform.WindowsDeleteString((void*)ptr);
        return res;
    }

    public static void FreeNativeValue(IntPtr native)
        => Platform.WindowsDeleteString((void*)native);

    public ref struct InDirection
    {
        // Marshalling state
        private string _managed;
        private Platform.HSTRING_HEADER _hstring_header;

        public void FromManaged(string str)
        {
            _managed = str;
        }

        public ref char GetPinnableReference()
        {
            return ref Unsafe.AsRef(in _managed.GetPinnableReference());
        }

        public IntPtr ToNativeValue()
        {
            var str = (byte*)Unsafe.AsPointer(ref GetPinnableReference());
            void* alloc;
            fixed (Platform.HSTRING_HEADER* header = &_hstring_header)
            Platform.WindowsCreateStringReference(str, (uint)_managed.Length, header, &alloc);
            return (IntPtr)alloc;
        }
    }
}

public unsafe ref struct ArrayMarshaller<T>
{
    public struct State
    {
        public void* States;
        public void* Elements;
        public int ElementCount;
    }

    State _state;

    public Span<N> InitializeToNative<N>(T[] arr, int elementSize, ref State state) where N : unmanaged
    {
        // State needs to be copied from the passed in state or freed and updated
        // if the marshaller decides more is needed.
        if (state.ElementCount != 0)
        {
            if (arr.Length <= state.ElementCount)
            {
                _state = state;
                return new Span<N>(_state.Elements, _state.ElementCount);
            }
            else
            {
                // Free the passed in state as it is insufficient.
                FreeNative(ref state);
            }
        }

        Span<N> elements = InitializeToNative<N>(arr, elementSize, sizeof(State));

        // Update the stored state.
        state = _state;
        return elements;
    }

    public Span<N> InitializeToNative<N>(T[] arr, int elementSize, int stateSize) where N : unmanaged
    {
        _state.ElementCount = arr.Length;

        int totalState = checked(_state.ElementCount * stateSize);
        _state.States = Platform.AllocZeroed((nuint)totalState);

        int totalNative = checked(_state.ElementCount * elementSize);
        _state.Elements = Platform.AllocZeroed((nuint)totalNative);

        return new Span<N>(_state.Elements, _state.ElementCount);
    }

    public IntPtr ToNative()
    {
        return (IntPtr)_state.Elements;
    }

    public Span<S> GetStateCollection<S>() where S : unmanaged
    {
        return new Span<S>(_state.States, _state.ElementCount);
    }

    public void InitializeFreeNative(ref State n)
    {
        _state = n;
    }

    public void FreeNative()
    {
        FreeNative(ref _state);
    }

    private static void FreeNative(ref State n)
    {
        Platform.Free(n.States);
        Platform.Free(n.Elements);
        n.ElementCount = 0;
    }
}