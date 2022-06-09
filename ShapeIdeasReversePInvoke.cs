// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ShapeIdeasReversePInvoke;

#nullable enable
#pragma warning disable CS8597

// [NativeTypeMarshalling(typeof(Marshaller))]
public struct TManaged { }
public struct TNative { }

public enum CustomTypeMarshallerKind
{
    Stateless,
    StatelessLinearCollection,
    Stateful,
    StatefulLinearCollection
}

public sealed class CustomTypeMarshallerAttribute : Attribute
{
    public CustomTypeMarshallerAttribute(Type managedType, CustomTypeMarshallerKind kind) { }
    public CustomTypeMarshallerAttribute(Type managedType, CustomTypeMarshallerKind kind, int bufferSize) { }
    // The marshaller type must implement a `void FreeNative()` method. This method should not throw any exceptions
    public bool UnmanagedResources { get; set; }
    // The marshaller must implement a `void NotifyInvokeSucceeded()` method.
    // This method will be called by any source-generated marshaller after the "target invocation" has successfully completed.
    public bool NotifyForSuccessfulInvoke { get; set; }
    public bool GuaranteedUnmarshal { get; set; }

    public sealed class GenericPlaceholder { }
}

// Specify marshallers for P/Invoke scenarios
public sealed class ManagedToUnmanagedMarshallersAttribute : Attribute
{
    public ManagedToUnmanagedMarshallersAttribute(Type? inMarshaller, Type? refMarshaller, Type? outMarshaller)
    {

    }
}

// Specify marshallers for Reverse P/Invoke scenarios
public sealed class UnmanagedToManagedMarshallersAttribute : Attribute
{
    public UnmanagedToManagedMarshallersAttribute(Type? inMarshaller, Type? refMarshaller, Type? outMarshaller)
    {

    }
}

// Specify marshaller for array-element marshalling and default struct field marshalling
public sealed class ElementMarshallerAttribute : Attribute
{
    public ElementMarshallerAttribute(Type elementMarshaller)
    {

    }
}

[ManagedToUnmanagedMarshallers(
    typeof(ManagedToNative),
    typeof(Bidirectional),
    typeof(NativeToManaged))]
[UnmanagedToManagedMarshallers(typeof(NativeToManaged), typeof(Bidirectional), typeof(ManagedToNative))]
[ElementMarshaller(typeof(Element))]
public unsafe static class Marshaller // Must be static class
{
    [CustomTypeMarshaller(typeof(TManaged), CustomTypeMarshallerKind.Stateful)]
    public unsafe ref struct ManagedToNative
    {
        public void FromManaged(TManaged managed) => throw null; // Optional caller allocation, Span<T>
        public ref byte GetPinnableReference() => throw null; // Optional, allowed on all "stateful" shapes
        public TNative ToNativeValue() => throw null;
        public void FreeNativeValue() => throw null; // Should not throw exceptions.
    }

    [CustomTypeMarshaller(typeof(TManaged), CustomTypeMarshallerKind.Stateful)]
    public unsafe ref struct Bidirectional
    {
        public void FromManaged(TManaged managed) => throw null; // Optional caller allocation, Span<T>
        public ref byte GetPinnableReference() => throw null; // Optional, allowed on all "stateful" shapes
        public TNative ToNativeValue() => throw null; // Should not throw exceptions.
        public void FromNativeValue(TNative native) => throw null; // Should not throw exceptions.
        public TManaged ToManagedValue() => throw null;
        public void FreeNativeValue() => throw null; // Should not throw exceptions.
    }

    [CustomTypeMarshaller(typeof(TManaged), CustomTypeMarshallerKind.Stateful)]
    public unsafe ref struct NativeToManaged
    {
        public void FromNativeValue(TNative native) => throw null;
        public TManaged ToManagedValue() => throw null; // Should not throw exceptions.
        public void FreeNativeValue() => throw null; // Should not throw exceptions.
    }

    [CustomTypeMarshaller(typeof(TManaged), CustomTypeMarshallerKind.Stateless)] // Currently only support stateless. May support stateful in the future
    public static class Element
    {
        // Defined by public interface IMarshaller<TManaged, TNative> where TNative : unmanaged
        public static TNative ConvertToNativeValue(TManaged managed) => throw null;
        public static TManaged ConvertToManagedValue(TNative native) => throw null;
        public static void FreeNativeValue(TNative native) => throw null; // Should not throw exceptions.
    }
}

[ManagedToUnmanagedMarshallers(
    typeof(ArrayMarshaller<>.In),
    typeof(ArrayMarshaller<>),
    typeof(ArrayMarshaller<>.Out))]
[UnmanagedToManagedMarshallers(typeof(ArrayMarshaller<>), typeof(ArrayMarshaller<>), typeof(ArrayMarshaller<>))]
[ElementMarshaller(typeof(ArrayMarshaller<>))]
[CustomTypeMarshaller(typeof(CustomTypeMarshallerAttribute.GenericPlaceholder[]), CustomTypeMarshallerKind.StatelessLinearCollection, bufferSize: 0x200, UnmanagedResources = true)]
public unsafe static class ArrayMarshaller<T> // Must be static class
{
    // Defined by public interface IMarshaller<TManaged, TNative> where TNative : unmanaged
    public static byte* ConvertToNativeValue<TNativeElement>(T[]? managed)
        where TNativeElement : unmanaged
    {
        if (managed is null)
        {
            return null;
        }
        return (byte*)Marshal.AllocCoTaskMem(checked(sizeof(TNativeElement) * managed.Length));
    }

    public static ReadOnlySpan<T> GetManagedValuesSource(T[] managed) => managed;

    public static Span<T> GetManagedValuesDestination(T[] managed) => managed;

    public static int GetNumElements(T[] managed) => managed?.Length ?? 0;

    public static Span<TNativeElement> GetNativeValuesDestination<TNativeElement>(byte* nativeValue, int numElements)
        where TNativeElement : unmanaged => new Span<TNativeElement>(nativeValue, numElements);

    public static ReadOnlySpan<TNativeElement> GetNativeValuesSource<TNativeElement>(byte* nativeValue, int numElements)
        where TNativeElement : unmanaged => new Span<TNativeElement>(nativeValue, numElements);

    public static T[] CreateManagedValueForNumElements(int length) => new T[length];
    public static void FreeNativeValue(byte* native) => Marshal.FreeCoTaskMem((IntPtr)native);

    // TODO: do we want to specify direction on CustomTypeMarshallerAttribute still?
    // It would simplify the analyzer as we'd know exactly which members to check for without having to look at
    // the "entry point" type.
    [CustomTypeMarshaller(typeof(CustomTypeMarshallerAttribute.GenericPlaceholder[]), CustomTypeMarshallerKind.StatefulLinearCollection, bufferSize: 0x200, UnmanagedResources = true)]
    public unsafe ref struct In
    {
        private T[]? _managedArray;
        private IntPtr _allocatedMemory;
        private Span<byte> _span;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayMarshaller{T}"/>.
        /// </summary>
        /// <param name="array">Array to be marshalled.</param>
        /// <param name="buffer">Buffer that may be used for marshalling.</param>
        /// <param name="sizeOfNativeElement">Size of the native element in bytes.</param>
        /// <remarks>
        /// The <paramref name="buffer"/> must not be movable - that is, it should not be
        /// on the managed heap or it should be pinned.
        /// <seealso cref="CustomTypeMarshallerFeatures.CallerAllocatedBuffer"/>
        /// </remarks>
        public void FromManaged<TNativeElement>(T[]? array, Span<byte> buffer)
            where TNativeElement : unmanaged
        {
            _allocatedMemory = default;
            if (array is null)
            {
                _managedArray = null;
                _span = default;
                return;
            }

            _managedArray = array;

            // Always allocate at least one byte when the array is zero-length.
            int bufferSize = checked(array.Length * sizeof(TNativeElement));
            int spaceToAllocate = Math.Max(bufferSize, 1);
            if (spaceToAllocate <= buffer.Length)
            {
                _span = buffer[0..spaceToAllocate];
            }
            else
            {
                _allocatedMemory = Marshal.AllocCoTaskMem(spaceToAllocate);
                _span = new Span<byte>((void*)_allocatedMemory, spaceToAllocate);
            }
        }

        /// <summary>
        /// Gets a span that points to the memory where the managed values of the array are stored.
        /// </summary>
        /// <returns>Span over managed values of the array.</returns>
        /// <remarks>
        /// <seealso cref="CustomTypeMarshallerDirection.In"/>
        /// </remarks>
        public ReadOnlySpan<T> GetManagedValuesSource() => _managedArray;

        /// <summary>
        /// Returns a span that points to the memory where the native values of the array should be stored.
        /// </summary>
        /// <returns>Span where native values of the array should be stored.</returns>
        /// <remarks>
        /// <seealso cref="CustomTypeMarshallerDirection.In"/>
        /// </remarks>
        public Span<TNativeElement> GetNativeValuesDestination<TNativeElement>()
            where TNativeElement : unmanaged => MemoryMarshal.Cast<byte, TNativeElement>(_span);

        /// <summary>
        /// Returns a reference to the marshalled array.
        /// </summary>
        public ref byte GetPinnableReference() => ref MemoryMarshal.GetReference(_span);

        /// <summary>
        /// Returns the native value representing the array.
        /// </summary>
        /// <remarks>
        /// <seealso cref="CustomTypeMarshallerFeatures.TwoStageMarshalling"/>
        /// </remarks>
        public byte* ToNativeValue() => (byte*)Unsafe.AsPointer(ref GetPinnableReference());

        /// <summary>
        /// Sets the native value representing the array.
        /// </summary>
        /// <param name="value">The native value.</param>
        /// <remarks>
        /// <seealso cref="CustomTypeMarshallerFeatures.TwoStageMarshalling"/>
        /// </remarks>
        public void FromNativeValue(byte* value)
        {
            _allocatedMemory = (IntPtr)value;
        }

        /// <summary>
        /// Returns the managed array.
        /// </summary>
        /// <remarks>
        /// <seealso cref="CustomTypeMarshallerDirection.Out"/>
        /// </remarks>
        public T[]? ToManaged() => _managedArray;

        /// <summary>
        /// Frees native resources.
        /// </summary>
        /// <remarks>
        /// <seealso cref="CustomTypeMarshallerFeatures.UnmanagedResources"/>
        /// </remarks>
        public void FreeNative()
        {
            Marshal.FreeCoTaskMem(_allocatedMemory);
        }

        public static ref T GetPinnableReference(T[] managed) // Optional, allowed on all shapes
        {
            if (managed is null)
            {
                return ref Unsafe.NullRef<T>();
            }
            return ref MemoryMarshal.GetArrayDataReference(managed);
        }
    }

    [CustomTypeMarshaller(typeof(CustomTypeMarshallerAttribute.GenericPlaceholder[]), CustomTypeMarshallerKind.StatefulLinearCollection, UnmanagedResources = true)]
    public unsafe ref struct Out
    {
        private T[]? _managedArray;
        private IntPtr _allocatedMemory;
        private Span<byte> _span;

        /// <summary>
        /// Gets a span that points to the memory where the unmarshalled managed values of the array should be stored.
        /// </summary>
        /// <param name="length">Length of the array.</param>
        /// <returns>Span where managed values of the array should be stored.</returns>
        public Span<T> GetManagedValuesDestination(int length) => _allocatedMemory == IntPtr.Zero ? null : _managedArray = new T[length];

        /// <summary>
        /// Returns a span that points to the memory where the native values of the array are stored after the native call.
        /// </summary>
        /// <param name="length">Length of the array.</param>
        /// <returns>Span over the native values of the array.</returns>
        public ReadOnlySpan<TNativeElement> GetNativeValuesSource<TNativeElement>(int length)
            where TNativeElement : unmanaged
        {
            if (_allocatedMemory == IntPtr.Zero)
                return default;

            Span<TNativeElement> spanOverNativeValues = new Span<TNativeElement>((void*)_allocatedMemory, length);
            _span = MemoryMarshal.AsBytes(spanOverNativeValues);
            return spanOverNativeValues;
        }

        /// <summary>
        /// Returns a reference to the marshalled array.
        /// </summary>
        public ref byte GetPinnableReference() => ref MemoryMarshal.GetReference(_span);

        /// <summary>
        /// Sets the native value representing the array.
        /// </summary>
        /// <param name="value">The native value.</param>
        public void FromNativeValue(byte* value)
        {
            _allocatedMemory = (IntPtr)value;
        }

        /// <summary>
        /// Returns the managed array.
        /// </summary>
        public T[]? ToManaged() => _managedArray;

        /// <summary>
        /// Frees native resources.
        /// </summary>
        /// <remarks>
        /// <seealso cref="CustomTypeMarshallerFeatures.UnmanagedResources"/>
        /// </remarks>
        public void FreeNative()
        {
            Marshal.FreeCoTaskMem(_allocatedMemory);
        }
    }
}

public static unsafe partial class NativeImport
{
    public static partial TManaged CallFunc(
        TManaged value,
        in TManaged in_value,
        ref TManaged ref_value,
        out TManaged out_value);

    // [return: MarshalUsing(ConstantElementCount = 30)]
    public static unsafe partial TManaged[] CallFunc(
        int[] value_blittable,
        TManaged[] value,
        [Out] TManaged[] out_byvalue,
        in TManaged[] in_value,
        // [MarshalUsing(ConstantElementCount = 10)]
        ref TManaged[] ref_value,
        // [MarshalUsing(ConstantElementCount = 20)]
        out TManaged[] out_value);
}

#nullable disable

public static unsafe partial class NativeImport
{
    public static partial TManaged CallFunc(
        TManaged value,
        in TManaged in_value,
        ref TManaged ref_value,
        out TManaged out_value)
    {
        Unsafe.SkipInit(out out_value);

        Marshaller.ManagedToNative _byval_m_ = default;
        TNative _byval_ = default;

        Marshaller.ManagedToNative _in_m_ = default;
        TNative _in_ = default;

        Marshaller.Bidirectional _ref_m_ = default;
        TNative _ref_ = default;

        Marshaller.NativeToManaged _out_m_ = default;
        TNative _out_ = default;

        Marshaller.NativeToManaged _ret_m_ = default;
        TNative _ret_ = default;

        TManaged _ret_value_ = default;

        try
        {
            // Marshal
            _byval_m_.FromManaged(value);
            fixed (void* _byval_dummy_ = &_byval_m_.GetPinnableReference())
            {
                _byval_ = _byval_m_.ToNativeValue();

                _in_m_.FromManaged(in_value);
                fixed (void* _in_dummy_ = &_in_m_.GetPinnableReference())
                {
                    _in_ = _in_m_.ToNativeValue();

                    _ref_m_.FromManaged(ref_value);
                    fixed (void* _ref_dummy_ = &_ref_m_.GetPinnableReference())
                    {
                        _ref_ = _ref_m_.ToNativeValue();

                        // Invoke
                        _ret_ = __PInvoke(_byval_, &_in_, &_ref_, &_out_);
                    }
                }
            }

            // Unmarshal
            _ref_m_.FromNativeValue(_ref_);
            ref_value = _ref_m_.ToManagedValue();

            _out_m_.FromNativeValue(_out_);
            out_value = _out_m_.ToManagedValue();

            _ret_m_.FromNativeValue(_ret_);
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

    public static unsafe partial TManaged[] CallFunc(
        int[] value_blittable,
        TManaged[] value,
        /*[Out]*/ TManaged[] out_byvalue,
        in TManaged[] in_value,
        ref TManaged[] ref_value,
        out TManaged[] out_value)
    {
        Unsafe.SkipInit(out out_value);

        ArrayMarshaller<TManaged>.In _byval_m_ = new();
        byte* _byval_ = default;

        ArrayMarshaller<TManaged>.In _out_byval_m_ = new();
        byte* _out_byval_ = default;

        ArrayMarshaller<TManaged>.In _in_m_ = new();
        byte* _in_ = default;

        byte* _ref_ = default;

        ArrayMarshaller<TManaged>.Out _out_m_ = new();
        byte* _out_ = default;

        ArrayMarshaller<TManaged>.Out _ret_m_ = new();
        byte* _ret_ = default;

        TManaged[] _ret_value_ = default;

        try
        {
            // Marshal
            byte* byval_m_ptr = stackalloc byte[0x200];
            _byval_m_.FromManaged<TNative>(value, new Span<byte>(byval_m_ptr, 0x200));

            {
                ReadOnlySpan<TManaged> _byval_m_managed_source = _byval_m_.GetManagedValuesSource();
                Span<TNative> _byval_m_native_source = _byval_m_.GetNativeValuesDestination<TNative>();

                for (int i = 0; i < _byval_m_managed_source.Length; i++)
                {
                    _byval_m_native_source[i] = Marshaller.Element.ConvertToNativeValue(_byval_m_managed_source[i]);
                }
            }

            byte* out_byval_m_ptr = stackalloc byte[0x200];
            _out_byval_m_.FromManaged<TNative>(value, new Span<byte>(out_byval_m_ptr, 0x200));

            byte* in_m_ptr = stackalloc byte[0x200];
            _in_m_.FromManaged<TNative>(value, new Span<byte>(in_m_ptr, 0x200));

            {
                ReadOnlySpan<TManaged> _in_m_managed_source = _byval_m_.GetManagedValuesSource();
                Span<TNative> _in_m_native_source = _in_m_.GetNativeValuesDestination<TNative>();

                for (int i = 0; i < _in_m_managed_source.Length; i++)
                {
                    _in_m_native_source[i] = Marshaller.Element.ConvertToNativeValue(_in_m_managed_source[i]);
                }
            }

            _ref_ = ArrayMarshaller<TManaged>.ConvertToNativeValue<IntPtr>(ref_value);
            {
                ReadOnlySpan<TManaged> _ref_m_managed_source = _byval_m_.GetManagedValuesSource();
                Span<TNative> _ref_m_native_source = ArrayMarshaller<TManaged>.GetNativeValuesDestination<TNative>(_ref_, ArrayMarshaller<TManaged>.GetNumElements(ref_value));

                for (int i = 0; i < _ref_m_managed_source.Length; i++)
                {
                    _ref_m_native_source[i] = Marshaller.Element.ConvertToNativeValue(_ref_m_managed_source[i]);
                }
            }

            // Pin
            fixed (void* _byval_blittable_ = &ArrayMarshaller<int>.In.GetPinnableReference(value_blittable))
            fixed (void* _byval_dummy_ = _byval_m_)
            {
                _byval_ = _byval_m_.ToNativeValue();
                fixed (void* _out_byval_dummy_ = _out_byval_m_)
                {
                    _out_byval_ = _out_byval_m_.ToNativeValue();
                    fixed (void* _in_dummy_ = _in_m_)
                    {
                        _in_ = _in_m_.ToNativeValue();

                        // Invoke
                        _ret_ = __PInvoke(_byval_blittable_, _byval_, _out_byval_, _in_, _ref_, &_out_);
                    }
                }
            }

            // Unmarshal setup
            // We do FromNativeValue here to capture the native values so we do not leak if later unmarshalling throws.
            _out_m_.FromNativeValue(_out_);
            _ret_m_.FromNativeValue(_ret_);

            // Unmarshal
            {
                Span<TManaged> _out_byval_managed_destination = MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in MemoryMarshal.GetReference(_out_byval_m_.GetManagedValuesSource())), _out_byval_m_.GetManagedValuesSource().Length);
                ReadOnlySpan<TNative> _out_byval_native_source = _out_byval_m_.GetNativeValuesDestination<TNative>();

                for (int i = 0; i < _out_byval_managed_destination.Length; i++)
                {
                    _out_byval_managed_destination[i] = Marshaller.Element.ConvertToManagedValue(_out_byval_native_source[i]);
                }
            }

            {
                TManaged[] ref_local = ArrayMarshaller<TManaged>.CreateManagedValueForNumElements(10);
                Span<TManaged> ref_managed_values_destination = ArrayMarshaller<TManaged>.GetManagedValuesDestination(ref_local);
                ReadOnlySpan<TNative> ref_native_values_source = ArrayMarshaller<TManaged>.GetNativeValuesSource<TNative>(_ref_, 10);

                for (int i = 0; i < ref_managed_values_destination.Length; i++)
                {
                    ref_managed_values_destination[i] = Marshaller.Element.ConvertToManagedValue(ref_native_values_source[i]);
                }
                ref_value = ref_local;
            }

            {
                Span<TManaged> out_managed_values_destination = _out_m_.GetManagedValuesDestination(20);
                ReadOnlySpan<TNative> ref_native_values_source = _out_m_.GetNativeValuesSource<TNative>(20);

                for (int i = 0; i < out_managed_values_destination.Length; i++)
                {
                    out_managed_values_destination[i] = Marshaller.Element.ConvertToManagedValue(ref_native_values_source[i]);
                }
                out_value = _out_m_.ToManaged();
            }

            {
                Span<TManaged> ret_managed_values_destination = _ret_m_.GetManagedValuesDestination(20);
                ReadOnlySpan<TNative> ref_native_values_source = _ret_m_.GetNativeValuesSource<TNative>(20);

                for (int i = 0; i < ret_managed_values_destination.Length; i++)
                {
                    ret_managed_values_destination[i] = Marshaller.Element.ConvertToManagedValue(ref_native_values_source[i]);
                }
                _ret_value_ = _ret_m_.ToManaged();
            }
        }
        finally
        {
            // Clean-up
            _byval_m_.FreeNative();
            _in_m_.FreeNative();
            ArrayMarshaller<TManaged>.FreeNativeValue(_ref_);
            _out_m_.FreeNative();
            _ret_m_.FreeNative();
        }

        return _ret_value_;

        // P/Invoke declaration
        static byte* __PInvoke(void* v_b, byte* v, byte* o_bv, byte* in_v, byte* r_v, byte** o_v) => throw null;
    }
}
