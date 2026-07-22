using Godot;

public partial class SoldadoRealPrime : TropaBase
{
	public override string Tipo => Tipos.METAL;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 250; escudoActual = escudoMaximo = 300; puntosAtaque = 150; }
		base._Ready();
	}

	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada) return;

		string grupoAliado = IsInGroup("tropas_jugador") ? "tropas_jugador" : "tropas_rival";

		escudoActual += 150;
		escudoMaximo = Mathf.Max(escudoMaximo, escudoActual);
		ActualizarBarrasUI();

		Tween tw = CreateTween();
		tw.TweenProperty(this, "modulate", new Color(0.7f, 0.8f, 1.5f), 0.2f);
		tw.TweenProperty(this, "modulate", Colors.White, 0.4f);

		foreach (Node n in GetTree().GetNodesInGroup(grupoAliado))
		{
			if (!(n is Node2D a) || !IsInstanceValid(a) || a == this) continue;
			int esc = 0, escMax = 0;
			try { esc = (int)a.Get("escudoActual"); } catch { continue; }
			try { escMax = (int)a.Get("escudoMaximo"); } catch { continue; }
			int nuevoEsc = esc + 150;
			int nuevoMax = Mathf.Max(escMax, nuevoEsc);
			try { a.Set("escudoActual", nuevoEsc); } catch { }
			try { a.Set("escudoMaximo", nuevoMax); } catch { }
			var stats = a.GetNodeOrNull<Control>("StatsTropa");
			if (stats != null)
			{
				stats.Visible = true;
				var be = stats.GetNodeOrNull<ProgressBar>("BarraEscudo");
				if (be != null && nuevoMax > 0) be.Value = (float)nuevoEsc / nuevoMax * 100;
			}
			Tween ta = a.CreateTween();
			ta.TweenProperty(a, "modulate", new Color(0.7f, 0.8f, 1.5f), 0.2f);
			ta.TweenProperty(a, "modulate", Colors.White, 0.4f);
		}

		habilidadUsada = true;
	}
}
