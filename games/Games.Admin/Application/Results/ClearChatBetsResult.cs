using BotFramework.Host;
using BotFramework.Sdk;
using Games.Darts;

namespace Games.Admin;

public sealed record ClearChatBetsResult(int ClearedCount, int TotalRefunded);
