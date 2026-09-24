namespace SoClover.Eval.Cli;

/// <summary>
/// Compteur de progression d'un verbe reprenable. Le rang affiché est celui de l'unité dans le
/// run entier (<c>[7/160]</c> après 6 unités déjà écrites), pas dans la session : sans quoi une
/// reprise affiche <c>[1/154]</c> et laisse croire que le run ne compte que 154 unités.
/// L'estimation, elle, extrapole le rythme de la session courante aux unités qui lui restent.
/// </summary>
public sealed record ResumeProgress(int Total, int AlreadyDone)
{
    public static ResumeProgress From(int total, int pending)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pending);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pending, total);
        return new ResumeProgress(total, total - pending);
    }

    private int Pending => Total - AlreadyDone;

    public string Counter(int doneThisSession) => $"[{AlreadyDone + doneThisSession}/{Total}]";

    public TimeSpan Remaining(int doneThisSession, TimeSpan elapsed) =>
        doneThisSession > 0
            ? TimeSpan.FromSeconds(elapsed.TotalSeconds / doneThisSession * (Pending - doneThisSession))
            : TimeSpan.Zero;
}
