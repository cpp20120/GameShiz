namespace ChatAdministration.Domain.Models;

public enum EffectExecutionStatus
{
    Pending,
    Ready,
    Executing,
    Applied,
    FailedRetryable,
    FailedPermanent,
    Unknown,
    Cancelled,
    Compensated,
}
