// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
namespace Pdnd.Metadata.Extraction.Pdnd;

/// <summary>
/// Helpers for handling Authorization header formats.
/// </summary>
public static class PdndAuthorization
{
    /// <summary>
    /// Tries to extract a Bearer token from an Authorization header value.
    /// </summary>
    /// <param name="authorizationHeader">Authorization header value.</param>
    /// <param name="token">Extracted token if present.</param>
    /// <returns><c>true</c> if a Bearer token is found; otherwise <c>false</c>.</returns>
    public static bool TryGetBearerToken(string? authorizationHeader, out string? token)
    {
        token = null;

        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return false;

        const string prefix = "Bearer ";
        if (!authorizationHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var t = authorizationHeader.Substring(prefix.Length).Trim();
        if (string.IsNullOrWhiteSpace(t))
            return false;

        token = t;
        return true;
    }
}