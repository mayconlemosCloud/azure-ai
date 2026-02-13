using Microsoft.CognitiveServices.Speech;

namespace TraducaoRealtime.Services;

/// <summary>
/// Cache de configurações Speech para evitar recriação custosa
/// </summary>
public class SpeechCacheManager
{
    private SpeechConfig? _cachedSpeechConfig;
    private string? _cachedKey;
    private string? _cachedRegion;

    public SpeechConfig GetCachedSpeechConfig(string speechKey, string region)
    {
        if (_cachedSpeechConfig == null || _cachedKey != speechKey || _cachedRegion != region)
        {
            _cachedSpeechConfig = SpeechConfig.FromSubscription(speechKey, region);
            _cachedKey = speechKey;
            _cachedRegion = region;
        }
        return _cachedSpeechConfig;
    }
}
