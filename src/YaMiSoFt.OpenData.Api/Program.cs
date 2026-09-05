using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using StackExchange.Redis;
using YaMiSoFt.OpenData.Api;
using YaMiSoFt.OpenData.Api.Configuration;
using YaMiSoFt.OpenData.Api.Endpoints;
using YaMiSoFt.OpenData.Api.OpenApi;
using YaMiSoFt.OpenData.Api.RateLimiting;
using YaMiSoFt.OpenData.Core.Json;
using YaMiSoFt.OpenData.Data;

var builder = WebApplication.CreateBuilder(args);

// Container platforms hand the listening port over in PORT rather than letting the image
// choose one. Reading it here keeps the entrypoint a plain exec: the chiselled runtime image
// has no shell to expand the variable with. An explicit ASPNETCORE_URLS still wins, so local
// development and launchSettings are unaffected.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port &&
    string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")) &&
    int.TryParse(port, CultureInfo.InvariantCulture, out var portNumber))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{portNumber}");
}

builder.Services.Configure<OpenDataOptions>(
    builder.Configuration.GetSection(OpenDataOptions.SectionName));

// Source-generated contracts only: no reflection-based serialization anywhere on the request
// path, which keeps the AOT option open for Faz 2 (PLAN.md 3.9).
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, OpenDataJsonContext.Default);
    options.SerializerOptions.Encoder = OpenDataJson.Options.Encoder;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ClientIdentityResolver>();
// Counters go to Redis only when a connection string is configured. Without one, counting is
// in-process: exact on a single instance, approximate once the platform scales out. That is
// an acceptable trade when an edge rule handles bursts ahead of the application, which is the
// arrangement on Vercel (PLAN.md 3.1, backlog A1).
var redisConnectionString = builder.Configuration["OpenData:RateLimit:RedisConnectionString"];

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        var configuration = ConfigurationOptions.Parse(redisConnectionString);
        configuration.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(configuration);
    });
    builder.Services.AddSingleton<IRateLimitStore, RedisRateLimitStore>();
}
else
{
    builder.Services.AddSingleton<IRateLimitStore, InMemoryRateLimitStore>();
}

// Datasets are read once, at startup, into immutable collections. A failure here should stop
// the process rather than let a pod serve an empty or half-loaded dataset (BRD 7.2).
builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OpenDataOptions>>().Value;
    var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
    var directory = DataSetLoader.ResolveDirectory(options.DataDirectory, AppContext.BaseDirectory);

    if (!Directory.Exists(directory))
    {
        // Falls back to the repository layout so `dotnet run` works from a source checkout.
        directory = DataSetLoader.ResolveDirectory(options.DataDirectory, environment.ContentRootPath);
    }

    return new DataDirectory(directory);
});

builder.Services.AddSingleton(sp => new TurkeyStore(
    DataSetLoader.LoadProvinces(sp.GetRequiredService<DataDirectory>().Path),
    DataSetLoader.LoadDistricts(sp.GetRequiredService<DataDirectory>().Path)));
// Quarters (semt) load at startup even though the settlements below them do not: 2,433 rows
// cost almost nothing and they carry the postal codes, so a postal code lookup never has to
// reach for the lazy dataset.
builder.Services.AddSingleton(sp => new QuarterStore(
    DataSetLoader.LoadQuarters(sp.GetRequiredService<DataDirectory>().Path),
    sp.GetRequiredService<TurkeyStore>()));
builder.Services.AddSingleton(sp => new CurrencyStore(
    DataSetLoader.LoadCurrencies(sp.GetRequiredService<DataDirectory>().Path)));
builder.Services.AddSingleton(sp => new LanguageStore(
    DataSetLoader.LoadLanguages(sp.GetRequiredService<DataDirectory>().Path)));
builder.Services.AddSingleton(sp => new CountryStore(
    DataSetLoader.LoadCountries(sp.GetRequiredService<DataDirectory>().Path)));
builder.Services.AddSingleton(sp => new HolidayStore(
    DataSetLoader.LoadHolidays(sp.GetRequiredService<DataDirectory>().Path)));
builder.Services.AddSingleton(sp => new MobileOperatorStore(
    DataSetLoader.LoadMobileOperators(sp.GetRequiredService<DataDirectory>().Path)));

// Settlements are the one dataset that does not load at startup. See NeighborhoodStore: the
// measured cost is ~520 ms of parsing, which on a scale-to-zero platform would land on every
// cold start including the requests that never touch it.
builder.Services.AddSingleton(sp => new NeighborhoodStore(
    sp.GetRequiredService<DataDirectory>().Path));

builder.Services.AddProblemDetails();

// Two documents, not one dynamically-translated document: OpenApiOperationTransformerContext
// carries no HttpContext, so a summary cannot vary per request the way `?lang=` does for data
// (RequestQuery.ResolveLanguage, FR-04). Decision in PLAN.md §6: English primary ("v1", also
// what WithSummary sets directly), Turkish supplementary ("v1-tr", filled in by
// TurkishSummaryTransformer from the WithBilingualSummary metadata on each endpoint).
// x-tagGroups nests related tags under one sidebar heading in Scalar — "GSM Operatörleri"/
// "Mobile Operators" and "Ülke Kodları"/"Countries" both live under a "Telefon"/"Phone"
// parent, since they are the two things a caller reaches for when they say "phone code".
// Every other tag gets its own singleton group so it still renders as a plain top-level
// entry (see TagGroupsDocumentTransformer's remarks on why nothing is left ungrouped).
TagGroup[] englishTagGroups =
[
    new("Address", "Address"),
    new("Phone", "Mobile Operators", "Countries"),
    new("Currencies", "Currencies"),
    new("Languages", "Languages"),
    new("Public Holidays", "Public Holidays"),
];
TagGroup[] turkishTagGroups =
[
    new("Adres", "Adres"),
    new("Telefon", "GSM Operatörleri", "Ülke Kodları"),
    new("Para Birimleri", "Para Birimleri"),
    new("Diller", "Diller"),
    new("Resmi Tatiller", "Resmi Tatiller"),
];

builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer(new TagGroupsDocumentTransformer(englishTagGroups)));
builder.Services.AddOpenApi("v1-tr", options =>
{
    options.AddOperationTransformer<TurkishSummaryTransformer>();
    options.AddOperationTransformer<TurkishTagTransformer>();
    options.AddDocumentTransformer(new TagGroupsDocumentTransformer(turkishTagGroups, TurkishTagNames.Map));
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(
    options => options.Level = CompressionLevel.Fastest);

builder.Services.AddOutputCache(options =>
{
    // Deliberately NOT calling SetVaryByQuery with an allow-list. That restricts the cache
    // key to the named parameters, so any parameter not on the list is ignored and unrelated
    // requests collide. The default varies by the whole query string, which is the only safe
    // behaviour as endpoints grow.
    options.AddBasePolicy(policy => policy
        .Expire(TimeSpan.FromMinutes(10))
        .SetVaryByHeader("Accept-Language"));
});

// CORS is wide open by design: the data is public and read-only, and the whole point is that
// a browser app can call it without a proxy (NFR-12, NFR-13).
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .WithMethods("GET", "HEAD", "OPTIONS")
    .WithExposedHeaders("X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset", "Retry-After")));

builder.Services.AddHealthChecks()
    .AddCheck<DataSetHealthCheck>("dataset", tags: ["ready"]);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("yamisoft-opendata-api"))
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

var proxyOptions = builder.Configuration
    .GetSection(OpenDataOptions.SectionName)
    .Get<OpenDataOptions>()?.Proxy ?? new ProxyOptions();

if (proxyOptions.UseForwardedHeadersMiddleware)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = proxyOptions.ForwardedHeadersToTrust;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        foreach (var proxy in proxyOptions.KnownProxies)
        {
            if (IPAddress.TryParse(proxy, out var address))
            {
                options.KnownProxies.Add(address);
            }
        }

        foreach (var network in proxyOptions.KnownNetworks)
        {
            var parts = network.Split('/', 2);

            if (parts.Length == 2 &&
                IPAddress.TryParse(parts[0], out var prefix) &&
                int.TryParse(parts[1], out var length))
            {
                options.KnownIPNetworks.Add(new System.Net.IPNetwork(prefix, length));
            }
        }
    });
}

var app = builder.Build();

if (proxyOptions.UseForwardedHeadersMiddleware)
{
    // Must run before anything reads the client address, or rate limiting partitions on the
    // proxy's address instead of the caller's (PLAN.md 3.3).
    app.UseForwardedHeaders();
}

WarnOnMisconfiguration(app);

app.UseExceptionHandler();
app.UseStatusCodePages();

// HTTPS termination happens at the platform edge on Vercel, and the container only ever
// sees plain HTTP from it. Redirecting there would produce a loop, so this stays off unless
// the application is the thing terminating TLS.
if (!app.Environment.IsDevelopment() && !proxyOptions.IsBehindProxy)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next().ConfigureAwait(false);
});

app.UseResponseCompression();
app.UseCors();
app.UseMiddleware<RateLimitMiddleware>();
app.UseOutputCache();

app.MapGet("/", () => Results.Redirect("/docs")).ExcludeFromDescription();

app.MapOpenApi();
app.MapScalarApiReference("/docs", options => options
    .WithTitle("OpenData API")
    .WithTheme(ScalarTheme.BluePlanet)
    .AddDocument("v1", "English", isDefault: true)
    .AddDocument("v1-tr", "Türkçe"));

var v1 = app.MapGroup("/api/v1");
v1.MapTurkeyEndpoints();
v1.MapQuarterEndpoints();
v1.MapReferenceEndpoints();
v1.MapHolidayEndpoints();
v1.MapPhoneEndpoints();
v1.MapNeighborhoodEndpoints();

app.MapHealthChecks("/health/live", new()
{
    Predicate = static _ => false,
}).ExcludeFromDescription();

app.MapHealthChecks("/health/ready", new()
{
    Predicate = static check => check.Tags.Contains("ready"),
}).ExcludeFromDescription();

app.MapPrometheusScrapingEndpoint("/metrics").ExcludeFromDescription();

await app.RunAsync().ConfigureAwait(false);

// Startup misconfiguration is silent at runtime and expensive in production: unmetered
// traffic, or every caller sharing one rate limit bucket. Both are worth a loud log line.
static void WarnOnMisconfiguration(WebApplication application)
{
    var logger = application.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var settings = application.Services.GetRequiredService<IOptions<OpenDataOptions>>().Value;
    var store = application.Services.GetRequiredService<IRateLimitStore>();

    if (settings.RateLimit.Enabled && !store.IsDistributed)
    {
        if (settings.RateLimit.EdgeBurstProtection)
        {
            logger.LogInformation(
                "Burst limiting is delegated to the edge; in-process counters cover the daily "
                + "window and the X-RateLimit headers, and are approximate once the platform "
                + "runs more than one instance.");
        }
        else
        {
            logger.LogWarning(
                "Rate limit counters are per-process and nothing limits bursts ahead of the "
                + "application. Configure an edge rule and set OpenData:RateLimit:"
                + "EdgeBurstProtection, or set OpenData:RateLimit:RedisConnectionString.");
        }
    }

    if (!settings.Proxy.IsBehindProxy)
    {
        logger.LogInformation(
            "No proxy provider configured; rate limits partition on the peer address. Behind a "
            + "CDN this collapses all traffic into one bucket — set OpenData:Proxy:Provider.");
    }
}

/// <summary>Exposed so the integration tests can boot the real application.</summary>
public partial class Program;
