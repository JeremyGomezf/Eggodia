using Godot;

/// <summary>
/// TanqueCartoonPrime — Tanque pesado de la era cartoon.
/// 820 HP / 350 ATQ / Sin escudo.
/// Animaciones dinámicas: cambia a variante "- dañado" cuando vida <= 400.
/// Disparo de misil en frame 2 de ataque; habilidad "Doble Disparo" (2 misiles con 2 s de pausa).
/// </summary>
public partial class TanqueCartoonPrime : TropaBase
{
	public override string Tipo => Tipos.METAL;
	protected override int TurnoDesbloqueoHabilidad => 4;

	// ── CONSTANTES ────────────────────────────────────────────────────────────
	private const int    UMBRAL_DAÑADO  = 400;                                        // vida en que cambian las animaciones
	private const string RUTA_MISIL     = "res://efectos/misil_cartoon.tscn";
	private const string RUTA_EXPLOSION = "res://efectos/explosion_centro_cartoon.tscn";

	// ── ESTADO ────────────────────────────────────────────────────────────────
	private bool _derrotaIniciada = false;
	private bool _habilidadActiva = false;  // true mientras se ejecuta el segundo disparo

	// ── OBJETIVO ──────────────────────────────────────────────────────────────
	private Node2D _objetivoAtaque;

	// ── RECURSOS PRE-CARGADOS ─────────────────────────────────────────────────
	private Marker2D    _spotCañon;        // posición de salida del misil (boca del cañón)
	private PackedScene _escenaMisil;
	private PackedScene _escenaExplosion;

	// ══════════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		if (vidaMaxima == 0)
		{
			vidaActual   = vidaMaxima   = 820;
			escudoActual = escudoMaximo = 0;   // El tanque no tiene escudo
			puntosAtaque = 350;
		}
		base._Ready();

		// Ocultar la barra de escudo: el Tanque no usa escudo
		var barraEscudo = GetNodeOrNull<ProgressBar>("StatsTropa/BarraEscudo");
		if (barraEscudo != null) barraEscudo.Visible = false;

		// Spot de disparo en la boca del cañón (Marker2D hijo en la escena)
		_spotCañon = GetNodeOrNull<Marker2D>("SpotCañon");

		// Pre-cargar escenas de efectos para no causar I/O durante la batalla
		if (ResourceLoader.Exists(RUTA_MISIL))
			_escenaMisil = GD.Load<PackedScene>(RUTA_MISIL);
		if (ResourceLoader.Exists(RUTA_EXPLOSION))
			_escenaExplosion = GD.Load<PackedScene>(RUTA_EXPLOSION);

		_anim.FrameChanged      += OnFrameChanged;
		_anim.AnimationFinished += OnAnimationFinished;
	}

	// ── HELPER: NOMBRE DE ANIMACIÓN SEGÚN VIDA ───────────────────────────────
	// Devuelve el nombre correcto según el estado de salud del tanque.
	// Si vida > UMBRAL_DAÑADO: versión normal.  Si vida <= UMBRAL_DAÑADO: versión "- dañado".
	private string AnimNombre(string animBase) =>
		vidaActual <= UMBRAL_DAÑADO ? $"{animBase} - dañado" : animBase;

	// ── CAPACIDADES ──────────────────────────────────────────────────────────
	// El daño al objetivo lo aplica el script (vía LanzarMisil), no Campo1.
	public override bool AutogestionaDañoAtaque() => true;
	// Sin postura defensiva ni escudo: ocultar botón defensa
	public override bool MostrarBotonDefensa() => false;

	// ── ACCIONES ──────────────────────────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (_estaMuerto) return;
		switch (accion)
		{
			case "atacar":
				_yaActuo        = true;
				_objetivoAtaque = BuscarObjetivoEnCarril();
				_anim.Play(AnimNombre("ataque"));    // misil se lanza en frame 2 vía OnFrameChanged
				break;

			case "preparar_defensa":
				// El tanque no tiene animación de defensa: se queda en idle como postura
				_yaActuo = true;
				_anim.Play(AnimNombre("idle"));
				break;

			case "recibir_daño":
				_anim.Play(AnimNombre("daño"));
				break;

			case "defender":
				// Sin animación de defensa específica
				_anim.Play(AnimNombre("idle"));
				break;

			case "usar_habilidad":
				UsarHabilidadPropia();
				break;
		}
	}

	// ── RECIBIR DAÑO ─────────────────────────────────────────────────────────
	public override void RecibirDaño(int cantidad)
	{
		if (_estaMuerto) return;
		base.RecibirDaño(cantidad);
	}

	// ── DERROTA ──────────────────────────────────────────────────────────────
	// El tanque solo tiene "derrota - dañado" (no existe "derrota" normal).
	// "new" oculta TropaBase.ReproducirDerrota para que Call("ReproducirDerrota") llegue aquí.
	public new void ReproducirDerrota()
	{
		_estaMuerto = true;
		_anim.Play("derrota - dañado");
	}

	// ── HABILIDAD ESPECIAL: DOBLE DISPARO ────────────────────────────────────
	// Primer disparo inmediato + segundo disparo automático 2 segundos después.
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada || _habilidadActiva) return;

		Node2D objetivo = BuscarObjetivoEnCarril();
		if (objetivo == null) return;

		_habilidadActiva = true;
		habilidadUsada   = true;
		_yaActuo         = true;
		_objetivoAtaque  = objetivo;

		// Primer disparo: reproducir animación de ataque (el misil sale en frame 2)
		_anim.Play(AnimNombre("ataque"));

		// El segundo disparo se encola en OnAnimationFinished cuando _habilidadActiva == true
	}

	// ── DISPARO DE MISIL ──────────────────────────────────────────────────────
	// Instancia el misil en el SpotCañon, lo desplaza mediante Tween hasta el objetivo,
	// y al llegar crea la explosión y aplica el daño.
	private void LanzarMisil(Node2D objetivo)
	{
		if (objetivo == null || !IsInstanceValid(objetivo)) return;
		if (_escenaMisil == null) return;

		// Punto de origen: boca del cañón (Marker2D, orientado al lado del rival si aplica)
		Vector2 origen  = ObtenerSpotOrientado(_spotCañon);
		// Punto de impacto: centro-superior del objetivo
		Vector2 destino = objetivo.GlobalPosition + new Vector2(0f, -40f);

		// Instanciar misil en la escena raíz para que no herede transformaciones del tanque
		Node2D misil = (Node2D)_escenaMisil.Instantiate();
		GetTree().Root.AddChild(misil);
		misil.GlobalPosition = origen;
		misil.ZIndex = ZIndex + 1;

		// Orientar el misil hacia el destino (útil si tiene sprite direccional)
		misil.Rotation = origen.AngleToPoint(destino);

		// Proyectil ultrarrápido: ráfaga directa desde el cañón
		float distancia = origen.DistanceTo(destino);
		float duracion  = Mathf.Clamp(distancia / 4500f, 0.04f, 0.09f);

		Tween tw = misil.CreateTween();
		tw.TweenProperty(misil, "global_position", destino, duracion);
		tw.Finished += () =>
		{
			if (IsInstanceValid(misil)) misil.QueueFree();
			CrearExplosion(destino);
			var campo = GetTree().Root.FindChild("Campo1", true, false);
			if (IsInstanceValid(objetivo))
			{
				objetivo.Call("RecibirDaño", puntosAtaque);
				if (campo != null) campo.Call("RegistrarDañoTropa", this, puntosAtaque);
			}
			// Vibración de cámara al impacto
			if (campo != null && campo.HasMethod("ScreenShake"))
				campo.Call("ScreenShake", 7f);
		};
	}

	// ── EXPLOSIÓN EN PUNTO DE IMPACTO ────────────────────────────────────────
	// La escena de explosión reproduce "explosion_centro_c" (9 frames) y se libera sola.
	private void CrearExplosion(Vector2 posicion)
	{
		if (_escenaExplosion == null) return;

		Node2D explosion = (Node2D)_escenaExplosion.Instantiate();
		GetTree().Root.AddChild(explosion);
		explosion.GlobalPosition = posicion;
		explosion.ZIndex = ZIndex + 1;

		var animExp = explosion.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (animExp != null)
		{
			animExp.Play("explosion_centro_c");
			DesvanecerAlAntepenultimoFrame(explosion, animExp);
		}
		else
		{
			// Fallback: liberar tras el tiempo estimado de 9 frames a ~12 fps
			GetTree().CreateTimer(0.75f).Timeout += () => DesvanecerYLiberar(explosion);
		}
	}

	// ── SEÑAL: CAMBIO DE FRAME ────────────────────────────────────────────────
	private void OnFrameChanged()
	{
		string anim  = (string)_anim.Animation;
		int    frame = _anim.Frame;

		// Frame 2 de cualquier variante de ataque → lanzar misil
		if ((anim == "ataque" || anim == "ataque - dañado") && frame == 2)
		{
			LanzarMisil(_objetivoAtaque);
		}

		// Inicio de fade-out en frame 18 de la derrota (antes del último frame)
		if (anim == "derrota - dañado" && frame == 18 && !_derrotaIniciada)
		{
			_derrotaIniciada = true;
			Tween tw = CreateTween();
			tw.TweenProperty(this, "modulate:a", 0.0f, 0.5f);
			tw.Finished += () => { if (IsInstanceValid(this)) QueueFree(); };
		}
	}

	// ── SEÑAL: FIN DE ANIMACIÓN ───────────────────────────────────────────────
	private void OnAnimationFinished()
	{
		switch ((string)_anim.Animation)
		{
			case "ataque":
			case "ataque - dañado":
				if (_estaMuerto) break;
				if (_habilidadActiva)
				{
					// Segundo disparo del Doble Disparo: esperar 2 segundos y volver a atacar
					GetTree().CreateTimer(2.0f).Timeout += () =>
					{
						if (_estaMuerto || !IsInstanceValid(this)) { _habilidadActiva = false; return; }
						// Refrescar objetivo por si cambió durante la espera
						Node2D obj2 = BuscarObjetivoEnCarril();
						if (obj2 != null) _objetivoAtaque = obj2;
						_anim.Play(AnimNombre("ataque"));   // misil sale en frame 2 vía OnFrameChanged
						// Al terminar esta segunda animación, _habilidadActiva se apaga en el siguiente ciclo
						_habilidadActiva = false;
					};
				}
				else
				{
					_anim.Play(AnimNombre("idle"));
				}
				break;

			case "daño":
			case "daño - dañado":
				if (!_estaMuerto) _anim.Play(AnimNombre("idle"));
				break;
		}
	}

}
