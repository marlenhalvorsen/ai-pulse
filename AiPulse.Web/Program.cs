using AiPulse.Application.UseCases;
using AiPulse.Infrastructure;
using AiPulse.Infrastructure.Jobs;
using AiPulse.Infrastructure.Persistence;
using AiPulse.Web.Api;
using AiPulse.Web.Middleware;
using Hangfire;
using Hangfire.InMemory;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<GetTrendingItemsQuery>();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddHangfire(config => config.UseInMemoryStorage());
builder.Services.AddHangfireServer();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseRouting();

app.MapTrendingEndpoints();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<TrendRefreshJob>(
    "trend-refresh",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/30 * * * *");

app.Run();

public partial class Program { }
