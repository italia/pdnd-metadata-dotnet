// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Buffers;
using System.Text;
using System.Text.Unicode;

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

    // Buffers up to this size live on the stack; larger tokens (bounded by MaxTokenLength) rent
    // from the shared pool. Either way no intermediate string or array is allocated.
    private const int StackLimit = 1024;

    /// <summary>
    /// Decodes one Base64Url segment into a UTF-8 string, rejecting anything that is not strict
    /// Base64Url or not valid UTF-8.
    /// </summary>
    /// <remarks>
    /// Exception-free by design. The previous implementation relied on FormatException and
    /// DecoderFallbackException to reject bad input, which made malformed tokens, the input an
    /// attacker controls, the expensive path. Decoding also used to translate the alphabet through
    /// two string replacements and a padded copy before converting.
    /// </remarks>
    private static bool TryBase64UrlDecodeToUtf8(string base64Url, out string utf8)
    {
        utf8 = string.Empty;

        if (string.IsNullOrEmpty(base64Url))
            return false;

        var length = base64Url.Length;

        // A length of 1 modulo 4 cannot come from any byte sequence.
        if (length % 4 == 1)
            return false;

        var paddedLength = (length + 3) & ~3;

        char[]? rentedChars = null;
        var chars = paddedLength <= StackLimit
            ? stackalloc char[StackLimit]
            : rentedChars = ArrayPool<char>.Shared.Rent(paddedLength);

        byte[]? rentedBytes = null;
        var maxBytes = paddedLength / 4 * 3;
        var bytes = maxBytes <= StackLimit
            ? stackalloc byte[StackLimit]
            : rentedBytes = ArrayPool<byte>.Shared.Rent(maxBytes);

        try
        {
            // Translate Base64Url to standard Base64, rejecting anything outside the Base64Url
            // alphabet. Convert tolerates whitespace and standard '+' and '/', which would let
            // non-Base64Url input decode successfully.
            for (var i = 0; i < length; i++)
            {
                var c = base64Url[i];
                switch (c)
                {
                    case >= 'A' and <= 'Z':
                    case >= 'a' and <= 'z':
                    case >= '0' and <= '9':
                        chars[i] = c;
                        break;
                    case '-':
                        chars[i] = '+';
                        break;
                    case '_':
                        chars[i] = '/';
                        break;
                    default:
                        return false;
                }
            }

            for (var i = length; i < paddedLength; i++)
                chars[i] = '=';

            if (!Convert.TryFromBase64Chars(chars[..paddedLength], bytes, out var written))
                return false;

            var decoded = bytes[..written];

            // The shared Encoding.UTF8 instance substitutes U+FFFD for invalid sequences, which
            // turns malformed input into a mangled string that only fails later, during JSON
            // parsing. Validating first rejects it where the problem actually is.
            if (!Utf8.IsValid(decoded))
                return false;

            utf8 = Encoding.UTF8.GetString(decoded);
            return true;
        }
        finally
        {
            if (rentedChars is not null)
                ArrayPool<char>.Shared.Return(rentedChars);
            if (rentedBytes is not null)
                ArrayPool<byte>.Shared.Return(rentedBytes);
        }
    }
}
