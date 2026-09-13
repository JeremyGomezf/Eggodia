using Godot;

/// <summary>
/// Machi — Hechicera.
/// </summary>
public partial class MachiPrime : TropaBase
{
	public override string Tipo => Tipos.METAL;
	protected override int TurnoDesbloqueoHabilidad => 2;

	private Node2D _objetivo;
	private Tween _tweenFlotacion;
	private Vector2 _offsetSpriteOriginal;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 200; escudoActual = escudoMaximo = 100; puntosAtaque = 180; }
		base._Ready();

		if (_anim != null)
		{
			// Guardar el offset visual original del Sprite para no desencajar las barras de vida
			_offsetSpriteOriginal = _anim.Position;

			_anim.Connect(AnimatedSprite2D.SignalName.FrameChanged, Callable.From(OnFrameChanged));
			_anim.Connect(AnimatedSprite2D.SignalName.AnimationFinished, Callable.From(OnAnimationFinished));
		}

		IniciarFlotacionIdle();
	}

	// ── CAPACIDADES ────────────────────────────────────────────────────────────
	public override bool AutogestionaDañoAtaque() => true;
	public override bool TieneHabilidadEspecial() => true; // Curación en área (mitad de vida máxima) a todos los aliados

	// ── ACCIONES ───────────────────────────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (_estaMuerto) return;

		if (accion == "atacar")
		{
			PausarFlotacion();
			_yaActuo = true;
			_objetivo = BuscarObjetivoEnCarril();
			_anim.Play("ataque");
			return;
		}
		base.EjecutarAccion(accion);
	}

	// HABILIDAD (1 vez por partida): Machi cura a TODOS los aliados vivos —incluida ella misma—
	// la MITAD de su vida máxima. La curación se aplica en el frame 3 de la animación "habilidad"
	// (ver OnFrameChanged) y cada tropa curada muestra el efecto verde.
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada || _estaMuerto || _anim == null) return;
		habilidadUsada = true;
		PausarFlotacion();
		_yaActuo = true;
		_anim.Play("habilidad");
	}

	private void CurarAliados()
	{
		string grupo = IsInGroup("tropas_rival") ? "tropas_rival" : "tropas_jugador";
		var campo = GetTree().Root.FindChild("Campo1", true, false) as Campo1;

		foreach (Node n in GetTree().GetNodesInGroup(grupo))
		{
			if (n is not TropaBase aliado || !IsInstanceValid(aliado)) continue;
			if (aliado.vidaActual <= 0) continue; // saltar tropas muertas / en proceso de morir

			int cura       = aliado.vidaMaxima / 2;                       // mitad de la vida TOTAL
			int nueva      = Mathf.Min(aliado.vidaMaxima, aliado.vidaActual + cura); // sin sobrecurar
			int curadoReal = nueva - aliado.vidaActual;
			if (curadoReal <= 0) continue;

			aliado.vidaActual = nueva;
			aliado.RefrescarBarras();

			// Efecto verde: número flotante "+N" y flash verde sobre la tropa curada.
			campo?.MostrarDañoFlotante(aliado.GlobalPosition, curadoReal, true);
			Tween tw = aliado.CreateTween();
			tw.TweenProperty(aliado, "modulate", new Color(0.3f, 1.6f, 0.5f), 0.2f);
			tw.TweenProperty(aliado, "modulate", Colors.White, 0.5f);
		}
	}

	// ── LEVITACIÓN CÍRCULO MINÚSCULO Y LENTO (SOLO VISUAL) ─────────────────────
	private void IniciarFlotacionIdle()
	{
		if (_anim == null) return;

		_tweenFlotacion?.Kill();
		_tweenFlotacion = CreateTween().SetLoops();

		// Micro-desplazamiento de solo 1.5 píxeles (casi imperceptible)
		float radioX = 1.5f;
		float radioY = 1.2f;
		float duracionPaso = 1.0f; // Súper despacio para que no vibre

		// Solo animamos la posición local del AnimatedSprite2D, no la tropa completa ni las barras de vida
		_tweenFlotacion.TweenProperty(_anim, "position", _offsetSpriteOriginal + new Vector2(0, -radioY), duracionPaso)
					   .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		_tweenFlotacion.TweenProperty(_anim, "position", _offsetSpriteOriginal + new Vector2(radioX, 0), duracionPaso)
					   .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		_tweenFlotacion.TweenProperty(_anim, "position", _offsetSpriteOriginal + new Vector2(0, radioY), duracionPaso)
					   .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		_tweenFlotacion.TweenProperty(_anim, "position", _offsetSpriteOriginal + new Vector2(-radioX, 0), duracionPaso)
					   .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	private void PausarFlotacion()
	{
		_tweenFlotacion?.Kill();
		if (_anim != null)
		{
			// Restaura la posición original del sprite exactamente
			_anim.Position = _offsetSpriteOriginal;
		}
	}

	// ── RECIBIR DAÑO ──────────────────────────────────────────────────────────
	public override void RecibirDaño(int cantidad)
	{
		if (_estaMuerto) return;
		PausarFlotacion();
		base.RecibirDaño(cantidad);
	}

	// ── EVENTO: FRAME CHANGED ──────────────────────────────────────────────────
	private void OnFrameChanged()
	{
		if (_anim == null) return;
		string anim = _anim.Animation.ToString();

		// Daño administrado exactamente en el Frame 1 de ataque
		if (anim == "ataque" && _anim.Frame == 1)
		{
			if (_objetivo != null && IsInstanceValid(_objetivo))
			{
				int danioFinal = puntosAtaque;
				_objetivo.Call("RecibirDaño", danioFinal);
				var campo = GetTree().Root.FindChild("Campo1", true, false);
				if (campo != null) campo.Call("RegistrarDañoTropa", this, danioFinal);
			}
		}

		// Curación en área aplicada exactamente en el Frame 3 de la habilidad
		if (anim == "habilidad" && _anim.Frame == 3)
			CurarAliados();
	}

	// ── EVENTO: FIN DE ANIMACIÓN ───────────────────────────────────────────────
	private void OnAnimationFinished()
	{
		if (_anim == null) return;
		string anim = _anim.Animation.ToString();

		if (anim == "ataque" || anim == "daño" || anim == "defensa" || anim == "habilidad")
		{
			if (!_estaMuerto)
			{
				_anim.Play("idle");
				IniciarFlotacionIdle();
			}
		}
	}

	public override void TickHabilidad()
	{
		// Sin lógica de habilidad activa por el momento
	}
}
