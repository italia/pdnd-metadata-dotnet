// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Text;

namespace Pdnd.Metadata.Tests.TestHelpers;

internal static class Base64UrlTestHelper
{
    public static string EncodeUtf8(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var b64 = Convert.ToBase64String(bytes);
        return b64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// Builds a JWT/JWS-like token in the form "header.payload[.signature]" using Base64Url JSON parts.
    /// </summary>
    public static string Jwt(string headerJson, string payloadJson, string? signature = null)
    {
        var header = EncodeUtf8(headerJson);
        var payload = EncodeUtf8(payloadJson);

        return string.IsNullOrWhiteSpace(signature)
            ? $"{header}.{payload}"
            : $"{header}.{payload}.{signature}";
    }
}