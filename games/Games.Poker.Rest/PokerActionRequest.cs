using BotFramework.Rest;
using Games.Poker.Application.Services;
using Games.Poker.Domain.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Games.Poker.Rest;

public sealed record PokerActionRequest(string Verb, int Amount = 0);
