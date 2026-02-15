// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Text;

namespace Pdnd.Metadata.Extraction.Jwt;

/// <summary>
/// Provides best-effort decoding for JWT/JWS tokens without validation.
/// </summary>
public static class JwtDecoder
{
    /// <summary>
    /// Tries to decode a token in the form "header.payload[.signature]" where header and payload are Base64Url JSON.
    /// </summary>
    /// <param name="token">The token string.</param>
    /// <param name="parts">Decoded token parts if successful.</param>
    /// <returns><c>true</c> if decoding succeeds; otherwise <c>false</c>.</returns>
    public static bool TryDecode(string token, out JwtParts parts)
    {
        parts = default!;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        var segments = token.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return false;

        if (!TryBase64UrlDecodeToUtf8(segments[0], out var headerJson))
            return false;

        if (!TryBase64UrlDecodeToUtf8(segments[1], out var payloadJson))
            return false;

        var signature = segments.Length >= 3 ? segments[2] : null;

        parts = new JwtParts(headerJson, payloadJson, signature);
        return true;
    }

    private static bool TryBase64UrlDecodeToUtf8(string base64Url, out string utf8)
    {
        utf8 = string.Empty;

        try
        {
            var bytes = Base64UrlDecode(base64Url);
            utf8 = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');

        switch (s.Length % 4)
        {
            case 0: break;
            case 2: s += "=="; break;
            case 3: s += "="; break;
            default: throw new FormatException("Invalid Base64Url length.");
        }

        return Convert.FromBase64String(s);
    }
}