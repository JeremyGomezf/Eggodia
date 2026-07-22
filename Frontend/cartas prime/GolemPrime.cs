using Godot;
using System.Collections.Generic;

/// <summary>Golem — habilidad: da +200 escudo a los 2 aliados más débiles.</summary>
public partial class GolemPrime : TropaBase
{
	public override string Tipo => Tipos.METAL;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 450; escudoActual = escudoMaximo = 500; puntosAtaque = 350; }
		base._Ready();
	}

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
}
