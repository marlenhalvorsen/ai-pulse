namespace AiPulse.Application.Services;

public class TrendScoreCalculator
{
    private const double UpvoteWeight = 0.6;
    private const double CommentWeight = 0.3;
    private const double RecencyWeight = 0.1;
    private const int DecayWindowDays = 7;

    public double Calculate(int upvotes, int comments, DateTime postedAt)
    {
        var ageInDays = (DateTime.UtcNow - postedAt).TotalDays;

        if (ageInDays >= DecayWindowDays)
            return 0;

        // Clamp future-dated items to age=0 (boost=1.0)
        var clampedAge = Math.Max(0, ageInDays);
        var recencyBoost = 1.0 - (clampedAge / DecayWindowDays);

        return (upvotes * UpvoteWeight)
             + (comments * CommentWeight)
             + (recencyBoost * RecencyWeight);
    }
}
