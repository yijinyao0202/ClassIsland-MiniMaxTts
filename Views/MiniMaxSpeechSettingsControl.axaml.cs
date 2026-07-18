using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.MiniMaxTts.Services;
using ClassIsland.Shared;

namespace ClassIsland.MiniMaxTts.Views;

public partial class MiniMaxSpeechSettingsControl : SpeechProviderControlBase
{
    private readonly MiniMaxTtsSettingsService _settings;
    private readonly MiniMaxApiClient _apiClient;

    public MiniMaxSpeechSettingsControl() : this(
        IAppHost.GetService<MiniMaxTtsSettingsService>(),
        IAppHost.GetService<MiniMaxApiClient>())
    {
    }

    public MiniMaxSpeechSettingsControl(MiniMaxTtsSettingsService settings, MiniMaxApiClient apiClient)
    {
        _settings = settings;
        _apiClient = apiClient;
        InitializeComponent();
        DataContext = settings;
    }

    private async void RefreshVoices_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_settings.IsBusy)
        {
            return;
        }
        try
        {
            _settings.IsBusy = true;
            _settings.StatusMessage = "正在连接 MiniMax 并读取音色…";
            var voices = await _apiClient.GetVoicesAsync(CancellationToken.None);
            _settings.UpdateAvailableVoices(voices);
            _settings.StatusMessage = voices.Count == 0
                ? "连接成功，但当前账号没有返回可用音色。"
                : $"连接成功，共读取到 {voices.Count} 个音色。";
        }
        catch (Exception exception)
        {
            _settings.StatusMessage = $"读取音色失败：{exception.Message}";
        }
        finally
        {
            _settings.IsBusy = false;
        }
    }

    private void SaveCustomVoice_OnClick(object? sender, RoutedEventArgs e)
    {
        _settings.StatusMessage = _settings.SaveCustomVoice()
            ? $"已保存自定义音色：{_settings.VoiceId}"
            : "请先填写自定义音色 ID。";
    }

    private void RemoveCustomVoice_OnClick(object? sender, RoutedEventArgs e)
    {
        _settings.StatusMessage = _settings.RemoveCustomVoice()
            ? "已删除当前自定义音色。"
            : "当前音色不是已保存的自定义音色。";
    }

    private void SaveCustomModel_OnClick(object? sender, RoutedEventArgs e)
    {
        _settings.StatusMessage = _settings.SaveCustomModel()
            ? $"已保存或选用模型：{_settings.Model}"
            : "请先填写模型 ID。";
    }

    private void RemoveCustomModel_OnClick(object? sender, RoutedEventArgs e)
    {
        _settings.StatusMessage = _settings.RemoveCustomModel()
            ? "已从自定义模型列表中删除当前模型。"
            : "当前模型是内置模型或尚未保存。";
    }
}
