// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

static unsafe class Platform
{
    //
    // Simulated native calls
    //

    public static delegate* unmanaged<void*, void*> Copy = &_Copy;

    [UnmanagedCallersOnly]
    private static void* _Copy(void* hstring)
    {
        void* res;
        char* ptr = WindowsGetStringRawBuffer(hstring);
        uint len = (uint)MemoryMarshal.CreateReadOnlySpanFromNullTerminated(ptr).Length;
        WindowsCreateString(ptr, len, &res);

        return res;
    }

    public static delegate* unmanaged<void**, int, void*> Concat = &_Concat;

    [UnmanagedCallersOnly]
    private static void* _Concat(void** hstrings, int count)
    {
        List<char> chars = new(count * 32); // Preallocate an anticipated capacity
        var arr = new Span<IntPtr>(hstrings, count);
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<char> strRaw = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(WindowsGetStringRawBuffer((void*)arr[i]));
            for (int j = 0; j < strRaw.Length; j++)
                chars.Add(strRaw[j]);
        }

        void* res;
        Span<char> charsSpan = CollectionsMarshal.AsSpan(chars);
        fixed (char* charsRaw = charsSpan)
            WindowsCreateString(charsRaw, (uint)charsSpan.Length, &res);

        return res;
    }

    public static delegate* unmanaged<void***, int, void*> ConcatMatrix = &_ConcatMatrix;

    [UnmanagedCallersOnly]
    private static void* _ConcatMatrix(void*** hstrings, int count)
    {
        List<char> chars = new(count * count * 32); // Preallocate an anticipated capacity
        var outer = new Span<IntPtr>(hstrings, count);
        for (int i = 0; i < count; i++)
        {
            var inner = new Span<IntPtr>((void*)outer[i], count);
            for (int j = 0; j < count; j++)
            {
                ReadOnlySpan<char> strRaw = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(WindowsGetStringRawBuffer((void*)inner[j]));
                for (int k = 0; k < strRaw.Length; k++)
                    chars.Add(strRaw[k]);
            }
        }

        void* res;
        Span<char> charsSpan = CollectionsMarshal.AsSpan(chars);
        fixed (char* charsRaw = charsSpan)
            WindowsCreateString(charsRaw, (uint)charsSpan.Length, &res);

        return res;
    }

    //
    // Pseudo HString APIs
    //

    [StructLayout(LayoutKind.Sequential, Size = 24)] // Uses 20 bytes for 32-bit or 24 bytes for 64-bit
    public struct HSTRING_HEADER
    {
        private void* _data1;
    }

    [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern int WindowsCreateString(void* sourceString, uint length, void** hstring);

    [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern int WindowsCreateStringReference(void* sourceString, uint length, HSTRING_HEADER* header, void** hstring);

    [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern int WindowsDeleteString(void* hstring);

    [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern char* WindowsGetStringRawBuffer(void* hstring, int* length = null);

    //
    // Memory tracking APIs to ensure native memory is cleaned up properly.
    //

    public static delegate*<nuint, void*> Alloc;
    public static delegate*<nuint, void*> AllocZeroed;
    public static delegate*<void*, void> Free;

    public static IDisposable InitializeAllocators(bool tracking)
    {
        if (tracking)
        {
            Alloc = &Tracker.Alloc;
            AllocZeroed = &Tracker.AllocZeroed;
            Free = &Tracker.Free;
            return new Tracker();
        }

        Alloc = &NativeMemory.Alloc;
        AllocZeroed = &NativeMemory.AllocZeroed;
        Free = &NativeMemory.Free;
        return new Nop();
    }

    private class Nop : IDisposable
    {
        public void Dispose() { }
    }

    private class Tracker : IDisposable
    {
        private static nint _allocs = 0;
        private static nint _alloczeroeds = 0;
        private static nint _frees = 0;

        public static void* Alloc(nuint a)
        {
            void* p = NativeMemory.Alloc(a);
            _allocs++;
            return p;
        }

        public static void* AllocZeroed(nuint a)
        {
            void* p = NativeMemory.AllocZeroed(a);
            _alloczeroeds++;
            return p;
        }

        public static void Free(void* p)
        {
            NativeMemory.Free(p);
            _frees++;
        }

        public void Dispose()
        {
            const int pad =  6;
            Console.WriteLine(
$@"*** Tracker ***
{_allocs,pad}: Alloc
{_alloczeroeds,pad}: AllocZeroed
--------------------------
{_allocs + _alloczeroeds,pad}: Allocations
{_frees,pad}: Free
==========================
{(_allocs + _alloczeroeds) - _frees,pad}: Delta
");
        }
    }
}