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

	private int  _faseTortuga     = 1;    // 1: normal, 2: caparazón roto
	private int  _turnosRestantes = 4;
	private bool _revirtiendo     = false;
	private bool _enDefensa       = false;
	private Node2D _objetivoAtaque;

	// ══════════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		if (vidaMaxima == 0)
		{
			if (especie == "tortuga") { vidaActual = vidaMaxima = 70;  escudoActual = escudoMaximo = 250; puntosAtaque = 40; }
			else                      { vidaActual = vidaMaxima = 100; escudoActual = escudoMaximo = 0;   puntosAtaque = 70; }
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
	/// (tropas_jugador/tropas_rival) al instanciar.</summary>
	public void AplicarOrientacion(bool esLadoJugador)
	{
		if (_anim != null) _anim.FlipH = !esLadoJugador;
	}

	// ── CAPACIDADES / UI ──────────────────────────────────────────────────────
	public override bool AutogestionaDañoAtaque() => true;
	public override bool TieneHabilidadEspecial() => false;
	public override bool MostrarBotonHabilidad()  => false;

	// Si está en Fase 2 (caparazón roto), NO puede defenderse ni la IA ni el Jugador.
	public override bool MostrarBotonDefensa()    => especie == "tortuga" && _faseTortuga == 1;
	public override bool TienePosturaDefensiva()  => especie == "tortuga" && _faseTortuga == 1;

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
				// La IA o el Jugador solo entran en defensa si la tortuga conserve su escudo (Fase 1)
				if (especie == "tortuga" && _faseTortuga == 1)
				{
					_enDefensa = true;
					_anim.Play("pre defensa-tortuga");
				}
				else
				{
					_enDefensa = false;
					_anim.Play(NombreAnim("idle"));
				}
				break;

			case "defender":
				if (especie == "tortuga" && _faseTortuga == 1 && _enDefensa) 
				{
					_anim.Play("defensa-tortuga");
				}
				break;

			case "recibir_daño":
				_anim.Play(NombreAnim("daño"));
				break;
		}
	}

	// ── RECIBIR DAÑO: el escudo SOLO se activa/gasta mientras está defendiendo ──
	// Si no está defendiendo, el golpe va directo a la vida y el escudo no se toca para nada
	// (ni se reduce, ni puede romperse) — así lo confirmó el diseño: "el escudo se activa
	// cuando la tortuga se defiende".
	public override void RecibirDaño(int cantidad)
	{
		if (_estaMuerto) return;
		EfectoGolpe();

		bool estabaDefendiendo = _enDefensa && especie == "tortuga" && _faseTortuga == 1;

		if (!estabaDefendiendo)
		{
			// No estaba defendiendo: el escudo ni se toca, todo el daño es directo a la vida.
			vidaActual -= cantidad;
			ActualizarBarrasUI();

			if (vidaActual <= 0)
			{
				var campoN = GetTree().Root.FindChild("Campo1", true, false);
				if (campoN != null) campoN.Call("EjecutarMuerteTropaSacrificada", this);
			}
			else
			{
				EjecutarAccion("recibir_daño"); // "daño-tortuga" o "daño-tortuga_2"
			}
			return;
		}

		// Defendiendo: 50% de reducción y el escudo absorbe primero.
		cantidad = (int)(cantidad * 0.5f);
		bool seRompioEscudoAhora = false;

		if (escudoActual > 0)
		{
			int escudoAntes = escudoActual;
			if (cantidad <= escudoActual) { escudoActual -= cantidad; cantidad = 0; }
			else                          { cantidad -= escudoActual; escudoActual = 0; }
			ActualizarBarrasUI();

			if (escudoAntes > 0 && escudoActual == 0)
			{
				_faseTortuga = 2;
				_enDefensa   = false;
				seRompioEscudoAhora = true;
			}
		}

		if (cantidad > 0) vidaActual -= cantidad;
		ActualizarBarrasUI();

		if (vidaActual <= 0)
		{
			var campo = GetTree().Root.FindChild("Campo1", true, false);
			if (campo != null) campo.Call("EjecutarMuerteTropaSacrificada", this);
		}
		else if (seRompioEscudoAhora)
		{
			_anim.Play("defensa-tortuga_2"); // Animación especial de rotura de caparazón
		}
		else
		{
			EjecutarAccion("defender"); // "defensa-tortuga": bloqueó y conserva algo de escudo
		}
	}

	// ── DERROTA: si muere transformada, la tropa original NUNCA se restaura ────
	public override void ReproducirDerrota()
	{
		_estaMuerto = true;
		_revirtiendo = true;
		if (IsInstanceValid(tropaOriginal)) tropaOriginal.QueueFree();
		_anim.Play(especie == "tortuga" ? $"derrota-tortuga{(_faseTortuga == 2 ? "_2" : "")}" : "derrota-pez");
	}

	// ── SEÑALES DE ANIMACIÓN ─────────────────────────────────────────────────
	private void OnFrameChanged()
	{
		string anim = (string)_anim.Animation;

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
			if (!_estaMuerto) _anim.Play("idle-tortuga_2");
		}
	}

	// ── DURACIÓN Y REVERSIÓN ──────────────────────────────────────────────────
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
		humo.AddToGroup("efectos_maguin"); // para LimpiezaEfectos.cs al reiniciar/salir
		humo.GlobalPosition = ObtenerSlotEfectoSecundario();
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
		animHumo.Play("humo_efecto");
		animHumo.Frame = 0;
		animHumo.FrameProgress = 0f;
		animHumo.FrameChanged += () =>
		{
			if (!restaurado && animHumo.Frame == 3)
			{
				restaurado = true;
				RestaurarTropaOriginal();
			}
		};

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
