using System.Text;
using AuthService.Data;
using AuthService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MongoDB
var mongoConnection =
    builder.Configuration["MongoDB:ConnectionString"]
    ?? throw new InvalidOperationException(
        "MongoDB connection string is missing.");

var databaseName =
    builder.Configuration["MongoDB:DatabaseName"]
    ?? throw new InvalidOperationException(
        "MongoDB database name is missing.");

builder.Services.AddSingleton<IMongoClient>(
    _ => new MongoClient(mongoConnection));

builder.Services.AddSingleton<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>()
        .GetDatabase(databaseName));

builder.Services.AddSingleton<UserRepository>();
builder.Services.AddScoped<AuthService.Services.AuthService>();

// JWT
var secret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException(
        "JWT secret is missing.");

if (Encoding.UTF8.GetByteCount(secret) < 32)
{
    throw new InvalidOperationException(
        "JWT secret must contain at least 32 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer =
                    builder.Configuration["Jwt:Issuer"],

                ValidateAudience = true,
                ValidAudience =
                    builder.Configuration["Jwt:Audience"],

                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(secret)),

                NameClaimType =
                    System.Security.Claims.ClaimTypes.Name,

                RoleClaimType =
                    System.Security.Claims.ClaimTypes.Role,

                ClockSkew = TimeSpan.FromSeconds(30)
            };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Create the unique email index on startup.
using (var scope = app.Services.CreateScope())
{
    var repository =
        scope.ServiceProvider.GetRequiredService<UserRepository>();

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
    service = "AuthService"
}));

app.Run();