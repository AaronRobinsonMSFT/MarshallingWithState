// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ShapeIdeas;

public struct TManaged { }
public struct TNative { }

public sealed class CustomTypeMarshallerAttribute : Attribute
{
    public CustomTypeMarshallerAttribute(Type t)
    {
        ManagedType = t;
    }

    public Type ManagedType { get; }

    public Type InMarshaller { get; set; }
    public Type RefMarshaller { get; set; }
    public Type OutMarshaller { get; set; }
    public Type ElementMarshaller { get; set; }

    public struct GenericPlaceholder { }
}

public sealed class CustomTypeMarshallerFeaturesAttribute : Attribute
{
    public bool UnmanagedResources { get; set; }
    public bool CallerAllocatedBuffer { get; set; }
    public bool TwoStageMarshalling { get; set; }
    public int BufferSize { get; set; }
}

[CustomTypeMarshaller(typeof(TManaged),
    InMarshaller = typeof(In),
    RefMarshaller = typeof(Ref),
    OutMarshaller = typeof(Out),
    ElementMarshaller = typeof(Element))]
public unsafe static class Marshaller // Must be static class
{
    // Defined by public interface IMarshaller<TManaged, TNative> where TNative : unmanaged
    public static TNative ConvertToNativeValue(TManaged managed) => throw null;
    public static TManaged ConvertToManagedValue(TNative native) => throw null;
    public static void FreeNativeValue(TNative native) => throw null;

    [CustomTypeMarshallerFeatures]
    public unsafe ref struct In // Must be C# unmanaged
    {
        // P/Invoke
        public void InitializeForNativeValue(TManaged managed) => throw null; // Optional caller allocation, Span<T>
        public ref byte GetPinnableReference() => throw null;
        public TNative ToNativeValue() => throw null;
        public void FreeNativeValue() => throw null;

        // Reverse P/Invoke - Uses static functions
    }

    [CustomTypeMarshallerFeatures]
    public unsafe ref struct Ref // Must be C# unmanaged
    {
        public void InitializeForNativeValue(ref TManaged managed) => throw null; // Optional caller allocation, Span<T>
        public ref byte GetPinnableReference() => throw null;
        public TNative ToNativeValue() => throw null;
        public void InitializeForManagedValue(TNative native) => throw null;
        public TManaged ToManagedValue() => throw null;
        public void FreeNativeValue() => throw null;
    }

    [CustomTypeMarshallerFeatures]
    public unsafe ref struct Out // Must be C# unmanaged
    {
        // P/Invoke
        public void InitializeForManagedValue(TNative native) => throw null;
        public TManaged ToManagedValue() => throw null;
        public void FreeNativeValue() => throw null;

        // Reverse P/Invoke - Uses static functions
    }

    [CustomTypeMarshallerFeatures]
    public unsafe struct Element // Must be C# unmanaged and non-ByRefLike
    {
        public void InitializeForNativeValue(ref TManaged managed) => throw null;
        public ref byte GetPinnableReference() => throw null;
        public TNative ToNativeValue() => throw null;
        public void InitializeForManagedValue(TNative native) => throw null;
        public TManaged ToManagedValue() => throw null;
        public void FreeNativeValue() => throw null;
    }
}

[CustomTypeMarshaller(typeof(TManaged[]),
    InMarshaller = typeof(In),
    RefMarshaller = typeof(Ref),
    OutMarshaller = typeof(Out),
    ElementMarshaller = typeof(Element))]
public unsafe static class ArrayMarshaller // Must be static class
{
    // Defined by public interface IMarshaller<TManaged, TNative> where TNative : unmanaged
    public static TNative* ConvertToNativeValue(TManaged[] managed) => throw null;
    public static TManaged[] ConvertToManagedValue(TNative* native) => throw null;
    public static void FreeNativeValue(TNative* native) => throw null;

    // A special case for out by-value marshalling - for example, [Out] T[]
    public static void ConvertToManagedOutByValue(TNative* native, ref TManaged[] managed) => throw null;

    [CustomTypeMarshallerFeatures]
    public unsafe ref struct In // Must be C# unmanaged
    {
        // P/Invoke
        public void InitializeForNativeValue(TManaged[] managed) => throw null; // Optional caller allocation, Span<T>
        public ref byte GetPinnableReference() => throw null;
        public TNative* ToNativeValue() => throw null;
        public void FreeNativeValue() => throw null;

        // Reverse P/Invoke - Uses static functions
    }

    [CustomTypeMarshallerFeatures]
    public unsafe ref struct Ref // Must be C# unmanaged
    {
        public void InitializeForNativeValue(ref TManaged[] managed) => throw null; // Optional caller allocation, Span<T>
        public ref byte GetPinnableReference() => throw null;
        public TNative* ToNativeValue() => throw null;
        public void InitializeForManagedValue(TNative* native) => throw null;
        public TManaged[] ToManagedValue() => throw null;
        public void FreeNativeValue() => throw null;
    }

    [CustomTypeMarshallerFeatures]
    public unsafe ref struct Out // Must be C# unmanaged
    {
        // P/Invoke
        public void InitializeForManagedValue(TNative* native) => throw null;
        public TManaged[] ToManagedValue() => throw null;
        public void FreeNativeValue() => throw null;

        // Reverse P/Invoke - Uses static functions
    }

    [CustomTypeMarshallerFeatures]
    public unsafe struct Element // Must be C# unmanaged and non-ByRefLike
    {
        public void InitializeForNativeValue(ref TManaged[] managed) => throw null;
        public ref byte GetPinnableReference() => throw null;
        public TNative* ToNativeValue() => throw null;
        public void InitializeForManagedValue(TNative* native) => throw null;
        public TManaged[] ToManagedValue() => throw null;
        public void FreeNativeValue() => throw null;
    }
}

public static unsafe partial class NativeImport
{
    public static partial TManaged CallFunc(
        TManaged value,
        in TManaged in_value,
        ref TManaged ref_value,
        out TManaged out_value);

    public static partial TManaged[] CallFunc(
        TManaged[] value,
        [Out] TManaged[] out_byvalue,
        in TManaged[] in_value,
        ref TManaged[] ref_value,
        out TManaged[] out_value);
}

public static unsafe partial class NativeImport
{
    public static partial TManaged CallFunc(
        TManaged value,
        in TManaged in_value,
        ref TManaged ref_value,
        out TManaged out_value)
    {
        Unsafe.SkipInit(out out_value);

        Marshaller.In _byval_m_ = default;
        TNative _byval_ = default;

        Marshaller.In _in_m_ = default;
        TNative _in_ = default;

        Marshaller.Ref _ref_m_ = default;
        TNative _ref_ = default;

        Marshaller.Out _out_m_ = default;
        TNative _out_ = default;

        Marshaller.Out _ret_m_ = default;
        TNative _ret_ = default;

        TManaged _ret_value_ = default;

        try
        {
            // Marshal
            _byval_m_.InitializeForNativeValue(value);
            fixed (void* _byval_dummy_ = &_byval_m_.GetPinnableReference())
            {
                _byval_ = _byval_m_.ToNativeValue();

                _in_m_.InitializeForNativeValue(in_value);
                fixed (void* _in_dummy_ = &_in_m_.GetPinnableReference())
                {
                    _in_ = _in_m_.ToNativeValue();

                    _ref_m_.InitializeForNativeValue(ref ref_value);
                    fixed (void* _ref_dummy_ = &_ref_m_.GetPinnableReference())
                    {
                        _ref_ = _ref_m_.ToNativeValue();

                        // Invoke
                        _ret_ = __PInvoke(_byval_, &_in_, &_ref_, &_out_);
                    }
                }
            }

            // Unmarshal
            _ref_m_.InitializeForManagedValue(_ref_);
            ref_value = _ref_m_.ToManagedValue();

            _out_m_.InitializeForManagedValue(_out_);
            out_value = _out_m_.ToManagedValue();

            _ret_m_.InitializeForManagedValue(_ret_);
            _ret_value_ = _ret_m_.ToManagedValue();
        }
        finally
        {
            // Clean-up
            _byval_m_.FreeNativeValue();
            _in_m_.FreeNativeValue();
            _ref_m_.FreeNativeValue();
            _out_m_.FreeNativeValue();
            _ret_m_.FreeNativeValue();
        }

        return _ret_value_;

        // P/Invoke declaration
        static TNative __PInvoke(TNative v, TNative* in_v, TNative* r_v, TNative* o_v) => throw null;
    }

    public static partial TManaged[] CallFunc(
        TManaged[] value,
        /*[Out]*/ TManaged[] out_byvalue,
        in TManaged[] in_value,
        ref TManaged[] ref_value,
        out TManaged[] out_value)
    {
        Unsafe.SkipInit(out out_value);

        ArrayMarshaller.In _byval_m_ = default;
        TNative* _byval_ = default;

        ArrayMarshaller.In _out_byval_m_ = default;
        TNative* _out_byval_ = default;

        ArrayMarshaller.In _in_m_ = default;
        TNative* _in_ = default;

        ArrayMarshaller.Ref _ref_m_ = default;
        TNative* _ref_ = default;

        ArrayMarshaller.Out _out_m_ = default;
        TNative* _out_ = default;

        ArrayMarshaller.Out _ret_m_ = default;
        TNative* _ret_ = default;

        TManaged[] _ret_value_ = default;

        try
        {
            // Marshal
            _byval_m_.InitializeForNativeValue(value);
            fixed (void* _byval_dummy_ = &_byval_m_.GetPinnableReference())
            {
                _byval_ = _byval_m_.ToNativeValue();

                _out_byval_m_.InitializeForNativeValue(value);
                fixed (void* _out_byval_dummy_ = &_out_byval_m_.GetPinnableReference())
                {
                    _out_byval_ = _out_byval_m_.ToNativeValue();

                    _in_m_.InitializeForNativeValue(in_value);
                    fixed (void* _in_dummy_ = &_in_m_.GetPinnableReference())
                    {
                        _in_ = _in_m_.ToNativeValue();

                        _ref_m_.InitializeForNativeValue(ref ref_value);
                        fixed (void* _ref_dummy_ = &_ref_m_.GetPinnableReference())
                        {
                            _ref_ = _ref_m_.ToNativeValue();

                            // Invoke
                            _ret_ = __PInvoke(_byval_, _out_byval_, _in_, _ref_, &_out_);
                        }
                    }
                }
            }

            // Unmarshal

            ArrayMarshaller.ConvertToManagedOutByValue(_out_byval_, ref out_byvalue);

            _ref_m_.InitializeForManagedValue(_ref_);
            ref_value = _ref_m_.ToManagedValue();

            _out_m_.InitializeForManagedValue(_out_);
            out_value = _out_m_.ToManagedValue();

            _ret_m_.InitializeForManagedValue(_ret_);
            _ret_value_ = _ret_m_.ToManagedValue();
        }
        finally
        {
            // Clean-up
            _byval_m_.FreeNativeValue();
            _in_m_.FreeNativeValue();
            _ref_m_.FreeNativeValue();
            _out_m_.FreeNativeValue();
            _ret_m_.FreeNativeValue();
        }

        return _ret_value_;

        // P/Invoke declaration
        static TNative* __PInvoke(TNative* v, TNative* o_bv, TNative* in_v, TNative* r_v, TNative** o_v) => throw null;
    }
}
