using System.Text;
using System.Threading.RateLimiting;
using Api.Middleware;
using Application.Auth;
using Application.OpenMrs;
using Application.Organizations;
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
using Infrastructure.Configuration;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Options;
using Application.DataRetention;
using Infrastructure.DataRetention;
using Infrastructure.Health;
using Infrastructure.OpenMrs;
using Infrastructure.Organizations;
using Infrastructure.Reminders;
using Infrastructure.Messaging.Providers;
using Infrastructure.Persistence;
using Infrastructure.Security;
using Infrastructure.Webhooks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Application.Webhooks;
using Microsoft.OpenApi;

// Load .env file for local development
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
{
    foreach (var line in File.ReadAllLines(envPath))
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
        var parts = line.Split('=', 2);
        if (parts.Length != 2) continue;
        var key = parts[0].Trim();
        var value = parts[1].Trim();
        Environment.SetEnvironmentVariable(key, value);
        if (key == "JWT_SECRET")
        {
            Environment.SetEnvironmentVariable("Jwt__SecretKey", value);
        }
    }
}

var builder = WebApplication.CreateBuilder(args);

var hospitalConfigPath = builder.Configuration["HospitalConfiguration:FilePath"];
if (!string.IsNullOrWhiteSpace(hospitalConfigPath))
{
    builder.Configuration.AddJsonFile(
        hospitalConfigPath,
        optional: false,
        reloadOnChange: false);
}

// ---------------------------------------------------------------------------
// Verwijder 'Server' header (lekt technologie-info aan aanvallers)
// ---------------------------------------------------------------------------
builder.WebHost.ConfigureKestrel(opts => opts.AddServerHeader = false);

// ---------------------------------------------------------------------------
// Configuratie-validatie: fail fast bij ontbrekende verplichte secrets
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured.");

var jwtSecret = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    jwtSecret = builder.Configuration["JWT_SECRET"];
}

if (string.IsNullOrWhiteSpace(jwtSecret))
{
    throw new InvalidOperationException(
        "JWT Secret is not configured. Set Jwt:SecretKey in configuration, or Jwt__SecretKey / JWT_SECRET in environment/dotenv.");
}

if (jwtSecret.Length < 32)
    throw new InvalidOperationException(
        "Jwt:SecretKey moet minimaal 32 tekens bevatten (256 bits voor HMAC-SHA256).");

// ---------------------------------------------------------------------------
// CORS — alleen geconfigureerde origins toestaan, geen wildcard
// ---------------------------------------------------------------------------
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:3032")
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
});

// ---------------------------------------------------------------------------
// Rate limiting — beschermt endpoints tegen misbruik en brute-force aanvallen
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(opts =>
{
    // Standaard 429 respons
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opts.OnRejected = async (ctx, ct) =>
    {
        var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("Security Event: Rate limit exceeded by IP {IpAddress} on path {Path}",
            ctx.HttpContext.Connection.RemoteIpAddress,
            ctx.HttpContext.Request.Path);

        ctx.HttpContext.Response.Headers["Retry-After"] = "60";
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many requests. Please try again later." }, ct);
    };

    // Strikte policy voor login/registratie: 5 verzoeken per minuut per IP
    opts.AddPolicy("AuthPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Gemiddelde policy voor formulieren/messaging: 10 verzoeken per minuut per IP
    opts.AddPolicy("MessagePolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Algemene policy voor normale API calls: 100 verzoeken per minuut per IP
    opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

// ---------------------------------------------------------------------------
// Database (PostgreSQL + EF Core; SQLite voor lokale ontwikkeling zonder Docker)
// ---------------------------------------------------------------------------
var databaseProvider = builder.Configuration["Database:Provider"] ?? "Postgres";
if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Identity (ASP.NET Core Identity — password hashing, user management)
// MapIdentityApi is NIET gebruikt: eigen AuthController biedt betere controle
// en voorkomt blootstelling van onbeheerde /manage/* endpoints.
builder.Services
    .AddIdentityCore<IdentityUser>(opts =>
    {
        // Aangescherpte wachtwoordeisen (NIST SP 800-63B)
        opts.Password.RequireDigit = true;
        opts.Password.RequireLowercase = true;
        opts.Password.RequireUppercase = false;  // NIST raadt af dit te verplichten
        opts.Password.RequireNonAlphanumeric = false;
        opts.Password.RequiredLength = 12;     // Verhoogd van 8 naar 12 tekens

        // Account lockout na herhaalde foute pogingen
        opts.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        opts.Lockout.MaxFailedAccessAttempts = 5;
        opts.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.Configure<AdminBootstrapOptions>(builder.Configuration.GetSection("Admin"));
builder.Services.AddScoped<AdminBootstrapSeeder>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

// ---------------------------------------------------------------------------
// Messaging providers (externe communicatieplatformen)
// ---------------------------------------------------------------------------
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("openmrs", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.Configure<HospitalConfigurationOptions>(builder.Configuration.GetSection("HospitalConfiguration"));
builder.Services.AddScoped<IOrganizationConfigRepository, OrganizationConfigRepository>();
builder.Services.AddScoped<OrganizationConfigSeeder>();
builder.Services.Configure<MessagingOptions>(builder.Configuration.GetSection("Messaging"));
builder.Services.Configure<SwiftSendOptions>(builder.Configuration.GetSection("Messaging:SwiftSend"));
builder.Services.Configure<SecurePostOptions>(builder.Configuration.GetSection("Messaging:SecurePost"));
builder.Services.Configure<LegacyLinkOptions>(builder.Configuration.GetSection("Messaging:LegacyLink"));
builder.Services.Configure<AsyncFlowOptions>(builder.Configuration.GetSection("Messaging:AsyncFlow"));

builder.Services.AddScoped<SwiftSendProvider>();
builder.Services.AddScoped<LegacyLinkProvider>();
builder.Services.AddSingleton<SecurePostProvider>(); // Singleton voor thread-veilige token-cache
builder.Services.AddSingleton<AsyncFlowProvider>();

builder.Services.AddScoped<IMessageProvider>(sp => sp.GetRequiredService<SwiftSendProvider>());
builder.Services.AddScoped<IMessageProvider>(sp => sp.GetRequiredService<SecurePostProvider>());
builder.Services.AddScoped<IMessageProvider>(sp => sp.GetRequiredService<LegacyLinkProvider>());
builder.Services.AddScoped<IMessageProvider>(sp => sp.GetRequiredService<AsyncFlowProvider>());
builder.Services.AddScoped<IAsyncMessageProvider>(sp => sp.GetRequiredService<AsyncFlowProvider>());
builder.Services.AddScoped<IMessagingService, MessagingService>();
builder.Services.AddScoped<IMessageLogRepository, MessageLogRepository>();

// ---------------------------------------------------------------------------
// OpenMRS FHIR integratie
// ---------------------------------------------------------------------------
builder.Services.Configure<OpenMrsOptions>(builder.Configuration.GetSection("OpenMrs"));
builder.Services.Configure<OpenMrsPollerOptions>(builder.Configuration.GetSection("OpenMrs:Poller"));
builder.Services.AddScoped<IOpenMrsService, OpenMrsService>();
builder.Services.AddHostedService<OpenMrsPollWorker>();

// ---------------------------------------------------------------------------
// OpenMRS webhook integratie
// ---------------------------------------------------------------------------
builder.Services.Configure<OpenMrsWebhookOptions>(builder.Configuration.GetSection("Webhooks:OpenMrs"));
builder.Services.AddScoped<IOpenMrsWebhookSignatureValidator, OpenMrsWebhookSignatureValidator>();
builder.Services.Configure<EncryptionOptions>(builder.Configuration.GetSection("Encryption"));
builder.Services.AddScoped<IEncryptionService, AesEncryptionService>();
builder.Services.AddScoped<IFieldEncryptionService, FieldEncryptionService>();
builder.Services.AddScoped<IOpenMrsWebhookService, OpenMrsWebhookService>();

// ---------------------------------------------------------------------------
// OpenTelemetry (tracing + metrics)
// ---------------------------------------------------------------------------
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

// ---------------------------------------------------------------------------
// MassTransit — RabbitMQ buiten IntegrationTest; in-memory alleen voor tests
// ---------------------------------------------------------------------------
var rabbitMqHost = builder.Configuration["RabbitMq:Host"];
var requiresDurableBroker =
    !builder.Environment.IsEnvironment("IntegrationTest");
if (requiresDurableBroker && string.IsNullOrWhiteSpace(rabbitMqHost))
{
    throw new InvalidOperationException(
        "RabbitMq:Host is required outside IntegrationTest. In-memory queueing is not durable and is not allowed for development runtime verification.");
}

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<SendReminderConsumer>();

    if (!string.IsNullOrEmpty(rabbitMqHost))
    {
        var rabbitMqUsername = builder.Configuration["RabbitMq:Username"];
        var rabbitMqPassword = builder.Configuration["RabbitMq:Password"];
        if (string.IsNullOrWhiteSpace(rabbitMqUsername) || string.IsNullOrWhiteSpace(rabbitMqPassword))
        {
            throw new InvalidOperationException(
                "RabbitMq:Username and RabbitMq:Password must be configured when RabbitMq:Host is set.");
        }

        x.UsingRabbitMq((ctx, cfg) =>
        {
            cfg.Host(rabbitMqHost, h =>
            {
                h.Username(rabbitMqUsername);
                h.Password(rabbitMqPassword);
            });
            cfg.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));
            cfg.ConfigureEndpoints(ctx);
        });
    }
    else if (builder.Environment.IsEnvironment("IntegrationTest"))
    {
        x.UsingInMemory((ctx, cfg) =>
        {
            cfg.UseMessageRetry(r => r.Immediate(3));
            cfg.ConfigureEndpoints(ctx);
        });
    }
    else
    {
        throw new InvalidOperationException("RabbitMQ transport is required outside IntegrationTest.");
    }
});

// ---------------------------------------------------------------------------
// Data-retentie (14 dagen patiëntdata, 1 jaar meta-logs)
// ---------------------------------------------------------------------------
builder.Services.Configure<DataRetentionOptions>(builder.Configuration.GetSection("DataRetention"));
builder.Services.AddScoped<IDataRetentionService, DataRetentionService>();
builder.Services.AddSingleton<DataRetentionWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DataRetentionWorker>());

// ---------------------------------------------------------------------------
// Afspraakherinneringen
// ---------------------------------------------------------------------------
builder.Services.Configure<ReminderOptions>(builder.Configuration.GetSection("Reminders"));
builder.Services.AddScoped<IReminderLogRepository, ReminderLogRepository>();
builder.Services.AddScoped<IScheduledReminderRepository, ScheduledReminderRepository>();
builder.Services.AddScoped<IMessageTemplateRepository, MessageTemplateRepository>();
builder.Services.AddScoped<IReminderMessageRenderer, ReminderMessageRenderer>();
builder.Services.AddSingleton<ReminderWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ReminderWorker>());

// ---------------------------------------------------------------------------
// JWT-authenticatie
// ---------------------------------------------------------------------------
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
            ClockSkew = TimeSpan.Zero  // Tokens verlopen op de exacte exp-time
        };
    });

// ---------------------------------------------------------------------------
// Autorisatie — Default policy: alle endpoints vereisen authenticatie.
// Endpoints die publiek moeten zijn krijgen expliciet [AllowAnonymous].
// ---------------------------------------------------------------------------
builder.Services.AddAuthorization(opts =>
{
    opts.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    opts.AddPolicy(AuthPolicies.AdminOnly, policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole(AuthPolicies.AdminRole));

    opts.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ---------------------------------------------------------------------------
// API & Swagger (met JWT Bearer auth in de UI)
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OpenMRS Communication Backend API",
        Version = "v1",
        Description = "Backend-only API for OpenMRS appointment reminders, messaging providers, durable retry handling, and signed OpenMRS webhooks."
    });

    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT bearer token from /auth/login. Example: Bearer eyJhbGciOi...",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    };

    options.AddSecurityDefinition("Bearer", bearerScheme);
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, "OpenMRSmoduleBackend.xml");
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});
builder.Services.AddOpenApi();


// ---------------------------------------------------------------------------
// Cookie-beleid (SameSite=Strict — beschermt tegen CSRF)
// ---------------------------------------------------------------------------
builder.Services.Configure<CookiePolicyOptions>(opts =>
{
    opts.MinimumSameSitePolicy = SameSiteMode.Strict;
    opts.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    opts.Secure = CookieSecurePolicy.Always;
});

// ============================================================================
// Applicatie-pipeline
// ============================================================================
var app = builder.Build();

// Automatische databasemigratie bij opstarten (kan uitgeschakeld worden via Database:RunMigrations)
if (app.Configuration.GetValue("Database:RunMigrations", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    // Seed standaard berichtsjablonen als de tabel leeg is
    if (!db.MessageTemplates.Any())
    {
        db.MessageTemplates.AddRange(
            new Domain.MessageTemplate
            {
                Window = "24h",
                Body = "Herinnering: u heeft morgen een {type} op {tijd}. Neem contact op bij vragen.",
                UpdatedAtUtc = DateTime.UtcNow
            },
            new Domain.MessageTemplate
            {
                Window = "1h",
                Body = "Herinnering: u heeft over ongeveer 1 uur een {type} op {tijd}.",
                UpdatedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();
    }

}

using (var seedScope = app.Services.CreateScope())
{
    if (app.Configuration.GetValue("Admin:SeedOnStartup", true))
        await seedScope.ServiceProvider.GetRequiredService<AdminBootstrapSeeder>().SeedAsync();
    if (app.Configuration.GetValue("HospitalConfiguration:SeedOnStartup", true))
        await seedScope.ServiceProvider.GetRequiredService<OrganizationConfigSeeder>().SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OpenMRS Module API v1");
        c.DocumentTitle = "OpenMRS Module API — Swagger UI";
    });
}
if (!app.Environment.IsDevelopment())
{
    // HSTS + HTTPS-redirect alleen in productie (TLS via reverse proxy)
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Global Exception Handler om leaking van stacktraces te voorkomen
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "Internal Server Error" });
    });
});

// Security headers (X-Frame-Options, CSP, etc.) — zo vroeg mogelijk in de pipeline
app.UseMiddleware<SecurityHeadersMiddleware>();

// Security events logging (401 Unauthorized / 403 Forbidden)
app.UseMiddleware<SecurityLoggingMiddleware>();

// Cookie-beleid (SameSite=Strict)
app.UseCookiePolicy();

// Routing expliciet uitvoeren zodat endpoint-metadata beschikbaar is
// voor middleware zoals rate limiting.
app.UseRouting();

// CORS — vóór authenticatie/autorisatie
// UseCors onderschept ook OPTIONS-preflight-verzoeken vóór controllers ze verwerpen
app.UseCors();

// Rate limiting — na routing zodat endpoint-specifieke policies via
// [EnableRateLimiting] betrouwbaar toegepast kunnen worden.
// In de IntegrationTest-omgeving uitgeschakeld omdat WebApplicationFactory alle requests
// vanaf loopback stuurt en de limiter dan tests blokkeert die meerdere users registreren.
if (!app.Environment.IsEnvironment("IntegrationTest"))
{
    app.UseRateLimiter();
}

// Prometheus metrics endpoint — alleen bereikbaar op intern pad
// In productie: beveilig met IP-allowlist of apart netwerk
app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.UseAuthentication();

// Map controllers. Algemene rate limiting (GlobalLimiter) geldt voor alles,
// tenzij overschreven door specifieke [EnableRateLimiting] attributen.
app.UseAuthorization();
app.MapHealthChecks("/health/readiness").AllowAnonymous();
app.MapControllers();

app.Run();

public partial class Program;
