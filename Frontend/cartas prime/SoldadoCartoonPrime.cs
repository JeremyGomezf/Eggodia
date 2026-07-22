using Godot;

/// <summary>SoldadoCartoon — Era Moderna: ráfaga de 4 balas (200 dmg total) y cobertura sostenida (50 dmg cada 3 s).</summary>
public partial class SoldadoCartoonPrime : TropaBase
{
	public override string Tipo => Tipos.METAL;

	// ── ESTADO HABILIDAD ──────────────────────────────────────────────────────
	private bool   _habilidadActiva = false;
	private bool   _derrotaIniciada = false;
	private int    _ciclosRafaga    = 0;   // vueltas completas del loop "pre defensa -> habilidad"

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
		if (habilidadUsada || _habilidadActiva) return;
		_objetivoRafaga = BuscarObjetivoEnCarril();
		if (_objetivoRafaga == null) return;

		_habilidadActiva = true;
		habilidadUsada   = true;
		_ciclosRafaga    = 0;

		// Animación propia de la habilidad (NO reutiliza "pre defensa")
		_anim.Play("pre defensa -> habilidad");
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
		_ciclosRafaga    = 0;
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

		// Habilidad: ráfaga en cobertura — 50 dmg cada 3 s, disparado en el frame 0.
		// "pre defensa -> habilidad" tiene 18 frames a 12 fps = 1.5 s por vuelta,
		// así que se aplica daño cada 2 vueltas (1.5 s x 2 = 3 s exactos).
		if (anim == "pre defensa -> habilidad" && frame == 0 && _habilidadActiva)
		{
			_ciclosRafaga++;
			if (_ciclosRafaga % 2 == 0)
			{
				if (_objetivoRafaga == null || !IsInstanceValid(_objetivoRafaga))
					CancelarHabilidad();
				else
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
