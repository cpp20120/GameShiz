using BotFramework.Contracts.Identity;
using BotFramework.Host.Contracts.Economics;

namespace CasinoShiz.AdminBff;

internal sealed record PlayerAggregateResponse(PlayerIdentity? Identity, WalletAccount? Wallet);
