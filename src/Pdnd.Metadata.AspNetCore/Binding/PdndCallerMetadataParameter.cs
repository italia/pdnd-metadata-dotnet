// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Pdnd.Metadata.AspNetCore.Access;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.AspNetCore.Binding;

/// <summary>
/// Minimal API parameter wrapper for PDND metadata.
/// Usage:
/// <code>
/// app.MapGet("/x", (PdndCallerMetadataParameter pdnd) =&gt; Results.Ok(pdnd.Value));
/// </code>
/// </summary>
public readonly struct PdndCallerMetadataParameter
{
    /// <summary>
    /// Gets the extracted metadata snapshot.
    /// </summary>
    public PdndCallerMetadata Value { get; }

    private PdndCallerMetadataParameter(PdndCallerMetadata value) => Value = value;

    /// <summary>
    /// Minimal API binder entry point.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="parameter">Parameter information.</param>
    /// <returns>The bound metadata snapshot wrapper.</returns>
    public static ValueTask<PdndCallerMetadataParameter> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var accessor = context.RequestServices.GetRequiredService<IPdndMetadataAccessor>();
        var md = accessor.Current ?? new PdndCallerMetadata();

        return ValueTask.FromResult(new PdndCallerMetadataParameter(md));
    }
}