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

public sealed class CustomTypeMarshallerAttribute : Attribute
{
    // We use a bool parameter here instead of an enum since we are going to move to interfaces in C# v.Next
    // to define our shapes as ref-struct generic parameters and ref-structs implementing interfaces are
    // planned for C# v.Next as one of the large features.
    // This way, we can [Obsolete] these constructors, introduce new ones that don't take the bool,
    // and push people to use the interfaces without having to obsolete a new type (ie. a CustomTypeMarshallerKind enum).
    public CustomTypeMarshallerAttribute(bool hasState) { }
    public CustomTypeMarshallerAttribute(bool hasState, int bufferSize) { }
    // The marshaller must implement a `void NotifyInvokeSucceeded()` method.
    // This method will be called by any source-generated marshaller after the "target invocation" has successfully completed.
    public bool NotifyForSuccessfulInvoke { get; set; }
    public bool GuaranteedUnmarshal { get; set; }

    public sealed class GenericPlaceholder { }
}

// Specify marshallers for P/Invoke scenarios
public sealed class ManagedToUnmanagedMarshallersAttribute : Attribute
{
    public ManagedToUnmanagedMarshallersAttribute(Type managedType, Type? inMarshaller, Type? refMarshaller, Type? outMarshaller)
    {

    }
}

// Specify marshallers for Reverse P/Invoke scenarios
public sealed class UnmanagedToManagedMarshallersAttribute : Attribute
{
    public UnmanagedToManagedMarshallersAttribute(Type managedType, Type? inMarshaller, Type? refMarshaller, Type? outMarshaller)
    {

    }
}

// Specify marshaller for array-element marshalling and default struct field marshalling
public sealed class ElementMarshallerAttribute : Attribute
{
    public ElementMarshallerAttribute(Type managedType, Type elementMarshaller)
    {

    }
}

// Specifies that a particular generic parameter is the collection element's unmanaged type.
// If this attribute is provided on a generic parameter of a marshaller, then the generator will assume that it is a linear collection
// marshaller.
// TODO: This only works if we plan to move towards interfaces in the future. Otherwise if we ever introduce another collection shape, we'll
// have to disambiguate somehow.
// See the comment in CustomTypeMarshallerAttribute about the bool parameters in the constructors.
[AttributeUsage(AttributeTargets.GenericParameter)]
public sealed class ElementUnmanagedTypeAttribute : Attribute
{
}

[ManagedToUnmanagedMarshallers(
    typeof(TManaged),
    typeof(ManagedToUnmanaged),
    typeof(Bidirectional),
    typeof(UnmanagedToManaged))]
[UnmanagedToManagedMarshallers(
    typeof(TManaged),
    typeof(UnmanagedToManaged),
    typeof(Bidirectional),
    typeof(ManagedToUnmanaged))]
[ElementMarshaller(
    typeof(TManaged),
    typeof(Element))]
public unsafe static class Marshaller // Must be static class
{
    [CustomTypeMarshaller(hasState: true)]
    public unsafe ref struct ManagedToUnmanaged
    {
        public void FromManaged(TManaged managed) => throw null; // Optional caller allocation, Span<T>
        public ref byte GetPinnableReference() => throw null; // Optional, allowed on all "stateful" shapes
        public TUnmanaged ToUnmanaged() => throw null;
        public void Dispose() => throw null; // Should not throw exceptions. Is pattern-matched on a ref struct. See https://sourceroslyn.io/#Microsoft.CodeAnalysis.CSharp/Binder/Binder_Statements.cs,fe4277a539498184 for Roslyn rules we will try to match.
    }

    [CustomTypeMarshaller(hasState: true)]
    public unsafe ref struct Bidirectional
    {
        public void FromManaged(TManaged managed) => throw null; // Optional caller allocation, Span<T>
        public ref byte GetPinnableReference() => throw null; // Optional, allowed on all "stateful" shapes. See https://sourceroslyn.io/#Microsoft.CodeAnalysis.CSharp/Binder/Binder_Statements.cs,9e4a165c20c84c57 for Roslyn rules we will try to match (excluding extension methods as that's too hard to look up).
        public TUnmanaged ToUnmanaged() => throw null; // Should not throw exceptions.
        public void FromUnmanaged(TUnmanaged native) => throw null; // Should not throw exceptions.
        public TManaged ToManaged() => throw null;
        public void Dispose() => throw null; // Should not throw exceptions.
    }

    [CustomTypeMarshaller(hasState: true)]
    public unsafe struct UnmanagedToManaged : IDisposable // IDisposable interface is required on the type to recognize the Dispose method. Following C#'s lead here and matching semantics of using statements.
    {
        public void FromUnmanaged(TUnmanaged native) => throw null;
        public TManaged ToManaged() => throw null; // Should not throw exceptions.
        public void Dispose() => throw null; // Should not throw exceptions.
    }

    [CustomTypeMarshaller(hasState: false)] // Currently only support stateless. May support stateful in the future
    public static class Element
    {
        // Defined by public interface IMarshaller<TManaged, TUnmanaged> where TUnmanaged : unmanaged
        public static TUnmanaged ConvertToUnmanaged(TManaged managed) => throw null;
        public static TManaged ConvertToManaged(TUnmanaged native) => throw null;
        public static void Dispose(TUnmanaged native) => throw null; // Should not throw exceptions.
    }
}

[ManagedToUnmanagedMarshallers(typeof(CustomTypeMarshallerAttribute.GenericPlaceholder[]),
    typeof(ArrayMarshaller<,>.In),
    typeof(ArrayMarshaller<,>),
    typeof(ArrayMarshaller<,>.Out))]
[UnmanagedToManagedMarshallers(typeof(CustomTypeMarshallerAttribute.GenericPlaceholder[]), typeof(ArrayMarshaller<>), typeof(ArrayMarshaller<>), typeof(ArrayMarshaller<>))]
[ElementMarshaller(typeof(CustomTypeMarshallerAttribute.GenericPlaceholder[]), typeof(ArrayMarshaller<>))]
[CustomTypeMarshaller(hasState: false, bufferSize: 0x200)]
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
    public static void Dispose(byte* native) => Marshal.FreeCoTaskMem((IntPtr)native); // We'll pattern match this Dispose method as well since we're already pattern-matching the Dispose method on the ref struct.

    [CustomTypeMarshaller(hasState: true, bufferSize: 20)] // As our buffer is typed to our native element type, we limit the buffer size based on number of elements, not number of bytes.
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
        public void Dispose()
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

    [CustomTypeMarshaller(hasState: true)]
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
        public void Dispose()
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
            _byval_m_.Dispose();
            _in_m_.Dispose();
            _ref_m_.Dispose();
            _out_m_.Dispose();
            _ret_m_.Dispose();
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
            _byval_m_.Dispose();
            _in_m_.Dispose();
            ArrayMarshaller<TManaged, TUnmanaged>.Dispose(_ref_);
            _out_m_.Dispose();
            _ret_m_.Dispose();
        }

        return _ret_value_;

        // P/Invoke declaration
        static byte* __PInvoke(void* v_b, byte* v, byte* o_bv, byte* in_v, byte* r_v, byte** o_v) => throw null;
    }
}
