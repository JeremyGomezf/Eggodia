using Godot;

/// <summary>
/// Muro de piedra invocado por el Gólem. No es una tropa: no ataca, no usa habilidad,
/// no participa del flujo de turnos. Solo bloquea el carril hasta que su durabilidad llega a 0.
/// </summary>
public partial class MuroGolemPrime : Area2D
{
	[Export] public int durabilidadMaxima = 250;
	public int durabilidadActual;

	/// <summary>Alias de compatibilidad: código de combate que lee "vidaActual" por reflexión
	/// (p. ej. Campo1.Combate.cs para tropas sin daño autogestionado) ve la durabilidad del muro.</summary>
	public int vidaActual => durabilidadActual;

	private AnimatedSprite2D _anim;
	private CollisionShape2D _col;
	private Control _contenedorStats;
	private bool _enRespawn  = true;
	private bool _destruido  = false;

	public override void _Ready()
	{
		durabilidadActual = durabilidadMaxima;
		_anim = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_col  = GetNode<CollisionShape2D>("CollisionShape2D");
		_contenedorStats = GetNodeOrNull<Control>("StatsMuro");

		_anim.Connect(AnimatedSprite2D.SignalName.FrameChanged, Callable.From(OnFrameChanged));
		_anim.Connect(AnimatedSprite2D.SignalName.AnimationFinished, Callable.From(OnAnimationFinished));

		_anim.Play("respawn_muro");
		ActualizarBarra();
	}

	private void OnAnimationFinished()
	{
		if ((string)_anim.Animation == "respawn_muro")
		{
			_enRespawn = false;
			_anim.Play("muro_solido");
		}
	}

	// ── DAÑO ────────────────────────────────────────────────────────────────
	public void RecibirDaño(int cantidad)
	{
		if (_destruido) return;

		durabilidadActual -= cantidad;
		if (durabilidadActual < 0) durabilidadActual = 0;
		ActualizarBarra();

		if (durabilidadActual <= 0)
		{
			if ((string)_anim.Animation != "destrozado_muro") _anim.Play("destrozado_muro");
		}
		else if (!_enRespawn)
		{
			string destino = durabilidadActual < 100 ? "muro_dañado" : "muro_solido";
			if ((string)_anim.Animation != destino) _anim.Play(destino);
		}
	}

	/// <summary>Alias para las tropas que llaman RecibirDañoDe(cantidad, atacante) antes de RecibirDaño.</summary>
	public void RecibirDañoDe(int cantidad, Node2D atacante) => RecibirDaño(cantidad);

	// ── DESTRUCCIÓN: frame 17 de "destrozado_muro" ─────────────────────────
	private void OnFrameChanged()
	{
		if (_destruido) return;
		if ((string)_anim.Animation == "destrozado_muro" && _anim.Frame == 17)
		{
			_destruido = true;
			_col.SetDeferred("disabled", true);
			RemoveFromGroup("muros_jugador");
			RemoveFromGroup("muros_rival");
			QueueFree();
		}
	}

	private void ActualizarBarra()
	{
		var barra = _contenedorStats?.GetNodeOrNull<ProgressBar>("BarraDurabilidad");
		if (barra != null) barra.Value = durabilidadMaxima > 0 ? (float)durabilidadActual / durabilidadMaxima * 100 : 0;
	}
}
