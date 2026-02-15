// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Http;
using Pdnd.Metadata.AspNetCore.Constants;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.AspNetCore.Access;

/// <summary>
/// Default accessor based on <see cref="IHttpContextAccessor"/>.
/// </summary>
public sealed class PdndMetadataAccessor : IPdndMetadataAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of <see cref="PdndMetadataAccessor"/>.
    /// </summary>
    /// <param name="httpContextAccessor">The ASP.NET Core HTTP context accessor.</param>
    public PdndMetadataAccessor(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    /// <inheritdoc />
    public PdndCallerMetadata? Current
    {
        get
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx is null) return null;

            return ctx.Items.TryGetValue(PdndMetadataAspNetCoreConstants.HttpContextItemKey, out var obj)
                ? obj as PdndCallerMetadata
                : null;
        }
    }
}