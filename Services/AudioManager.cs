using TraducaoRealtime.Interfaces;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace TraducaoRealtime.Services;

/// <summary>
/// Gerencia dispositivos e reprodução de áudio
/// </summary>
public class AudioManager : IAudioManager
{
    private readonly Lazy<MMDeviceEnumerator> _deviceEnumerator;

    public AudioManager()
    {
        _deviceEnumerator = new Lazy<MMDeviceEnumerator>(() => new MMDeviceEnumerator());
    }

    public List<string> GetInputDevices()
    {
        return GetDevices(DataFlow.Capture);
    }

    public List<string> GetOutputDevices()
    {
        return GetDevices(DataFlow.Render).Where(d => !d.Contains("CABLE")).ToList();
    }

    public List<string> GetVirtualDevices()
    {
        return GetDevices(DataFlow.Render).Where(d => d.Contains("CABLE")).ToList();
    }

    public async Task PlayAudioFromMemoryAsync(MemoryStream audioStream, string deviceName)
    {
        try
        {
            var enumerator = _deviceEnumerator.Value;
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

    private List<string> GetDevices(DataFlow dataFlow)
    {
        var devices = new List<string>();

        try
        {
            var enumerator = _deviceEnumerator.Value;
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
