using Godot;

/// <summary>
/// GranaderoCartoonPrime — Soldado atrincherado. 50 HP / 600 ESC / 235 ATQ.
/// Sin postura defensiva. Tres facetas visuales según el escudo restante.
/// Ataque: granada parabólica en frame 2. Habilidad: mortero vertical (350 dmg).
/// </summary>
public partial class GranaderoCartoonPrime : TropaBase
{
	public override string Tipo => Tipos.METAL;

	// ── CONSTANTES ────────────────────────────────────────────────────────────
	private const int  ATQ_MORTERO     = 350;
	private const string RUTA_GRANADA   = "res://efectos/granada_cartoon.tscn";
	private const string RUTA_EXP_GRAN  = "res://efectos/explosion_granada_cartoon.tscn";
	private const string RUTA_EXP_CENT  = "res://efectos/explosion_centro_cartoon.tscn";

	// ── ESTADO ────────────────────────────────────────────────────────────────
	private int    _faceta          = 1;   // 1=600-301, 2=300-1, 3=0
	private bool   _derrotaIniciada = false;
	private Node2D _objetivo;

	// ── RECURSOS ──────────────────────────────────────────────────────────────
	private Marker2D    _spotMano;
	private PackedScene _escenaGranada;
	private PackedScene _escenaExpGran;
	private PackedScene _escenaExpCent;

	// ══════════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		if (vidaMaxima == 0)
		{
			vidaActual   = vidaMaxima   = 50;
			escudoActual = escudoMaximo = 600;
			puntosAtaque = 235;
		}
		base._Ready();

		_spotMano = GetNodeOrNull<Marker2D>("SpotMano");

		if (ResourceLoader.Exists(RUTA_GRANADA))  _escenaGranada = GD.Load<PackedScene>(RUTA_GRANADA);
		if (ResourceLoader.Exists(RUTA_EXP_GRAN)) _escenaExpGran = GD.Load<PackedScene>(RUTA_EXP_GRAN);
		if (ResourceLoader.Exists(RUTA_EXP_CENT)) _escenaExpCent = GD.Load<PackedScene>(RUTA_EXP_CENT);

		_anim.FrameChanged      += OnFrameChanged;
		_anim.AnimationFinished += OnAnimationFinished;

		_faceta = 1;
		_anim.Play("idle 1");
	}

	// ── CAPACIDADES ───────────────────────────────────────────────────────────
	public override bool AutogestionaDañoAtaque()   => true;
	public override bool TieneHabilidadEspecial()   => true;
	public override bool TienePosturaDefensiva()    => false;  // sin postura de defensa

	// ── HELPER: NOMBRE DE ANIMACIÓN SEGÚN FACETA ─────────────────────────────
	private string AF(string base_) => $"{base_} {_faceta}";

	// ── ACCIONES ──────────────────────────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (_estaMuerto) return;
		switch (accion)
		{
			case "atacar":
				_yaActuo  = true;
				_objetivo = BuscarObjetivo();
				_anim.Play(AF("ataque"));
				break;

			case "preparar_defensa":
			case "defender":
				// Atrincherado: no adopta postura defensiva; consume el turno en idle
				_yaActuo = true;
				_anim.Play(AF("idle"));
				break;

			case "recibir_daño":
				// Gestionado internamente en RecibirDaño (depende del estado del escudo)
				break;

			case "usar_habilidad":
				UsarHabilidadPropia();
				break;
		}
	}

	// ── RECIBIR DAÑO ─────────────────────────────────────────────────────────
	// El escudo actúa como parapeto. Las facetas cambian según cuánto queda.
	public override void RecibirDaño(int cantidad)
	{
		if (_estaMuerto) return;
		EfectoGolpe();

		int escudoAntes = escudoActual;

		// Absorber con escudo primero
		if (escudoActual > 0)
		{
			int abs = Mathf.Min(escudoActual, cantidad);
			escudoActual -= abs;
			cantidad     -= abs;
		}

		// Actualizar barra de escudo
		var barraEsc = GetNodeOrNull<ProgressBar>("StatsTropa/BarraEscudo");
		if (barraEsc != null) { barraEsc.MaxValue = escudoMaximo; barraEsc.Value = escudoActual; }

		// ── DETERMINAR ANIMACIÓN DE DAÑO Y TRANSICIÓN DE FACETA ──────────────
		if (escudoAntes > 300 && escudoActual is > 0 and <= 300)
		{
			// Transición 1→2: escudo cae de zona alta a zona baja
			_faceta = 2;
			_anim.Play("daño 2");
		}
		else if (escudoAntes > 0 && escudoActual == 0)
		{
			_faceta = 3;
			// Impacto masivo: destruyó los 600 puntos de una sola vez (desde máximo)
			bool impactoMasivo = escudoAntes == escudoMaximo;
			_anim.Play(impactoMasivo ? "daño 4" : "daño 3");
		}
		else
		{
			// Daño normal dentro de la faceta actual
			_anim.Play(AF("daño"));
		}

		// Aplicar daño restante a la vida (si el escudo no absorbió todo)
		if (cantidad > 0)
		{
			vidaActual -= cantidad;
			var barraVid = GetNodeOrNull<ProgressBar>("StatsTropa/BarraVida");
			if (barraVid != null) { barraVid.MaxValue = vidaMaxima; barraVid.Value = Mathf.Max(0, vidaActual); }
		}

		if (vidaActual <= 0 && escudoActual <= 0)
		{
			vidaActual  = 0;
			_estaMuerto = true;
			var campo = GetTree().Root.FindChild("Campo1", true, false);
			if (campo != null) campo.Call("EjecutarMuerteTropaSacrificada", this);
		}
	}

	// ── DERROTA POR SACRIFICIO ────────────────────────────────────────────────
	public new void ReproducirDerrota()
	{
		_estaMuerto = true;
		_anim.Play(AF("derrota"));
	}

	// ── HABILIDAD: MORTERO ────────────────────────────────────────────────────
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada) return;
		Node2D obj = BuscarObjetivo();
		if (obj == null) return;

		habilidadUsada = true;
		_yaActuo       = true;
		_anim.Play(AF("habilidad"));

		// Lanzar proyectil con un pequeño retardo para sincronizar con la animación
		GetTree().CreateTimer(0.35f).Timeout += () => LanzarMortero(obj);
	}

	// ── ATAQUE: GRANADA PARABÓLICA ────────────────────────────────────────────
	private void LanzarGranada(Node2D objetivo)
	{
		if (objetivo == null || !IsInstanceValid(objetivo) || _escenaGranada == null) return;

		Vector2 origen  = _spotMano != null ? _spotMano.GlobalPosition : GlobalPosition + new Vector2(30f, -40f);
		Vector2 destino = objetivo.GlobalPosition + new Vector2(0f, -30f);
		// Cima de la parábola: punto medio desplazado hacia arriba
		Vector2 mid     = (origen + destino) * 0.5f + new Vector2(0f, -100f);

		Node2D granada = (Node2D)_escenaGranada.Instantiate();
		GetTree().Root.AddChild(granada);
		granada.GlobalPosition = origen;
		granada.ZIndex = 50;

		float dist     = origen.DistanceTo(destino);
		float duracion = Mathf.Clamp(dist / 650f, 0.35f, 0.75f);

		// Capturar referencias para el closure
		Node2D gRef = granada, oRef = objetivo;
		Vector2 o = origen, m = mid, d = destino;

		// Trayectoria Bézier cuadrática mediante TweenMethod (t va de 0 a 1)
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
			if (IsInstanceValid(oRef)) oRef.Call("RecibirDaño", puntosAtaque);
		};
	}

	// ── HABILIDAD: MORTERO ────────────────────────────────────────────────────
	// Sube verticalmente hasta salir de pantalla, pausa, cae en picado sobre el objetivo.
	private void LanzarMortero(Node2D objetivo)
	{
		if (objetivo == null || !IsInstanceValid(objetivo) || _escenaGranada == null) return;

		Vector2 origen = GlobalPosition + new Vector2(0f, -80f);
		Vector2 cima   = origen + new Vector2(0f, -350f);  // fuera del margen superior

		Node2D misil = (Node2D)_escenaGranada.Instantiate();
		GetTree().Root.AddChild(misil);
		misil.GlobalPosition = origen;
		misil.ZIndex = 50;

		Node2D mRef = misil, oRef = objetivo;

		// Fase 1: subida vertical rápida
		Tween twSube = misil.CreateTween();
		twSube.TweenProperty(misil, "global_position", cima, 0.35f)
			  .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

		twSube.Finished += () =>
		{
			if (!IsInstanceValid(mRef)) return;

			// Pausa de 0.5–0.8 s antes de la caída
			float pausa = GD.Randf() * 0.3f + 0.5f;
			GetTree().CreateTimer(pausa).Timeout += () =>
			{
				if (!IsInstanceValid(mRef)) return;
				Vector2 destino = oRef != null && IsInstanceValid(oRef)
					? oRef.GlobalPosition + new Vector2(0f, -30f)
					: mRef.GlobalPosition;  // fallback: cae donde está

				// Fase 2: caída vertical en picado
				Tween twCae = mRef.CreateTween();
				twCae.TweenProperty(mRef, "global_position", destino, 0.3f)
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
		exp.ZIndex = 55;

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

	// ── BUSCAR OBJETIVO ───────────────────────────────────────────────────────
	private Node2D BuscarObjetivo()
	{
		if (!HasMeta("carril")) return null;
		string grupo    = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		string miCarril = ((string)GetMeta("carril"))
			.ToLower().Replace("modrival", "").Replace("mod", "").Trim();

		Node2D mejor = null;
		int    min   = int.MaxValue;

		foreach (Node n in GetTree().GetNodesInGroup(grupo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			string c = ((string)e.GetMeta("carril")).ToLower().Replace("modrival","").Replace("mod","").Trim();
			if (c != miCarril) continue;
			int v = 0;
			try { v = (int)e.Get("vidaActual"); } catch { }
			if (v > 0 && v < min) { min = v; mejor = e; }
		}
		return mejor;
	}

	// ── SEÑAL: CAMBIO DE FRAME ────────────────────────────────────────────────
	private void OnFrameChanged()
	{
		string anim  = (string)_anim.Animation;
		int    frame = _anim.Frame;

		// Frame 2 de cualquier variante de ataque → lanzar granada
		if ((anim == "ataque 1" || anim == "ataque 2" || anim == "ataque 3") && frame == 2)
			LanzarGranada(_objetivo);
	}

	// ── SEÑAL: FIN DE ANIMACIÓN ───────────────────────────────────────────────
	private void OnAnimationFinished()
	{
		if (_estaMuerto) return;
		string anim = (string)_anim.Animation;

		switch (anim)
		{
			case "ataque 1": case "ataque 2": case "ataque 3":
			case "daño 1":   case "daño 2":   case "daño 3": case "daño 4":
			case "habilidad 1": case "habilidad 2": case "habilidad 3":
				_anim.Play(AF("idle"));
				break;

			case "derrota 1": case "derrota 2": case "derrota 3":
				if (_derrotaIniciada) break;
				_derrotaIniciada = true;
				Tween tw = CreateTween();
				tw.TweenProperty(this, "modulate:a", 0f, 0.5f);
				tw.Finished += () => { if (IsInstanceValid(this)) QueueFree(); };
				break;
		}
	}
}
