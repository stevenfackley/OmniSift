// ============================================================
// Unit Tests — ReciprocalRankFusion
// Verifies ordering correctness, tie handling, and edge cases.
// ============================================================

using FluentAssertions;
using OmniSift.Api.Services;

namespace OmniSift.UnitTests.Services;

public sealed class ReciprocalRankFusionTests
{
    // ── Basic ordering ───────────────────────────────────────

    [Fact]
    public void Fuse_ItemInBothLists_ScoresHigherThanItemInOneList()
    {
        // "a" appears at rank 1 in both lists → high combined score
        // "b" appears at rank 1 in vector only, missing from keyword
        // "c" appears at rank 1 in keyword only, missing from vector
        var vector = new[]
        {
            new RankedItem("a", 0.95),
            new RankedItem("b", 0.90)
        };
        var keyword = new[]
        {
            new RankedItem("a", 10.0),
            new RankedItem("c", 8.0)
        };

        var fused = ReciprocalRankFusion.Fuse(vector, keyword);

        fused[0].Should().Be("a", because: "'a' appears top-1 in both arms");
    }

    [Fact]
    public void Fuse_ThreeItems_CorrectDescendingOrder()
    {
        // x: rank 1 vector + rank 1 keyword   → 1/61 + 1/61 ≈ 0.0328
        // y: rank 1 vector only               → 1/61 ≈ 0.0164
        // z: rank 1 keyword only              → 1/61 ≈ 0.0164 (tie with y, broken by key)
        var vector = new[] { new RankedItem("x", 1.0), new RankedItem("y", 0.9) };
        var keyword = new[] { new RankedItem("x", 1.0), new RankedItem("z", 0.9) };

        var fused = ReciprocalRankFusion.Fuse(vector, keyword);

        fused[0].Should().Be("x");
        // y and z tie; tie-broken by key ascending → "y" before "z"
        fused.Should().ContainInConsecutiveOrder("y", "z");
    }

    [Fact]
    public void Fuse_EmptyVectorArm_ReturnsKeywordOrder()
    {
        var keyword = new[]
        {
            new RankedItem("alpha", 1.0),
            new RankedItem("beta", 0.5)
        };

        var fused = ReciprocalRankFusion.Fuse([], keyword);

        fused.Should().ContainInOrder("alpha", "beta");
    }

    [Fact]
    public void Fuse_EmptyKeywordArm_ReturnsVectorOrder()
    {
        var vector = new[]
        {
            new RankedItem("alpha", 1.0),
            new RankedItem("beta", 0.5)
        };

        var fused = ReciprocalRankFusion.Fuse(vector, []);

        fused.Should().ContainInOrder("alpha", "beta");
    }

    [Fact]
    public void Fuse_BothEmpty_ReturnsEmpty()
    {
        var fused = ReciprocalRankFusion.Fuse([], []);
        fused.Should().BeEmpty();
    }

    // ── Tie handling ─────────────────────────────────────────

    [Fact]
    public void Fuse_TieItems_BrokenByKeyAlphabetically()
    {
        // "bb" and "aa" each appear at rank 1 in exactly one arm
        // → equal RRF scores → tie-break by key → "aa" first
        var vector = new[] { new RankedItem("bb", 1.0) };
        var keyword = new[] { new RankedItem("aa", 1.0) };

        var fused = ReciprocalRankFusion.Fuse(vector, keyword);

        fused.Should().ContainInOrder("aa", "bb");
    }

    // ── Formula correctness ──────────────────────────────────

    [Fact]
    public void FuseWithScores_SingleItemInBothArms_ScoreEqualsDoubleContribution()
    {
        // With k=60, rank 1 (0-indexed in the accumulate = rank+1=1):
        //   contribution per list = 1/(60+1) = 1/61
        //   both lists → 2/61
        const double expected = 2.0 / 61.0;
        const int k = 60;

        var item = new[] { new RankedItem("x", 1.0) };
        var results = ReciprocalRankFusion.FuseWithScores(item, item, k);

        results.Should().HaveCount(1);
        results[0].RrfScore.Should().BeApproximately(expected, 1e-10);
    }

    [Fact]
    public void FuseWithScores_DifferentRanks_HigherRankGetsHigherScore()
    {
        // "top" at rank 1, "bottom" at rank 3
        var vector = new[]
        {
            new RankedItem("top", 1.0),
            new RankedItem("mid", 0.8),
            new RankedItem("bottom", 0.6)
        };

        var results = ReciprocalRankFusion.FuseWithScores(vector, []);

        var topScore = results.First(r => r.Key == "top").RrfScore;
        var bottomScore = results.First(r => r.Key == "bottom").RrfScore;

        topScore.Should().BeGreaterThan(bottomScore);
    }

    // ── Null guard ───────────────────────────────────────────

    [Fact]
    public void Fuse_NullVectorArg_Throws()
    {
        var act = () => ReciprocalRankFusion.Fuse(null!, []);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Fuse_NullKeywordArg_Throws()
    {
        var act = () => ReciprocalRankFusion.Fuse([], null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
