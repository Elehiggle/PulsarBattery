using System;

namespace PulsarBattery.Models;

public sealed record BatteryReading(DateTimeOffset Timestamp, int Percentage, bool IsCharging, string Model);