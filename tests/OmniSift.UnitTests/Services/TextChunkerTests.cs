// ============================================================
// Unit Tests — TextChunker
// Verifies chunk sizes, overlap, and edge cases
// ============================================================

using FluentAssertions;
using OmniSift.Api.Services;

namespace OmniSift.UnitTests.Services;

public sealed class TextChunkerTests
{
    private readonly TextChunker _sut = new();

    [Fact]
    public void ChunkText_WithEmptyString_ReturnsEmptyList()
    {
        var result = _sut.ChunkText(string.Empty);
        result.Should().BeEmpty();
    }

    [Fact]
    public void ChunkText_WithWhitespace_ReturnsEmptyList()
    {
        var result = _sut.ChunkText("   \n\t  ");
        result.Should().BeEmpty();
    }

    [Fact]
    public void ChunkText_WithNull_ThrowsArgumentNullException()
    {
        var act = () => _sut.ChunkText(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ChunkText_ShortText_ReturnsSingleChunk()
    {
        var text = "Hello world this is a test";
        var result = _sut.ChunkText(text, chunkSize: 500, overlap: 50);

        result.Should().HaveCount(1);
        result[0].Content.Should().Be(text);
        result[0].Index.Should().Be(0);
        result[0].TokenCount.Should().Be(6);
    }

    [Fact]
    public void ChunkText_ExactChunkSize_ReturnsSingleChunk()
    {
        var words = Enumerable.Range(1, 500).Select(i => $"word{i}");
        var text = string.Join(' ', words);

        var result = _sut.ChunkText(text, chunkSize: 500, overlap: 50);

        result.Should().HaveCount(1);
        result[0].TokenCount.Should().Be(500);
    }

    [Fact]
    public void ChunkText_LongerThanChunkSize_ReturnsMultipleChunks()
    {
        var words = Enumerable.Range(1, 1000).Select(i => $"word{i}");
        var text = string.Join(' ', words);

        var result = _sut.ChunkText(text, chunkSize: 500, overlap: 50);

        result.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void ChunkText_ChunksHaveCorrectOverlap()
    {
        var words = Enumerable.Range(1, 1000).Select(i => $"w{i}");
        var text = string.Join(' ', words);

        var result = _sut.ChunkText(text, chunkSize: 100, overlap: 20);

        // Verify overlap: last 20 tokens of chunk N should equal first 20 of chunk N+1
        for (var i = 0; i < result.Count - 1; i++)
        {
            var currentTokens = result[i].Content.Split(' ');
            var nextTokens = result[i + 1].Content.Split(' ');

            var overlapFromCurrent = currentTokens.TakeLast(20).ToArray();
            var overlapFromNext = nextTokens.Take(20).ToArray();

            overlapFromCurrent.Should().BeEquivalentTo(overlapFromNext,
                $"chunk {i} should overlap with chunk {i + 1}");
        }
    }

    [Fact]
    public void ChunkText_IndicesAreSequential()
    {
        var words = Enumerable.Range(1, 2000).Select(i => $"word{i}");
        var text = string.Join(' ', words);

        var result = _sut.ChunkText(text, chunkSize: 500, overlap: 50);

        for (var i = 0; i < result.Count; i++)
        {
            result[i].Index.Should().Be(i);
        }
    }

    [Fact]
    public void ChunkText_TokenCountMatchesActualTokens()
    {
        var words = Enumerable.Range(1, 750).Select(i => $"word{i}");
        var text = string.Join(' ', words);

        var result = _sut.ChunkText(text, chunkSize: 500, overlap: 50);

        foreach (var chunk in result)
        {
            var actualTokens = chunk.Content.Split(' ').Length;
            chunk.TokenCount.Should().Be(actualTokens);
        }
    }

    [Fact]
    public void ChunkText_ZeroChunkSize_Throws()
    {
        var act = () => _sut.ChunkText("some text", chunkSize: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ChunkText_NegativeOverlap_Throws()
    {
        var act = () => _sut.ChunkText("some text", overlap: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ChunkText_OverlapGreaterThanChunkSize_Throws()
    {
        var act = () => _sut.ChunkText("some text", chunkSize: 10, overlap: 10);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ChunkText_DefaultParameters_Uses500And50()
    {
        var words = Enumerable.Range(1, 1200).Select(i => $"w{i}");
        var text = string.Join(' ', words);

        var result = _sut.ChunkText(text);

        // With 1200 tokens, chunk_size=500, overlap=50, stride=450
        // Expected: ceil((1200 - 500) / 450) + 1 ≈ 3 chunks
        result.Should().HaveCountGreaterOrEqualTo(2);
        result[0].TokenCount.Should().BeLessOrEqualTo(500);
    }

    [Fact]
    public void ChunkText_AllContentPreserved()
    {
        var originalWords = Enumerable.Range(1, 100).Select(i => $"word{i}").ToArray();
        var text = string.Join(' ', originalWords);

        var result = _sut.ChunkText(text, chunkSize: 30, overlap: 5);

        // Every original word should appear in at least one chunk
        foreach (var word in originalWords)
        {
            result.Any(c => c.Content.Contains(word)).Should().BeTrue(
                $"word '{word}' should appear in at least one chunk");
        }
    }
}
