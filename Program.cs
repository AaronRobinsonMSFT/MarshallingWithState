// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

public class Program
{
    private static string s_helloworld = "hello, world!";
    private static string[] s_array = new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" };
    private static string[][] s_3x3 = new string[][] { new[] { "1", "2", "3" }, new[] { "4", "5", "6" }, new[] { "7", "8", "9" } };

    static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            var summary = BenchmarkRunner.Run<Program>(args: args);
        }
        else
        {
            // Sanity check. Pass 'true` to InitializeAllocators() to track
            // allocations and confirm native memory is properly cleaned up.
            using var _ = Platform.InitializeAllocators(tracking: false);

            Console.WriteLine(Copy1(s_helloworld));
            Console.WriteLine(Concat1(s_array));
            Console.WriteLine(ConcatMatrix1(s_3x3));

            Console.WriteLine(Copy2(s_helloworld));
            Console.WriteLine(Concat2(s_array));
            Console.WriteLine(ConcatMatrix2(s_3x3));

            Console.WriteLine(Copy3(s_helloworld));
            Console.WriteLine(Concat3(s_array));
            Console.WriteLine(ConcatMatrix3(s_3x3));
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
    public string HString1() => Copy1(s_helloworld);

    [Benchmark]
    public string HStringArray1() => Concat1(s_array);

    [Benchmark]
    public string HStringMatrix1() => ConcatMatrix1(s_3x3);

    [Benchmark]
    public string HString2() => Copy2(s_helloworld);

    [Benchmark]
    public string HStringArray2() => Concat2(s_array);

    [Benchmark]
    public string HStringMatrix2() => ConcatMatrix2(s_3x3);

    [Benchmark]
    public string HString3() => Copy3(s_helloworld);

    [Benchmark]
    public string HStringArray3() => Concat3(s_array);

    [Benchmark]
    public string HStringMatrix3() => ConcatMatrix3(s_3x3);

    //
    // Generated P/Invoke calls
    //

    static unsafe string Copy1(string str)
    {
        HStringMarshaller1 marshaller = default;
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
        static void* __PInvoke(void* s) => Platform.Copy(s);
    }

    static unsafe string Concat1(string[] strs)
    {
        ArrayMarshaller<string> native = default;
        HStringMarshaller1 ret = default;
        string managed = default;

        try
        {
            // Marshal
            Span<IntPtr> elements = native.InitializeToNative<IntPtr>(strs, IntPtr.Size, sizeof(HStringMarshaller1));
            Span<HStringMarshaller1> states = native.GetStateCollection<HStringMarshaller1>();
            for (int i = 0; i < strs.Length; ++i)
            {
                ref HStringMarshaller1 hstring = ref states[i];
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
            Span<HStringMarshaller1> states = native.GetStateCollection<HStringMarshaller1>();
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

    static unsafe string ConcatMatrix1(string[][] strss)
    {
        ArrayMarshaller<string[]> native1 = default;
        ArrayMarshaller<string> native2 = default;
        HStringMarshaller1 ret = default;
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
                Span<HStringMarshaller1> states2 = native2.GetStateCollection<HStringMarshaller1>();
                for (int j = 0; j < strs.Length; ++j)
                {
                    ref HStringMarshaller1 hstring = ref states2[j];
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
                Span<HStringMarshaller1> states2 = native2.GetStateCollection<HStringMarshaller1>();
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
        static void* __PInvoke(void* a, int c) => Platform.ConcatMatrix((void***)a, c);
    }

    static unsafe string Copy2(string str)
    {
        HStringMarshaller2.State state = default;
        string managed;

        try
        {
            // Marshal
            void* nstr = (void*)HStringMarshaller2.InitializeToNativeValue(str, ref state);

            // Invoke
            void* nres = __PInvoke(nstr);

            // Unmarshal
            managed = HStringMarshaller2.ToManaged((IntPtr)nres);
        }
        finally
        {
            // Clean up
            HStringMarshaller2.FreeNative(ref state);
        }

        return managed;

        // P/Invoke declaration
        static void* __PInvoke(void* s) => Platform.Copy(s);
    }

    static unsafe string Concat2(string[] strs)
    {
        ArrayMarshaller<string> native = default;
        string managed = default;

        try
        {
            // Marshal
            Span<IntPtr> elements = native.InitializeToNative<IntPtr>(strs, IntPtr.Size, sizeof(HStringMarshaller2.State));
            Span<HStringMarshaller2.State> states = native.GetStateCollection<HStringMarshaller2.State>();
            for (int i = 0; i < strs.Length; ++i)
            {
                elements[i] = HStringMarshaller2.InitializeToNativeValue(strs[i], ref states[i]);
            }
            void* nstrs = (void*)native.ToNative();

            // Invoke
            void* nres = __PInvoke(nstrs, elements.Length);

            // Unmarshal
            managed = HStringMarshaller2.ToManaged((IntPtr)nres);
        }
        finally
        {
            // Clean up
            Span<HStringMarshaller2.State> states = native.GetStateCollection<HStringMarshaller2.State>();
            for (int i = 0; i < states.Length; ++i)
            {
                HStringMarshaller2.FreeNative(ref states[i]);
            }
            native.FreeNative();
        }

        return managed;

        // P/Invoke declaration
        static void* __PInvoke(void* a, int c) => Platform.Concat((void**)a, c);
    }

    static unsafe string ConcatMatrix2(string[][] strss)
    {
        ArrayMarshaller<string[]> native1 = default;
        ArrayMarshaller<string> native2 = default;
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
                Span<HStringMarshaller2.State> states2 = native2.GetStateCollection<HStringMarshaller2.State>();
                for (int j = 0; j < strs.Length; ++j)
                {
                    elements2[j] = HStringMarshaller2.InitializeToNativeValue(strs[j], ref states2[j]);
                }

                elements1[i] = native2.ToNative();
            }

            void* nstrs = (void*)native1.ToNative();

            // Invoke
            void* nres = __PInvoke(nstrs, elements1.Length);

            // Unmarshal
            managed = HStringMarshaller2.ToManaged((IntPtr)nres);
        }
        finally
        {
            // Clean up
            Span<ArrayMarshaller<string>.State> states1 = native1.GetStateCollection<ArrayMarshaller<string>.State>();
            for (int i = 0; i < states1.Length; ++i)
            {
                native2.InitializeFreeNative(ref states1[i]);
                Span<HStringMarshaller2.State> states2 = native2.GetStateCollection<HStringMarshaller2.State>();
                for (int j = 0; j < states2.Length; ++j)
                {
                    HStringMarshaller2.FreeNative(ref states2[j]);
                }
                native2.FreeNative();
            }
            native1.FreeNative();
        }

        return managed;

        // P/Invoke declaration
        static void* __PInvoke(void* a, int c) => Platform.ConcatMatrix((void***)a, c);
    }

    static unsafe string Copy3(string str)
    {
        HStringMarshaller3.InDirection marshaller = default;
        void* nres;
        string managed;

        // Marshal
        marshaller.FromManaged(str);
        fixed (void* _ = &marshaller.GetPinnableReference())
        {
            void* nstr = (void*)marshaller.ToNativeValue();

            // Invoke
            nres = __PInvoke(nstr);
        }

        // Unmarshal
        managed = HStringMarshaller3.ConvertToManaged((IntPtr)nres);

        return managed;

        // P/Invoke declaration
        static void* __PInvoke(void* s) => Platform.Copy(s);
    }

    static unsafe string Concat3(string[] strs)
    {
        ArrayMarshaller<string> native = default;
        string managed = default;

        try
        {
            // Marshal
            Span<IntPtr> elements = native.InitializeToNative<IntPtr>(strs, IntPtr.Size, sizeof(IntPtr));
            Span<IntPtr> states = native.GetStateCollection<IntPtr>();
            for (int i = 0; i < strs.Length; ++i)
            {
                elements[i] = HStringMarshaller3.ConvertToNativeValue(strs[i]);
                states[i] = elements[i];
            }
            void* nstrs = (void*)native.ToNative();

            // Invoke
            void* nres = __PInvoke(nstrs, elements.Length);

            // Unmarshal
            managed = HStringMarshaller3.ConvertToManaged((IntPtr)nres);
        }
        finally
        {
            // Clean up
            Span<IntPtr> states = native.GetStateCollection<IntPtr>();
            for (int i = 0; i < states.Length; ++i)
            {
                HStringMarshaller3.FreeNativeValue(states[i]);
            }
            native.FreeNative();
        }

        return managed;

        // P/Invoke declaration
        static void* __PInvoke(void* a, int c) => Platform.Concat((void**)a, c);
    }

    static unsafe string ConcatMatrix3(string[][] strss)
    {
        ArrayMarshaller<string[]> native1 = default;
        ArrayMarshaller<string> native2 = default;
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
                Span<IntPtr> states2 = native2.GetStateCollection<IntPtr>();
                for (int j = 0; j < strs.Length; ++j)
                {
                    elements2[j] = HStringMarshaller3.ConvertToNativeValue(strs[j]);
                    states2[j] = elements2[j];
                }

                elements1[i] = native2.ToNative();
            }

            void* nstrs = (void*)native1.ToNative();

            // Invoke
            void* nres = __PInvoke(nstrs, elements1.Length);

            // Unmarshal
            managed = HStringMarshaller3.ConvertToManaged((IntPtr)nres);
        }
        finally
        {
            // Clean up
            Span<ArrayMarshaller<string>.State> states1 = native1.GetStateCollection<ArrayMarshaller<string>.State>();
            for (int i = 0; i < states1.Length; ++i)
            {
                native2.InitializeFreeNative(ref states1[i]);
                Span<IntPtr> states2 = native2.GetStateCollection<IntPtr>();
                for (int j = 0; j < states2.Length; ++j)
                {
                    HStringMarshaller3.FreeNativeValue(states2[j]);
                }
                native2.FreeNative();
            }
            native1.FreeNative();
        }

        return managed;

        // P/Invoke declaration
        static void* __PInvoke(void* a, int c) => Platform.ConcatMatrix((void***)a, c);
    }
}
