// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

public class Program
{
    private static string s_helloworld = "hello, world!";
    private static string[] s_array = new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" };
    private static string[][] s_3x3 = new string[][] { new[] { "1", "2", "3" }, new[] { "4", "5", "6" }, new[] { "7", "8", "9" } };

    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            // Sanity check
            using var _ = Platform.InitializeAllocators(tracking: false);
            Console.WriteLine(ToUpper(s_helloworld));
            Console.WriteLine(Concat(s_array));
            Console.WriteLine(Concat2(s_3x3));
        }
        else
        {
            var summary = BenchmarkRunner.Run<Program>(args: args);
        }
    }

    //
    // BenchmarkDotNet tests
    //

    IDisposable _memTracker;

    [GlobalSetup]
    public void GlobalSetup() => _memTracker = Platform.InitializeAllocators(tracking: false);

    [GlobalCleanup]
    public void GlobalCleanup() => _memTracker.Dispose();

    [Benchmark]
    public string HString() => ToUpper(s_helloworld);

    [Benchmark]
    public string HStringArray() => Concat(s_array);

    [Benchmark]
    public string HStringMatrix() => Concat2(s_3x3);

    //
    // Generated P/Invoke calls
    //

    static unsafe string ToUpper(string str)
    {
        HStringMarshaller marshaller = default;
        string managed;

        try
        {
            // Marshal
            marshaller.FromManagedValue(str);
            void* nstr = (void*)marshaller.ToNativeValue();

            // Invoke
            void* nres = __PInvoke(nstr);

            // Unmarshal
            marshaller.FromNativeValue((IntPtr)nres);
            managed = marshaller.ToManaged();
        }
        finally
        {
            // Clean up
            marshaller.FreeNative();
        }

        return managed;

        // P/Invoke declaration
        static void* __PInvoke(void* s) => Platform.ToUpper(s);
    }

    static unsafe string Concat(string[] strs)
    {
        ArrayMarshaller<string> native = default;
        HStringMarshaller ret = default;
        string managed = default;

        try
        {
            // Marshal
            Span<IntPtr> elements = native.InitializeToNative<IntPtr>(strs, IntPtr.Size, sizeof(HStringMarshaller));
            Span<HStringMarshaller> states = native.GetStateCollection<HStringMarshaller>();
            for (int i = 0; i < strs.Length; ++i)
            {
                ref HStringMarshaller hstring = ref states[i];
                hstring.FromManagedValue(strs[i]);
                elements[i] = hstring.ToNativeValue();
            }
            void* nstrs = (void*)native.ToNative();

            // Invoke
            void* nres = __PInvoke(nstrs, elements.Length);

            // Unmarshal
            ret.FromNativeValue((IntPtr)nres);
            managed = ret.ToManaged();
        }
        finally
        {
            // Clean up
            Span<HStringMarshaller> states = native.GetStateCollection<HStringMarshaller>();
            for (int i = 0; i < states.Length; ++i)
            {
                states[i].FreeNative();
            }
            native.FreeNative();
            ret.FreeNative();
        }

        return managed;

        // P/Invoke declaration
        static void* __PInvoke(void* a, int c) => Platform.Concat((void**)a, c);
    }

    static unsafe string Concat2(string[][] strss)
    {
        ArrayMarshaller<string[]> native1 = default;
        ArrayMarshaller<string> native2 = default;
        HStringMarshaller ret = default;
        string managed = default;

        try
        {
            // Marshal
            Span<IntPtr> elements1 = native1.InitializeToNative<IntPtr>(strss, IntPtr.Size, sizeof(ArrayMarshaller<string[]>.State));
            Span<ArrayMarshaller<string>.State> states1 = native1.GetStateCollection<ArrayMarshaller<string>.State>();
            for (int i = 0; i < strss.Length; ++i)
            {
                string[] strs = strss[i];

                Span<IntPtr> elements2 = native2.InitializeToNative<IntPtr>(strs, IntPtr.Size, ref states1[i]);
                Span<HStringMarshaller> states2 = native2.GetStateCollection<HStringMarshaller>();
                for (int j = 0; j < strs.Length; ++j)
                {
                    ref HStringMarshaller hstring = ref states2[j];
                    hstring.FromManagedValue(strs[j]);
                    elements2[j] = hstring.ToNativeValue();
                }

                elements1[i] = native2.ToNative();
            }

            void* nstrs = (void*)native1.ToNative();

            // Invoke
            void* nres = __PInvoke(nstrs, elements1.Length);

            // Unmarshal
            ret.FromNativeValue((IntPtr)nres);
            managed = ret.ToManaged();
        }
        finally
        {
            // Clean up
            Span<ArrayMarshaller<string>.State> states1 = native1.GetStateCollection<ArrayMarshaller<string>.State>();
            for (int i = 0; i < states1.Length; ++i)
            {
                native2.InitializeFreeNative(ref states1[i]);
                Span<HStringMarshaller> states2 = native2.GetStateCollection<HStringMarshaller>();
                for (int j = 0; j < states2.Length; ++j)
                {
                    states2[j].FreeNative();
                }
                native2.FreeNative();
            }
            native1.FreeNative();
            ret.FreeNative();
        }

        return managed;

        // P/Invoke declaration
        static void* __PInvoke(void* a, int c) => Platform.Concat2((void***)a, c);
    }
}
