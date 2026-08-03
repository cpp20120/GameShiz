using System.Data.Common;

namespace BotFramework.Contracts.Messaging;

/// <summary>Connection and transaction that must be shared by local writes.</summary>
public sealed class IntegrationTransactionContext(
    DbConnection connection,
    DbTransaction transaction)
{
    public DbConnection Connection { get; } = connection ?? throw new ArgumentNullException(nameof(connection));
    public DbTransaction Transaction { get; } = transaction ?? throw new ArgumentNullException(nameof(transaction));

    public static IntegrationTransactionContext From(IntegrationInboxContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new(context.Connection, context.Transaction);
    }
}
