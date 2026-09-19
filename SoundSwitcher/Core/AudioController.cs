using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using static SoundSwitcher.Core.AudioNative;

namespace SoundSwitcher.Core;

public class AudioController : IMMNotificationClient, IDisposable
{
    private IMMDeviceEnumerator? _enumerator;
    private bool _isDisposed;

    public event Action? DevicesChanged;
    public event Action<string>? DefaultDeviceChanged;

    public AudioController()
    {
        try
        {
            _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            _enumerator.RegisterEndpointNotificationCallback(this);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AudioController init error: {ex.Message}");
        }
    }

    public List<AudioDevice> GetPlaybackDevices()
    {
        var list = new List<AudioDevice>();
        if (_enumerator == null) return list;

        try
        {
            string? defaultId = GetDefaultPlaybackDeviceId();

            int hr = _enumerator.EnumAudioEndpoints(EDataFlow.eRender, DEVICE_STATE_ACTIVE, out var collection);
            if (hr != 0 || collection == null) return list;

            collection.GetCount(out uint count);
            for (uint i = 0; i < count; i++)
            {
                collection.Item(i, out var device);
                if (device == null) continue;

                device.GetId(out string id);

                string friendlyName = "Unknown Device";
                string deviceDesc = "";
                DeviceFormFactor formFactor = DeviceFormFactor.Speakers;

                if (device.OpenPropertyStore(STGM_READ, out var store) == 0 && store != null)
                {
                    var pkeyName = PKEY_Device_FriendlyName;
                    if (store.GetValue(ref pkeyName, out var pvName) == 0 && pvName.data.pwszVal != IntPtr.Zero)
                    {
                        friendlyName = Marshal.PtrToStringUni(pvName.data.pwszVal) ?? friendlyName;
                        pvName.Clear();
                    }

                    var pkeyDesc = PKEY_Device_DeviceDesc;
                    if (store.GetValue(ref pkeyDesc, out var pvDesc) == 0 && pvDesc.data.pwszVal != IntPtr.Zero)
                    {
                        deviceDesc = Marshal.PtrToStringUni(pvDesc.data.pwszVal) ?? "";
                        pvDesc.Clear();
                    }

                    var pkeyForm = PKEY_AudioEndpoint_FormFactor;
                    if (store.GetValue(ref pkeyForm, out var pvForm) == 0)
                    {
                        formFactor = (DeviceFormFactor)pvForm.data.uintVal;
                        pvForm.Clear();
                    }

                    Marshal.ReleaseComObject(store);
                }

                float volume = 0f;
                bool isMuted = false;
                try
                {
                    var iid = IID_IAudioEndpointVolume;
                    if (device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out var iface) == 0 && iface is IAudioEndpointVolume epVol)
                    {
                        epVol.GetMasterVolumeLevelScalar(out float volScalar);
                        epVol.GetMute(out isMuted);
                        volume = Math.Clamp(volScalar * 100f, 0f, 100f);
                        Marshal.ReleaseComObject(epVol);
                    }
                }
                catch { }

                Marshal.ReleaseComObject(device);

                list.Add(new AudioDevice
                {
                    Id = id,
                    Name = CleanDeviceName(friendlyName),
                    Description = deviceDesc,
                    FormFactor = formFactor,
                    IsDefault = !string.IsNullOrEmpty(defaultId) && id == defaultId,
                    Volume = volume,
                    IsMuted = isMuted
                });
            }

            Marshal.ReleaseComObject(collection);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting devices: {ex.Message}");
        }

        return list;
    }

    public string? GetDefaultPlaybackDeviceId()
    {
        if (_enumerator == null) return null;
        try
        {
            int hr = _enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var endpoint);
            if (hr == 0 && endpoint != null)
            {
                endpoint.GetId(out string id);
                Marshal.ReleaseComObject(endpoint);
                return id;
            }
        }
        catch { }
        return null;
    }

    public AudioDevice? GetDefaultPlaybackDevice()
    {
        var devices = GetPlaybackDevices();
        return devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
    }

    public bool SetDefaultPlaybackDevice(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return false;

        try
        {
            var policy = (IPolicyConfig)new CPolicyConfigClient();
            policy.SetDefaultEndpoint(deviceId, ERole.eConsole);
            policy.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
            policy.SetDefaultEndpoint(deviceId, ERole.eCommunications);
            Marshal.ReleaseComObject(policy);
            
            DefaultDeviceChanged?.Invoke(deviceId);
            DevicesChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set default device: {ex.Message}");
            return false;
        }
    }

    public bool SetDeviceVolume(string deviceId, float volumePercent)
    {
        if (_enumerator == null) return false;
        try
        {
            if (_enumerator.GetDevice(deviceId, out var device) == 0 && device != null)
            {
                var iid = IID_IAudioEndpointVolume;
                if (device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out var iface) == 0 && iface is IAudioEndpointVolume epVol)
                {
                    float scalar = Math.Clamp(volumePercent / 100f, 0f, 1f);
                    var ctx = Guid.Empty;
                    epVol.SetMasterVolumeLevelScalar(scalar, ref ctx);
                    Marshal.ReleaseComObject(epVol);
                    Marshal.ReleaseComObject(device);
                    return true;
                }
                Marshal.ReleaseComObject(device);
            }
        }
        catch { }
        return false;
    }

    public bool SetDeviceMute(string deviceId, bool isMuted)
    {
        if (_enumerator == null) return false;
        try
        {
            if (_enumerator.GetDevice(deviceId, out var device) == 0 && device != null)
            {
                var iid = IID_IAudioEndpointVolume;
                if (device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out var iface) == 0 && iface is IAudioEndpointVolume epVol)
                {
                    var ctx = Guid.Empty;
                    epVol.SetMute(isMuted, ref ctx);
                    Marshal.ReleaseComObject(epVol);
                    Marshal.ReleaseComObject(device);
                    return true;
                }
                Marshal.ReleaseComObject(device);
            }
        }
        catch { }
        return false;
    }

    public bool ToggleBetween(string? deviceAId, string? deviceBId)
    {
        var devices = GetPlaybackDevices();
        if (devices.Count == 0) return false;

        var currentDefault = GetDefaultPlaybackDevice();

        // If current is Device B and Device A is available, switch to Device A
        if (currentDefault != null && !string.IsNullOrEmpty(deviceBId) && currentDefault.Id == deviceBId && !string.IsNullOrEmpty(deviceAId))
        {
            return SetDefaultPlaybackDevice(deviceAId);
        }

        // If current is Device A and Device B is available, switch to Device B
        if (currentDefault != null && !string.IsNullOrEmpty(deviceAId) && currentDefault.Id == deviceAId && !string.IsNullOrEmpty(deviceBId))
        {
            return SetDefaultPlaybackDevice(deviceBId);
        }

        // If neither, switch to Device A if available, else Device B
        if (!string.IsNullOrEmpty(deviceAId) && devices.Any(d => d.Id == deviceAId))
        {
            return SetDefaultPlaybackDevice(deviceAId);
        }

        if (!string.IsNullOrEmpty(deviceBId) && devices.Any(d => d.Id == deviceBId))
        {
            return SetDefaultPlaybackDevice(deviceBId);
        }

        return ToggleNextDevice();
    }

    public bool ToggleNextDevice()
    {
        var devices = GetPlaybackDevices();
        if (devices.Count < 2) return false;

        int currentIndex = devices.FindIndex(d => d.IsDefault);
        int nextIndex = (currentIndex + 1) % devices.Count;
        if (currentIndex == -1) nextIndex = 0;

        return SetDefaultPlaybackDevice(devices[nextIndex].Id);
    }

    private static string CleanDeviceName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Audio Device";
        return raw.Trim();
    }

    #region IMMNotificationClient implementation
    public void OnDeviceStateChanged(string pwstrDeviceId, uint dwNewState)
    {
        DevicesChanged?.Invoke();
    }

    public void OnDeviceAdded(string pwstrDeviceId)
    {
        DevicesChanged?.Invoke();
    }

    public void OnDeviceRemoved(string pwstrDeviceId)
    {
        DevicesChanged?.Invoke();
    }

    public void OnDefaultDeviceChanged(EDataFlow flow, ERole role, string pwstrDefaultDeviceId)
    {
        if (flow == EDataFlow.eRender && (role == ERole.eMultimedia || role == ERole.eConsole))
        {
            DefaultDeviceChanged?.Invoke(pwstrDefaultDeviceId);
            DevicesChanged?.Invoke();
        }
    }

    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
        DevicesChanged?.Invoke();
    }
    #endregion

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_enumerator != null)
        {
            try
            {
                _enumerator.UnregisterEndpointNotificationCallback(this);
                Marshal.ReleaseComObject(_enumerator);
            }
            catch { }
            _enumerator = null;
        }
    }
}
