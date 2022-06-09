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
[ManagedToUnmanagedMarshallers(typeof(SafeHandleMarshaller<>.In), typeof(SafeHandleMarshaller<>.Ref), typeof(SafeHandleMarshaller<>.Out))]
public static class SafeHandleMarshaller<T> where T : SafeHandle
{
    [CustomTypeMarshaller(typeof(CustomTypeMarshallerAttribute.GenericPlaceholder), CustomTypeMarshallerKind.Stateful, UnmanagedResources = true)]
    public struct In
    {
        private bool _addRefd;
        private T _handle;
        public void InitializeFromManagedValue(T handle)
        {
            _handle = handle;
            handle.DangerousAddRef(ref _addRefd);
        }

        public IntPtr ToNativeValue() => _handle.DangerousGetHandle();

        public void FreeNative()
        {
            if (_addRefd)
            {
                _handle.DangerousRelease();
            }
        }
    }

    [CustomTypeMarshaller(typeof(CustomTypeMarshallerAttribute.GenericPlaceholder), CustomTypeMarshallerKind.Stateful)]
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
            _newHandle = (T)Activator.CreateInstance(typeof(T), nonPublic: true);
        }

        public void InitializeFromManagedValue(T handle)
        {
            _handle = handle;
            handle.DangerousAddRef(ref _addRefd);
            _originalHandleValue = handle.DangerousGetHandle();
        }

        public IntPtr ToNativeValue() => _originalHandleValue;

        public void InitializeFromNativeValue(IntPtr value)
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

        public T ToManaged() => _handleToReturn;

        public void FreeNative()
        {
            if (_addRefd)
            {
                _handle.DangerousRelease();
            }
        }
    }

    [CustomTypeMarshaller(typeof(CustomTypeMarshallerAttribute.GenericPlaceholder), CustomTypeMarshallerKind.Stateful)]
    public struct Out
    {
        private T _newHandle;
        public Out()
        {
            _newHandle = (T)Activator.CreateInstance(typeof(T), nonPublic: true);
        }

        public void InitializeFromNativeValue(IntPtr value)
        {
            Marshal.InitHandle(_newHandle, value);
        }

        public T ToManaged() => _newHandle;
    }
}

// [NativeMarshalling(typeof(StructWithSafeHandleFieldMarshaller))]
struct StructWithSafeHandleField
{
    public SafeFileHandle handle;
}

// P/Invoke Out and Reverse P/Invoke unsupported.
// Element marshalling unsupported until we add support for stateful element marshallers
[ManagedToUnmanagedMarshallers(typeof(In), typeof(Ref), null)]
static class StructWithSafeHandleFieldMarshaller
{
    [CustomTypeMarshaller(typeof(StructWithSafeHandleField), CustomTypeMarshallerKind.Stateful, UnmanagedResources = true)]
    public struct In
    {
        public struct Native
        {
            public IntPtr handle;
        }
        private bool _addRefd;
        private SafeFileHandle _handle;

        public void InitializeFromManagedValue(StructWithSafeHandleField managed)
        {
            _handle = managed.handle;
            _handle.DangerousAddRef(ref _addRefd);
        }

        public Native ToNativeValue()
        {
            return new Native { handle = _handle.DangerousGetHandle() };
        }

        public void FreeNative()
        {
            if (_addRefd)
            {
                _handle.DangerousRelease();
            }
        }
    }

    [CustomTypeMarshaller(typeof(StructWithSafeHandleField), CustomTypeMarshallerKind.Stateful, UnmanagedResources = true)]
    public struct Ref
    {
        public struct Native
        {
            public IntPtr handle;
        }
        private bool _addRefd;
        private SafeFileHandle _handle;

        public void InitializeFromManagedValue(StructWithSafeHandleField managed)
        {
            _handle = managed.handle;
            _handle.DangerousAddRef(ref _addRefd);
        }

        public Native ToNativeValue()
        {
            return new Native { handle = _handle.DangerousGetHandle() };
        }

        public void InitializeFromNativeValue(Native native)
        {
            if (_handle.DangerousGetHandle() != native.handle)
            {
                throw new InvalidOperationException();
            }
        }

        public void FreeNative()
        {
            if (_addRefd)
            {
                _handle.DangerousRelease();
            }
        }
    }
}

[ManagedToUnmanagedMarshallers(typeof(HStringMarshaller.In), typeof(HStringMarshaller), typeof(HStringMarshaller))]
[UnmanagedToManagedMarshallers(typeof(HStringMarshaller), typeof(HStringMarshaller), typeof(HStringMarshaller))]
[ElementMarshaller(typeof(HStringMarshaller))]
[CustomTypeMarshaller(typeof(string), CustomTypeMarshallerKind.Stateless)]
static class HStringMarshaller
{
    public static unsafe IntPtr ConvertToNativeValue(string str)
    {
        void* hstring;
        fixed (char* ptr = str)
        {
            Marshal.ThrowExceptionForHR(Platform.WindowsCreateString(ptr, (uint)str.Length, &hstring));
        }
        return (IntPtr)hstring;
    }

    public static unsafe string ConvertToManagedValue(IntPtr hstring)
    {
        int length;
        char* rawBuffer = Platform.WindowsGetStringRawBuffer((void*)hstring, &length);
        return new string(rawBuffer, 0, length);
    }

    public static unsafe void FreeNativeValue(IntPtr hstring)
    {
        Marshal.ThrowExceptionForHR(Platform.WindowsDeleteString((void*)hstring));
    }

    [CustomTypeMarshaller(typeof(string), CustomTypeMarshallerKind.Stateful)]
    public unsafe struct In
    {
        private Platform.HSTRING_HEADER header;
        private void* hstring;
        public unsafe void InitializeFromManagedValue(string s)
        {
            fixed (char* ptr = s)
            fixed (Platform.HSTRING_HEADER* headerPtr = &header)
            fixed (void** hstringPtr = &hstring)
            {
                Marshal.ThrowExceptionForHR(Platform.WindowsCreateStringReference(ptr, (uint)s.Length, headerPtr, hstringPtr));
            }
        }

        public IntPtr ToNativeValue() => (IntPtr)hstring;
    }
}