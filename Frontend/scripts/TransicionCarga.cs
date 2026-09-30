using Godot;
using System.Collections.Generic;

/// <summary>
/// Entrada a una partida SIN congelar el juego.
///
/// Antes, "VS BOT" hacía ChangeSceneToFile(campo_1) de golpe en el hilo principal: cargar el
/// campo + las tropas del mazo (con sus hojas de animación enormes) dejaba la pantalla congelada
/// 8–10 s en el celular, con el botón "apretado" y sin ninguna señal — parecía que no entraba, y
/// si se tocaba algo mientras tanto Android podía mostrar "la app no responde".
///
/// Ahora aparece al instante una pantalla de carga (fondo del juego + "Cargando partida…" + %),
/// que bloquea los toques, y la escena + las tropas del mazo se cargan EN SEGUNDO PLANO. Recién
/// cuando todo está listo se cambia de escena. Las tropas quedan retenidas en caché unos segundos
/// para que el Campo1 las encuentre ya cargadas (su GD.Load pasa a ser instantáneo).
///
/// Uso: TransicionCarga.Ir(this, "res://escenas/gameplay/campo_1.tscn", SesionJuego.Instance?.MazoSeleccionado);
/// </summary>
public partial class TransicionCarga : CanvasLayer
{
	private const string RUTA_FONDO = "res://imagenes/Fondo_de_pantalla_eggodia.png";

	private string _destino;
	private readonly List<string> _pendientes = new();
	private readonly List<string> _todas = new();
	private Label _lblPorcentaje;
	private ProgressBar _barra;
	private bool _cambiando;

	// Una sola transición a la vez (evita que un doble toque lance dos cargas).
	private static bool _enCurso;

	// Referencias a lo precargado: mientras existan, la caché de Godot no lo libera y el GD.Load del
	// Campo1 lo encuentra listo. Se sueltan unos segundos después de entrar a la partida.
	private static readonly List<Resource> _retenidos = new();

	public static void Ir(Node desde, string rutaEscena, IEnumerable<string> precargar = null)
	{
		if (_enCurso || desde == null || !desde.IsInsideTree()) return;
		_enCurso = true;

		var t = new TransicionCarga { _destino = rutaEscena, Layer = 1000, ProcessMode = ProcessModeEnum.Always };
		t._todas.Add(rutaEscena);
		if (precargar != null)
			foreach (string r in precargar)
				if (!string.IsNullOrEmpty(r) && !t._todas.Contains(r)) t._todas.Add(r);
		desde.GetTree().Root.AddChild(t);
	}

	public override void _Ready()
	{
		ConstruirPantalla();

		foreach (string ruta in _todas)
		{
			if (!ResourceLoader.Exists(ruta)) continue;
			// Si ya está en caché (p. ej. volviendo a jugar) no hace falta pedirla de nuevo.
			if (ResourceLoader.HasCached(ruta)) { _retenidos.Add(ResourceLoader.Load(ruta)); continue; }
			if (ResourceLoader.LoadThreadedRequest(ruta, "", true) == Error.Ok) _pendientes.Add(ruta);
		}
	}

	public override void _Process(double delta)
	{
		if (_cambiando) return;

		float progreso = 0f;
		var p = new Godot.Collections.Array();
		for (int i = _pendientes.Count - 1; i >= 0; i--)
		{
			var estado = ResourceLoader.LoadThreadedGetStatus(_pendientes[i], p);
			if (estado == ResourceLoader.ThreadLoadStatus.Loaded)
			{
				var res = ResourceLoader.LoadThreadedGet(_pendientes[i]);
				if (res != null) _retenidos.Add(res);
				_pendientes.RemoveAt(i);
			}
			else if (estado == ResourceLoader.ThreadLoadStatus.Failed || estado == ResourceLoader.ThreadLoadStatus.InvalidResource)
			{
				_pendientes.RemoveAt(i); // el Campo1 la cargará por su cuenta como antes
			}
			else if (p.Count > 0) progreso += (float)p[0];
		}

		int total = Mathf.Max(1, _todas.Count);
		float fraccion = Mathf.Clamp((total - _pendientes.Count + progreso) / total, 0f, 1f);
		if (_barra != null) _barra.Value = fraccion * 100f;
		if (_lblPorcentaje != null) _lblPorcentaje.Text = $"{Mathf.RoundToInt(fraccion * 100f)}%";

		if (_pendientes.Count == 0) Entrar();
	}

	private void Entrar()
	{
		_cambiando = true;
		var escena = ResourceLoader.Load<PackedScene>(_destino); // ya está en caché: instantáneo
		var tree = GetTree();
		if (escena != null) tree.ChangeSceneToPacked(escena);
		else tree.ChangeSceneToFile(_destino);

		// La capa se va un instante después de que la partida ya dibujó su primer frame (sin parpadeo),
		// y las tropas retenidas se sueltan cuando el Campo1 ya las tomó.
		tree.CreateTimer(0.25, processAlways: true).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); _enCurso = false; };
		tree.CreateTimer(5.0, processAlways: true).Timeout += () => _retenidos.Clear();
	}

	private void ConstruirPantalla()
	{
		var fondo = new TextureRect
		{
			ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = Control.MouseFilterEnum.Stop, // nada de toques dobles mientras carga
		};
		if (ResourceLoader.Exists(RUTA_FONDO)) fondo.Texture = GD.Load<Texture2D>(RUTA_FONDO);
		fondo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(fondo);

		var velo = new ColorRect { Color = new Color(0.02f, 0.03f, 0.07f, 0.55f), MouseFilter = Control.MouseFilterEnum.Ignore };
		velo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		fondo.AddChild(velo);

		var caja = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		caja.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		caja.AddThemeConstantOverride("separation", 22);
		fondo.AddChild(caja);

		var titulo = new Label { Text = "CARGANDO PARTIDA…", HorizontalAlignment = HorizontalAlignment.Center };
		EstiloUI.Titulo(titulo, 56);
		titulo.AddThemeConstantOverride("outline_size", 10);
		titulo.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
		caja.AddChild(titulo);

		var centro = new CenterContainer();
		caja.AddChild(centro);
		_barra = new ProgressBar { CustomMinimumSize = new Vector2(620, 26), ShowPercentage = false, MaxValue = 100 };
		var sbFondo = new StyleBoxFlat { BgColor = new Color(0.05f, 0.07f, 0.13f, 0.9f), BorderColor = EstiloUI.OroBorde };
		sbFondo.BorderWidthLeft = sbFondo.BorderWidthTop = sbFondo.BorderWidthRight = sbFondo.BorderWidthBottom = 3;
		sbFondo.CornerRadiusTopLeft = sbFondo.CornerRadiusTopRight = sbFondo.CornerRadiusBottomLeft = sbFondo.CornerRadiusBottomRight = 13;
		var sbRelleno = new StyleBoxFlat { BgColor = EstiloUI.Dorado };
		sbRelleno.CornerRadiusTopLeft = sbRelleno.CornerRadiusTopRight = sbRelleno.CornerRadiusBottomLeft = sbRelleno.CornerRadiusBottomRight = 11;
		_barra.AddThemeStyleboxOverride("background", sbFondo);
		_barra.AddThemeStyleboxOverride("fill", sbRelleno);
		centro.AddChild(_barra);

		_lblPorcentaje = new Label { Text = "0%", HorizontalAlignment = HorizontalAlignment.Center };
		EstiloUI.Texto(_lblPorcentaje, 34, EstiloUI.TextoClaro);
		caja.AddChild(_lblPorcentaje);
	}
}
