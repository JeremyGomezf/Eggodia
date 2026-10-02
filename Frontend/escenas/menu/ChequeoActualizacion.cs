using Godot;
using System.Text;
using System.Text.Json;

/// <summary>
/// Consulta al backend (GET /api/version) la última versión del APK. Si la versión instalada
/// (project.godot → application/config/version) es MENOR que la última, muestra un aviso OBLIGATORIO
/// ("Hay una actualización disponible") y el juego NO avanza: en la pantalla de carga no se pasa al
/// login, y en el menú tapa todo. Corre al abrir la app (PantallaCarga), al entrar al menú y al volver
/// a la app estando en el menú.
///
/// Regla: con una actualización pendiente NUNCA se avanza, sea la primera vez que se abre el juego, se
/// haya actualizado antes o no se haya abierto nunca. Para eso:
///   • Cada respuesta del servidor se recuerda en el celular (Preferencias.VersionUltimaConocida y su
///     enlace). Si una vez se supo que había versión nueva, se bloquea aunque después no haya internet.
///   • Si la consulta falla se reintenta; si al final no hay respuesta (sin internet, servidor caído o
///     muy lento: PLAZO_SEG) se decide con lo recordado. Sin nada recordado (instalación nueva sin
///     internet) no hay forma de saberlo y se sigue: el menú vuelve a revisar al haber conexión.
///
/// La versión última y la URL de descarga se configuran en el backend (appsettings.json → sección
/// AppVersion), sin recompilar. En el editor no se revisa (salvo EGGODIA_FORZAR_CHEQUEO=1, para probar).
/// </summary>
public partial class ChequeoActualizacion : Node
{
	private HttpRequest _http;

	// Se invoca EXACTAMENTE una vez cuando el chequeo termina, con true si hay una actualización
	// obligatoria bloqueando (se mostró el aviso y NO se debe avanzar) o false si no hay nada que
	// hacer (sin versión nueva, sin conexión o corriendo en editor). Lo usa la PantallaCarga para
	// decidir si sigue al login o se queda mostrando el aviso. Puede ser null (uso sin callback).
	public System.Action<bool> AlTerminar;
	private bool _avisado = false;

	// Momento del último chequeo que respondió "estás al día". Sirve para no repetir la consulta si el
	// menú se abre segundos después de la pantalla de carga. Un fallo de red NO lo actualiza, así que
	// la próxima vez que se entre al menú se vuelve a intentar.
	private static ulong _ultimoAlDiaMs;
	private static bool  _huboChequeoAlDia;
	public static bool RevisadoHaceMenosDe(int segundos) =>
		_huboChequeoAlDia && Time.GetTicksMsec() - _ultimoAlDiaMs < (ulong)(segundos * 1000);
	private void Avisar(bool bloquea) { if (_avisado) return; _avisado = true; AlTerminar?.Invoke(bloquea); }

	private const int    MAX_INTENTOS = 3;   // un fallo puntual de red no alcanza para saltarse el aviso
	private const double PLAZO_SEG    = 12;  // tope para decidir: pasado esto se decide con lo recordado
	private int _intentos = 0;

	/// <summary>¿Se sabe (por una consulta anterior) que hay una versión más nueva que la instalada?
	/// La PantallaCarga lo usa como último recurso: si por lo que sea el chequeo no terminó, no avanza
	/// cuando esto es true.</summary>
	public static bool HayActualizacionConocida() =>
		Comparar(VersionInstalada, Preferencias.VersionUltimaConocida) < 0;

	private static string VersionInstalada =>
		(string)ProjectSettings.GetSetting("application/config/version", "0.0.0");

	private class InfoVersion
	{
		public string Ultima { get; set; } = "0.0.0";
		public string Minima { get; set; } = "0.0.0";
		public string Url    { get; set; } = "";
	}

	public override void _Ready()
	{
		// Corriendo desde el editor de Godot (F5/F6) NO molesta con el aviso de actualización: así
		// puedes desarrollar y probar sin que te bloquee aunque tu versión local sea menor que la del
		// servidor. El bloqueo solo aplica en el APK exportado (build real), que es lo que juegan los
		// usuarios: ahí "editor" no está presente en OS.HasFeature.
		bool forzado = OS.GetEnvironment("EGGODIA_FORZAR_CHEQUEO") == "1";
		if (OS.HasFeature("editor") && !forzado) { Avisar(false); QueueFree(); return; }

		_http = new HttpRequest();
		_http.Timeout = 5;
		AddChild(_http);
		_http.RequestCompleted += OnRespuesta;
		// Tope: si en PLAZO_SEG no hubo respuesta válida (red muy lenta, servidor caído), se decide con
		// la versión recordada. Nunca se avanza "a ciegas" con una actualización que ya se conocía.
		GetTree().CreateTimer(PLAZO_SEG).Timeout += () =>
		{
			if (_avisado || !IsInstanceValid(this)) return;
			_http?.CancelRequest();
			DecidirSinServidor();
		};
		Pedir();
	}

	private void Pedir()
	{
		if (_avisado) return;
		_intentos++;
		if (_http.Request($"{ApiConfig.Base}/api/version") != Error.Ok) Fallo();
	}

	private void Fallo()
	{
		if (_avisado) return;
		if (_intentos < MAX_INTENTOS)
		{
			GetTree().CreateTimer(1.0).Timeout += () => { if (IsInstanceValid(this)) Pedir(); };
			return;
		}
		DecidirSinServidor();
	}

	/// <summary>Sin respuesta del servidor: bloquea si alguna vez se supo que había una versión más
	/// nueva que la instalada; si no, deja seguir (no hay forma de saberlo).</summary>
	private void DecidirSinServidor()
	{
		if (_avisado) return;
		if (HayActualizacionConocida())
		{
			MostrarAviso(VersionInstalada, new InfoVersion
			{
				Ultima = Preferencias.VersionUltimaConocida,
				Url = Preferencias.UrlDescargaConocida,
			});
			Avisar(true);
		}
		else { Avisar(false); QueueFree(); }
	}

	private void OnRespuesta(long result, long code, string[] headers, byte[] body)
	{
		if (_avisado) return;
		if (result != (long)HttpRequest.Result.Success || code != 200) { Fallo(); return; }
		InfoVersion info = null;
		try
		{
			var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			info = JsonSerializer.Deserialize<InfoVersion>(Encoding.UTF8.GetString(body), opts);
		}
		catch { }
		if (info == null || string.IsNullOrEmpty(info.Ultima)) { Fallo(); return; }

		// Se recuerda SIEMPRE lo último que dijo el servidor (también si ahora estás al día: así, si
		// alguna vez retrocede la versión publicada, lo recordado no queda bloqueando de más).
		Preferencias.VersionUltimaConocida = info.Ultima;
		Preferencias.UrlDescargaConocida = info.Url ?? "";

		if (Comparar(VersionInstalada, info.Ultima) < 0) { MostrarAviso(VersionInstalada, info); Avisar(true); } // versión nueva → obligar (el aviso queda en pantalla)
		else { _ultimoAlDiaMs = Time.GetTicksMsec(); _huboChequeoAlDia = true; Avisar(false); QueueFree(); }
	}

	// Compara "1.2.3" numéricamente. <0 si a<b, 0 si igual, >0 si a>b.
	private static int Comparar(string a, string b)
	{
		string[] pa = (a ?? "0").Split('.'), pb = (b ?? "0").Split('.');
		int n = Mathf.Max(pa.Length, pb.Length);
		for (int i = 0; i < n; i++)
		{
			int va = i < pa.Length && int.TryParse(pa[i], out int x) ? x : 0;
			int vb = i < pb.Length && int.TryParse(pb[i], out int y) ? y : 0;
			if (va != vb) return va < vb ? -1 : 1;
		}
		return 0;
	}

	private void MostrarAviso(string actual, InfoVersion info)
	{
		var capa = new CanvasLayer { Layer = 300 };
		AddChild(capa);

		// Fondo que cubre TODO y bloquea el menú de atrás: no se puede jugar sin actualizar.
		var fondo = new ColorRect();
		fondo.Color = new Color(0.02f, 0.03f, 0.06f, 0.95f);
		fondo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		fondo.MouseFilter = Control.MouseFilterEnum.Stop;
		capa.AddChild(fondo);

		// Panel "vidrio oscuro" del juego (en vez de texto flotante sobre negro).
		var panel = new PanelContainer();
		panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		panel.OffsetLeft = -460; panel.OffsetRight = 460; panel.OffsetTop = -240; panel.OffsetBottom = 240;
		EstiloUI.Panel(panel);
		fondo.AddChild(panel);

		var caja = new VBoxContainer();
		caja.AddThemeConstantOverride("separation", 30);
		panel.AddChild(caja);

		var titulo = new Label();
		titulo.Text = "Hay una actualización disponible";
		titulo.HorizontalAlignment = HorizontalAlignment.Center;
		titulo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		EstiloUI.Titulo(titulo, 54);
		caja.AddChild(titulo);

		var texto = new Label();
		texto.Text = "¿Deseas descargar la nueva versión?\nNecesitas actualizar para seguir jugando.";
		texto.HorizontalAlignment = HorizontalAlignment.Center;
		texto.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		EstiloUI.Texto(texto, 34, EstiloUI.TextoClaro);
		caja.AddChild(texto);

		var btnFila = new HBoxContainer();
		btnFila.Alignment = BoxContainer.AlignmentMode.Center;
		btnFila.AddThemeConstantOverride("separation", 20);
		caja.AddChild(btnFila);

		var btnDescargar = new Button();
		btnDescargar.Text = "DESCARGAR";
		btnDescargar.CustomMinimumSize = new Vector2(300, 90);
		EstiloUI.Boton(btnDescargar, 36);
		btnDescargar.Pressed += () => { if (!string.IsNullOrEmpty(info.Url)) OS.ShellOpen(info.Url); };
		btnFila.AddChild(btnDescargar);

		// Si se corre en editor o en PC (no Android), permitir omitir para pruebas y desarrollo
		if (OS.HasFeature("editor") || OS.GetName() != "Android")
		{
			var btnOmitir = new Button();
			btnOmitir.Text = "OMITIR / CANCELAR";
			btnOmitir.CustomMinimumSize = new Vector2(300, 90);
			EstiloUI.Boton(btnOmitir, 32);
			btnOmitir.Pressed += () => capa.QueueFree();
			btnFila.AddChild(btnOmitir);
		}
	}
}
