using Godot;

public partial class TiburonPrime : TropaBase
{
	private bool    _mordidaCargada;
	private int     _ataqueOriginal;
	private Vector2 _escalaOrig;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 300; escudoActual = escudoMaximo = 200; puntosAtaque = 230; }
		base._Ready();
	}

	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada) return;
		_mordidaCargada = true;
		_ataqueOriginal = puntosAtaque;
		_escalaOrig     = Scale;
		puntosAtaque   *= 2;

		Tween tw = CreateTween();
		tw.TweenProperty(this, "modulate", new Color(1.5f, 0.3f, 0.3f), 0.15f);
		tw.TweenProperty(this, "modulate", new Color(1.2f, 0.6f, 0.6f), 0.3f);

		Tween sc = CreateTween();
		sc.TweenProperty(this, "scale", _escalaOrig * 1.15f, 0.2f).SetTrans(Tween.TransitionType.Back);
		sc.TweenProperty(this, "scale", _escalaOrig, 0.15f);

		habilidadUsada = true;
	}

	public override void EjecutarAccion(string accion)
	{
		if (accion == "atacar" && _mordidaCargada)
		{
			_mordidaCargada = false;
			base.EjecutarAccion(accion);
			puntosAtaque = _ataqueOriginal;
			Modulate = Colors.White;
			return;
		}
		base.EjecutarAccion(accion);
	}
}
