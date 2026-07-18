namespace ClassIsland.MiniMaxTts.Models;

public sealed record MiniMaxVoiceInfo(string VoiceId, string VoiceName, string Category)
{
    public string DisplayName => string.IsNullOrWhiteSpace(VoiceName)
        ? $"{VoiceId}（{Category}）"
        : $"{VoiceName} · {VoiceId}";
}
