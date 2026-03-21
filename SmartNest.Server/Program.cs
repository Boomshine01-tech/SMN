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
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string connStr;

if (!string.IsNullOrEmpty(databaseUrl))
{
    // Format Render : postgresql://user:pass@host:port/db
    var uri  = new Uri(databaseUrl);
    var info = uri.UserInfo.Split(':');
    connStr  = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={info[0]};Password={info[1]};SSL Mode=Require;Trust Server Certificate=true";
}
else
{
    connStr = builder.Configuration.GetConnectionString("DefaultConnection")
              ?? "Host=localhost;Database=smartnest;Username=postgres;Password=postgres";
}

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connStr));

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
            ClockSkew                = TimeSpan.Zero
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
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

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
