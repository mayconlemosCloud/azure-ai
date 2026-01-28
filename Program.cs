using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using dotenv.net;

public class Program
{
    static async Task Main(string[] args)
    {
        DotEnv.Load();
        DisplayHeader();
        SelectAudioDevice();
        await TestAzureSpeechConnection();
        await StartRealTimeTranslation();
    }

    static void SelectAudioDevice()
    {
        Console.WriteLine("🔊 Detectando dispositivos de áudio disponíveis...\n");

        var devices = GetAudioDevices();

        if (devices.Count == 0)
        {
            Console.WriteLine("❌ Nenhum dispositivo de áudio encontrado!\n");
            return;
        }

        Console.WriteLine("Dispositivos encontrados:\n");
        for (int i = 0; i < devices.Count; i++)
        {
            Console.WriteLine($"{i + 1}️⃣  {devices[i]}");
        }

        Console.Write($"\nDigite o número do dispositivo (1-{devices.Count}): ");
        string option = Console.ReadLine();
        Console.WriteLine();

        if (int.TryParse(option, out int deviceIndex) && deviceIndex > 0 && deviceIndex <= devices.Count)
        {
            Console.WriteLine($"✓ Dispositivo selecionado: {devices[deviceIndex - 1]}\n");
        }
        else
        {
            Console.WriteLine("❌ Opção inválida! Usando dispositivo padrão...\n");
        }
    }

    static List<string> GetAudioDevices()
    {
        var devices = new List<string>();

        try
        {
            // Dispositivos de reprodução (speakers)
            var enumerator = new MMDeviceEnumerator();
            var renderDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            foreach (var device in renderDevices)
            {
                devices.Add(device.FriendlyName);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Erro ao enumerar dispositivos: {ex.Message}");
        }

        return devices;
    }

    static async Task TestAzureSpeechConnection()
    {
        try
        {
            Console.WriteLine("📂 Carregando configurações...\n");

            string speechKey = Environment.GetEnvironmentVariable("SPEECH_KEY");
            string region = Environment.GetEnvironmentVariable("SPEECH_REGION");
            string recognitionLanguage = Environment.GetEnvironmentVariable("RECOGNITION_LANGUAGE");
            string translationTargetLanguage = Environment.GetEnvironmentVariable("TRANSLATION_TARGET_LANGUAGE");
            string synthesisLanguage = Environment.GetEnvironmentVariable("SYNTHESIS_LANGUAGE");
            string voiceName = Environment.GetEnvironmentVariable("VOICE_NAME");

            if (string.IsNullOrEmpty(speechKey) || string.IsNullOrEmpty(region))
            {
                Console.WriteLine("❌ Erro: SPEECH_KEY ou SPEECH_REGION não configurados no .env\n");
                return;
            }

            DisplayConfig(region, recognitionLanguage, translationTargetLanguage, synthesisLanguage, voiceName);

            Console.WriteLine("🔗 Conectando ao Azure Speech Services...");
            var speechConfig = SpeechConfig.FromSubscription(speechKey, region);
            DisplaySuccess();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro na conexão: {ex.Message}\n");
        }
    }

    static void DisplayHeader()
    {
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("   🎤 TRADUÇÃO EM TEMPO REAL - Azure Speech");
        Console.WriteLine(new string('=', 50) + "\n");
    }

    static void DisplayConfig(string region, string? recognitionLang, string? translationLang, string? synthesisLang, string? voiceName)
    {
        Console.WriteLine("✓ Variáveis carregadas:");
        Console.WriteLine($"  • Region: {region}");
        Console.WriteLine($"  • Reconhecimento: {recognitionLang ?? "N/A"}");
        Console.WriteLine($"  • Tradução: {translationLang ?? "N/A"}");
        Console.WriteLine($"  • Síntese: {synthesisLang ?? "N/A"}");
        Console.WriteLine($"  • Voz: {voiceName ?? "N/A"}\n");
    }

    static void DisplaySuccess()
    {
        Console.WriteLine("✓ Conexão estabelecida!\n");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine("✅ Sistema pronto para usar!");
        Console.WriteLine(new string('=', 50) + "\n");
    }

    static async Task StartRealTimeTranslation()
    {
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("🎤 INICIANDO TRADUÇÃO EM TEMPO REAL");
        Console.WriteLine(new string('=', 50) + "\n");

        try
        {
            string speechKey = Environment.GetEnvironmentVariable("SPEECH_KEY");
            string region = Environment.GetEnvironmentVariable("SPEECH_REGION");
            string targetLanguage = Environment.GetEnvironmentVariable("TRANSLATION_TARGET_LANGUAGE");
            string synthesisLanguage = Environment.GetEnvironmentVariable("SYNTHESIS_LANGUAGE");
            string voiceName = Environment.GetEnvironmentVariable("VOICE_NAME");

            var speechConfig = SpeechConfig.FromSubscription(speechKey, region);
            speechConfig.SpeechRecognitionLanguage = "pt-BR";

            var translationConfig = SpeechTranslationConfig.FromSubscription(speechKey, region);
            translationConfig.SpeechRecognitionLanguage = "pt-BR";
            translationConfig.AddTargetLanguage(targetLanguage ?? "en");

            using (var audioConfig = AudioConfig.FromDefaultMicrophoneInput())
            {
                using (var recognizer = new TranslationRecognizer(translationConfig, audioConfig))
                {
                    Console.WriteLine("🎤 Fale algo em português... (pressione Enter para parar)\n");

                    while (true)
                    {
                        Console.Write("Aguardando áudio... ");
                        var result = await recognizer.RecognizeOnceAsync();

                        if (result.Reason == ResultReason.TranslatedSpeech)
                        {
                            Console.WriteLine($"✓ Reconhecido (PT-BR): {result.Text}");

                            if (result.Translations.ContainsKey(targetLanguage))
                            {
                                string translatedText = result.Translations[targetLanguage];
                                Console.WriteLine($"✓ Traduzido ({targetLanguage.ToUpper()}): {translatedText}\n");

                                await SynthesizeAndPlayAudio(speechKey, region, translatedText, synthesisLanguage, voiceName);
                            }
                        }
                        else if (result.Reason == ResultReason.NoMatch)
                        {
                            Console.WriteLine("⚠️  Nenhuma fala detectada\n");
                        }
                        else if (result.Reason == ResultReason.Canceled)
                        {
                            var cancellation = CancellationDetails.FromResult(result);
                            Console.WriteLine($"❌ Erro: {cancellation.ErrorDetails}\n");
                            break;
                        }
                    }
                }
            }

            Console.WriteLine("\n✅ Tradução finalizada!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro: {ex.Message}");
        }
    }

    static async Task SynthesizeAndPlayAudio(string speechKey, string region, string text, string language, string voiceName)
    {
        try
        {
            var speechConfig = SpeechConfig.FromSubscription(speechKey, region);
            speechConfig.SpeechSynthesisLanguage = language;
            speechConfig.SpeechSynthesisVoiceName = voiceName;

            using (var audioConfig = AudioConfig.FromDefaultSpeakerOutput())
            using (var synthesizer = new SpeechSynthesizer(speechConfig, audioConfig))
            {
                Console.WriteLine("🔊 Reproduzindo áudio traduzido...");
                var result = await synthesizer.SpeakTextAsync(text);

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
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao reproduzir áudio: {ex.Message}\n");
        }
    }
}