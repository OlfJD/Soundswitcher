using System;

namespace SoundSwitcher.Core;

public enum DeviceFormFactor
{
    Speakers = 1,
    LineLevel = 2,
    Headphones = 3,
    Microphone = 4,
    Headset = 5,
    Handset = 6,
    UnknownDigitalPassthrough = 7,
    SPDIF = 8,
    DigitalAudioDisplayDevice = 9,
    Unknown = 10
}

public class AudioDevice : IEquatable<AudioDevice>
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public DeviceFormFactor FormFactor { get; set; } = DeviceFormFactor.Speakers;
    public bool IsDefault { get; set; }
    public float Volume { get; set; }
    public bool IsMuted { get; set; }

    public string DisplayIcon => FormFactor switch
    {
        DeviceFormFactor.Headphones => "🎧",
        DeviceFormFactor.Headset => "🎧",
        DeviceFormFactor.DigitalAudioDisplayDevice => "🖥️",
        DeviceFormFactor.SPDIF => "🎛️",
        _ => "🔊"
    };

    public string IconKind => FormFactor switch
    {
        DeviceFormFactor.Headphones => "Headphones",
        DeviceFormFactor.Headset => "Headset",
        DeviceFormFactor.DigitalAudioDisplayDevice => "Monitor",
        _ => "Speaker"
    };

    public override string ToString() => Name;

    public bool Equals(AudioDevice? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => Equals(obj as AudioDevice);
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Id);
}
