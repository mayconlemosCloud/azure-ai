using TraducaoRealtime.Interfaces;
using TraducaoRealtime.Models;
using TraducaoRealtime.Utils;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;

namespace TraducaoRealtime.Services;

/// <summary>
/// Orquestra toda a lógica de tradução em tempo real
/// Desacoplada da implementação específica (Azure, Google, etc)
/// </summary>
public class TranslationEngine
{
    private readonly ISpeechProvider _speechProvider;
    private readonly IAudioManager _audioManager;
    private readonly ConfigManager _configManager;
    private readonly AudioConfiguration _audioConfig;
    private CancellationTokenSource? _cancellationSource;

    public TranslationEngine(
        ISpeechProvider speechProvider,
        IAudioManager audioManager,
        ConfigManager configManager,
        AudioConfiguration audioConfig)
    {
        _speechProvider = speechProvider;
        _audioManager = audioManager;
        _configManager = configManager;
        _audioConfig = audioConfig;
    }

    public async Task StartAsync()
    {
        if (!_configManager.IsConfigured)
        {
            Console.WriteLine("❌ Erro: SPEECH_KEY ou SPEECH_REGION não configurados no .env\n");
            return;
        }

        _cancellationSource = new CancellationTokenSource();

        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("🎤 INICIANDO TRADUÇÃO EM TEMPO REAL");
        Console.WriteLine(new string('=', 50) + "\n");

        try
        {
            Console.WriteLine("🎤 Fale algo em português... (pressione Ctrl+C para parar)\n");

            await _speechProvider.StartContinuousRecognitionAsync(
                OnRecognizing,
                OnRecognized,
                OnError,
                _cancellationSource.Token);

            // Aguardar até ser cancelado
            while (!_cancellationSource.Token.IsCancellationRequested)
            {
                await Task.Delay(100);
            }

            await _speechProvider.StopContinuousRecognitionAsync();
            Console.WriteLine("\n✅ Tradução finalizada!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro: {ex.Message}");
        }
    }

    public void Stop()
    {
        _cancellationSource?.Cancel();
    }

    private void OnRecognizing(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            Console.WriteLine($"🔄 Reconhecendo: {text}");
        }
    }

    private async Task OnRecognized(string translatedText)
    {
        // Orquestração da síntese e reprodução
        string outputDevice = _audioConfig.OthersWantToHear 
            ? _audioConfig.SelectedOutputDevice ?? "" 
            : "";

        await _speechProvider.SynthesizeAndPlayAsync(
            translatedText,
            outputDevice,
            _cancellationSource?.Token ?? CancellationToken.None);

        // Pausa para evitar feedback do microfone
        await Task.Delay(500);
    }

    private void OnError(string error)
    {
        Console.WriteLine($"❌ Erro: {error}\n");
    }
}
