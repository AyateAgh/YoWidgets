using System.Runtime.InteropServices;
using System.Management;
using System.Text.Json;
using Microsoft.Win32;
namespace YoWidgets;

internal sealed class SystemControls : IDisposable
{
    public double Volume
    {
        get; private set;
    }
    public bool Muted
    {
        get; private set;
    }
    public double? Brightness
    {
        get; private set;
    }
    public string Error { get; private set; } = ""; public event Action? Changed; readonly SemaphoreSlim brightnessLock = new(1); IAudioEndpointVolume? endpoint; IMMDevice? device; IMMDeviceEnumerator? enumerator; string deviceId = "";
    public async Task RefreshAsync()
    {
        try
        {
            ReleaseAudio();
            enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new("BCDE0395-E52F-467C-8E3D-C4579291692E"))!)!;
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0, 1, out device));
            device.GetId(out deviceId);
            var iid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref iid, 23, IntPtr.Zero, out var obj);
            endpoint = (IAudioEndpointVolume)obj;
            endpoint.GetMasterVolumeLevelScalar(out var v);
            endpoint.GetMute(out var mute);
            Volume = v * 100;
            Muted = mute;
            Error = "";
        }
        catch { Error = "Аудиоустройство недоступно"; }
        try
        {
            Brightness = await Task.Run(() => { using var s = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness WHERE Active=True"); using var items = s.Get(); foreach (ManagementObject item in items) return (double?)Convert.ToDouble(item["CurrentBrightness"]); return null; });
        }
        catch { Brightness = null; }
        Changed?.Invoke();
    }
    public void SetVolume(double value)
    {
        try
        {
            if (endpoint == null)
                return;
            endpoint.GetMasterVolumeLevelScalar(out var before);
            var old = before * 100;
            var next = Math.Clamp(value, 0, 100);
            Record("volume", deviceId, old, next, "prepared");
            var id = Guid.Empty;
            Marshal.ThrowExceptionForHR(endpoint.SetMasterVolumeLevelScalar((float)(next / 100), ref id));
            Record("volume", deviceId, old, next, "applied");
            Volume = next;
            Changed?.Invoke();
        }
        catch (Exception e) { Error = "Не удалось изменить громкость"; App.Log("volume", e); }
    }
    public void SetMute()
    {
        try
        {
            if (endpoint == null)
                return;
            endpoint.GetMute(out var beforeMute);
            Muted = beforeMute;
            Record("mute", deviceId, Muted ? 1 : 0, Muted ? 0 : 1, "prepared");
            var id = Guid.Empty;
            Marshal.ThrowExceptionForHR(endpoint.SetMute(!Muted, ref id));
            Record("mute", deviceId, Muted ? 1 : 0, Muted ? 0 : 1, "applied");
            Muted = !Muted;
            Changed?.Invoke();
        }
        catch (Exception e) { Error = "Не удалось изменить mute"; App.Log("mute", e); }
    }
    public async Task SetBrightnessAsync(double value)
    {
        await brightnessLock.WaitAsync();
        try
        {
            var next = (byte)Math.Clamp(value, 1, 100);
            await Task.Run(() => { using var statesSearch = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness WHERE Active=True"); using var states = statesSearch.Get(); var originals = new Dictionary<string, double>(); foreach (ManagementObject state in states) { var name = state["InstanceName"]?.ToString(); if (name != null) originals[name] = Convert.ToDouble(state["CurrentBrightness"]); } using var search = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=True"); using var items = search.Get(); var writes = 0; foreach (ManagementObject item in items) { var deviceName = item["InstanceName"]?.ToString() ?? ""; if (!originals.TryGetValue(deviceName, out var before)) throw new InvalidOperationException("Cannot read matching display brightness before mutation"); Record("brightness", deviceName, before, next, "prepared"); var result = item.InvokeMethod("WmiSetBrightness", new object[] { 0u, next }); if (Convert.ToUInt32(result) != 0) throw new InvalidOperationException("WMI brightness rejected"); Record("brightness", deviceName, before, next, "applied"); writes++; } if (writes == 0) throw new InvalidOperationException("No brightness provider"); });
            Brightness = next;
            Changed?.Invoke();
        }
        catch (Exception e) { Error = "Яркость не поддерживается этим дисплеем"; App.Log("brightness", e); }
        finally { brightnessLock.Release(); }
    }
    static readonly object journalLock = new();
    static void Record(string kind, string device, double original, double value, string stage = "applied")
    {
        lock (journalLock)
        {
            var path = Path.Combine(App.DataRoot, "system-changes.json");
            Dictionary<string, SystemChange> records = [];
            if (File.Exists(path))
                try
                {
                    records = JsonSerializer.Deserialize<Dictionary<string, SystemChange>>(File.ReadAllText(path)) ?? [];
                }
                catch { }
            var key = kind + "|" + device;
            records.TryGetValue(key, out var old);
            records[key] = ChangeJournal.Advance(old, kind, device, original, value, stage);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(records));
            File.Move(path + ".tmp", path, true);
        }
    }
    public async Task<string> RestoreAsync()
    {
        var file = Path.Combine(App.DataRoot, "system-changes.json");
        if (!File.Exists(file))
            return "";
        await RefreshAsync();
        var issues = new List<string>();
        Dictionary<string, SystemChange> records;
        try
        {
            records = JsonSerializer.Deserialize<Dictionary<string, SystemChange>>(File.ReadAllText(file)) ?? [];
        }
        catch { return "Журнал изменений повреждён. Автоматический откат остановлен."; }
        foreach (var pair in records.ToArray())
        {
            var change = pair.Value;
            if (change.Stage != "applied")
            {
                issues.Add("Незавершённое изменение " + change.Kind + ": автоматический откат остановлен");
                continue;
            }
            try
            {
                if (change.Kind == "volume" || change.Kind == "mute")
                {
                    if (endpoint == null || change.Device != deviceId)
                    {
                        issues.Add("Аудиоустройство изменилось");
                        continue;
                    }
                    var ctx = Guid.Empty;
                    if (change.Kind == "volume")
                    {
                        endpoint.GetMasterVolumeLevelScalar(out var actual);
                        if (!RollbackPolicy.CanRestore(change.Device, deviceId, actual * 100, change.Last))
                        {
                            issues.Add("Громкость уже изменена вне YoWidgets");
                            continue;
                        }
                        Marshal.ThrowExceptionForHR(endpoint.SetMasterVolumeLevelScalar((float)(change.Original / 100), ref ctx));
                    }
                    else
                    {
                        endpoint.GetMute(out var actual);
                        if ((actual ? 1 : 0) != change.Last)
                        {
                            issues.Add("Mute уже изменён вне YoWidgets");
                            continue;
                        }
                        Marshal.ThrowExceptionForHR(endpoint.SetMute(change.Original > 0, ref ctx));
                    }
                }
                else if (change.Kind == "brightness")
                {
                    var restored = await Task.Run(() => { using var search = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness WHERE Active=True"); using var items = search.Get(); foreach (ManagementObject item in items) { if (item["InstanceName"]?.ToString() != change.Device) continue; if (!RollbackPolicy.CanRestore(change.Device, item["InstanceName"]?.ToString() ?? "", Convert.ToDouble(item["CurrentBrightness"]), change.Last)) return false; using var methods = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=True"); using var methodItems = methods.Get(); foreach (ManagementObject method in methodItems) if (method["InstanceName"]?.ToString() == change.Device) return Convert.ToUInt32(method.InvokeMethod("WmiSetBrightness", new object[] { 0u, (byte)Math.Clamp(change.Original, 1, 100) })) == 0; } return false; });
                    if (!restored)
                    {
                        issues.Add("Яркость изменилась или дисплей недоступен");
                        continue;
                    }
                }
                else
                {
                    issues.Add("Неизвестный тип изменения");
                    continue;
                }
                records.Remove(pair.Key);
            }
            catch { issues.Add("Не удалось восстановить " + change.Kind); }
        }
        File.WriteAllText(file, JsonSerializer.Serialize(records));
        return string.Join("; ", issues.Distinct());
    }
    void ReleaseAudio()
    {
        if (endpoint != null)
            Marshal.ReleaseComObject(endpoint);
        if (device != null)
            Marshal.ReleaseComObject(device);
        if (enumerator != null)
            Marshal.ReleaseComObject(enumerator);
        endpoint = null;
        device = null;
        enumerator = null;
    }
    public void Dispose()
    {
        ReleaseAudio();
    }
}
[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(int flow, int mask, out IntPtr devices); [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
}
[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, int context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object obj); [PreserveSig] int OpenPropertyStore(int access, out IntPtr store); [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
}
[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig] int RegisterControlChangeNotify(IntPtr n); [PreserveSig] int UnregisterControlChangeNotify(IntPtr n); [PreserveSig] int GetChannelCount(out uint count); [PreserveSig] int SetMasterVolumeLevel(float v, ref Guid ctx); [PreserveSig] int SetMasterVolumeLevelScalar(float v, ref Guid ctx); [PreserveSig] int GetMasterVolumeLevel(out float v); [PreserveSig] int GetMasterVolumeLevelScalar(out float v); [PreserveSig] int SetChannelVolumeLevel(uint ch, float v, ref Guid ctx); [PreserveSig] int SetChannelVolumeLevelScalar(uint ch, float v, ref Guid ctx); [PreserveSig] int GetChannelVolumeLevel(uint ch, out float v); [PreserveSig] int GetChannelVolumeLevelScalar(uint ch, out float v); [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx); [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}
internal static class StartupRegistration
{
    const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    static string Command => "\"" + Environment.ProcessPath + "\"";
    public static bool Enabled
    {
        get
        {
            using var k = Registry.CurrentUser.OpenSubKey(Run);
            return string.Equals(k?.GetValue("YoWidgets") as string, Command, StringComparison.Ordinal);
        }
    }
    public static void Set(bool enabled)
    {
        using var k = Registry.CurrentUser.CreateSubKey(Run);
        var old = k.GetValue("YoWidgets") as string;
        if (enabled)
        {
            if (old != null && old != Command)
                throw new InvalidOperationException("Автозапуск YoWidgets уже принадлежит другой установке");
            k.SetValue("YoWidgets", Command);
        }
        else if (old == Command)
            k.DeleteValue("YoWidgets", false);
    }
}
