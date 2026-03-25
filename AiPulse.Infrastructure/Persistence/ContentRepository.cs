using AiPulse.Application.Interfaces;
using AiPulse.Application.Services;
using AiPulse.Domain.Enums;
using AiPulse.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AiPulse.Infrastructure.Persistence;

public class ContentRepository : IContentRepository, ITrendingQuery
{
    private readonly AppDbContext _db;
    private readonly TrendScoreCalculator _calculator;

    public ContentRepository(AppDbContext db, TrendScoreCalculator calculator)
    {
        _db = db;
        _calculator = calculator;
    }

    public async Task UpsertAsync(IEnumerable<ContentItem> items, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            var existing = await _db.ContentItems.FindAsync([item.Id], cancellationToken);
            if (existing is null)
            {
                _db.ContentItems.Add(item);
            }
            else
            {
                _db.Entry(existing).State = EntityState.Detached;
                _db.ContentItems.Update(item);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<ContentItem>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _db.ContentItems.ToListAsync(cancellationToken);

    public async Task<IEnumerable<ContentItem>> GetTrendingAsync(
        ContentType? type,
        SourceType? source,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - window;

        var query = _db.ContentItems.Where(i => i.PostedAt > cutoff);

        if (type.HasValue)
            query = query.Where(i => i.ContentType == type.Value);

        if (source.HasValue)
            query = query.Where(i => i.Source == source.Value);

        var items = await query.ToListAsync(cancellationToken);

        return items
            .Select(i => (item: i, score: _calculator.Calculate(i.Upvotes, i.CommentCount, i.PostedAt)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .Take(limit)
            .Select(x => x.item);
    }
}
