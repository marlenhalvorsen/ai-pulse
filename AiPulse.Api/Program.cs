using System.Threading.RateLimiting;
using Hangfire.Storage.SQLite;
using Microsoft.AspNetCore.RateLimiting;
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

var hangfireConnection = builder.Configuration.GetConnectionString("HangfireConnection")!;
builder.Services.AddHangfire(config => config.UseSQLiteStorage(hangfireConnection));
builder.Services.AddHangfireServer();

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

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
app.UseRateLimiter();

app.MapTrendingEndpoints();

app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<TrendRefreshJob>(
    "trend-refresh",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/30 * * * *");

_ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<TrendRefreshJob>().ExecuteAsync();
});

app.Run();

public partial class Program { }
