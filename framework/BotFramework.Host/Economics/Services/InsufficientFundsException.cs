using System.Globalization;

namespace BotFramework.Host.Economics.Services;

public sealed class InsufficientFundsException : InvalidOperationException
{
    public InsufficientFundsException()
    {
    }

    public InsufficientFundsException(string? message) : base(message)
    {
    }

    public InsufficientFundsException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    public InsufficientFundsException(long userId, long balanceScopeId, int requested, int available)
        : base(string.Create(CultureInfo.InvariantCulture, $"User {userId} scope {balanceScopeId} has insufficient funds: requested {requested}, available {available}"))
    {
        UserId = userId;
        BalanceScopeId = balanceScopeId;
        Requested = requested;
        Available = available;
    }

    public long UserId { get; }
    public long BalanceScopeId { get; }
    public int Requested { get; }
    public int Available { get; }
}
