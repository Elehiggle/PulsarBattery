namespace PulsarBattery.Device;

public sealed record DeviceBatteryStatus(int Percentage, bool IsCharging, string Model);
