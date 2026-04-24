using Godot;
using System;

public partial class DamaPrime : Area2D
{
	private AnimatedSprite2D _anim;
	private bool _estaMuerto = false;
	private bool _yaActuo = false;

	// --- ESTADÍSTICAS (Basadas en tu carta) ---
	[Export] public int vidaActual = 350;
	[Export] public int vidaMaxima = 350;
	[Export] public int escudoActual = 300;
	[Export] public int escudoMaximo = 300;
	[Export] public int puntosAtaque = 350;

	private Control _contenedorStats;

	public override void _Ready()
	{
		_anim = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_contenedorStats = GetNodeOrNull<Control>("StatsTropa"); 
		if (_contenedorStats != null) _contenedorStats.Visible = false;

		ReproducirIdle();
	}

	public override void _InputEvent(Viewport viewport, InputEvent @event, int shapeIdx)
	{
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			if (IsInGroup("tropas_rival")) 
			{
				MostrarBarras(true);
				return;
			}

			MostrarBarras(true);
			if (_estaMuerto || _yaActuo) return;

			var campo = GetTree().Root.FindChild("Campo1", true, false) as Campo1;
			if (campo != null) campo.MostrarMenuTropa(this);
		}
	}

	public void SetActivo(bool estado)
	{
		_yaActuo = !estado;
		if (estado) MostrarBarras(false);
	}

	// --- LÓGICA DE DAÑO CON ESCUDO CONDICIONAL ---
	public void RecibirDaño(int cantidad)
	{
		if (_estaMuerto) return;

		// --- NUEVA LÓGICA DE ESCUDO Y DEFENSA ---
		if (_anim.Animation == "pre defensa")
		{
			// Si estaba en pre-defensa, activa la animación de cubrirse
			EjecutarAccion("defender");
			
			// Primero el daño se reduce al 50% por la posición de defensa
			cantidad = (int)(cantidad * 0.5f);

			// SOLO AQUÍ se usa el escudo
			if (escudoActual > 0)
			{
				if (cantidad <= escudoActual) 
				{ 
					escudoActual -= cantidad; 
					cantidad = 0; 
				}
				else 
				{ 
					cantidad -= escudoActual; 
					escudoActual = 0; 
				}
			}
		}

		// Si sobra daño o NO estaba en defensa, el daño va directo a la vida
		if (cantidad > 0) 
		{
			vidaActual -= cantidad;
		}

		ActualizarBarrasUI();
		
		if (vidaActual <= 0) 
		{
			// Llamada unificada al Campo1 para procesar la muerte y liberar el carril
			var campo = GetTree().Root.FindChild("Campo1", true, false);
			if (campo != null) campo.Call("EjecutarMuerteTropaSacrificada", this);
		}
		else 
		{
			// Si recibió daño y NO estaba defendiendo, hace la animación de dolor
			if (_anim.Animation != "defensa") 
			{
				EjecutarAccion("recibir_daño");
			}
		}
	}

	public void EjecutarAccion(string accion)
	{
		if (_estaMuerto) return;
		switch (accion)
		{
			case "atacar": 
				_yaActuo = true;
				ReproducirAtaque(); 
				break;
			case "preparar_defensa": 
				_yaActuo = true;
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

	// --- MÉTODOS DE ANIMACIÓN ---

	public void ReproducirIdle() { if (!_estaMuerto) _anim.Play("idle"); }

	public async void ReproducirAtaque()
	{
		if (_estaMuerto) return;
		_anim.Play("ataque");
		await ToSignal(_anim, "animation_finished");
		ReproducirIdle();
	}

	public void ReproducirPreDefensa() { if (!_estaMuerto) _anim.Play("pre defensa"); }

	public async void ReproducirDefensa()
	{
		if (_estaMuerto) return;
		_anim.Play("defensa"); // Esta es la animación donde se cubre del golpe
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

	// --- UI ---
	private void MostrarBarras(bool mostrar)
	{
		if (_contenedorStats != null) { _contenedorStats.Visible = mostrar; ActualizarBarrasUI(); }
	}

	private void ActualizarBarrasUI()
	{
		var barraVida = _contenedorStats?.GetNodeOrNull<ProgressBar>("BarraVida");
		var barraEscudo = _contenedorStats?.GetNodeOrNull<ProgressBar>("BarraEscudo");
		
		// Actualización en porcentaje (0 a 100)
		if (barraVida != null) barraVida.Value = (float)vidaActual / vidaMaxima * 100;
		if (barraEscudo != null) barraEscudo.Value = (float)escudoActual / escudoMaximo * 100;
	}
}
