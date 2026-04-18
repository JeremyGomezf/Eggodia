using Godot;
using System;

public partial class TRexPrime : Area2D
{
	private AnimatedSprite2D _anim;
	private bool _estaMuerto = false;

	public override void _Ready()
	{
		_anim = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		ReproducirIdle();
	}

	// 1. IDLE (Estado de espera)
	public void ReproducirIdle()
	{
		if (_estaMuerto) return;
		_anim.Play("idle");
	}

	// 2. ATAQUE (Activa el ataque y vuelve solo a idle)
	public async void ReproducirAtaque()
	{
		if (_estaMuerto) return;
		_anim.Play("ataque");
		
		// Espera a que termine la animación de ataque antes de volver a idle
		await ToSignal(_anim, "animation_finished");
		ReproducirIdle();
	}

	// 3. PRE DEFENSA (El personaje se pone en guardia)
	public void ReproducirPreDefensa()
	{
		if (_estaMuerto) return;
		_anim.Play("pre defensa");
	}

	// 4. DEFENSA (Cuando bloquea el ataque)
	public async void ReproducirDefensa()
	{
		if (_estaMuerto) return;
		_anim.Play("defensa");
		
		await ToSignal(_anim, "animation_finished");
		ReproducirIdle(); // Vuelve a idle tras defenderse
	}

	// 5. DAÑO (Cuando recibe el golpe)
	public async void ReproducirDaño()
	{
		if (_estaMuerto) return;
		_anim.Play("daño");
		
		await ToSignal(_anim, "animation_finished");
		ReproducirIdle();
	}

	// 6. DERROTA (Muerte)
	public void ReproducirDerrota()
	{
		_estaMuerto = true;
		_anim.Play("derrota");
		// Aquí no volvemos a idle porque el personaje ya perdió
	}
}
