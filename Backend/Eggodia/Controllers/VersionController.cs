using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Informa al juego cuál es la última versión del APK disponible, para que el cliente avise
/// (o obligue) a actualizar. Los valores se leen de appsettings.json (sección "AppVersion"),
/// así se cambian sin recompilar: al publicar un APK nuevo, sube "Ultima" (y "Minima" si quieres
/// forzar), guarda y reinicia el servicio (sudo systemctl restart eggodia-api).
///
/// GET /api/version → { ultima, minima, url }
///   ultima : última versión publicada (si el jugador tiene una menor, se le avisa).
///   minima : versión mínima permitida (si el jugador tiene una menor, se le OBLIGA a actualizar).
///   url    : enlace de descarga del APK nuevo.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VersionController : ControllerBase
{
    private readonly IConfiguration _config;

    public VersionController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var s = _config.GetSection("AppVersion");
        return Ok(new
        {
            ultima = s["Ultima"] ?? "1.0.0",
            minima = s["Minima"] ?? "1.0.0",
            url    = s["UrlDescarga"] ?? ""
        });
    }
}
