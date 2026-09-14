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
			{
				_objetivo.Call("RecibirDaño", puntosAtaque);
				var campo = GetTree().Root.FindChild("Campo1", true, false);
				if (campo != null) campo.Call("RegistrarDañoTropa", this, puntosAtaque);
			}
		}
	}

	// ── PASIVA: +35 ATQ cada cambio de turno de forma continua ──────────────────
	public override void TickHabilidad()
	{
		if (_estaMuerto) return;
		puntosAtaque += 35;
	}
}
