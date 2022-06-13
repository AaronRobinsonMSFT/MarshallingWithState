using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ShapeIdeasReversePInvoke;

// Reverse P/Invoke unsupported.
// Element marshalling is unsupported until we can write stateful element marshallers
[ManagedToUnmanagedMarshallers(typeof(ManagedToUnmanagedMarshallersAttribute.GenericPlaceholder),
    InMarshaller = typeof(SafeHandleMarshaller<>.In),
    RefMarshaller = typeof(SafeHandleMarshaller<>.Ref),
    OutMarshaller = typeof(SafeHandleMarshaller<>.Out))]
public static class SafeHandleMarshaller<T> where T : SafeHandle, new() // Require SafeHandles to be a concrete type and have a public parameterless constructor.
{
    public struct In
    {
        private bool _addRefd;
        private T _handle;
        public void FromManaged(T handle)
        {
            _handle = handle;
            handle.DangerousAddRef(ref _addRefd);
        }

        public IntPtr ToUnmanaged() => _handle.DangerousGetHandle();

        public void Free()
        {
            if (_addRefd)
            {
                _handle.DangerousRelease();
            }
        }
    }

    public struct Ref
    {
        private bool _addRefd;
        private T _handle;
        private IntPtr _originalHandleValue;
        private T _newHandle;
        private T _handleToReturn;

        public Ref()
        {
            _addRefd = false;
            _newHandle = new T();
        }

        public void FromManaged(T handle)
        {
            _handle = handle;
            handle.DangerousAddRef(ref _addRefd);
            _originalHandleValue = handle.DangerousGetHandle();
        }

        public IntPtr ToUnmanaged() => _originalHandleValue;

        public void FromUnmanaged(IntPtr value)
        {
            if (value == _originalHandleValue)
            {
                _handleToReturn = _handle;
            }
            else
            {
                Marshal.InitHandle(_newHandle, value);
                _handleToReturn = _newHandle;
            }
        }

        // ToManagedGuaranteed requests the generator to move the unmarshalling method calls to be called as part of the GuaranteedUnmarshalling stage.
        // The generator will pattern-match this method as an alternative to ToManaged().
        public T ToManagedGuaranteed() => _handleToReturn;

        public void Free()
        {
            if (_addRefd)
            {
                _handle.DangerousRelease();
            }
        }
    }

    public struct Out
    {
        private T _newHandle;
        public Out()
        {
            _newHandle = new T();
        }

        public void FromUnmanaged(IntPtr value)
        {
            Marshal.InitHandle(_newHandle, value);
        }

        public T ToManaged() => _newHandle;
    }
}

// [UnmanagedMarshalling(typeof(StructWithSafeHandleFieldMarshaller))]
struct StructWithSafeHandleField
{
    public SafeFileHandle handle;
}

// P/Invoke Out and Reverse P/Invoke unsupported.
// Element marshalling unsupported until we add support for stateful element marshallers
[ManagedToUnmanagedMarshallers(typeof(StructWithSafeHandleField),
    InMarshaller = typeof(In),
    RefMarshaller = typeof(Ref))]
static class StructWithSafeHandleFieldMarshaller
{
    public struct In
    {
        public struct Unmanaged
        {
            public IntPtr handle;
        }
        private bool _addRefd;
        private SafeFileHandle _handle;

        public void FromManaged(StructWithSafeHandleField managed)
        {
            _handle = managed.handle;
            _handle.DangerousAddRef(ref _addRefd);
        }

        public Unmanaged ToUnmanaged()
        {
            return new Unmanaged { handle = _handle.DangerousGetHandle() };
        }

        public void Free()
        {
            if (_addRefd)
            {
                _handle.DangerousRelease();
            }
        }
    }

    public struct Ref
    {
        public struct Unmanaged
        {
            public IntPtr handle;
        }
        private bool _addRefd;
        private SafeFileHandle _handle;
        private IntPtr _nativeHandleValue;

        public void FromManaged(StructWithSafeHandleField managed)
        {
            _handle = managed.handle;
            _handle.DangerousAddRef(ref _addRefd);
        }

        public Unmanaged ToUnmanaged()
        {
            return new Unmanaged { handle = _handle.DangerousGetHandle() };
        }

        public void FromUnmanaged(Unmanaged native)
        {
            _nativeHandleValue = native.handle;
        }

        public StructWithSafeHandleField ToManaged()
        {
            if (_handle.DangerousGetHandle() != _nativeHandleValue)
            {
                throw new InvalidOperationException();
            }

            return new StructWithSafeHandleField { handle = _handle };
        }

        public void Free()
        {
            if (_addRefd)
            {
                _handle.DangerousRelease();
            }
        }
    }
}

[ManagedToUnmanagedMarshallers(typeof(string), InMarshaller = typeof(HStringMarshaller.In), RefMarshaller = typeof(HStringMarshaller), OutMarshaller = typeof(HStringMarshaller))]
[UnmanagedToManagedMarshallers(typeof(string), InMarshaller = typeof(HStringMarshaller), RefMarshaller = typeof(HStringMarshaller), OutMarshaller = typeof(HStringMarshaller))]
[ElementMarshaller(typeof(string), typeof(HStringMarshaller))]
static class HStringMarshaller
{
    public static unsafe IntPtr ConvertToUnmanaged(string str)
    {
        void* hstring;
        fixed (char* ptr = str)
        {
            Marshal.ThrowExceptionForHR(Platform.WindowsCreateString(ptr, (uint)str.Length, &hstring));
        }
        return (IntPtr)hstring;
    }

    public static unsafe string ConvertToManaged(IntPtr hstring)
    {
        int length;
        char* rawBuffer = Platform.WindowsGetStringRawBuffer((void*)hstring, &length);
        return new string(rawBuffer, 0, length);
    }

    public static unsafe void Free(IntPtr hstring)
    {
        // We can ignore the HResult here as it's documented to always return S_OK.
        // This ensures that we follow the guidance to not throw from FreeUnmanaged
        _ = Platform.WindowsDeleteString((void*)hstring);
    }

    public unsafe ref struct In // Marked as a ref struct
    {
        private string _str;
        private Platform.HSTRING_HEADER header;
        public unsafe void FromManaged(string s)
        {
            _str = s;
        }

        public ref readonly char GetPinnableReference() => ref _str.GetPinnableReference();

        public IntPtr ToUnmanaged()
        {
            void* hstring;
            fixed (char* ptr = _str)
            fixed (Platform.HSTRING_HEADER* headerPtr = &header)
            {
                Marshal.ThrowExceptionForHR(Platform.WindowsCreateStringReference(ptr, (uint)_str.Length, headerPtr, &hstring));
            }
            return (IntPtr)hstring;
        }
    }
}

// Marshals a System.Delegate-derived type to and from native code, keeping the managed delegate instance alive across the call.
[ManagedToUnmanagedMarshallers(typeof(ManagedToUnmanagedMarshallersAttribute.GenericPlaceholder), InMarshaller = typeof(DelegateMarshaller<>.KeepAlive), RefMarshaller = typeof(DelegateMarshaller<>.KeepAlive), OutMarshaller = typeof(DelegateMarshaller<>))]
[UnmanagedToManagedMarshallers(typeof(UnmanagedToManagedMarshallersAttribute.GenericPlaceholder), InMarshaller = typeof(DelegateMarshaller<>), RefMarshaller = typeof(DelegateMarshaller<>), OutMarshaller = typeof(DelegateMarshaller<>))]
[ElementMarshaller(typeof(ElementMarshallerAttribute.GenericPlaceholder), typeof(DelegateMarshaller<>))]
static class DelegateMarshaller<T> where T : Delegate
{
    public static IntPtr ConvertToUnmanaged(T del) => Marshal.GetFunctionPointerForDelegate(del);

    public static T ConvertToManaged(IntPtr ptr) => Marshal.GetDelegateForFunctionPointer<T>(ptr);

    public struct KeepAlive
    {
        private T _del;
        private IntPtr _nativeDelegate;
        public void FromManaged(T managed)
        {
            _del = managed;
            _nativeDelegate = Marshal.GetFunctionPointerForDelegate(_del);
        }

        public IntPtr ToUnmanaged() => _nativeDelegate;

        // We will pattern-match this method and use it when available.
        // Instead of providing a new method hook that really only has one usage scenario,
        // we could instead recognize an overload of ToUnmanaged with the following signature (TOther can be any non-byref-like type): TUnmanaged ToUnmanaged(out TOther keepAlive);
        // When this overload is provided, the generator will recognize the overload and keep alive the out parameter value across the native call.
        public void NotifyInvokeSucceeded()
        {
            GC.KeepAlive(_del);
        }

        public void FromUnmanaged(IntPtr value)
        {
            _nativeDelegate = value;
        }

        public T ToManaged() => Marshal.GetDelegateForFunctionPointer<T>(_nativeDelegate);
    }
}