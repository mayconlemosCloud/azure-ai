using TraducaoRealtime.Interfaces;
using TraducaoRealtime.Services;
using TraducaoRealtime.Utils;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;

namespace TraducaoRealtime.Providers;

/// <summary>
/// Implementação específica para Azure Speech Services
/// Pode ser substituída por GoogleSpeechProvider, OpenAISpeechProvider, etc
/// sem alterar a regra de negócio
/// </summary>
public class AzureSpeechProvider : ISpeechProvider
{
    private readonly string _speechKey;
    private readonly string _region;
    private readonly string _recognitionLanguage;
    private readonly string _translationTargetLanguage;
    private readonly string _synthesisLanguage;
    private readonly string? _voiceName;
    private readonly IAudioManager _audioManager;
    private readonly SpeechCacheManager _cacheManager;
    private readonly AudioPool _audioPool;
    private TranslationRecognizer? _recognizer;

    public AzureSpeechProvider(
        string speechKey,
        string region,
        string recognitionLanguage,
        string translationTargetLanguage,
        string synthesisLanguage,
        string? voiceName,
        IAudioManager audioManager,
        SpeechCacheManager cacheManager,
        AudioPool audioPool)
    {
        _speechKey = speechKey;
        _region = region;
        _recognitionLanguage = recognitionLanguage;
        _translationTargetLanguage = translationTargetLanguage;
        _synthesisLanguage = synthesisLanguage;
        _voiceName = voiceName;
        _audioManager = audioManager;
        _cacheManager = cacheManager;
        _audioPool = audioPool;
    }

    public async Task StartContinuousRecognitionAsync(
        Action<string> onRecognizing,
        Func<string, Task> onRecognized,
        Action<string> onError,
        CancellationToken cancellationToken)
    {
        try
        {
            var translationConfig = SpeechTranslationConfig.FromSubscription(_speechKey, _region);
            translationConfig.SpeechRecognitionLanguage = _recognitionLanguage;
            translationConfig.AddTargetLanguage(_translationTargetLanguage);

            using (var audioConfig = AudioConfig.FromDefaultMicrophoneInput())
            {
                _recognizer = new TranslationRecognizer(translationConfig, audioConfig);

                _recognizer.Recognizing += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Result.Text))
                    {
                        onRecognizing?.Invoke(e.Result.Text);
                    }
                };

                _recognizer.Recognized += async (s, e) =>
                {
                    if (e.Result.Reason == ResultReason.TranslatedSpeech)
                    {
                        Console.WriteLine($"✓ Reconhecido (PT-BR): {e.Result.Text}");

                        if (e.Result.Translations.ContainsKey(_translationTargetLanguage))
                        {
                            string translatedText = e.Result.Translations[_translationTargetLanguage];
                            Console.WriteLine($"✓ Traduzido ({_translationTargetLanguage.ToUpper()}): {translatedText}\n");

                            await (onRecognized?.Invoke(translatedText) ?? Task.CompletedTask);
                        }
                    }
                    else if (e.Result.Reason == ResultReason.NoMatch)
                    {
                        Console.WriteLine("⚠️  Nenhuma fala detectada\n");
                    }
                };

                _recognizer.Canceled += (s, e) =>
                {
                    var cancellation = CancellationDetails.FromResult(e.Result);
                    onError?.Invoke(cancellation.ErrorDetails);
                };

                await _recognizer.StartContinuousRecognitionAsync();
            }
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex.Message);
        }
    }

    public async Task StopContinuousRecognitionAsync()
    {
        if (_recognizer != null)
        {
            await _recognizer.StopContinuousRecognitionAsync();
            _recognizer?.Dispose();
        }
    }

    public async Task SynthesizeAndPlayAsync(
        string text,
        string outputDevice,
        CancellationToken cancellationToken)
    {
        try
        {
            var speechConfig = _cacheManager.GetCachedSpeechConfig(_speechKey, _region);
            speechConfig.SpeechSynthesisLanguage = _synthesisLanguage;

            string voiceName = _voiceName ?? SSMLBuilder.GetDefaultVoice(_synthesisLanguage);
            speechConfig.SpeechSynthesisVoiceName = voiceName;

            // Se um dispositivo foi selecionado, usar MemoryStream (MAIS RÁPIDO que arquivo)
            if (!string.IsNullOrEmpty(outputDevice))
            {
                await SynthesizeToVirtualDeviceAsync(speechConfig, text, voiceName, outputDevice);
            }
            else
            {
                await SynthesizeToDefaultSpeakerAsync(speechConfig, text, voiceName);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao reproduzir áudio: {ex.Message}\n");
        }
    }

    private async Task SynthesizeToVirtualDeviceAsync(
        SpeechConfig speechConfig,
        string text,
        string voiceName,
        string outputDevice)
    {
        Console.WriteLine("🔊 Sintetizando áudio traduzido (memória)...");

        var audioStream = _audioPool.Rent();

        try
        {
            byte[] audioData = null;
            var pushStream = AudioOutputStream.CreatePushStream(new PushStreamCallback(audioBytes =>
            {
                audioStream.Write(audioBytes, 0, audioBytes.Length);
            }));

            using (var audioConfig = AudioConfig.FromStreamOutput(pushStream))
            using (var synthesizer = new SpeechSynthesizer(speechConfig, audioConfig))
            {
                string ssml = SSMLBuilder.CreateSSML(text, _synthesisLanguage, voiceName);
                var result = await synthesizer.SpeakSsmlAsync(ssml);

                if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                {
                    audioStream.Position = 0;
                    Console.WriteLine("✓ Áudio sintetizado com sucesso!");

                    await _audioManager.PlayAudioFromMemoryAsync(audioStream, outputDevice);
                }
                else if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                    Console.WriteLine($"❌ Erro na síntese: {cancellation.ErrorDetails}\n");
                }
            }
        }
        finally
        {
            _audioPool.Return(audioStream);
        }
    }

    private async Task SynthesizeToDefaultSpeakerAsync(
        SpeechConfig speechConfig,
        string text,
        string voiceName)
    {
        using (var audioConfig = AudioConfig.FromDefaultSpeakerOutput())
        using (var synthesizer = new SpeechSynthesizer(speechConfig, audioConfig))
        {
            Console.WriteLine("🔊 Reproduzindo áudio traduzido...");
            string ssml = SSMLBuilder.CreateSSML(text, _synthesisLanguage, voiceName);
            var result = await synthesizer.SpeakSsmlAsync(ssml);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                Console.WriteLine("✓ Áudio reproduzido com sucesso!\n");
            }
            else if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                Console.WriteLine($"❌ Erro na síntese: {cancellation.ErrorDetails}\n");
            }
        }
    }
}

/// <summary>
/// Callback helper para capturar áudio do synthesizer
/// </summary>
public class PushStreamCallback : Microsoft.CognitiveServices.Speech.Audio.PushAudioOutputStreamCallback
{
    private Action<byte[]> _onAudioData;

    public PushStreamCallback(Action<byte[]> onAudioData)
    {
        _onAudioData = onAudioData;
    }

    public override uint Write(byte[] dataBuffer)
    {
        _onAudioData?.Invoke(dataBuffer);
        return (uint)dataBuffer.Length;
    }

    public override void Close()
    {
        // Nada a fazer
    }
}
