// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Text.Json;
using Pdnd.Metadata.Extraction.Jwt;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.Extraction.Pdnd;

/// <summary>
/// Extracts best-effort fields from the Agid-JWT-Signature header (JWS/JWT-like token).
/// Used in PDND flows for request signing (integrity).
/// </summary>
public static class PdndSignatureExtractor
{
    /// <summary>
    /// Extracts header fields (alg/kid/typ) and selected payload claims if present.
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

                AddHeaderIfPresent(metadata, h, "alg", PdndMetadataKeys.PdndSignatureAlg);
                AddHeaderIfPresent(metadata, h, "kid", PdndMetadataKeys.PdndSignatureKid);
                AddHeaderIfPresent(metadata, h, "typ", PdndMetadataKeys.PdndSignatureTyp);
            }

            using (var payloadDoc = JsonDocument.Parse(token.PayloadJson))
            {
                var p = payloadDoc.RootElement;

                AddPayloadIfPresent(metadata, p, "iss", PdndMetadataKeys.PdndSignatureIss);
                AddPayloadIfPresent(metadata, p, "sub", PdndMetadataKeys.PdndSignatureSub);
                AddPayloadIfPresent(metadata, p, "jti", PdndMetadataKeys.PdndSignatureJti);

                if (JwtJsonReader.TryReadAudience(p, out var aud) && !string.IsNullOrWhiteSpace(aud))
                    metadata.Add(PdndMetadataKeys.PdndSignatureAud, aud!, PdndMetadataSource.Claims);

                AddPayloadIfPresent(metadata, p, "iat", PdndMetadataKeys.PdndSignatureIat);
                AddPayloadIfPresent(metadata, p, "exp", PdndMetadataKeys.PdndSignatureExp);
                AddPayloadIfPresent(metadata, p, "signed_headers", PdndMetadataKeys.PdndSignatureSignedHeaders);
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
