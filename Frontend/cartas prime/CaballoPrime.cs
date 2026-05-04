using Godot;
using System;
using System.Collections.Generic;

public partial class CaballoPrime : Area2D
{
	private AnimatedSprite2D _anim;
	private bool _estaMuerto = false;
	private bool _yaActuo = false;

	[Export] public int vidaActual  = 230;
	[Export] public int vidaMaxima  = 230;
	[Export] public int escudoActual = 250;
	[Export] public int escudoMaximo = 250;
	[Export] public int puntosAtaque = 200;

	// ── HABILIDAD: Ataque en L ────────────────────────────────────────────
	public bool habilidadUsada = false;

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
			case "atacar":           _yaActuo = true; ReproducirAtaque();    break;
			case "preparar_defensa": _yaActuo = true; ReproducirPreDefensa();break;
			case "recibir_daño":     ReproducirDaño();    break;
			case "defender":         ReproducirDefensa(); break;
			case "usar_habilidad":   UsarHabilidad();     break;
		}
	}

	// ── HABILIDAD: Salto en L ─────────────────────────────────────────────
	private void UsarHabilidad()
	{
		if (habilidadUsada || !HasMeta("carril")) return;

		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		string miCarril = ((string)GetMeta("carril")).ToLower().Replace("modrival","").Replace("mod","");

		var carrilesL = new List<string>();
		switch (miCarril)
		{
			case "1": carrilesL.Add("2"); carrilesL.Add("3"); break;
			case "2": carrilesL.Add("1"); carrilesL.Add("3"); break;
			case "3": carrilesL.Add("1"); carrilesL.Add("2"); break;
		}

		int dañoDoble = puntosAtaque * 2;

		// Visual: salto
		Tween tw = CreateTween();
		tw.TweenProperty(this, "position:y", Position.Y - 45f, 0.2f);
		tw.TweenProperty(this, "position:y", Position.Y,       0.2f);

		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			string id = ((string)e.GetMeta("carril")).ToLower().Replace("modrival","").Replace("mod","");
			if (!carrilesL.Contains(id)) continue;
			if (e.HasMethod("RecibirDaño")) e.Call("RecibirDaño", dañoDoble);
		}

		habilidadUsada = true;
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
