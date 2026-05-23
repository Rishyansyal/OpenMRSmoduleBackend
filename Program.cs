using System.Text;
using Application.Auth;
using Application.OpenMrs;
using Application.Security;
using Infrastructure.Messaging.Consumers;
using Infrastructure.Observability;
using MassTransit;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Application.Reminders;
using Application.Messaging;
using Infrastructure.Auth;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Options;
using Application.DataRetention;
using Infrastructure.DataRetention;
using Infrastructure.OpenMrs;
using Infrastructure.Reminders;
using Infrastructure.Messaging.Providers;
using Infrastructure.Persistence;
using Infrastructure.Security;
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

// Encryptie (AES-256-GCM)
builder.Services.Configure<EncryptionOptions>(builder.Configuration.GetSection("Encryption"));
builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();

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

// OpenTelemetry
builder.Services.AddSingleton<MessagingMetrics>();
builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("OpenMRSmoduleBackend"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("MassTransit"))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter(MessagingMetrics.MeterName)
        .AddPrometheusExporter());

// MassTransit — in-memory voor dev, RabbitMQ voor productie
var rabbitMqHost = builder.Configuration["RabbitMq:Host"];
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<SendReminderConsumer>();

    if (!string.IsNullOrEmpty(rabbitMqHost))
    {
        x.UsingRabbitMq((ctx, cfg) =>
        {
            cfg.Host(rabbitMqHost, h =>
            {
                h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
            });
            cfg.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));
            cfg.ConfigureEndpoints(ctx);
        });
    }
    else
    {
        x.UsingInMemory((ctx, cfg) =>
        {
            cfg.UseMessageRetry(r => r.Immediate(3));
            cfg.ConfigureEndpoints(ctx);
        });
    }
});

// Data-retentie (14 dagen patiëntdata, 1 jaar meta-logs)
builder.Services.Configure<DataRetentionOptions>(builder.Configuration.GetSection("DataRetention"));
builder.Services.AddScoped<IDataRetentionService, DataRetentionService>();
builder.Services.AddSingleton<DataRetentionWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DataRetentionWorker>());

// Afspraakherinneringen
builder.Services.Configure<ReminderOptions>(builder.Configuration.GetSection("Reminders"));
builder.Services.AddScoped<IReminderLogRepository, ReminderLogRepository>();
builder.Services.AddSingleton<ReminderWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ReminderWorker>());

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
app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
