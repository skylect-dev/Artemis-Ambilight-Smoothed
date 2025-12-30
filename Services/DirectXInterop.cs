using System;
using System.Runtime.InteropServices;

namespace Artemis.Plugins.LayerBrushes.AmbilightSmoothed.Services;

#region DXGI Enums and Structs

internal enum DXGI_FORMAT : uint
{
    DXGI_FORMAT_UNKNOWN = 0,
    DXGI_FORMAT_R16G16B16A16_FLOAT = 10,
    DXGI_FORMAT_R10G10B10A2_UNORM = 24,
    DXGI_FORMAT_B8G8R8A8_UNORM = 87,
}

internal enum DXGI_MODE_ROTATION
{
    DXGI_MODE_ROTATION_UNSPECIFIED = 0,
    DXGI_MODE_ROTATION_IDENTITY = 1,
    DXGI_MODE_ROTATION_ROTATE90 = 2,
    DXGI_MODE_ROTATION_ROTATE180 = 3,
    DXGI_MODE_ROTATION_ROTATE270 = 4
}

public enum DXGI_COLOR_SPACE_TYPE
{
    DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709 = 0,
    DXGI_COLOR_SPACE_RGB_FULL_G10_NONE_P709 = 1,
    DXGI_COLOR_SPACE_RGB_STUDIO_G2084_NONE_P2020 = 11, // HDR10 Studio
    DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020 = 12, // HDR10
    DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_LEFT_P2020 = 13, // HDR10 YCbCr
    DXGI_COLOR_SPACE_RGB_STUDIO_G22_NONE_P2020 = 14, // Display P3 Studio
    DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P2020 = 15, // Display P3
}

internal enum D3D_DRIVER_TYPE
{
    D3D_DRIVER_TYPE_UNKNOWN = 0,
    D3D_DRIVER_TYPE_HARDWARE = 1,
    D3D_DRIVER_TYPE_REFERENCE = 2,
    D3D_DRIVER_TYPE_NULL = 3,
    D3D_DRIVER_TYPE_SOFTWARE = 4,
    D3D_DRIVER_TYPE_WARP = 5
}

internal enum D3D_FEATURE_LEVEL
{
    D3D_FEATURE_LEVEL_11_1 = 0xb100,
    D3D_FEATURE_LEVEL_11_0 = 0xb000,
    D3D_FEATURE_LEVEL_10_1 = 0xa100,
    D3D_FEATURE_LEVEL_10_0 = 0xa000,
}

[Flags]
internal enum D3D11_CREATE_DEVICE_FLAG
{
    D3D11_CREATE_DEVICE_SINGLETHREADED = 0x1,
    D3D11_CREATE_DEVICE_DEBUG = 0x2,
    D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20,
}

internal enum D3D11_USAGE
{
    D3D11_USAGE_DEFAULT = 0,
    D3D11_USAGE_IMMUTABLE = 1,
    D3D11_USAGE_DYNAMIC = 2,
    D3D11_USAGE_STAGING = 3
}

[Flags]
internal enum D3D11_CPU_ACCESS_FLAG
{
    D3D11_CPU_ACCESS_READ = 0x20000,
    D3D11_CPU_ACCESS_WRITE = 0x10000,
}

internal enum D3D11_MAP
{
    D3D11_MAP_READ = 1,
    D3D11_MAP_WRITE = 2,
    D3D11_MAP_READ_WRITE = 3,
    D3D11_MAP_WRITE_DISCARD = 4,
    D3D11_MAP_WRITE_NO_OVERWRITE = 5
}

[StructLayout(LayoutKind.Sequential)]
internal struct DXGI_SAMPLE_DESC
{
    public uint Count;
    public uint Quality;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DXGI_MODE_DESC
{
    public uint Width;
    public uint Height;
    public DXGI_RATIONAL RefreshRate;
    public DXGI_FORMAT Format;
    public uint ScanlineOrdering;
    public uint Scaling;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DXGI_RATIONAL
{
    public uint Numerator;
    public uint Denominator;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DXGI_OUTDUPL_DESC
{
    public DXGI_MODE_DESC ModeDesc;
    public DXGI_MODE_ROTATION Rotation;
    public bool DesktopImageInSystemMemory;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DXGI_OUTDUPL_FRAME_INFO
{
    public long LastPresentTime;
    public long LastMouseUpdateTime;
    public uint AccumulatedFrames;
    public bool RectsCoalesced;
    public bool ProtectedContentMaskedOut;
    public DXGI_OUTDUPL_POINTER_POSITION PointerPosition;
    public uint TotalMetadataBufferSize;
    public uint PointerShapeBufferSize;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DXGI_OUTDUPL_POINTER_POSITION
{
    public POINT Position;
    public bool Visible;
}

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct D3D11_TEXTURE2D_DESC
{
    public uint Width;
    public uint Height;
    public uint MipLevels;
    public uint ArraySize;
    public DXGI_FORMAT Format;
    public DXGI_SAMPLE_DESC SampleDesc;
    public D3D11_USAGE Usage;
    public uint BindFlags;
    public D3D11_CPU_ACCESS_FLAG CPUAccessFlags;
    public uint MiscFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct D3D11_MAPPED_SUBRESOURCE
{
    public IntPtr pData;
    public uint RowPitch;
    public uint DepthPitch;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DXGI_OUTPUT_DESC1
{
    public string DeviceName;
    public RECT DesktopCoordinates;
    public bool AttachedToDesktop;
    public DXGI_MODE_ROTATION Rotation;
    public IntPtr Monitor;
    public uint BitsPerColor;
    public DXGI_COLOR_SPACE_TYPE ColorSpace;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public float[] RedPrimary;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public float[] GreenPrimary;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public float[] BluePrimary;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public float[] WhitePoint;
    public float MinLuminance;
    public float MaxLuminance;
    public float MaxFullFrameLuminance;
}

#endregion

#region COM Interfaces

[ComImport]
[Guid("aec22fb8-76f3-4639-9be0-28eb43a67a2e")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIObject
{
    void SetPrivateData(ref Guid Name, uint DataSize, IntPtr pData);
    void SetPrivateDataInterface(ref Guid Name, IntPtr pUnknown);
    void GetPrivateData(ref Guid Name, ref uint pDataSize, IntPtr pData);
    void GetParent(ref Guid riid, out IntPtr ppParent);
}

[ComImport]
[Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIFactory1
{
    // IDXGIObject methods
    void SetPrivateData(ref Guid Name, uint DataSize, IntPtr pData);
    void SetPrivateDataInterface(ref Guid Name, IntPtr pUnknown);
    void GetPrivateData(ref Guid Name, ref uint pDataSize, IntPtr pData);
    void GetParent(ref Guid riid, out IntPtr ppParent);
    
    // IDXGIFactory methods
    [PreserveSig]
    int EnumAdapters(uint Adapter, out IntPtr ppAdapter);
    void MakeWindowAssociation(IntPtr WindowHandle, uint Flags);
    void GetWindowAssociation(out IntPtr pWindowHandle);
    void CreateSwapChain(IntPtr pDevice, IntPtr pDesc, out IntPtr ppSwapChain);
    void CreateSoftwareAdapter(IntPtr Module, out IntPtr ppAdapter);
    
    // IDXGIFactory1 methods
    [PreserveSig]
    int EnumAdapters1(uint Adapter, out IntPtr ppAdapter);
    [PreserveSig]
    int IsCurrent();
}

[ComImport]
[Guid("2411e7e1-12ac-4ccf-bd14-9798e8534dc0")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIAdapter
{
    // IDXGIObject methods
    void SetPrivateData(ref Guid Name, uint DataSize, IntPtr pData);
    void SetPrivateDataInterface(ref Guid Name, IntPtr pUnknown);
    void GetPrivateData(ref Guid Name, ref uint pDataSize, IntPtr pData);
    void GetParent(ref Guid riid, out IntPtr ppParent);
    
    // IDXGIAdapter methods
    [PreserveSig]
    int EnumOutputs(uint Output, out IntPtr ppOutput);
    void GetDesc(IntPtr pDesc);
    void CheckInterfaceSupport(ref Guid InterfaceName, out long pUMDVersion);
}

[ComImport]
[Guid("ae02eedb-c735-4690-8d52-5a8dc20213aa")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIOutput
{
    // IDXGIObject methods
    void SetPrivateData(ref Guid Name, uint DataSize, IntPtr pData);
    void SetPrivateDataInterface(ref Guid Name, IntPtr pUnknown);
    void GetPrivateData(ref Guid Name, ref uint pDataSize, IntPtr pData);
    void GetParent(ref Guid riid, out IntPtr ppParent);
    
    // IDXGIOutput methods
    void GetDesc(out RECT pDesc);
    void GetDisplayModeList(DXGI_FORMAT EnumFormat, uint Flags, ref uint pNumModes, IntPtr pDesc);
    void FindClosestMatchingMode(IntPtr pModeToMatch, IntPtr pClosestMatch, IntPtr pConcernedDevice);
    void WaitForVBlank();
    void TakeOwnership(IntPtr pDevice, bool Exclusive);
    void ReleaseOwnership();
    void GetGammaControlCapabilities(IntPtr pGammaCaps);
    void SetGammaControl(IntPtr pArray);
    void GetGammaControl(IntPtr pArray);
    void SetDisplaySurface(IntPtr pScanoutSurface);
    void GetDisplaySurfaceData(IntPtr pDestination);
    void GetFrameStatistics(IntPtr pStats);
    [PreserveSig]
    int DuplicateOutput(IntPtr pDevice, out IntPtr ppOutputDuplication);
}

[ComImport]
[Guid("068346e8-aaec-4b84-add7-137f513f77a1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIOutput6
{
    // IDXGIObject methods
    void SetPrivateData(ref Guid Name, uint DataSize, IntPtr pData);
    void SetPrivateDataInterface(ref Guid Name, IntPtr pUnknown);
    void GetPrivateData(ref Guid Name, ref uint pDataSize, IntPtr pData);
    void GetParent(ref Guid riid, out IntPtr ppParent);
    
    // IDXGIOutput methods
    void GetDesc(IntPtr pDesc);
    void GetDisplayModeList(DXGI_FORMAT EnumFormat, uint Flags, ref uint pNumModes, IntPtr pDesc);
    void FindClosestMatchingMode(IntPtr pModeToMatch, IntPtr pClosestMatch, IntPtr pConcernedDevice);
    void WaitForVBlank();
    void TakeOwnership(IntPtr pDevice, bool Exclusive);
    void ReleaseOwnership();
    void GetGammaControlCapabilities(IntPtr pGammaCaps);
    void SetGammaControl(IntPtr pArray);
    void GetGammaControl(IntPtr pArray);
    void SetDisplaySurface(IntPtr pScanoutSurface);
    void GetDisplaySurfaceData(IntPtr pDestination);
    void GetFrameStatistics(IntPtr pStats);
    [PreserveSig]
    int DuplicateOutput(IntPtr pDevice, out IntPtr ppOutputDuplication);
    
    // IDXGIOutput1 methods
    void GetDisplayModeList1(DXGI_FORMAT EnumFormat, uint Flags, ref uint pNumModes, IntPtr pDesc);
    void FindClosestMatchingMode1(IntPtr pModeToMatch, IntPtr pClosestMatch, IntPtr pConcernedDevice);
    void GetDisplaySurfaceData1(IntPtr pDestination);
    [PreserveSig]
    int DuplicateOutput1(
        IntPtr pDevice,
        uint Flags,
        uint SupportedFormatsCount,
        [MarshalAs(UnmanagedType.LPArray)] DXGI_FORMAT[] pSupportedFormats,
        out IntPtr ppOutputDuplication);
    
    // IDXGIOutput2 methods  
    bool SupportsOverlays();
    
    // IDXGIOutput3 methods
    void CheckOverlaySupport(DXGI_FORMAT EnumFormat, IntPtr pConcernedDevice, out uint pFlags);
    
    // IDXGIOutput4 methods
    void CheckOverlayColorSpaceSupport(DXGI_FORMAT Format, DXGI_COLOR_SPACE_TYPE ColorSpace, IntPtr pConcernedDevice, out uint pFlags);
    
    // IDXGIOutput5 methods
    [PreserveSig]
    int DuplicateOutput1_5(
        IntPtr pDevice,
        uint Flags,
        uint SupportedFormatsCount,
        [MarshalAs(UnmanagedType.LPArray)] DXGI_FORMAT[] pSupportedFormats,
        out IntPtr ppOutputDuplication);
    
    // IDXGIOutput6 methods
    void GetDesc1(out DXGI_OUTPUT_DESC1 pDesc);
    void CheckHardwareCompositionSupport(out uint pFlags);
}

[ComImport]
[Guid("191cfac3-a341-470d-b26e-a864f428319c")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIOutputDuplication
{
    void GetDesc(out DXGI_OUTDUPL_DESC pDesc);
    [PreserveSig]
    int AcquireNextFrame(uint TimeoutInMilliseconds, out DXGI_OUTDUPL_FRAME_INFO pFrameInfo, out IntPtr ppDesktopResource);
    void GetFrameDirtyRects(uint DirtyRectsBufferSize, IntPtr pDirtyRectsBuffer, out uint pDirtyRectsBufferSizeRequired);
    void GetFrameMoveRects(uint MoveRectsBufferSize, IntPtr pMoveRectBuffer, out uint pMoveRectsBufferSizeRequired);
    void GetFramePointerShape(uint PointerShapeBufferSize, IntPtr pPointerShapeBuffer, out uint pPointerShapeBufferSizeRequired, out IntPtr pPointerShapeInfo);
    void MapDesktopSurface(out IntPtr pLockedRect);
    void UnMapDesktopSurface();
    void ReleaseFrame();
}

[ComImport]
[Guid("db6f6ddb-ac77-4e88-8253-819df9bbf140")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID3D11Device
{
    void CreateBuffer(IntPtr pDesc, IntPtr pInitialData, out IntPtr ppBuffer);
    void CreateTexture1D(IntPtr pDesc, IntPtr pInitialData, out IntPtr ppTexture1D);
    void CreateTexture2D(ref D3D11_TEXTURE2D_DESC pDesc, IntPtr pInitialData, out IntPtr ppTexture2D);
    // ... other methods omitted for brevity
}

[ComImport]
[Guid("c0bfa96c-e089-44fb-8eaf-26f8796190da")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID3D11DeviceContext
{
    void VSSetConstantBuffers(uint StartSlot, uint NumBuffers, IntPtr ppConstantBuffers);
    void PSSetShaderResources(uint StartSlot, uint NumViews, IntPtr ppShaderResourceViews);
    void PSSetShader(IntPtr pPixelShader, IntPtr ppClassInstances, uint NumClassInstances);
    // ... many more methods
    void CopyResource(IntPtr pDstResource, IntPtr pSrcResource);
    void CopySubresourceRegion(IntPtr pDstResource, uint DstSubresource, uint DstX, uint DstY, uint DstZ, IntPtr pSrcResource, uint SrcSubresource, IntPtr pSrcBox);
    void UpdateSubresource(IntPtr pDstResource, uint DstSubresource, IntPtr pDstBox, IntPtr pSrcData, uint SrcRowPitch, uint SrcDepthPitch);
    // ...
    [PreserveSig]
    int Map(IntPtr pResource, uint Subresource, D3D11_MAP MapType, uint MapFlags, out D3D11_MAPPED_SUBRESOURCE pMappedResource);
    void Unmap(IntPtr pResource, uint Subresource);
}

[ComImport]
[Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID3D11Texture2D
{
    void GetDevice(out IntPtr ppDevice);
    void GetPrivateData(ref Guid guid, ref uint pDataSize, IntPtr pData);
    void SetPrivateData(ref Guid guid, uint DataSize, IntPtr pData);
    void SetPrivateDataInterface(ref Guid guid, IntPtr pData);
    void GetType(out uint pResourceDimension);
    void SetEvictionPriority(uint EvictionPriority);
    void GetEvictionPriority(out uint pEvictionPriority);
    void GetDesc(out D3D11_TEXTURE2D_DESC pDesc);
}

[ComImport]
[Guid("cafcb56c-6ac3-4889-bf47-9e23bbd260ec")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIResource
{
    void GetSharedHandle(out IntPtr pSharedHandle);
    void GetUsage(out uint pUsage);
    void SetEvictionPriority(uint EvictionPriority);
    void GetEvictionPriority(out uint pEvictionPriority);
}

#endregion

#region Native Methods

internal static class NativeMethods
{
    private const string DXGI_DLL = "dxgi.dll";
    private const string D3D11_DLL = "d3d11.dll";

    [DllImport(DXGI_DLL)]
    public static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr ppFactory);

    [DllImport(D3D11_DLL)]
    public static extern int D3D11CreateDevice(
        IntPtr pAdapter,
        D3D_DRIVER_TYPE DriverType,
        IntPtr Software,
        D3D11_CREATE_DEVICE_FLAG Flags,
        [MarshalAs(UnmanagedType.LPArray)] D3D_FEATURE_LEVEL[] pFeatureLevels,
        uint FeatureLevels,
        uint SDKVersion,
        out IntPtr ppDevice,
        out D3D_FEATURE_LEVEL pFeatureLevel,
        out IntPtr ppImmediateContext);

    public static readonly Guid IID_IDXGIFactory1 = new Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369");
    public static readonly int DXGI_ERROR_WAIT_TIMEOUT = unchecked((int)0x887A0027);
    public static readonly int DXGI_ERROR_ACCESS_LOST = unchecked((int)0x887A0026);
    public static readonly int DXGI_ERROR_INVALID_CALL = unchecked((int)0x887A0001);
    public static readonly int S_OK = 0;
}

#endregion
