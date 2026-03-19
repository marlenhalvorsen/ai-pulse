using AiPulse.Application.Services;
using FluentAssertions;

namespace AiPulse.Tests.Domain;

public class TrendScoreCalculatorTests
{
    private readonly TrendScoreCalculator _sut = new();

    // --- Basic formula ---

    [Fact]
    public void Calculate_FreshItem_AppliesWeightedFormula()
    {
        // posted 1 hour ago: full recency_boost = 1.0
        var postedAt = DateTime.UtcNow.AddHours(-1);

        var score = _sut.Calculate(upvotes: 1000, comments: 100, postedAt: postedAt);

        // (1000 × 0.6) + (100 × 0.3) + (1.0 × 0.1) = 600 + 30 + 0.1 = 630.1
        score.Should().BeApproximately(630.1, precision: 0.01);
    }

    [Fact]
    public void Calculate_ZeroEngagement_ReturnsOnlyRecencyComponent()
    {
        // posted 1 hour ago: recency_boost ≈ 1.0
        var postedAt = DateTime.UtcNow.AddHours(-1);

        var score = _sut.Calculate(upvotes: 0, comments: 0, postedAt: postedAt);

        // 0 + 0 + (1.0 × 0.1) ≈ 0.1
        score.Should().BeApproximately(0.1, precision: 0.01);
    }

    // --- 7-day decay boundary ---

    [Fact]
    public void Calculate_ItemOlderThan7Days_ReturnsZero()
    {
        var postedAt = DateTime.UtcNow.AddDays(-8);

        var score = _sut.Calculate(upvotes: 1000, comments: 500, postedAt: postedAt);

        score.Should().Be(0);
    }

    [Fact]
    public void Calculate_ItemExactly7DaysOld_ReturnsZero()
    {
        var postedAt = DateTime.UtcNow.AddDays(-7);

        var score = _sut.Calculate(upvotes: 1000, comments: 500, postedAt: postedAt);

        score.Should().Be(0);
    }

    [Fact]
    public void Calculate_ItemJustUnder7DaysOld_ReturnsPositiveScore()
    {
        var postedAt = DateTime.UtcNow.AddDays(-7).AddMinutes(1);

        var score = _sut.Calculate(upvotes: 1000, comments: 500, postedAt: postedAt);

        score.Should().BeGreaterThan(0);
    }

    // --- Recency boost ---

    [Fact]
    public void Calculate_OlderItemWithinWindow_HasLowerScoreThanNewerItem()
    {
        var newer = DateTime.UtcNow.AddHours(-1);
        var older = DateTime.UtcNow.AddDays(-6);

        var newerScore = _sut.Calculate(upvotes: 100, comments: 10, postedAt: newer);
        var olderScore = _sut.Calculate(upvotes: 100, comments: 10, postedAt: older);

        newerScore.Should().BeGreaterThan(olderScore);
    }

    [Fact]
    public void Calculate_RecencyBoostIsProportionalToAgeWithin7Days()
    {
        // At age=0 days boost=1.0; at age=3.5 days boost≈0.5
        var halfwayOld = DateTime.UtcNow.AddDays(-3.5);

        var score = _sut.Calculate(upvotes: 0, comments: 0, postedAt: halfwayOld);

        // Only recency component: 0.5 × 0.1 = 0.05
        score.Should().BeApproximately(0.05, precision: 0.01);
    }

    // --- Future date guard ---

    [Fact]
    public void Calculate_FutureDatedItem_TreatedAsFreshNotNegative()
    {
        var postedAt = DateTime.UtcNow.AddHours(1);

        var score = _sut.Calculate(upvotes: 100, comments: 10, postedAt: postedAt);

        score.Should().BeGreaterThanOrEqualTo(0);
    }

    // --- Upvotes-only and comments-only ---

    [Fact]
    public void Calculate_UpvotesOnly_WeightedCorrectly()
    {
        var postedAt = DateTime.UtcNow.AddHours(-1);

        var score = _sut.Calculate(upvotes: 500, comments: 0, postedAt: postedAt);

        // (500 × 0.6) + 0 + (1.0 × 0.1) = 300.1
        score.Should().BeApproximately(300.1, precision: 0.01);
    }

    [Fact]
    public void Calculate_CommentsOnly_WeightedCorrectly()
    {
        var postedAt = DateTime.UtcNow.AddHours(-1);

        var score = _sut.Calculate(upvotes: 0, comments: 200, postedAt: postedAt);

        // 0 + (200 × 0.3) + (1.0 × 0.1) = 60.1
        score.Should().BeApproximately(60.1, precision: 0.01);
    }
}
