using dotenv.net;

namespace TraducaoRealtime.Services;

/// <summary>
/// Gerencia carregamento e acesso às configurações do .env
/// </summary>
public class ConfigManager
{
    private string? _speechKey;
    private string? _region;
    private string? _recognitionLanguage;
    private string? _translationTargetLanguage;
    private string? _synthesisLanguage;
    private string? _voiceName;

    public ConfigManager()
    {
        LoadConfiguration();
    }

    private void LoadConfiguration()
    {
        DotEnv.Load();
        _speechKey = Environment.GetEnvironmentVariable("SPEECH_KEY");
        _region = Environment.GetEnvironmentVariable("SPEECH_REGION");
        _recognitionLanguage = Environment.GetEnvironmentVariable("RECOGNITION_LANGUAGE") ?? "pt-BR";
        _translationTargetLanguage = Environment.GetEnvironmentVariable("TRANSLATION_TARGET_LANGUAGE") ?? "en";
        _synthesisLanguage = Environment.GetEnvironmentVariable("SYNTHESIS_LANGUAGE") ?? "en-US";
        _voiceName = Environment.GetEnvironmentVariable("VOICE_NAME");
    }

    public string SpeechKey => _speechKey ?? throw new InvalidOperationException("SPEECH_KEY não configurado");
    public string Region => _region ?? throw new InvalidOperationException("SPEECH_REGION não configurado");
    public string RecognitionLanguage => _recognitionLanguage ?? "pt-BR";
    public string TranslationTargetLanguage => _translationTargetLanguage ?? "en";
    public string SynthesisLanguage => _synthesisLanguage ?? "en-US";
    public string? VoiceName => _voiceName;

    public bool IsConfigured => !string.IsNullOrEmpty(_speechKey) && !string.IsNullOrEmpty(_region);
}
