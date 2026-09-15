using Godot;

/// <summary>
/// KaBarCartoonPrime — Soldado fantasma que resucita como espíritu combatiente.
/// 250 HP / 330 ESC / 115 ATQ.
/// El escudo absorbe daño SOLO en postura defensiva explícita.
/// Al morir en combate: 4 s después aparece como fantasma invulnerable en zona rival,
/// ataca cada 5 s (30 dmg), dura 25 s máx o hasta eliminar al rival.
/// </summary>
public partial class KaBarCartoonPrime : TropaBase
{
	public override string Tipo => Tipos.NEUTRO;

	// ── ESTADO ────────────────────────────────────────────────────────────────
	private bool   _enDefensa        = false;  // true solo mientras está esperando/bloqueando
	private bool   _modoFantasma     = false;
	private bool   _fantasmaActivo   = false;
	private bool   _eraJugador       = true;
	private string _carrilNum        = "";
	private float  _timerAtaque      = 5.0f;
	private float  _timerVida        = 25.0f;
	private bool   _atacandoFantasma = false;
	private bool   _finalizando      = false;
	private Node2D _objetivoAtaque   = null;

	// ══════════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		if (vidaMaxima == 0)
		{
			vidaActual   = vidaMaxima   = 250;
			escudoActual = escudoMaximo = 330;
			puntosAtaque = 115;
		}
		base._Ready();

		if (_anim != null)
		{
			_anim.Connect(AnimatedSprite2D.SignalName.FrameChanged, Callable.From(OnFrameChanged));
			_anim.Connect(AnimatedSprite2D.SignalName.AnimationFinished, Callable.From(OnAnimationFinished));
		}

		SetProcess(false);
	}

	// ── PROCESO (SOLO MODO FANTASMA ACTIVO) ──────────────────────────────────
	public override void _Process(double delta)
	{
		if (!_modoFantasma || !_fantasmaActivo || _finalizando) return;

		_timerVida   -= (float)delta;
		_timerAtaque -= (float)delta;

		if (BuscarObjetivoFantasma() == null) { IniciarFinalFantasma(victoria: true);  return; }
		if (_timerVida <= 0f)                 { IniciarFinalFantasma(victoria: false); return; }

		if (_timerAtaque <= 0f && !_atacandoFantasma)
		{
			_timerAtaque      = 5.0f;
			_atacandoFantasma = true;
			_anim.Play("ataque_fantasma");
		}
	}

	// ── CAPACIDADES ──────────────────────────────────────────────────────────
	// Daño de ataque propio en frame 4 (vivo) y frame 1 (fantasma).
	public override bool AutogestionaDañoAtaque() => true;
	// Sin habilidad activa propia: ocultar botón habilidad completamente
	public override bool TieneHabilidadEspecial() => false;
	public override bool MostrarBotonHabilidad()  => false;

	// ── ACCIONES ESTÁNDAR ──────────────────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (_estaMuerto)   return;
		if (_modoFantasma) return;

		switch (accion)
		{
			case "atacar":
				_enDefensa = false;
				_yaActuo   = true;
				_objetivoAtaque = BuscarObjetivoEnCarril();
				_anim.Play("ataque");
				break;
			case "preparar_defensa":
			case "defender":
				_yaActuo   = true;
				_enDefensa = true; // Activa la guardia desde que inicia el bucle de espera
				_anim.Play("pre defensa ");   // espacio final: nombre exacto del sprite sheet
				break;
			case "recibir_daño":
				if (_enDefensa)
				{
					_anim.Play("defensa"); // Si le pegan estando en guardia, hace la animación de impacto con escudo
				}
				else
				{
					_anim.Play("daño"); // Daño normal fuera de postura defensiva
				}
				break;
		}
	}

	public new void SetActivo(bool activo)
	{
		if (_modoFantasma) return;
		base.SetActivo(activo);
	}

	// ── RECIBIR DAÑO ──────────────────────────────────────────────────────────
	// El escudo solo absorbe daño si la tropa está en postura defensiva explícita.
	public override void RecibirDaño(int cantidad)
	{
		if (_modoFantasma) return;  // el fantasma es completamente invulnerable
		if (_estaMuerto)   return;

		// Absorción de escudo SOLO en postura defensiva
		if (_enDefensa && escudoActual > 0)
		{
			int abs = Mathf.Min(escudoActual, cantidad);
			escudoActual -= abs;
			cantidad     -= abs;
			var barraEsc = GetNodeOrNull<ProgressBar>("StatsTropa/BarraEscudo");
			if (barraEsc != null) { barraEsc.MaxValue = escudoMaximo; barraEsc.Value = escudoActual; }
		}

		if (cantidad <= 0)
		{
			EjecutarAccion("recibir_daño");
			return;
		}

		vidaActual -= cantidad;
		var barraVid = GetNodeOrNull<ProgressBar>("StatsTropa/BarraVida");
		if (barraVid != null) { barraVid.MaxValue = vidaMaxima; barraVid.Value = Mathf.Max(0, vidaActual); }

		if (vidaActual <= 0)
		{
			vidaActual = 0;
			IniciarMuerteCombate();
		}
		else
		{
			EjecutarAccion("recibir_daño");
		}
	}

	// ── DERROTA POR SACRIFICIO ────────────────────────────────────────────────
	// Llamado por Campo1 vía Call("ReproducirDerrota"). Sin fase fantasma.
	public override void ReproducirDerrota()
	{
		_estaMuerto = true;
		_anim.Play("derrota");
	}

	// ── MUERTE EN COMBATE ─────────────────────────────────────────────────────
	private void IniciarMuerteCombate()
	{
		_estaMuerto = true;
		_eraJugador = IsInGroup("tropas_jugador");

		if (HasMeta("carril"))
			_carrilNum = ((string)GetMeta("carril"))
				.ToLower().Replace("modrival", "").Replace("mod", "").Trim();

		LiberarSpotOcupado();

		if (IsInGroup("tropas_jugador")) RemoveFromGroup("tropas_jugador");
		if (IsInGroup("tropas_rival"))   RemoveFromGroup("tropas_rival");

		_anim.Play("derrota");

		GetTree().CreateTimer(4.0f).Timeout += AparecerComoFantasma;
	}

	private void LiberarSpotOcupado()
	{
		if (HasMeta("carril"))
		{
			string carril = (string)GetMeta("carril");
			var spot = GetTree().Root.FindChild(carril, true, false);
			if (spot != null) { spot.GetNodeOrNull("Ocupado")?.Free(); return; }
		}
		var padre = GetParent();
		if (padre == null) return;
		var ocupado = padre.GetNodeOrNull<Node>("Ocupado");
		if (ocupado != null) { ocupado.QueueFree(); return; }
		var abuelo = padre.GetParent();
		abuelo?.GetNodeOrNull<Node>("Ocupado")?.QueueFree();
	}

	// ── APARICIÓN COMO FANTASMA ───────────────────────────────────────────────
	private void AparecerComoFantasma()
	{
		if (!IsInstanceValid(this)) return;

		_modoFantasma = true;

		var col = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (col != null) col.Disabled = true;
		Monitorable = false;
		Monitoring  = false;

		var stats = GetNodeOrNull<Control>("StatsTropa");
		if (stats != null) stats.QueueFree();

		Vector2 posRival = EncontrarPosicionRival();
		float offsetX    = _eraJugador ? -80f : 80f;
		GlobalPosition   = posRival + new Vector2(offsetX, 0f);
		ZIndex = 60;

		_anim.Stop();
		_anim.Animation = "respawn_fantasma";
		_anim.Frame     = 0;

		Modulate = new Color(1f, 1f, 1f, 0f);
		Tween tw = CreateTween();
		tw.TweenProperty(this, "modulate:a", 1.0f, 0.4f);
		tw.Finished += () =>
		{
			_anim.Frame = 0;
			_anim.Play("respawn_fantasma");
		};
	}

	private Vector2 EncontrarPosicionRival()
	{
		string grupoRival = _eraJugador ? "tropas_rival" : "tropas_jugador";

		foreach (Node n in GetTree().GetNodesInGroup(grupoRival))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			string c = ((string)e.GetMeta("carril"))
				.ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			if (c == _carrilNum) return e.GlobalPosition;
		}

		string spotName = _eraJugador ? $"ModRival{_carrilNum}" : $"Mod{_carrilNum}";
		var spot = GetTree().Root.FindChild(spotName, true, false);
		if (spot is Node2D s2d) return s2d.GlobalPosition;

		return GlobalPosition + new Vector2(_eraJugador ? 250f : -250f, 0f);
	}

	// ── OBJETIVO FANTASMA / CARRIL ────────────────────────────────────────────
	private Node2D BuscarObjetivoFantasma()
	{
		string grupoRival = _eraJugador ? "tropas_rival" : "tropas_jugador";

		foreach (Node n in GetTree().GetNodesInGroup(grupoRival))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			string c = ((string)e.GetMeta("carril"))
				.ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			if (c == _carrilNum) return e;
		}
		return null;
	}

	// ── FINAL DEL FANTASMA ────────────────────────────────────────────────────
	private void IniciarFinalFantasma(bool victoria)
	{
		if (_finalizando) return;
		_finalizando    = true;
		_fantasmaActivo = false;
		SetProcess(false);
		_anim.Play(victoria ? "victoria_fantasma" : "derrota_fantasma");
	}

	private void FadeOutYLiberar()
	{
		Tween tw = CreateTween();
		tw.TweenProperty(this, "modulate:a", 0f, 0.5f);
		tw.Finished += () => { if (IsInstanceValid(this)) QueueFree(); };
	}

	// ── SEÑAL: CAMBIO DE FRAME ────────────────────────────────────────────────
	private void OnFrameChanged()
	{
		if (_anim == null) return;
		string anim = _anim.Animation.ToString();
		int   frame = _anim.Frame;

		// Frame 4 del ataque en vivo → infligir el daño exacto
		if (anim == "ataque" && frame == 4)
		{
			Node2D obj = _objetivoAtaque ?? BuscarObjetivoEnCarril();
			if (obj != null && IsInstanceValid(obj))
			{
				obj.Call("RecibirDaño", puntosAtaque);
				var campo = GetTree().Root.FindChild("Campo1", true, false);
				if (campo != null) campo.Call("RegistrarDañoTropa", this, puntosAtaque);
			}
		}

		// Frame 19/20 de pre defensa → bucle de espera en postura defensiva
		if (anim == "pre defensa ")
		{
			if (frame == 20)
			{
				_anim.Frame = 19; // Vuelve al frame 19 haciendo el bucle 19 <-> 20
			}
		}

		// Frame 1 del ataque fantasma → infligir 30 dmg al objetivo
		if (anim == "ataque_fantasma" && frame == 1)
		{
			Node2D obj = BuscarObjetivoFantasma();
			if (obj != null && IsInstanceValid(obj))
			{
				obj.Call("RecibirDaño", 30);
				var campo = GetTree().Root.FindChild("Campo1", true, false);
				if (campo != null) campo.Call("RegistrarDañoTropa", this, 30);
			}
		}
	}

	// ── SEÑAL: FIN DE ANIMACIÓN ───────────────────────────────────────────────
	private void OnAnimationFinished()
	{
		if (_anim == null) return;
		string anim = _anim.Animation.ToString();

		switch (anim)
		{
			case "ataque":
				_enDefensa = false;
				if (!_estaMuerto) _anim.Play("idle");
				break;

			case "defensa":
			case "daño":
				// Al terminar la animación de reacción de golpe, vuelve a su posición de reposo
				_enDefensa = false;
				if (!_estaMuerto) _anim.Play("idle");
				break;

			case "respawn_fantasma":
				_fantasmaActivo = true;
				_timerAtaque    = 5.0f;
				_timerVida      = 25.0f;
				SetProcess(true);
				_anim.Play("idle_fantasma");
				break;

			case "ataque_fantasma":
				_atacandoFantasma = false;
				if (!_finalizando) _anim.Play("idle_fantasma");
				break;

			case "victoria_fantasma":
			case "derrota_fantasma":
				FadeOutYLiberar();
				break;
		}
	}
}
