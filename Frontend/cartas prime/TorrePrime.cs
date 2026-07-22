using Godot;

/// <summary>Torre — habilidad: Forma Gigante por 2 turnos (x2 ataque y escudo, x1.6 tamaño).</summary>
public partial class TorrePrime : TropaBase
{
	public override string Tipo => Tipos.METAL;

	private bool    _habilidadActiva = false;
	private int     _turnosHabilidad = 0;
	private int     _ataqueOrig, _escudoMaxOrig;
	private Vector2 _escalaOrig;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 500; escudoActual = escudoMaximo = 450; puntosAtaque = 350; }
		base._Ready();
	}

	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada) return;
		_ataqueOrig    = puntosAtaque;
		_escudoMaxOrig = escudoMaximo;
		_escalaOrig    = Scale;

		puntosAtaque = _ataqueOrig * 2;
		escudoMaximo = _escudoMaxOrig * 2;
		escudoActual = Mathf.Min(escudoActual * 2, escudoMaximo);

		Tween tw = CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
		tw.TweenProperty(this, "scale", _escalaOrig * 1.6f, 0.45f);
		Tween tw2 = CreateTween();
		tw2.TweenProperty(this, "modulate", new Color(1.4f, 1.1f, 0.2f), 0.3f);
		tw2.TweenProperty(this, "modulate", Colors.White, 0.5f);

		habilidadUsada   = true;
		_habilidadActiva = true;
		_turnosHabilidad = 2;
		ActualizarBarrasUI();
	}

	/// <summary>Llamado por Campo1.ProcesarStatusEfectos para desactivar la forma gigante.</summary>
	public override void TickHabilidad()
	{
		if (!_habilidadActiva) return;
		_turnosHabilidad--;
		if (_turnosHabilidad <= 0)
		{
			puntosAtaque = _ataqueOrig;
			escudoMaximo = _escudoMaxOrig;
			if (escudoActual > escudoMaximo) escudoActual = escudoMaximo;
			Tween tw = CreateTween().SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Back);
			tw.TweenProperty(this, "scale", _escalaOrig, 0.4f);
			_habilidadActiva = false;
		}
	}
}
