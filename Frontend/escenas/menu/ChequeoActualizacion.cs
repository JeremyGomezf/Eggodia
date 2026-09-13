using Godot;
using System.Text;
using System.Text.Json;

/// <summary>
/// Al abrir el menú principal consulta al backend (GET /api/version) la última versión del APK.
/// Si la versión instalada (project.godot → application/config/version) es menor que la "última",
/// muestra un aviso con botón para descargar la nueva. Si es menor que la "mínima", OBLIGA a
/// actualizar (el aviso no se puede cerrar; solo queda el botón de descarga).
///
/// Sin conexión o si el backend no responde, no molesta: se salta el chequeo en silencio.
/// La URL de descarga y las versiones se configuran en el backend (appsettings.json → AppVersion),
/// así que publicar una versión nueva es: exportar el APK, subir "Ultima" en el servidor y reiniciar.
/// </summary>
public partial class ChequeoActualizacion : Node
{
	private HttpRequest _http;

	private class InfoVersion
	{
		public string Ultima { get; set; } = "0.0.0";
		public string Minima { get; set; } = "0.0.0";
		public string Url    { get; set; } = "";
	}

	public override void _Ready()
	{
		_http = new HttpRequest();
		AddChild(_http);
		_http.RequestCompleted += OnRespuesta;
		// Si algo falla al pedir, no pasa nada: nunca bloqueamos por un fallo de red.
		if (_http.Request($"{ApiConfig.Base}/api/version") != Error.Ok)
			QueueFree();
	}

	private void OnRespuesta(long result, long code, string[] headers, byte[] body)
	{
		if (result != (long)HttpRequest.Result.Success || code != 200) { QueueFree(); return; }
		try
		{
			string json = Encoding.UTF8.GetString(body);
			var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			var info = JsonSerializer.Deserialize<InfoVersion>(json, opts);
			if (info == null) { QueueFree(); return; }

			string actual = (string)ProjectSettings.GetSetting("application/config/version", "0.0.0");
			bool hayNueva    = Comparar(actual, info.Ultima) < 0;
			bool obligatoria = Comparar(actual, info.Minima) < 0;

			if (hayNueva) MostrarAviso(actual, info, obligatoria);
			else QueueFree();
		}
		catch { QueueFree(); }
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

	private void MostrarAviso(string actual, InfoVersion info, bool obligatoria)
	{
		var capa = new CanvasLayer { Layer = 300 };
		AddChild(capa);

		var fondo = new ColorRect();
		fondo.Color = new Color(0.02f, 0.03f, 0.06f, 0.9f);
		fondo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		fondo.MouseFilter = Control.MouseFilterEnum.Stop; // bloquea el menú de atrás
		capa.AddChild(fondo);

		var caja = new VBoxContainer();
		caja.SetAnchorsPreset(Control.LayoutPreset.Center);
		caja.AddThemeConstantOverride("separation", 26);
		caja.OffsetLeft = -420; caja.OffsetRight = 420; caja.OffsetTop = -230; caja.OffsetBottom = 230;
		fondo.AddChild(caja);

		var titulo = new Label();
		titulo.Text = "¡Actualización disponible!";
		titulo.HorizontalAlignment = HorizontalAlignment.Center;
		titulo.AddThemeFontSizeOverride("font_size", 56);
		titulo.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
		caja.AddChild(titulo);

		var texto = new Label();
		texto.Text = obligatoria
			? $"Tu versión ({actual}) ya no es compatible.\nActualiza para seguir jugando."
			: $"Hay una versión nueva ({info.Ultima}).\nTú tienes la {actual}. ¡Descarga la última!";
		texto.HorizontalAlignment = HorizontalAlignment.Center;
		texto.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		texto.AddThemeFontSizeOverride("font_size", 34);
		texto.AddThemeColorOverride("font_color", new Color(0.9f, 0.93f, 0.98f));
		caja.AddChild(texto);

		var fila = new HBoxContainer();
		fila.Alignment = BoxContainer.AlignmentMode.Center;
		fila.AddThemeConstantOverride("separation", 24);
		caja.AddChild(fila);

		var btnDescargar = new Button();
		btnDescargar.Text = "DESCARGAR";
		btnDescargar.CustomMinimumSize = new Vector2(300, 96);
		btnDescargar.AddThemeFontSizeOverride("font_size", 38);
		btnDescargar.Pressed += () => { if (!string.IsNullOrEmpty(info.Url)) OS.ShellOpen(info.Url); };
		fila.AddChild(btnDescargar);

		if (!obligatoria)
		{
			var btnDespues = new Button();
			btnDespues.Text = "DESPUÉS";
			btnDespues.CustomMinimumSize = new Vector2(240, 96);
			btnDespues.AddThemeFontSizeOverride("font_size", 34);
			btnDespues.Pressed += () => QueueFree(); // libera capa + este nodo
			fila.AddChild(btnDespues);
		}
	}
}
