// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
namespace Pdnd.Metadata.AspNetCore.Constants;

/// <summary>
/// Constants used by the ASP.NET Core integration.
/// </summary>
public static class PdndMetadataAspNetCoreConstants
{
    /// <summary>
    /// HttpContext.Items key used to store the per-request metadata snapshot.
    /// </summary>
    public const string HttpContextItemKey = "__pdnd.metadata.snapshot__";
}