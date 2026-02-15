// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Mvc;

namespace Pdnd.Metadata.AspNetCore.Binding;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromPdndMetadataAttribute : ModelBinderAttribute
{
    public FromPdndMetadataAttribute() : base(typeof(PdndCallerMetadataModelBinder))
    {
    }
}