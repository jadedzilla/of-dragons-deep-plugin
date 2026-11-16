using Dalamud.Configuration;

namespace OfDragonsDeep;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public bool IncludeSpearfishing { get; set; } = true;
    public bool ShowFishGuideButton { get; set; } = true;
    public bool IncludeAchievementProgress { get; set; } = true;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
