using System.Text.Json.Serialization;

namespace TrimbleConnector.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SyncDirection
{
    TwoWay,
    LocalToCloud,
    CloudToLocal
}
