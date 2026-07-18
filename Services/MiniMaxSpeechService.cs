using System.Collections.Concurrent;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Services.SpeechService;
using ClassIsland.Core.Attributes;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace ClassIsland.MiniMaxTts.Services;

[SpeechProviderInfo("cn.classisland.minimax-tts", "MiniMax Speech TTS")]
public sealed class MiniMaxSpeechService(
    IAudioService audioService,
    MiniMaxApiClient apiClient,
    MiniMaxTtsSettingsService settings,
    ILogger<MiniMaxSpeechService> logger) : ISpeechService
{
    private static readonly string CacheFolderPath = Path.Combine(CommonDirectories.AppCacheFolderPath, "MiniMaxTTS");
    private readonly ConcurrentQueue<SpeechQueueItem> _queue = new();
    private readonly SemaphoreSlim _processingGate = new(1, 1);
    private readonly object _currentLock = new();
    private readonly MiniMaxPhraseCacheStore _phraseCacheStore = new(CacheFolderPath, logger);
    private SpeechQueueItem? _currentItem;

    public void EnqueueSpeechQueue(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var item = new SpeechQueueItem(text, new CancellationTokenSource());
        _queue.Enqueue(item);
        _ = ProcessQueueAsync();
    }

    public void ClearSpeechQueue()
    {
        lock (_currentLock)
        {
            _currentItem?.CancellationTokenSource.Cancel();
        }
        while (_queue.TryDequeue(out var queuedItem))
        {
            queuedItem.CancellationTokenSource.Cancel();
            queuedItem.CancellationTokenSource.Dispose();
        }
    }

    private async Task ProcessQueueAsync()
    {
        if (!await _processingGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            while (_queue.TryDequeue(out var item))
            {
                lock (_currentLock)
                {
                    _currentItem = item;
                }
                try
                {
                    await ProcessItemAsync(item);
                }
                catch (OperationCanceledException)
                {
                    logger.LogDebug("已取消 MiniMax 语音播报。");
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "MiniMax 语音播报失败。");
                    UpdateStatus($"语音播报失败：{exception.Message}");
                }
                finally
                {
                    lock (_currentLock)
                    {
                        if (ReferenceEquals(_currentItem, item))
                        {
                            _currentItem = null;
                        }
                    }
                    item.CancellationTokenSource.Dispose();
                }
            }
        }
        finally
        {
            _processingGate.Release();
            if (!_queue.IsEmpty)
            {
                _ = ProcessQueueAsync();
            }
        }
    }

    private async Task ProcessItemAsync(SpeechQueueItem item)
    {
        var cancellationToken = item.CancellationTokenSource.Token;
        cancellationToken.ThrowIfCancellationRequested();

        var runtimeSettings = settings.CreateRuntimeSettings();
        var cachedPhrases = _phraseCacheStore.GetKnownPhrases(runtimeSettings);
        var segments = MiniMaxSpeechSegmenter.Segment(item.Text, cachedPhrases);
        if (segments.Count == 0)
        {
            UpdateStatus("没有可播报的 MiniMax 语音文本。");
            return;
        }

        logger.LogInformation("MiniMax 语音分段：{Segments}", string.Join(" | ", segments));
        var cachePaths = new List<string>(segments.Count);
        var cacheHitCount = 0;
        foreach (var segment in segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cachePath = _phraseCacheStore.GetAudioPath(segment, runtimeSettings);
            if (!File.Exists(cachePath))
            {
                logger.LogInformation("正在通过 MiniMax 生成短语：{Segment}，音色：{VoiceId}", segment, runtimeSettings.VoiceId);
                var audio = await apiClient.SynthesizeAsync(segment, runtimeSettings, cancellationToken);
                await SaveAudioCacheAsync(cachePath, audio, cancellationToken);
            }
            else
            {
                cacheHitCount++;
                logger.LogDebug("使用 MiniMax 短语缓存：{Segment}，{CachePath}", segment, cachePath);
            }

            _phraseCacheStore.RegisterPhrase(segment, runtimeSettings);
            cachePaths.Add(cachePath);
        }

        var volume = (float)(ISpeechService.GlobalSettings?.SpeechVolume ?? 1);
        foreach (var cachePath in cachePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var audioStream = File.OpenRead(cachePath);
            await audioService.PlayAudioAsync(audioStream, volume, cancellationToken);
        }

        UpdateStatus($"最近一次 MiniMax 语音播报成功：{segments.Count} 个短语，{cacheHitCount} 个缓存命中。");
    }

    private static async Task SaveAudioCacheAsync(string cachePath, byte[] audio, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(CacheFolderPath);
        var temporaryPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, audio, cancellationToken);
            File.Move(temporaryPath, cachePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void UpdateStatus(string message)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            settings.StatusMessage = message;
            return;
        }
        Dispatcher.UIThread.Post(() => settings.StatusMessage = message);
    }

    private sealed record SpeechQueueItem(string Text, CancellationTokenSource CancellationTokenSource);
}
