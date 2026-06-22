namespace Games.Meta.Infrastructure.Catalog;

internal sealed class QuestDefinition
{
    public string Id { get; set; } = "";
    public string Period { get; set; } = "daily";
    public string Kind { get; set; } = "play";
    public List<string> Tags { get; set; } = [];
    public List<string> GameKeys { get; set; } = [];
    public List<int> Targets { get; set; } = [];
    public string Rarity { get; set; } = "common";
    public string Cluster { get; set; } = "core";
    public int MinLevel { get; set; }
    public int MinGamesPlayed { get; set; }
    public long MinTotalStaked { get; set; }
    public List<long> MinStakes { get; set; } = [];
    public List<long> MaxStakes { get; set; } = [];
    public List<long> MinPayouts { get; set; } = [];
    public List<long> MinProfits { get; set; } = [];
    public List<decimal> MinMultipliers { get; set; } = [];
    public long RewardXp { get; set; }
    public long RewardCoins { get; set; }
    public List<string> Titles { get; set; } = [];
    public List<string> Descriptions { get; set; } = [];
}
