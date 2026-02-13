namespace TraducaoRealtime.Utils;

/// <summary>
/// Construtor de SSML (Speech Synthesis Markup Language)
/// para geração de fala com prosódia natural
/// </summary>
public class SSMLBuilder
{
    /// <summary>
    /// Cria SSML com prosódia adaptada ao idioma
    /// </summary>
    public static string CreateSSML(string text, string language, string voiceName)
    {
        // Escapar caracteres especiais para XML
        text = System.Security.SecurityElement.Escape(text);

        // Configurações de prosódia por idioma
        var (rate, pitch) = GetProsodySettings(language);

        return $"<speak version='1.0' xml:lang='{language}' xmlns='http://www.w3.org/2001/10/synthesis'>" +
               $"<voice name='{voiceName}'>" +
               $"<prosody rate='{rate}' pitch='{pitch}'>" +
               $"{text}" +
               $"</prosody></voice></speak>";
    }

    /// <summary>
    /// Retorna configurações de prosódia (rate e pitch) por idioma
    /// </summary>
    private static (string rate, string pitch) GetProsodySettings(string language)
    {
        return language?.Substring(0, 2).ToLower() switch
        {
            "pt" => ("0.95", "2%"),    // Português
            "en" => ("1.0", "0%"),     // Inglês
            "es" => ("0.98", "1%"),    // Espanhol
            "fr" => ("0.96", "3%"),    // Francês
            "de" => ("0.97", "1%"),    // Alemão
            "it" => ("1.0", "1%"),     // Italiano
            "ja" => ("1.0", "0%"),     // Japonês
            _ => ("1.0", "0%")         // Padrão
        };
    }

    /// <summary>
    /// Retorna voz neural padrão por idioma se não especificada
    /// </summary>
    public static string GetDefaultVoice(string language)
    {
        return language switch
        {
            "pt-BR" => "pt-BR-BrendaNeural",
            "pt-PT" => "pt-PT-FernandaNeural",
            "en-US" => "en-US-AriaNeural",
            "es-ES" => "es-ES-ElviraNeural",
            "fr-FR" => "fr-FR-DeniseNeural",
            "de-DE" => "de-DE-KatjaNeural",
            "it-IT" => "it-IT-IsabellaNeural",
            "ja-JP" => "ja-JP-NanamiNeural",
            _ => "pt-BR-BrendaNeural"
        };
    }
}
