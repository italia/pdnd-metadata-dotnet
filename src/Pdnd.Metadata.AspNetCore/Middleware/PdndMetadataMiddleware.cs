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
        if (!context.Items.ContainsKey(PdndMetadataAspNetCoreConstants.HttpContextItemKey))
        {
            try
            {
                var requestContext = HttpContextPdndRequestContextMapper.Map(context);
                var snapshot = extractor.Extract(requestContext, options.Value);

                context.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] = snapshot;
            }
            catch (Exception ex)
            {
                // Fail-soft: log the error and continue with an empty metadata snapshot
                _logger?.LogWarning(ex, "Failed to extract PDND metadata from request. Continuing with empty metadata.");
                context.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] = new PdndCallerMetadata();
            }
        }

        await _next(context);
    }
}
