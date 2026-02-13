namespace TraducaoRealtime.Interfaces;

/// <summary>
/// Interface para ocultação do serviço de tradução
/// Permite trocar Azure Translator por Google Translate, etc
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Traduz texto do idioma origem para idioma destino
    /// </summary>
    Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage);
}
