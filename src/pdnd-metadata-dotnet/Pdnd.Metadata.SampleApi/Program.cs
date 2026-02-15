// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Pdnd.Metadata.AspNetCore.Binding;
using Pdnd.Metadata.AspNetCore.Extensions;
using Pdnd.Metadata.Extraction;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddPdndMetadata(options =>
{
    // Generic behavior
    options.CaptureAllHeaders = true;

    // Keep secrets out (Authorization/Cookies are already denied by default)
    options.HeaderDenyList.Add("X-Api-Key"); // example

    // PDND parsing (best-effort, no validation)
    options.ParsePdndVoucherFromAuthorizationBearer = true;
    options.ParsePdndTrackingEvidence = true;
    options.ParseDpopHeader = true;
    options.ParseDigestHeader = true;

    // Recommended defaults: do not store signed blobs as raw headers
    options.CaptureRawTrackingEvidenceHeader = false;
    options.CaptureRawDpopHeader = false;

    // Digest is less sensitive; keep raw if you want (or set false)
    options.CaptureRawDigestHeader = true;

    // Guard-rail
    options.MaxTokenLength = 16_384;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UsePdndMetadata();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.RoutePrefix = "swagger"; });
}

app.MapControllers();

app.MapGet("/minimal/metadata", (PdndCallerMetadataParameter pdnd) =>
{
    var md = pdnd.Value;

    return Results.Ok(new
    {
        createdAtUtc = md.CreatedAtUtc,
        correlationId = md.GetFirstValue(PdndMetadataKeys.CorrelationId),
        remoteIp = md.GetFirstValue(PdndMetadataKeys.NetRemoteIp),
        forwardedFor = md.GetFirstValue(PdndMetadataKeys.NetForwardedFor),
        traceparent = md.GetFirstValue(PdndMetadataKeys.TraceParent),
        items = md.Items
    });
})
.WithName("GetPdndMetadata");

app.MapGet("/minimal/headers", (PdndCallerMetadataParameter pdnd) =>
{
    var md = pdnd.Value;

    var headers = md.Items
        .Where(kvp => kvp.Key.StartsWith(PdndMetadataKeys.HeaderPrefix, StringComparison.OrdinalIgnoreCase))
        .ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(x => new { x.Value, x.Source }).ToArray(),
            StringComparer.OrdinalIgnoreCase);

    return Results.Ok(headers);
})
.WithName("GetCapturedHeaders");

app.MapGet("/minimal/pdnd", (PdndCallerMetadataParameter pdnd) =>
{
    var md = pdnd.Value;

    return Results.Ok(new
    {
        voucher = new
        {
            iss = md.GetFirstValue(PdndMetadataKeys.PdndVoucherIss),
            sub = md.GetFirstValue(PdndMetadataKeys.PdndVoucherSub),
            aud = md.GetFirstValue(PdndMetadataKeys.PdndVoucherAud),
            jti = md.GetFirstValue(PdndMetadataKeys.PdndVoucherJti),
            iat = md.GetFirstValue(PdndMetadataKeys.PdndVoucherIat),
            nbf = md.GetFirstValue(PdndMetadataKeys.PdndVoucherNbf),
            exp = md.GetFirstValue(PdndMetadataKeys.PdndVoucherExp),
            purposeId = md.GetFirstValue(PdndMetadataKeys.PdndVoucherPurposeId),
            clientId = md.GetFirstValue(PdndMetadataKeys.PdndVoucherClientId)
        },
        trackingEvidence = new
        {
            alg = md.GetFirstValue(PdndMetadataKeys.PdndTrackingAlg),
            kid = md.GetFirstValue(PdndMetadataKeys.PdndTrackingKid),
            typ = md.GetFirstValue(PdndMetadataKeys.PdndTrackingTyp),
            iss = md.GetFirstValue(PdndMetadataKeys.PdndTrackingIss),
            sub = md.GetFirstValue(PdndMetadataKeys.PdndTrackingSub),
            jti = md.GetFirstValue(PdndMetadataKeys.PdndTrackingJti)
        },
        dpop = new
        {
            alg = md.GetFirstValue(PdndMetadataKeys.PdndDpopAlg),
            kid = md.GetFirstValue(PdndMetadataKeys.PdndDpopKid),
            typ = md.GetFirstValue(PdndMetadataKeys.PdndDpopTyp),
            htm = md.GetFirstValue(PdndMetadataKeys.PdndDpopHtm),
            htu = md.GetFirstValue(PdndMetadataKeys.PdndDpopHtu),
            jti = md.GetFirstValue(PdndMetadataKeys.PdndDpopJti),
            iat = md.GetFirstValue(PdndMetadataKeys.PdndDpopIat)
        },
        digest = new
        {
            alg = md.GetFirstValue(PdndMetadataKeys.PdndDigestAlg),
            value = md.GetFirstValue(PdndMetadataKeys.PdndDigestValue)
        }
    });
})
.WithName("GetPdndFields");

app.MapGet("/minimal/dpop", (PdndCallerMetadataParameter pdnd) =>
{
    var md = pdnd.Value;

    return Results.Ok(new
    {
        alg = md.GetFirstValue(PdndMetadataKeys.PdndDpopAlg),
        kid = md.GetFirstValue(PdndMetadataKeys.PdndDpopKid),
        typ = md.GetFirstValue(PdndMetadataKeys.PdndDpopTyp),
        htm = md.GetFirstValue(PdndMetadataKeys.PdndDpopHtm),
        htu = md.GetFirstValue(PdndMetadataKeys.PdndDpopHtu),
        jti = md.GetFirstValue(PdndMetadataKeys.PdndDpopJti),
        iat = md.GetFirstValue(PdndMetadataKeys.PdndDpopIat)
    });
})
.WithName("GetDpopFields");

app.MapGet("/minimal/sanity", (PdndCallerMetadataParameter pdnd) =>
{
    var md = pdnd.Value;

    var hasRawAuthorization = md.Items.Keys.Any(k =>
        k.Equals("http.header.authorization", StringComparison.OrdinalIgnoreCase));

    var hasRawDpop = md.Items.Keys.Any(k =>
        k.Equals("http.header.dpop", StringComparison.OrdinalIgnoreCase));

    var hasRawTrackingEvidence = md.Items.Keys.Any(k =>
        k.Equals("http.header.agid-jwt-tracking-evidence", StringComparison.OrdinalIgnoreCase) ||
        k.Equals("http.header.agid-jwt-trackingevidence", StringComparison.OrdinalIgnoreCase));

    return Results.Ok(new
    {
        ok = !hasRawAuthorization && !hasRawDpop && !hasRawTrackingEvidence,
        hasRawAuthorizationHeaderCaptured = hasRawAuthorization,
        hasRawDpopHeaderCaptured = hasRawDpop,
        hasRawTrackingEvidenceHeaderCaptured = hasRawTrackingEvidence
    });
})
.WithName("GetSanityChecks");

app.Run();