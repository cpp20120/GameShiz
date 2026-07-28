using BotFramework.Host.Admin.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Admin.Execution;
using Dapper;
using Games.Admin.Infrastructure.Models;

namespace Games.Admin.Application.Effects;

public sealed record ClearChatBetsAdminEffect(long ChatId) : IAdminEffect;
