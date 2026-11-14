// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pdnd.Metadata.AspNetCore.Middleware;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Options;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Pdnd.Metadata.AspNetCore.Extensions;

/// <summary>
/// Application builder extensions for PDND metadata extraction.
/// </summary>
public static class PdndMetadataApplicationBuilderExtensions
{
    /// <summary>
    /// Enables PDND metadata extraction middleware.
    /// </summary>
    /// <remarks>
    /// The extractor and the options are resolved per request from
    /// <see cref="HttpContext.RequestServices"/>, as convention-based middleware does, so scoped
    /// registrations and per-tenant containers keep working. When <c>AddPdndMetadata()</c> has
    /// not been called, the middleware runs with the default extractor and default options and
    /// logs a warning once, instead of failing every request.
    /// </remarks>
    /// <param name="app">Application builder.</param>
    /// <returns>The same application builder.</returns>
    public static IApplicationBuilder UsePdndMetadata(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var logger = app.ApplicationServices.GetService<ILoggerFactory>()?.CreateLogger<PdndMetadataMiddleware>();
        var fallbackExtractor = new DefaultPdndMetadataExtractor();
        var fallbackOptions = MsOptions.Create(new PdndMetadataOptions());
        var warned = 0;

        return app.Use(next =>
        {
            var middleware = new PdndMetadataMiddleware(next, logger);

            return context =>
            {
                IPdndMetadataExtractor? extractor = null;
                IOptions<PdndMetadataOptions>? options = null;

                try
                {
                    extractor = context.RequestServices?.GetService<IPdndMetadataExtractor>();
                    options = context.RequestServices?.GetService<IOptions<PdndMetadataOptions>>();
                }
                catch
                {
                    // A container that fails to resolve falls back to the defaults below.
                }

                if (extractor is null && Interlocked.Exchange(ref warned, 1) == 0)
                {
                    try
                    {
                        logger?.LogWarning(
                            "UsePdndMetadata() found no IPdndMetadataExtractor; call services.AddPdndMetadata(). " +
                            "Extraction runs with the default extractor and default options.");
                    }
                    catch
                    {
                        // A failing logger must not fail the request.
                    }
                }

                return middleware.InvokeAsync(context, extractor ?? fallbackExtractor, options ?? fallbackOptions);
            };
        });
    }
}
