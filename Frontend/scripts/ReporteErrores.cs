using Godot;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Reporte automático de errores al servidor (POST /api/errores). Antes los errores del juego en el
/// celular solo se veían conectándolo por cable y leyendo el logcat; ahora llegan solos y se revisan
/// con GET /api/errores?clave=... (la clave del panel de administración).
///
/// Se engancha al motor con OS.AddLogger: recibe los errores de Godot y las excepciones de C# no
/// atrapadas. Para no inundar el servidor: cada error distinto se manda UNA vez por sesión, como mucho
/// MAX_POR_SESION en total, en tandas cada INTERVALO_ENVIO_SEG. Las advertencias no se mandan. Desde
/// el editor no se reporta nada (ahí los errores se ven en la consola).
///
/// Ojo: _LogError puede llamarse desde cualquier hilo y no debe imprimir nada (se llamaría a sí mismo):
/// solo guarda el error en una lista; el envío lo hace el hilo principal (Enviar, con un Timer).
/// </summary>
public partial class ReporteErrores : Logger
{
	private const int MAX_POR_SESION = 30;
	private const double INTERVALO_ENVIO_SEG = 20.0;

	// Errores conocidos que no son del juego (p. ej. el audio del sistema caído en algunos celulares).
	private static readonly string[] IGNORAR = { "audio_driver_opensl" };

	private static ReporteErrores _instancia; // referencia viva: si el GC la junta, el motor pierde el logger
	private static readonly object _lock = new();
	private static readonly List<Dictionary<string, string>> _pendientes = new();
	private static readonly HashSet<string> _vistos = new();
	private static int _aceptados = 0;
	private static Node _nodoEnvio;

	/// <summary>Se llama una vez al arrancar (SesionJuego._Ready).</summary>
	public static void Instalar(Node nodo)
	{
		// En el editor no (ahí se ven en la consola), salvo para probarlo: EGGODIA_REPORTAR_ERRORES=1.
		bool forzado = OS.GetEnvironment("EGGODIA_REPORTAR_ERRORES") == "1";
		if (_instancia != null || (OS.HasFeature("editor") && !forzado)) return;
		_instancia = new ReporteErrores();
		OS.AddLogger(_instancia);
		_nodoEnvio = nodo;
		var timer = new Timer { WaitTime = INTERVALO_ENVIO_SEG, OneShot = false, Autostart = true };
		nodo.AddChild(timer);
		timer.Timeout += Enviar;
	}

	public override void _LogError(string function, string file, int line, string code, string rationale,
		bool editorNotify, int errorType, Godot.Collections.Array<ScriptBacktrace> scriptBacktraces)
	{
		if (errorType == (int)ErrorType.Warning) return;
		string mensaje = string.IsNullOrEmpty(rationale) ? code : rationale;
		if (string.IsNullOrEmpty(mensaje)) return;
		foreach (string ignorar in IGNORAR)
			if ((file ?? "").Contains(ignorar) || mensaje.Contains(ignorar)) return;

		string clave = $"{file}:{line}:{mensaje}";
		lock (_lock)
		{
			if (_aceptados >= MAX_POR_SESION || !_vistos.Add(clave)) return;
			_aceptados++;
			_pendientes.Add(new Dictionary<string, string>
			{
				{ "mensaje", mensaje },
				{ "detalle", $"{code}\n{function} ({file}:{line})" },
			});
		}
	}

	/// <summary>Manda lo acumulado (hilo principal). Si falla la red, se pierde: no vale la pena
	/// reintentar un reporte de errores y arriesgar llenar la memoria.</summary>
	public static void Enviar()
	{
		if (_nodoEnvio == null || !GodotObject.IsInstanceValid(_nodoEnvio)) return;
		List<Dictionary<string, string>> tanda;
		lock (_lock)
		{
			if (_pendientes.Count == 0) return;
			tanda = new List<Dictionary<string, string>>(_pendientes);
			_pendientes.Clear();
		}

		string cuerpo = JsonSerializer.Serialize(new
		{
			version = (string)ProjectSettings.GetSetting("application/config/version", ""),
			plataforma = $"{OS.GetName()} {OS.GetModelName()}",
			usuarioId = SesionJuego.Instance?.UsuarioId ?? -1,
			errores = tanda,
		});
		var h = new HttpRequest { Timeout = 15 };
		_nodoEnvio.AddChild(h);
		h.RequestCompleted += (long r, long c, string[] hd, byte[] b) => { if (GodotObject.IsInstanceValid(h)) h.QueueFree(); };
		string[] hdr = { "Content-Type: application/json" };
		if (h.Request($"{ApiConfig.Base}/api/errores", hdr, HttpClient.Method.Post, cuerpo) != Error.Ok)
			h.QueueFree();
	}
}
