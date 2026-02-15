// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
namespace Pdnd.Metadata.Extraction.Jwt;

/// <summary>
/// Represents decoded parts of a JWT/JWS-like token (best-effort).
/// </summary>
/// <param name="HeaderJson">Decoded header JSON.</param>
/// <param name="PayloadJson">Decoded payload JSON.</param>
/// <param name="SignatureBase64Url">Signature part (Base64Url) if present.</param>
public sealed record JwtParts(
    string HeaderJson,
    string PayloadJson,
    string? SignatureBase64Url);