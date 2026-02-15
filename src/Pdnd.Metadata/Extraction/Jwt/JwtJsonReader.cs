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