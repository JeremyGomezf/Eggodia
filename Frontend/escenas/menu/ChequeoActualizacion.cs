using Godot;
using System.Text;
using System.Text.Json;

/// <summary>
/// Al abrir el menú principal consulta al backend (GET /api/version) la última versión del APK.
/// Si la versión instalada (project.godot → application/config/version) es MENOR que la última,
/// muestra un aviso OBLIGATORIO: "Hay una actualización disponible, ¿deseas descargar?" — y
/// bloquea el juego (no se puede jugar) hasta que el jugador descargue e instale la nueva.
///
/// Solo bloquea cuando CONFIRMA que hay una versión más nueva. Sin conexión o si el backend no
/// responde, no molesta (se salta en silencio): así nadie queda encerrado por un fallo de red.
///
/// La versión última y la URL de descarga se configuran en el backend (appsettings.json →
/// sección AppVersion), sin recompilar.
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
	private void Avisar(bool bloquea) { if (_avisado) return; _avisado = true; AlTerminar?.Invoke(bloquea); }

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
		if (OS.HasFeature("editor")) { Avisar(false); QueueFree(); return; }

		_http = new HttpRequest();
		_http.Timeout = 5; // sin conexión no debe dejar al jugador esperando en la carga: a los 5s sigue
		AddChild(_http);
		_http.RequestCompleted += OnRespuesta;
		if (_http.Request($"{ApiConfig.Base}/api/version") != Error.Ok)
			{ Avisar(false); QueueFree(); }
	}

	private void OnRespuesta(long result, long code, string[] headers, byte[] body)
	{
		if (result != (long)HttpRequest.Result.Success || code != 200) { Avisar(false); QueueFree(); return; }
		try
		{
			string json = Encoding.UTF8.GetString(body);
			var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			var info = JsonSerializer.Deserialize<InfoVersion>(json, opts);
			if (info == null) { Avisar(false); QueueFree(); return; }

			string actual = (string)ProjectSettings.GetSetting("application/config/version", "0.0.0");
			if (Comparar(actual, info.Ultima) < 0) { MostrarAviso(actual, info); Avisar(true); } // versión nueva → obligar (el aviso queda en pantalla)
			else { Avisar(false); QueueFree(); }
		}
		catch { Avisar(false); QueueFree(); }
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

		var caja = new VBoxContainer();
		caja.SetAnchorsPreset(Control.LayoutPreset.Center);
		caja.AddThemeConstantOverride("separation", 30);
		caja.OffsetLeft = -440; caja.OffsetRight = 440; caja.OffsetTop = -220; caja.OffsetBottom = 220;
		fondo.AddChild(caja);

		var titulo = new Label();
		titulo.Text = "Hay una actualización disponible";
		titulo.HorizontalAlignment = HorizontalAlignment.Center;
		titulo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		titulo.AddThemeFontSizeOverride("font_size", 54);
		titulo.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
		caja.AddChild(titulo);

		var texto = new Label();
		texto.Text = "¿Deseas descargar la nueva versión?\nNecesitas actualizar para seguir jugando.";
		texto.HorizontalAlignment = HorizontalAlignment.Center;
		texto.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		texto.AddThemeFontSizeOverride("font_size", 34);
		texto.AddThemeColorOverride("font_color", new Color(0.9f, 0.93f, 0.98f));
		caja.AddChild(texto);

		var btnFila = new HBoxContainer();
		btnFila.Alignment = BoxContainer.AlignmentMode.Center;
		btnFila.AddThemeConstantOverride("separation", 20);
		caja.AddChild(btnFila);

		var btnDescargar = new Button();
		btnDescargar.Text = "DESCARGAR";
		btnDescargar.CustomMinimumSize = new Vector2(300, 90);
		btnDescargar.AddThemeFontSizeOverride("font_size", 36);
		btnDescargar.Pressed += () => { if (!string.IsNullOrEmpty(info.Url)) OS.ShellOpen(info.Url); };
		btnFila.AddChild(btnDescargar);

		// Si se corre en editor o en PC (no Android), permitir omitir para pruebas y desarrollo
		if (OS.HasFeature("editor") || OS.GetName() != "Android")
		{
			var btnOmitir = new Button();
			btnOmitir.Text = "OMITIR / CANCELAR";
			btnOmitir.CustomMinimumSize = new Vector2(300, 90);
			btnOmitir.AddThemeFontSizeOverride("font_size", 32);
			btnOmitir.Pressed += () => capa.QueueFree();
			btnFila.AddChild(btnOmitir);
		}
	}
}
