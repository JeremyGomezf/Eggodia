using Godot;

/// <summary>
/// Tiburón — Pasiva: +50 ATQ cada turno (ambos turnos). Sin botón de habilidad.
/// Daño de ataque en frame 3 de la animación "ataque".
/// </summary>
public partial class TiburonPrime : TropaBase
{
	public override string Tipo => Tipos.AGUA;

	private Node2D _objetivo;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 300; escudoActual = escudoMaximo = 200; puntosAtaque = 230; }
		base._Ready();
		_anim.FrameChanged += OnFrameChanged;
	}

	// ── CAPACIDADES ────────────────────────────────────────────────────────────
	public override bool AutogestionaDañoAtaque() => true;
	public override bool TieneHabilidadEspecial() => false;
	public override bool MostrarBotonHabilidad()  => false;

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

	// ── PASIVA: +50 ATQ cada cambio de turno ──────────────────────────────────
	public override void TickHabilidad()
	{
		if (_estaMuerto) return;
		puntosAtaque += 50;
	}

	// ── BUSCAR OBJETIVO EN CARRIL ──────────────────────────────────────────────
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
