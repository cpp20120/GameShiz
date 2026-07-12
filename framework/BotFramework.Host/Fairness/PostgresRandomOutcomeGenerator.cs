using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BotFramework.Contracts.Operations;
using Dapper;

namespace BotFramework.Host.Fairness;

public sealed class PostgresRandomOutcomeGenerator(
    INpgsqlConnectionFactory connections,
    TimeProvider timeProvider) : IRandomOutcomeGenerator
{
    public const string CurrentAlgorithmVersion = "sha256-counter-v1";
    public string AlgorithmVersion => CurrentAlgorithmVersion;

    public async Task<FairnessCommitment> CommitAsync(string gameId, string canonicalInput,
        FairnessEntropySource entropySource = FairnessEntropySource.Server, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gameId) || string.IsNullOrWhiteSpace(canonicalInput))
            throw new ArgumentException("Game ID and canonical input are required.");

        var seed = RandomNumberGenerator.GetBytes(32);
        var seedText = Convert.ToBase64String(seed);
        var commitment = Hex(SHA256.HashData(seed));
        var inputHash = Hash(canonicalInput);
        var createdAt = timeProvider.GetUtcNow();
        const string sql = """
            INSERT INTO fairness_audit
                (game_id, algorithm_version, commitment, canonical_input_hash, server_seed,
                 entropy_source, status, created_at)
            VALUES (@gameId, @algorithmVersion, @commitment, @inputHash, @seedText,
                    @entropySource, 'committed', @createdAt)
            RETURNING id
            """;
        await using var connection = await connections.OpenAsync(ct);
        var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            gameId,
            algorithmVersion = AlgorithmVersion,
            commitment,
            inputHash,
            seedText,
            entropySource = entropySource.ToString().ToLowerInvariant(),
            createdAt,
        }, cancellationToken: ct));
        return new(id, gameId, AlgorithmVersion, commitment, inputHash, entropySource, createdAt);
    }

    public async Task<FairnessResult> RevealAsync(long commitmentId, string canonicalInput,
        int exclusiveUpperBound, CancellationToken ct = default)
    {
        if (exclusiveUpperBound <= 0)
            throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound));
        const string selectSql = """
            SELECT id AS "Id", game_id AS "GameId", algorithm_version AS "AlgorithmVersion",
                   commitment AS "Commitment", canonical_input_hash AS "CanonicalInputHash",
                   server_seed AS "ServerSeed", entropy_source AS "EntropySource",
                   status AS "Status", created_at AS "CreatedAt"
            FROM fairness_audit WHERE id=@commitmentId
            """;
        await using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<AuditRow>(
            new CommandDefinition(selectSql, new { commitmentId }, cancellationToken: ct))
            ?? throw new InvalidOperationException($"Fairness commitment {commitmentId} does not exist.");
        if (!string.Equals(row.Status, "committed", StringComparison.Ordinal))
            throw new InvalidOperationException($"Fairness commitment {commitmentId} is already finalized.");
        if (!string.Equals(row.CanonicalInputHash, Hash(canonicalInput), StringComparison.Ordinal))
            throw new InvalidOperationException("Canonical input does not match the commitment.");
        if (!string.Equals(row.AlgorithmVersion, AlgorithmVersion, StringComparison.Ordinal))
            throw new InvalidOperationException($"Unsupported fairness algorithm '{row.AlgorithmVersion}'.");

        var seed = Convert.FromBase64String(row.ServerSeed);
        var result = Generate(seed, canonicalInput, exclusiveUpperBound);
        var resultHash = ResultHash(row.CanonicalInputHash, row.ServerSeed, result);
        var completedAt = timeProvider.GetUtcNow();
        const string updateSql = """
            UPDATE fairness_audit
            SET result_value=@result, result_hash=@resultHash, revealed_seed=server_seed,
                status='completed', completed_at=@completedAt
            WHERE id=@commitmentId AND status='committed'
            """;
        var changed = await connection.ExecuteAsync(new CommandDefinition(updateSql,
            new { commitmentId, result, resultHash, completedAt }, cancellationToken: ct));
        if (changed != 1)
            throw new InvalidOperationException("The fairness commitment was completed concurrently.");
        return new(row.Id, row.GameId, row.AlgorithmVersion, row.Commitment, row.CanonicalInputHash,
            row.ServerSeed, resultHash, result, ParseEntropy(row.EntropySource),
            FairnessAuditStatus.Completed, row.CreatedAt, completedAt);
    }

    public FairnessVerification Verify(FairnessResult result, string canonicalInput, int exclusiveUpperBound)
    {
        if (result.Status != FairnessAuditStatus.Completed)
            return new(false, "The commitment is not completed.");
        if (!string.Equals(result.AlgorithmVersion, AlgorithmVersion, StringComparison.Ordinal))
            return new(false, "The algorithm version is unsupported.");
        var inputHash = Hash(canonicalInput);
        if (!FixedEquals(inputHash, result.CanonicalInputHash))
            return new(false, "Canonical input was changed.");
        byte[] seed;
        try { seed = Convert.FromBase64String(result.RevealedSeed); }
        catch (FormatException) { return new(false, "The revealed seed is invalid."); }
        if (!FixedEquals(Hex(SHA256.HashData(seed)), result.Commitment))
            return new(false, "The revealed seed does not match the commitment.");
        if (exclusiveUpperBound <= 0 || Generate(seed, canonicalInput, exclusiveUpperBound) != result.Result)
            return new(false, "The result cannot be reproduced.");
        if (!FixedEquals(ResultHash(inputHash, result.RevealedSeed, result.Result), result.ResultHash))
            return new(false, "The result hash was changed.");
        return new(true, null);
    }

    public async Task<IReadOnlyList<FairnessCommitment>> ListIncompleteAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT id AS "Id", game_id AS "GameId", algorithm_version AS "AlgorithmVersion",
                   commitment AS "Commitment", canonical_input_hash AS "CanonicalInputHash",
                   entropy_source AS "EntropySource", created_at AS "CreatedAt"
            FROM fairness_audit WHERE status='committed' ORDER BY created_at
            """;
        await using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<IncompleteRow>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.Select(row => new FairnessCommitment(row.Id, row.GameId, row.AlgorithmVersion,
            row.Commitment, row.CanonicalInputHash, ParseEntropy(row.EntropySource), row.CreatedAt)).ToList();
    }

    private static int Generate(byte[] seed, string canonicalInput, int exclusiveUpperBound)
    {
        var input = Encoding.UTF8.GetBytes(canonicalInput);
        var material = new byte[seed.Length + 1 + input.Length];
        seed.CopyTo(material, 0);
        material[seed.Length] = (byte)'\n';
        input.CopyTo(material, seed.Length + 1);
        var value = BinaryPrimitives.ReadUInt64BigEndian(SHA256.HashData(material));
        return (int)(value % (uint)exclusiveUpperBound);
    }

    private static string ResultHash(string inputHash, string seed, int result) =>
        Hash(string.Join('\n', inputHash, seed, result.ToString(CultureInfo.InvariantCulture)));
    private static string Hash(string value) => Hex(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Hex(byte[] value) => Convert.ToHexString(value).ToLowerInvariant();
    private static bool FixedEquals(string left, string right) => left.Length == right.Length &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static FairnessEntropySource ParseEntropy(string value) =>
        string.Equals(value, "external", StringComparison.OrdinalIgnoreCase)
            ? FairnessEntropySource.External : FairnessEntropySource.Server;

    private sealed record AuditRow(long Id, string GameId, string AlgorithmVersion, string Commitment,
        string CanonicalInputHash, string ServerSeed, string EntropySource, string Status, DateTimeOffset CreatedAt);
    private sealed record IncompleteRow(long Id, string GameId, string AlgorithmVersion, string Commitment,
        string CanonicalInputHash, string EntropySource, DateTimeOffset CreatedAt);
}
