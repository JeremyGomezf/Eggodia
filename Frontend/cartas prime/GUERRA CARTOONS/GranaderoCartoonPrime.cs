using Godot;

/// <summary>
/// GranaderoCartoonPrime — Soldado atrincherado. 50 HP / 600 ESC / 230 ATQ.
/// Anti-overkill: el exceso de daño nunca traspasa el escudo a la vida.
/// 3 facetas visuales (600-301, 300-1, 0). Sin postura defensiva.
/// Ataque: granada parabólica (frame 2). Habilidad: mortero vertical (350 dmg, frame 12).
/// Último aliento: en frame 3 de la derrota lanza una granada final antes de morir.
/// Botón de defensa completamente remóvido de la UI.
/// </summary>
public partial class GranaderoCartoonPrime : TropaBase
{
	public override string Tipo => Tipos.METAL;

	// ── CONSTANTES ────────────────────────────────────────────────────────────
	private const int    ATQ_MORTERO   = 350;
	private const string RUTA_GRANADA  = "res://efectos/granada_cartoon.tscn";
	private const string RUTA_MISIL    = "res://efectos/misil_cartoon.tscn";
	private const string RUTA_EXP_GRAN = "res://efectos/explosion_granada_cartoon.tscn";
	private const string RUTA_EXP_CENT = "res://efectos/explosion_centro_cartoon.tscn";

	// ── ESTADO ────────────────────────────────────────────────────────────────
	private int    _faceta          = 1;   // 1=600-301 | 2=300-1 | 3=escudo=0
	private bool   _mortalDisparado = false;
	private Node2D _objetivo;

	// ── RECURSOS / SPOTS ──────────────────────────────────────────────────────
	private Marker2D    _spotGranada;
	private Marker2D    _spotTroll;
	private Marker2D    _spotMortero;
	private PackedScene _escenaGranada;
	private PackedScene _escenaMisil;
	private PackedScene _escenaExpGran;
	private PackedScene _escenaExpCent;

	// ══════════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		if (vidaMaxima == 0)
		{
			vidaActual   = vidaMaxima   = 50;
			escudoActual = escudoMaximo = 600;
			puntosAtaque = 230;
		}
		base._Ready();

		// Ocultar por completo el botón de defensa de la UI (sin dejar silueta)
		var btnDefensa = GetNodeOrNull<Control>("UI/BtnDefensa") 
					  ?? GetNodeOrNull<Control>("BtnDefensa");
		if (btnDefensa != null) btnDefensa.Visible = false;

		// Asignación de cada uno de los Marker2D exactos de tu escena
		_spotGranada = GetNodeOrNull<Marker2D>("SpotGranada");
		_spotTroll   = GetNodeOrNull<Marker2D>("SpotTroll");
		_spotMortero = GetNodeOrNull<Marker2D>("SpotMortero");

		if (ResourceLoader.Exists(RUTA_GRANADA))  _escenaGranada = GD.Load<PackedScene>(RUTA_GRANADA);
		if (ResourceLoader.Exists(RUTA_MISIL))    _escenaMisil   = GD.Load<PackedScene>(RUTA_MISIL);
		if (ResourceLoader.Exists(RUTA_EXP_GRAN)) _escenaExpGran = GD.Load<PackedScene>(RUTA_EXP_GRAN);
		if (ResourceLoader.Exists(RUTA_EXP_CENT)) _escenaExpCent = GD.Load<PackedScene>(RUTA_EXP_CENT);

		// Conexiones de señales seguras para C# en Godot 4
		if (_anim != null)
		{
			_anim.Connect(AnimatedSprite2D.SignalName.FrameChanged, Callable.From(OnFrameChanged));
			_anim.Connect(AnimatedSprite2D.SignalName.AnimationFinished, Callable.From(OnAnimationFinished));
		}

		_faceta = 1;
		_anim?.Play("idle 1");
	}

	// ── CAPACIDADES ───────────────────────────────────────────────────────────
	public override bool AutogestionaDañoAtaque() => true;
	public override bool TieneHabilidadEspecial() => true;
	public override bool TienePosturaDefensiva()  => false;

	private string AF(string b) => $"{b} {_faceta}";

	// ── ACCIONES ──────────────────────────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (_estaMuerto) return;
		switch (accion)
		{
			case "atacar":
				_yaActuo = true;
				_objetivo = BuscarObjetivoEnCarril(true);
				_anim.Play(AF("ataque"));
				break;

			case "preparar_defensa":
			case "defender":
				// Atrincherado: sin postura de defensa, consume el turno en idle
				_yaActuo = true;
				_anim.Play(AF("idle"));
				break;

			case "usar_habilidad":
				UsarHabilidadPropia();
				break;
		}
	}

	// ── RECIBIR DAÑO: ANTI-OVERKILL ───────────────────────────────────────────
	// Mientras haya escudo, el exceso de daño NUNCA traspasa a la vida (50 HP).
	public override void RecibirDaño(int cantidad)
	{
		if (_estaMuerto) return;
		EfectoGolpe();

		int escudoAntes = escudoActual;

		if (escudoActual > 0)
		{
			// Absorción total: el exceso desaparece — anti-overkill estricto
			escudoActual = Mathf.Max(0, escudoActual - cantidad);

			// Actualizar barra de escudo
			var barraEsc = GetNodeOrNull<ProgressBar>("StatsTropa/BarraEscudo");
			if (barraEsc != null) { barraEsc.MaxValue = escudoMaximo; barraEsc.Value = escudoActual; }

			// ── TRANSICIONES DE FACETA ────────────────────────────────────────
			if (escudoAntes > 300 && escudoActual is > 0 and <= 300)
			{
				// Transición 1→2: única vez que se reproduce daño 2
				_faceta = 2;
				_anim.Play("daño 2");
			}
			else if (escudoAntes > 0 && escudoActual == 0)
			{
				// Escudo destruido completamente
				_faceta = 3;
				bool impactoMasivo = escudoAntes == escudoMaximo;
				_anim.Play(impactoMasivo ? "daño 4" : "daño 3");
			}
			else if (_faceta == 1)
			{
				// Daño normal dentro de Faceta 1 (sin cruzar umbral)
				_anim.Play("daño 1");
			}
			// En Faceta 2: golpes sin romper escudo no reproducen animación de daño,
			// solo baja la barra visualmente (ya actualizada arriba)
			return;  // HP nunca recibe daño mientras hubo escudo
		}

		// ── DAÑO A LA VIDA (escudo ya en 0 desde el turno anterior) ──────────
		vidaActual -= cantidad;
		var barraVid = GetNodeOrNull<ProgressBar>("StatsTropa/BarraVida");
		if (barraVid != null) { barraVid.MaxValue = vidaMaxima; barraVid.Value = Mathf.Max(0, vidaActual); }
		// En Faceta 3 los golpes a la vida no reproducen animación de daño

		if (vidaActual <= 0)
		{
			vidaActual  = 0;
			_estaMuerto = true;
			GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.SetDeferred("disabled", true);
			ReproducirDerrota();
		}
	}

	// ── DERROTA (llamada al morir o por EjecutarMuerteTropaSacrificada) ───────
	public new void ReproducirDerrota()
	{
		_estaMuerto = true;
		_anim.Play(AF("derrota"));
	}

	// ── HABILIDAD: MORTERO ────────────────────────────────────────────────────
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada) return;
		Node2D obj = BuscarObjetivoEnCarril(true);
		if (obj == null) return;

		habilidadUsada = true;
		_yaActuo       = true;
		_objetivo      = obj;
		_anim.Play(AF("habilidad"));
		// El misil se instancia en OnFrameChanged al frame 12
	}

	// ── LANZAR GRANADA PARABÓLICA ─────────────────────────────────────────────
	private void LanzarGranada(Node2D objetivo, int danio, Marker2D spotOrigen)
	{
		if (objetivo == null || !IsInstanceValid(objetivo) || _escenaGranada == null) return;

		// Utiliza el Marker2D que corresponda (SpotGranada o SpotTroll)
		Vector2 origen  = spotOrigen != null ? spotOrigen.GlobalPosition
											 : GlobalPosition + new Vector2(30f, -40f);
		Vector2 destino = objetivo.GlobalPosition + new Vector2(0f, 35f);  // base del sprite
		Vector2 mid     = (origen + destino) * 0.5f + new Vector2(0f, -110f);

		Node2D granada = (Node2D)_escenaGranada.Instantiate();
		GetTree().Root.AddChild(granada);
		granada.GlobalPosition = origen;
		granada.ZIndex = 50;

		float dist     = origen.DistanceTo(destino);
		float duracion = Mathf.Clamp(dist / 650f, 0.35f, 0.75f);

		// Capturar referencias para el closure
		Node2D gRef = granada, oRef = objetivo;
		Vector2 o = origen, m = mid, d = destino;
		int dmg = danio;

		// Trayectoria Bézier cuadrática via TweenMethod
		Tween tw = CreateTween();
		tw.TweenMethod(
			Callable.From<float>(t =>
			{
				if (!IsInstanceValid(gRef)) return;
				float u = 1f - t;
				gRef.GlobalPosition  = u * u * o + 2f * u * t * m + t * t * d;
				gRef.RotationDegrees += 7f;
			}),
			0f, 1f, duracion
		);
		tw.Finished += () =>
		{
			if (IsInstanceValid(gRef)) gRef.QueueFree();
			CrearExplosion(d, esGranada: true);
			if (IsInstanceValid(oRef)) oRef.Call("RecibirDaño", dmg);
		};
	}

	// ── LANZAR MORTERO ────────────────────────────────────────────────────────
	// Sube recto, se reposiciona sobre el objetivo fuera de pantalla, cae en picado.
	private void LanzarMortero(Node2D objetivo)
	{
		if (objetivo == null || !IsInstanceValid(objetivo) || _escenaMisil == null) return;

		// Utiliza la posición exacta de SpotMortero
		Vector2 origenLocal = _spotMortero != null ? _spotMortero.GlobalPosition
												   : GlobalPosition + new Vector2(0f, -80f);
		Vector2 cima = new Vector2(origenLocal.X, -220f);

		Node2D misil = (Node2D)_escenaMisil.Instantiate();
		GetTree().Root.AddChild(misil);
		misil.GlobalPosition = origenLocal;
		misil.RotationDegrees = -90f; // Punta orientada directo hacia arriba al despegar
		misil.ZIndex = 50;

		Node2D mRef = misil, oRef = objetivo;

		// Fase 1: subida recta hacia el cielo
		Tween twSube = misil.CreateTween();
		twSube.TweenProperty(misil, "global_position", cima, 0.3f)
			  .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		twSube.Finished += () =>
		{
			if (!IsInstanceValid(mRef)) return;

			// Reposicionar sobre la cabeza del objetivo y voltear la punta hacia abajo (+90°)
			Vector2 arribaObj = new Vector2(
				oRef != null && IsInstanceValid(oRef) ? oRef.GlobalPosition.X : mRef.GlobalPosition.X,
				-220f
			);
			mRef.GlobalPosition  = arribaObj;
			mRef.RotationDegrees = 90f; // Punta apuntando directamente a la cabeza del rival

			float pausa = GD.Randf() * 0.15f + 0.5f;
			GetTree().CreateTimer(pausa).Timeout += () =>
			{
				if (!IsInstanceValid(mRef)) return;
				Vector2 destino = oRef != null && IsInstanceValid(oRef)
					? oRef.GlobalPosition + new Vector2(0f, 35f)
					: mRef.GlobalPosition;

				Tween twCae = mRef.CreateTween();
				twCae.TweenProperty(mRef, "global_position", destino, 0.28f)
					 .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
				twCae.Finished += () =>
				{
					if (IsInstanceValid(mRef)) mRef.QueueFree();
					CrearExplosion(destino, esGranada: false);
					if (IsInstanceValid(oRef)) oRef.Call("RecibirDaño", ATQ_MORTERO);
				};
			};
		};
	}

	// ── EXPLOSIÓN ─────────────────────────────────────────────────────────────
	private void CrearExplosion(Vector2 pos, bool esGranada)
	{
		PackedScene escena = esGranada ? _escenaExpGran : _escenaExpCent;
		if (escena == null) return;

		Node2D exp = (Node2D)escena.Instantiate();
		GetTree().Root.AddChild(exp);
		exp.GlobalPosition = pos;
		exp.ZIndex = esGranada ? 55 : 60;

		var animExp = exp.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (animExp != null)
		{
			if (!esGranada) animExp.Play("explosion_centro_c");
			else            animExp.Play();
			animExp.AnimationFinished += () => { if (IsInstanceValid(exp)) exp.QueueFree(); };
		}
		else
		{
			GetTree().CreateTimer(0.8f).Timeout += () => { if (IsInstanceValid(exp)) exp.QueueFree(); };
		}
	}

	// ── SEÑAL: CAMBIO DE FRAME ────────────────────────────────────────────────
	private void OnFrameChanged()
	{
		if (_anim == null) return;
		string anim = _anim.Animation.ToString();
		int    frame = _anim.Frame;

		// Frame 2 de cualquier ataque → granada parabólica desde SpotGranada
		if ((anim == "ataque 1" || anim == "ataque 2" || anim == "ataque 3") && frame == 2)
			LanzarGranada(_objetivo, puntosAtaque, _spotGranada);

		// Frame 12 de cualquier habilidad → mortero desde SpotMortero
		if ((anim == "habilidad 1" || anim == "habilidad 2" || anim == "habilidad 3") && frame == 12)
			LanzarMortero(_objetivo);

		// Frame 3 de derrota → último aliento desde SpotTroll (una sola vez)
		if ((anim == "derrota 1" || anim == "derrota 2" || anim == "derrota 3") && frame == 3 && !_mortalDisparado)
		{
			_mortalDisparado = true;
			Node2D ultimoObj = BuscarObjetivoEnCarril(true);
			if (ultimoObj != null) LanzarGranada(ultimoObj, puntosAtaque, _spotTroll);
		}
	}

	// ── SEÑAL: FIN DE ANIMACIÓN ───────────────────────────────────────────────
	private void OnAnimationFinished()
	{
		if (_anim == null) return;
		string anim = _anim.Animation.ToString();

		// Al terminar la animación de derrota, notifica al tablero
		if (anim.StartsWith("derrota"))
		{
			var campo = GetTree().Root.FindChild("Campo1", true, false);
			if (campo != null) campo.Call("EjecutarMuerteTropaSacrificada", this);
			else QueueFree();
			return;
		}

		switch (anim)
		{
			case "ataque 1":    case "ataque 2":    case "ataque 3":
			case "habilidad 1": case "habilidad 2": case "habilidad 3":
			case "daño 1":      case "daño 2":      case "daño 3":      case "daño 4":
				_anim.Play(AF("idle"));
				break;
		}
	}
}
