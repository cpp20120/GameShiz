using BotFramework.Contracts.Messaging;
using BotFramework.Rest;
using Games.Dice.Contracts.Play;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Games.Dice.Rest;

public sealed record DiceRollRequest(int SlotValue);
