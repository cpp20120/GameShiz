using BotFramework.Rest;
using Games.Blackjack.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Games.Blackjack.Rest;

public sealed record BlackjackStartRequest(int Bet);
