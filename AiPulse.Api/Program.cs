using AiPulse.Api.Api;
using AiPulse.Api.Middleware;
using AiPulse.Application.Interfaces;
using AiPulse.Application.UseCases;
using AiPulse.Infrastructure;
using AiPulse.Infrastructure.Fetchers;
using AiPulse.Infrastructure.Jobs;
using AiPulse.Infrastructure.Persistence;
using Hangfire;
using Hangfire.InMemory;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<GetTrendingItemsQuery>();
builder.Services.AddScoped<GetSourceItemsQuery>();

builder.Services.AddHangfire(config => config.UseInMemoryStorage());
builder.Services.AddHangfireServer();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5243").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

_ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<AppDbContext>();
    var fetcher = sp.GetRequiredService<IEnumerable<ITrendFetcher>>().OfType<PodcastFetcher>().Single();
    var repo = sp.GetRequiredService<IContentRepository>();
    await new PodcastDescriptionCleanup(db, fetcher, repo).RunAsync();
});

app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseCors();
app.UseStaticFiles();
app.UseRouting();

app.MapTrendingEndpoints();

app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<TrendRefreshJob>(
    "trend-refresh",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/30 * * * *");

// TODO: remove before production — triggers an immediate fetch on startup for dev/testing
if (app.Environment.IsDevelopment())
{
    _ = Task.Run(async () =>
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TrendRefreshJob>().ExecuteAsync();
    });
}

app.Run();

public partial class Program { }
