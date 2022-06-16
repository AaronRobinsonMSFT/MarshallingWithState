// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ShapeIdeasReversePInvoke;

#nullable enable
#pragma warning disable CS8597

// [UnmanagedTypeMarshalling(typeof(Marshaller))]
public struct TManaged { }
public struct TUnmanaged { }


/// <summary>
/// Define features for a custom type marshaller.
/// </summary>
public sealed class CustomTypeMarshallerFeaturesAttribute : Attribute
{
    /// <summary>
    /// Desired caller buffer size for the marshaller.
    /// </summary>
    public int BufferSize { get; set; }
}


/// <summary>
/// Base class attribute for custom marshaller attributes.
/// </summary>
/// <remarks>
/// Use a base class here to allow doing ManagedToUnmanagedMarshallersAttribute.GenericPlaceholder, etc. without having 3 separate placeholder types.
/// For the following attribute types, any marshaller types that are provided will be validated by an analyzer to have the correct members to prevent
/// developers from accidentally typoing a member like Free() and causing memory leaks.
/// </remarks>
public abstract class CustomUnmanagedTypeMarshallersAttributeBase : Attribute
{
    /// <summary>
    /// Placeholder type for generic parameter
    /// </summary>
    public sealed class GenericPlaceholder { }
}

/// <summary>
/// Specify marshallers used in the managed to unmanaged direction (that is, P/Invoke)
/// </summary>
public sealed class ManagedToUnmanagedMarshallersAttribute : CustomUnmanagedTypeMarshallersAttributeBase
{
    /// <summary>
    /// Create instance of <see cref="ManagedToUnmanagedMarshallersAttribute"/>.
    /// </summary>
    /// <param name="managedType">Managed type to marshal</param>
    public ManagedToUnmanagedMarshallersAttribute(Type managedType) { }

    /// <summary>
    /// Marshaller to use when a parameter of the managed type is passed by-value or with the <c>in</c> keyword.
    /// </summary>
    public Type? InMarshaller { get; set; }

    /// <summary>
    /// Marshaller to use when a parameter of the managed type is passed by-value or with the <c>ref</c> keyword.
    /// </summary>
    public Type? RefMarshaller { get; set; }

    /// <summary>
    /// Marshaller to use when a parameter of the managed type is passed by-value or with the <c>out</c> keyword.
    /// </summary>
    public Type? OutMarshaller { get; set; }
}

/// <summary>
/// Specify marshallers used in the unmanaged to managed direction (that is, Reverse P/Invoke)
/// </summary>
public sealed class UnmanagedToManagedMarshallersAttribute : CustomUnmanagedTypeMarshallersAttributeBase
{
    /// <summary>
    /// Create instance of <see cref="UnmanagedToManagedMarshallersAttribute"/>.
    /// </summary>
    /// <param name="managedType">Managed type to marshal</param>
    public UnmanagedToManagedMarshallersAttribute(Type managedType) { }

    /// <summary>
    /// Marshaller to use when a parameter of the managed type is passed by-value or with the <c>in</c> keyword.
    /// </summary>
    public Type? InMarshaller { get; set; }

    /// <summary>
    /// Marshaller to use when a parameter of the managed type is passed by-value or with the <c>ref</c> keyword.
    /// </summary>
    public Type? RefMarshaller { get; set; }

    /// <summary>
    /// Marshaller to use when a parameter of the managed type is passed by-value or with the <c>out</c> keyword.
    /// </summary>
    public Type? OutMarshaller { get; set; }
}

/// <summary>
/// Specify marshaller for array-element marshalling and default struct field marshalling.
/// </summary>
public sealed class ElementMarshallerAttribute : CustomUnmanagedTypeMarshallersAttributeBase
{
    /// <summary>
    /// Create instance of <see cref="ElementMarshallerAttribute"/>.
    /// </summary>
    /// <param name="managedType">Managed type to marshal</param>
    /// <param name="elementMarshaller">Marshaller type to use for marshalling <paramref name="managedType"/>.</param>
    public ElementMarshallerAttribute(Type managedType, Type elementMarshaller) { }
}

/// <summary>
/// Specifies that a particular generic parameter is the collection element's unmanaged type.
/// </summary>
/// <remarks>
/// If this attribute is provided on a generic parameter of a marshaller, then the generator will assume
/// that it is a linear collection marshaller.
/// </remarks>
[AttributeUsage(AttributeTargets.GenericParameter)]
public sealed class ElementUnmanagedTypeAttribute : Attribute
{
}

[ManagedToUnmanagedMarshallers(
    typeof(TManaged),
    InMarshaller = typeof(ManagedToUnmanaged),
    RefMarshaller = typeof(Bidirectional),
    OutMarshaller = typeof(UnmanagedToManaged))]
[UnmanagedToManagedMarshallers(
    typeof(TManaged),
    InMarshaller = typeof(UnmanagedToManaged),
    RefMarshaller = typeof(Bidirectional),
    OutMarshaller = typeof(ManagedToUnmanaged))]
[ElementMarshaller(
    typeof(TManaged),
    typeof(Element))]
public unsafe static class Marshaller // Must be static class
{
    public unsafe ref struct ManagedToUnmanaged
    {
        public void FromManaged(TManaged managed) => throw null; // Optional caller allocation, Span<T>
        public ref byte GetPinnableReference() => throw null; // Optional, allowed on all "stateful" shapes
        public TUnmanaged ToUnmanaged() => throw null;
        public void Free() => throw null; // Should not throw exceptions. Use Free instead of Dispose to avoid issues with marshallers needing to follow the Dispose pattern guidance.
    }

    public unsafe ref struct Bidirectional
    {
        public void FromManaged(TManaged managed) => throw null; // Optional caller allocation, Span<T>
        public ref byte GetPinnableReference() => throw null; // Optional, allowed on all "stateful" shapes.
        public TUnmanaged ToUnmanaged() => throw null; // Should not throw exceptions.
        public void FromUnmanaged(TUnmanaged native) => throw null; // Should not throw exceptions.
        public TManaged ToManaged() => throw null;
        public void Free() => throw null; // Should not throw exceptions.
    }

    public unsafe struct UnmanagedToManaged
    {
        public void FromUnmanaged(TUnmanaged native) => throw null;
        public TManaged ToManaged() => throw null; // Should not throw exceptions.
        public void Free() => throw null; // Should not throw exceptions.
    }

    // Currently only support stateless. May support stateful in the future
    public static class Element
    {
        // Defined by public interface IMarshaller<TManaged, TUnmanaged> where TUnmanaged : unmanaged
        public static TUnmanaged ConvertToUnmanaged(TManaged managed) => throw null;
        public static TManaged ConvertToManaged(TUnmanaged native) => throw null;
        public static void Free(TUnmanaged native) => throw null; // Should not throw exceptions.
    }
}

[ManagedToUnmanagedMarshallers(typeof(ManagedToUnmanagedMarshallersAttribute.GenericPlaceholder[]),
    InMarshaller = typeof(ArrayMarshaller<,>.In),
    RefMarshaller = typeof(ArrayMarshaller<,>),
    OutMarshaller = typeof(ArrayMarshaller<,>.Out))]
[UnmanagedToManagedMarshallers(typeof(UnmanagedToManagedMarshallersAttribute.GenericPlaceholder[]),
    InMarshaller = typeof(ArrayMarshaller<,>),
    RefMarshaller = typeof(ArrayMarshaller<,>),
    OutMarshaller = typeof(ArrayMarshaller<,>))]
[ElementMarshaller(typeof(ElementMarshallerAttribute.GenericPlaceholder[]), typeof(ArrayMarshaller<,>))]
[CustomTypeMarshallerFeatures(BufferSize = 20)]
public unsafe static class ArrayMarshaller<T, [ElementUnmanagedType] TUnmanagedElement> // Must be static class
    where TUnmanagedElement : unmanaged
{
    // Defined by public interface IMarshaller<TManaged, TUnmanaged> where TUnmanaged : unmanaged
    public static byte* AllocateContainerForUnmanagedElements(T[]? managed, out int numElements)
    {
        if (managed is null)
        {
            numElements = 0;
            return null;
        }
        numElements = managed.Length;
        return (byte*)Marshal.AllocCoTaskMem(checked(sizeof(TUnmanagedElement) * numElements));
    }

    public static ReadOnlySpan<T> GetManagedValuesSource(T[] managed) => managed;

    public static Span<T> GetManagedValuesDestination(T[] managed) => managed;

    public static Span<TUnmanagedElement> GetUnmanagedValuesDestination(byte* nativeValue, int numElements)
        => new Span<TUnmanagedElement>(nativeValue, numElements);

    public static ReadOnlySpan<TUnmanagedElement> GetUnmanagedValuesSource(byte* nativeValue, int numElements)
        => new Span<TUnmanagedElement>(nativeValue, numElements);

    public static T[] AllocateContainerForManagedElements(int length) => new T[length];
    public static void Free(byte* native) => Marshal.FreeCoTaskMem((IntPtr)native);

    [CustomTypeMarshallerFeatures(BufferSize = 20)] // As our buffer is typed to our native element type, we limit the buffer size based on number of elements, not number of bytes.
    public unsafe ref struct In
    {
        private T[]? _managedArray;
        private IntPtr _allocatedMemory;
        private Span<TUnmanagedElement> _span;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayMarshaller{T}"/>.
        /// </summary>
        /// <param name="array">Array to be marshalled.</param>
        /// <param name="buffer">Buffer that may be used for marshalling.</param>
        /// <param name="sizeOfUnmanagedElement">Size of the native element in bytes.</param>
        /// <remarks>
        /// The <paramref name="buffer"/> must not be movable - that is, it should not be
        /// on the managed heap or it should be pinned.
        /// <seealso cref="CustomTypeMarshallerFeatures.CallerAllocatedBuffer"/>
        /// </remarks>
        public void FromManaged(T[]? array, Span<TUnmanagedElement> buffer)
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
            int bufferSize = checked(array.Length * sizeof(TUnmanagedElement));
            int spaceToAllocate = Math.Max(bufferSize, 1);
            if (spaceToAllocate <= buffer.Length)
            {
                _span = buffer[0..spaceToAllocate];
            }
            else
            {
                _allocatedMemory = Marshal.AllocCoTaskMem(spaceToAllocate);
                _span = new Span<TUnmanagedElement>((void*)_allocatedMemory, spaceToAllocate);
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
        public Span<TUnmanagedElement> GetUnmanagedValuesDestination() => _span;

        /// <summary>
        /// Returns a reference to the marshalled array.
        /// </summary>
        public ref TUnmanagedElement GetPinnableReference() => ref MemoryMarshal.GetReference(_span);

        /// <summary>
        /// Returns the native value representing the array.
        /// </summary>
        public byte* ToUnmanaged() => (byte*)Unsafe.AsPointer(ref GetPinnableReference());

        /// <summary>
        /// Sets the native value representing the array.
        /// </summary>
        /// <param name="value">The native value.</param>
        public void FromUnmanaged(byte* value)
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
        public void Free()
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

    public unsafe ref struct Out
    {
        private T[]? _managedArray;
        private IntPtr _allocatedMemory;
        private Span<TUnmanagedElement> _span;

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
        public ReadOnlySpan<TUnmanagedElement> GetUnmanagedValuesSource(int length)
        {
            if (_allocatedMemory == IntPtr.Zero)
                return default;

            return _span = new Span<TUnmanagedElement>((void*)_allocatedMemory, length);
        }

        /// <summary>
        /// Returns a reference to the marshalled array.
        /// </summary>
        public ref TUnmanagedElement GetPinnableReference() => ref MemoryMarshal.GetReference(_span);

        /// <summary>
        /// Sets the native value representing the array.
        /// </summary>
        /// <param name="value">The native value.</param>
        public void FromUnmanaged(byte* value)
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
        public void Free()
        {
            Marshal.FreeCoTaskMem(_allocatedMemory);
        }
    }
}

public static unsafe partial class UnmanagedImport
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

public static unsafe partial class UnmanagedImport
{
    public static partial TManaged CallFunc(
        TManaged value,
        in TManaged in_value,
        ref TManaged ref_value,
        out TManaged out_value)
    {
        Unsafe.SkipInit(out out_value);

        Marshaller.ManagedToUnmanaged _byval_m_ = default;
        TUnmanaged _byval_ = default;

        Marshaller.ManagedToUnmanaged _in_m_ = default;
        TUnmanaged _in_ = default;

        Marshaller.Bidirectional _ref_m_ = default;
        TUnmanaged _ref_ = default;

        Marshaller.UnmanagedToManaged _out_m_ = default;
        TUnmanaged _out_ = default;

        Marshaller.UnmanagedToManaged _ret_m_ = default;
        TUnmanaged _ret_ = default;

        TManaged _ret_value_ = default;

        try
        {
            // Marshal
            _byval_m_.FromManaged(value);
            fixed (void* _byval_dummy_ = &_byval_m_.GetPinnableReference())
            {
                _byval_ = _byval_m_.ToUnmanaged();

                _in_m_.FromManaged(in_value);
                fixed (void* _in_dummy_ = &_in_m_.GetPinnableReference())
                {
                    _in_ = _in_m_.ToUnmanaged();

                    _ref_m_.FromManaged(ref_value);
                    fixed (void* _ref_dummy_ = &_ref_m_.GetPinnableReference())
                    {
                        _ref_ = _ref_m_.ToUnmanaged();

                        // Invoke
                        _ret_ = __PInvoke(_byval_, &_in_, &_ref_, &_out_);
                    }
                }
            }

            // Unmarshal setup
            // We do FromUnmanaged here to capture the native values so we do not leak if later unmarshalling throws.
            _ref_m_.FromUnmanaged(_ref_);
            _out_m_.FromUnmanaged(_out_);
            _ret_m_.FromUnmanaged(_ret_);

            // Unmarshal
            ref_value = _ref_m_.ToManaged();
            out_value = _out_m_.ToManaged();
            _ret_value_ = _ret_m_.ToManaged();
        }
        finally
        {
            // Clean-up
            _byval_m_.Free();
            _in_m_.Free();
            _ref_m_.Free();
            _out_m_.Free();
            _ret_m_.Free();
        }

        return _ret_value_;

        // P/Invoke declaration
        static TUnmanaged __PInvoke(TUnmanaged v, TUnmanaged* in_v, TUnmanaged* r_v, TUnmanaged* o_v) => throw null;
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

        ArrayMarshaller<TManaged, TUnmanaged>.In _byval_m_ = new();
        byte* _byval_ = default;

        ArrayMarshaller<TManaged, TUnmanaged>.In _out_byval_m_ = new();
        byte* _out_byval_ = default;

        ArrayMarshaller<TManaged, TUnmanaged>.In _in_m_ = new();
        byte* _in_ = default;

        byte* _ref_ = default;

        ArrayMarshaller<TManaged, TUnmanaged>.Out _out_m_ = new();
        byte* _out_ = default;

        ArrayMarshaller<TManaged, TUnmanaged>.Out _ret_m_ = new();
        byte* _ret_ = default;

        TManaged[] _ret_value_ = default;

        try
        {
            // Marshal
            byte* byval_m_ptr = stackalloc byte[0x200];
            _byval_m_.FromManaged(value, new Span<TUnmanaged>(byval_m_ptr, 20));

            {
                ReadOnlySpan<TManaged> _byval_m_managed_source = _byval_m_.GetManagedValuesSource();
                Span<TUnmanaged> _byval_m_native_source = _byval_m_.GetUnmanagedValuesDestination();

                for (int i = 0; i < _byval_m_managed_source.Length; i++)
                {
                    _byval_m_native_source[i] = Marshaller.Element.ConvertToUnmanaged(_byval_m_managed_source[i]);
                }
            }

            TUnmanaged* out_byval_m_ptr = stackalloc TUnmanaged[20];
            _out_byval_m_.FromManaged(value, new Span<TUnmanaged>(out_byval_m_ptr, 20));

            TUnmanaged* in_m_ptr = stackalloc TUnmanaged[20];
            _in_m_.FromManaged(value, new Span<TUnmanaged>(in_m_ptr, 20));

            {
                ReadOnlySpan<TManaged> _in_m_managed_source = _byval_m_.GetManagedValuesSource();
                Span<TUnmanaged> _in_m_native_source = _in_m_.GetUnmanagedValuesDestination();

                for (int i = 0; i < _in_m_managed_source.Length; i++)
                {
                    _in_m_native_source[i] = Marshaller.Element.ConvertToUnmanaged(_in_m_managed_source[i]);
                }
            }

            _ref_ = ArrayMarshaller<TManaged, TUnmanaged>.AllocateContainerForUnmanagedElements(ref_value, out int _ref_num_elements);
            {
                ReadOnlySpan<TManaged> _ref_m_managed_source = _byval_m_.GetManagedValuesSource();
                Span<TUnmanaged> _ref_m_native_source = ArrayMarshaller<TManaged, TUnmanaged>.GetUnmanagedValuesDestination(_ref_, _ref_num_elements);

                for (int i = 0; i < _ref_m_managed_source.Length; i++)
                {
                    _ref_m_native_source[i] = Marshaller.Element.ConvertToUnmanaged(_ref_m_managed_source[i]);
                }
            }

            // Pin
            fixed (void* _byval_blittable_ = &ArrayMarshaller<int, int>.In.GetPinnableReference(value_blittable))
            fixed (void* _byval_dummy_ = _byval_m_)
            {
                _byval_ = _byval_m_.ToUnmanaged();
                fixed (void* _out_byval_dummy_ = _out_byval_m_)
                {
                    _out_byval_ = _out_byval_m_.ToUnmanaged();
                    fixed (void* _in_dummy_ = _in_m_)
                    {
                        _in_ = _in_m_.ToUnmanaged();

                        // Invoke
                        _ret_ = __PInvoke(_byval_blittable_, _byval_, _out_byval_, _in_, _ref_, &_out_);
                    }
                }
            }

            // Unmarshal setup
            // We do FromUnmanaged here to capture the native values so we do not leak if later unmarshalling throws.
            _out_m_.FromUnmanaged(_out_);
            _ret_m_.FromUnmanaged(_ret_);

            // Unmarshal
            {
                Span<TManaged> _out_byval_managed_destination = MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in MemoryMarshal.GetReference(_out_byval_m_.GetManagedValuesSource())), _out_byval_m_.GetManagedValuesSource().Length);
                ReadOnlySpan<TUnmanaged> _out_byval_native_source = _out_byval_m_.GetUnmanagedValuesDestination();

                for (int i = 0; i < _out_byval_managed_destination.Length; i++)
                {
                    _out_byval_managed_destination[i] = Marshaller.Element.ConvertToManaged(_out_byval_native_source[i]);
                }
            }

            {
                TManaged[] ref_local = ArrayMarshaller<TManaged, TUnmanaged>.AllocateContainerForManagedElements(10);
                Span<TManaged> ref_managed_values_destination = ArrayMarshaller<TManaged, TUnmanaged>.GetManagedValuesDestination(ref_local);
                ReadOnlySpan<TUnmanaged> ref_native_values_source = ArrayMarshaller<TManaged, TUnmanaged>.GetUnmanagedValuesSource(_ref_, 10);

                for (int i = 0; i < ref_managed_values_destination.Length; i++)
                {
                    ref_managed_values_destination[i] = Marshaller.Element.ConvertToManaged(ref_native_values_source[i]);
                }
                ref_value = ref_local;
            }

            {
                Span<TManaged> out_managed_values_destination = _out_m_.GetManagedValuesDestination(20);
                ReadOnlySpan<TUnmanaged> ref_native_values_source = _out_m_.GetUnmanagedValuesSource(20);

                for (int i = 0; i < out_managed_values_destination.Length; i++)
                {
                    out_managed_values_destination[i] = Marshaller.Element.ConvertToManaged(ref_native_values_source[i]);
                }
                out_value = _out_m_.ToManaged();
            }

            {
                Span<TManaged> ret_managed_values_destination = _ret_m_.GetManagedValuesDestination(20);
                ReadOnlySpan<TUnmanaged> ref_native_values_source = _ret_m_.GetUnmanagedValuesSource(20);

                for (int i = 0; i < ret_managed_values_destination.Length; i++)
                {
                    ret_managed_values_destination[i] = Marshaller.Element.ConvertToManaged(ref_native_values_source[i]);
                }
                _ret_value_ = _ret_m_.ToManaged();
            }
        }
        finally
        {
            // Clean-up
            _byval_m_.Free();
            _in_m_.Free();
            ArrayMarshaller<TManaged, TUnmanaged>.Free(_ref_);
            _out_m_.Free();
            _ret_m_.Free();
        }

        return _ret_value_;

        // P/Invoke declaration
        static byte* __PInvoke(void* v_b, byte* v, byte* o_bv, byte* in_v, byte* r_v, byte** o_v) => throw null;
    }
}
