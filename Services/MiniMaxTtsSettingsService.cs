using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ClassIsland.MiniMaxTts.Models;

namespace ClassIsland.MiniMaxTts.Services;

public sealed class MiniMaxTtsSettingsService : INotifyPropertyChanged
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly IReadOnlyList<string> BuiltInModelOptions =
    [
        "speech-2.8-hd",
        "speech-2.8-turbo",
        "speech-2.6-hd",
        "speech-2.6-turbo",
        "speech-02-hd",
        "speech-02-turbo",
        "speech-01-hd",
        "speech-01-turbo"
    ];
    private readonly string _settingsPath;
    private readonly List<MiniMaxVoiceInfo> _accountVoices = [];
    private readonly List<MiniMaxVoiceInfo> _customVoices = [];
    private readonly List<string> _customModels = [];
    private readonly object _syncRoot = new();
    private bool _isLoading;
    private string _apiKey = "";
    private string _apiBaseUrl = "https://api.minimaxi.com";
    private string _model = "speech-2.8-hd";
    private string _voiceId = "Chinese (Mandarin)_News_Anchor";
    private double _speed = 1;
    private double _volume = 1;
    private int _pitch;
    private string _emotion = "";
    private string _languageBoost = "auto";
    private string _customVoiceName = "";
    private MiniMaxVoiceInfo? _selectedVoice;
    private string _statusMessage = "填写 API Key 后可读取账户音色。";
    private bool _isBusy;

    public MiniMaxTtsSettingsService(string settingsPath)
    {
        _settingsPath = settingsPath;
        Load();
        RebuildAvailableVoices();
        RebuildModelOptions();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> ModelOptions { get; } = [];

    public IReadOnlyList<string> LanguageBoostOptions { get; } =
    [
        "auto", "Chinese", "Chinese,Yue", "English", "Japanese", "Korean", "French", "German", "Spanish"
    ];

    public IReadOnlyList<string> EmotionOptions { get; } =
    [
        "", "happy", "sad", "angry", "fearful", "disgusted", "surprised", "calm", "fluent", "whisper"
    ];

    public ObservableCollection<MiniMaxVoiceInfo> AvailableVoices { get; } = [];

    public string ApiKey
    {
        get => _apiKey;
        set => SetSetting(ref _apiKey, value ?? "");
    }

    public string ApiBaseUrl
    {
        get => _apiBaseUrl;
        set => SetSetting(ref _apiBaseUrl, string.IsNullOrWhiteSpace(value) ? "https://api.minimaxi.com" : value.Trim());
    }

    public string Model
    {
        get => _model;
        set => SetSetting(ref _model, value?.Trim() ?? "");
    }

    public string VoiceId
    {
        get => _voiceId;
        set => SetSetting(ref _voiceId, value?.Trim() ?? "");
    }

    public double Speed
    {
        get => _speed;
        set => SetSetting(ref _speed, Math.Clamp(value, 0.5, 2));
    }

    public double Volume
    {
        get => _volume;
        set => SetSetting(ref _volume, Math.Clamp(value, 0.1, 10));
    }

    public int Pitch
    {
        get => _pitch;
        set => SetSetting(ref _pitch, Math.Clamp(value, -12, 12));
    }

    public string Emotion
    {
        get => _emotion;
        set => SetSetting(ref _emotion, value ?? "");
    }

    public string LanguageBoost
    {
        get => _languageBoost;
        set => SetSetting(ref _languageBoost, value ?? "auto");
    }

    public string CustomVoiceName
    {
        get => _customVoiceName;
        set => SetSetting(ref _customVoiceName, value ?? "");
    }

    public MiniMaxVoiceInfo? SelectedVoice
    {
        get => _selectedVoice;
        set
        {
            if (!SetField(ref _selectedVoice, value) || value is null)
            {
                return;
            }
            VoiceId = value.VoiceId;
            CustomVoiceName = value.Category == "自定义" ? value.VoiceName : "";
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public void UpdateAvailableVoices(IEnumerable<MiniMaxVoiceInfo> voices)
    {
        _accountVoices.Clear();
        _accountVoices.AddRange(voices
            .Where(x => !string.IsNullOrWhiteSpace(x.VoiceId))
            .Select(x => new MiniMaxVoiceInfo(x.VoiceId.Trim(), x.VoiceName.Trim(), x.Category.Trim())));
        Save();
        RebuildAvailableVoices();
    }

    public bool SaveCustomVoice()
    {
        if (string.IsNullOrWhiteSpace(VoiceId))
        {
            return false;
        }
        var voiceId = VoiceId.Trim();
        var voiceName = string.IsNullOrWhiteSpace(CustomVoiceName) ? voiceId : CustomVoiceName.Trim();
        _customVoices.RemoveAll(x => x.VoiceId == voiceId);
        _customVoices.Insert(0, new MiniMaxVoiceInfo(voiceId, voiceName, "自定义"));
        VoiceId = voiceId;
        Save();
        RebuildAvailableVoices();
        SelectedVoice = AvailableVoices.First(x => x.VoiceId == voiceId);
        return true;
    }

    public bool RemoveCustomVoice()
    {
        if (_customVoices.RemoveAll(x => x.VoiceId == VoiceId) == 0)
        {
            return false;
        }
        CustomVoiceName = "";
        Save();
        RebuildAvailableVoices();
        return true;
    }

    public bool SaveCustomModel()
    {
        if (string.IsNullOrWhiteSpace(Model))
        {
            return false;
        }
        Model = Model.Trim();
        if (!BuiltInModelOptions.Contains(Model) && !_customModels.Contains(Model))
        {
            _customModels.Insert(0, Model);
            Save();
            RebuildModelOptions();
        }
        return true;
    }

    public MiniMaxTtsRuntimeSettings CreateRuntimeSettings()
    {
        lock (_syncRoot)
        {
            return new MiniMaxTtsRuntimeSettings(
                _apiKey.Trim(),
                _apiBaseUrl.Trim().TrimEnd('/'),
                _model.Trim(),
                _voiceId.Trim(),
                _speed,
                _volume,
                _pitch,
                _emotion,
                _languageBoost);
        }
    }

    public bool RemoveCustomModel()
    {
        if (!_customModels.Remove(Model))
        {
            return false;
        }
        Save();
        RebuildModelOptions();
        return true;
    }

    private void RebuildAvailableVoices()
    {
        var selectedVoiceId = VoiceId;
        AvailableVoices.Clear();
        foreach (var voice in _customVoices.Concat(_accountVoices).DistinctBy(x => x.VoiceId))
        {
            AvailableVoices.Add(voice);
        }
        SelectedVoice = AvailableVoices.FirstOrDefault(x => x.VoiceId == selectedVoiceId);
    }

    private void RebuildModelOptions()
    {
        ModelOptions.Clear();
        foreach (var model in _customModels.Concat(BuiltInModelOptions).Distinct())
        {
            ModelOptions.Add(model);
        }
    }

    private void Load()
    {
        if (!File.Exists(_settingsPath))
        {
            Save();
            return;
        }

        try
        {
            _isLoading = true;
            var model = JsonSerializer.Deserialize<SettingsModel>(File.ReadAllText(_settingsPath), JsonOptions);
            if (model is null)
            {
                return;
            }
            _apiKey = model.ApiKey ?? "";
            _apiBaseUrl = string.IsNullOrWhiteSpace(model.ApiBaseUrl) ? _apiBaseUrl : model.ApiBaseUrl;
            _model = string.IsNullOrWhiteSpace(model.Model) ? _model : model.Model.Trim();
            _voiceId = string.IsNullOrWhiteSpace(model.VoiceId) ? _voiceId : model.VoiceId.Trim();
            _speed = Math.Clamp(model.Speed, 0.5, 2);
            _volume = Math.Clamp(model.Volume, 0.1, 10);
            _pitch = Math.Clamp(model.Pitch, -12, 12);
            _emotion = model.Emotion ?? "";
            _languageBoost = string.IsNullOrWhiteSpace(model.LanguageBoost) ? "auto" : model.LanguageBoost;
            _customVoiceName = model.CustomVoiceName ?? "";
            _accountVoices.Clear();
            _accountVoices.AddRange(model.AccountVoices
                .Where(x => !string.IsNullOrWhiteSpace(x.VoiceId))
                .Select(x => new MiniMaxVoiceInfo(x.VoiceId.Trim(), x.VoiceName.Trim(),
                    string.IsNullOrWhiteSpace(x.Category) ? "账户音色" : x.Category.Trim())));
            _customVoices.Clear();
            _customVoices.AddRange(model.CustomVoices
                .Where(x => !string.IsNullOrWhiteSpace(x.VoiceId))
                .Select(x => new MiniMaxVoiceInfo(x.VoiceId.Trim(), x.VoiceName.Trim(), "自定义")));
            _customModels.Clear();
            _customModels.AddRange(model.CustomModels
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct());
        }
        catch (JsonException)
        {
            _isLoading = false;
            Save();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void Save()
    {
        if (_isLoading)
        {
            return;
        }
        lock (_syncRoot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var model = new SettingsModel
            {
                ApiKey = _apiKey,
                ApiBaseUrl = _apiBaseUrl,
                Model = _model,
                VoiceId = _voiceId,
                Speed = _speed,
                Volume = _volume,
                Pitch = _pitch,
                Emotion = _emotion,
                LanguageBoost = _languageBoost,
                CustomVoiceName = _customVoiceName,
                AccountVoices = _accountVoices.Select(ToPresetModel).ToList(),
                CustomVoices = _customVoices.Select(ToPresetModel).ToList(),
                CustomModels = [.. _customModels]
            };
            var temporaryPath = $"{_settingsPath}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(model, JsonOptions));
            File.Move(temporaryPath, _settingsPath, true);
        }
    }

    private bool SetSetting<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!SetField(ref field, value, propertyName))
        {
            return false;
        }
        Save();
        return true;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private sealed class SettingsModel
    {
        public string ApiKey { get; set; } = "";
        public string ApiBaseUrl { get; set; } = "https://api.minimaxi.com";
        public string Model { get; set; } = "speech-2.8-hd";
        public string VoiceId { get; set; } = "Chinese (Mandarin)_News_Anchor";
        public double Speed { get; set; } = 1;
        public double Volume { get; set; } = 1;
        public int Pitch { get; set; }
        public string Emotion { get; set; } = "";
        public string LanguageBoost { get; set; } = "auto";
        public string CustomVoiceName { get; set; } = "";
        public List<VoicePresetModel> AccountVoices { get; set; } = [];
        public List<VoicePresetModel> CustomVoices { get; set; } = [];
        public List<string> CustomModels { get; set; } = [];
    }

    private sealed class VoicePresetModel
    {
        public string VoiceId { get; set; } = "";
        public string VoiceName { get; set; } = "";
        public string Category { get; set; } = "";
    }

    private static VoicePresetModel ToPresetModel(MiniMaxVoiceInfo voice) => new()
    {
        VoiceId = voice.VoiceId,
        VoiceName = voice.VoiceName,
        Category = voice.Category
    };
}

public sealed record MiniMaxTtsRuntimeSettings(
    string ApiKey,
    string ApiBaseUrl,
    string Model,
    string VoiceId,
    double Speed,
    double Volume,
    int Pitch,
    string Emotion,
    string LanguageBoost);
