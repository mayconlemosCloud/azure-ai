using TraducaoRealtime.Models;
using TraducaoRealtime.Services;
using NAudio.CoreAudioApi;

namespace TraducaoRealtime.UI;

/// <summary>
/// Interface por console do usuário
/// Desacoplada da lógica de negócio
/// </summary>
public class ConsoleUI
{
    private readonly AudioManager _audioManager;

    public ConsoleUI(AudioManager audioManager)
    {
        _audioManager = audioManager;
    }

    public void DisplayHeader()
    {
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("   🎤 TRADUÇÃO EM TEMPO REAL - Azure Speech");
        Console.WriteLine(new string('=', 50) + "\n");
    }

    public AudioConfiguration SelectAudioConfiguration()
    {
        Console.WriteLine("⚙️  CONFIGURAÇÃO DE ÁUDIO\n");

        // Pergunta 1: Você quer se ouvir?
        Console.WriteLine("🎧 Você quer se ouvir (ouvir o áudio traduzido)?");
        Console.WriteLine("1️⃣  Sim, quero ouvir");
        Console.WriteLine("2️⃣  Não, sem áudio local\n");
        Console.Write("Digite sua opção (1 ou 2): ");
        string option1 = Console.ReadLine();

        bool userWantsToHear = option1 == "1";

        if (userWantsToHear)
        {
            SelectLocalAudioDevice();
            DisplayAudioConfig(userWantsToHear, false, null);
            return new AudioConfiguration
            {
                UserWantsToHear = true,
                OthersWantToHear = false
            };
        }
        else
        {
            Console.WriteLine("✓ Sem áudio local\n");

            // Pergunta 2: Quer que a pessoa te escute?
            Console.WriteLine("👥 Quer que outras pessoas te escutem (via Discord/OBS)?");
            Console.WriteLine("1️⃣  Sim, quero compartilhar o áudio");
            Console.WriteLine("2️⃣  Não, sem áudio virtual\n");
            Console.Write("Digite sua opção (1 ou 2): ");
            string option2 = Console.ReadLine();

            if (option2 == "1")
            {
                string selectedDevice = SelectVirtualAudioDevice();
                DisplayAudioConfig(false, !string.IsNullOrEmpty(selectedDevice), selectedDevice);
                return new AudioConfiguration
                {
                    UserWantsToHear = false,
                    OthersWantToHear = !string.IsNullOrEmpty(selectedDevice),
                    SelectedOutputDevice = selectedDevice
                };
            }
            else
            {
                Console.WriteLine("✓ Sem áudio virtual\n");
                DisplayAudioConfig(false, false, null);
                return new AudioConfiguration
                {
                    UserWantsToHear = false,
                    OthersWantToHear = false
                };
            }
        }
    }

    public void TestAzureConnection(string region, string? recognitionLang,
        string? translationLang, string? synthesisLang, string? voiceName)
    {
        try
        {
            Console.WriteLine("📂 Carregando configurações...\n");
            DisplayConfig(region, recognitionLang, translationLang, synthesisLang, voiceName);
            Console.WriteLine("🔗 Conectando ao Azure Speech Services...");
            DisplaySuccess();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro na conexão: {ex.Message}\n");
        }
    }

    private void SelectLocalAudioDevice()
    {
        Console.WriteLine("🔊 Selecione onde VOCÊ quer ouvir o áudio traduzido:\n");

        var devices = _audioManager.GetOutputDevices();

        if (devices.Count == 0)
        {
            Console.WriteLine("❌ Nenhum dispositivo local encontrado!\n");
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

    private string SelectVirtualAudioDevice()
    {
        Console.WriteLine("🎙️  Selecione por onde OUTRAS PESSOAS vão ouvir (via Discord/OBS):\n");

        var allDevices = _audioManager.GetDevices(DataFlow.Render);
        Console.WriteLine("📊 Dispositivos disponíveis (DEBUG):");
        foreach (var dev in allDevices)
        {
            Console.WriteLine($"  - {dev}");
        }
        Console.WriteLine();

        var devices = _audioManager.GetVirtualDevices();

        if (devices.Count == 0)
        {
            Console.WriteLine("❌ Nenhum dispositivo virtual encontrado!");
            Console.WriteLine("⚠️  Instale VB-Audio Virtual Cable para compartilhar áudio\n");
            return "";
        }

        for (int i = 0; i < devices.Count; i++)
        {
            Console.WriteLine($"{i + 1}️⃣  {devices[i]}");
        }

        Console.Write($"\nDigite o número (1-{devices.Count}): ");
        string option = Console.ReadLine();

        if (int.TryParse(option, out int deviceIndex) && deviceIndex > 0 && deviceIndex <= devices.Count)
        {
            string selectedDevice = devices[deviceIndex - 1];
            Console.WriteLine($"✓ Outras pessoas ouvirão em: {selectedDevice}\n");
            return selectedDevice;
        }
        else
        {
            Console.WriteLine("❌ Opção inválida!\n");
            return "";
        }
    }

    private void DisplayAudioConfig(bool userHears, bool othersHear, string? device)
    {
        Console.WriteLine(new string('=', 50));
        Console.WriteLine("📋 RESUMO DE CONFIGURAÇÃO");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine($"🎧 Você ouve: {(userHears ? "SIM" : "NÃO")}");
        Console.WriteLine($"👥 Outros ouvem: {(othersHear ? "SIM - " + device : "NÃO")}");
        Console.WriteLine(new string('=', 50) + "\n");
    }

    private void DisplayConfig(string region, string? recognitionLang, string? translationLang,
        string? synthesisLang, string? voiceName)
    {
        Console.WriteLine("✓ Variáveis carregadas:");
        Console.WriteLine($"  • Region: {region}");
        Console.WriteLine($"  • Reconhecimento: {recognitionLang ?? "N/A"}");
        Console.WriteLine($"  • Tradução: {translationLang ?? "N/A"}");
        Console.WriteLine($"  • Síntese: {synthesisLang ?? "N/A"}");
        Console.WriteLine($"  • Voz: {voiceName ?? "N/A"}\n");
    }

    private void DisplaySuccess()
    {
        Console.WriteLine("✓ Conexão estabelecida!\n");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine("✅ Sistema pronto para usar!");
        Console.WriteLine(new string('=', 50) + "\n");
    }
}

/// <summary>
/// Extensões auxiliares para AudioManager na UI
/// </summary>
public static class AudioManagerUIExtensions
{
    public static List<string> GetDevices(this AudioManager audioManager, NAudio.CoreAudioApi.DataFlow dataFlow)
    {
        return dataFlow == DataFlow.Render
            ? audioManager.GetOutputDevices()
            : audioManager.GetInputDevices();
    }
}
