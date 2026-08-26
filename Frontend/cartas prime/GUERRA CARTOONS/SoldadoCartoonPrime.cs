using Godot;

/// <summary>SoldadoCartoon — Era Moderna: ráfaga de 4 balas (200 dmg total) y cobertura sostenida (50 dmg cada 5 s real).</summary>
public partial class SoldadoCartoonPrime : TropaBase
{
	public override string Tipo => Tipos.METAL;

	// ── ESTADO HABILIDAD ──────────────────────────────────────────────────────
	private bool   _habilidadActiva = false;
	private bool   _derrotaIniciada = false;
	private Timer  _timerHabilidad;

	// ── OBJETIVOS ─────────────────────────────────────────────────────────────
	private Node2D _objetivoAtaque;   // objetivo almacenado al inicio del ataque normal
	private Node2D _objetivoRafaga;   // objetivo de la habilidad especial

	// ══════════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		if (vidaMaxima == 0)
		{
			vidaActual   = vidaMaxima   = 220;
			escudoActual = escudoMaximo = 350;
			puntosAtaque = 50;
		}
		base._Ready();
		
		// Configuración del Timer con conteo real de 5.0 segundos
		_timerHabilidad = new Timer();
		_timerHabilidad.WaitTime = 5.0f;
		_timerHabilidad.OneShot = false;
		_timerHabilidad.Autostart = false;
		_timerHabilidad.Timeout += EjecutarDisparoHabilidad;
		AddChild(_timerHabilidad);

		_anim.FrameChanged      += OnFrameChanged;
		_anim.AnimationFinished += OnAnimationFinished;
	}

	/// <summary>La ráfaga (4×50 en los frames 2/4/6/8) se aplica sola vía OnFrameChanged;
	/// el llamador no debe sumar "puntosAtaque" aparte o se duplicaría el daño.</summary>
	public override bool AutogestionaDañoAtaque() => true;

	// ── ACCIONES ──────────────────────────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (_estaMuerto) return;
		switch (accion)
		{
			case "atacar":
				_yaActuo        = true;
				_objetivoAtaque = BuscarObjetivoEnCarril();
				ReproducirAtaque();   // TropaBase: play "ataque" → async → idle al terminar
				break;

			case "preparar_defensa":
				_yaActuo = true;
				_anim.Play("pre defensa");   // OnAnimationFinished → Pause() en último frame
				break;

			case "recibir_daño":
				// Sin await: reinicia la animación en cada golpe para soportar multi-hit
				_anim.Play("daño");
				break;

			case "defender":
				// Si la habilidad está activa, el impacto la cancela (daño ya fue absorbido al escudo en RecibirDaño)
				if (_habilidadActiva)
					CancelarHabilidad();   // → ReproducirDefensa → idle
				else
					ReproducirDefensa();   // TropaBase: play "defensa" → async → idle
				break;

			case "usar_habilidad":
				UsarHabilidadPropia();
				break;
		}
	}

	// ── HABILIDAD ESPECIAL: RÁFAGA EN COBERTURA ───────────────────────────────
	protected override void UsarHabilidadPropia()
	{
		// REGLA: Solo se puede activar si la barra de escudo tiene vida (> 0)
		if (habilidadUsada || _habilidadActiva || escudoActual <= 0) return;
		
		_objetivoRafaga = BuscarObjetivoEnCarril();
		if (_objetivoRafaga == null) return;

		_habilidadActiva = true;
		habilidadUsada   = true;

		// Paso 1: Transición vía "pre defensa" (al terminar OnAnimationFinished iniciará "habilidad")
		_anim.Play("pre defensa");
	}

	/// <summary>
	/// Mientras la habilidad está activa, el soldado está en cobertura: cualquier
	/// impacto recibido golpea únicamente el escudo (nunca la vida, sin importar
	/// la cantidad) y cancela la habilidad de inmediato.
	/// </summary>
	public override void RecibirDaño(int cantidad)
	{
		if (_estaMuerto) return;

		if (_habilidadActiva)
		{
			EfectoGolpe();
			escudoActual = Mathf.Max(0, escudoActual - cantidad);
			ActualizarBarrasUI();
			EjecutarAccion("defender");
			return;
		}

		base.RecibirDaño(cantidad);
	}

	private void CancelarHabilidad()
	{
		_habilidadActiva = false;
		if (_timerHabilidad != null && !_timerHabilidad.IsStopped())
			_timerHabilidad.Stop();

		// "defensa" → "idle" automáticamente (TropaBase.ReproducirDefensa es async)
		ReproducirDefensa();
	}

	private void EjecutarDisparoHabilidad()
	{
		if (!_habilidadActiva || _estaMuerto) return;

		if (_objetivoRafaga == null || !IsInstanceValid(_objetivoRafaga))
		{
			CancelarHabilidad();
			return;
		}

		// Reproduce la animación "habilidad". Al tocar el frame 0 aplicará el daño
		_anim.Play("habilidad");
	}

	// ── SEÑAL: CAMBIO DE FRAME ────────────────────────────────────────────────
	private void OnFrameChanged()
	{
		string anim  = (string)_anim.Animation;
		int    frame = _anim.Frame;

		// Ataque en ráfaga: 4 impactos de 50 en los frames 2, 4, 6 y 8 → total 200
		if (anim == "ataque" && (frame == 2 || frame == 4 || frame == 6 || frame == 8))
		{
			if (_objetivoAtaque != null && IsInstanceValid(_objetivoAtaque))
			{
				// CORREGIDO: Se llama solo a RecibirDaño para no romper la postura defensiva del objetivo
				_objetivoAtaque.Call("RecibirDaño", 50);
			}
		}

		// Habilidad: disparo en el frame 0 de la animación "habilidad"
		if (anim == "habilidad" && frame == 0 && _habilidadActiva)
		{
			if (_objetivoRafaga == null || !IsInstanceValid(_objetivoRafaga))
			{
				CancelarHabilidad();
			}
			else
			{
				// CORREGIDO: Se llama solo a RecibirDaño para no romper la postura defensiva del objetivo
				_objetivoRafaga.Call("RecibirDaño", 50);
			}
		}

		// Derrota: inicia fade-out en el frame 18 (antepenúltimo)
		if (anim == "derrota" && frame == 18 && !_derrotaIniciada)
		{
			_derrotaIniciada = true;
			Tween tw = CreateTween();
			tw.TweenProperty(this, "modulate:a", 0.0f, 0.4f);
			tw.Finished += () => { if (IsInstanceValid(this)) QueueFree(); };
		}
	}

	// ── SEÑAL: FIN DE ANIMACIÓN ───────────────────────────────────────────────
	private void OnAnimationFinished()
	{
		switch ((string)_anim.Animation)
		{
			case "pre defensa":
				if (_habilidadActiva)
				{
					// Transición: Entra en animación habilidad, hace su primer disparo
					_anim.Play("habilidad");
					
					// Y AHORA SÍ arranca el reloj de 5 segundos para el SIGUIENTE disparo
					_timerHabilidad.Start(5.0f);
				}
				else
				{
					// Congelar en el último frame: mantiene postura defensiva normal
					_anim.Pause();
				}
				break;

			case "daño":
				// Volver a idle una vez que termina el último golpe recibido
				if (!_estaMuerto) ReproducirIdle();
				break;
		}
	}
}
