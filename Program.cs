using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using dotenv.net;

// Classe helper para callback de PushAudioOutputStream
public class PushStreamCallback : PushAudioOutputStreamCallback
{
    private Action<byte[]> onAudioData;

    public PushStreamCallback(Action<byte[]> onAudioData)
    {
        this.onAudioData = onAudioData;
    }

    public override uint Write(byte[] dataBuffer)
    {
        onAudioData?.Invoke(dataBuffer);
        return (uint)dataBuffer.Length;
    }

    public override void Close()
    {
        // Nada a fazer
    }
}

public class Program
{
    static string selectedOutputDevice = "";
    static bool userWantsToHear = false;
    static bool otherWantsToHear = false;

    // Cache de configurações para evitar recriação
    static SpeechConfig? cachedSpeechConfig = null;
    static SpeechTranslationConfig? cachedTranslationConfig = null;
    static string cachedSpeechKey = "";
    static string cachedRegion = "";

    // Token para cancellation
    static CancellationTokenSource? translationCancellation = null;

    // Pool de MemoryStream para reuso (melhor performance)
    static readonly ConcurrentBag<MemoryStream> memoryStreamPool = new ConcurrentBag<MemoryStream>();

    // Cache de dispositivos de áudio (Lazy initialization)
    static readonly Lazy<MMDeviceEnumerator> deviceEnumerator = new Lazy<MMDeviceEnumerator>(() => new MMDeviceEnumerator());

    static async Task Main(string[] args)
    {
        DotEnv.Load();
        DisplayHeader();
        SelectAudioConfiguration();
        await TestAzureSpeechConnection();
        translationCancellation = new CancellationTokenSource();
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
            otherWantsToHear = false;
        }
        else
        {
            userWantsToHear = false;
            Console.WriteLine("✓ Sem áudio local\n");

            // Pergunta 2: Quer que a pessoa te escute? (apenas se NÃO quer ouvir)
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

            // Usar cache de configurações para evitar recriação a cada loop
            var translationConfig = GetCachedTranslationConfig(speechKey, region);
            translationConfig.SpeechRecognitionLanguage = "pt-BR";
            translationConfig.AddTargetLanguage(targetLanguage ?? "en");

            using (var audioConfig = AudioConfig.FromDefaultMicrophoneInput())
            using (var recognizer = new TranslationRecognizer(translationConfig, audioConfig))
            {
                Console.WriteLine("🎤 Fale algo em português... (pressione Ctrl+C para parar)\n");

                // Usar reconhecimento contínuo é MUITO mais rápido que RecognizeOnceAsync
                recognizer.Recognizing += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Result.Text))
                    {
                        Console.WriteLine($"🔄 Reconhecendo: {e.Result.Text}");
                    }
                };

                recognizer.Recognized += async (s, e) =>
                {
                    if (e.Result.Reason == ResultReason.TranslatedSpeech)
                    {
                        Console.WriteLine($"✓ Reconhecido (PT-BR): {e.Result.Text}");

                        if (e.Result.Translations.ContainsKey(targetLanguage))
                        {
                            string translatedText = e.Result.Translations[targetLanguage];
                            Console.WriteLine($"✓ Traduzido ({targetLanguage.ToUpper()}): {translatedText}\n");

                            // Executar síntese e reprodução
                            await SynthesizeAndPlayAudioOptimized(speechKey, region, translatedText, synthesisLanguage, voiceName);
                        }
                    }
                    else if (e.Result.Reason == ResultReason.NoMatch)
                    {
                        Console.WriteLine("⚠️  Nenhuma fala detectada\n");
                    }
                };

                recognizer.Canceled += (s, e) =>
                {
                    var cancellation = CancellationDetails.FromResult(e.Result);
                    Console.WriteLine($"❌ Erro: {cancellation.ErrorDetails}\n");
                };

                // INICIAR RECONHECIMENTO CONTÍNUO
                await recognizer.StartContinuousRecognitionAsync();

                // Aguardar até ser cancelado
                while (!translationCancellation?.Token.IsCancellationRequested ?? true)
                {
                    await Task.Delay(100);
                }

                await recognizer.StopContinuousRecognitionAsync();
            }

            Console.WriteLine("\n✅ Tradução finalizada!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro: {ex.Message}");
        }
    }

    // Método auxiliar para cache de SpeechTranslationConfig
    static SpeechTranslationConfig GetCachedTranslationConfig(string speechKey, string region)
    {
        if (cachedTranslationConfig == null || cachedSpeechKey != speechKey || cachedRegion != region)
        {
            cachedTranslationConfig = SpeechTranslationConfig.FromSubscription(speechKey, region);
            cachedSpeechKey = speechKey;
            cachedRegion = region;
        }
        return cachedTranslationConfig;
    }

    // Método auxiliar para cache de SpeechConfig
    static SpeechConfig GetCachedSpeechConfig(string speechKey, string region)
    {
        if (cachedSpeechConfig == null || cachedSpeechKey != speechKey || cachedRegion != region)
        {
            cachedSpeechConfig = SpeechConfig.FromSubscription(speechKey, region);
            cachedSpeechKey = speechKey;
            cachedRegion = region;
        }
        return cachedSpeechConfig;
    }

    static async Task SynthesizeAndPlayAudioOptimized(string speechKey, string region, string text, string language, string voiceName)
    {
        try
        {
            // Usar cache para SpeechConfig
            var speechConfig = GetCachedSpeechConfig(speechKey, region);
            speechConfig.SpeechSynthesisLanguage = language;
            speechConfig.SpeechSynthesisVoiceName = voiceName;

            // Se um dispositivo foi selecionado, usar MemoryStream (MAIS RÁPIDO que arquivo)
            if (!string.IsNullOrEmpty(selectedOutputDevice))
            {
                Console.WriteLine("🔊 Sintetizando áudio traduzido (memória)...");

                // Pegar MemoryStream do pool ou criar novo
                if (!memoryStreamPool.TryTake(out var audioStream))
                {
                    audioStream = new MemoryStream(65536); // Pré-alocar 64KB para melhor performance
                }

                audioStream.Position = 0;
                audioStream.SetLength(0); // Limpar stream reutilizado

                try
                {
                    // SINTETIZAR direto em memória usando PushAudioOutputStream com callback
                    byte[] audioData = null;
                    var pushStream = AudioOutputStream.CreatePushStream(new PushStreamCallback(audioBytes =>
                    {
                        audioStream.Write(audioBytes, 0, audioBytes.Length);
                    }));

                    using (var audioConfig = AudioConfig.FromStreamOutput(pushStream))
                    using (var synthesizer = new SpeechSynthesizer(speechConfig, audioConfig))
                    {
                        var result = await synthesizer.SpeakTextAsync(text);

                        if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                        {
                            audioStream.Position = 0;
                            Console.WriteLine("✓ Áudio sintetizado com sucesso!");

                            // Reproduzir do stream de memória (bem mais rápido!)
                            await PlayAudioFromMemoryOptimizedAsync(audioStream, selectedOutputDevice);
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
                    // Devolver stream ao pool para reuso
                    memoryStreamPool.Add(audioStream);
                }
            }
            else if (userWantsToHear)
            {
                // Usar dispositivo padrão para o usuário ouvir (mais rápido)
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

    // Versão otimizada: reproduz áudio PCM bruto diretamente da memória
    static async Task PlayAudioFromMemoryOptimizedAsync(MemoryStream audioStream, string deviceName)
    {
        try
        {
            var enumerator = deviceEnumerator.Value;
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            int deviceIndex = -1;
            int currentIndex = 0;

            // Busca rápida do dispositivo
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

            // Azure Speech entrega PCM bruto (16-bit, 16kHz, mono)
            // Usar RawSourceWaveStream para ler PCM bruto sem header RIFF
            audioStream.Position = 0;
            var waveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, mono

            using (var rawStream = new RawSourceWaveStream(audioStream, waveFormat))
            using (var waveOutEvent = new WaveOutEvent { DeviceNumber = deviceIndex })
            {
                waveOutEvent.Init(rawStream);
                waveOutEvent.Play();
                Console.WriteLine($"▶️  Reproduzindo em: {deviceName}");

                // Aguardar reprodução terminar
                while (waveOutEvent.PlaybackState == PlaybackState.Playing)
                {
                    await Task.Delay(50);
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