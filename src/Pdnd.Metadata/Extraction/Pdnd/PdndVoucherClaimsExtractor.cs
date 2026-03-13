// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Text.Json;
using Pdnd.Metadata.Extraction.Jwt;
using Pdnd.Metadata.Models;

namespace Pdnd.Metadata.Extraction.Pdnd;

/// <summary>
/// Extracts PDND voucher-related fields from a decoded JWT (best-effort, no validation).
/// </summary>
public static class PdndVoucherClaimsExtractor
{
    /// <summary>
    /// Extracts known voucher fields and stores them into metadata using canonical PDND keys.
    /// This method is fail-soft and never throws.
    /// </summary>
    /// <param name="metadata">Target metadata snapshot.</param>
    /// <param name="jwt">Decoded JWT parts.</param>
    public static void Extract(PdndCallerMetadata metadata, JwtParts jwt)
    {
        try
        {
            using var payloadDoc = JsonDocument.Parse(jwt.PayloadJson);
            var root = payloadDoc.RootElement;

            AddIfPresent(metadata, root, "iss", PdndMetadataKeys.PdndVoucherIss);
            AddIfPresent(metadata, root, "sub", PdndMetadataKeys.PdndVoucherSub);

            if (JwtJsonReader.TryReadAudience(root, out var aud) && !string.IsNullOrWhiteSpace(aud))
                metadata.Add(PdndMetadataKeys.PdndVoucherAud, aud!, PdndMetadataSource.Claims);

            AddIfPresent(metadata, root, "jti", PdndMetadataKeys.PdndVoucherJti);

            AddIfPresent(metadata, root, "iat", PdndMetadataKeys.PdndVoucherIat);
            AddIfPresent(metadata, root, "nbf", PdndMetadataKeys.PdndVoucherNbf);
            AddIfPresent(metadata, root, "exp", PdndMetadataKeys.PdndVoucherExp);

            AddIfPresent(metadata, root, "purposeId", PdndMetadataKeys.PdndVoucherPurposeId);
            AddIfPresent(metadata, root, "clientId", PdndMetadataKeys.PdndVoucherClientId);
            AddIfPresent(metadata, root, "client_id", PdndMetadataKeys.PdndVoucherClientIdUnderscore);
        }
        catch
        {
            // Fail-soft: ignore parsing errors to avoid breaking request execution.
        }
    }

    private static void AddIfPresent(PdndCallerMetadata md, JsonElement root, string claim, string key)
    {
        if (JwtJsonReader.TryReadString(root, claim, out var value) && !string.IsNullOrWhiteSpace(value))
            md.Add(key, value!, PdndMetadataSource.Claims);
    }
}