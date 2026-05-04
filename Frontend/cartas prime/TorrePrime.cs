using Godot;
using System;

public partial class TorrePrime : Area2D
{
	private AnimatedSprite2D _anim;
	private bool _estaMuerto = false;
	private bool _yaActuo = false;

	[Export] public int vidaActual  = 500;
	[Export] public int vidaMaxima  = 500;
	[Export] public int escudoActual = 450;
	[Export] public int escudoMaximo = 450;
	[Export] public int puntosAtaque = 350;

	// ── HABILIDAD: Forma Gigante ───────────────────────────────────────────
	public bool habilidadUsada    = false;
	private bool _habilidadActiva = false;
	private int  _turnosHabilidad = 0;
	private int  _ataqueOrig, _escudoMaxOrig;
	private Vector2 _escalaOrig;

	private Control _contenedorStats;

	public override void _Ready()
	{
		_anim = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_contenedorStats = GetNodeOrNull<Control>("StatsTropa");
		if (_contenedorStats != null) _contenedorStats.Visible = false;
		ReproducirIdle();
	}

	public override void _InputEvent(Viewport viewport, InputEvent @event, int shapeIdx)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			if (IsInGroup("tropas_rival")) { MostrarBarras(true); return; }
			MostrarBarras(true);
			if (_estaMuerto || _yaActuo) return;
			var campo = GetTree().Root.FindChild("Campo1", true, false) as Campo1;
			if (campo != null) campo.MostrarMenuTropa(this);
		}
	}

	public void SetActivo(bool estado) { _yaActuo = !estado; if (estado) MostrarBarras(false); }

	public void RecibirDaño(int cantidad)
	{
		if (_estaMuerto) return;
		if (_anim.Animation == "pre defensa")
		{
			EjecutarAccion("defender");
			cantidad = (int)(cantidad * 0.5f);
			if (escudoActual > 0)
			{
				if (cantidad <= escudoActual) { escudoActual -= cantidad; cantidad = 0; }
				else { cantidad -= escudoActual; escudoActual = 0; }
			}
		}
		if (cantidad > 0) vidaActual -= cantidad;
		ActualizarBarrasUI();
		if (vidaActual <= 0)
		{
			var campo = GetTree().Root.FindChild("Campo1", true, false);
			if (campo != null) campo.Call("EjecutarMuerteTropaSacrificada", this);
		}
		else if (_anim.Animation != "defensa") EjecutarAccion("recibir_daño");
	}

	public void EjecutarAccion(string accion)
	{
		if (_estaMuerto) return;
		switch (accion)
		{
			case "atacar":          _yaActuo = true; ReproducirAtaque();    break;
			case "preparar_defensa":_yaActuo = true; ReproducirPreDefensa();break;
			case "recibir_daño":    ReproducirDaño();    break;
			case "defender":        ReproducirDefensa(); break;
			case "usar_habilidad":  UsarHabilidad();     break;
		}
	}

	// ── HABILIDAD: Torre Gigante ──────────────────────────────────────────
	private void UsarHabilidad()
	{
		if (habilidadUsada) return;
		_ataqueOrig    = puntosAtaque;
		_escudoMaxOrig = escudoMaximo;
		_escalaOrig    = Scale;

		puntosAtaque = _ataqueOrig * 2;
		escudoMaximo = _escudoMaxOrig * 2;
		escudoActual = Mathf.Min(escudoActual * 2, escudoMaximo);

		// Visual: crecer
		Tween tw = CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
		tw.TweenProperty(this, "scale", _escalaOrig * 1.6f, 0.45f);
		Tween tw2 = CreateTween();
		tw2.TweenProperty(this, "modulate", new Color(1.4f, 1.1f, 0.2f), 0.3f);
		tw2.TweenProperty(this, "modulate", Colors.White, 0.5f);

		habilidadUsada  = true;
		_habilidadActiva = true;
		_turnosHabilidad = 2;
		ActualizarBarrasUI();
	}

	// Llamado por Campo1.ProcesarStatusEfectos cada turno
	public void TickHabilidad()
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

	// ── ANIMACIONES ───────────────────────────────────────────────────────
	public void ReproducirIdle() { if (!_estaMuerto) _anim.Play("idle"); }

	public async void ReproducirAtaque()
	{
		if (_estaMuerto) return;
		_anim.Play("ataque");
		await ToSignal(_anim, "animation_finished");
		ReproducirIdle();
	}

	public void ReproducirPreDefensa() { if (!_estaMuerto) _anim.Play("pre defensa"); }

	public async void ReproducirDefensa()
	{
		if (_estaMuerto) return;
		_anim.Play("defensa");
		await ToSignal(_anim, "animation_finished");
		ReproducirIdle();
	}

	public async void ReproducirDaño()
	{
		if (_estaMuerto) return;
		_anim.Play("daño");
		await ToSignal(_anim, "animation_finished");
		ReproducirIdle();
	}

	public bool TieneHabilidadEspecial() => true;

	public void ReproducirDerrota() { _estaMuerto = true; _anim.Play("derrota"); }

	private void MostrarBarras(bool mostrar)
	{
		if (_contenedorStats != null) { _contenedorStats.Visible = mostrar; ActualizarBarrasUI(); }
	}

	private void ActualizarBarrasUI()
	{
		var bv = _contenedorStats?.GetNodeOrNull<ProgressBar>("BarraVida");
		var be = _contenedorStats?.GetNodeOrNull<ProgressBar>("BarraEscudo");
		if (bv != null) bv.Value = (float)vidaActual / vidaMaxima * 100;
		if (be != null) be.Value = escudoMaximo > 0 ? (float)escudoActual / escudoMaximo * 100 : 0;
	}
}
