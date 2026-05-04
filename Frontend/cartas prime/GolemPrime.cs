using Godot;
using System;
using System.Collections.Generic;

public partial class GolemPrime : Area2D
{
	private AnimatedSprite2D _anim;
	private bool _estaMuerto = false;
	private bool _yaActuo = false;

	[Export] public int vidaActual  = 450;
	[Export] public int vidaMaxima  = 450;
	[Export] public int escudoActual = 500;
	[Export] public int escudoMaximo = 500;
	[Export] public int puntosAtaque = 350;

	// ── HABILIDAD: Rocas escudo ───────────────────────────────────────────
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

	// ── HABILIDAD: Rocas escudo a 2 aliados ──────────────────────────────
	private void UsarHabilidad()
	{
		if (habilidadUsada) return;

		string grupoAliado = IsInGroup("tropas_jugador") ? "tropas_jugador" : "tropas_rival";
		var aliados = new List<(Node2D n, float pct)>();

		foreach (Node n in GetTree().GetNodesInGroup(grupoAliado))
		{
			if (!(n is Node2D a) || !IsInstanceValid(a) || a == this) continue;
			int esc = 0, escMax = 1;
			try { esc    = (int)a.Get("escudoActual"); } catch { }
			try { escMax = (int)a.Get("escudoMaximo"); if (escMax <= 0) escMax = 1; } catch { }
			aliados.Add((a, (float)esc / escMax));
		}
		aliados.Sort((a, b) => a.pct.CompareTo(b.pct));

		// Golpe de tierra visual
		Tween tw = CreateTween();
		tw.TweenProperty(this, "modulate", new Color(0.7f,0.7f,0.9f), 0.15f);
		tw.TweenProperty(this, "modulate", Colors.White, 0.3f);
		Tween sc = CreateTween();
		sc.TweenProperty(this, "scale", Scale * new Vector2(1.2f, 0.8f), 0.15f);
		sc.TweenProperty(this, "scale", Scale, 0.25f);

		int rocas = 0;
		foreach (var (a, _) in aliados)
		{
			if (rocas >= 2) break;
			int escActual = 0, escMax = 0;
			try { escActual = (int)a.Get("escudoActual"); } catch { }
			try { escMax    = (int)a.Get("escudoMaximo"); } catch { }
			int nuevo = escActual + 200;
			int nuevoMax = Mathf.Max(escMax, nuevo);
			try { a.Set("escudoActual", nuevo); }    catch { }
			try { a.Set("escudoMaximo", nuevoMax); } catch { }

			// Refrescar barra de escudo visualmente
			var stats = a.GetNodeOrNull<Control>("StatsTropa");
			if (stats != null)
			{
				stats.Visible = true;
				var be = stats.GetNodeOrNull<ProgressBar>("BarraEscudo");
				if (be != null && nuevoMax > 0) be.Value = (float)nuevo / nuevoMax * 100;
			}

			// Visual del aliado: destello dorado
			Tween ta = a.CreateTween();
			ta.TweenProperty(a, "modulate", new Color(1.2f,1f,0.4f), 0.2f);
			ta.TweenProperty(a, "modulate", Colors.White, 0.4f);
			rocas++;
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
