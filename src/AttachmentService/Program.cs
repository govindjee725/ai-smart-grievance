
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT secret is missing.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddSingleton<IMongoCollection<AttachmentRecord>>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var client = new MongoClient(config["MongoDB:ConnectionString"]);
    var database = client.GetDatabase(config["MongoDB:DatabaseName"]);
    return database.GetCollection<AttachmentRecord>("attachments");
});

builder.Services.AddHttpClient("GrievanceService", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["GrievanceService:BaseUrl"]
        ?? "http://localhost:4102/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

var uploadDirectory = Path.GetFullPath(
    builder.Configuration["FileStorage:UploadDirectory"] ?? "uploads");

Directory.CreateDirectory(uploadDirectory);

var maxFileSize = builder.Configuration.GetValue<long>(
    "FileStorage:MaxFileSizeBytes", 5 * 1024 * 1024);

var allowedTypes = new Dictionary<string, string>
{
    [".jpg"] = "image/jpeg",
    [".jpeg"] = "image/jpeg",
    [".png"] = "image/png",
    [".webp"] = "image/webp",
    [".pdf"] = "application/pdf"
};

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    service = "AttachmentService",
    status = "Healthy"
}));

app.MapPost("/api/attachments/{trackingId}",
    async (string trackingId,
        IFormFile file,
        ClaimsPrincipal user,
        IMongoCollection<AttachmentRecord> collection,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken) =>
    {
        var citizenId = user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (string.IsNullOrWhiteSpace(citizenId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(trackingId) ||
            trackingId.Length > 100 ||
            trackingId.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
        {
            return Results.BadRequest(new { message = "Invalid tracking ID." });
        }

        if (file is null || file.Length == 0)
            return Results.BadRequest(new { message = "Select a file." });

        if (file.Length > maxFileSize)
            return Results.BadRequest(new { message = "Maximum file size is 5 MB." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedTypes.TryGetValue(extension, out var expectedContentType) ||
            !string.Equals(file.ContentType, expectedContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new
            {
                message = "Only JPG, PNG, WEBP and PDF files are allowed."
            });
        }

        // Confirm that this grievance belongs to the authenticated citizen.
        var client = httpClientFactory.CreateClient("GrievanceService");
        var bearerToken = user.FindFirst("access_token")?.Value;

        // The JWT is forwarded from the original request by reading its header
        // in the endpoint below, rather than trusting a client-supplied citizen ID.
        // Ownership check is performed in the handler that receives HttpContext.
        return Results.Problem(
            "Upload handler setup incomplete. Use the HttpContext-based endpoint below.");
    })
    .RequireAuthorization()
    .DisableAntiforgery();


app.MapPost("/api/attachments/{trackingId}",
    async (
        string trackingId,
        HttpContext http,
        IMongoCollection<AttachmentRecord> collection,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken) =>
    {
        var user = http.User;

        var citizenId =
            user.FindFirst("sub")?.Value ??
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(citizenId))
            return Results.Unauthorized();

        if (trackingId.Length > 100 ||
            trackingId.Any(c =>
                !char.IsLetterOrDigit(c) && c != '-'))
        {
            return Results.BadRequest(new
            {
                message = "Invalid tracking ID."
            });
        }

        if (!http.Request.HasFormContentType)
        {
            return Results.BadRequest(new
            {
                message = "Use multipart/form-data."
            });
        }

        var form = await http.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");

        if (file is null || file.Length == 0)
            return Results.BadRequest(new
            {
                message = "Select a file using the field name 'file'."
            });

        if (file.Length > maxFileSize)
            return Results.BadRequest(new
            {
                message = "Maximum file size is 5 MB."
            });

        var extension = Path.GetExtension(file.FileName)
            .ToLowerInvariant();

        if (!allowedTypes.TryGetValue(
                extension, out var expectedContentType) ||
            !string.Equals(
                file.ContentType,
                expectedContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new
            {
                message = "Only JPG, PNG, WEBP and PDF are allowed."
            });
        }

        // Verify ownership through GrievanceService.
        var client = httpClientFactory.CreateClient("GrievanceService");

        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"api/grievances/track/{trackingId}");

        var authorization = http.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authorization))
            return Results.Unauthorized();

        request.Headers.TryAddWithoutValidation(
            "Authorization", authorization);

        using var response = await client.SendAsync(
            request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return Results.BadRequest(new
            {
                message = "Could not verify grievance ownership."
            });

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var root = json.RootElement;

        // Adjust these property names if your tracking endpoint uses a
        // different response shape.
        var ownerId = root.TryGetProperty("citizenId", out var owner)
            ? owner.GetString()
            : null;

        if (ownerId is null ||
            !string.Equals(ownerId, citizenId, StringComparison.Ordinal))
        {
            return Results.Forbid();
        }

        // Validate file signature instead of trusting only its extension.
        await using var input = file.OpenReadStream();
        var header = new byte[8];
        var bytesRead = await input.ReadAsync(
            header.AsMemory(0, header.Length), cancellationToken);

        var validSignature = extension switch
        {
            ".jpg" or ".jpeg" =>
                bytesRead >= 3 &&
                header[0] == 0xFF && header[1] == 0xD8 &&
                header[2] == 0xFF,

            ".png" =>
                bytesRead >= 8 &&
                header.SequenceEqual(
                    new byte[] { 0x89, 0x50, 0x4E, 0x47,
                                 0x0D, 0x0A, 0x1A, 0x0A }),

            ".pdf" =>
                bytesRead >= 5 &&
                System.Text.Encoding.ASCII.GetString(header, 0, 5) == "%PDF-",

            ".webp" =>
                bytesRead >= 8 &&
                System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF" &&
                System.Text.Encoding.ASCII.GetString(header, 8 - 4, 4) == "WEBP",

            _ => false
        };

        if (!validSignature)
            return Results.BadRequest(new
            {
                message = "File content does not match the allowed format."
            });

        var storedName = $"{Guid.NewGuid():N}{extension}";
        var destination = Path.Combine(uploadDirectory, storedName);

        try
        {
            await using (var output = File.Create(destination))
            {
                input.Position = 0;
                await input.CopyToAsync(output, cancellationToken);
            }

            var record = new AttachmentRecord
            {
                TrackingId = trackingId,
                CitizenId = citizenId,
                StoredFileName = storedName,
                OriginalFileName = Path.GetFileName(file.FileName),
                ContentType = expectedContentType,
                Size = file.Length,
                UploadedAt = DateTime.UtcNow
            };

            await collection.InsertOneAsync(
                record, cancellationToken: cancellationToken);

            return Results.Created(
                $"/api/attachments/{record.Id}",
                new
                {
                    record.Id,
                    record.TrackingId,
                    record.OriginalFileName,
                    record.ContentType,
                    record.Size,
                    record.UploadedAt
                });
        }
        catch
        {
            if (File.Exists(destination))
                File.Delete(destination);

            throw;
        }
    })
    .RequireAuthorization()
    .DisableAntiforgery();


app.Run();

public sealed class AttachmentRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string TrackingId { get; set; } = "";
    public string CitizenId { get; set; } = "";
    public string StoredFileName { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}



