using System;
using System.Runtime.InteropServices;
using RGB.NET.Core;
using ScreenCapture.NET;

namespace Artemis.Plugins.LayerBrushes.AmbilightSmoothed.Services;

/// <summary>
/// HDR-capable screen capture using DirectX 11 Desktop Duplication API with FP16 format support
/// </summary>
public sealed class HdrScreenCapture : IDisposable
{
    private readonly string _displayName;
    private IntPtr _dxgiFactory;
    private IntPtr _d3dDevice;
    private IntPtr _d3dContext;
    private IntPtr _dxgiOutput;
    private IntPtr _dxgiDuplication;
    private IntPtr _stagingTexture;
    
    private bool _isInitialized;
    private bool _isHdr;
    private int _width;
    private int _height;
    private byte[]? _frameBuffer;
    private DXGI_FORMAT _captureFormat;
    
    public bool IsHdr => _isHdr;
    public int Width => _width;
    public int Height => _height;
    
    public HdrScreenCapture(Display display)
    {
        _displayName = display.DeviceName;
    }
    
    public bool Initialize()
    {
        try
        {
            // Create DXGI Factory
            Guid factoryGuid = NativeMethods.IID_IDXGIFactory1;
            int hr = NativeMethods.CreateDXGIFactory1(ref factoryGuid, out _dxgiFactory);
            if (hr != NativeMethods.S_OK)
                return false;
            
            IDXGIFactory1 factory = (IDXGIFactory1)Marshal.GetObjectForIUnknown(_dxgiFactory);
            
            // Enumerate adapters and outputs to find our display
            bool found = false;
            IntPtr outputPtr = IntPtr.Zero;
            IntPtr adapterPtr = IntPtr.Zero;
            
            for (uint adapterIndex = 0; !found; adapterIndex++)
            {
                try
                {
                    factory.EnumAdapters1(adapterIndex, out adapterPtr);
                    IDXGIAdapter adapter = (IDXGIAdapter)Marshal.GetObjectForIUnknown(adapterPtr);
                    
                    for (uint outputIndex = 0; !found; outputIndex++)
                    {
                        try
                        {
                            adapter.EnumOutputs(outputIndex, out outputPtr);
                            IDXGIOutput output = (IDXGIOutput)Marshal.GetObjectForIUnknown(outputPtr);
                            
                            // Check if this is our display
                            // For now, just use the first output on the first adapter
                            // In a full implementation, we'd match by device name
                            _dxgiOutput = outputPtr;
                            found = true;
                            break;
                        }
                        catch
                        {
                            break;
                        }
                    }
                    
                    if (found && outputPtr != IntPtr.Zero)
                    {
                        // Create D3D11 device
                        D3D_FEATURE_LEVEL[] featureLevels = new[]
                        {
                            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_1,
                            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0,
                            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_1,
                            D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_0,
                        };
                        
                        hr = NativeMethods.D3D11CreateDevice(
                            adapterPtr,
                            D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_UNKNOWN,
                            IntPtr.Zero,
                            D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                            featureLevels,
                            (uint)featureLevels.Length,
                            7, // D3D11_SDK_VERSION
                            out _d3dDevice,
                            out D3D_FEATURE_LEVEL featureLevel,
                            out _d3dContext);
                        
                        if (hr != NativeMethods.S_OK)
                            return false;
                        
                        // Query for IDXGIOutput6 to check HDR support
                        IDXGIOutput6 output6 = (IDXGIOutput6)Marshal.GetObjectForIUnknown(outputPtr);
                        
                        output6.GetDesc1(out DXGI_OUTPUT_DESC1 outputDesc);
                        
                        // Check if display is HDR
                        _isHdr = outputDesc.ColorSpace == DXGI_COLOR_SPACE_TYPE.DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020;
                        
                        RECT bounds = outputDesc.DesktopCoordinates;
                        _width = bounds.Right - bounds.Left;
                        _height = bounds.Bottom - bounds.Top;
                        
                        // Try HDR formats if display is HDR
                        if (_isHdr)
                        {
                            DXGI_FORMAT[] hdrFormats = new[]
                            {
                                DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_FLOAT,
                                DXGI_FORMAT.DXGI_FORMAT_R10G10B10A2_UNORM
                            };
                            
                            hr = output6.DuplicateOutput1(_d3dDevice, 0, (uint)hdrFormats.Length, hdrFormats, out _dxgiDuplication);
                            
                            if (hr == NativeMethods.S_OK)
                            {
                                _captureFormat = DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_FLOAT;
                            }
                            else
                            {
                                // Fallback to SDR
                                _isHdr = false;
                            }
                        }
                        
                        // Fallback to standard BGRA format
                        if (!_isHdr)
                        {
                            output6.DuplicateOutput(_d3dDevice, out _dxgiDuplication);
                            _captureFormat = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM;
                        }
                        
                        if (_dxgiDuplication == IntPtr.Zero)
                            return false;
                        
                        // Create staging texture for CPU readback
                        CreateStagingTexture();
                        
                        _frameBuffer = new byte[_width * _height * 4];
                        _isInitialized = true;
                        return true;
                    }
                    
                    Marshal.Release(adapterPtr);
                }
                catch
                {
                    break;
                }
            }
            
            return false;
        }
        catch
        {
            Dispose();
            return false;
        }
    }
    
    private void CreateStagingTexture()
    {
        D3D11_TEXTURE2D_DESC desc = new D3D11_TEXTURE2D_DESC
        {
            Width = (uint)_width,
            Height = (uint)_height,
            MipLevels = 1,
            ArraySize = 1,
            Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, // Always output to BGRA8 for simplicity
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
            Usage = D3D11_USAGE.D3D11_USAGE_STAGING,
            CPUAccessFlags = D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ,
            BindFlags = 0,
            MiscFlags = 0
        };
        
        ID3D11Device device = (ID3D11Device)Marshal.GetObjectForIUnknown(_d3dDevice);
        device.CreateTexture2D(ref desc, IntPtr.Zero, out _stagingTexture);
    }
    
    public bool CaptureFrame(byte[] buffer)
    {
        if (!_isInitialized || _dxgiDuplication == IntPtr.Zero)
            return false;
        
        try
        {
            IDXGIOutputDuplication duplication = (IDXGIOutputDuplication)Marshal.GetObjectForIUnknown(_dxgiDuplication);
            
            // Acquire next frame
            int hr = duplication.AcquireNextFrame(0, out DXGI_OUTDUPL_FRAME_INFO frameInfo, out IntPtr desktopResource);
            
            if (hr == NativeMethods.DXGI_ERROR_WAIT_TIMEOUT)
            {
                // No new frame, return last cached frame
                if (_frameBuffer != null)
                {
                    Array.Copy(_frameBuffer, buffer, Math.Min(buffer.Length, _frameBuffer.Length));
                    return true;
                }
                return false;
            }
            
            if (hr != NativeMethods.S_OK)
                return false;
            
            try
            {
                // Get the texture
                IDXGIResource resource = (IDXGIResource)Marshal.GetObjectForIUnknown(desktopResource);
                ID3D11Texture2D desktopTexture = (ID3D11Texture2D)Marshal.GetObjectForIUnknown(desktopResource);
                
                ID3D11DeviceContext context = (ID3D11DeviceContext)Marshal.GetObjectForIUnknown(_d3dContext);
                
                // If HDR, convert FP16 -> BGRA8 with tone mapping
                if (_isHdr)
                {
                    // For now, simple copy - proper tone mapping would require compute shader
                    // This is a simplified version - a full implementation would use GPU shaders
                    CopyAndConvertHdrFrame(context, desktopResource, buffer);
                }
                else
                {
                    // Standard BGRA8 copy
                    context.CopyResource(_stagingTexture, desktopResource);
                    MapAndCopyFrame(context, buffer);
                }
                
                // Cache the frame
                Array.Copy(buffer, _frameBuffer, Math.Min(buffer.Length, _frameBuffer.Length));
            }
            finally
            {
                Marshal.Release(desktopResource);
                duplication.ReleaseFrame();
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private void CopyAndConvertHdrFrame(ID3D11DeviceContext context, IntPtr hdrTexture, byte[] buffer)
    {
        // Get HDR texture description
        ID3D11Texture2D texture = (ID3D11Texture2D)Marshal.GetObjectForIUnknown(hdrTexture);
        texture.GetDesc(out D3D11_TEXTURE2D_DESC desc);
        
        // Create a staging texture for HDR data if format is FP16
        D3D11_TEXTURE2D_DESC stagingDesc = new D3D11_TEXTURE2D_DESC
        {
            Width = desc.Width,
            Height = desc.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = desc.Format,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
            Usage = D3D11_USAGE.D3D11_USAGE_STAGING,
            CPUAccessFlags = D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ,
            BindFlags = 0,
            MiscFlags = 0
        };
        
        ID3D11Device device = (ID3D11Device)Marshal.GetObjectForIUnknown(_d3dDevice);
        device.CreateTexture2D(ref stagingDesc, IntPtr.Zero, out IntPtr hdrStagingTexture);
        
        try
        {
            // Copy HDR data to staging
            context.CopyResource(hdrStagingTexture, hdrTexture);
            
            // Map and convert
            int hr = context.Map(hdrStagingTexture, 0, D3D11_MAP.D3D11_MAP_READ, 0, out D3D11_MAPPED_SUBRESOURCE mapped);
            
            if (hr == NativeMethods.S_OK)
            {
                try
                {
                    ConvertHdrToSdr(mapped.pData, (int)mapped.RowPitch, buffer);
                }
                finally
                {
                    context.Unmap(hdrStagingTexture, 0);
                }
            }
        }
        finally
        {
            Marshal.Release(hdrStagingTexture);
        }
    }
    
    private unsafe void ConvertHdrToSdr(IntPtr hdrData, int stride, byte[] output)
    {
        // Simple FP16 -> BGRA8 conversion with basic tone mapping
        // This is a simplified version - HyperHDR uses complex LUTs and shaders
        
        if (_captureFormat == DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_FLOAT)
        {
            // FP16 format: 8 bytes per pixel (RGBA, 2 bytes each)
            byte* src = (byte*)hdrData;
            int outputIndex = 0;
            
            for (int y = 0; y < _height; y++)
            {
                ushort* rowPtr = (ushort*)(src + y * stride);
                
                for (int x = 0; x < _width; x++)
                {
                    // Read FP16 values (R, G, B, A)
                    float r = HalfToFloat(rowPtr[x * 4 + 0]);
                    float g = HalfToFloat(rowPtr[x * 4 + 1]);
                    float b = HalfToFloat(rowPtr[x * 4 + 2]);
                    
                    // Simple tone mapping: scale from [0, 1] linear HDR to [0, 255] sRGB
                    // This is a very basic approach - proper tone mapping would use ACES or similar
                    const float exposure = 1.0f;
                    r = Math.Clamp(r * exposure, 0f, 1f);
                    g = Math.Clamp(g * exposure, 0f, 1f);
                    b = Math.Clamp(b * exposure, 0f, 1f);
                    
                    // Apply simple gamma correction (sRGB approximation)
                    r = (float)Math.Pow(r, 1.0 / 2.2);
                    g = (float)Math.Pow(g, 1.0 / 2.2);
                    b = (float)Math.Pow(b, 1.0 / 2.2);
                    
                    // Convert to BGRA8
                    output[outputIndex++] = (byte)(b * 255);
                    output[outputIndex++] = (byte)(g * 255);
                    output[outputIndex++] = (byte)(r * 255);
                    output[outputIndex++] = 255; // Alpha
                }
            }
        }
        else
        {
            // R10G10B10A2 format - simplified for now
            // Full implementation would unpack 10-bit values
            MapAndCopyFrame((ID3D11DeviceContext)Marshal.GetObjectForIUnknown(_d3dContext), output);
        }
    }
    
    private static float HalfToFloat(ushort half)
    {
        // Convert IEEE 754 half-precision to single-precision
        uint sign = (uint)(half >> 15) & 0x1u;
        uint exponent = (uint)(half >> 10) & 0x1Fu;
        uint mantissa = (uint)half & 0x3FFu;
        
        uint result;
        if (exponent == 0)
        {
            if (mantissa == 0)
            {
                // Zero
                result = sign << 31;
            }
            else
            {
                // Denormalized
                exponent = 1;
                while ((mantissa & 0x400) == 0)
                {
                    mantissa <<= 1;
                    exponent--;
                }
                mantissa &= 0x3FF;
                result = (sign << 31) | ((exponent + (127 - 15)) << 23) | (mantissa << 13);
            }
        }
        else if (exponent == 31)
        {
            // Infinity or NaN
            result = (sign << 31) | 0x7F800000u | (mantissa << 13);
        }
        else
        {
            // Normalized
            result = (sign << 31) | ((exponent + (127 - 15)) << 23) | (mantissa << 13);
        }
        
        return BitConverter.Int32BitsToSingle((int)result);
    }
    
    private void MapAndCopyFrame(ID3D11DeviceContext context, byte[] buffer)
    {
        int hr = context.Map(_stagingTexture, 0, D3D11_MAP.D3D11_MAP_READ, 0, out D3D11_MAPPED_SUBRESOURCE mapped);
        
        if (hr == NativeMethods.S_OK)
        {
            try
            {
                // Copy row by row due to potential stride differences
                unsafe
                {
                    byte* src = (byte*)mapped.pData;
                    int outputIndex = 0;
                    int rowBytes = _width * 4;
                    
                    for (int y = 0; y < _height; y++)
                    {
                        Marshal.Copy((IntPtr)(src + y * mapped.RowPitch), buffer, outputIndex, rowBytes);
                        outputIndex += rowBytes;
                    }
                }
            }
            finally
            {
                context.Unmap(_stagingTexture, 0);
            }
        }
    }
    
    public void Dispose()
    {
        if (_dxgiDuplication != IntPtr.Zero)
        {
            Marshal.Release(_dxgiDuplication);
            _dxgiDuplication = IntPtr.Zero;
        }
        
        if (_stagingTexture != IntPtr.Zero)
        {
            Marshal.Release(_stagingTexture);
            _stagingTexture = IntPtr.Zero;
        }
        
        if (_dxgiOutput != IntPtr.Zero)
        {
            Marshal.Release(_dxgiOutput);
            _dxgiOutput = IntPtr.Zero;
        }
        
        if (_d3dContext != IntPtr.Zero)
        {
            Marshal.Release(_d3dContext);
            _d3dContext = IntPtr.Zero;
        }
        
        if (_d3dDevice != IntPtr.Zero)
        {
            Marshal.Release(_d3dDevice);
            _d3dDevice = IntPtr.Zero;
        }
        
        if (_dxgiFactory != IntPtr.Zero)
        {
            Marshal.Release(_dxgiFactory);
            _dxgiFactory = IntPtr.Zero;
        }
        
        _isInitialized = false;
    }
}
