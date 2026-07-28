using System.Security.Cryptography;
using System.Text;
using BotFramework.Sdk.Execution;
using Games.Pick.Domain.Events;

namespace Games.Pick.Application.Execution;

public sealed record PickWalletCreditEffect(long UserId, long ChatId, int Amount, string Reason) : IGameEffect;
