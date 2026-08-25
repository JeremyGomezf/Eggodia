using Godot;

/// <summary>Dragón de Flama — daño de ataque en frame 3. Habilidad: aliento AoE al 40% del ATQ.</summary>
public partial class DragonPrime : TropaBase
{
	public override string Tipo => Tipos.FUEGO;

	private Node2D _objetivo;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 350; escudoActual = escudoMaximo = 250; puntosAtaque = 280; }
		base._Ready();
		_anim.FrameChanged += OnFrameChanged;
	}

	// ── CAPACIDADES ────────────────────────────────────────────────────────────
	public override bool AutogestionaDañoAtaque() => true;

	// ── ATAQUE: daño en frame 3 ────────────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (accion == "atacar")
		{
			_yaActuo = true;
			_objetivo = BuscarObjetivoEnCarril();
			ReproducirAtaque();
			return;
		}
		base.EjecutarAccion(accion);
	}

	private void OnFrameChanged()
	{
		if ((string)_anim.Animation == "ataque" && _anim.Frame == 3)
		{
			if (_objetivo != null && IsInstanceValid(_objetivo))
				_objetivo.Call("RecibirDaño", puntosAtaque);
		}
	}

	// ── HABILIDAD: aliento AoE 40% ─────────────────────────────────────────────
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada) return;

		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		int dañoAoE = (int)(puntosAtaque * 0.4f);

		Tween tw = CreateTween();
		tw.TweenProperty(this, "modulate", new Color(2f, 0.5f, 0.1f), 0.2f);
		tw.TweenProperty(this, "modulate", Colors.White, 0.4f);
		Tween sc = CreateTween();
		sc.TweenProperty(this, "scale", Scale * 1.15f, 0.15f);
		sc.TweenProperty(this, "scale", Scale, 0.2f);

		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (n is Node2D e && IsInstanceValid(e))
			{
				e.Call("RecibirDaño", dañoAoE);
				Tween tf = e.CreateTween();
				tf.TweenProperty(e, "modulate", new Color(2f, 0.3f, 0.0f), 0.15f);
				tf.TweenProperty(e, "modulate", Colors.White, 0.3f);
			}
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
