using System.Runtime.InteropServices;
var source = File.ReadAllBytes(args[0]);
var hr = Compiler.D3DCompile(source, (nuint)source.Length, args[0], IntPtr.Zero, IntPtr.Zero, "main", "ps_3_0", 1 << 15, 0, out var code, out var errors);
if (hr < 0) { if (errors != null) Console.Error.WriteLine(Marshal.PtrToStringAnsi(errors.GetBufferPointer(), (int)errors.GetBufferSize())); Marshal.ThrowExceptionForHR(hr); }
var result = new byte[(int)code.GetBufferSize()];
Marshal.Copy(code.GetBufferPointer(), result, 0, result.Length);
File.WriteAllBytes(args[1], result);
Console.WriteLine($"Compiled PS 3.0: {result.Length} bytes");
static class Compiler
{
    [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi)] public static extern int D3DCompile(byte[] source, nuint length, string name, IntPtr defines, IntPtr include, string entry, string target, uint flags, uint effectFlags, out Blob code, out Blob errors);
}
[ComImport, Guid("8BA5FB08-5195-40E2-AC58-0D989C3A0102"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface Blob
{
    [PreserveSig] IntPtr GetBufferPointer(); [PreserveSig] nuint GetBufferSize();
}
