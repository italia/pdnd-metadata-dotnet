// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Mvc;
using Pdnd.Metadata.AspNetCore.Access;
using Pdnd.Metadata.AspNetCore.Binding;
using Pdnd.Metadata.AspNetCore.Constants;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.SampleApi.Controllers;

[ApiController]
[Route("controller/metadata")]
public sealed class MetadataController : ControllerBase
{
    // 1) Access via DI (accessor) - explicit, works everywhere
    [HttpGet("accessor")]
    public IActionResult GetViaAccessor([FromServices] IPdndMetadataAccessor accessor)
    {
        var md = accessor.Current;
        return Ok(ToPayload(md));
    }

    // 2) Access via MVC model binding - most convenient for Controllers
    [HttpGet("binder")]
    public IActionResult GetViaBinder([FromPdndMetadata] PdndCallerMetadata md)
        => Ok(ToPayload(md));

    // 3) Access directly from HttpContext.Items - useful for debugging/understanding the internals
    [HttpGet("httpcontext")]
    public IActionResult GetViaHttpContextItems()
    {
        var md =
            HttpContext.Items.TryGetValue(PdndMetadataAspNetCoreConstants.HttpContextItemKey, out var obj)
                ? obj as PdndCallerMetadata
                : null;

        return Ok(ToPayload(md));
    }

    // Flattened view: shows a curated set of canonical keys (handy for quick inspection)
    [HttpGet("flat")]
    public IActionResult GetFlat([FromPdndMetadata] PdndCallerMetadata md)
        => Ok(new
        {
            createdAtUtc = md.CreatedAtUtc,
            correlationId = md.GetFirstValue(PdndMetadataKeys.CorrelationId),
            // requestId = md.GetFirstValue("request.id"), // enable if you added the RequestId key
            traceparent = md.GetFirstValue(PdndMetadataKeys.TraceParent),
            remoteIp = md.GetFirstValue(PdndMetadataKeys.NetRemoteIp),
            forwardedFor = md.GetFirstValue(PdndMetadataKeys.NetForwardedFor),

            // PDND voucher (best-effort decoded)
            pdndVoucherIss = md.GetFirstValue(PdndMetadataKeys.PdndVoucherIss),
            pdndVoucherSub = md.GetFirstValue(PdndMetadataKeys.PdndVoucherSub),
            pdndPurposeId = md.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId),
            pdndClientId = md.GetFirstValue(PdndMetadataKeys.PdndVoucherClientId),

            // Tracking evidence (best-effort decoded)
            pdndTrackingAlg = md.GetFirstValue(PdndMetadataKeys.PdndTrackingAlg),
            pdndTrackingIss = md.GetFirstValue(PdndMetadataKeys.PdndTrackingIss),
            pdndTrackingSub = md.GetFirstValue(PdndMetadataKeys.PdndTrackingSub),

            // DPoP (best-effort decoded)
            pdndDpopHtm = md.GetFirstValue(PdndMetadataKeys.PdndDpopHtm),
            pdndDpopHtu = md.GetFirstValue(PdndMetadataKeys.PdndDpopHtu),

            // Digest (best-effort parsed)
            pdndDigestAlg = md.GetFirstValue(PdndMetadataKeys.PdndDigestAlg),
            pdndDigestValue = md.GetFirstValue(PdndMetadataKeys.PdndDigestValue),
        });

    private static object ToPayload(PdndCallerMetadata? md)
    {
        // If middleware did not run (or services are not wired), accessor/items may be empty
        if (md is null)
            return new { message = "No metadata snapshot available (middleware not executed?)" };

        // Default view: includes a few key fields + the full items dictionary
        return new
        {
            createdAtUtc = md.CreatedAtUtc,
            correlationId = md.GetFirstValue(PdndMetadataKeys.CorrelationId),
            remoteIp = md.GetFirstValue(PdndMetadataKeys.NetRemoteIp),
            forwardedFor = md.GetFirstValue(PdndMetadataKeys.NetForwardedFor),
            traceparent = md.GetFirstValue(PdndMetadataKeys.TraceParent),

            pdndVoucherIss = md.GetFirstValue(PdndMetadataKeys.PdndVoucherIss),
            pdndPurposeId = md.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId),
            pdndTrackingAlg = md.GetFirstValue(PdndMetadataKeys.PdndTrackingAlg),
            pdndDigestAlg = md.GetFirstValue(PdndMetadataKeys.PdndDigestAlg),

            items = md.Items
        };
    }
}