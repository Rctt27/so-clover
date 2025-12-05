namespace SoClover.UseCases.Abstractions;

/// <summary>
/// Indicates who is invoking a use case: a regular client action or the internal system/background process.
/// Use this instead of sentinel values like Guid.Empty to avoid privilege escalation risks.
/// </summary>
public enum InvocationOrigin
{
    Client = 0,
    System = 1
}
