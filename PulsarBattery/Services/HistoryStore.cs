using PulsarBattery.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PulsarBattery.Services;

internal sealed class HistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public HistoryStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(GetAppDataDir(), "history.json");
    }

    public async Task<IReadOnlyList<BatteryReading>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                return Array.Empty<BatteryReading>();
            }

            await using var stream = File.OpenRead(_filePath);
            var data = await JsonSerializer.DeserializeAsync<List<BatteryReading>>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return data ?? new List<BatteryReading>();
        }
        catch
        {
            return Array.Empty<BatteryReading>();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(IReadOnlyCollection<BatteryReading> readings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            var tmp = _filePath + ".tmp";
            await using (var stream = File.Create(tmp))
            {
                await JsonSerializer.SerializeAsync(stream, readings, JsonOptions, cancellationToken).ConfigureAwait(false);
            }

            File.Copy(tmp, _filePath, overwrite: true);
            File.Delete(tmp);
        }
        catch
        {
            // best-effort persistence
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string GetAppDataDir()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PulsarBattery");

        return dir;
    }
}