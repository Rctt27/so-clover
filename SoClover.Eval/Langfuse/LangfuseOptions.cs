namespace SoClover.Eval.Langfuse;

/// <summary>D'où le harnais tire ses prompts. Voir spec §6.5 : jamais de repli silencieux de l'un à l'autre.</summary>
public enum PromptSource
{
    File,
    Langfuse,
}

/// <summary>
/// Section <c>Langfuse</c> d'<c>evalsettings.json</c>, surchargeable par <c>LANGFUSE__*</c>.
/// Les clés ne vivent jamais dans le fichier committé : variables d'environnement ou
/// <c>evalsettings.local.json</c>.
/// </summary>
public sealed class LangfuseOptions
{
    public string BaseUrl { get; set; } = "http://localhost:3000";
    public string? PublicKey { get; set; }
    public string? SecretKey { get; set; }
    public PromptSource PromptSource { get; set; } = PromptSource.Langfuse;
    public string PromptLabel { get; set; } = "production";

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(SecretKey);
}
