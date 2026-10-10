using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using System.Drawing;

namespace ToolSlice;

// One instance per PIP session. WGC supplies GPU frames; only the selected area is read back.
public sealed class GraphicsCaptureService : IDisposable
{
    private static readonly Guid CaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid TextureGuid = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
    private readonly RectangleF _crop;
    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _context;
    private readonly IDirect3DDevice _winrtDevice;
    private readonly GraphicsCaptureItem _item;
    private Direct3D11CaptureFramePool _pool;
    private GraphicsCaptureSession _session;
    private SizeInt32 _poolSize;
    private ID3D11Texture2D? _staging;
    private int _stagingWidth, _stagingHeight;
    private readonly SemaphoreSlim _readback = new(1, 1);
    private long _lastFrameTicks;
    private bool _disposed;

    public event Action<byte[], int, int>? FrameReady;
    public event Action<string>? CaptureFailed;
    public event Action? SourceClosed;

    public GraphicsCaptureService(IntPtr hwnd, RectangleF? crop = null)
    {
        _crop = crop ?? new RectangleF(0, 0, 1, 1);
        if (!GraphicsCaptureSession.IsSupported()) throw new NotSupportedException("이 Windows에서는 Graphics Capture를 사용할 수 없습니다.");
        _device = D3D11.D3D11CreateDevice(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
        _context = _device.ImmediateContext;
        using var dxgi = _device.QueryInterface<IDXGIDevice>();
        Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi.NativePointer, out var ptr));
        try { _winrtDevice = MarshalInterface<IDirect3DDevice>.FromAbi(ptr); }
        finally { Marshal.Release(ptr); }
        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        var itemPtr = interop.CreateForWindow(hwnd, CaptureItemGuid);
        try { _item = GraphicsCaptureItem.FromAbi(itemPtr); }
        finally { Marshal.Release(itemPtr); }
        _poolSize = _item.Size;
        _pool = Direct3D11CaptureFramePool.CreateFreeThreaded(_winrtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _poolSize);
        _session = _pool.CreateCaptureSession(_item);
        _item.Closed += (_, _) => SourceClosed?.Invoke();
        _pool.FrameArrived += OnFrameArrived;
        _session.StartCapture();
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        if (_disposed || !_readback.Wait(0)) return;
        try
        {
            SizeInt32 frameSize;
            using (var frame = sender.TryGetNextFrame())
            {
                if (frame is null) return;
                var now = Environment.TickCount64;
                if (now - Interlocked.Read(ref _lastFrameTicks) < 66) return;
                Interlocked.Exchange(ref _lastFrameTicks, now);
                frameSize = frame.ContentSize;
                var region = Crop(frameSize.Width, frameSize.Height);
                if (region.Width <= 0 || region.Height <= 0) return;
                var access = frame.Surface.As<IDirect3DDxgiInterfaceAccess>();
                IntPtr texturePtr;
                try { texturePtr = access.GetInterface(TextureGuid); }
                finally { Marshal.ReleaseComObject(access); }
                using var texture = new ID3D11Texture2D(texturePtr);
                EnsureStaging(region.Width, region.Height);
                var box = new Box(region.Left, region.Top, 0, region.Right, region.Bottom, 1);
                _context.CopySubresourceRegion(_staging!, 0, 0, 0, 0, texture, 0, box);
                var mapped = _context.Map(_staging!, 0, MapMode.Read);
                var bytes = new byte[checked(region.Width * region.Height * 4)];
                try
                {
                    var rowBytes = region.Width * 4;
                    for (var row = 0; row < region.Height; row++)
                        Marshal.Copy(mapped.DataPointer + row * (int)mapped.RowPitch, bytes, row * rowBytes, rowBytes);
                }
                finally { _context.Unmap(_staging!, 0); }
                FrameReady?.Invoke(bytes, region.Width, region.Height);
            }
            if (frameSize.Width != _poolSize.Width || frameSize.Height != _poolSize.Height)
            {
                sender.Recreate(_winrtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, frameSize);
                _poolSize = frameSize;
            }
        }
        catch (Exception ex) when (!_disposed) { CaptureFailed?.Invoke(ex.Message); }
        finally { _readback.Release(); }
    }

    private Rectangle Crop(int width, int height)
    {
        var left = Math.Clamp((int)Math.Round(_crop.Left * width), 0, Math.Max(0, width - 1));
        var top = Math.Clamp((int)Math.Round(_crop.Top * height), 0, Math.Max(0, height - 1));
        var right = Math.Clamp((int)Math.Round(_crop.Right * width), left + 1, width);
        var bottom = Math.Clamp((int)Math.Round(_crop.Bottom * height), top + 1, height);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    private void EnsureStaging(int width, int height)
    {
        if (_staging is not null && _stagingWidth == width && _stagingHeight == height) return;
        _staging?.Dispose();
        _staging = _device.CreateTexture2D(new Texture2DDescription(
            Format.B8G8R8A8_UNorm, (uint)width, (uint)height,
            mipLevels: 1, bindFlags: BindFlags.None,
            usage: ResourceUsage.Staging, cpuAccessFlags: CpuAccessFlags.Read));
        _stagingWidth = width;
        _stagingHeight = height;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pool.FrameArrived -= OnFrameArrived;
        _session.Dispose();
        _pool.Dispose();
        _staging?.Dispose();
        _winrtDevice.Dispose();
        _context.Dispose();
        _device.Dispose();
    }

    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow(IntPtr window, in Guid iid);
        IntPtr CreateForMonitor(IntPtr monitor, in Guid iid);
    }

    [ComImport, Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        IntPtr GetInterface(in Guid iid);
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);
}
