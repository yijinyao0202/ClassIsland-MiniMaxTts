using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.MiniMaxTts.Services;
using ClassIsland.MiniMaxTts.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassIsland.MiniMaxTts;

[PluginEntrance]
public sealed class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        var settings = new MiniMaxTtsSettingsService(Path.Combine(PluginConfigFolder, "settings.json"));
        services.AddSingleton(settings);
        services.AddSingleton<MiniMaxApiClient>();
        services.AddSpeechProvider<MiniMaxSpeechService, MiniMaxSpeechSettingsControl>();
    }
}
