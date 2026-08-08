using BotFramework.Host.Persistence.Connections;
using Microsoft.EntityFrameworkCore;

namespace Games.Challenges.Infrastructure.Persistence;

public sealed class ChallengeDbContext(INpgsqlConnectionFactory connections) : DbContext
{
    public DbSet<ChallengeEntity> Challenges => Set<ChallengeEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder
                .UseNpgsql(connections.Create().ConnectionString)
                .AddInterceptors(TenantDatabaseConnectionInterceptor.Instance);
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
