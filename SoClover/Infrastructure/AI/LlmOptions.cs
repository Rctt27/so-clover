namespace SoClover.Infrastructure.AI;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public LlmProvider Provider { get; set; } = LlmProvider.OpenAI;
    public string BaseUrl { get; set; } = "http://localhost:1234/v1";
    public string ApiKey { get; set; } = "lm-studio";
    public string DefaultModel { get; set; } = "lmstudio-community/Meta-Llama-3.1-8B-Instruct-GGUF";
    public double DefaultTemperature { get; set; } = 0.7;

    // Sampling nucleus (top_p). Null = défaut du provider. Reco modèles reasoning Mistral : 0.95.
    public double? TopP { get; set; } = null;

    // Plafond de tokens de complétion (max_tokens). Null = défaut du provider. Utile pour borner les
    // runs de raisonnement et éviter qu'un modèle reasoning ne s'emballe.
    public int? MaxOutputTokens { get; set; } = null;

    public int MaxRetries { get; set; } = 2;
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxConcurrency { get; set; } = 4;
    public int MaxCallsPerGame { get; set; } = 200;

    // Granularité des appels LLM pour la génération d'indices IA. PerBoard (défaut) = 1 appel couvrant
    // toutes les directions restantes du board (comportement historique). PerDirection = 1 appel par
    // direction (jusqu'à 4 appels/board), plus fiable pour les modèles reasoning locaux. Axe orthogonal
    // à ReasoningEnabled. Surcharge : LLM__GENERATIONMODE.
    public AiClueGenerationMode GenerationMode { get; set; } = AiClueGenerationMode.PerBoard;

    // Mode reasoning natif (cf. docs CLAUDE.md). Défaut OFF : le prompt prescriptif baseline est utilisé
    // et aucun paramètre natif n'est passé. Quand ON, le prompt advisory est activé et le
    // IReasoningRequestConfigurator injecte les paramètres natifs du provider.
    public bool ReasoningEnabled { get; set; } = false;

    // OpenAI/o-series/LM Studio : "low" | "medium" | "high". Null = défaut du provider.
    public string? ReasoningEffort { get; set; } = null;

    // Anthropic extended thinking : budget de tokens de raisonnement. Null = pas de budget explicite.
    public int? ThinkingBudgetTokens { get; set; } = null;

    // Préambule système de raisonnement propre au modèle (agnostique de la tâche/langue). C'est le
    // DÉCLENCHEUR du reasoning natif : certains modèles (ex. Mistral Ministral-Reasoning) n'activent
    // leur raisonnement QUE si leur system prompt officiel est présent. À ne pas confondre avec le
    // prompt métier reasoning (board-clues-per-direction.reasoning.md), qui décrit la tâche So Clover.
    // Renseigner ici le chemin vers ce fichier (ex. SYSTEM_PROMPT.txt du modèle) ; son contenu est
    // préfixé au system prompt quand le mode reasoning est actif. Null/vide = rien injecté.
    public string? ReasoningSystemPromptPathEnabler { get; set; } = null;
}