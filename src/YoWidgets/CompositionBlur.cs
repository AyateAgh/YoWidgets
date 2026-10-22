using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Graphics.Effects;
using Windows.Foundation;
using WinRT;
using WinRT.Interop;
namespace YoWidgets;

// A tiny adapter for the documented Direct2D effect interop. No WinUI runtime,
// native helper DLL, image buffer or screen texture is required.
internal sealed class CompositionBlur(float amount, IGraphicsEffectSource source) : IGraphicsEffect, IGraphicsEffectSource, IBlurInterop
{
    public string Name { get; set; } = "SoftBlur";
    internal float Amount { get; } = amount;
    internal IGraphicsEffectSource Source { get; } = source;
}

[Guid("2FC57384-A068-44D7-A331-30982FCF7177")]
[WindowsRuntimeType]
[WindowsRuntimeHelperType(typeof(IBlurInterop))]
internal unsafe interface IBlurInterop
{
    [Guid("2FC57384-A068-44D7-A331-30982FCF7177")]
    public struct Vftbl
    {
        public IUnknownVftbl Unknown;
        public delegate* unmanaged[Stdcall]<IntPtr, Guid*, int> EffectId;
        public delegate* unmanaged[Stdcall]<IntPtr, char*, uint*, uint*, int> Mapping;
        public delegate* unmanaged[Stdcall]<IntPtr, uint*, int> PropertyCount;
        public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int> Property;
        public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int> Source;
        public delegate* unmanaged[Stdcall]<IntPtr, uint*, int> SourceCount;
        public static readonly IntPtr AbiToProjectionVftablePtr = Create();
        static IntPtr Create()
        {
            var table = (Vftbl*)ComWrappersSupport.AllocateVtableMemory(typeof(Vftbl), sizeof(Vftbl));
            *table = new Vftbl { Unknown = IUnknownVftbl.AbiToProjectionVftbl, EffectId = &Id, Mapping = &Map, PropertyCount = &Properties, Property = &GetProperty, Source = &GetSource, SourceCount = &Sources };
            return (IntPtr)table;
        }
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        static int Id(IntPtr self, Guid* value) { *value = new("1FEB6D69-2FE6-4AC9-8C58-1D7F93E7A6A5"); return 0; }
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        static int Map(IntPtr self, char* name, uint* index, uint* mapping)
        {
            *index = 0; *mapping = 1; // GRAPHICS_EFFECT_PROPERTY_MAPPING_DIRECT (0 is UNKNOWN).
            return Marshal.PtrToStringUni((IntPtr)name) == "BlurAmount" ? 0 : unchecked((int)0x80070057);
        }
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        static int Properties(IntPtr self, uint* count) { *count = 3; return 0; }
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        static int Sources(IntPtr self, uint* count) { *count = 1; return 0; }
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        static int GetSource(IntPtr self, uint index, IntPtr* value)
        {
            *value = IntPtr.Zero;
            if (index != 0) return unchecked((int)0x80070057);
            try { *value = MarshalInterface<IGraphicsEffectSource>.FromManaged(ComWrappersSupport.FindObject<CompositionBlur>(self).Source); return 0; }
            catch (Exception e) { return Marshal.GetHRForException(e); }
        }
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        static int GetProperty(IntPtr self, uint index, IntPtr* value)
        {
            *value = IntPtr.Zero;
            if (index > 2) return unchecked((int)0x80070057);
            try
            {
                object property = index == 0 ? PropertyValue.CreateSingle(ComWrappersSupport.FindObject<CompositionBlur>(self).Amount) : PropertyValue.CreateUInt32(1u);
                var raw = MarshalInspectable<object>.FromManaged(property);
                try { var iid = new Guid("4BD682DD-7554-40E9-9A9B-82654EDE7E62"); return Marshal.QueryInterface(raw, in iid, out *value); }
                finally { Marshal.Release(raw); }
            }
            catch (Exception e) { return Marshal.GetHRForException(e); }
        }
    }
}
