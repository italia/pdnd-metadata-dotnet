// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pdnd.Metadata.AspNetCore.Constants;
using Pdnd.Metadata.AspNetCore.Mapping;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Models;
using Pdnd.Metadata.Options;

namespace Pdnd.Metadata.AspNetCore.Middleware;

/// <summary>
/// Middleware that extracts caller metadata and stores it into <see cref="HttpContext.Items"/>.
/// Extraction failures are handled gracefully (fail-soft) to avoid blocking requests.
/// </summary>
public sealed class PdndMetadataMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PdndMetadataMiddleware>? _logger;

    // A failure that recurs on every request must not flood the log: the first occurrence is a
    // warning with the exception, the following ones are debug entries without it.
    private int _failures;

    /// <summary>
    /// Initializes a new instance of <see cref="PdndMetadataMiddleware"/>.
    /// </summary>
    /// <param name="next">Next middleware in the pipeline.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public PdndMetadataMiddleware(RequestDelegate next, ILogger<PdndMetadataMiddleware>? logger = null)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Executes the middleware.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="extractor">Metadata extractor.</param>
    /// <param name="options">Extraction options.</param>
    public async Task InvokeAsync(
        HttpContext context,
        IPdndMetadataExtractor extractor,
        IOptions<PdndMetadataOptions> options)
    {
        // Everything before _next is fail-soft, including reading the options: a configure
        // delegate that throws would otherwise fail every request rather than none.
        try
        {
            var settings = options.Value;

            if (!settings.IsPathExcluded(context.Request.Path.Value) &&
                !context.Items.ContainsKey(PdndMetadataAspNetCoreConstants.HttpContextItemKey))
            {
                var requestContext = HttpContextPdndRequestContextMapper.Map(context);
                context.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] =
                    extractor.Extract(requestContext, settings);
            }
        }
        catch (Exception ex)
        {
            LogFailure(ex);
            context.Items.TryAdd(PdndMetadataAspNetCoreConstants.HttpContextItemKey, new PdndCallerMetadata());
        }

        // Outside the try: exceptions from the rest of the pipeline are not ours to swallow.
        await _next(context);
    }

    private void LogFailure(Exception ex)
    {
        if (_logger is null)
            return;

        try
        {
            if (Interlocked.Increment(ref _failures) == 1)
                _logger.LogWarning(ex, "Failed to extract PDND metadata from request. Continuing with empty metadata.");
            else
                _logger.LogDebug("Failed to extract PDND metadata from request ({Message}). Continuing with empty metadata.", ex.Message);
        }
        catch
        {
            // A failing logger must not turn a fail-soft path into a failed request.
        }
    }
}
