namespace TraducaoRealtime.Interfaces;

/// <summary>
/// Interface para gerenciar dispositivos de áudio
/// </summary>
public interface IAudioManager
{
    /// <summary>
    /// Retorna lista de dispositivos de entrada
    /// </summary>
    List<string> GetInputDevices();

    /// <summary>
    /// Retorna lista de dispositivos de saída (locais)
    /// </summary>
    List<string> GetOutputDevices();

    /// <summary>
    /// Retorna lista de dispositivos virtuais
    /// </summary>
    List<string> GetVirtualDevices();

    /// <summary>
    /// Reproduz áudio em memória em um dispositivo específico
    /// </summary>
    Task PlayAudioFromMemoryAsync(MemoryStream audioStream, string deviceName);
}
