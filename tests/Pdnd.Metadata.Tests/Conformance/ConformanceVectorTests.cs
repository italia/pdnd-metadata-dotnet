// (c) 2026 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using System.Text.Json;
using FluentAssertions;
using Pdnd.Metadata.Extraction;
using Pdnd.Metadata.Models;
using Pdnd.Metadata.Options;
using Pdnd.Metadata.RequestContext;

namespace Pdnd.Metadata.Tests.Conformance;

/// <summary>
/// Executes the shared conformance corpus in <c>spec/vectors/vectors.json</c> against this
/// implementation.
/// </summary>
/// <remarks>
/// The corpus is language neutral by design, so that implementations in other stacks can run
/// the same vectors. This runner is what keeps the corpus honest: a vector that no
/// implementation executes is a claim, not a test.
/// </remarks>
public sealed class ConformanceVectorTests
{
    private const string AbsentMarker = "__absent__";

    public static TheoryData<string> VectorIds()
    {
        var data = new TheoryData<string>();

        foreach (var vector in LoadCorpus().RootElement.GetProperty("vectors").EnumerateArray())
            data.Add(vector.GetProperty("id").GetString()!);

        return data;
    }

    [Theory]
    [MemberData(nameof(VectorIds))]
    public void Vector_ProducesTheExpectedCanonicalKeys(string vectorId)
    {
        var vector = LoadCorpus().RootElement
            .GetProperty("vectors")
            .EnumerateArray()
            .Single(v => v.GetProperty("id").GetString() == vectorId);

        var headers = vector.GetProperty("request").GetProperty("headers")
            .EnumerateObject()
            .Select(p => new PdndRequestHeader(p.Name, new[] { p.Value.GetString()! }))
            .ToList();

        var context = new PdndRequestContext
        {
            Method = "POST",
            Scheme = "https",
            Host = "erogatore.example.it",
            Path = "/v1/resource",
            Headers = headers
        };

        // Vectors describe extraction, not capture policy, so the permissive posture is used
        // here to keep raw-header expectations meaningful.
        var options = new PdndMetadataOptions { CaptureAllHeaders = true };

        var metadata = new DefaultPdndMetadataExtractor().Extract(context, options);

        foreach (var expectation in vector.GetProperty("expect").EnumerateObject())
        {
            if (expectation.Name == AbsentMarker)
            {
                foreach (var key in expectation.Value.EnumerateArray())
                {
                    metadata.GetFirstValue(key.GetString()!)
                        .Should().BeNull($"vector {vectorId} requires '{key.GetString()}' to be absent");
                }

                continue;
            }

            metadata.GetFirstValue(expectation.Name)
                .Should().Be(expectation.Value.GetString(), $"vector {vectorId} expects '{expectation.Name}'");
        }
    }

    [Fact]
    public void Corpus_HasUniqueVectorIdentifiers()
    {
        var ids = LoadCorpus().RootElement.GetProperty("vectors")
            .EnumerateArray()
            .Select(v => v.GetProperty("id").GetString())
            .ToList();

        ids.Should().OnlyHaveUniqueItems();
    }

    private static JsonDocument LoadCorpus()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "spec", "vectors", "vectors.json");
            if (File.Exists(candidate))
                return JsonDocument.Parse(File.ReadAllText(candidate));

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate spec/vectors/vectors.json by walking up from the test output directory.");
    }
}
