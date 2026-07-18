using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ClassIsland.MiniMaxTts.Models;

namespace ClassIsland.MiniMaxTts.Services;

public sealed class MiniMaxApiClient(MiniMaxTtsSettingsService settings)
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    public async Task<IReadOnlyList<MiniMaxVoiceInfo>> GetVoicesAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAsync("/v1/get_voice", new { voice_type = "all" }, cancellationToken);
        using var document = await ReadResponseAsync(response, cancellationToken);
        var root = document.RootElement;
        EnsureSuccess(root);

        var voices = new List<MiniMaxVoiceInfo>();
        AddVoices(root, "system_voice", "系统音色", voices);
        AddVoices(root, "voice_cloning", "复刻音色", voices);
        AddVoices(root, "voice_generation", "生成音色", voices);
        return voices;
    }

    public async Task<byte[]> SynthesizeAsync(string text, CancellationToken cancellationToken)
    {
        return await SynthesizeAsync(text, settings.CreateRuntimeSettings(), cancellationToken);
    }

    public async Task<byte[]> SynthesizeAsync(string text, MiniMaxTtsRuntimeSettings runtimeSettings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("朗读文本不能为空。", nameof(text));
        }
        if (text.Length >= 10000)
        {
            throw new ArgumentException("MiniMax 同步语音合成单次文本不能达到或超过 10000 个字符。", nameof(text));
        }
        if (string.IsNullOrWhiteSpace(runtimeSettings.VoiceId))
        {
            throw new InvalidOperationException("请先配置 MiniMax 音色 ID。");
        }
        if (string.IsNullOrWhiteSpace(runtimeSettings.Model))
        {
            throw new InvalidOperationException("请先配置 MiniMax 模型 ID。");
        }

        var voiceSetting = new Dictionary<string, object>
        {
            ["voice_id"] = runtimeSettings.VoiceId,
            ["speed"] = runtimeSettings.Speed,
            ["vol"] = runtimeSettings.Volume,
            ["pitch"] = runtimeSettings.Pitch
        };
        if (!string.IsNullOrWhiteSpace(runtimeSettings.Emotion))
        {
            voiceSetting["emotion"] = runtimeSettings.Emotion;
        }

        var payload = new Dictionary<string, object>
        {
            ["model"] = runtimeSettings.Model,
            ["text"] = text,
            ["stream"] = false,
            ["output_format"] = "hex",
            ["language_boost"] = runtimeSettings.LanguageBoost,
            ["voice_setting"] = voiceSetting,
            ["audio_setting"] = new
            {
                sample_rate = 32000,
                bitrate = 128000,
                format = "mp3",
                channel = 1
            }
        };

        using var response = await SendAsync("/v1/t2a_v2", payload, runtimeSettings, cancellationToken);
        using var document = await ReadResponseAsync(response, cancellationToken);
        var root = document.RootElement;
        EnsureSuccess(root);

        if (!root.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("audio", out var audio) ||
            audio.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(audio.GetString()))
        {
            throw new InvalidOperationException("MiniMax 返回结果中没有音频数据。");
        }

        try
        {
            return Convert.FromHexString(audio.GetString()!);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("MiniMax 返回了无效的十六进制音频数据。", exception);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string path, object payload, CancellationToken cancellationToken)
    {
        return await SendAsync(path, payload, settings.CreateRuntimeSettings(), cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(string path, object payload, MiniMaxTtsRuntimeSettings runtimeSettings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(runtimeSettings.ApiKey))
        {
            throw new InvalidOperationException("请先填写 MiniMax API Key。");
        }
        if (!Uri.TryCreate($"{runtimeSettings.ApiBaseUrl.TrimEnd('/')}{path}", UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("MiniMax API 地址无效。");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", runtimeSettings.ApiKey.Trim());
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        return await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static async Task<JsonDocument> ReadResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"MiniMax 请求失败：HTTP {(int)response.StatusCode} {response.ReasonPhrase}。{GetStatusMessage(responseText)}");
        }
        try
        {
            return JsonDocument.Parse(responseText);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("MiniMax 返回了无法解析的响应。", exception);
        }
    }

    private static string GetStatusMessage(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (document.RootElement.TryGetProperty("base_resp", out var baseResponse) &&
                baseResponse.TryGetProperty("status_msg", out var statusMessage))
            {
                return statusMessage.GetString() ?? "";
            }
        }
        catch (JsonException)
        {
        }
        return "";
    }

    private static void EnsureSuccess(JsonElement root)
    {
        if (!root.TryGetProperty("base_resp", out var baseResponse))
        {
            throw new InvalidOperationException("MiniMax 响应缺少状态信息。");
        }
        var statusCode = baseResponse.TryGetProperty("status_code", out var code) ? code.GetInt32() : -1;
        if (statusCode == 0)
        {
            return;
        }
        var statusMessage = baseResponse.TryGetProperty("status_msg", out var message)
            ? message.GetString()
            : "未知错误";
        throw new InvalidOperationException($"MiniMax API 错误 {statusCode}：{statusMessage}");
    }

    private static void AddVoices(JsonElement root, string propertyName, string category, ICollection<MiniMaxVoiceInfo> voices)
    {
        if (!root.TryGetProperty(propertyName, out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return;
        }
        foreach (var item in list.EnumerateArray())
        {
            if (!item.TryGetProperty("voice_id", out var voiceIdElement))
            {
                continue;
            }
            var voiceId = voiceIdElement.GetString();
            if (string.IsNullOrWhiteSpace(voiceId))
            {
                continue;
            }
            var voiceName = item.TryGetProperty("voice_name", out var voiceNameElement)
                ? voiceNameElement.GetString() ?? ""
                : "";
            voices.Add(new MiniMaxVoiceInfo(voiceId, voiceName, category));
        }
    }
}
