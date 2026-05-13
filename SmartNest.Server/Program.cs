using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartNest.Server.Data;
using SmartNest.Server.Hubs;
using SmartNest.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── Configuration depuis variables d'environnement (Render) ─────────────────
// Render fournit DATABASE_URL, JWT_SECRET, TURN_* via le dashboard

// ─── Base de données PostgreSQL ───────────────────────────────────────────────

string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

if (!string.IsNullOrEmpty(databaseUrl))
{
    var uri      = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2);
    var host     = uri.Host;
    var port     = uri.Port > 0 ? uri.Port : 5432;
    var database = uri.AbsolutePath.Trim('/');
    var username = Uri.UnescapeDataString(userInfo[0]);
    var password = Uri.UnescapeDataString(userInfo[1]);

    // Supabase utilise le port 5432 (direct) ou 6543 (pgBouncer/pooler).
    // EF Core Migrations nécessite une connexion directe → port 5432.
    // Pour Supabase, on désactive le pooling interne de Npgsql afin
    // d'éviter les conflits avec pgBouncer si l'URL pointe vers le port 6543.
    bool isSupabase = host.Contains("supabase.co");

    Console.WriteLine($" DATABASE_URL détectée ({(isSupabase ? "Supabase" : "Render/autre")})");
    Console.WriteLine($" PostgreSQL Host     : {host}");
    Console.WriteLine($" PostgreSQL Port     : {port}");
    Console.WriteLine($" PostgreSQL Database : {database}");
    Console.WriteLine($" PostgreSQL User     : {username}");

    connectionString =
        $"Host={host};" +
        $"Port={port};" +
        $"Database={database};" +
        $"Username={username};" +
        $"Password={password};" +
        $"SSL Mode=Require;" +
        $"Trust Server Certificate=true;" +
        (isSupabase ? "Pooling=false;" : "");
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
              ?? "Host=localhost;Database=smartnest;Username=postgres;Password=postgres";
}

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connectionString));

// ─── Services applicatifs ─────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSignalR();
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddRazorPages();

// ─── JWT ──────────────────────────────────────────────────────────────────────
var jwtSecret   = Environment.GetEnvironmentVariable("JWT_SECRET")
                  ?? builder.Configuration["Jwt:Secret"]
                  ?? throw new InvalidOperationException("JWT_SECRET manquant.");
var jwtIssuer   = builder.Configuration["Jwt:Issuer"]   ?? "SmartNest";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SmartNestClient";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAudience,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew                = TimeSpan.FromMinutes(5)

        };
        // SignalR a besoin du token depuis la query string
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var access = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(access) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = access;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ─── CORS ─────────────────────────────────────────────────────────────────────
var allowedOrigins = (Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")
    ?? builder.Configuration["AllowedOrigins"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);

builder.Services.AddCors(opt => opt.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    else
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
}));

// ─── Swagger (dev uniquement) ─────────────────────────────────────────────────
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

// ─── PORT Render ─────────────────────────────────────────────────────────────
var portEnv = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{portEnv}");

var app = builder.Build();

// ─── Migrations automatiques ──────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try { db.Database.Migrate(); }
    catch { db.Database.EnsureCreated(); }
}

// ─── Pipeline ─────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseWebAssemblyDebugging();
}

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
app.MapHub<WebRtcHub>("/hubs/webrtc");
app.MapFallbackToFile("index.html");

app.Run();
