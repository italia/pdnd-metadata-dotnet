// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pdnd.Metadata.AspNetCore.Constants;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.Diagnostics;

/// <summary>
/// Records PDND instrumentation for each request that carries a metadata snapshot.
/// </summary>
public sealed class PdndDiagnosticsMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of <see cref="PdndDiagnosticsMiddleware"/>.
    /// </summary>
    /// <param name="next">Next middleware in the pipeline.</param>
    public PdndDiagnosticsMiddleware(RequestDelegate next) => _next = next;

    /// <summary>
    /// Executes the middleware. Uses the <see cref="PdndDiagnosticsOptions"/> registered with
    /// <c>AddPdndDiagnostics()</c> when present, and process-wide defaults otherwise.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Items.TryGetValue(PdndMetadataAspNetCoreConstants.HttpContextItemKey, out var value) &&
            value is PdndCallerMetadata metadata)
        {
            // IOptions<T> is a singleton, so the distinct-value bound held by the options
            // instance is shared across requests as intended.
            var options = context.RequestServices?.GetService<IOptions<PdndDiagnosticsOptions>>()?.Value;

            if (options is null)
                PdndInstrumentation.Record(metadata);
            else
                PdndInstrumentation.Record(metadata, options);
        }

        await _next(context);
    }
}

/// <summary>
/// Pipeline registration for PDND instrumentation.
/// </summary>
public static class PdndDiagnosticsApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the PDND instrumentation middleware. Register it after <c>UsePdndMetadata()</c>,
    /// since it reads the snapshot that middleware produces.
    /// </summary>
    /// <param name="app">Application builder.</param>
    /// <returns>The same application builder.</returns>
    public static IApplicationBuilder UsePdndDiagnostics(this IApplicationBuilder app)
        => app.UseMiddleware<PdndDiagnosticsMiddleware>();
}

/// <summary>
/// DI registration for PDND instrumentation.
/// </summary>
public static class PdndDiagnosticsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="PdndDiagnosticsOptions"/>. Optional: without it the middleware
    /// uses the defaults.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Optional options configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddPdndDiagnostics(
        this IServiceCollection services,
        Action<PdndDiagnosticsOptions>? configure = null)
    {
        if (configure is not null)
            services.Configure(configure);
        else
            services.AddOptions<PdndDiagnosticsOptions>();

        return services;
    }
}
