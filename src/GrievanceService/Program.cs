using System.Text;
using GrievanceService.Data;
using GrievanceService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var mongoConnection =
    builder.Configuration["MongoDB:ConnectionString"]
    ?? throw new InvalidOperationException("Missing MongoDB URI.");

var databaseName =
    builder.Configuration["MongoDB:DatabaseName"]
    ?? throw new InvalidOperationException("Missing database name.");

builder.Services.AddSingleton<IMongoClient>(
    _ => new MongoClient(mongoConnection));

builder.Services.AddSingleton<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>()
        .GetDatabase(databaseName));

builder.Services.AddSingleton<GrievanceRepository>();

var aiUrl = builder.Configuration["AI:BaseUrl"]
    ?? throw new InvalidOperationException("Missing AI URL.");

builder.Services.AddHttpClient<AiClient>(client =>
{
    client.BaseAddress = new Uri(aiUrl);
    client.Timeout = TimeSpan.FromSeconds(3);
});

var secret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Missing JWT secret.");

if (Encoding.UTF8.GetByteCount(secret) < 32)
    throw new InvalidOperationException("JWT secret is too short.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],

                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"],

                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(secret)),

                NameClaimType = System.Security.Claims.ClaimTypes.Name,
                RoleClaimType = System.Security.Claims.ClaimTypes.Role
            };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var repository = scope.ServiceProvider
        .GetRequiredService<GrievanceRepository>();

    await repository.EnsureIndexesAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "GrievanceService"
}));

app.Run();