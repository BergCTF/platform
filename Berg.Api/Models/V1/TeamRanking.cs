using System.Text.Json.Serialization;

namespace Berg.Api.Models.V1;

public class TeamRanking
{
    [JsonPropertyName("teamId")]
    public Guid TeamId { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("lastSolve")]
    public DateTime? LastSolve { get; set; }

    [JsonPropertyName("solves")]
    public List<TeamSolve> Solves { get; set; } = [];
}