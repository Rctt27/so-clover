using System.Text.Encodings.Web;
using System.Text.Json;
using SoClover.Eval.Human;

namespace SoClover.Eval.Web;

/// <summary>
/// Assemble le kit hors ligne <b>par substitution d'une seule région</b> de <c>guess.html</c> —
/// celle entre <c>// transport:start</c> et <c>// transport:end</c>, qui ne contient que
/// <c>api()</c> et <c>post()</c>. Tout le reste de la page (CSS, <c>render()</c>,
/// <c>toggle()</c>, la touche Entrée) est recopié <b>octet pour octet</b>.
/// <para>
/// C'est la garde 1 appliquée à la séance E : la seule variable entre D et E doit être la
/// personne. Une page réécrite comparerait deux présentations en même temps que deux devineurs ;
/// une substitution vérifiée par test ne compare que ce qu'on veut comparer.
/// </para>
/// </summary>
public static class GuessKitPage
{
    public const string TransportStart = "// transport:start";
    public const string TransportEnd = "// transport:end";

    private const string PayloadPlaceholder = "/*{{KIT_PAYLOAD}}*/null";

    /// <summary>
    /// Encodeur <b>strict</b>, contrairement à <c>EvalJson.Options</c> : la charge utile est
    /// inscrite dans un <c>&lt;script&gt;</c>, où un <c>&lt;</c> littéral pourrait fermer la
    /// balise. Les non-ASCII partent en <c>\uXXXX</c> — le kit reste lisible par n'importe quel
    /// navigateur quel que soit l'encodage supposé du fichier local.
    /// </summary>
    private static readonly JsonSerializerOptions EmbedOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Default,
        WriteIndented = false,
    };

    public static string Build(string pageHtml, GuessKitPayload payload)
    {
        var (head, tail) = SplitTransport(pageHtml);
        var newline = pageHtml.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        var shim = OfflineTransport.Replace(
            PayloadPlaceholder,
            JsonSerializer.Serialize(payload, EmbedOptions),
            StringComparison.Ordinal);

        return head + shim.ReplaceLineEndings(newline) + newline + tail;
    }

    /// <summary>
    /// Coupe la page en deux moitiés invariantes : ce qui précède (marqueur d'ouverture inclus) et
    /// ce qui suit (marqueur de fermeture inclus). Un test compare ces deux moitiés entre
    /// <c>guess.html</c> et le kit — c'est là que vit la garantie, pas dans une relecture humaine.
    /// </summary>
    public static (string Head, string Tail) SplitTransport(string pageHtml)
    {
        var start = pageHtml.IndexOf(TransportStart, StringComparison.Ordinal);
        if (start < 0)
            throw new InvalidOperationException(
                $"Marqueur « {TransportStart} » absent de la page : le kit ne peut pas être " +
                "assemblé par substitution, et une page réécrite à la main introduirait une " +
                "seconde variable dans la comparaison D ↔ E.");

        var end = pageHtml.IndexOf(TransportEnd, start, StringComparison.Ordinal);
        if (end < 0)
            throw new InvalidOperationException(
                $"Marqueur « {TransportEnd} » absent après « {TransportStart} ».");

        if (pageHtml.IndexOf(TransportStart, start + TransportStart.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException(
                $"Marqueur « {TransportStart} » présent plusieurs fois : région de transport ambiguë.");

        return (pageHtml[..(start + TransportStart.Length)], pageHtml[end..]);
    }

    /// <summary>
    /// Le transport hors ligne. Il réimplémente <c>api()</c> et <c>post()</c> avec <b>les mêmes
    /// contrats et les mêmes messages</b> que <see cref="HumanServer.BuildGuessApp"/> et
    /// <see cref="GuessingSession.SubmitGuess"/> : la page appelante ne peut pas faire la
    /// différence.
    /// <para>
    /// Ce qu'il ajoute et que la séance D n'avait pas : un bouton d'enregistrement (une séance
    /// perdue faute de bouton visible coûterait bien plus cher que ce seul écart de présentation),
    /// une reprise par <c>localStorage</c>, et un repli manuel si le navigateur refuse le
    /// téléchargement. Le score n'est <b>jamais</b> calculé ici.
    /// </para>
    /// </summary>
    private const string OfflineTransport =
        """

        // ── Transport hors ligne (séance E) ─────────────────────────────────────
        // Substitué au transport réseau par GuessKitPage — ce fichier n'ouvre aucune connexion.
        // Aucune réponse attendue n'y figure : le score est calculé à l'import, sur la machine de
        // l'opérateur.
        const KIT = /*{{KIT_PAYLOAD}}*/null;

        const STORAGE_KEY = "soclover-guess-kit-" + KIT.kitHash;
        let persistent = true;
        let downloadedCount = 0;

        function utcStamp() {
          const now = new Date();
          const pad = (n) => String(n).padStart(2, "0");
          return "" + now.getUTCFullYear() + pad(now.getUTCMonth() + 1) + pad(now.getUTCDate())
            + pad(now.getUTCHours()) + pad(now.getUTCMinutes()) + pad(now.getUTCSeconds());
        }

        function restore() {
          try {
            const raw = window.localStorage.getItem(STORAGE_KEY);
            if (raw) return JSON.parse(raw);
          } catch (error) {
            persistent = false;
          }
          return null;
        }

        let state = restore() || {
          sessionId: "e-" + utcStamp() + "-"
            + Math.floor(Math.random() * 65536).toString(16).padStart(4, "0"),
          startedAtUtc: new Date().toISOString(),
          answers: [],
        };

        function save() {
          if (!persistent) return;
          try {
            window.localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
          } catch (error) {
            persistent = false;
            warnVolatile();
          }
        }

        // ── Les deux fonctions que la page appelle ──────────────────────────────

        async function api(path) {
          if (path === "/api/next") return { status: 200, body: nextView() };
          return { status: 404, body: { message: "Route inconnue hors ligne : " + path } };
        }

        const post = (path, payload) => Promise.resolve(
          path === "/api/guess"
            ? submitGuess(payload)
            : { status: 404, body: { message: "Route inconnue hors ligne : " + path } });

        function nextView() {
          const done = state.answers.length;
          if (done >= KIT.items.length) {
            return { finished: true, presentedWords: null, clue: null, itemOrdinal: 0,
                     completedCount: done, totalCount: KIT.items.length };
          }
          return { finished: false, presentedWords: KIT.items[done].presentedWords,
                   clue: KIT.items[done].clue, itemOrdinal: done + 1,
                   completedCount: done, totalCount: KIT.items.length };
        }

        // Mêmes refus, mêmes messages, mêmes codes que GuessingSession.SubmitGuess.
        function submitGuess(request) {
          const done = state.answers.length;
          if (done >= KIT.items.length) {
            return { status: 409, body: { message: "Séance terminée.", finished: true } };
          }

          const picked = (request && request.picked) || [];
          const elapsedMs = (request && request.elapsedMs) || 0;

          if (elapsedMs < 0) return refuse("elapsedMs doit être ≥ 0.");
          if (picked.length !== 2) {
            return refuse("Il faut exactement deux mots, " + picked.length + " reçu(s).");
          }
          if (picked[0] === picked[1]) return refuse("Les deux mots doivent être différents.");

          const presented = KIT.items[done].presentedWords;
          for (const word of picked) {
            if (!presented.includes(word)) {
              return refuse("« " + word + " » n'est pas un mot de ce plateau.");
            }
          }

          state.answers.push({ kind: "kit-guess", itemIndex: done, picked, elapsedMs,
                               guessedAtUtc: new Date().toISOString() });
          save();
          refreshBar();
          return { status: 200, body: { ok: true } };
        }

        const refuse = (message) => ({ status: 400, body: { message } });

        // ── Enregistrement du résultat ──────────────────────────────────────────

        function resultText() {
          const header = {
            kind: "kit-manifest", benchHash: KIT.benchHash, seed: KIT.seed, runId: KIT.runId,
            kitHash: KIT.kitHash, harnessVersion: KIT.harnessVersion, itemCount: KIT.items.length,
            sessionId: state.sessionId, startedAtUtc: state.startedAtUtc,
            downloadedAtUtc: new Date().toISOString(), userAgent: navigator.userAgent,
          };
          return [header].concat(state.answers).map((o) => JSON.stringify(o)).join("\n") + "\n";
        }

        function download() {
          const text = resultText();
          try {
            const url = URL.createObjectURL(new Blob([text], { type: "application/x-ndjson" }));
            const anchor = document.createElement("a");
            anchor.href = url;
            anchor.download = "seance-e-" + KIT.kitHash + ".jsonl";
            document.body.appendChild(anchor);
            anchor.click();
            anchor.remove();
            setTimeout(() => URL.revokeObjectURL(url), 2000);
            downloadedCount = state.answers.length;
            refreshBar();
          } catch (error) {
            showFallback(text);
          }
        }

        // Repli si le navigateur refuse un téléchargement depuis file:// — le texte reste
        // récupérable à la main plutôt que perdu.
        function showFallback(text) {
          const box = document.createElement("textarea");
          box.value = text;
          box.style.cssText = "position:fixed;inset:2rem;z-index:20;background:var(--bg);"
            + "color:var(--fg);border:1px solid var(--target);border-radius:8px;padding:1rem;"
            + "font:13px/1.4 monospace";
          document.body.appendChild(box);
          box.focus();
          box.select();
          alert("Le téléchargement automatique a échoué. Copiez tout le texte affiché et "
            + "collez-le dans un fichier nommé seance-e-" + KIT.kitHash + ".jsonl");
        }

        function warnVolatile() {
          if (document.getElementById("volatile")) return;
          const banner = document.createElement("div");
          banner.id = "volatile";
          banner.textContent = "Ce navigateur refuse la sauvegarde locale : ne fermez pas cet "
            + "onglet, et enregistrez vos réponses régulièrement.";
          banner.style.cssText = "position:fixed;left:0;right:0;top:0;z-index:20;padding:.6rem 1rem;"
            + "background:#3a2a10;color:var(--target);border-bottom:1px solid var(--target)";
          document.body.appendChild(banner);
        }

        const bar = document.createElement("div");
        bar.style.cssText = "position:fixed;right:1rem;bottom:1rem;z-index:10";
        const saveButton = document.createElement("button");
        saveButton.style.cssText = "background:#232830;color:var(--fg);border:1px solid var(--target);"
          + "border-radius:6px;padding:.55rem 1rem;font:inherit;cursor:pointer";
        saveButton.addEventListener("click", download);
        bar.appendChild(saveButton);
        document.body.appendChild(bar);

        function refreshBar() {
          const done = state.answers.length;
          const pending = done > downloadedCount;
          const fini = done === KIT.items.length;

          saveButton.textContent =
            (pending || done === 0 ? "Enregistrer mes réponses" : "Réponses enregistrées")
            + " (" + done + " / " + KIT.items.length + ")";
          saveButton.style.opacity = pending ? "1" : ".5";

          // À la dernière direction, le bouton devient l'action évidente : c'est le seul moment où
          // une séance entière peut se perdre par inadvertance.
          saveButton.style.background = pending && fini ? "var(--target)" : "#232830";
          saveButton.style.color = pending && fini ? "var(--bg)" : "var(--fg)";
        }

        window.addEventListener("beforeunload", (event) => {
          if (state.answers.length > downloadedCount) {
            event.preventDefault();
            event.returnValue = "";
          }
        });

        if (!persistent) warnVolatile();
        refreshBar();
        """;
}
