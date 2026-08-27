using Godot;

/// <summary>
/// Dragón de Flama — daño de ataque en frame 3.
/// Habilidad: bola de fuego dirigida a un carril (320 daño directo) + quemadura DoT
/// que persiste en el módulo aunque el objetivo muera y se transfiere a la tropa que lo reemplace.
/// </summary>
public partial class DragonPrime : TropaBase
{
	public override string Tipo => Tipos.FUEGO;
	protected override int TurnoDesbloqueoHabilidad => 3;

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
			Node2D objetivo    = BuscarObjetivoEnCarril(); // fuerza el grupo opuesto al propio; respeta muro
			// Filtro de seguridad: bajo ninguna circunstancia el objetivo puede ser el propio Dragón.
			_objetivo = (objetivo == null || objetivo == this) ? null : objetivo;
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
			if (_objetivo != null && IsInstanceValid(_objetivo) && _objetivo != this)
			{
				_objetivo.Call("RecibirDaño", puntosAtaque);
				var campo = GetTree().Root.FindChild("Campo1", true, false);
				if (campo != null) campo.Call("RegistrarDañoTropa", this, puntosAtaque);
			}
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

		// Grupo contrario según el bando del Dragón (jugador ataca a tropas_rival, rival ataca a
		// tropas_jugador) — BuscarObjetivoEnCarril() ya aplica esto internamente; se recalcula
		// aquí solo para el diagnóstico de abajo, sin cambiar el resultado de la búsqueda real.
		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";

		Node2D objetivo = BuscarObjetivoEnCarril(); // fuerza el grupo opuesto al propio; respeta muro
		if (objetivo == null || objetivo == this || !EsObjetivoValido(objetivo))
		{
			string miCarril = HasMeta("carril") ? (string)GetMeta("carril") : "(sin carril)";
			GD.Print($"[DragonPrime] Habilidad sin objetivo: bando={(IsInGroup("tropas_jugador") ? "jugador" : "rival")}, " +
				$"carril propio={miCarril}, buscando en grupo='{grupoEnemigo}' con el mismo carril normalizado. " +
				$"Verifica que exista una tropa de ese grupo en el carril equivalente (mismo número de slot).");
			return;
		}

		habilidadUsada     = true;
		_yaActuo            = true;
		_esAtaqueHabilidad = true;
		_objetivo           = objetivo;

		DestelloHabilidad();
		ReproducirAtaque(); // reutiliza la animación "ataque"; frame 4 lanza la bola de fuego
	}

	/// <summary>Validación estricta: descarta el objetivo si es este mismo Dragón o pertenece a
	/// nuestro propio bando — nunca debe atacarse a sí mismo ni a un aliado. Acepta tanto una
	/// tropa enemiga como un muro enemigo (el muro sí puede bloquear el ataque del Dragón).</summary>
	private bool EsObjetivoValido(Node2D objetivo)
	{
		if (objetivo == null || !IsInstanceValid(objetivo) || objetivo == this) return false;

		bool esJugador = IsInGroup("tropas_jugador");
		string grupoEnemigoEsperado = esJugador ? "tropas_rival" : "tropas_jugador";
		string grupoMuroEsperado    = esJugador ? "muros_rival"  : "muros_jugador";
		return objetivo.IsInGroup(grupoEnemigoEsperado) || objetivo.IsInGroup(grupoMuroEsperado);
	}

	private void LanzarBolaDeFuego(Node2D objetivo)
	{
		if (!EsObjetivoValido(objetivo) || _escenaFuegoDragon == null) return;

		Vector2 origen  = ObtenerSpotOrientado(_spotFuego);
		Vector2 destino = objetivo.GlobalPosition;

		Node2D bola = (Node2D)_escenaFuegoDragon.Instantiate();
		GetTree().Root.AddChild(bola);
		bola.GlobalPosition = origen;
		bola.ZIndex = objetivo.ZIndex + 1;
		bola.AddToGroup("efectos_dragon_root");

		var animBola = bola.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		animBola?.Play("movimiento_fuego");
		// La llama se dibuja pensada para viajar hacia la derecha (lado jugador); si el Dragón es
		// rival, viaja hacia la izquierda, así que hay que voltear el sprite para que apunte en
		// su misma dirección de vuelo (igual que se hace con la textura del propio Dragón).
		if (animBola != null && !IsInGroup("tropas_jugador")) animBola.FlipH = true;

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
		int zIndexQuemadura = objetivo.ZIndex + 1;

		if (IsInstanceValid(objetivo))
		{
			objetivo.Call("RecibirDaño", DAÑO_BOLA_FUEGO);
			var campo = GetTree().Root.FindChild("Campo1", true, false);
			if (campo != null) campo.Call("RegistrarDañoTropa", this, DAÑO_BOLA_FUEGO);
		}

		if (animBola != null)
		{
			// Mini-explosión: agranda ligeramente la bola durante el impacto.
			bola.Scale *= 1.3f;
			animBola.Play("impacto_fuego");
			animBola.AnimationFinished += () =>
			{
				if (IsInstanceValid(bola)) bola.QueueFree();
				// La quemadura continua solo arranca cuando termina la animación de impacto.
				if (carril != null) IniciarQuemadura(this, grupoEnemigo, carril, posicionImpacto, zIndexQuemadura);
			};
		}
		else
		{
			if (IsInstanceValid(bola)) bola.QueueFree();
			if (carril != null) IniciarQuemadura(this, grupoEnemigo, carril, posicionImpacto, zIndexQuemadura);
		}
	}

	// ── QUEMADURA (DoT que persiste en el carril/módulo, independiente de si el Dragón sigue vivo) ──
	private void IniciarQuemadura(Node2D atacante, string grupoEnemigo, string carrilNormalizado, Vector2 posicionSuelo, int zIndex)
	{
		if (_escenaFuegoEfecto == null) return;

		SceneTree tree = GetTree();
		Node2D efecto = (Node2D)_escenaFuegoEfecto.Instantiate();
		tree.Root.AddChild(efecto);
		efecto.GlobalPosition = posicionSuelo;
		efecto.ZIndex = zIndex;
		efecto.AddToGroup("efectos_dragon_root");
		efecto.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D")?.Play("animate_fuego");

		TickQuemadura(tree, atacante, grupoEnemigo, carrilNormalizado, efecto, TICKS_QUEMADURA);
	}

	// Estático + SceneTree explícito: la quemadura no depende de que "this" (el Dragón) siga vivo.
	// "atacante" solo sirve para atribuir el daño de cada tick al Dragón que la originó (MVT);
	// si ya murió, RegistrarDañoTropa lo ignora sin interrumpir el DoT.
	private static void TickQuemadura(SceneTree tree, Node2D atacante, string grupoEnemigo, string carrilNormalizado, Node2D efecto, int ticksRestantes)
	{
		tree.CreateTimer(2.0).Timeout += () =>
		{
			Node2D ocupante = BuscarOcupanteCarril(tree, grupoEnemigo, carrilNormalizado);
			if (ocupante != null && IsInstanceValid(ocupante))
			{
				ocupante.Call("RecibirDaño", DAÑO_QUEMADURA);
				if (atacante != null && IsInstanceValid(atacante))
				{
					var campo = tree.Root.FindChild("Campo1", true, false);
					if (campo != null) campo.Call("RegistrarDañoTropa", atacante, DAÑO_QUEMADURA);
				}

				int vidaTrasGolpe = 0;
				try { vidaTrasGolpe = (int)ocupante.Get("vidaActual"); } catch { }
				if (vidaTrasGolpe <= 0)
				{
					// La quemadura mató a la tropa: el fuego no debe quedar flotando en su posición.
					if (IsInstanceValid(efecto)) efecto.QueueFree();
					return;
				}

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
				TickQuemadura(tree, atacante, grupoEnemigo, carrilNormalizado, efecto, restantes);
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
