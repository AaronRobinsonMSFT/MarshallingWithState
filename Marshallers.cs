// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

public unsafe struct HStringMarshaller
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
            Platform.Free((void*)_native);
        _native = value;
    }

    public string ToManaged()
        => new string((char*)_native);

    public void FreeNative()
        => Platform.WindowsDeleteString((void*)_native);
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