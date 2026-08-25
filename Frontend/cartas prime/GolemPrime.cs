using Godot;
using System.Collections.Generic;

/// <summary>
/// Gólem — daño dividido en dos oleadas: frame 2 (50%) + frame 4 (50%).
/// Si el frame 2 agota el escudo del rival, el frame 4 golpea directo a la vida.
/// Habilidad: +200 escudo a los 2 aliados más débiles.
/// </summary>
public partial class GolemPrime : TropaBase
{
	public override string Tipo => Tipos.METAL;

	private Node2D _objetivo;
	private bool   _golpeF2Disparado = false;
	private bool   _golpeF4Disparado = false;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 450; escudoActual = escudoMaximo = 500; puntosAtaque = 350; }
		base._Ready();
		_anim.FrameChanged += OnFrameChanged;
	}

	// ── CAPACIDADES ────────────────────────────────────────────────────────────
	public override bool AutogestionaDañoAtaque() => true;

	// ── ATAQUE: daño en frames 2 y 4 ──────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (accion == "atacar")
		{
			_yaActuo            = true;
			_objetivo           = BuscarObjetivoEnCarril();
			_golpeF2Disparado   = false;
			_golpeF4Disparado   = false;
			ReproducirAtaque();
			return;
		}
		base.EjecutarAccion(accion);
	}

	private void OnFrameChanged()
	{
		string anim = (string)_anim.Animation;
		if (anim != "ataque") return;

		int hit = puntosAtaque / 2;

		if (_anim.Frame == 2 && !_golpeF2Disparado)
		{
			_golpeF2Disparado = true;
			if (_objetivo != null && IsInstanceValid(_objetivo))
				_objetivo.Call("RecibirDaño", hit);
		}

		if (_anim.Frame == 4 && !_golpeF4Disparado)
		{
			_golpeF4Disparado = true;
			if (_objetivo != null && IsInstanceValid(_objetivo))
				_objetivo.Call("RecibirDaño", hit);
		}
	}

	// ── HABILIDAD: +200 escudo a los 2 aliados más débiles ────────────────────
	protected override void UsarHabilidadPropia()
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

		Tween tw = CreateTween();
		tw.TweenProperty(this, "modulate", new Color(0.7f, 0.7f, 0.9f), 0.15f);
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
			int nuevo    = escActual + 200;
			int nuevoMax = Mathf.Max(escMax, nuevo);
			try { a.Set("escudoActual", nuevo);    } catch { }
			try { a.Set("escudoMaximo", nuevoMax); } catch { }

			var stats = a.GetNodeOrNull<Control>("StatsTropa");
			if (stats != null)
			{
				stats.Visible = true;
				var be = stats.GetNodeOrNull<ProgressBar>("BarraEscudo");
				if (be != null && nuevoMax > 0) be.Value = (float)nuevo / nuevoMax * 100;
			}
			Tween ta = a.CreateTween();
			ta.TweenProperty(a, "modulate", new Color(1.2f, 1f, 0.4f), 0.2f);
			ta.TweenProperty(a, "modulate", Colors.White, 0.4f);
			rocas++;
		}
		habilidadUsada = true;
	}

	// ── HELPER ─────────────────────────────────────────────────────────────────
	private Node2D BuscarObjetivoEnCarril()
	{
		if (!HasMeta("carril")) return null;
		string grupo    = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		string miCarril = ((string)GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
		Node2D mejor = null; int min = int.MaxValue;
		foreach (Node n in GetTree().GetNodesInGroup(grupo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			string c = ((string)e.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			if (c != miCarril) continue;
			int v = 0; try { v = (int)e.Get("vidaActual"); } catch { }
			if (v > 0 && v < min) { min = v; mejor = e; }
		}
		return mejor;
	}
}
