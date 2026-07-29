using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public interface IModerationRateLimitStore
{
    ValueTask<ModerationRateObservation> RecordAsync(
        NormalizedMessage message,
        TimeSpan window,
        CancellationToken cancellationToken = default);
}
