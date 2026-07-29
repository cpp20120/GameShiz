namespace ChatAdministration.Application.Services;

public sealed record VerificationPersistenceResult(
    bool Applied,
    bool Duplicate);
