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

class Program
{
    // Configurações principais
    private static readonly string SubscriptionKey = GetSubscriptionKey();
    private static readonly string ServiceRegion = "eastus"; // DragonHD suporta eastus

    // Método para obter a chave de forma segura
    private static string GetSubscriptionKey()
    {
        // Tenta obter da variável de ambiente primeiro (mais seguro)
        string? key = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_KEY");

        if (string.IsNullOrEmpty(key))
        {
            // Se não encontrar, tenta ler de um arquivo .env
            // Começa do diretório atual e sobe
            string? projectDir = FindProjectDirectory();
            if (projectDir != null)
            {
                string envFilePath = Path.Combine(projectDir, ".env");
                if (File.Exists(envFilePath))
                {
                    foreach (var lineRaw in File.ReadAllLines(envFilePath))
                    {
                        string trimmedLine = lineRaw.Trim();
                        if (trimmedLine.StartsWith("AZURE_SUBSCRIPTION_KEY=") && !trimmedLine.StartsWith("#"))
                        {
                            key = trimmedLine.Substring("AZURE_SUBSCRIPTION_KEY=".Length).Trim();
                            break;
                        }
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException("Chave de subscrição do Azure não encontrada. " +
                "Defina a variável de ambiente AZURE_SUBSCRIPTION_KEY ou crie um arquivo .env");
        }

        return key;
    }

    // Encontra o diretório do projeto procurando por .csproj
    private static string? FindProjectDirectory()
    {
        string? currentDir = Directory.GetCurrentDirectory();

        for (int i = 0; i < 5; i++)
        {
            if (File.Exists(Path.Combine(currentDir, "TraducaoRealtime.csproj")) ||
                File.Exists(Path.Combine(currentDir, "traducao.sln")))
            {
                return currentDir;
            }

            var parent = Directory.GetParent(currentDir);
            if (parent == null) break;
            currentDir = parent.FullName;
        }

        return currentDir;
    }
    private static readonly string RecognitionLanguage = "pt-BR"; // Reconhecimento em português
    private static readonly string TranslationTargetLanguage = "en-US"; // Tradução para inglês
    private static readonly string SynthesisLanguage = "en-US"; // Síntese em inglês
    private static readonly string VoiceName = "en-US-Brian:DragonHDLatestNeural"; // DragonHD - Máxima qualidade emocional
    // Arquivo temporário para áudio capturado (para Voice Conversion)
    private static string? TempAudioPath;
    // Variável para armazenar o dispositivo de saída selecionado
    private static string? SelectedDeviceId;
    private static AudioOutputType SelectedAudioOutputType;
    private enum AudioOutputType
    {
        Speaker = 1,        // Enviar para alto-falante
        VirtualMic = 2      // Enviar para microfone virtual
    }

    static async Task Main(string[] args)
    {
        // Menu de seleção de tipo de saída de áudio
        Console.WriteLine("=== CONFIGURAÇÃO DE SAÍDA DE ÁUDIO ===");
        Console.WriteLine("1. Enviar para alto-falante (você ouve o áudio traduzido)");
        Console.WriteLine("2. Enviar para microfone virtual (pessoas ouvem áudio traduzido em vez de sua voz em português)");
        Console.WriteLine();

        int audioOutputChoice = -1;
        while (audioOutputChoice < 1 || audioOutputChoice > 2)
        {
            Console.Write("Escolha a opção (1 ou 2): ");
            var input = Console.ReadLine();
            int.TryParse(input, out audioOutputChoice);
        }

        SelectedAudioOutputType = (AudioOutputType)audioOutputChoice;

        if (SelectedAudioOutputType == AudioOutputType.Speaker)
        {
            Console.WriteLine("\n=== SELEÇÃO DE DISPOSITIVO DE SAÍDA ===");
            Console.WriteLine("Listando dispositivos de saída (alto-falantes/fones) disponíveis:\n");

            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            if (devices.Count == 0)
            {
                Console.WriteLine("Erro: Nenhum dispositivo de saída encontrado!");
                Environment.Exit(1);
            }

            int index = 1;
            foreach (var device in devices)
            {
                Console.WriteLine($"{index}: {device.FriendlyName}");
                Console.WriteLine($"   ID: {device.ID}");
                Console.WriteLine();
                index++;
            }

            // Solicita ao usuário escolher o dispositivo pelo número
            int selectedIndex = -1;
            while (selectedIndex < 1 || selectedIndex > devices.Count)
            {
                Console.Write($"Digite o número do dispositivo (1-{devices.Count}): ");
                var input = Console.ReadLine();
                int.TryParse(input, out selectedIndex);
            }

            // Define o ID do dispositivo selecionado
            SelectedDeviceId = devices[selectedIndex - 1].ID;
            Console.WriteLine($"Dispositivo selecionado: {devices[selectedIndex - 1].FriendlyName}");
        }
        else
        {
            Console.WriteLine("\n=== LOCALIZANDO MICROFONE VIRTUAL ===");
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            Console.WriteLine("Dispositivos encontrados:");
            foreach (var device in devices)
            {
                Console.WriteLine($"  - {device.FriendlyName}");
            }

            // Procura pelo CABLE Input (VB-Audio)
            var cableDevice = devices.FirstOrDefault(d => d.FriendlyName.Contains("CABLE"));

            if (cableDevice != null)
            {
                SelectedDeviceId = cableDevice.ID;
                Console.WriteLine($"\n✓ Encontrado: {cableDevice.FriendlyName}");
                Console.WriteLine("Áudio será enviado APENAS para o VB-Audio Virtual Cable");
            }
            else
            {
                Console.WriteLine("\n✗ ERRO: Nenhum dispositivo CABLE encontrado!");
                Console.WriteLine("Dispositivos disponíveis: " + string.Join(", ", devices.Select(d => d.FriendlyName)));
                Environment.Exit(1);
            }
        }

        Console.WriteLine("\nIniciando reconhecimento e tradução...");
        Console.WriteLine("Falando português... ouvindo continuamente\n");

        try
        {
            var speechTranslationConfig = ConfigureSpeechTranslation();

            while (true)
            {
                await ProcessSpeechTranslation(speechTranslationConfig);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro inesperado: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("Programa encerrado.");
        }
    }

    private static SpeechTranslationConfig ConfigureSpeechTranslation()
    {
        var config = SpeechTranslationConfig.FromSubscription(SubscriptionKey, ServiceRegion);
        config.SpeechRecognitionLanguage = RecognitionLanguage;
        config.AddTargetLanguage(TranslationTargetLanguage);
        config.VoiceName = VoiceName;
        return config;
    }

    private static async Task ProcessSpeechTranslation(SpeechTranslationConfig config)
    {
        Console.WriteLine("[1/4] Criando áudio input com captura WAV...");

        // Criar arquivo temporário para salvar áudio WAV
        TempAudioPath = Path.Combine(Path.GetTempPath(), $"audio_{Guid.NewGuid()}.wav");

        // Iniciar captura de áudio em background
        var captureTask = CaptureAudioToWavAsync(TempAudioPath);

        // Pequena pausa para garantir que a captura começou
        await Task.Delay(100);

        using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();

        Console.WriteLine("[2/4] Criando translator...");
        using var translator = new TranslationRecognizer(config, audioConfig);

        TranslationRecognitionResult? capturedResult = null;
        object resultLock = new object();

        translator.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.TranslatedSpeech)
            {
                lock (resultLock)
                {
                    capturedResult = e.Result;
                }
                Console.WriteLine("[3/4] Áudio reconhecido!");
            }
        };

        translator.Recognizing += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Result.Text))
            {
                Console.WriteLine($"[Detectando]: {e.Result.Text}");
            }
        };

        translator.Canceled += (s, e) =>
        {
            Console.WriteLine($"[ERRO] Reconhecimento cancelado: {e.Reason}");
            if (e.Result.Reason == ResultReason.Canceled)
            {
                var cancellation = CancellationDetails.FromResult(e.Result);
                Console.WriteLine($"Detalhes: {cancellation.ErrorDetails}");
            }
        };

        // Inicia reconhecimento contínuo
        Console.WriteLine("Ouvindo...");
        await translator.StartContinuousRecognitionAsync();

        // Aguarda até capturar algo
        int timeout = 0;
        while (capturedResult == null && timeout < 300) // 30 segundos max
        {
            await Task.Delay(100);
            timeout++;
        }

        // Para o reconhecimento
        Console.WriteLine("[3b/4] Parando reconhecimento...");
        await translator.StopContinuousRecognitionAsync();

        lock (resultLock)
        {
            if (capturedResult != null)
            {
                HandleRecognitionResult(capturedResult);
            }
        }
    }

    private static async Task CaptureAudioToWavAsync(string outputPath)
    {
        try
        {
            using (var waveInEvent = new WaveInEvent())
            {
                waveInEvent.DeviceNumber = 0; // Usar dispositivo padrão
                waveInEvent.WaveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, mono

                using (var writer = new WaveFileWriter(outputPath, waveInEvent.WaveFormat))
                {
                    waveInEvent.DataAvailable += (s, e) =>
                    {
                        writer.Write(e.Buffer, 0, e.BytesRecorded);
                    };

                    waveInEvent.RecordingStopped += (s, e) =>
                    {
                        writer.Dispose();
                    };

                    waveInEvent.StartRecording();

                    // Registrar por até 30 segundos ou enquanto houver reconhecimento
                    await Task.Delay(30000);
                    waveInEvent.StopRecording();
                }
            }

            Console.WriteLine($"✓ Áudio capturado em: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao capturar áudio WAV: {ex.Message}");
        }
    }

    private static void HandleRecognitionResult(TranslationRecognitionResult result)
    {
        if (result.Reason == ResultReason.TranslatedSpeech)
        {
            Console.WriteLine($"Reconhecido: {result.Text}");
            Console.WriteLine($"Traduzido: {result.Translations[TranslationTargetLanguage]}");

            SynthesizeTranslatedSpeech(result.Translations[TranslationTargetLanguage]);
        }
        else if (result.Reason == ResultReason.NoMatch)
        {
            Console.WriteLine("Nenhuma fala foi reconhecida.");
        }
        else if (result.Reason == ResultReason.Canceled)
        {
            var cancellation = CancellationDetails.FromResult(result);
            Console.WriteLine($"CANCELADO: Motivo={cancellation.Reason}");
        }
    }

    private static void SynthesizeTranslatedSpeech(string translatedText)
    {
        var speechConfig = SpeechConfig.FromSubscription(SubscriptionKey, ServiceRegion);
        speechConfig.SpeechSynthesisLanguage = SynthesisLanguage;
        speechConfig.SpeechSynthesisVoiceName = VoiceName;

        try
        {
            AudioConfig audioConfig;

            if (SelectedAudioOutputType == AudioOutputType.Speaker)
            {
                audioConfig = AudioConfig.FromSpeakerOutput(SelectedDeviceId);
                Console.WriteLine($"[4/4] Enviando áudio para o alto-falante...");
            }
            else
            {
                audioConfig = AudioConfig.FromSpeakerOutput(SelectedDeviceId);
                Console.WriteLine($"[4/4] Enviando áudio para o VB-Audio Virtual Cable ({SelectedDeviceId})...");
            }

            using (audioConfig)
            using (var synthesizerVirtual = new SpeechSynthesizer(speechConfig, audioConfig))
            {
                string ssml;

                // Tentar usar Voice Conversion se temos áudio capturado
                if (!string.IsNullOrEmpty(TempAudioPath) && File.Exists(TempAudioPath))
                {
                    try
                    {
                        Console.WriteLine("✓ Usando Voice Conversion para preservar emoções!");

                        // Servir arquivo WAV via HTTP local
                        string audioUrl = StartHttpServer(TempAudioPath);

                        if (!string.IsNullOrEmpty(audioUrl))
                        {
                            ssml = $@"
<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' 
       xmlns:mstts='http://www.w3.org/2001/mstts' xml:lang='{SynthesisLanguage}'>
  <voice name='{VoiceName}'>
    <mstts:voiceconversion url='{audioUrl}'/>
  </voice>
</speak>";
                        }
                        else
                        {
                            throw new Exception("Falha ao servir arquivo de áudio");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Voice Conversion falhou ({ex.Message}), usando síntese normal...");
                        string style = DetermineEmotionalStyle(translatedText);

                        ssml = $@"
<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' 
       xmlns:mstts='http://www.w3.org/2001/mstts' xml:lang='{SynthesisLanguage}'>
  <voice name='{VoiceName}'>
    <mstts:express-as style='{style}' styledegree='1.5'>
      <prosody rate='1.0' pitch='0Hz' volume='100'>{translatedText}</prosody>
    </mstts:express-as>
  </voice>
</speak>";
                    }
                }
                else
                {
                    // Fallback: usar estilos emocionais
                    Console.WriteLine("⚠️ Usando síntese com detecção de estilos...");
                    string style = DetermineEmotionalStyle(translatedText);

                    ssml = $@"
<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' 
       xmlns:mstts='http://www.w3.org/2001/mstts' xml:lang='{SynthesisLanguage}'>
  <voice name='{VoiceName}'>
    <mstts:express-as style='{style}' styledegree='1.5'>
      <prosody rate='1.0' pitch='0Hz' volume='100'>{translatedText}</prosody>
    </mstts:express-as>
  </voice>
</speak>";
                }

                Console.WriteLine("✓ Áudio sintetizado com sucesso!");
                synthesizerVirtual.SpeakSsmlAsync(ssml).Wait();
                Console.WriteLine("✓ Áudio enviado! Voltando a ouvir...\n");

                // Limpar arquivo temporário
                if (!string.IsNullOrEmpty(TempAudioPath) && File.Exists(TempAudioPath))
                {
                    try { File.Delete(TempAudioPath); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ ERRO ao enviar áudio: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }

    private static string StartHttpServer(string filePath)
    {
        try
        {
            var listener = new HttpListener();
            string port = "8888";
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            string fileName = Path.GetFileName(filePath);
            string url = $"http://localhost:{port}/{fileName}";

            // Processar requisição em background
            _ = Task.Run(() =>
            {
                try
                {
                    var context = listener.GetContext();
                    var response = context.Response;

                    if (File.Exists(filePath))
                    {
                        byte[] buffer = File.ReadAllBytes(filePath);
                        response.ContentLength64 = buffer.Length;
                        response.ContentType = "audio/wav";
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                    }

                    response.OutputStream.Close();
                    listener.Stop();
                }
                catch { }
            });

            return url;
        }
        catch
        {
            return "";
        }
    }

    private static string DetermineEmotionalStyle(string text)
    {
        string lower = text.ToLower();

        // Palavras-chave para detectar emoções
        if (lower.Contains("haha") || lower.Contains("hehe") || lower.Contains("laugh") || lower.Contains("funny") || lower.Contains("lol"))
            return "cheerful";

        if (lower.Contains("sorry") || lower.Contains("apologies") || lower.Contains("apologize"))
            return "empathetic";

        if (lower.Contains("amazing") || lower.Contains("wonderful") || lower.Contains("great") || lower.Contains("excellent"))
            return "excited";

        if (lower.Contains("please") || lower.Contains("help") || lower.Contains("need") || lower.Contains("could"))
            return "friendly";

        if (lower.Contains("calm") || lower.Contains("relax") || lower.Contains("cool") || lower.Contains("peaceful"))
            return "calm";

        // Padrão: chat natural e conversacional
        return "chat";
    }
}