// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Runtime.InteropServices;

static unsafe class Platform
{
    //
    // Simulated native calls
    //

    public static delegate* unmanaged<void*, void*> ToUpper = &_ToUpper;

    [UnmanagedCallersOnly]
    private static void* _ToUpper(void* hstring)
    {
        void* res;
        var resStr = new string((char*)hstring).ToUpper();
        fixed (char* ptr = resStr)
            WindowsCreateString(ptr, (uint)resStr.Length, &res);

        return res;
    }

    public static delegate* unmanaged<void**, int, void*> Concat = &_Concat;

    [UnmanagedCallersOnly]
    private static void* _Concat(void** hstrings, int count)
    {
        var builder = new StringBuilder();
        var arr = new Span<IntPtr>((void*)hstrings, count);
        for (int i = 0; i < count; i++)
        {
            builder.Append(new string((char*)arr[i]));
        }

        void* res;
        var resStr = builder.ToString();
        fixed (char* ptr = resStr)
            WindowsCreateString(ptr, (uint)resStr.Length, &res);

        return res;
    }

    public static delegate* unmanaged<void***, int, void*> Concat2 = &_Concat2;

    [UnmanagedCallersOnly]
    private static void* _Concat2(void*** hstrings, int count)
    {
        var builder = new StringBuilder();
        var outer = new Span<IntPtr>((void*)hstrings, count);
        for (int i = 0; i < count; i++)
        {
            var inner = new Span<IntPtr>((void*)outer[i], count);
            for (int j = 0; j < count; j++)
            {
                builder.Append(new string((char*)inner[j]));
            }
        }

        void* res;
        var resStr = builder.ToString();
        fixed (char* ptr = resStr)
            WindowsCreateString(ptr, (uint)resStr.Length, &res);

        return res;
    }

    //
    // Pseudo HString APIs
    //

    public static delegate* unmanaged<void*, uint, void**, int> WindowsCreateString = &_WindowsCreateString;

    [UnmanagedCallersOnly]
    private static int _WindowsCreateString(void* str, uint len, void** hstring)
    {
        char* alloc = (char*)Platform.Alloc(sizeof(char) * (len + 1));
        var src = new Span<char>(str, (int)len);
        src.CopyTo(new Span<char>(alloc, (int)len));
        alloc[len] = '\0';
        *hstring = alloc;
        return 0;
    }

    public static delegate* unmanaged<void*, int> WindowsDeleteString = &_WindowsDeleteString;

    [UnmanagedCallersOnly]
    private static int _WindowsDeleteString(void* hstring)
    {
        Platform.Free(hstring);
        return 0;
    }

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