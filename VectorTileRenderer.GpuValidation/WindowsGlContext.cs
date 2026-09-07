using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WuGing.VectorTileRenderer.GpuValidation;

// A hidden WGL window for this Windows-only experiment, not an application host API.
internal sealed class WindowsGlContext : IDisposable
{
    private nint window;
    private nint device;
    private nint context;
    private readonly int threadId = Environment.CurrentManagedThreadId;

    public string Vendor { get; }
    public string Renderer { get; }
    public string Version { get; }
    public bool IsHardwarePixelFormat { get; }
    public static bool HasCurrentContext => wglGetCurrentContext() != 0;

    public WindowsGlContext()
    {
        if (HasCurrentContext)
        {
            throw new InvalidOperationException("The validation thread already has a GL context.");
        }

        try
        {
            // No WS_VISIBLE: no interactive window is shown.
            window = CreateWindowExW(0, "STATIC", "VectorTileRenderer GPU validation", 0,
                0, 0, 1, 1, 0, 0, 0, 0);
            Check(window != 0, "CreateWindowEx");
            device = GetDC(window);
            Check(device != 0, "GetDC");
            var descriptor = new PixelFormatDescriptor
            {
                Size = (ushort)Marshal.SizeOf<PixelFormatDescriptor>(),
                Version = 1,
                Flags = 0x00000004 | 0x00000020 | 0x00000001, // window, OpenGL, double buffer
                ColorBits = 32,
                AlphaBits = 8,
                DepthBits = 24,
                StencilBits = 8
            };
            var format = ChoosePixelFormat(device, ref descriptor);
            Check(format != 0, "ChoosePixelFormat");
            Check(DescribePixelFormat(device, format, (uint)Marshal.SizeOf<PixelFormatDescriptor>(),
                ref descriptor) != 0, "DescribePixelFormat");
            IsHardwarePixelFormat = (descriptor.Flags & 0x00000040) == 0; // reject generic GDI software
            Check(SetPixelFormat(device, format, ref descriptor), "SetPixelFormat");
            context = wglCreateContext(device);
            Check(context != 0, "wglCreateContext");
            Check(wglMakeCurrent(device, context), "wglMakeCurrent");
            Vendor = ReadString(0x1F00);
            Renderer = ReadString(0x1F01);
            Version = ReadString(0x1F02);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void VerifyCurrent()
    {
        if (Environment.CurrentManagedThreadId != threadId || context == 0 || wglGetCurrentContext() != context)
        {
            throw new InvalidOperationException("GPU render left its owning thread/current GL context.");
        }
    }

    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != threadId)
        {
            throw new InvalidOperationException("Dispose the WGL host on its owning thread.");
        }

        if (context != 0)
        {
            Check(wglMakeCurrent(0, 0), "Unbind WGL context");
            Check(wglDeleteContext(context), "Delete WGL context");
            context = 0;
        }
        if (device != 0)
        {
            Check(ReleaseDC(window, device) != 0, "ReleaseDC");
            device = 0;
        }
        if (window != 0)
        {
            Check(DestroyWindow(window), "DestroyWindow");
            window = 0;
        }
    }

    private static string ReadString(uint name) => Marshal.PtrToStringAnsi(glGetString(name)) ?? "Unavailable";
    private static void Check(bool success, string operation)
    {
        if (!success) throw new Win32Exception(Marshal.GetLastWin32Error(), operation);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PixelFormatDescriptor
    {
        public ushort Size, Version;
        public uint Flags;
        public byte PixelType, ColorBits, RedBits, RedShift, GreenBits, GreenShift, BlueBits, BlueShift;
        public byte AlphaBits, AlphaShift, AccumBits, AccumRedBits, AccumGreenBits, AccumBlueBits, AccumAlphaBits;
        public byte DepthBits, StencilBits, AuxBuffers, LayerType, Reserved;
        public uint LayerMask, VisibleMask, DamageMask;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(uint exStyle, string className, string name, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint GetDC(nint window);
    [DllImport("user32.dll", SetLastError = true)] private static extern int ReleaseDC(nint window, nint device);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyWindow(nint window);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern int ChoosePixelFormat(nint device, ref PixelFormatDescriptor descriptor);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern int DescribePixelFormat(nint device, int format, uint size, ref PixelFormatDescriptor descriptor);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool SetPixelFormat(nint device, int format, ref PixelFormatDescriptor descriptor);
    [DllImport("opengl32.dll", SetLastError = true)] private static extern nint wglCreateContext(nint device);
    [DllImport("opengl32.dll", SetLastError = true)] private static extern bool wglMakeCurrent(nint device, nint context);
    [DllImport("opengl32.dll", SetLastError = true)] private static extern bool wglDeleteContext(nint context);
    [DllImport("opengl32.dll")] private static extern nint wglGetCurrentContext();
    [DllImport("opengl32.dll")] private static extern nint glGetString(uint name);
}
