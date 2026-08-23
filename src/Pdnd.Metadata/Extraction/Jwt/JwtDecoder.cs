// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Text;

namespace Pdnd.Metadata.Extraction.Jwt;

/// <summary>
/// Provides best-effort decoding for JWT/JWS tokens without validation.
/// Supports JWS (3 segments) and limited JWE compact support (5 segments):
/// for JWE, only the protected header is decoded and payload is normalized to an empty JSON object.
/// </summary>
public static class JwtDecoder
{
    /// <summary>
    /// Tries to decode a compact token in the form "header.payload[.signature]" (JWS/JWT-like).
    /// For JWE tokens (5 segments), decodes only the protected header and returns an empty JSON payload ("{}").
    /// </summary>
    /// <param name="token">The token string.</param>
    /// <param name="parts">Decoded token parts if successful.</param>
    /// <returns><c>true</c> if decoding succeeds; otherwise <c>false</c>.</returns>
    public static bool TryDecode(string token, out JwtParts? parts)
    {
        parts = null;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        // Trim any whitespace that may have been introduced
        token = token.Trim();

        var segments = token.Split('.');

        // Valid compact serializations are: 2 segments (unsecured JWT), 3 (JWS), 5 (JWE).
        // 4 segments is neither, so it is rejected rather than partially decoded.
        if (segments.Length is not (2 or 3 or 5))
            return false;

        if (!TryBase64UrlDecodeToUtf8(segments[0], out var headerJson))
            return false;

        // JWE compact serialization (5 segments): payload is encrypted and not JSON-decodable.
        // For best-effort extraction we keep header metadata and expose an empty JSON payload.
        if (segments.Length == 5)
        {
            // Segment 1 (encrypted key) can be empty for some algorithms (e.g., "dir").
            if (string.IsNullOrEmpty(segments[2]) || string.IsNullOrEmpty(segments[3]) || string.IsNullOrEmpty(segments[4]))
                return false;

            parts = new JwtParts(headerJson, "{}", null);
            return true;
        }

        // JWS / JWT-like: require second segment to decode as UTF-8 JSON payload.
        if (string.IsNullOrEmpty(segments[1]))
            return false;

        if (!TryBase64UrlDecodeToUtf8(segments[1], out var payloadJson))
            return false;

        var signature = segments.Length >= 3 && !string.IsNullOrEmpty(segments[2]) ? segments[2] : null;

        parts = new JwtParts(headerJson, payloadJson, signature);
        return true;
    }

    // The shared Encoding.UTF8 instance substitutes U+FFFD instead of throwing, which turns
    // malformed input into a mangled string that only fails later, during JSON parsing.
    // This instance rejects it at the point where the problem actually is.
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static bool TryBase64UrlDecodeToUtf8(string base64Url, out string utf8)
    {
        utf8 = string.Empty;

        if (string.IsNullOrEmpty(base64Url))
            return false;

        if (!TryBase64UrlDecode(base64Url, out var bytes))
            return false;

        try
        {
            utf8 = StrictUtf8.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool TryBase64UrlDecode(string input, out byte[] result)
    {
        result = Array.Empty<byte>();

        if (string.IsNullOrEmpty(input))
            return false;

        // Reject anything outside the Base64Url alphabet before translating. Convert.FromBase64String
        // tolerates embedded whitespace and standard-Base64 '+' and '/', which would let
        // non-Base64Url input decode successfully.
        foreach (var c in input)
        {
            var isAllowed = c is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-' or '_';

            if (!isAllowed)
                return false;
        }

        // Convert Base64Url to standard Base64
        var s = input.Replace('-', '+').Replace('_', '/');

        // Add padding based on length
        // Valid Base64 lengths are: 0, 2, 3 (mod 4) - case 1 is invalid
        switch (s.Length % 4)
        {
            case 0:
                // No padding needed
                break;
            case 2:
                s += "==";
                break;
            case 3:
                s += "=";
                break;
            case 1:
                // Invalid Base64 length - cannot produce length % 4 == 1 from valid encoding
                return false;
            default:
                return false;
        }

        try
        {
            result = Convert.FromBase64String(s);
            return true;
        }
        catch (FormatException)
        {
            // Invalid Base64 characters
            return false;
        }
    }
}
