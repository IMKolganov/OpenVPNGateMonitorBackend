using DataGateMonitor.Models;

namespace DataGateMonitor.Services.Api.Auth.Registers;

/// <summary>
/// Result of registering an API client: persisted entity (secret hashed) plus one-time plaintext.
/// </summary>
public sealed class RegisteredClientApplication
{
    public required ClientApplication Application { get; init; }

    /// <summary>Plaintext secret returned only once to the caller; never persisted.</summary>
    public required string PlaintextClientSecret { get; init; }
}
