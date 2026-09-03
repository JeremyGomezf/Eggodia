using Godot;

/// <summary>Maguín — daño de ataque en frame 2. Habilidad (turno 4): Transmutación — convierte a
/// la tropa rival con más vida actual en Tortuga o Pez (res://cartas prime/TortugaYPescado.tscn)
/// durante 2 rondas completas, en vez de infligir daño directo.</summary>
public partial class MaguinPrime : TropaBase
{
	public override string Tipo => Tipos.AGUA;
	protected override int TurnoDesbloqueoHabilidad => 4;

	private const string RUTA_HUMO   = "res://efectos/humo_transformacion.tscn";
	private const string RUTA_ANIMAL = "res://cartas prime/TortugaYPescado.tscn";
	private const string RUTA_FUEGO  = "res://efectos/fuego_maguin.tscn";

	private Node2D _objetivo;
	private bool   _esHabilidadPendiente = false;
	private Node2D _objetivoTransmutacion;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 200; escudoActual = escudoMaximo = 220; puntosAtaque = 250; }
		base._Ready();
		_anim.FrameChanged += OnFrameChanged;
	}

	// ── CAPACIDADES ────────────────────────────────────────────────────────────
	public override bool AutogestionaDañoAtaque() => true;

	// ── ATAQUE: directo, ignora muros — el fuego se instancia en frame 3 ────────
	public override void EjecutarAccion(string accion)
	{
		if (accion == "atacar")
		{
			_yaActuo              = true;
			_esHabilidadPendiente = false;
			_objetivo             = BuscarObjetivoEnCarril(ignorarMuro: true);
			ReproducirAtaque();
			return;
		}
		base.EjecutarAccion(accion);
	}

	private void OnFrameChanged()
	{
		if ((string)_anim.Animation != "ataque") return;

		// Frame 2: si había una habilidad de transmutación pendiente, dispara el humo aquí.
		if (_esHabilidadPendiente && _anim.Frame == 2)
		{
			_esHabilidadPendiente = false;
			EjecutarTransmutacion();
			return;
		}

		// Frame 3: ataque normal -> instancia el fuego sobre el objetivo (el daño lo aplica
		// el propio fuego en SU frame 1, no aquí).
		if (!_esHabilidadPendiente && _anim.Frame == 3 && _objetivo != null && IsInstanceValid(_objetivo))
		{
			InstanciarFuego(_objetivo);
		}
	}

	// ── FUEGO DE ATAQUE ──────────────────────────────────────────────────────
	private void InstanciarFuego(Node2D objetivo)
	{
		if (!ResourceLoader.Exists(RUTA_FUEGO) || !IsInstanceValid(objetivo)) return;

		Node2D fuego = (Node2D)GD.Load<PackedScene>(RUTA_FUEGO).Instantiate();
		GetTree().Root.AddChild(fuego);

		Vector2 anclaFuego = objetivo is TropaBase objTB ? objTB.ObtenerSlotEfectoSecundario() : objetivo.GlobalPosition;
		fuego.GlobalPosition = anclaFuego;
		// Por delante de la tropa Y de los tentáculos si coinciden sobre la misma unidad,
		// pero por detrás del muro (que usa tier + NivelZIndex.Muro, el escalón más alto).
		fuego.ZIndex = objetivo.ZIndex + NivelZIndex.Fuego;

		if (fuego is FuegoMaguinPrime fp)
		{
			fp.objetivo   = objetivo;
			fp.atacante   = this;
			fp.dañoAtaque = puntosAtaque;
		}
	}

	// ── HABILIDAD: TRANSMUTACIÓN ────────────────────────────────────────────────
	// Reutiliza la animación de ataque estándar; en vez de aplicar daño en el frame 2,
	// dispara la transmutación sobre el enemigo con más vida actual del tablero.
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada) return;

		Node2D objetivo = BuscarEnemigoConMasVida();
		if (objetivo == null) return;

		habilidadUsada          = true;
		_yaActuo                = true;
		_esHabilidadPendiente   = true;
		_objetivoTransmutacion  = objetivo;
		ReproducirAtaque();
	}

	private Node2D BuscarEnemigoConMasVida()
	{
		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		Node2D mejor = null; int maxVida = -1;
		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			// Una tropa ya transmutada (Tortuga/Pez) no puede volver a transmutarse encima.
			if (!(n is Node2D e) || !IsInstanceValid(e) || e is TortugaYPescado) continue;
			int v = 0; try { v = (int)e.Get("vidaActual"); } catch { continue; }
			if (v > maxVida) { maxVida = v; mejor = e; }
		}
		return mejor;
	}

	private void EjecutarTransmutacion()
	{
		if (_objetivoTransmutacion == null || !IsInstanceValid(_objetivoTransmutacion)) return;
		if (!ResourceLoader.Exists(RUTA_HUMO)) return;

		var campo = GetTree().Root.FindChild("Campo1", true, false);
		campo?.Call("IniciarBloqueoTablero");

		Node2D objetivoRef = _objetivoTransmutacion;
		Vector2 posObjetivo = objetivoRef is TropaBase objTB ? objTB.ObtenerSlotEfectoSecundario() : objetivoRef.GlobalPosition;
		int zIndexObjetivo  = objetivoRef.ZIndex;

		Node2D humo = (Node2D)GD.Load<PackedScene>(RUTA_HUMO).Instantiate();
		GetTree().Root.AddChild(humo);
		humo.AddToGroup("efectos_maguin"); // para LimpiezaEfectos.cs al reiniciar/salir
		humo.GlobalPosition = posObjetivo;
		// La capa más alta sobre la propia tropa (por delante de tentáculos y fuego si
		// coinciden), pero todavía por detrás de un muro si lo hubiera en ese carril.
		humo.ZIndex = zIndexObjetivo + NivelZIndex.Humo;

		var animHumo = humo.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (animHumo != null) animHumo.FlipH = !objetivoRef.IsInGroup("tropas_jugador");
		if (animHumo == null)
		{
			TransformarEnAnimal(objetivoRef);
			humo.QueueFree();
			campo?.Call("FinalizarBloqueoTablero");
			return;
		}

		bool transformado = false;
		// La escena guarda frame=9/frame_progress=1.0 (último frame) como vista previa del
		// editor. Sin este reinicio explícito, Play() reanuda ahí en vez de en 0 y la animación
		// nunca vuelve a pasar por el frame 3 — la transmutación jamás se dispararía.
		animHumo.Play("humo_efecto");
		animHumo.Frame = 0;
		animHumo.FrameProgress = 0f;
		animHumo.FrameChanged += () =>
		{
			if (!transformado && animHumo.Frame == 3)
			{
				transformado = true;
				TransformarEnAnimal(objetivoRef);
			}
		};

		// Fade-out suave (no corte abrupto): al llegar al antepenúltimo frame (~80% de
		// progreso) se desvanece el alfa y recién ahí se libera — mismo criterio que las
		// explosiones cartoon del juego (TropaBase.DesvanecerAlAntepenultimoFrame).
		bool desvanecido = false;
		animHumo.FrameChanged += () =>
		{
			if (desvanecido || !IsInstanceValid(humo)) return;
			int total = animHumo.SpriteFrames != null ? animHumo.SpriteFrames.GetFrameCount(animHumo.Animation) : 0;
			if (total < 3 || animHumo.Frame < total - 3) return;
			desvanecido = true;
			Tween twFade = humo.CreateTween();
			twFade.TweenProperty(humo, "modulate:a", 0.0f, 0.18f);
			twFade.Finished += () =>
			{
				if (IsInstanceValid(humo)) humo.QueueFree();
				campo?.Call("FinalizarBloqueoTablero");
			};
		};
		// Red de seguridad: si por algo el fade nunca se dispara, igual se libera al terminar.
		animHumo.AnimationFinished += () =>
		{
			if (desvanecido) return;
			desvanecido = true;
			if (IsInstanceValid(humo)) humo.QueueFree();
			campo?.Call("FinalizarBloqueoTablero");
		};
	}

	// Decide Tortuga (EscudoActual > VidaActual) o Pez (caso contrario, o sin escudo), oculta a
	// la tropa original sin destruirla y coloca al animal exactamente en su lugar (carril,
	// grupo, posición, Z-Index) — funciona igual para cualquiera de las 17 cartas del roster,
	// sin necesitar tocar sus scripts individuales.
	private void TransformarEnAnimal(Node2D objetivo)
	{
		if (!IsInstanceValid(objetivo) || !ResourceLoader.Exists(RUTA_ANIMAL)) return;

		int vidaObjetivo = 0, escudoObjetivo = 0, vidaMaxObjetivo = 0, escudoMaxObjetivo = 0;
		try { vidaObjetivo      = (int)objetivo.Get("vidaActual");    } catch { }
		try { escudoObjetivo    = (int)objetivo.Get("escudoActual");  } catch { }
		try { vidaMaxObjetivo   = (int)objetivo.Get("vidaMaxima");    } catch { }
		try { escudoMaxObjetivo = (int)objetivo.Get("escudoMaximo");  } catch { }

		// Tortuga si está 100% intacta (vida Y escudo al máximo) o si tiene más escudo que vida.
		// Pez en cualquier otro caso (más vida que escudo, o directamente sin escudo).
		bool intacta100 = vidaMaxObjetivo > 0 && vidaObjetivo == vidaMaxObjetivo
						&& escudoMaxObjetivo > 0 && escudoObjetivo == escudoMaxObjetivo;
		string especie = (intacta100 || escudoObjetivo > vidaObjetivo) ? "tortuga" : "pez";

		bool esJugadorObjetivo = objetivo.IsInGroup("tropas_jugador");
		string carrilObjetivo  = objetivo.HasMeta("carril") ? (string)objetivo.GetMeta("carril") : null;
		int zIndexOriginal     = objetivo.ZIndex;

		Node2D animalNode = (Node2D)GD.Load<PackedScene>(RUTA_ANIMAL).Instantiate();
		if (animalNode is TortugaYPescado animal)
		{
			animal.especie                = especie;
			animal.tropaOriginal          = objetivo;
			animal.vidaOriginalGuardada   = vidaObjetivo;
			animal.escudoOriginalGuardado = escudoObjetivo;
		}

		Node padre = objetivo.GetParent() ?? GetTree().Root;
		padre.AddChild(animalNode);
		if (animalNode is TropaBase animalTB && objetivo is TropaBase objTBPos)
			animalTB.ColocarPorCentroColision(objTBPos.PosicionCentroColision);
		else
			animalNode.GlobalPosition = objetivo.GlobalPosition;
		animalNode.ZIndex = zIndexOriginal;
		animalNode.AddToGroup(esJugadorObjetivo ? "tropas_jugador" : "tropas_rival");
		if (carrilObjetivo != null) animalNode.SetMeta("carril", carrilObjetivo);

		if (animalNode is TortugaYPescado animalOrientable)
			animalOrientable.AplicarOrientacion(esJugadorObjetivo);

		// Re-apunta el marcador "Ocupado" de la zona al animal (mismo patrón que Enroque/Promoción).
		if (carrilObjetivo != null)
		{
			Node2D zona = GetTree().Root.FindChild(carrilObjetivo, true, false) as Node2D;
			Node ocupado = zona?.GetNodeOrNull("Ocupado");
			if (ocupado != null) ocupado.SetMeta("tropa_instanciada", animalNode);
		}

		// Oculta y neutraliza a la tropa original SIN destruirla — se restaura al terminar la
		// transformación (TortugaYPescado.RestaurarTropaOriginal).
		objetivo.RemoveFromGroup(esJugadorObjetivo ? "tropas_jugador" : "tropas_rival");
		objetivo.Visible = false;
		objetivo.SetProcess(false);
		objetivo.SetPhysicsProcess(false);
		if (objetivo is Area2D areaObjetivo)
		{
			areaObjetivo.InputPickable = false;
			areaObjetivo.Monitoring    = false;
			areaObjetivo.Monitorable   = false;
		}
		var colision = objetivo.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (colision != null) colision.SetDeferred("disabled", true);
	}
}
