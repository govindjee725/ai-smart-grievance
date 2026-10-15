using System.Security.Claims;
using GrievanceService.Data;
using GrievanceService.DTOs;
using GrievanceService.Models;
using GrievanceService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrievanceService.Controllers;

[ApiController]
[Route("api/grievances")]
[Authorize]
public class GrievancesController : ControllerBase
{
    private readonly GrievanceRepository _repository;
    private readonly AiClient _ai;

    public GrievancesController(
        GrievanceRepository repository,
        AiClient ai)
    {
        _repository = repository;
        _ai = ai;
    }

    [HttpPost]
    [Authorize(Roles = "citizen")]
    public async Task<IActionResult> Create(
        CreateGrievanceRequest request)
    {
        var citizenId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(citizenId))
            return Unauthorized();

        var prediction = await _ai.PredictAsync(
            $"{request.Title}. {request.Description}");

        var trackingId =
            $"GRV-{Guid.NewGuid():N}"[..16].ToUpperInvariant();

        var grievance = new Grievance
        {
            TrackingId = trackingId,
            CitizenId = citizenId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Location = request.Location.Trim(),
            Category = prediction?.Category ?? "Pending AI analysis",
            AiConfidence = prediction?.Confidence ?? 0,
            SuggestedPriority = prediction?.Priority ?? "Medium",
            Priority = prediction?.Priority ?? "Medium",
            History =
            [
                new StatusHistory
                {
                    Status = "Submitted",
                    Note = "Complaint submitted",
                    UpdatedBy = citizenId
                }
            ]
        };

        await _repository.CreateAsync(grievance);

        return CreatedAtAction(
            nameof(GetMine),
            new { },
            new
            {
                grievance.TrackingId,
                grievance.Category,
                grievance.AiConfidence,
                grievance.SuggestedPriority,
                grievance.Status
            });
    }

    [HttpGet("mine")]
    [Authorize(Roles = "citizen")]
    public async Task<IActionResult> GetMine()
    {
        var citizenId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(citizenId))
            return Unauthorized();

        var complaints =
            await _repository.GetByCitizenAsync(citizenId);

        return Ok(complaints);
    }

    [HttpGet("track/{trackingId}")]
    [AllowAnonymous]
    public async Task<IActionResult> Track(string trackingId)
    {
        var grievance =
            await _repository.GetByTrackingIdAsync(trackingId);

        if (grievance is null)
            return NotFound();

        return Ok(new
        {
            grievance.TrackingId,
            grievance.Title,
            grievance.Category,
            grievance.Priority,
            grievance.Status,
            grievance.CreatedAt,
            grievance.UpdatedAt,
            History = grievance.History.Select(h => new
            {
                h.Status,
                h.Note,
                h.ChangedAt
            })
        });
    }

    [HttpGet]
    [Authorize(Roles = "admin,officer")]
    public async Task<IActionResult> GetAll()
    {
        var grievances = await _repository.GetAllAsync();
        var role = User.FindFirstValue(ClaimTypes.Role);

        if (role == "officer")
        {
            var officerId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            grievances = grievances
                .Where(g => g.AssignedOfficerId == officerId)
                .ToList();
        }

        return Ok(grievances);
    }

    [HttpPatch("{trackingId}/status")]
    [Authorize(Roles = "admin,officer")]
    public async Task<IActionResult> UpdateStatus(
        string trackingId,
        UpdateStatusRequest request)
    {
        var validStatuses = new[]
        {
            "Submitted", "Assigned", "In Progress",
            "Resolved", "Rejected"
        };

        if (!validStatuses.Contains(request.Status))
            return BadRequest(new { message = "Invalid status." });

        var grievance =
            await _repository.GetByTrackingIdAsync(trackingId);

        if (grievance is null)
            return NotFound();

        var role = User.FindFirstValue(ClaimTypes.Role);
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (role == "officer" &&
            grievance.AssignedOfficerId != userId)
        {
            return Forbid();
        }

        var validPriorities = new[]
        {
            "Low", "Medium", "High", "Critical"
        };

        if (request.Priority is not null &&
            !validPriorities.Contains(request.Priority))
        {
            return BadRequest(new { message = "Invalid priority." });
        }

        grievance.Status = request.Status;

        if (request.Priority is not null)
            grievance.Priority = request.Priority;

        grievance.UpdatedAt = DateTime.UtcNow;

        grievance.History.Add(new StatusHistory
        {
            Status = request.Status,
            Note = request.Note,
            UpdatedBy = userId ?? "unknown"
        });

        await _repository.SaveAsync(grievance);

        return Ok(new
        {
            grievance.TrackingId,
            grievance.Status,
            grievance.Priority
        });
    }
}