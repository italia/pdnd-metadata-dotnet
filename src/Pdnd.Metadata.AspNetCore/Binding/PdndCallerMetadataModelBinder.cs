// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Pdnd.Metadata.AspNetCore.Access;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.AspNetCore.Binding;

/// <summary>
/// Model binder that resolves <see cref="PdndCallerMetadata"/> from the current request's metadata snapshot.
/// </summary>
public sealed class PdndCallerMetadataModelBinder : IModelBinder
{
    /// <inheritdoc />
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var accessor = bindingContext.HttpContext.RequestServices
            .GetRequiredService<IPdndMetadataAccessor>();

        var md = accessor.Current ?? new PdndCallerMetadata();

        bindingContext.Result = ModelBindingResult.Success(md);
        return Task.CompletedTask;
    }
}