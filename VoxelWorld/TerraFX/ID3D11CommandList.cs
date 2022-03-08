﻿// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

// Ported from um/d3d11.h in the Windows SDK for Windows 10.0.20348.0
// Original source is Copyright © Microsoft. All rights reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;

namespace TerraFX.Interop.DirectX
{
    [Guid("A24BC4D1-769E-43F7-8013-98FF566C18E2")]
    [NativeTypeName("struct ID3D11CommandList : ID3D11DeviceChild")]
    [NativeInheritance("ID3D11DeviceChild")]
    public unsafe partial struct ID3D11CommandList : ID3D11CommandList.Interface
    {
        public void** lpVtbl;

        [VtblIndex(0)]
        public HRESULT QueryInterface([NativeTypeName("const IID &")] Guid* riid, void** ppvObject)
        {
            return ((delegate* unmanaged[Stdcall]<ID3D11CommandList*, Guid*, void**, int>)(lpVtbl[0]))((ID3D11CommandList*)Unsafe.AsPointer(ref this), riid, ppvObject);
        }

        [VtblIndex(1)]
        [return: NativeTypeName("ULONG")]
        public uint AddRef()
        {
            return ((delegate* unmanaged[Stdcall]<ID3D11CommandList*, uint>)(lpVtbl[1]))((ID3D11CommandList*)Unsafe.AsPointer(ref this));
        }

        [VtblIndex(2)]
        [return: NativeTypeName("ULONG")]
        public uint Release()
        {
            return ((delegate* unmanaged[Stdcall]<ID3D11CommandList*, uint>)(lpVtbl[2]))((ID3D11CommandList*)Unsafe.AsPointer(ref this));
        }

        [VtblIndex(3)]
        public void GetDevice(ID3D11Device** ppDevice)
        {
            ((delegate* unmanaged[Stdcall]<ID3D11CommandList*, ID3D11Device**, void>)(lpVtbl[3]))((ID3D11CommandList*)Unsafe.AsPointer(ref this), ppDevice);
        }

        [VtblIndex(4)]
        public HRESULT GetPrivateData([NativeTypeName("const GUID &")] Guid* guid, uint* pDataSize, void* pData)
        {
            return ((delegate* unmanaged[Stdcall]<ID3D11CommandList*, Guid*, uint*, void*, int>)(lpVtbl[4]))((ID3D11CommandList*)Unsafe.AsPointer(ref this), guid, pDataSize, pData);
        }

        [VtblIndex(5)]
        public HRESULT SetPrivateData([NativeTypeName("const GUID &")] Guid* guid, uint DataSize, [NativeTypeName("const void *")] void* pData)
        {
            return ((delegate* unmanaged[Stdcall]<ID3D11CommandList*, Guid*, uint, void*, int>)(lpVtbl[5]))((ID3D11CommandList*)Unsafe.AsPointer(ref this), guid, DataSize, pData);
        }

        [VtblIndex(6)]
        public HRESULT SetPrivateDataInterface([NativeTypeName("const GUID &")] Guid* guid, [NativeTypeName("const IUnknown *")] IUnknown* pData)
        {
            return ((delegate* unmanaged[Stdcall]<ID3D11CommandList*, Guid*, IUnknown*, int>)(lpVtbl[6]))((ID3D11CommandList*)Unsafe.AsPointer(ref this), guid, pData);
        }

        [VtblIndex(7)]
        public uint GetContextFlags()
        {
            return ((delegate* unmanaged[Stdcall]<ID3D11CommandList*, uint>)(lpVtbl[7]))((ID3D11CommandList*)Unsafe.AsPointer(ref this));
        }

        public interface Interface : ID3D11DeviceChild.Interface
        {
            [VtblIndex(7)]
            uint GetContextFlags();
        }
    }
}
