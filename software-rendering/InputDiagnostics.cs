using System.Runtime.InteropServices;
using System.Text;

namespace SoftwareRendering;

internal static class InputDiagnostics
{
    public static void Run() {
        if (!OperatingSystem.IsWindows()) {
            Console.WriteLine("DirectInput diagnostics require Windows.");
            return;
        }

        Console.WriteLine($"Process: {Environment.ProcessPath}");
        Console.WriteLine($"Working directory: {Environment.CurrentDirectory}");
        Console.WriteLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"Apartment: {Thread.CurrentThread.GetApartmentState()}");
        var iid = new Guid("BF798031-483A-4DA2-AA99-5D64ED369700");
        var result = DirectInput8Create(GetModuleHandleW(null), 0x0800, ref iid,
            out var input, IntPtr.Zero);
        Console.WriteLine($"DirectInput8Create HRESULT: 0x{result:X8}");
        if (result < 0) {
            Console.WriteLine(Marshal.GetExceptionForHR(result)?.Message);
        }
        var path = new StringBuilder(32768);
        GetModuleFileNameW(GetModuleHandleW("dinput8.dll"), path, path.Capacity);
        Console.WriteLine($"DirectInput DLL: {path}");
        if (input != IntPtr.Zero) {
            Marshal.Release(input);
        }
    }

    [DllImport("dinput8.dll", ExactSpelling = true)]
    private static extern int DirectInput8Create(IntPtr instance, uint version,
        ref Guid iid, out IntPtr input, IntPtr outer);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr GetModuleHandleW(string? moduleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint GetModuleFileNameW(IntPtr module, StringBuilder path, int size);
}
