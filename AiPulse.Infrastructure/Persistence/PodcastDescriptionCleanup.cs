using AiPulse.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiPulse.Infrastructure.Persistence;

public class PodcastDescriptionCleanup
{
    private readonly AppDbContext _db;
    private readonly ITrendFetcher _podcastFetcher;
    private readonly IContentRepository _repository;

    public PodcastDescriptionCleanup(AppDbContext db, ITrendFetcher podcastFetcher, IContentRepository repository)
    {
        _db = db;
        _podcastFetcher = podcastFetcher;
        _repository = repository;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.ExecuteSqlRawAsync(
            "DELETE FROM ContentItems WHERE ContentType = 'Podcast'",
            cancellationToken);

        var items = await _podcastFetcher.FetchAsync(cancellationToken);
        await _repository.UpsertAsync(items, cancellationToken);
    }
}
