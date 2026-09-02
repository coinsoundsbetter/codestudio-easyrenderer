using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

internal sealed class Win32TriangleApp : IDisposable
{
    private const string WindowClassName = "RenderLabD3D11Window";
    private const uint WindowStyleOverlapped = 0x00CF0000;
    private const uint WindowMessageDestroy = 0x0002;
    private const uint WindowMessageSize = 0x0005;
    private const uint WindowMessageClose = 0x0010;
    private const uint WindowMessageQuit = 0x0012;
    private const uint PeekMessageRemove = 0x0001;
    private const int ShowNormal = 1;

    private static Win32TriangleApp? s_current;
    private readonly WindowProcedure _windowProcedure;
    private IntPtr _window;
    private ushort _classAtom;
    private bool _shouldQuit;
    private bool _resizePending;

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGISwapChain? _swapChain;
    private ID3D11RenderTargetView? _renderTargetView;
    private ID3D11VertexShader? _vertexShader;
    private ID3D11PixelShader? _pixelShader;
    private ID3D11InputLayout? _inputLayout;
    private ID3D11Buffer? _vertexBuffer;

    public Win32TriangleApp() => _windowProcedure = HandleWindowMessage;

    public void Run()
    {
        s_current = this;
        CreateWindow();
        InitializeGraphics();
        ShowWindow(_window, ShowNormal);
        UpdateWindow(_window);

        while (!_shouldQuit)
        {
            while (PeekMessage(out Message message, IntPtr.Zero, 0, 0, PeekMessageRemove))
            {
                if (message.Value == WindowMessageQuit)
                {
                    _shouldQuit = true;
                    break;
                }

                TranslateMessage(in message);
                DispatchMessage(in message);
            }

            if (!_shouldQuit)
            {
                Render();
            }
        }
    }

    public void Dispose()
    {
        _context?.ClearState();
        _vertexBuffer?.Dispose();
        _inputLayout?.Dispose();
        _pixelShader?.Dispose();
        _vertexShader?.Dispose();
        _renderTargetView?.Dispose();
        _swapChain?.Dispose();
        _context?.Dispose();
        _device?.Dispose();

        if (_window != IntPtr.Zero)
        {
            DestroyWindow(_window);
            _window = IntPtr.Zero;
        }

        if (_classAtom != 0)
        {
            UnregisterClass(WindowClassName, GetModuleHandle(null));
            _classAtom = 0;
        }

        if (ReferenceEquals(s_current, this))
        {
            s_current = null;
        }
    }

    private void CreateWindow()
    {
        IntPtr instance = GetModuleHandle(null);
        WindowClassEx windowClass = new()
        {
            Size = (uint)Marshal.SizeOf<WindowClassEx>(),
            WindowProcedure = _windowProcedure,
            Instance = instance,
            ClassName = WindowClassName
        };

        _classAtom = RegisterClassEx(in windowClass);
        if (_classAtom == 0)
        {
            throw new InvalidOperationException($"RegisterClassEx failed with Win32 error {Marshal.GetLastWin32Error()}.");
        }

        Rectangle rectangle = new() { Right = 960, Bottom = 540 };
        AdjustWindowRectEx(ref rectangle, WindowStyleOverlapped, false, 0);
        _window = CreateWindowEx(0, WindowClassName, "Render Lab · D3D11 Hello Triangle", WindowStyleOverlapped,
            int.MinValue, int.MinValue, rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top,
            IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);

        if (_window == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CreateWindowEx failed with Win32 error {Marshal.GetLastWin32Error()}.");
        }
    }

    private void InitializeGraphics()
    {
        GetClientRect(_window, out Rectangle clientRectangle);
        SwapChainDescription description = new()
        {
            BufferCount = 2,
            BufferDescription = new ModeDescription((uint)clientRectangle.Right, (uint)clientRectangle.Bottom, new Rational(60, 1), Format.R8G8B8A8_UNorm),
            BufferUsage = Usage.RenderTargetOutput,
            OutputWindow = _window,
            SampleDescription = new SampleDescription(1, 0),
            Windowed = true,
            SwapEffect = SwapEffect.Discard
        };

        D3D11.D3D11CreateDeviceAndSwapChain(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport,
            [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0], description,
            out _swapChain, out _device, out _, out _context).CheckError();

        CreateRenderTarget();
        CreatePipeline();
    }

    private void CreateRenderTarget()
    {
        using ID3D11Texture2D backBuffer = _swapChain!.GetBuffer<ID3D11Texture2D>(0);
        _renderTargetView = _device!.CreateRenderTargetView(backBuffer);
    }

    private void CreatePipeline()
    {
        byte[] vertexBytecode = CompileShader("VSMain", "vs_5_0");
        byte[] pixelBytecode = CompileShader("PSMain", "ps_5_0");
        _vertexShader = _device!.CreateVertexShader(vertexBytecode);
        _pixelShader = _device.CreatePixelShader(pixelBytecode);
        _inputLayout = _device.CreateInputLayout(
        [
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 12, 0)
        ], vertexBytecode);

        Vertex[] vertices =
        [
            new(new Vector3(0.0f, 0.65f, 0.0f), new Vector4(1.0f, 0.2f, 0.2f, 1.0f)),
            new(new Vector3(0.65f, -0.65f, 0.0f), new Vector4(0.2f, 1.0f, 0.2f, 1.0f)),
            new(new Vector3(-0.65f, -0.65f, 0.0f), new Vector4(0.2f, 0.4f, 1.0f, 1.0f))
        ];
        _vertexBuffer = _device.CreateBuffer(vertices, BindFlags.VertexBuffer, ResourceUsage.Default,
            CpuAccessFlags.None, ResourceOptionFlags.None, 0, 0);
    }

    private static byte[] CompileShader(string entryPoint, string profile)
    {
        string sourcePath = Path.Combine(AppContext.BaseDirectory, "Shaders", "Triangle.hlsl");
        return Compiler.Compile(File.ReadAllText(sourcePath), entryPoint, sourcePath, profile,
            ShaderFlags.EnableStrictness, EffectFlags.None).ToArray();
    }

    private void Render()
    {
        if (_resizePending)
        {
            ResizeBackBuffer();
            _resizePending = false;
        }

        if (_context is null || _swapChain is null || _renderTargetView is null || !GetClientRect(_window, out Rectangle rectangle))
        {
            return;
        }

        uint width = (uint)(rectangle.Right - rectangle.Left);
        uint height = (uint)(rectangle.Bottom - rectangle.Top);
        if (width == 0 || height == 0)
        {
            return;
        }

        _context.RSSetViewport(new Viewport(0, 0, width, height));
        _context.OMSetRenderTargets(_renderTargetView, null);
        _context.ClearRenderTargetView(_renderTargetView, new Color4(0.05f, 0.07f, 0.12f, 1.0f));
        _context.IASetInputLayout(_inputLayout);
        _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _context.IASetVertexBuffers(0, [_vertexBuffer!], [(uint)Marshal.SizeOf<Vertex>()], [0u]);
        _context.VSSetShader(_vertexShader);
        _context.PSSetShader(_pixelShader);
        _context.Draw(3, 0);
        _swapChain.Present(1, PresentFlags.None).CheckError();
    }

    private void ResizeBackBuffer()
    {
        if (_context is null || _swapChain is null || _renderTargetView is null || !GetClientRect(_window, out Rectangle rectangle) || rectangle.Right == 0 || rectangle.Bottom == 0)
        {
            return;
        }

        _context.OMSetRenderTargets(Array.Empty<ID3D11RenderTargetView>(), null);
        _renderTargetView.Dispose();
        _renderTargetView = null;
        _swapChain.ResizeBuffers(0, 0, 0, Format.Unknown, SwapChainFlags.None).CheckError();
        CreateRenderTarget();
    }

    private static IntPtr HandleWindowMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case WindowMessageSize:
                if (s_current is not null) s_current._resizePending = true;
                return IntPtr.Zero;
            case WindowMessageClose:
                DestroyWindow(window);
                return IntPtr.Zero;
            case WindowMessageDestroy:
                PostQuitMessage(0);
                return IntPtr.Zero;
            default:
                return DefWindowProc(window, message, wParam, lParam);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClassEx
    {
        public uint Size;
        public uint Style;
        public WindowProcedure WindowProcedure;
        public int ClassExtraBytes;
        public int WindowExtraBytes;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr BackgroundBrush;
        [MarshalAs(UnmanagedType.LPWStr)] public string? MenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string ClassName;
        public IntPtr SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr Window;
        public uint Value;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Point Point;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Vertex(Vector3 Position, Vector4 Color);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(in WindowClassEx windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool UnregisterClass(string className, IntPtr instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, IntPtr window, uint minimumFilter, uint maximumFilter, uint removeMessage);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(in Message message);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(in Message message);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int exitCode);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool AdjustWindowRectEx(ref Rectangle rectangle, uint style, bool hasMenu, uint extendedStyle);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetClientRect(IntPtr window, out Rectangle rectangle);
}
