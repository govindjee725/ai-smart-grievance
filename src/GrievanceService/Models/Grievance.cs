using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GrievanceService.Models;

public class Grievance
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string TrackingId { get; set; } = string.Empty;

    public string CitizenId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string Category { get; set; } = "Pending AI analysis";

    public double AiConfidence { get; set; }

    public string SuggestedPriority { get; set; } = "Medium";

    public string Priority { get; set; } = "Medium";

    public string Status { get; set; } = "Submitted";

    public string? AssignedOfficerId { get; set; }

    public List<StatusHistory> History { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class StatusHistory
{
    public string Status { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public string UpdatedBy { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}