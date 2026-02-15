// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Builder;
using Pdnd.Metadata.AspNetCore.Middleware;

namespace Pdnd.Metadata.AspNetCore.Extensions;

/// <summary>
/// Application builder extensions for PDND metadata extraction.
/// </summary>
public static class PdndMetadataApplicationBuilderExtensions
{
    /// <summary>
    /// Enables PDND metadata extraction middleware.
    /// </summary>
    /// <param name="app">Application builder.</param>
    /// <returns>The same application builder.</returns>
    public static IApplicationBuilder UsePdndMetadata(this IApplicationBuilder app)
        => app.UseMiddleware<PdndMetadataMiddleware>();
}