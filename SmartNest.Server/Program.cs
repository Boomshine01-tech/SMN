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
    // Npgsql supporte nativement le format postgresql:// — on évite System.Uri
    // qui plante sur les mots de passe Supabase contenant des caractères spéciaux
    // (crochets, arobase, etc.) ou sur le username "postgres.xxxxx" du pooler.
    bool isSupabase = databaseUrl.Contains("supabase");
    Console.WriteLine($" DATABASE_URL détectée ({(isSupabase ? "Supabase" : "Render/autre")})");

    // Npgsql accepte les paramètres SSL directement en query string sur l'URL
    string sslParams = "sslmode=require&Trust Server Certificate=true";
    if (isSupabase) sslParams += "&Pooling=false";

    // Ajoute les params SSL seulement s'ils ne sont pas déjà dans l'URL
    if (!databaseUrl.Contains("sslmode"))
    {
        string sep = databaseUrl.Contains('?') ? "&" : "?";
        databaseUrl += $"{sep}{sslParams}";
    }

    connectionString = databaseUrl;
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
