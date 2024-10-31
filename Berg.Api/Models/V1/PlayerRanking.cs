using System.Text.Json.Serialization;

namespace Berg.Api.Models.V1;

public class PlayerRanking
{
    [JsonPropertyName("playerId")]
    public Guid PlayerId { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("lastSolve")]
    public DateTime? LastSolve { get; set; }

    [JsonPropertyName("solves")]
    public List<PlayerSolve> Solves { get; set; } = [];
}