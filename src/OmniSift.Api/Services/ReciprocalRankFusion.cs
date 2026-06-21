// ============================================================
// OmniSift.Api — Reciprocal Rank Fusion (RRF)
// Pure, side-effect-free function — fully unit-testable.
// Given two ranked lists (vector arm + keyword arm), fuses them
// into a single ranking using the standard RRF formula:
//   score(d) = Σ  1 / (k + rank_i(d))
// where k = 60 (Cormack et al. 2009).
// ============================================================

namespace OmniSift.Api.Services;

/// <summary>
/// A single item in a ranked list, identified by an opaque string key.
/// </summary>
/// <param name="Key">Stable identifier used to correlate across ranked lists.</param>
/// <param name="Score">Original score from the ranking arm (higher = better).</param>
public readonly record struct RankedItem(string Key, double Score);

/// <summary>
/// Stateless helper — instantiate or call static methods as needed.
/// </summary>
public static class ReciprocalRankFusion
{
    /// <summary>
    /// Default k constant from Cormack et al. 2009. Controls how much early
    /// ranks are penalised. Larger k → softer penalty for lower ranks.
    /// </summary>
    public const int DefaultK = 60;

    /// <summary>
    /// Fuse two ranked lists into a single list ordered by RRF score descending.
    /// Items that appear in only one list still receive that list's RRF contribution.
    /// Ties are broken by key (stable sort).
    /// </summary>
    /// <param name="vectorRanking">Items ordered by vector similarity (best first).</param>
    /// <param name="keywordRanking">Items ordered by keyword similarity (best first).</param>
    /// <param name="k">RRF k constant (default 60).</param>
    /// <returns>Keys ordered by fused RRF score descending.</returns>
    public static IReadOnlyList<string> Fuse(
        IReadOnlyList<RankedItem> vectorRanking,
        IReadOnlyList<RankedItem> keywordRanking,
        int k = DefaultK)
    {
        ArgumentNullException.ThrowIfNull(vectorRanking);
        ArgumentNullException.ThrowIfNull(keywordRanking);

        var scores = new Dictionary<string, double>(StringComparer.Ordinal);

        Accumulate(vectorRanking, k, scores);
        Accumulate(keywordRanking, k, scores);

        return [.. scores
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key) // stable tie-break
            .Select(kv => kv.Key)];
    }

    /// <summary>
    /// Like <see cref="Fuse"/> but also returns the fused RRF scores.
    /// Useful for applying a relevance threshold.
    /// </summary>
    public static IReadOnlyList<(string Key, double RrfScore)> FuseWithScores(
        IReadOnlyList<RankedItem> vectorRanking,
        IReadOnlyList<RankedItem> keywordRanking,
        int k = DefaultK)
    {
        ArgumentNullException.ThrowIfNull(vectorRanking);
        ArgumentNullException.ThrowIfNull(keywordRanking);

        var scores = new Dictionary<string, double>(StringComparer.Ordinal);

        Accumulate(vectorRanking, k, scores);
        Accumulate(keywordRanking, k, scores);

        return [.. scores
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Select(kv => (kv.Key, kv.Value))];
    }

    // ── private helpers ───────────────────────────────────────

    private static void Accumulate(
        IReadOnlyList<RankedItem> ranking,
        int k,
        Dictionary<string, double> scores)
    {
        for (var i = 0; i < ranking.Count; i++)
        {
            var key = ranking[i].Key;
            var contribution = 1.0 / (k + i + 1); // 1-based rank
            scores[key] = scores.GetValueOrDefault(key) + contribution;
        }
    }
}
