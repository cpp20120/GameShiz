using BotFramework.Sdk.Admin.Execution;

namespace BotFramework.Sdk.Admin.Effects;

/// <summary>Atomically compensates a ledger row and records the compensation.</summary>
public sealed record LedgerRevertAdminEffect(long LedgerId) : IAdminEffect;