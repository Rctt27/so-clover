using SoClover.Eval.Calibration;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Épinglage littéral des invariants historiques. Les manifestes ci-dessous sont la première ligne,
/// copiée à l'octet près, de deux artefacts de mesure réels de <c>eval/runs/</c> (gitignorés, donc
/// absents d'un clone neuf) : le hash8 et l'empreinte décodeur qu'ils portent dans leur nom de
/// fichier et dans <c>eval/LEDGER.md</c> doivent se recalculer à l'identique, quelle que soit
/// l'évolution des records (provenance du prompt ajoutée en fin de record, notamment).
/// </summary>
public class HistoricalInvariantsTests
{
    // Première ligne de eval/runs/20260728-v5-google-gemma-4-12b-qat-d79a63b9.jsonl
    private const string RunManifestLine = """
        {"kind":"manifest","runId":"20260728-v5-google-gemma-4-12b-qat-d79a63b9","stage":"generate","createdAtUtc":"2026-07-28T19:19:20.5432608Z","benchFile":"eval/boards.dev.jsonl","benchHash":"416b819a41a1","promptFile":"C:/Users/FlowUP/RiderProjects/so-clover/SoClover.Eval/bin/Debug/net9.0/Infrastructure/AI/Prompts/fr/board-clues-per-direction.md","promptVersion":5,"generationMode":"PerDirection","reasoningEnabled":false,"provider":"OpenAI","baseUrl":"http://localhost:1234/v1","modelId":"google/gemma-4-12b-qat","modelSnapshotDate":"2026-07-28","providerModelListHash":"45141160fc31","temperature":1,"topP":0.95,"maxOutputTokens":4096,"maxRetries":0,"language":"Français_OFF","harnessVersion":1,"operatorNotes":"gemma-4-12b-qat thinking OFF, LM Studio JIT"}
        """;

    // Première ligne de eval/runs/20260728-v5-google-gemma-4-12b-qat-d79a63b9.9a829dc206d2.decoded.jsonl
    private const string DecodeManifestLine = """
        {"kind":"manifest","decodeRunId":"20260728-v5-google-gemma-4-12b-qat-d79a63b9+decode-20260804152143","createdAtUtc":"2026-08-04T15:21:43.3193244Z","generatorRunId":"20260728-v5-google-gemma-4-12b-qat-d79a63b9","benchFile":"eval/boards.dev.jsonl","benchHash":"416b819a41a1","provider":"OpenAI","baseUrl":"http://localhost:1234/v1","modelId":"qwen/qwen3-8b","modelSnapshotDate":"2026-08-04","providerModelListHash":"45141160fc31","temperature":0.3,"topP":null,"maxOutputTokens":512,"cluePromptFile":"C:/Users/FlowUP/RiderProjects/so-clover/SoClover.Eval/bin/Debug/net9.0/Decoder/Prompts/fr/decode-clue.md","cluePromptVersion":2,"boardPromptFile":"C:/Users/FlowUP/RiderProjects/so-clover/SoClover.Eval/bin/Debug/net9.0/Decoder/Prompts/fr/decode-board.md","boardPromptVersion":1,"decodesPerClue":3,"harnessVersion":1,"operatorNotes":null}
        """;

    [Fact]
    public void Le_manifeste_historique_d79a63b9_redonne_son_hash8()
    {
        var manifest = EvalJson.Deserialize<RunManifest>(RunManifestLine);

        Assert.Equal("d79a63b9", RunFile.ComputeHash8(manifest));
    }

    [Fact]
    public void Le_manifeste_de_decodage_historique_redonne_l_empreinte_9a829dc206d2()
    {
        var manifest = EvalJson.Deserialize<DecodeManifest>(DecodeManifestLine);

        Assert.Equal("9a829dc206d2", DecoderFingerprint.FromManifest(manifest));
    }

    [Fact]
    public void Un_manifeste_de_calibration_avec_provenance_du_prompt_clue_se_relit_a_l_identique()
    {
        var manifest = new CalibrationManifest(
            Kind: "manifest",
            CalibrationId: "calibration-20260805",
            CreatedAtUtc: new DateTime(2026, 8, 5, 10, 0, 0, DateTimeKind.Utc),
            ComparisonsFile: "eval/human/comparisons.jsonl",
            BenchFile: "eval/boards.dev.jsonl",
            BenchHash: "416b819a41a1",
            CoupleAndAnchorCount: 40,
            ClueCount: 120,
            DecoderFingerprint: "9a829dc206d2",
            Provider: "OpenAI",
            BaseUrl: "http://localhost:1234/v1",
            ModelId: "qwen/qwen3-8b",
            ModelSnapshotDate: "2026-08-05",
            ProviderModelListHash: "45141160fc31",
            Temperature: 0.3,
            TopP: null,
            MaxOutputTokens: 512,
            CluePromptFile: "eval/prompts/resolved/aaaaaaaaaaaa/fr/decode-clue.md",
            CluePromptVersion: 2,
            DecodesPerClue: 3,
            Epsilon: 0.05,
            HarnessVersion: 1,
            OperatorNotes: null,
            Quantization: "Q4_K_M",
            LoadedContextLength: 16000,
            CluePrompt: LangfuseFixtures.LangfuseClue);

        var json = EvalJson.Serialize(manifest);

        Assert.Contains("\"cluePrompt\":", json);
        Assert.Equal(manifest, EvalJson.Deserialize<CalibrationManifest>(json));
    }
}
