using Godot;

/// <summary>Maguín — daño de ataque en frame 2. Habilidad: 100 daño de hielo + bloqueo 1 turno al carril.</summary>
public partial class MaguinPrime : TropaBase
{
	public override string Tipo => Tipos.AGUA;
	protected override int TurnoDesbloqueoHabilidad => 3;

	private Node2D _objetivo;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 200; escudoActual = escudoMaximo = 220; puntosAtaque = 250; }
		base._Ready();
		_anim.FrameChanged += OnFrameChanged;
	}

	// ── CAPACIDADES ────────────────────────────────────────────────────────────
	public override bool AutogestionaDañoAtaque() => true;

	// ── ATAQUE: daño en frame 2 ────────────────────────────────────────────────
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
		if ((string)_anim.Animation == "ataque" && _anim.Frame == 2)
		{
			if (_objetivo != null && IsInstanceValid(_objetivo))
			{
				_objetivo.Call("RecibirDaño", puntosAtaque);
				var campo = GetTree().Root.FindChild("Campo1", true, false);
				if (campo != null) campo.Call("RegistrarDañoTropa", this, puntosAtaque);
			}
		}
	}

	// ── HABILIDAD: ventisca al carril ──────────────────────────────────────────
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada) return;
		if (!HasMeta("carril")) return;

		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		string miCarril = ((string)GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();

		Node2D obj = null;
		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (n is Node2D e && IsInstanceValid(e) && e.HasMeta("carril"))
			{
				string c = ((string)e.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
				if (c == miCarril) { obj = e; break; }
			}
		}

		Tween tw = CreateTween();
		tw.TweenProperty(this, "modulate", new Color(0.5f, 0.8f, 2f), 0.2f);
		tw.TweenProperty(this, "modulate", Colors.White, 0.4f);

		if (obj != null)
		{
			obj.Call("RecibirDaño", 100);
			var campoHab = GetTree().Root.FindChild("Campo1", true, false);
			if (campoHab != null) campoHab.Call("RegistrarDañoTropa", this, 100);
			obj.SetMeta("bloqueado", true);
			obj.SetMeta("turnosBloqueo", 1);
			Tween te = obj.CreateTween();
			te.TweenProperty(obj, "modulate", new Color(0.4f, 0.7f, 1.5f), 0.2f);
			Vector2 orig = obj.Position;
			Tween sh = obj.CreateTween();
			sh.TweenProperty(obj, "position", orig + new Vector2(5, 0), 0.04f);
			sh.TweenProperty(obj, "position", orig - new Vector2(5, 0), 0.04f);
			sh.TweenProperty(obj, "position", orig, 0.04f);
		}

		habilidadUsada = true;
	}
}
