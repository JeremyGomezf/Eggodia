using Godot;

public partial class PeonPrime : TropaBase
{
	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 150; escudoActual = escudoMaximo = 150; puntosAtaque = 100; }
		base._Ready();
	}

	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada) return;

		string grupoAliado = IsInGroup("tropas_jugador") ? "tropas_jugador" : "tropas_rival";
		Node2D masDebil = null;
		int minVida = int.MaxValue;

		foreach (Node n in GetTree().GetNodesInGroup(grupoAliado))
		{
			if (!(n is Node2D a) || !IsInstanceValid(a) || a == this) continue;
			int vida = 0, vidaMax = 1;
			try { vida    = (int)a.Get("vidaActual"); } catch { continue; }
			try { vidaMax = (int)a.Get("vidaMaxima"); } catch { continue; }
			if (vida < vidaMax && vida < minVida) { minVida = vida; masDebil = a; }
		}

		if (masDebil == null) return;

		int vidaMaxAliado = 0;
		try { vidaMaxAliado = (int)masDebil.Get("vidaMaxima"); } catch { return; }
		try { masDebil.Set("vidaActual", vidaMaxAliado); } catch { return; }

		Tween th = masDebil.CreateTween();
		th.TweenProperty(masDebil, "modulate", new Color(0.3f, 2f, 0.5f), 0.3f);
		th.TweenProperty(masDebil, "modulate", Colors.White, 0.5f);

		var stats = masDebil.GetNodeOrNull<Control>("StatsTropa");
		if (stats != null)
		{
			stats.Visible = true;
			var bv = stats.GetNodeOrNull<ProgressBar>("BarraVida");
			if (bv != null) bv.Value = 100;
		}

		habilidadUsada = true;
		var campo = GetTree().Root.FindChild("Campo1", true, false);
		if (campo != null) campo.Call("EjecutarMuerteTropaSacrificada", this);
	}
}
