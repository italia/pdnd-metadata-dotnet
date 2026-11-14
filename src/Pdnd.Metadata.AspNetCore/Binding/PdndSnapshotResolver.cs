// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Pdnd.Metadata.AspNetCore.Access;
using Pdnd.Metadata.AspNetCore.Constants;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.AspNetCore.Binding;

/// <summary>
/// Resolves the current snapshot for the binding entry points without ever failing the request.
/// </summary>
internal static class PdndSnapshotResolver
{
    /// <summary>
    /// Returns, in order: what a registered <see cref="IPdndMetadataAccessor"/> reports, the
    /// snapshot stored in <see cref="HttpContext.Items"/>, or an empty snapshot.
    /// </summary>
    /// <remarks>
    /// The accessor is consulted first so that a custom implementation keeps working. It is
    /// optional: binding an endpoint parameter must not turn a missing <c>AddPdndMetadata()</c>
    /// into a 500 response.
    /// </remarks>
    internal static PdndCallerMetadata Resolve(HttpContext context)
    {
        try
        {
            var accessor = context.RequestServices?.GetService<IPdndMetadataAccessor>();
            if (accessor?.Current is { } current)
                return current;
        }
        catch
        {
            // A faulty custom accessor falls through to the snapshot in Items.
        }

        return context.Items.TryGetValue(PdndMetadataAspNetCoreConstants.HttpContextItemKey, out var value)
               && value is PdndCallerMetadata snapshot
            ? snapshot
            : new PdndCallerMetadata();
    }
}
