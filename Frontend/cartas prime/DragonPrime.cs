using Godot;

/// <summary>
/// Dragón de Flama — daño de ataque en frame 3.
/// Habilidad: bola de fuego dirigida a un carril (320 daño directo) + quemadura DoT
/// que persiste en el módulo aunque el objetivo muera y se transfiere a la tropa que lo reemplace.
/// </summary>
public partial class DragonPrime : TropaBase
{
	public override string Tipo => Tipos.FUEGO;

	private const string RUTA_FUEGO_DRAGON = "res://efectos/fuego_dragon.tscn";
	private const string RUTA_FUEGO_EFECTO = "res://efectos/fuego_efecto.tscn";
	private const int    DAÑO_BOLA_FUEGO   = 320;
	private const int    DAÑO_QUEMADURA    = 12;
	private const int    TICKS_QUEMADURA   = 5;   // 5 × 2s = 10s

	private Node2D _objetivo;
	private bool   _esAtaqueHabilidad = false;

	private Marker2D    _spotFuego;
	private PackedScene _escenaFuegoDragon;
	private PackedScene _escenaFuegoEfecto;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 350; escudoActual = escudoMaximo = 250; puntosAtaque = 280; }
		base._Ready();

		_spotFuego = GetNodeOrNull<Marker2D>("SpotFuego");
		if (ResourceLoader.Exists(RUTA_FUEGO_DRAGON)) _escenaFuegoDragon = GD.Load<PackedScene>(RUTA_FUEGO_DRAGON);
		if (ResourceLoader.Exists(RUTA_FUEGO_EFECTO)) _escenaFuegoEfecto = GD.Load<PackedScene>(RUTA_FUEGO_EFECTO);

		_anim.FrameChanged += OnFrameChanged;
	}

	// ── CAPACIDADES ────────────────────────────────────────────────────────────
	public override bool AutogestionaDañoAtaque() => true;

	// ── ATAQUE NORMAL: daño en frame 3 ─────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (accion == "atacar")
		{
			_yaActuo           = true;
			_esAtaqueHabilidad = false;
			_objetivo          = BuscarObjetivoEnCarril();
			ReproducirAtaque();
			return;
		}
		base.EjecutarAccion(accion);
	}

	private void OnFrameChanged()
	{
		if ((string)_anim.Animation != "ataque") return;

		if (!_esAtaqueHabilidad && _anim.Frame == 3)
		{
			if (_objetivo != null && IsInstanceValid(_objetivo))
				_objetivo.Call("RecibirDaño", puntosAtaque);
		}

		if (_esAtaqueHabilidad && _anim.Frame == 4)
		{
			LanzarBolaDeFuego(_objetivo);
		}
	}

	// ── HABILIDAD: BOLA DE FUEGO DIRIGIDA ────────────────────────────────────
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada) return;

		Node2D objetivo = BuscarObjetivoEnCarril(); // respeta muro
		if (objetivo == null) return;

		habilidadUsada     = true;
		_yaActuo            = true;
		_esAtaqueHabilidad = true;
		_objetivo           = objetivo;

		ReproducirAtaque(); // reutiliza la animación "ataque"; frame 4 lanza la bola de fuego
	}

	private void LanzarBolaDeFuego(Node2D objetivo)
	{
		if (objetivo == null || !IsInstanceValid(objetivo) || _escenaFuegoDragon == null) return;

		Vector2 origen  = _spotFuego != null ? _spotFuego.GlobalPosition : GlobalPosition;
		Vector2 destino = objetivo.GlobalPosition;

		Node2D bola = (Node2D)_escenaFuegoDragon.Instantiate();
		GetTree().Root.AddChild(bola);
		bola.GlobalPosition = origen;
		bola.ZIndex = 100;
		bola.AddToGroup("efectos_dragon_root");

		var animBola = bola.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		animBola?.Play("movimiento_fuego");

		Tween tw = bola.CreateTween();
		tw.TweenProperty(bola, "global_position", destino, 0.35f)
		  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		tw.Finished += () => ImpactarBolaDeFuego(bola, animBola, objetivo, destino);
	}

	private void ImpactarBolaDeFuego(Node2D bola, AnimatedSprite2D animBola, Node2D objetivo, Vector2 posicionImpacto)
	{
		// Captura de datos del carril ANTES de aplicar daño, para que la quemadura
		// persista en el módulo aunque este golpe mate al objetivo.
		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		string carril = objetivo.HasMeta("carril")
			? ((string)objetivo.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim()
			: null;

		if (IsInstanceValid(objetivo)) objetivo.Call("RecibirDaño", DAÑO_BOLA_FUEGO);

		if (animBola != null)
		{
			// Mini-explosión: agranda ligeramente la bola durante el impacto.
			bola.Scale *= 1.3f;
			animBola.Play("impacto_fuego");
			animBola.AnimationFinished += () =>
			{
				if (IsInstanceValid(bola)) bola.QueueFree();
				// La quemadura continua solo arranca cuando termina la animación de impacto.
				if (carril != null) IniciarQuemadura(grupoEnemigo, carril, posicionImpacto);
			};
		}
		else
		{
			if (IsInstanceValid(bola)) bola.QueueFree();
			if (carril != null) IniciarQuemadura(grupoEnemigo, carril, posicionImpacto);
		}
	}

	// ── QUEMADURA (DoT que persiste en el carril/módulo, independiente de si el Dragón sigue vivo) ──
	private void IniciarQuemadura(string grupoEnemigo, string carrilNormalizado, Vector2 posicionSuelo)
	{
		if (_escenaFuegoEfecto == null) return;

		SceneTree tree = GetTree();
		Node2D efecto = (Node2D)_escenaFuegoEfecto.Instantiate();
		tree.Root.AddChild(efecto);
		efecto.GlobalPosition = posicionSuelo;
		efecto.ZIndex = 100;
		efecto.AddToGroup("efectos_dragon_root");
		efecto.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D")?.Play("animate_fuego");

		TickQuemadura(tree, grupoEnemigo, carrilNormalizado, efecto, TICKS_QUEMADURA);
	}

	// Estático + SceneTree explícito: la quemadura no depende de que "this" (el Dragón) siga vivo.
	private static void TickQuemadura(SceneTree tree, string grupoEnemigo, string carrilNormalizado, Node2D efecto, int ticksRestantes)
	{
		tree.CreateTimer(2.0).Timeout += () =>
		{
			Node2D ocupante = BuscarOcupanteCarril(tree, grupoEnemigo, carrilNormalizado);
			if (ocupante != null && IsInstanceValid(ocupante))
			{
				ocupante.Call("RecibirDaño", DAÑO_QUEMADURA);
				ocupante.Modulate = new Color(1f, 0.3f, 0.3f);
			}

			int restantes = ticksRestantes - 1;
			if (restantes <= 0)
			{
				if (ocupante != null && IsInstanceValid(ocupante)) ocupante.Modulate = Colors.White;
				if (IsInstanceValid(efecto)) efecto.QueueFree();
			}
			else
			{
				TickQuemadura(tree, grupoEnemigo, carrilNormalizado, efecto, restantes);
			}
		};
	}

	/// <summary>Busca quien ocupa actualmente un carril dado (sin importar quién lo ocupaba antes) — permite que la quemadura se transfiera a una tropa nueva.</summary>
	private static Node2D BuscarOcupanteCarril(SceneTree tree, string grupo, string carrilNormalizado)
	{
		foreach (Node n in tree.GetNodesInGroup(grupo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			string c = ((string)e.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			if (c == carrilNormalizado) return e;
		}
		return null;
	}
}
