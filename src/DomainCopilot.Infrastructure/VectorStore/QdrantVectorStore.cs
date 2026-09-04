using DomainCopilot.Application.Retrieval;
using DomainCopilot.Application.VectorStore;
using DomainCopilot.Domain.Documents;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace DomainCopilot.Infrastructure.VectorStore;

/// <summary>
/// The Qdrant adapter for <see cref="IVectorStore"/>. One collection for the whole knowledge
/// corpus (ADR-0004) — a claims-adjudication query legitimately needs policy wording + guidelines
/// + a specific endorsement together, and Qdrant's payload filtering is efficient enough at this
/// scale that splitting collections would only add complexity.
/// </summary>
public sealed class QdrantVectorStore(QdrantClient client) : IVectorStore
{
    public const string CollectionName = "domain_copilot_knowledge_chunks";

    public async Task EnsureCollectionAsync(int vectorSize, CancellationToken cancellationToken = default)
    {
        if (await client.CollectionExistsAsync(CollectionName, cancellationToken))
        {
            return;
        }

        await client.CreateCollectionAsync(
            CollectionName,
            new VectorParams { Size = (ulong)vectorSize, Distance = Distance.Cosine },
            cancellationToken: cancellationToken);
    }

    public async Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
        {
            return;
        }

        var points = records.Select(ToPointStruct).ToList();
        await client.UpsertAsync(CollectionName, points, cancellationToken: cancellationToken);
    }

    public async Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        if (!await client.CollectionExistsAsync(CollectionName, cancellationToken))
        {
            return;
        }

        Filter filter = Conditions.MatchKeyword("documentId", documentId.ToString());
        await client.DeleteAsync(CollectionName, filter, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ScoredChunk>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int topK,
        RetrievalFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        if (!await client.CollectionExistsAsync(CollectionName, cancellationToken))
        {
            return [];
        }

        var points = await client.QueryAsync(
            CollectionName,
            query: queryEmbedding.ToArray(),
            filter: BuildFilter(filter),
            limit: (ulong)topK,
            payloadSelector: true,
            cancellationToken: cancellationToken);

        return [.. points.Select(ToScoredChunk)];
    }

    private static Filter? BuildFilter(RetrievalFilter? filter)
    {
        if (filter is null || (filter.Category is null && filter.FormVersion is null))
        {
            return null;
        }

        var result = new Filter();

        if (filter.Category is { } category)
        {
            result.Must.Add(Conditions.MatchKeyword("category", category.ToString()));
        }

        if (filter.FormVersion is { } formVersion)
        {
            // A chunk matches when it's tagged with this exact version, OR carries no version at
            // all (reference material, most endorsements) — version-agnostic content must stay
            // visible regardless of which policy edition governs the query.
            var versionOrUntagged = new Filter();
            versionOrUntagged.Should.Add(Conditions.MatchKeyword("formVersion", formVersion));
            versionOrUntagged.Should.Add(Conditions.IsEmpty("formVersion"));
            result.Must.Add(Conditions.Filter(versionOrUntagged));
        }

        return result;
    }

    private static ScoredChunk ToScoredChunk(ScoredPoint point)
    {
        var payload = point.Payload;
        var pageNumber = payload.TryGetValue("pageNumber", out var pageValue) ? (int?)pageValue.IntegerValue : null;
        var formVersion = payload.TryGetValue("formVersion", out var versionValue) ? versionValue.StringValue : null;
        var effectiveDate = payload.TryGetValue("effectiveDate", out var dateValue) && DateOnly.TryParse(dateValue.StringValue, out var parsed)
            ? parsed
            : (DateOnly?)null;

        return new ScoredChunk(
            Guid.Parse(payload["documentId"].StringValue),
            (int)payload["chunkIndex"].IntegerValue,
            payload["sectionTitle"].StringValue,
            pageNumber,
            Enum.Parse<DocumentCategory>(payload["category"].StringValue),
            formVersion,
            effectiveDate,
            payload["text"].StringValue,
            point.Score);
    }

    private static PointStruct ToPointStruct(VectorRecord record)
    {
        var point = new PointStruct
        {
            Id = record.PointId,
            Vectors = record.Embedding.ToArray(),
        };

        point.Payload["documentId"] = record.DocumentId.ToString();
        point.Payload["chunkIndex"] = record.ChunkIndex;
        point.Payload["sectionTitle"] = record.SectionTitle;
        point.Payload["category"] = record.Category.ToString();
        point.Payload["contentHash"] = record.ContentHash;
        point.Payload["text"] = record.Text;

        if (record.PageNumber is { } page)
        {
            point.Payload["pageNumber"] = page;
        }

        if (record.FormVersion is { } formVersion)
        {
            point.Payload["formVersion"] = formVersion;
        }

        if (record.EffectiveDate is { } effectiveDate)
        {
            point.Payload["effectiveDate"] = effectiveDate.ToString("yyyy-MM-dd");
        }

        return point;
    }
}
