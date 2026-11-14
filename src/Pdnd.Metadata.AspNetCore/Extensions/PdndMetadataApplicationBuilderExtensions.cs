// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pdnd.Metadata.AspNetCore.Constants;
using Pdnd.Metadata.AspNetCore.Middleware;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Models;
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
    /// <para>
    /// The extractor and the options are resolved per request from
    /// <see cref="HttpContext.RequestServices"/>, as convention-based middleware does, so scoped
    /// registrations and per-tenant containers keep working.
    /// </para>
    /// <para>
    /// When no <see cref="IPdndMetadataExtractor"/> is registered (<c>AddPdndMetadata()</c> was not
    /// called), the default extractor runs with the options found in the container, and a
    /// warning is logged once. When a registered extractor or the options fail to resolve, the
    /// error is logged and the request proceeds with an empty snapshot: falling back to the
    /// defaults would silently drop the operator's exclusions and capture restrictions.
    /// </para>
    /// </remarks>
    /// <param name="app">Application builder.</param>
    /// <returns>The same application builder.</returns>
    public static IApplicationBuilder UsePdndMetadata(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var logger = app.ApplicationServices.GetService<ILoggerFactory>()?.CreateLogger<PdndMetadataMiddleware>();
        var fallbackExtractor = new DefaultPdndMetadataExtractor();
        var fallbackOptions = MsOptions.Create(new PdndMetadataOptions());
        var warnedMissing = 0;
        var resolutionFailures = 0;

        return app.Use(next =>
        {
            var middleware = new PdndMetadataMiddleware(next, logger);

            return context =>
            {
                IPdndMetadataExtractor? extractor;
                IOptions<PdndMetadataOptions>? options;

                try
                {
                    extractor = context.RequestServices?.GetService<IPdndMetadataExtractor>();
                    options = context.RequestServices?.GetService<IOptions<PdndMetadataOptions>>();
                }
                catch (Exception ex)
                {
                    // Registered but broken: do not substitute the defaults, which would ignore
                    // the configured exclusions and capture restrictions. Skip extraction.
                    Log(logger, ref resolutionFailures, ex,
                        "Failed to resolve the PDND metadata services. Continuing with empty metadata.");
                    context.Items.TryAdd(PdndMetadataAspNetCoreConstants.HttpContextItemKey, new PdndCallerMetadata());
                    return next(context);
                }

                if (extractor is null && Interlocked.Exchange(ref warnedMissing, 1) == 0)
                {
                    Log(logger, ref warnedMissing, null,
                        "UsePdndMetadata() found no IPdndMetadataExtractor; call services.AddPdndMetadata(). " +
                        "Extraction runs with the default extractor.");
                }

                // Options are absent only when no options infrastructure is registered at all,
                // in which case nothing could have configured them: the defaults are faithful.
                return middleware.InvokeAsync(context, extractor ?? fallbackExtractor, options ?? fallbackOptions);
            };
        });
    }

    /// <summary>
    /// Logs a warning the first time (with the exception, if any), debug entries afterwards, and
    /// never lets a failing logger fail the request.
    /// </summary>
    private static void Log(ILogger? logger, ref int counter, Exception? exception, string message)
    {
        if (logger is null)
            return;

        try
        {
            if (exception is null || Interlocked.Increment(ref counter) == 1)
                logger.LogWarning(exception, message);
            else
                logger.LogDebug("{Message} ({Reason})", message, exception.Message);
        }
        catch
        {
            // Fail-soft.
        }
    }
}
