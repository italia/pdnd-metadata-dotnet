// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Text.Json;
using Pdnd.Metadata.Extraction.Jwt;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.Extraction.Pdnd;

/// <summary>
/// Extracts best-effort fields from PDND Tracking Evidence (JWS/JWT-like token).
/// </summary>
public static class PdndTrackingEvidenceExtractor
{
    /// <summary>
    /// Extracts header fields (alg/kid/typ) and selected payload claims (iss/sub/jti) if present.
    /// This method is fail-soft and never throws.
    /// </summary>
    /// <param name="metadata">Target metadata snapshot.</param>
    /// <param name="token">Decoded token parts.</param>
    public static void Extract(PdndCallerMetadata metadata, JwtParts token)
    {
        try
        {
            using (var headerDoc = JsonDocument.Parse(token.HeaderJson))
            {
                var h = headerDoc.RootElement;

                AddHeaderIfPresent(metadata, h, "alg", PdndMetadataKeys.PdndTrackingAlg);
                AddHeaderIfPresent(metadata, h, "kid", PdndMetadataKeys.PdndTrackingKid);
                AddHeaderIfPresent(metadata, h, "typ", PdndMetadataKeys.PdndTrackingTyp);
            }

            using (var payloadDoc = JsonDocument.Parse(token.PayloadJson))
            {
                var p = payloadDoc.RootElement;

                AddPayloadIfPresent(metadata, p, "iss", PdndMetadataKeys.PdndTrackingIss);
                AddPayloadIfPresent(metadata, p, "sub", PdndMetadataKeys.PdndTrackingSub);
                AddPayloadIfPresent(metadata, p, "jti", PdndMetadataKeys.PdndTrackingJti);
            }
        }
        catch
        {
            // Fail-soft
        }
    }

    private static void AddHeaderIfPresent(PdndCallerMetadata md, JsonElement root, string name, string key)
    {
        if (JwtJsonReader.TryReadString(root, name, out var value) && !string.IsNullOrWhiteSpace(value))
            md.Add(key, value!, PdndMetadataSource.Header);
    }

    private static void AddPayloadIfPresent(PdndCallerMetadata md, JsonElement root, string name, string key)
    {
        if (JwtJsonReader.TryReadString(root, name, out var value) && !string.IsNullOrWhiteSpace(value))
            md.Add(key, value!, PdndMetadataSource.Claims);
    }
}