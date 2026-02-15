// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Pdnd.Metadata.AspNetCore.Constants;
using Pdnd.Metadata.AspNetCore.Mapping;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Options;

namespace Pdnd.Metadata.AspNetCore.Middleware;

/// <summary>
/// Middleware that extracts caller metadata and stores it into <see cref="HttpContext.Items"/>.
/// </summary>
public sealed class PdndMetadataMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of <see cref="PdndMetadataMiddleware"/>.
    /// </summary>
    /// <param name="next">Next middleware in the pipeline.</param>
    public PdndMetadataMiddleware(RequestDelegate next) => _next = next;

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
            var requestContext = HttpContextPdndRequestContextMapper.Map(context);
            var snapshot = extractor.Extract(requestContext, options.Value);

            context.Items[PdndMetadataAspNetCoreConstants.HttpContextItemKey] = snapshot;
        }

        await _next(context);
    }
}