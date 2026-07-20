using Godot;

/// <summary>SoldadoCartoon — Era Moderna: ráfaga de 4 balas (200 dmg total) y cobertura sostenida (50 dmg cada 3 s).</summary>
public partial class SoldadoCartoonPrime : TropaBase
{
	// ── ESTADO HABILIDAD ──────────────────────────────────────────────────────
	private bool   _habilidadActiva = false;
	private bool   _derrotaIniciada = false;
	private Timer  _timerRafaga;

	// ── OBJETIVOS ─────────────────────────────────────────────────────────────
	private Node2D _objetivoAtaque;   // objetivo almacenado al inicio del ataque normal
	private Node2D _objetivoRafaga;   // objetivo de la habilidad especial

	// ══════════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		if (vidaMaxima == 0)
		{
			vidaActual   = vidaMaxima   = 400;
			escudoActual = escudoMaximo = 600;
			puntosAtaque = 50;
		}
		base._Ready();
		_anim.FrameChanged      += OnFrameChanged;
		_anim.AnimationFinished += OnAnimationFinished;
	}

	/// <summary>Permite que Campo1 muestre el botón ⚡ HABILIDAD para esta tropa.</summary>
	public bool TieneHabilidadEspecial() => true;

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
				// Si la habilidad está activa, el impacto la cancela (daño ya fue absorbido al escudo por TropaBase)
				if (_habilidadActiva)
					CancelarHabilidad();   // detiene timer → ReproducirDefensa → idle
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
		if (habilidadUsada || _habilidadActiva) return;
		_objetivoRafaga = BuscarObjetivoEnCarril();
		if (_objetivoRafaga == null) return;

		_habilidadActiva = true;
		habilidadUsada   = true;

		// Animación propia de la habilidad (NO reutiliza "pre defensa")
		_anim.Play("pre defensa -> habilidad");

		_timerRafaga          = new Timer();
		_timerRafaga.WaitTime = 3.0f;
		_timerRafaga.Timeout  += OnTickRafaga;
		AddChild(_timerRafaga);
		_timerRafaga.Start();
	}

	private void OnTickRafaga()
	{
		// Condición de cancelación A: el personaje murió
		if (!_habilidadActiva || _estaMuerto) return;

		// Condición de cancelación A: el objetivo en su carril murió
		if (_objetivoRafaga == null || !IsInstanceValid(_objetivoRafaga))
		{
			CancelarHabilidad();
			return;
		}

		// Disparo sostenido: 50 de daño cada 3 segundos
		_objetivoRafaga.Call("RecibirDaño", 50);
	}

	private void CancelarHabilidad()
	{
		_habilidadActiva = false;
		if (_timerRafaga != null && IsInstanceValid(_timerRafaga))
		{
			_timerRafaga.Stop();
			_timerRafaga.QueueFree();
			_timerRafaga = null;
		}
		// "defensa" → "idle" automáticamente (TropaBase.ReproducirDefensa es async)
		ReproducirDefensa();
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
				_objetivoAtaque.Call("RecibirDaño", 50);
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
				// Congelar en el último frame: mantiene postura defensiva
				_anim.Pause();
				break;

			case "daño":
				// Volver a idle una vez que termina el último golpe recibido
				if (!_estaMuerto) ReproducirIdle();
				break;
		}
	}

	// ── HELPER: BUSCAR ENEMIGO EN EL MISMO CARRIL ────────────────────────────
	private Node2D BuscarObjetivoEnCarril()
	{
		if (!HasMeta("carril")) return null;

		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		string miCarril = ((string)GetMeta("carril"))
			.ToLower().Replace("modrival", "").Replace("mod", "").Trim();

		Node2D mejor = null;
		int    min   = int.MaxValue;
		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			string c = ((string)e.GetMeta("carril"))
				.ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			if (c != miCarril) continue;
			int v = 0;
			try { v = (int)e.Get("vidaActual"); } catch { }
			if (v < min) { min = v; mejor = e; }
		}
		return mejor;
	}
}
