// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Text;

namespace Pdnd.Metadata.Extraction.Jwt;

/// <summary>
/// Provides best-effort decoding for JWT/JWS tokens without validation.
/// Supports JWS (3 segments) and provides limited support for JWE (5 segments) by extracting header only.
/// </summary>
public static class JwtDecoder
{
    /// <summary>
    /// Tries to decode a token in the form "header.payload[.signature]" where header and payload are Base64Url JSON.
    /// For JWE tokens (5 segments), only the header is reliably decoded; payload may be encrypted.
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

        // JWS has 3 segments, JWE has 5 segments
        // We require at least 2 segments (header + payload)
        if (segments.Length < 2 || segments.Length > 5)
            return false;

        // Validate segments are not empty
        if (string.IsNullOrEmpty(segments[0]) || string.IsNullOrEmpty(segments[1]))
            return false;

        if (!TryBase64UrlDecodeToUtf8(segments[0], out var headerJson))
            return false;

        if (!TryBase64UrlDecodeToUtf8(segments[1], out var payloadJson))
            return false;

        var signature = segments.Length >= 3 && !string.IsNullOrEmpty(segments[2]) ? segments[2] : null;

        parts = new JwtParts(headerJson, payloadJson, signature);
        return true;
    }

    private static bool TryBase64UrlDecodeToUtf8(string base64Url, out string utf8)
    {
        utf8 = string.Empty;

        if (string.IsNullOrEmpty(base64Url))
            return false;

        if (!TryBase64UrlDecode(base64Url, out var bytes))
            return false;

        try
        {
            utf8 = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryBase64UrlDecode(string input, out byte[] result)
    {
        result = Array.Empty<byte>();

        if (string.IsNullOrEmpty(input))
            return false;

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
