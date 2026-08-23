// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Text.Json;

namespace Pdnd.Metadata.Extraction.Jwt;

/// <summary>
/// Helpers for reading common values from JWT JSON safely.
/// </summary>
public static class JwtJsonReader
{
    /// <summary>
    /// Tries to read a string property from a JSON element.
    /// </summary>
    public static bool TryReadString(JsonElement root, string name, out string? value)
    {
        value = null;

        if (!root.TryGetProperty(name, out var prop))
            return false;

        if (prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        if (prop.ValueKind == JsonValueKind.Number)
        {
            value = prop.GetRawText();
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }

    /// <summary>
    /// Tries to read the ModI "signed_headers" claim.
    /// </summary>
    /// <remarks>
    /// In the AgID integrity patterns the claim carries the set of signed headers together with
    /// their values, so that the provider can compare them against what actually arrived on the
    /// wire. The canonical encoding is an array of single-property objects, for example
    /// <c>[{"digest":"SHA-256=..."},{"content-type":"application/json"}]</c>.
    /// Two defensive shapes are also accepted: an array of header names, and a single
    /// comma-separated string. Neither is conformant, but both have been observed in the wild
    /// and silently dropping them would be worse than normalizing them.
    /// </remarks>
    /// <param name="root">Payload root element.</param>
    /// <param name="pairs">
    /// Signed header names, lowercased, each paired with its value. The value is an empty string
    /// when the encoding carries names only.
    /// </param>
    /// <returns><c>true</c> if at least one signed header was read; otherwise <c>false</c>.</returns>
    public static bool TryReadSignedHeaders(JsonElement root, out IReadOnlyList<KeyValuePair<string, string>> pairs)
    {
        pairs = Array.Empty<KeyValuePair<string, string>>();

        if (!root.TryGetProperty("signed_headers", out var claim))
            return false;

        var result = new List<KeyValuePair<string, string>>();

        switch (claim.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var element in claim.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in element.EnumerateObject())
                        {
                            if (string.IsNullOrWhiteSpace(property.Name))
                                continue;

                            var headerValue = property.Value.ValueKind == JsonValueKind.String
                                ? property.Value.GetString() ?? string.Empty
                                : property.Value.GetRawText();

                            result.Add(new KeyValuePair<string, string>(
                                property.Name.ToLowerInvariant(),
                                headerValue));
                        }
                    }
                    else if (element.ValueKind == JsonValueKind.String)
                    {
                        var name = element.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                            result.Add(new KeyValuePair<string, string>(name!.ToLowerInvariant(), string.Empty));
                    }
                }

                break;

            case JsonValueKind.String:
                var raw = claim.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                    return false;

                foreach (var name in raw!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    result.Add(new KeyValuePair<string, string>(name.ToLowerInvariant(), string.Empty));

                break;

            default:
                return false;
        }

        if (result.Count == 0)
            return false;

        pairs = result;
        return true;
    }

    /// <summary>
    /// Tries to read an "aud" claim that can be either string or array of strings.
    /// Returns a single normalized string (comma-separated).
    /// </summary>
    public static bool TryReadAudience(JsonElement root, out string? value)
    {
        value = null;

        if (!root.TryGetProperty("aud", out var aud))
            return false;

        if (aud.ValueKind == JsonValueKind.String)
        {
            value = aud.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        if (aud.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var el in aud.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        list.Add(s!);
                }
            }

            if (list.Count > 0)
            {
                value = string.Join(", ", list);
                return true;
            }
        }

        return false;
    }
}