using System.Data.Common;

namespace BotFramework.Contracts.Messaging;

/// <summary>
/// Database resources belonging to the current integration-message
/// transaction. Framework repositories may use these to make domain writes
/// and inbox completion atomic.
/// </summary>
public sealed class IntegrationInboxContext(
    DbConnection connection,
    DbTransaction transaction,
    IntegrationInboxMessage message)
{
    public DbConnection Connection { get; } = connection ?? throw new ArgumentNullException(nameof(connection));
    public DbTransaction Transaction { get; } = transaction ?? throw new ArgumentNullException(nameof(transaction));
    public IntegrationInboxMessage Message { get; } = message ?? throw new ArgumentNullException(nameof(message));
}
