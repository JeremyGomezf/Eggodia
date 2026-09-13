using Godot;
using System.Collections.Generic;

/// <summary>
/// PantallaCarga — primera escena que ve el jugador. Precarga en segundo plano (hilos de
/// Godot) todos los scripts/escenas de las 17 cartas, las 5 skins de huevo y los efectos de
/// habilidad, mostrando una barra de progreso morada sobre un fondo fijo. Al terminar, pasa a
/// la pantalla de login.
/// </summary>
public partial class PantallaCarga : Control
{
	private const string RUTA_SIGUIENTE = "res://escenas/menu/PanelLogin.tscn";

	private static readonly string[] RUTAS_A_PRECARGAR =
	{
		// Cartas (las 17 tropas)
		"res://cartas prime/MEDIEVAL/Dragon_prime.tscn",
		"res://cartas prime/MEDIEVAL/Golem_prime.tscn",
		"res://cartas prime/MEDIEVAL/Maguin_prime.tscn",
		"res://cartas prime/MEDIEVAL/Machi_prime.tscn",
		"res://cartas prime/MEDIEVAL/SoldadoReal_prime.tscn",
		"res://cartas prime/MEDIEVAL/TortugaYPescado.tscn",
		"res://cartas prime/PAPEL/Paper_Rex.tscn",
		"res://cartas prime/PACIFICO/Tiburon_prime.tscn",
		"res://cartas prime/PACIFICO/CalamarG_prime.tscn",
		"res://cartas prime/AJEDREZ/Peon_prime.tscn",
		"res://cartas prime/AJEDREZ/Caballo_prime.tscn",
		"res://cartas prime/AJEDREZ/Dama_prime.tscn",
		"res://cartas prime/AJEDREZ/Torre_prime.tscn",
		"res://cartas prime/AJEDREZ/Arfil_prime.tscn",
		"res://cartas prime/TOONS/Tanque_cartoon_prime.tscn",
		"res://cartas prime/TOONS/Soldado_cartoon_prime.tscn",
		"res://cartas prime/TOONS/Campero_cartoon_prime.tscn",
		"res://cartas prime/TOONS/Ka-Bar_cartoon_prime.tscn",
		"res://cartas prime/TOONS/Granadero_cartoon_prime.tscn",
		// Efectos de habilidad
		"res://efectos/humo_transformacion.tscn",
		"res://efectos/fuego_maguin.tscn",
		"res://efectos/tentaculos_habilidad.tscn",
		"res://efectos/muro_golem.tscn",
		"res://efectos/misil_cartoon.tscn",
		"res://efectos/granada_cartoon.tscn",
		"res://efectos/explosion_centro_cartoon.tscn",
		"res://efectos/explosion_granada_cartoon.tscn",
		// Skins de huevo
		"res://escenas/personajes/reyhuevo1.tscn",
		"res://escenas/personajes/capitanhuevo1.tscn",
		"res://escenas/personajes/dinohuevo1.tscn",
		"res://escenas/personajes/majestadhuevo1.tscn",
		"res://escenas/personajes/paperdinohuevo1.tscn",
	};

	private const string RUTA_PEON = "res://cartas prime/AJEDREZ/Peon_prime.tscn";

	private TextureProgressBar _barra;
	private Label _lblPorcentaje;
	private Node2D _peon;
	private readonly List<string> _pendientes = new();
	private int _total;

	public override void _Ready()
	{
		_barra         = GetNodeOrNull<TextureProgressBar>("BarraContenedor/Margin/Barra");
		_lblPorcentaje = GetNodeOrNull<Label>("LblPorcentaje");

		CargarPeon();

		// En móvil, precargar las ~30 escenas a la vez consume ~2.6 GB de RAM y el sistema
		// mata la app (Low Memory Killer). Se omite la precarga: cada escena se carga bajo
		// demanda durante la partida (mucho menos memoria). En PC sí se precarga.
		bool esMovil = OS.HasFeature("mobile");

		if (!esMovil)
		{
			foreach (string ruta in RUTAS_A_PRECARGAR)
			{
				if (!ResourceLoader.Exists(ruta)) continue;
				if (ResourceLoader.LoadThreadedRequest(ruta) == Error.Ok)
					_pendientes.Add(ruta);
			}
		}
		_total = _pendientes.Count;

		if (_total == 0)
		{
			if (esMovil)
				MostrarCargaDecorativa(); // barra animada 1.5s, sin precarga real
			else
				CallDeferred(nameof(IrASiguiente));
		}
	}

	// Móvil: llena la barra en ~1.5s (solo estético) y pasa a login, sin precargar nada pesado.
	private void MostrarCargaDecorativa()
	{
		Tween tw = CreateTween();
		tw.TweenMethod(Callable.From<double>(ActualizarBarraDecorativa), 0.0, 100.0, 1.5);
		tw.Finished += IrASiguiente;
	}

	private void ActualizarBarraDecorativa(double v)
	{
		if (_barra != null) _barra.Value = v;
		if (_lblPorcentaje != null) _lblPorcentaje.Text = $"{Mathf.RoundToInt((float)v)}%";
		if (_peon != null && _barra != null)
		{
			Vector2 origen = _barra.GlobalPosition;
			_peon.GlobalPosition = new Vector2(origen.X + (float)(v / 100.0) * _barra.Size.X, origen.Y - 6f);
		}
	}

	// Instancia al Peón (su animación "idle" ya arranca sola vía TropaBase._Ready) como
	// mascota decorativa que brinca sobre la barra hasta la meta.
	private void CargarPeon()
	{
		if (!ResourceLoader.Exists(RUTA_PEON)) return;
		var escena = GD.Load<PackedScene>(RUTA_PEON);
		if (escena == null) return;

		_peon = escena.Instantiate<Node2D>();
		_peon.Scale = new Vector2(0.72f, 0.72f); // el doble del tamaño original (0.36 -> 0.72)
		_peon.ZIndex = 60;
		if (_peon is Area2D area)
		{
			area.InputPickable = false;
			area.Monitoring    = false;
			area.Monitorable   = false;
		}
		AddChild(_peon);

		var barraStats = _peon.GetNodeOrNull<Control>("StatsTropa");
		if (barraStats != null) barraStats.Visible = false;

		// El brinco lo hace el sprite hijo (posición relativa) — así nunca pelea con el
		// GlobalPosition absoluto que le asigno cada frame para seguir el avance de la barra.
		var anim = _peon.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (anim != null)
		{
			float baseY = anim.Position.Y;
			Tween tw = anim.CreateTween().SetLoops();
			tw.TweenProperty(anim, "position:y", baseY - 34f, 0.22f)
			  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
			tw.TweenProperty(anim, "position:y", baseY, 0.22f)
			  .SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.In);
		}
	}

	public override void _Process(double delta)
	{
		if (_total == 0) return;

		float progresoPendientes = 0f;
		var progresoActual = new Godot.Collections.Array();

		for (int i = _pendientes.Count - 1; i >= 0; i--)
		{
			string ruta = _pendientes[i];
			var estado = ResourceLoader.LoadThreadedGetStatus(ruta, progresoActual);

			if (estado == ResourceLoader.ThreadLoadStatus.Loaded || estado == ResourceLoader.ThreadLoadStatus.Failed)
			{
				_pendientes.RemoveAt(i); // pasa a contar como completado
			}
			else
			{
				progresoPendientes += progresoActual.Count > 0 ? (float)progresoActual[0] : 0f;
			}
		}

		int completados = _total - _pendientes.Count;
		float fraccion = Mathf.Clamp((completados + progresoPendientes) / _total, 0f, 1f);

		if (_barra != null) _barra.Value = fraccion * 100.0;
		if (_lblPorcentaje != null) _lblPorcentaje.Text = $"{Mathf.RoundToInt(fraccion * 100f)}%";

		// El Peón avanza en X exactamente al ritmo de la barra, parado justo encima de ella.
		if (_peon != null && _barra != null)
		{
			Vector2 origen = _barra.GlobalPosition;
			float x = origen.X + fraccion * _barra.Size.X;
			float y = origen.Y - 6f;
			_peon.GlobalPosition = new Vector2(x, y);
		}

		if (_pendientes.Count == 0) IrASiguiente();
	}

	private bool _yendo = false;
	private void IrASiguiente()
	{
		if (_yendo) return;
		_yendo = true;
		GetTree().ChangeSceneToFile(RUTA_SIGUIENTE);
	}
}
