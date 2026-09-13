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
			if (Comparar(actual, info.Ultima) < 0) MostrarAviso(actual, info); // hay versión nueva → obligar
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

		// Único botón: DESCARGAR. No hay forma de cerrar el aviso ni de seguir sin actualizar.
		var btnDescargar = new Button();
		btnDescargar.Text = "DESCARGAR";
		btnDescargar.CustomMinimumSize = new Vector2(340, 104);
		btnDescargar.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		btnDescargar.AddThemeFontSizeOverride("font_size", 40);
		btnDescargar.Pressed += () => { if (!string.IsNullOrEmpty(info.Url)) OS.ShellOpen(info.Url); };
		caja.AddChild(btnDescargar);
	}
}
