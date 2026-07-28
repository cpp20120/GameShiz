using System.Security.Cryptography;
using System.Text.Json;
using BotFramework.Rendering;

namespace Games.Poker.Infrastructure.Rendering;

public sealed record PokerBoardRenderSpec(TableSnapshot Snapshot, string CultureCode = "ru");
