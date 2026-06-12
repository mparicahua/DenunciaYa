using System.Text;
using DenunciaYA.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Scalar (documentación interactiva)
builder.Services.AddOpenApi();

// Cache para catálogos
builder.Services.AddMemoryCache();

// Rate limiting en login
builder.Services.AddRateLimiter(options =>
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
    }));

// Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<DenunciaService>();
builder.Services.AddScoped<AsignacionService>();
builder.Services.AddScoped<DerivacionService>();
builder.Services.AddScoped<ReporteService>();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "DenunciaYA API";
    options.Theme = ScalarTheme.BluePlanet;
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();
