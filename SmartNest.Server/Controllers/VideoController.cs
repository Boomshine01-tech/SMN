using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using SmartNest.Shared.DTOs;

namespace SmartNest.Server.Controllers;

[ApiController, Route("api/video")]
public class VideoController : ControllerBase
{
    private readonly IWebHostEnvironment   _env;
    private readonly IConfiguration  _config;
    private readonly ILogger<VideoController> _log;

    private int    Uid      => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
    private string VideoDir => Path.Combine(_env.ContentRootPath, "videos", Uid.ToString());

    public VideoController(IWebHostEnvironment env, ILogger<VideoController> log) { _env = env; _log = log; }

    private void EnsureDir() => Directory.CreateDirectory(VideoDir);

    [HttpPost("upload")]
    
    [RequestSizeLimit(500_000_000)]
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string? sessionId)
    {
        if (file == null || file.Length == 0) return BadRequest(new { message = "Fichier vide." });
        EnsureDir();
        var ts       = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var ext      = Path.GetExtension(file.FileName).ToLower();
        if (string.IsNullOrEmpty(ext)) ext = ".webm";
        var fileName = $"rec_{ts}_{sessionId ?? "s"}{ext}";
        var path     = Path.Combine(VideoDir, fileName);
        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);
        _log.LogInformation("[Video] Upload: {File} ({Size} o)", fileName, file.Length);
        return Ok(new VideoRecordingDto { FileName = fileName, Url = $"/api/video/stream/{Uid}/{fileName}", RecordedAt = DateTime.UtcNow, SizeBytes = file.Length });
    }

    [HttpGet("list")]
    public IActionResult List()
    {
        EnsureDir();
        var files = Directory.GetFiles(VideoDir, "*.webm")
            .Concat(Directory.GetFiles(VideoDir, "*.mp4"))
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc).Take(50)
            .Select(f => new VideoRecordingDto { FileName = f.Name, Url = $"/api/video/stream/{Uid}/{f.Name}", RecordedAt = f.CreationTimeUtc, SizeBytes = f.Length });
        return Ok(files);
    }

    [HttpGet("stream/{userId}/{fileName}")]
    [AllowAnonymous]
    public IActionResult Stream(int userId, string fileName, [FromQuery] string? t)
    {
        if (string.IsNullOrEmpty(t)) return Unauthorized();
            if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
            return BadRequest();

        // Valider le token et extraire le userId
        var secret = Environment.GetEnvironmentVariable("JWT_SECRET")
                     ?? _config["Jwt:Secret"] ?? "";
        try
        {
            var handler    = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var key        = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                                 System.Text.Encoding.UTF8.GetBytes(secret));
            handler.ValidateToken(t, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = key,
                ValidateIssuer           = false,
                ValidateAudience         = false,
                ClockSkew                = System.TimeSpan.FromMinutes(5)
            }, out var validated);

            // Extraire userId depuis le token
            var jwt      = (System.IdentityModel.Tokens.Jwt.JwtSecurityToken)validated;
            var tokenUid = jwt.Claims
                .FirstOrDefault(c => c.Type == "sub" ||
                                     c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)
                ?.Value;

            if (tokenUid != userId.ToString()) return Forbid();
        }
        catch { return Unauthorized(); }

        var path = System.IO.Path.Combine(
            _env.ContentRootPath, "videos", userId.ToString(), fileName);
        if (!System.IO.File.Exists(path)) return NotFound();

        new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider()
            .TryGetContentType(fileName, out var ct);
        return PhysicalFile(path, ct ?? "video/webm", enableRangeProcessing: true);
    }

    [HttpDelete("{fileName}")]
    public IActionResult Delete(string fileName)
    {
        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\')) return BadRequest();
        var path = Path.Combine(VideoDir, fileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        System.IO.File.Delete(path);
        return NoContent();
    }
}
