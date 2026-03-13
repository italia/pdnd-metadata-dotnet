// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Mvc;

namespace Pdnd.Metadata.AspNetCore.Binding;

/// <summary>
/// Binds a controller action parameter to the current request's <see cref="Pdnd.Metadata.Models.PdndCallerMetadata"/> snapshot.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromPdndMetadataAttribute : ModelBinderAttribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="FromPdndMetadataAttribute"/>.
    /// </summary>
    public FromPdndMetadataAttribute() : base(typeof(PdndCallerMetadataModelBinder))
    {
    }
}