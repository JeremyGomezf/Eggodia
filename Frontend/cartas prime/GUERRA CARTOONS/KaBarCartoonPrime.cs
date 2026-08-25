using Godot;

/// <summary>
/// KaBarCartoonPrime — Soldado fantasma que resucita como espíritu combatiente.
/// 250 HP / 360 ESC / 10 ATQ.
/// El escudo absorbe daño SOLO en postura defensiva explícita.
/// Al morir en combate: 4 s después aparece como fantasma invulnerable en zona rival,
/// ataca cada 5 s (30 dmg), dura 25 s máx o hasta eliminar al rival.
/// </summary>
public partial class KaBarCartoonPrime : TropaBase
{
	public override string Tipo => Tipos.NEUTRO;

	// ── ESTADO ────────────────────────────────────────────────────────────────
	private bool   _enDefensa       = false;  // true solo mientras anima "defensa"
	private bool   _modoFantasma    = false;
	private bool   _fantasmaActivo  = false;
	private bool   _eraJugador      = true;
	private string _carrilNum       = "";
	private float  _timerAtaque     = 5.0f;
	private float  _timerVida       = 25.0f;
	private bool   _atacandoFantasma = false;
	private bool   _finalizando     = false;

	// ══════════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		if (vidaMaxima == 0)
		{
			vidaActual   = vidaMaxima   = 250;
			escudoActual = escudoMaximo = 360;
			puntosAtaque = 10;
		}
		base._Ready();

		_anim.FrameChanged      += OnFrameChanged;
		_anim.AnimationFinished += OnAnimationFinished;
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

	// ── AUTOGESTION DE DAÑO ───────────────────────────────────────────────────
	// Campo1 aplica puntosAtaque (10) en ataques normales.
	// El fantasma gestiona sus 30 dmg directamente en OnFrameChanged.
	public override bool AutogestionaDañoAtaque() => false;

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
				_anim.Play("ataque");
				break;
			case "preparar_defensa":
				_yaActuo = true;
				_anim.Play("pre defensa ");   // espacio final: nombre exacto del sprite sheet
				break;
			case "defender":
				_enDefensa = true;
				_anim.Play("defensa");
				break;
			case "recibir_daño":
				_enDefensa = false;
				_anim.Play("daño");
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
	public new void ReproducirDerrota()
	{
		_estaMuerto = true;
		_anim.Play("derrota");
		// Campo1 gestiona QueueFree vía EjecutarMuerteTropaSacrificada.
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
		// Usar el meta "carril" para encontrar el spot directamente (fiable)
		if (HasMeta("carril"))
		{
			string carril = (string)GetMeta("carril");
			var spot = GetTree().Root.FindChild(carril, true, false);
			if (spot != null) { spot.GetNodeOrNull("Ocupado")?.Free(); return; }
		}
		// Fallback: buscar en jerarquía de padres
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

		// Deshabilitar colisiones: el fantasma es completamente intangible
		var col = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (col != null) col.Disabled = true;
		Monitorable = false;
		Monitoring  = false;

		// Destruir barras de stats: el fantasma no tiene UI de combate
		var stats = GetNodeOrNull<Control>("StatsTropa");
		if (stats != null) stats.QueueFree();

		// Reposicionar en la zona rival del mismo carril
		Vector2 posRival = EncontrarPosicionRival();
		float offsetX    = _eraJugador ? -80f : 80f;
		GlobalPosition   = posRival + new Vector2(offsetX, 0f);
		ZIndex = 60;

		// Fijar frame 0 de respawn_fantasma ANTES del fade-in para evitar
		// que se vean fotogramas de la animación "derrota"
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

		// Fallback: spot por nombre en el árbol de escena
		string spotName = _eraJugador ? $"ModRival{_carrilNum}" : $"Mod{_carrilNum}";
		var spot = GetTree().Root.FindChild(spotName, true, false);
		if (spot is Node2D s2d) return s2d.GlobalPosition;

		return GlobalPosition + new Vector2(_eraJugador ? 250f : -250f, 0f);
	}

	// ── OBJETIVO FANTASMA ─────────────────────────────────────────────────────
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
		string anim  = (string)_anim.Animation;
		int    frame = _anim.Frame;

		// Frame 1 del ataque fantasma → infligir 30 dmg al objetivo
		if (anim == "ataque_fantasma" && frame == 1)
		{
			Node2D obj = BuscarObjetivoFantasma();
			if (obj != null && IsInstanceValid(obj))
				obj.Call("RecibirDaño", 30);
		}
	}

	// ── SEÑAL: FIN DE ANIMACIÓN ───────────────────────────────────────────────
	private void OnAnimationFinished()
	{
		string anim = (string)_anim.Animation;

		switch (anim)
		{
			case "ataque":
				_enDefensa = false;
				if (!_estaMuerto) _anim.Play("idle");
				break;

			case "daño":
				_enDefensa = false;
				if (!_estaMuerto) _anim.Play("idle");
				break;

			case "pre defensa ":
				if (!_estaMuerto) { _enDefensa = true; _anim.Play("defensa"); }
				break;

			case "respawn_fantasma":
				_fantasmaActivo  = true;
				_timerAtaque     = 5.0f;
				_timerVida       = 25.0f;
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
