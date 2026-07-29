using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed record SettingsCallbackState(
    string Token,
    ChatId ChatId,
    string Key,
    string Value,
    DateTimeOffset ExpiresAt);
