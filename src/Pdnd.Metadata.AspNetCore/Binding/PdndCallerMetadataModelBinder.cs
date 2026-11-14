// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Mvc.ModelBinding;
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

        bindingContext.Result = ModelBindingResult.Success(
            PdndSnapshotResolver.Resolve(bindingContext.HttpContext));
        return Task.CompletedTask;
    }
}