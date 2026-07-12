using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

internal sealed record WalletMutationResult(bool Applied, bool Rejected, WalletSnapshot Wallet);
