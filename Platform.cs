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
        var resStr = new string(WindowsGetStringRawBuffer(hstring)).ToUpper();
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
            builder.Append(new string(WindowsGetStringRawBuffer((void*)arr[i])));
        }

        void* res;
        var resStr = builder.ToString();
        fixed (char* ptr = resStr)
            WindowsCreateString(ptr, (uint)resStr.Length, &res);

        return res;
    }

    public static delegate* unmanaged<void***, int, void*> ConcatMatrix = &_ConcatMatrix;

    [UnmanagedCallersOnly]
    private static void* _ConcatMatrix(void*** hstrings, int count)
    {
        var builder = new StringBuilder();
        var outer = new Span<IntPtr>((void*)hstrings, count);
        for (int i = 0; i < count; i++)
        {
            var inner = new Span<IntPtr>((void*)outer[i], count);
            for (int j = 0; j < count; j++)
            {
                builder.Append(new string(WindowsGetStringRawBuffer((void*)inner[j])));
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