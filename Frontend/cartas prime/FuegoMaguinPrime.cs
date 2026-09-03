using Godot;

/// <summary>
/// FuegoMaguinPrime — efecto de fuego instanciado por el ataque directo del Maguín (frame 3 de
/// su animación de ataque). No es una tropa: es un efecto de un solo uso. Aplica el daño en el
/// frame 1 de su propia animación "respawn_fuego_maguin", modula a la tropa alcanzada a rojo
/// encendido durante 0.5s simulando la quemadura, y se elimina sola al terminar.
/// </summary>
public partial class FuegoMaguinPrime : Area2D
{
	public Node2D objetivo;
	public Node2D atacante;
	public int    dañoAtaque;

	private AnimatedSprite2D _anim;
	private bool _dañoAplicado = false;

	public override void _Ready()
	{
		_anim = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (_anim != null)
		{
			_anim.FrameChanged      += OnFrameChanged;
			_anim.AnimationFinished += OnAnimationFinished;
			// La escena guarda un frame estático de vista previa (ej. frame 2) como valor por
			// defecto del editor. Sin este reinicio explícito, Play() podría reanudar desde ese
			// frame en vez de 0, y la animación jamás volvería a pasar por el frame 1 — el daño
			// nunca se aplicaría.
			_anim.Play("respawn_fuego_maguin");
			_anim.Frame = 0;
			_anim.FrameProgress = 0f;
		}
		else
		{
			// Sin sprite: aplica el daño de inmediato y no deja el nodo huérfano.
			AplicarDaño();
			QueueFree();
		}
	}

	private void OnFrameChanged()
	{
		if (_dañoAplicado || _anim.Frame != 1) return;
		AplicarDaño();
	}

	private void AplicarDaño()
	{
		if (_dañoAplicado || !IsInstanceValid(objetivo)) return;
		_dañoAplicado = true;

		objetivo.Call("RecibirDaño", dañoAtaque);

		if (IsInstanceValid(atacante))
		{
			var campo = GetTree().Root.FindChild("Campo1", true, false);
			if (campo != null) campo.Call("RegistrarDañoTropa", atacante, dañoAtaque);
		}

		// Modulación de quemadura: rojo encendido 0.5s sobre la tropa alcanzada.
		Color colorBase = objetivo.Modulate;
		Tween tw = objetivo.CreateTween();
		tw.TweenProperty(objetivo, "modulate", new Color(1.5f, 0.4f, 0.4f), 0.1f);
		tw.TweenProperty(objetivo, "modulate", colorBase, 0.4f);
	}

	private void OnAnimationFinished()
	{
		if (IsInstanceValid(this)) QueueFree();
	}
}
