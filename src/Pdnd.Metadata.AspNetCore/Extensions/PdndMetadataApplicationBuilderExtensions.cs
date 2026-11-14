// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    /// When <c>AddPdndMetadata()</c> has not been called, the middleware still runs, with the
    /// default extractor and default options, and a warning is logged once at startup. Resolving
    /// the missing services per request would otherwise fail every request.
    /// </remarks>
    /// <param name="app">Application builder.</param>
    /// <returns>The same application builder.</returns>
    public static IApplicationBuilder UsePdndMetadata(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Ask whether the extractor is registered without resolving it. Resolving from the root
        // provider would fail at startup for a scoped extractor when scope validation is on
        // (the Development default), and would capture it as a singleton otherwise.
        // A container that cannot answer is assumed to have it, as in 2.0.0.
        var registrations = app.ApplicationServices.GetService<IServiceProviderIsService>();
        if (registrations is null || registrations.IsService(typeof(IPdndMetadataExtractor)))
            return app.UseMiddleware<PdndMetadataMiddleware>();

        var logger = app.ApplicationServices.GetService<ILoggerFactory>()?.CreateLogger<PdndMetadataMiddleware>();
        logger?.LogWarning(
            "UsePdndMetadata() was called without services.AddPdndMetadata(). " +
            "Extraction runs with default options; call AddPdndMetadata() to configure it.");

        var extractor = new DefaultPdndMetadataExtractor();
        var options = MsOptions.Create(new PdndMetadataOptions());

        return app.Use(next =>
        {
            var middleware = new PdndMetadataMiddleware(next, logger);
            return context => middleware.InvokeAsync(context, extractor, options);
        });
    }
}
