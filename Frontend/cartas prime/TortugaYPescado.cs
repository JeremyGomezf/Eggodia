using Godot;

/// <summary>
/// TortugaYPescado — forma temporal en la que la habilidad del Maguín transmuta a la tropa
/// enemiga con más vida actual. La especie (Tortuga o Pez) la decide Maguín ANTES de instanciar
/// esta escena, según Escudo vs Vida del objetivo, y se asigna vía <see cref="especie"/> antes
/// de AddChild() para que _Ready() ya sepa qué stats/animaciones usar.
///
/// Dura 4 medios-turnos (Jugador -> Rival -> Jugador -> Rival, contados por
/// <see cref="TickTransformacion"/>, uno por cada CambiarTurno) y al terminar revierte a la
/// tropa original —guardada en <see cref="tropaOriginal"/>, oculta pero nunca destruida— con su
/// vida/escudo exactos de antes de la transformación.
/// </summary>
public partial class TortugaYPescado : TropaBase
{
	public override string Tipo => Tipos.AGUA;

	// ── Asignados por Maguín ANTES de AddChild(), _Ready() los usa para configurarse ───────
	public string especie = "pez"; // "tortuga" o "pez"
	public Node2D tropaOriginal;
	public int    vidaOriginalGuardada;
	public int    escudoOriginalGuardado;

	private int  _faseTortuga     = 1;    // solo aplica si especie == "tortuga"
	private int  _turnosRestantes = 4;
	private bool _revirtiendo     = false;
	private bool _enDefensa       = false;
	private Node2D _objetivoAtaque;

	// ══════════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		if (vidaMaxima == 0)
		{
			vidaActual = vidaMaxima = 100;
			if (especie == "tortuga") { escudoActual = escudoMaximo = 250; puntosAtaque = 50; }
			else                      { escudoActual = escudoMaximo = 0;   puntosAtaque = 70; }
		}
		base._Ready();

		if (especie != "tortuga")
		{
			var barraEsc = GetNodeOrNull<ProgressBar>("StatsTropa/BarraEscudo");
			if (barraEsc != null) barraEsc.Visible = false;
		}

		_anim.FrameChanged      += OnFrameChanged;
		_anim.AnimationFinished += OnAnimationFinished;
		_anim.Play(NombreAnim("idle"));
	}

	/// <summary>Orientación explícita, llamada por Maguín justo después de asignar el grupo
	/// (tropas_jugador/tropas_rival) al instanciar. Misma convención que el resto del roster
	/// (Campo1.AsegurarOrientacionRival): el arte base mira a la derecha sin voltear; se voltea
	/// únicamente cuando la transmutación queda del lado del rival, que debe mirar a la
	/// izquierda.</summary>
	public void AplicarOrientacion(bool esLadoJugador)
	{
		if (_anim != null) _anim.FlipH = !esLadoJugador;
	}

	// ── CAPACIDADES / UI ──────────────────────────────────────────────────────
	public override bool AutogestionaDañoAtaque() => true;
	public override bool TieneHabilidadEspecial() => false;
	public override bool MostrarBotonHabilidad()  => false;                 // ninguna de las dos formas tiene habilidad
	public override bool MostrarBotonDefensa()    => especie == "tortuga";  // el Pez no defiende
	public override bool TienePosturaDefensiva()  => especie == "tortuga";

	// ── NOMBRES DE ANIMACIÓN SEGÚN ESPECIE/FASE ──────────────────────────────
	private string NombreAnim(string baseAnim)
	{
		if (especie == "pez") return $"{baseAnim}-pez";
		return _faseTortuga == 2 ? $"{baseAnim}-tortuga_2" : $"{baseAnim}-tortuga";
	}

	// ── ACCIONES ──────────────────────────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (_estaMuerto) return;
		switch (accion)
		{
			case "atacar":
				_yaActuo        = true;
				_enDefensa      = false;
				_objetivoAtaque = BuscarObjetivoEnCarril();
				_anim.Play(NombreAnim("ataque"));
				break;

			case "preparar_defensa":
				_yaActuo = true;
				if (especie == "tortuga")
				{
					_enDefensa = true;
					_anim.Play("pre defensa-tortuga"); // única variante, sin "_2"
				}
				else
				{
					_anim.Play(NombreAnim("idle")); // el Pez no tiene postura defensiva
				}
				break;

			case "defender":
				if (especie == "tortuga") _anim.Play(NombreAnim("defensa"));
				break;

			case "recibir_daño":
				if (!_enDefensa) _anim.Play(NombreAnim("daño"));
				break;
		}
	}

	// ── RECIBIR DAÑO: escudo persistente (Tortuga) + transición de fase ─────────
	public override void RecibirDaño(int cantidad)
	{
		if (_estaMuerto) return;
		EfectoGolpe();

		if (_enDefensa && especie == "tortuga")
		{
			EjecutarAccion("defender");
			cantidad = (int)(cantidad * 0.5f);
		}

		if (especie == "tortuga" && escudoActual > 0)
		{
			int escudoAntes = escudoActual;
			// Anti-overkill: el exceso nunca traspasa a la vida mientras haya escudo.
			if (cantidad <= escudoActual) { escudoActual -= cantidad; cantidad = 0; }
			else                          { cantidad = 0; escudoActual = 0; }
			ActualizarBarrasUI();

			if (escudoAntes > 0 && escudoActual == 0 && _faseTortuga == 1)
			{
				// Escudo roto por primera vez -> Fase 2: transición y luego idle en bucle.
				_faseTortuga = 2;
				_enDefensa   = false;
				_anim.Play("defensa-tortuga_2");
				return;
			}
		}

		if (cantidad > 0) vidaActual -= cantidad;
		ActualizarBarrasUI();

		if (vidaActual <= 0)
		{
			var campo = GetTree().Root.FindChild("Campo1", true, false);
			if (campo != null) campo.Call("EjecutarMuerteTropaSacrificada", this);
		}
		else if (!_enDefensa)
		{
			EjecutarAccion("recibir_daño");
		}
	}

	// ── DERROTA: si muere transformada, la tropa original NUNCA se restaura ────
	public override void ReproducirDerrota()
	{
		_estaMuerto = true;
		_revirtiendo = true; // cancela cualquier reversión pendiente
		if (IsInstanceValid(tropaOriginal)) tropaOriginal.QueueFree();
		_anim.Play(especie == "tortuga" ? $"derrota-tortuga{(_faseTortuga == 2 ? "_2" : "")}" : "derrota-pez");
	}

	// ── SEÑALES DE ANIMACIÓN ─────────────────────────────────────────────────
	private void OnFrameChanged()
	{
		string anim = (string)_anim.Animation;

		// Golpe: frame de impacto según especie (pez: frame 5 de 7; tortuga: último frame, 3).
		if (anim == NombreAnim("ataque"))
		{
			int frameImpacto = especie == "pez" ? 5 : 3;
			if (_anim.Frame == frameImpacto && _objetivoAtaque != null && IsInstanceValid(_objetivoAtaque))
			{
				_objetivoAtaque.Call("RecibirDaño", puntosAtaque);
				var campo = GetTree().Root.FindChild("Campo1", true, false);
				if (campo != null) campo.Call("RegistrarDañoTropa", this, puntosAtaque);
			}
		}
	}

	private void OnAnimationFinished()
	{
		string anim = (string)_anim.Animation;

		if (anim == NombreAnim("ataque") || anim == NombreAnim("daño"))
		{
			if (!_estaMuerto) _anim.Play(NombreAnim("idle"));
		}
		else if (anim == "defensa-tortuga_2")
		{
			// Fin de la transición de fase: entra en bucle a partir de aquí.
			if (!_estaMuerto) _anim.Play("idle-tortuga_2");
		}
	}

	// ── DURACIÓN Y REVERSIÓN ──────────────────────────────────────────────────
	// Llamado una vez por cada CambiarTurno() (ambos bandos) desde Campo1.ProcesarStatusEfectos.
	public void TickTransformacion()
	{
		if (_estaMuerto || _revirtiendo) return;
		_turnosRestantes--;
		if (_turnosRestantes <= 0) IniciarReversion();
	}

	private void IniciarReversion()
	{
		if (_revirtiendo || !IsInstanceValid(this)) return;
		_revirtiendo = true;

		var campo = GetTree().Root.FindChild("Campo1", true, false);
		campo?.Call("IniciarBloqueoTablero");

		var escenaHumo = ResourceLoader.Exists("res://efectos/humo_transformacion.tscn")
			? GD.Load<PackedScene>("res://efectos/humo_transformacion.tscn") : null;
		if (escenaHumo == null)
		{
			RestaurarTropaOriginal();
			campo?.Call("FinalizarBloqueoTablero");
			return;
		}

		Node2D humo = (Node2D)escenaHumo.Instantiate();
		GetTree().Root.AddChild(humo);
		humo.GlobalPosition = ObtenerSlotEfectoSecundario();
		// La capa más alta sobre la propia criatura, por detrás de un muro si lo hubiera.
		humo.ZIndex = ZIndex + NivelZIndex.Humo;

		var animHumo = humo.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (animHumo != null) animHumo.FlipH = !IsInGroup("tropas_jugador");
		if (animHumo == null)
		{
			RestaurarTropaOriginal();
			humo.QueueFree();
			campo?.Call("FinalizarBloqueoTablero");
			return;
		}

		bool restaurado = false;
		// Igual que en la transmutación de ida: la escena guarda frame=9/frame_progress=1.0
		// (último frame) como vista previa del editor — hay que forzar el reinicio a 0.
		animHumo.Play("humo_efecto");
		animHumo.Frame = 0;
		animHumo.FrameProgress = 0f;
		animHumo.FrameChanged += () =>
		{
			if (!restaurado && animHumo.Frame == 3)
			{
				restaurado = true;
				RestaurarTropaOriginal(); // esto ya libera "this" (el animal); el humo se libera aparte, abajo
			}
		};

		// Fade-out suave (no corte abrupto): al llegar al antepenúltimo frame (~80% de
		// progreso) se desvanece el alfa y recién ahí se libera — mismo criterio que las
		// explosiones cartoon del juego.
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

	private void RestaurarTropaOriginal()
	{
		if (IsInstanceValid(tropaOriginal))
		{
			bool esJugador = IsInGroup("tropas_jugador");
			string carril  = HasMeta("carril") ? (string)GetMeta("carril") : null;

			tropaOriginal.Set("vidaActual", vidaOriginalGuardada);
			tropaOriginal.Set("escudoActual", escudoOriginalGuardado);
			if (tropaOriginal is TropaBase tropaOriginalTB) tropaOriginalTB.ColocarPorCentroColision(PosicionCentroColision);
			else                                             tropaOriginal.GlobalPosition = GlobalPosition;
			tropaOriginal.ZIndex         = ZIndex;
			tropaOriginal.Visible        = true;
			tropaOriginal.SetProcess(true);
			tropaOriginal.SetPhysicsProcess(true);
			if (tropaOriginal is Area2D areaOriginal)
			{
				areaOriginal.InputPickable = true;
				areaOriginal.Monitoring    = true;
				areaOriginal.Monitorable   = true;
			}
			var colision = tropaOriginal.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
			if (colision != null) colision.SetDeferred("disabled", false);

			string grupoDestino = esJugador ? "tropas_jugador" : "tropas_rival";
			if (!tropaOriginal.IsInGroup(grupoDestino)) tropaOriginal.AddToGroup(grupoDestino);
			if (tropaOriginal.HasMethod("SetActivo")) tropaOriginal.Call("SetActivo", false);

			if (carril != null)
			{
				Node2D zona = GetTree().Root.FindChild(carril, true, false) as Node2D;
				Node ocupado = zona?.GetNodeOrNull("Ocupado");
				if (ocupado != null) ocupado.SetMeta("tropa_instanciada", tropaOriginal);
			}
		}

		QueueFree();
	}
}
