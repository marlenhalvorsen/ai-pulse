using AiPulse.Application.Interfaces;
using AiPulse.Application.Services;
using AiPulse.Domain.Enums;
using AiPulse.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiPulse.Infrastructure.Persistence;

public class ContentRepository : IContentRepository, ITrendingQuery
{
    private readonly AppDbContext _db;
    private readonly TrendScoreCalculator _calculator;
    private readonly ILogger<ContentRepository> _logger;

    public ContentRepository(AppDbContext db, TrendScoreCalculator calculator, ILogger<ContentRepository> logger)
    {
        _db = db;
        _calculator = calculator;
        _logger = logger;
    }

    public async Task UpsertAsync(IEnumerable<ContentItem> items, CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();
        _logger.LogInformation("DEBUG UpsertAsync called with {Count} items", itemList.Count);

        int inserted = 0, updated = 0;
        foreach (var item in itemList)
        {
            var existing = await _db.ContentItems.FindAsync([item.Id], cancellationToken);
            if (existing is null)
            {
                _db.ContentItems.Add(item);
                inserted++;
            }
            else
            {
                _db.Entry(existing).State = EntityState.Detached;
                _db.ContentItems.Update(item);
                updated++;
            }
        }

        var saved = await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("DEBUG SaveChangesAsync wrote {Saved} rows ({Inserted} inserted, {Updated} updated)", saved, inserted, updated);
    }

    public async Task<IEnumerable<ContentItem>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _db.ContentItems.ToListAsync(cancellationToken);

    public async Task<IEnumerable<ContentItem>> GetTrendingAsync(
        ContentType? type,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - window;
        _logger.LogInformation("DEBUG GetTrendingAsync: type={Type}, cutoff={Cutoff:u}, totalInDb={Total}",
            type, cutoff, await _db.ContentItems.CountAsync(cancellationToken));

        var query = _db.ContentItems.Where(i => i.PostedAt > cutoff);

        if (type.HasValue)
            query = query.Where(i => i.ContentType == type.Value);

        var items = await query.ToListAsync(cancellationToken);
        _logger.LogInformation("DEBUG GetTrendingAsync: {InWindow} items within window, oldest PostedAt in DB: {OldestUtc:u}",
            items.Count,
            await _db.ContentItems.AnyAsync(cancellationToken)
                ? (await _db.ContentItems.MinAsync(i => i.PostedAt, cancellationToken)).ToString("u")
                : "none");

        var result = items
            .Select(i => (item: i, score: _calculator.Calculate(i.Upvotes, i.CommentCount, i.PostedAt)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .Take(limit)
            .Select(x => x.item)
            .ToList();

        _logger.LogInformation("DEBUG GetTrendingAsync: {Count} items returned after score filter", result.Count);
        return result;
    }
}
