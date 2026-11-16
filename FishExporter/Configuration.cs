using Dalamud.Configuration;

namespace FishExporter;

public enum ExportFormat
{
    Json,
    IdList,
    Csv,
    NameList,
}

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public ExportFormat Format { get; set; } = ExportFormat.Json;
    public bool IncludeUncaught { get; set; } = false;
    public bool IncludeSpearfishing { get; set; } = true;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
