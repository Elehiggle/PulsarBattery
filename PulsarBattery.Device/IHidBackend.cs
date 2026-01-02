namespace PulsarBattery.Device;

public interface IHidBackend
{
    string Name { get; }

    DeviceBatteryStatus? ReadBatteryStatus(bool debug);
}
