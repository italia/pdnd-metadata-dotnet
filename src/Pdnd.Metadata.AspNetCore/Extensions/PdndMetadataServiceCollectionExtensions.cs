// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.Extensions.DependencyInjection;
using Pdnd.Metadata.AspNetCore.Access;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Options;

namespace Pdnd.Metadata.AspNetCore.Extensions;

/// <summary>
/// DI extensions for PDND metadata extraction.
/// </summary>
public static class PdndMetadataServiceCollectionExtensions
{
    /// <summary>
    /// Registers PDND metadata services (extractor, options, accessor).
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Optional options configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddPdndMetadata(
        this IServiceCollection services,
        Action<PdndMetadataOptions>? configure = null)
    {
        services.AddHttpContextAccessor();

        if (configure is not null)
            services.Configure(configure);
        else
            services.AddOptions<PdndMetadataOptions>();

        services.AddSingleton<IPdndMetadataExtractor, DefaultPdndMetadataExtractor>();
        services.AddScoped<IPdndMetadataAccessor, PdndMetadataAccessor>();

        return services;
    }
}
