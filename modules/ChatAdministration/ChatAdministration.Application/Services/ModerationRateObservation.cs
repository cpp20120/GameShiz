using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed record ModerationRateObservation(
    RateLimitSnapshot RateLimits,
    ModerationHistorySummary History);
