using Godot;

/// <summary>
/// Soldado Real — Habilidad: Parada (contra-ataque).
/// Al activar: animación "pre defensa" + aura amarilla.
/// Si lo atacan con parada activa → 0 daño + 300 contra al atacante.
/// El aura desaparece tras el primer contra o al fin del siguiente turno.
/// Daño de ataque propio en frame 1 de "ataque".
/// </summary>
public partial class SoldadoRealPrime : TropaBase
{
	public override string Tipo => Tipos.METAL;

	private bool   _enParry         = false;
	private int    _parryTicksRestantes = 0;
	private Node2D _objetivo;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 250; escudoActual = escudoMaximo = 300; puntosAtaque = 150; }
		base._Ready();
		_anim.FrameChanged += OnFrameChanged;
	}

	// ── CAPACIDADES ────────────────────────────────────────────────────────────
	public override bool AutogestionaDañoAtaque() => true;
	public override bool TieneHabilidadEspecial() => true;

	// ── ATAQUE: daño en frame 1 ────────────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (accion == "atacar")
		{
			_enParry = false;
			_parryTicksRestantes = 0;
			Modulate = Colors.White;
			_yaActuo = true;
			_objetivo = BuscarObjetivoEnCarril();
			ReproducirAtaque();
			return;
		}
		base.EjecutarAccion(accion);
	}

	private void OnFrameChanged()
	{
		if ((string)_anim.Animation == "ataque" && _anim.Frame == 1)
		{
			if (_objetivo != null && IsInstanceValid(_objetivo))
				_objetivo.Call("RecibirDaño", puntosAtaque);
		}
	}

	// ── HABILIDAD: PARADA ──────────────────────────────────────────────────────
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada || _enParry) return;
		habilidadUsada       = true;
		_yaActuo             = true;
		_enParry             = true;
		_parryTicksRestantes = 2;  // expira tras 2 cambios de turno si no lo atacan

		_anim.Play("pre defensa");
		Modulate = new Color(2f, 1.8f, 0.3f, 1f);
	}

	// ── RECIBIR DAÑO: PARADA ──────────────────────────────────────────────────
	public override void RecibirDaño(int cantidad)
	{
		if (_estaMuerto) return;

		if (_enParry)
		{
			_enParry             = false;
			_parryTicksRestantes = 0;
			Modulate             = Colors.White;

			// 0 daño recibido; contra-ataque 300 al atacante del mismo carril
			Node2D atacante = BuscarAtacanteEnCarril();
			if (atacante != null && IsInstanceValid(atacante))
				atacante.Call("RecibirDaño", 300);

			EjecutarAccion("defender");
			return;
		}

		base.RecibirDaño(cantidad);
	}

	// ── TICK: expirar parada si no la usaron ──────────────────────────────────
	public override void TickHabilidad()
	{
		if (!_enParry) return;
		_parryTicksRestantes--;
		if (_parryTicksRestantes <= 0)
		{
			_enParry = false;
			Modulate = Colors.White;
			if (!_estaMuerto) _anim.Play("idle");
		}
	}

	// ── HELPERS ────────────────────────────────────────────────────────────────
	private Node2D BuscarAtacanteEnCarril()
	{
		if (!HasMeta("carril")) return null;
		string grupo    = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		string miCarril = ((string)GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();

		foreach (Node n in GetTree().GetNodesInGroup(grupo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			string c = ((string)e.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			if (c != miCarril) continue;
			var animSprite = e.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
			if (animSprite != null && ((string)animSprite.Animation).Contains("ataque")) return e;
		}
		return null;
	}

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
