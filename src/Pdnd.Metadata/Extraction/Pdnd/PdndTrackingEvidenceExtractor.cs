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
    /// Extracts header fields (alg/kid/typ), registered claims, and the AUDIT_REST audit claims
    /// (userID, userLocation, LoA, purposeId, dnonce) if present.
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

                if (JwtJsonReader.TryReadAudience(p, out var aud) && !string.IsNullOrWhiteSpace(aud))
                    metadata.Add(PdndMetadataKeys.PdndTrackingAud, aud!, PdndMetadataSource.Claims);

                AddPayloadIfPresent(metadata, p, "iat", PdndMetadataKeys.PdndTrackingIat);
                AddPayloadIfPresent(metadata, p, "nbf", PdndMetadataKeys.PdndTrackingNbf);
                AddPayloadIfPresent(metadata, p, "exp", PdndMetadataKeys.PdndTrackingExp);

                // AUDIT_REST_01/02 audit claims. The set is agreed between fruitore and
                // erogatore: userID, userLocation and LoA are the ones the AgID guidelines
                // suggest; purposeId and dnonce appear in published examples.
                AddPayloadIfPresent(metadata, p, "userID", PdndMetadataKeys.PdndTrackingUserId);
                AddPayloadIfPresent(metadata, p, "userLocation", PdndMetadataKeys.PdndTrackingUserLocation);
                AddPayloadIfPresent(metadata, p, "LoA", PdndMetadataKeys.PdndTrackingLoA);
                AddPayloadIfPresent(metadata, p, "purposeId", PdndMetadataKeys.PdndTrackingPurposeId);
                AddPayloadIfPresent(metadata, p, "dnonce", PdndMetadataKeys.PdndTrackingDnonce);
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