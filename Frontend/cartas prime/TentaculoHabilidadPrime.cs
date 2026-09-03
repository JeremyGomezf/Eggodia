using Godot;

/// <summary>
/// TentaculoHabilidadPrime — efecto de bloqueo instanciado por la habilidad del Calamar
/// Gigante sobre una tropa enemiga de un carril adyacente. No es una tropa: no ataca, no usa
/// habilidad, no participa del flujo de turnos.
///
/// Mientras existe, marca a su objetivo con la meta "atrapado_tentaculo" (que
/// Campo1.EstaBlockeada ya respeta, tanto para el jugador como para la CPU) y le inflige 10 de
/// daño cada 10 segundos continuos. Se libera —reproduciendo "irse_tentaculo" antes de
/// eliminarse— cuando el Calamar que lo invocó pierde su postura (llama a <see cref="Liberar"/>)
/// o si el objetivo muere primero.
/// </summary>
public partial class TentaculoHabilidadPrime : Area2D
{
	public Node2D objetivo;

	private AnimatedSprite2D _anim;
	private bool _liberando = false;

	public override void _Ready()
	{
		// Vive fuera del árbol de Campo1 (instanciado en GetTree().Root) — este grupo permite
		// que LimpiezaEfectos.cs lo elimine al reiniciar/salir de la partida.
		AddToGroup("efectos_calamar");

		_anim = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (_anim != null)
		{
			_anim.AnimationFinished += OnAnimationFinished;
			_anim.Play("respawn_tentaculo");
			_anim.Frame = 0;
			_anim.FrameProgress = 0f;
		}

		if (IsInstanceValid(objetivo)) objetivo.SetMeta("atrapado_tentaculo", true);

		ProgramarDañoPeriodico();
	}

	/// <summary>Orientación: los tentáculos "se estiran" de vuelta hacia el Calamar que los
	/// invocó — si es del jugador, señalan a la izquierda; si es del rival, a la derecha
	/// (al revés que la convención normal de tropas).</summary>
	public void AplicarOrientacion(bool esCalamarJugador)
	{
		if (_anim != null) _anim.FlipH = esCalamarJugador;
	}

	private void OnAnimationFinished()
	{
		if (_anim == null || !IsInstanceValid(this)) return;
		string anim = (string)_anim.Animation;

		if (anim == "respawn_tentaculo")
		{
			_anim.Play("idle_tentaculo");
		}
		else if (anim == "irse_tentaculo")
		{
			QueueFree();
		}
	}

	private void ProgramarDañoPeriodico()
	{
		GetTree().CreateTimer(10.0).Timeout += () =>
		{
			if (_liberando || !IsInstanceValid(this)) return;
			if (!IsInstanceValid(objetivo))
			{
				// La tropa atrapada murió por otra causa: no queda nada que sujetar.
				Liberar();
				return;
			}
			objetivo.Call("RecibirDaño", 10);
			ProgramarDañoPeriodico();
		};
	}

	/// <summary>Libera al objetivo (quita "atrapado_tentaculo"/"tentaculo_activo") y reproduce
	/// la salida antes de eliminarse. Llamado por CalamarGPrime cuando su escudo llega a 0, por
	/// Campo1 de inmediato si el objetivo muere, o por el propio tick de daño si el objetivo ya
	/// no es válido.</summary>
	public void Liberar()
	{
		if (_liberando) return;
		_liberando = true;

		if (IsInstanceValid(objetivo))
		{
			objetivo.RemoveMeta("atrapado_tentaculo");
			objetivo.RemoveMeta("tentaculo_activo");
		}

		if (_anim != null) _anim.Play("irse_tentaculo");
		else QueueFree();
	}
}
