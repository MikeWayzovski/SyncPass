using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrimbleConnector.Models;

public sealed class ConnectProject
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("rootId")]
    public string? RootId { get; set; }

    [JsonPropertyName("rootFolderId")]
    public string? RootFolderId { get; set; }

    [JsonPropertyName("root")]
    public ConnectFolder? Root { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    public string? EffectiveRootId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(RootId))
            {
                return RootId;
            }

            if (!string.IsNullOrWhiteSpace(RootFolderId))
            {
                return RootFolderId;
            }

            return string.IsNullOrWhiteSpace(Root?.Id) ? null : Root.Id;
        }
    }

    public string? EffectiveLocation => Location ?? Region;
}

public sealed class ConnectFolder
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("path")]
    [JsonConverter(typeof(FlexiblePathConverter))]
    public string? Path { get; set; }
}

public sealed class ConnectFile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("path")]
    [JsonConverter(typeof(FlexiblePathConverter))]
    public string? Path { get; set; }

    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    [JsonPropertyName("size")]
    [JsonConverter(typeof(FlexibleInt64Converter))]
    public long? Size { get; set; }

    [JsonPropertyName("versionId")]
    public string? VersionId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public sealed class DownloadUrlResponse
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    public string? EffectiveUrl => Url ?? DownloadUrl;
}

public sealed class UploadInitRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("parentId")]
    public string ParentId { get; set; } = string.Empty;

    [JsonPropertyName("parentType")]
    public string ParentType { get; set; } = "FOLDER";

    [JsonPropertyName("fileId")]
    public string? FileId { get; set; }

    /// <summary>
    /// Project id is not accepted in the initiate JSON body; it is not sent.
    /// </summary>
    [JsonIgnore]
    public string? ProjectId { get; set; }
}

public sealed class FolderCreateRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("parentId")]
    public string ParentId { get; set; } = string.Empty;

    [JsonPropertyName("parentType")]
    public string ParentType { get; set; } = "FOLDER";
}

public sealed class UploadInitResponse
{
    [JsonPropertyName("uploadId")]
    public string? UploadId { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("uploadUrl")]
    public string? UploadUrl { get; set; }

    [JsonPropertyName("fileId")]
    public string? FileId { get; set; }

    public string? EffectiveUrl => Url ?? UploadUrl;
}

public sealed class UploadCommitRequest
{
    [JsonPropertyName("uploadId")]
    public string? UploadId { get; set; }

    [JsonPropertyName("fileId")]
    public string? FileId { get; set; }
}

/// <summary>
/// Trimble Connect returns <c>path</c> as a string or as an array of path segments.
/// </summary>
internal sealed class FlexiblePathConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return null;
        }

        var parts = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    parts.Add(value);
                }
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                using var item = JsonDocument.ParseValue(ref reader);
                if (item.RootElement.TryGetProperty("name", out var name)
                    && name.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(name.GetString()))
                {
                    parts.Add(name.GetString()!);
                }
            }
            else
            {
                reader.Skip();
            }
        }

        return parts.Count == 0 ? null : string.Join('/', parts);
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }
}

internal sealed class FlexibleInt64Converter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var number))
        {
            return number;
        }

        if (reader.TokenType == JsonTokenType.String
            && long.TryParse(reader.GetString(), out var parsed))
        {
            return parsed;
        }

        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteNumberValue(value.Value);
    }
}
