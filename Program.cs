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
    static string selectedOutputDevice = "";
    static bool userWantsToHear = false;
    static bool otherWantsToHear = false;

    static async Task Main(string[] args)
    {
        DotEnv.Load();
        DisplayHeader();
        SelectAudioConfiguration();
        await TestAzureSpeechConnection();
        await StartRealTimeTranslation();
    }

    static void SelectAudioConfiguration()
    {
        Console.WriteLine("⚙️  CONFIGURAÇÃO DE ÁUDIO\n");

        // Pergunta 1: Você quer se ouvir?
        Console.WriteLine("🎧 Você quer se ouvir (ouvir o áudio traduzido)?");
        Console.WriteLine("1️⃣  Sim, quero ouvir");
        Console.WriteLine("2️⃣  Não, sem áudio local\n");
        Console.Write("Digite sua opção (1 ou 2): ");
        string option1 = Console.ReadLine();

        if (option1 == "1")
        {
            userWantsToHear = true;
            Console.WriteLine();
            SelectLocalAudioDevice();
        }
        else
        {
            userWantsToHear = false;
            Console.WriteLine("✓ Sem áudio local\n");
        }

        // Pergunta 2: Quer que a pessoa te escute?
        Console.WriteLine("👥 Quer que outras pessoas te escutem (via Discord/OBS)?");
        Console.WriteLine("1️⃣  Sim, quero compartilhar o áudio");
        Console.WriteLine("2️⃣  Não, sem áudio virtual\n");
        Console.Write("Digite sua opção (1 ou 2): ");
        string option2 = Console.ReadLine();

        if (option2 == "1")
        {
            otherWantsToHear = true;
            Console.WriteLine();
            SelectVirtualAudioDevice();
        }
        else
        {
            otherWantsToHear = false;
            Console.WriteLine("✓ Sem áudio virtual\n");
        }

        DisplayAudioConfig();
    }

    static void SelectLocalAudioDevice()
    {
        Console.WriteLine("🔊 Selecione onde VOCÊ quer ouvir o áudio traduzido:\n");

        var devices = GetAudioDevices(DataFlow.Render).Where(d => !d.Contains("CABLE")).ToList();

        if (devices.Count == 0)
        {
            Console.WriteLine("❌ Nenhum dispositivo local encontrado!\n");
            userWantsToHear = false;
            return;
        }

        for (int i = 0; i < devices.Count; i++)
        {
            Console.WriteLine($"{i + 1}️⃣  {devices[i]}");
        }

        Console.Write($"\nDigite o número (1-{devices.Count}): ");
        string option = Console.ReadLine();

        if (int.TryParse(option, out int deviceIndex) && deviceIndex > 0 && deviceIndex <= devices.Count)
        {
            Console.WriteLine($"✓ Você ouvirá em: {devices[deviceIndex - 1]}\n");
        }
        else
        {
            Console.WriteLine("❌ Opção inválida! Usando dispositivo padrão...\n");
        }
    }

    static void SelectVirtualAudioDevice()
    {
        Console.WriteLine("🎙️  Selecione por onde OUTRAS PESSOAS vão ouvir (via Discord/OBS):\n");

        var allDevices = GetAudioDevices(DataFlow.Render);
        Console.WriteLine("📊 Dispositivos disponíveis (DEBUG):");
        foreach (var dev in allDevices)
        {
            Console.WriteLine($"  - {dev}");
        }
        Console.WriteLine();

        var devices = allDevices.Where(d => d.Contains("CABLE")).ToList();

        if (devices.Count == 0)
        {
            Console.WriteLine("❌ Nenhum dispositivo virtual encontrado!");
            Console.WriteLine("⚠️  Instale VB-Audio Virtual Cable para compartilhar áudio\n");
            otherWantsToHear = false;
            return;
        }

        for (int i = 0; i < devices.Count; i++)
        {
            Console.WriteLine($"{i + 1}️⃣  {devices[i]}");
        }

        Console.Write($"\nDigite o número (1-{devices.Count}): ");
        string option = Console.ReadLine();

        if (int.TryParse(option, out int deviceIndex) && deviceIndex > 0 && deviceIndex <= devices.Count)
        {
            selectedOutputDevice = devices[deviceIndex - 1];
            Console.WriteLine($"✓ Outras pessoas ouvirão em: {selectedOutputDevice}\n");
        }
        else
        {
            Console.WriteLine("❌ Opção inválida!\n");
            otherWantsToHear = false;
        }
    }

    static void DisplayAudioConfig()
    {
        Console.WriteLine(new string('=', 50));
        Console.WriteLine("📋 RESUMO DE CONFIGURAÇÃO");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine($"🎧 Você ouve: {(userWantsToHear ? "SIM" : "NÃO")}");
        Console.WriteLine($"👥 Outros ouvem: {(otherWantsToHear ? "SIM - " + selectedOutputDevice : "NÃO")}");
        Console.WriteLine(new string('=', 50) + "\n");
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

            // Se um dispositivo foi selecionado, usar arquivo temporário e reproduzir com NAudio
            if (!string.IsNullOrEmpty(selectedOutputDevice))
            {
                string tempFile = Path.Combine(Path.GetTempPath(), "traducao_audio.wav");

                // Deletar arquivo anterior se existir
                try
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                catch { }

                // SINTETIZAR - deixar sair do using antes de ler
                using (var audioConfig = AudioConfig.FromWavFileOutput(tempFile))
                using (var synthesizer = new SpeechSynthesizer(speechConfig, audioConfig))
                {
                    Console.WriteLine("🔊 Sintetizando áudio traduzido...");
                    var result = await synthesizer.SpeakTextAsync(text);

                    if (result.Reason != ResultReason.SynthesizingAudioCompleted)
                    {
                        if (result.Reason == ResultReason.Canceled)
                        {
                            var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                            Console.WriteLine($"❌ Erro na síntese: {cancellation.ErrorDetails}\n");
                        }
                        return;
                    }

                    Console.WriteLine("✓ Áudio sintetizado com sucesso!");
                } // Aqui o synthesizer e audioConfig são fechados e liberados

                // Agora SIM ler e reproduzir (fora do using)
                await PlayAudioFromFileAsync(tempFile, selectedOutputDevice);
            }
            else if (userWantsToHear)
            {
                // Usar dispositivo padrão para o usuário ouvir
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao reproduzir áudio: {ex.Message}\n");
        }
    }

    static async Task PlayAudioFromFileAsync(string filePath, string deviceName)
    {
        try
        {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            int deviceIndex = -1;
            int currentIndex = 0;

            foreach (var device in devices)
            {
                if (device.FriendlyName == deviceName)
                {
                    deviceIndex = currentIndex;
                    break;
                }
                currentIndex++;
            }

            if (deviceIndex == -1)
            {
                Console.WriteLine("⚠️  Dispositivo não encontrado.\n");
                return;
            }

            // Aguardar arquivo estar pronto
            await Task.Delay(300);

            using (var waveFileReader = new WaveFileReader(filePath))
            using (var waveOutEvent = new WaveOutEvent { DeviceNumber = deviceIndex })
            {
                waveOutEvent.Init(waveFileReader);
                waveOutEvent.Play();
                Console.WriteLine($"▶️  Reproduzindo em: {deviceName}");

                // Aguardar reprodução terminar
                while (waveOutEvent.PlaybackState == PlaybackState.Playing)
                {
                    await Task.Delay(100);
                }

                Console.WriteLine("✓ Reprodução concluída!\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao reproduzir áudio: {ex.Message}\n");
        }
    }

    static List<string> GetAudioDevices(DataFlow dataFlow)
    {
        var devices = new List<string>();

        try
        {
            var enumerator = new MMDeviceEnumerator();
            var audioDevices = enumerator.EnumerateAudioEndPoints(dataFlow, DeviceState.Active);

            foreach (var device in audioDevices)
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
}