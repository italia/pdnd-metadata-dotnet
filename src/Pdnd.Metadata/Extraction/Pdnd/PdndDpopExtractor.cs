// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Text.Json;
using Pdnd.Metadata.Extraction.Jwt;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.Extraction.Pdnd;

/// <summary>
/// Extracts best-effort fields from the DPoP proof header (JWS/JWT-like token).
/// </summary>
public static class PdndDpopExtractor
{
    /// <summary>
    /// Extracts header fields (alg/kid/typ) and selected payload claims (htm/htu/jti/iat) if present.
    /// This method is fail-soft and never throws.
    /// </summary>
    /// <param name="metadata">Target metadata snapshot.</param>
    /// <param name="token">Decoded DPoP proof parts.</param>
    public static void Extract(PdndCallerMetadata metadata, JwtParts token)
    {
        try
        {
            using (var headerDoc = JsonDocument.Parse(token.HeaderJson))
            {
                var h = headerDoc.RootElement;

                AddHeaderIfPresent(metadata, h, "alg", PdndMetadataKeys.PdndDpopAlg);
                AddHeaderIfPresent(metadata, h, "kid", PdndMetadataKeys.PdndDpopKid);
                AddHeaderIfPresent(metadata, h, "typ", PdndMetadataKeys.PdndDpopTyp);
            }

            using (var payloadDoc = JsonDocument.Parse(token.PayloadJson))
            {
                var p = payloadDoc.RootElement;

                AddPayloadIfPresent(metadata, p, "htm", PdndMetadataKeys.PdndDpopHtm);
                AddPayloadIfPresent(metadata, p, "htu", PdndMetadataKeys.PdndDpopHtu);
                AddPayloadIfPresent(metadata, p, "jti", PdndMetadataKeys.PdndDpopJti);
                AddPayloadIfPresent(metadata, p, "iat", PdndMetadataKeys.PdndDpopIat);
                AddPayloadIfPresent(metadata, p, "exp", PdndMetadataKeys.PdndDpopExp);
                AddPayloadIfPresent(metadata, p, "ath", PdndMetadataKeys.PdndDpopAth);
                AddPayloadIfPresent(metadata, p, "nonce", PdndMetadataKeys.PdndDpopNonce);
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