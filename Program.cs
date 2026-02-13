using TraducaoRealtime.Models;
using TraducaoRealtime.Services;
using TraducaoRealtime.Providers;
using TraducaoRealtime.UI;
using TraducaoRealtime.Utils;

public class Program
{
    static async Task Main(string[] args)
    {
        // Configuração
        var configManager = new ConfigManager();

        // UI
        var audioManager = new AudioManager();
        var consoleUI = new ConsoleUI(audioManager);
        consoleUI.DisplayHeader();

        // Obter configuração de áudio do usuário
        var audioConfig = consoleUI.SelectAudioConfiguration();

        // Testar conexão
        consoleUI.TestAzureConnection(
            configManager.Region,
            configManager.RecognitionLanguage,
            configManager.TranslationTargetLanguage,
            configManager.SynthesisLanguage,
            configManager.VoiceName);

        // Dependências
        var cacheManager = new SpeechCacheManager();
        var audioPool = new AudioPool();

        // Criar provider de speech (Azure por padrão, pode ser substituído)
        var speechProvider = new AzureSpeechProvider(
            configManager.SpeechKey,
            configManager.Region,
            configManager.RecognitionLanguage,
            configManager.TranslationTargetLanguage,
            configManager.SynthesisLanguage,
            configManager.VoiceName,
            audioManager,
            cacheManager,
            audioPool);

        // Orquestrador da tradução
        var translationEngine = new TranslationEngine(
            speechProvider,
            audioManager,
            configManager,
            audioConfig);

        // Iniciar tradução
        await translationEngine.StartAsync();
    }
}
