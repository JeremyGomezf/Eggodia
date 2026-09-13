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
	public override bool TieneHabilidadEspecial() => false; // Habilidad pendiente por implementar

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

	protected override void UsarHabilidadPropia()
	{
		// Habilidad vacía por el momento
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
	}

	// ── EVENTO: FIN DE ANIMACIÓN ───────────────────────────────────────────────
	private void OnAnimationFinished()
	{
		if (_anim == null) return;
		string anim = _anim.Animation.ToString();

		if (anim == "ataque" || anim == "daño" || anim == "defensa")
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
