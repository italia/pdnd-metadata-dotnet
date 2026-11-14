// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
namespace Pdnd.Metadata.Extraction.Pdnd;

/// <summary>
/// Helpers for handling Authorization header formats.
/// </summary>
public static class PdndAuthorization
{
    private const string BearerScheme = "Bearer";
    private const string DpopScheme = "DPoP";

    /// <summary>
    /// Tries to extract a Bearer token from an Authorization header value.
    /// </summary>
    /// <param name="authorizationHeader">Authorization header value.</param>
    /// <param name="token">Extracted token if present.</param>
    /// <returns><c>true</c> if a Bearer token is found; otherwise <c>false</c>.</returns>
    public static bool TryGetBearerToken(string? authorizationHeader, out string? token)
        => TryGetToken(authorizationHeader, BearerScheme, out token);

    /// <summary>
    /// Tries to extract the PDND voucher from an Authorization header value, accepting both
    /// the <c>Bearer</c> scheme and the <c>DPoP</c> scheme (RFC 9449 §7.1) used when the
    /// voucher is bound to a DPoP proof. Scheme matching is case-insensitive.
    /// </summary>
    /// <param name="authorizationHeader">Authorization header value.</param>
    /// <param name="token">Extracted token if present.</param>
    /// <returns><c>true</c> if a voucher token is found; otherwise <c>false</c>.</returns>
    public static bool TryGetVoucherToken(string? authorizationHeader, out string? token)
        => TryGetToken(authorizationHeader, BearerScheme, out token)
           || TryGetToken(authorizationHeader, DpopScheme, out token);

    private static bool TryGetToken(string? authorizationHeader, string scheme, out string? token)
    {
        token = null;

        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return false;

        var value = authorizationHeader.TrimStart();

        // The scheme must be followed by at least one space: "Bearerabc" is not a Bearer token.
        if (value.Length <= scheme.Length ||
            !value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) ||
            value[scheme.Length] != ' ')
        {
            return false;
        }

        var t = value.Substring(scheme.Length + 1).Trim();
        if (string.IsNullOrWhiteSpace(t))
            return false;

        token = t;
        return true;
    }
}
