namespace Games.Leaderboard.Domain.Models;

public sealed record MultiChatLeaderboard(IReadOnlyList<ChatLeaderboard> Chats);
