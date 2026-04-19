using Godot;
using System;

public partial class GolemPrime : Area2D
{
	private AnimatedSprite2D _anim;
	private bool _estaMuerto = false;
	private bool _yaActuo = false; // Control de si ya usó su turno

	public override void _Ready()
	{
		_anim = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		ReproducirIdle();
	}

	// Detectar el clic para abrir el menú en Campo1
	public override void _InputEvent(Viewport viewport, InputEvent @event, int shapeIdx)
	{
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			if (_estaMuerto || _yaActuo) return; // Si está muerto o ya actuó, no hace nada

			var campo = GetTree().Root.FindChild("Campo1", true, false) as Campo1;
			if (campo != null)
			{
				campo.MostrarMenuTropa(this);
			}
		}
	}

	// Método para bloquear/desbloquear la tropa (sin cambios de color)
	public void SetActivo(bool estado)
	{
		_yaActuo = !estado;
	}

	// Función central para ejecutar animaciones desde el Campo
	public void EjecutarAccion(string accion)
	{
		if (_estaMuerto) return;

		switch (accion)
		{
			case "atacar":
				ReproducirAtaque();
				break;
			case "preparar_defensa":
				ReproducirPreDefensa();
				break;
			case "recibir_daño":
				ReproducirDaño();
				break;
			case "defender":
				ReproducirDefensa();
				break;
		}
	}

	public void ReproducirIdle()
	{
		if (_estaMuerto) return;
		_anim.Play("idle");
	}

	public async void ReproducirAtaque()
	{
		if (_estaMuerto) return;
		_anim.Play("ataque");
		await ToSignal(_anim, "animation_finished");
		ReproducirIdle();
	}

	public void ReproducirPreDefensa()
	{
		if (_estaMuerto) return;
		_anim.Play("pre defensa");
	}

	public async void ReproducirDefensa()
	{
		if (_estaMuerto) return;
		_anim.Play("defensa");
		await ToSignal(_anim, "animation_finished");
		ReproducirIdle();
	}

	public async void ReproducirDaño()
	{
		if (_estaMuerto) return;
		_anim.Play("daño");
		await ToSignal(_anim, "animation_finished");
		ReproducirIdle();
	}

	public void ReproducirDerrota()
	{
		_estaMuerto = true;
		_anim.Play("derrota");
	}
}
