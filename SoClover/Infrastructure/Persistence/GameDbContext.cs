using Microsoft.EntityFrameworkCore;

namespace SoClover.Infrastructure.Persistence;

public sealed class GameDbContext : DbContext
{
    public DbSet<GameEntity> Games => Set<GameEntity>();
    public DbSet<GameResultEntity> GameResults => Set<GameResultEntity>();

    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var game = modelBuilder.Entity<GameEntity>();
        game.ToTable("games");
        game.HasKey(x => x.Id);
        game.Property(x => x.Id).HasColumnName("id");
        game.Property(x => x.Status).HasMaxLength(64).IsRequired().HasColumnName("status");
        game.Property(x => x.Language).HasMaxLength(64).HasColumnName("language");
        game.Property(x => x.PhaseEndsAtUtc).HasColumnName("phase_ends_at_utc");
        game.Property(x => x.UpdatedAtUtc).IsRequired().HasColumnName("updated_at_utc");
        game.Property(x => x.PayloadJson).IsRequired().HasColumnName("payload_json");

        // jsonb column type
        game.Property(x => x.PayloadJson).HasColumnType("jsonb");

        // Optimistic concurrency using xmin
        game.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();

        game.HasIndex(x => x.Status).HasDatabaseName("ix_games_status");
        game.HasIndex(x => x.UpdatedAtUtc).HasDatabaseName("ix_games_updated_at_utc");
        game.HasIndex(x => x.PhaseEndsAtUtc).HasDatabaseName("ix_games_phase_ends_at_utc");

        // Game results mapping
        var result = modelBuilder.Entity<GameResultEntity>();
        result.ToTable("game_results");
        result.HasKey(x => x.Id);
        result.Property(x => x.Id).HasColumnName("id");
        result.Property(x => x.GameId).IsRequired().HasColumnName("game_id");
        result.Property(x => x.PlayerName).IsRequired().HasMaxLength(128).HasColumnName("player_name");
        result.Property(x => x.BoardIsGuessed).IsRequired().HasColumnName("board_is_guessed");
        result.Property(x => x.Attempts).IsRequired().HasColumnName("attempts");
        result.Property(x => x.DurationSeconds).IsRequired().HasColumnName("duration_seconds");

        result.HasIndex(x => x.GameId).HasDatabaseName("ix_game_results_game_id");
        result.HasOne<GameEntity>()
              .WithMany()
              .HasForeignKey(x => x.GameId)
              .HasConstraintName("fk_game_results_games")
              .OnDelete(DeleteBehavior.Cascade);
    }
}