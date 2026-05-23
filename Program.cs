using System.Text;
using Application.Auth;
using Application.OpenMrs;
using Application.Messaging;
using Infrastructure.Auth;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Options;
using Infrastructure.OpenMrs;
using Infrastructure.Messaging.Providers;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured.");

var jwtSecret = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException(
        "Jwt:SecretKey is not configured. Set via env var Jwt__SecretKey.");

// CORS: haal allowed origins op uit configuratie (komma-gescheiden)
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:3001")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services
    .AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Messaging providers
builder.Services.AddHttpClient();
builder.Services.Configure<MessagingOptions>(builder.Configuration.GetSection("Messaging"));
builder.Services.Configure<SwiftSendOptions>(builder.Configuration.GetSection("Messaging:SwiftSend"));
builder.Services.Configure<SecurePostOptions>(builder.Configuration.GetSection("Messaging:SecurePost"));
builder.Services.Configure<LegacyLinkOptions>(builder.Configuration.GetSection("Messaging:LegacyLink"));
builder.Services.Configure<AsyncFlowOptions>(builder.Configuration.GetSection("Messaging:AsyncFlow"));

builder.Services.AddScoped<SwiftSendProvider>();
builder.Services.AddScoped<LegacyLinkProvider>();
builder.Services.AddSingleton<SecurePostProvider>(); // Singleton voor token-cache
builder.Services.AddSingleton<AsyncFlowProvider>();

builder.Services.AddScoped<IMessageProvider>(sp => sp.GetRequiredService<SwiftSendProvider>());
builder.Services.AddScoped<IMessageProvider>(sp => sp.GetRequiredService<SecurePostProvider>());
builder.Services.AddScoped<IMessageProvider>(sp => sp.GetRequiredService<LegacyLinkProvider>());
builder.Services.AddScoped<IMessageProvider>(sp => sp.GetRequiredService<AsyncFlowProvider>());
builder.Services.AddScoped<IAsyncMessageProvider>(sp => sp.GetRequiredService<AsyncFlowProvider>());
builder.Services.AddScoped<IMessagingService, MessagingService>();
builder.Services.AddScoped<IMessageLogRepository, MessageLogRepository>();

// OpenMRS FHIR integratie
builder.Services.Configure<OpenMrsOptions>(builder.Configuration.GetSection("OpenMrs"));
builder.Services.AddScoped<IOpenMrsService, OpenMrsService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // HSTS + HTTPS-redirect alleen in productie (TLS 1.3 via reverse proxy)
    app.UseHsts();
    app.UseHttpsRedirection();
}

// UseCors zonder argument gebruikt de default policy en onderschept ook
// OPTIONS-preflight-verzoeken vóór de controller-routing ze verwerpt.
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityApi<IdentityUser>();
app.MapControllers();

app.Run();
