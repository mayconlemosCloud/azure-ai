using Microsoft.CognitiveServices.Speech.Translation;

namespace TraducaoRealtime.Interfaces;

/// <summary>
/// Interface para abstrair a implementação de speech (reconhecimento e síntese).
/// Permite trocar entre Azure, Google, OpenAI, etc sem alterar a regra de negócio.
/// </summary>
public interface ISpeechProvider
{
    /// <summary>
    /// Inicia reconhecimento de fala contínuo
    /// </summary>
    Task StartContinuousRecognitionAsync(
        Action<string> onRecognizing,
        Func<string, Task> onRecognized,
        Action<string> onError,
        CancellationToken cancellationToken);

    /// <summary>
    /// Para reconhecimento de fala
    /// </summary>
    Task StopContinuousRecognitionAsync();

    /// <summary>
    /// Sintetiza texto em áudio e reproduz
    /// </summary>
    Task SynthesizeAndPlayAsync(
        string text,
        string outputDevice,
        CancellationToken cancellationToken);
}
