using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace YoWidgets;
internal static class Native {
 [StructLayout(LayoutKind.Sequential)] public struct RECT{public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out RECT r);
 [DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindow(string? cls,string? name);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string? cls,string? name);
 public delegate bool EnumProc(IntPtr hwnd,IntPtr p);
 [DllImport("user32.dll")]public static extern bool EnumWindows(EnumProc proc,IntPtr p);
 [DllImport("user32.dll",SetLastError=true)]public static extern IntPtr SetParent(IntPtr h,IntPtr parent);
 [DllImport("user32.dll")]public static extern IntPtr GetParent(IntPtr h);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]public static extern IntPtr GetStyle(IntPtr h,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)]public static extern IntPtr SetStyle(IntPtr h,int index,IntPtr v);
 [DllImport("user32.dll")]public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")]public static extern IntPtr GetWindowDpiAwarenessContext(IntPtr h);
 [DllImport("user32.dll")]public static extern bool AreDpiAwarenessContextsEqual(IntPtr first,IntPtr second);
 [DllImport("dwmapi.dll")]public static extern int DwmSetWindowAttribute(IntPtr h,int attr,ref int value,int size);
 public static IntPtr Handle(Window w)=>new WindowInteropHelper(w).Handle;
 public static RectD Bounds(Window w){GetWindowRect(Handle(w),out var r);return new(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);}
 public static void ToolWindow(Window w){var h=Handle(w);var ex=GetStyle(h,-20).ToInt64();SetStyle(h,-20,new((ex|0x80)&~0x40000));}
}
internal sealed class DesktopHost {
 public bool Attached{get;private set;} public string Status{get;private set;}="Обычное окно";
 private IntPtr window;private long oldStyle;
 public bool TryAttach(Window w){window=Native.Handle(w);IntPtr parent=IntPtr.Zero;
  Native.EnumWindows((h,p)=>{if(Native.FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero)parent=h;return true;},IntPtr.Zero);
  if(parent==IntPtr.Zero){Status="Слой рабочего стола недоступен";return false;}
  if(!Native.AreDpiAwarenessContextsEqual(Native.GetWindowDpiAwarenessContext(window),Native.GetWindowDpiAwarenessContext(parent))){Status="Настольный режим недоступен: разные DPI-контексты";App.Trace("desktop",Status);return false;}
  // Only change our window. Never create/restart/patch Explorer or use private desktop COM.
  oldStyle=Native.GetStyle(window,-16).ToInt64();
  Native.SetStyle(window,-16,new((oldStyle|0x40000000L)&~unchecked((long)0x80000000)));
  Native.SetParent(window,parent);
  Attached=Native.GetParent(window)==parent;
  if(!Attached)Native.SetStyle(window,-16,new(oldStyle));
  Status=Attached?"На рабочем столе":"Привязка не поддерживается";App.Trace("desktop",Status);return Attached;
 }
 public void Detach(){if(!Attached)return;Native.SetParent(window,IntPtr.Zero);Native.SetStyle(window,-16,new(oldStyle));Attached=false;}
}
