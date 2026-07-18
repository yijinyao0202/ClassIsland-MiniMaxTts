using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ClassIsland.MiniMaxTts.Services;

internal sealed class MiniMaxPhraseCacheStore(string cacheFolderPath, ILogger logger)
{
    private const int CurrentIndexVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly Dictionary<string, PhraseCacheIndexModel> _indexes = [];
    private readonly object _syncRoot = new();

    public string GetAudioPath(string text, MiniMaxTtsRuntimeSettings runtimeSettings)
    {
        var cacheKey = string.Join('\n',
            text,
            runtimeSettings.ApiBaseUrl,
            runtimeSettings.Model,
            runtimeSettings.VoiceId,
            runtimeSettings.Speed,
            runtimeSettings.Volume,
            runtimeSettings.Pitch,
            runtimeSettings.Emotion,
            runtimeSettings.LanguageBoost);
        var hash = ComputeHash(cacheKey);
        return Path.Combine(cacheFolderPath, $"{hash}.mp3");
    }

    public IReadOnlyList<string> GetKnownPhrases(MiniMaxTtsRuntimeSettings runtimeSettings)
    {
        lock (_syncRoot)
        {
            var profileKey = GetProfileKey(runtimeSettings);
            var index = LoadIndex(profileKey);
            var validEntries = new List<PhraseCacheEntryModel>();
            var seenPhrases = new HashSet<string>(StringComparer.Ordinal);
            var hasChanges = false;

            foreach (var entry in index.Phrases)
            {
                if (entry is null || string.IsNullOrWhiteSpace(entry.Text))
                {
                    hasChanges = true;
                    continue;
                }

                var expectedAudioPath = GetAudioPath(entry.Text, runtimeSettings);
                var expectedFileName = Path.GetFileName(expectedAudioPath);
                if (!string.Equals(entry.FileName, expectedFileName, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(expectedAudioPath) ||
                    !seenPhrases.Add(entry.Text))
                {
                    hasChanges = true;
                    continue;
                }

                validEntries.Add(new PhraseCacheEntryModel
                {
                    Text = entry.Text,
                    FileName = expectedFileName
                });
            }

            if (hasChanges)
            {
                index.Phrases = validEntries;
                TrySaveIndex(profileKey, index);
            }

            return validEntries.Select(x => x.Text).ToArray();
        }
    }

    public void RegisterPhrase(string text, MiniMaxTtsRuntimeSettings runtimeSettings)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        lock (_syncRoot)
        {
            var audioPath = GetAudioPath(text, runtimeSettings);
            if (!File.Exists(audioPath))
            {
                return;
            }

            var profileKey = GetProfileKey(runtimeSettings);
            var index = LoadIndex(profileKey);
            var fileName = Path.GetFileName(audioPath);
            var existingEntry = index.Phrases.FirstOrDefault(x =>
                x is not null && string.Equals(x.Text, text, StringComparison.Ordinal));
            if (existingEntry is not null &&
                string.Equals(existingEntry.FileName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            index.Phrases.RemoveAll(x => x is null || string.Equals(x.Text, text, StringComparison.Ordinal));
            index.Phrases.Add(new PhraseCacheEntryModel
            {
                Text = text,
                FileName = fileName
            });
            TrySaveIndex(profileKey, index);
        }
    }

    private PhraseCacheIndexModel LoadIndex(string profileKey)
    {
        if (_indexes.TryGetValue(profileKey, out var cachedIndex))
        {
            return cachedIndex;
        }

        var indexPath = GetIndexPath(profileKey);
        var index = new PhraseCacheIndexModel();
        if (File.Exists(indexPath))
        {
            try
            {
                var loadedIndex = JsonSerializer.Deserialize<PhraseCacheIndexModel>(File.ReadAllText(indexPath), JsonOptions);
                if (loadedIndex?.Version == CurrentIndexVersion)
                {
                    index = loadedIndex;
                    index.Phrases ??= [];
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                logger.LogWarning(exception, "无法读取 MiniMax 短语缓存索引：{IndexPath}", indexPath);
            }
        }

        _indexes[profileKey] = index;
        return index;
    }

    private void TrySaveIndex(string profileKey, PhraseCacheIndexModel index)
    {
        var indexPath = GetIndexPath(profileKey);
        var temporaryPath = $"{indexPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(cacheFolderPath);
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(index, JsonOptions));
            File.Move(temporaryPath, indexPath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "无法保存 MiniMax 短语缓存索引：{IndexPath}", indexPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private string GetIndexPath(string profileKey) =>
        Path.Combine(cacheFolderPath, $"segments-{profileKey}.json");

    private static string GetProfileKey(MiniMaxTtsRuntimeSettings runtimeSettings)
    {
        var profile = string.Join('\n',
            runtimeSettings.ApiBaseUrl,
            runtimeSettings.Model,
            runtimeSettings.VoiceId,
            runtimeSettings.Speed,
            runtimeSettings.Volume,
            runtimeSettings.Pitch,
            runtimeSettings.Emotion,
            runtimeSettings.LanguageBoost);
        return ComputeHash(profile);
    }

    private static string ComputeHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class PhraseCacheIndexModel
    {
        public int Version { get; set; } = CurrentIndexVersion;
        public List<PhraseCacheEntryModel> Phrases { get; set; } = [];
    }

    private sealed class PhraseCacheEntryModel
    {
        public string Text { get; set; } = "";
        public string FileName { get; set; } = "";
    }
}
