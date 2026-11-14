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
    // Names whose signed value is a credential, withheld regardless of configuration. The
    // default extractor additionally applies PdndMetadataOptions.HeaderDenyList.
    private static readonly HashSet<string> CredentialHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization",
        "proxy-authorization",
        "cookie",
        "set-cookie"
    };

    private const int MaxSignedHeaderNameLength = 256;

    /// <summary>RFC 9110 §5.6.2: a header name is a non-empty sequence of tchar.</summary>
    private static bool IsHeaderNameToken(string name)
    {
        if (name.Length == 0)
            return false;

        foreach (var c in name)
        {
            var isTchar = c is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.'
                or '^' or '_' or '`' or '|' or '~';

            if (!isTchar)
                return false;
        }

        return true;
    }

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

                if (JwtJsonReader.TryReadSignedHeaders(p, out var signedHeaders))
                {
                    metadata.Add(
                        PdndMetadataKeys.PdndSignatureSignedHeaders,
                        string.Join(", ", signedHeaders.Select(h => h.Key)),
                        PdndMetadataSource.Claims);

                    foreach (var header in signedHeaders)
                    {
                        if (string.IsNullOrEmpty(header.Value))
                            continue;

                        // A client may sign its Authorization header. Its value is then the
                        // voucher itself, which must never be stored. The name stays in the
                        // signed_headers list; only the value is withheld.
                        if (CredentialHeaders.Contains(header.Key))
                            continue;

                        // The name becomes part of a key. Real header names are short tokens of
                        // RFC 9110 tchar; anything else (zero-width or other invisible characters
                        // included) is not a header that could have been on the wire, and could
                        // otherwise disguise a credential header from the check above.
                        if (header.Key.Length > MaxSignedHeaderNameLength || !IsHeaderNameToken(header.Key))
                            continue;

                        metadata.Add(
                            PdndMetadataKeys.PdndSignatureSignedHeaderPrefix + header.Key,
                            header.Value,
                            PdndMetadataSource.Claims);
                    }
                }
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
