using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace YaMiSoFt.OpenData.Api.Tests;

/// <summary>
/// Boots the real application in-process.
/// </summary>
/// <remarks>
/// Rate limiting is off by default so that a test asserting on response shape does not fail
/// because an earlier test in the same class spent the allowance. The one test that is about
/// limiting turns it back on with its own tight settings.
/// </remarks>
public sealed class OpenDataApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _overrides = new(StringComparer.Ordinal)
    {
        ["OpenData:RateLimit:Enabled"] = "false",
    };

    /// <summary>Applies a configuration override before the host is built.</summary>
    public OpenDataApiFactory With(string key, string value)
    {
        _overrides[key] = value;
        return this;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            // The datasets are copied next to the test assembly, so point the app at them
            // rather than relying on the content root the test host infers.
            _overrides["OpenData:DataDirectory"] = Path.Combine(AppContext.BaseDirectory, "data");
            configuration.AddInMemoryCollection(_overrides);
        });
    }
}
