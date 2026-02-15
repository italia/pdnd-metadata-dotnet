// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
namespace Pdnd.Metadata.Extraction.Pdnd;

/// <summary>
/// Best-effort parser for the HTTP Digest header.
/// </summary>
public static class DigestParser
{
    /// <summary>
    /// Tries to parse a Digest header value like "SHA-256=base64value" (possibly with multiple entries).
    /// This is best-effort and does not validate the content.
    /// </summary>
    /// <param name="digestHeader">Digest header raw value.</param>
    /// <param name="algorithm">Parsed algorithm (e.g., SHA-256).</param>
    /// <param name="value">Parsed digest value (base64 or opaque string).</param>
    /// <returns><c>true</c> if a digest pair is parsed; otherwise <c>false</c>.</returns>
    public static bool TryParseDigestHeader(string digestHeader, out string? algorithm, out string? value)
    {
        algorithm = null;
        value = null;

        if (string.IsNullOrWhiteSpace(digestHeader))
            return false;

        var parts = digestHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var eq = part.IndexOf('=');
            if (eq <= 0 || eq >= part.Length - 1)
                continue;

            var alg = part.Substring(0, eq).Trim();
            var val = part.Substring(eq + 1).Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(alg) || string.IsNullOrWhiteSpace(val))
                continue;

            algorithm = alg;
            value = val;
            return true;
        }

        return false;
    }
}