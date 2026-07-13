using Microsoft.EntityFrameworkCore;

namespace Games.Challenges.Infrastructure.Persistence;

public sealed class ChallengeDbContext(INpgsqlConnectionFactory connections) : DbContext
{
    public DbSet<ChallengeEntity> Challenges => Set<ChallengeEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseNpgsql(connections.Create().ConnectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var challenge = modelBuilder.Entity<ChallengeEntity>();
        challenge.ToTable("challenge_duels");
        challenge.HasKey(x => x.Id);
        challenge.Property(x => x.Id).HasColumnName("id");
        challenge.Property(x => x.ChatId).HasColumnName("chat_id");
        challenge.Property(x => x.ChallengerId).HasColumnName("challenger_id");
        challenge.Property(x => x.ChallengerName).HasColumnName("challenger_name");
        challenge.Property(x => x.TargetId).HasColumnName("target_id");
        challenge.Property(x => x.TargetName).HasColumnName("target_name");
        challenge.Property(x => x.Amount).HasColumnName("amount");
        challenge.Property(x => x.Game).HasColumnName("game");
        challenge.Property(x => x.Status).HasColumnName("status");
        challenge.Property(x => x.CreatedAt).HasColumnName("created_at");
        challenge.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        challenge.Property(x => x.RespondedAt).HasColumnName("responded_at");
        challenge.Property(x => x.CompletedAt).HasColumnName("completed_at");
        challenge.HasIndex(x => new { x.ChatId, x.Status, x.CreatedAt })
            .HasDatabaseName("ix_challenge_duels_chat_status_created");
        challenge.HasIndex(x => new { x.TargetId, x.Status, x.ExpiresAt })
            .HasDatabaseName("ix_challenge_duels_target_status");
    }
}

public sealed class ChallengeEntity
{
    public Guid Id { get; init; }
    public long ChatId { get; init; }
    public long ChallengerId { get; init; }
    public string ChallengerName { get; init; } = "";
    public long TargetId { get; init; }
    public string TargetName { get; init; } = "";
    public int Amount { get; init; }
    public string Game { get; init; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RespondedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public static ChallengeEntity From(Challenge value) => new()
    {
        Id = value.Id, ChatId = value.ChatId, ChallengerId = value.ChallengerId,
        ChallengerName = value.ChallengerName, TargetId = value.TargetId, TargetName = value.TargetName,
        Amount = value.Amount, Game = value.Game.ToString(), Status = value.Status.ToString(),
        CreatedAt = value.CreatedAt, ExpiresAt = value.ExpiresAt,
    };

    public Challenge ToDomain() => new(
        Id, ChatId, ChallengerId, ChallengerName, TargetId, TargetName, Amount,
        Enum.Parse<ChallengeGame>(Game), Enum.Parse<ChallengeStatus>(Status), CreatedAt, ExpiresAt);
}
