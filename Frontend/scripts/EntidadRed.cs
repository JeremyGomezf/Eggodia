using Godot;
using System;
using Godot.Collections;

public partial class EntidadRed : Node2D
{
	// --- VARIABLES ---
	[Export] public string IdPersonaje = "rey_huevo";
	
	[Export] public int VidaMax = 100;
	public int VidaActual;
	
	// 🔥 ¡NUEVO! Sistema de Maná
	[Export] public int ManaMax = 50;
	public int ManaActual;
	[Export] public int CostoHabilidad = 20; // Cuesta 20 MP usar el ataque doble

	[Export] public int Ataque = 15;

	public bool EstaDefendiendo = false; 
	public static string TurnoDe = "rey_huevo"; 

	private ProgressBar _barraVida;
	private ProgressBar _barraMana; // Buscaremos la barra azul
	private Label _miLabel;
	private Control _menuAcciones;
	private CpuParticles2D _chispas;

	// --- ARRANQUE ---
	public override void _Ready()
	{
		VidaActual = VidaMax;
		ManaActual = ManaMax; // Llenamos el tanque de magia al empezar

		_miLabel = GetNodeOrNull<Label>("Label");
		_barraVida = GetNodeOrNull<ProgressBar>("ProgressBar");
		_barraMana = GetNodeOrNull<ProgressBar>("BarraMana"); 
		_menuAcciones = GetNodeOrNull<Control>("MenuAcciones");
		_chispas = GetNodeOrNull<CpuParticles2D>("Chispas");
		
		if (_barraVida != null) { _barraVida.MaxValue = VidaMax; _barraVida.Value = VidaActual; }
		if (_barraMana != null) { _barraMana.MaxValue = ManaMax; _barraMana.Value = ManaActual; }

		if (_menuAcciones != null)
		{
			_menuAcciones.GetNode<Button>("BtnAtaque").Pressed += AccionAtacar;
			_menuAcciones.GetNode<Button>("BtnDefensa").Pressed += AccionDefensa;
			_menuAcciones.GetNode<Button>("BtnHabilidad").Pressed += AccionHabilidad;
		}

		ActualizarInterfaz();
	}

	// --- LAS ACCIONES DEL MENÚ ---
	private void AccionAtacar()
	{
		GD.Print($"{IdPersonaje} lanza un ataque normal.");
		EjecutarTurno("ataque");
	}

	private void AccionDefensa()
	{
		EstaDefendiendo = true;
		GD.Print($"{IdPersonaje} levanta su escudo.");
		EjecutarTurno("defensa"); 
	}

	private void AccionHabilidad()
	{
		// 🧠 Lógica de Maná: Revisamos si nos alcanza el dinero (magia)
		if (ManaActual >= CostoHabilidad)
		{
			ManaActual -= CostoHabilidad; // Cobramos el Maná
			GD.Print($"¡{IdPersonaje} gasta {CostoHabilidad} MP y usa su ATAQUE ESPECIAL!");
			EjecutarTurno("habilidad");
		}
		else
		{
			// Si hace trampa y le da clic sin maná, no pasa de turno
			GD.PrintErr($"¡{IdPersonaje} no tiene suficiente Maná!");
		}
	}

	// --- EL CEREBRO DEL DUELO ---
	private void EjecutarTurno(string tipo)
	{
		string nombreEnemigo = (IdPersonaje == "rey_huevo") ? "dino_huevo" : "rey_huevo";
		var enemigo = GetTree().CurrentScene.FindChild(nombreEnemigo, true, false) as EntidadRed;

		if (enemigo != null && enemigo.VidaActual > 0)
		{
			if (tipo == "ataque") 
			{
				enemigo.RecibirDanio(Ataque, this); 
			}
			else if (tipo == "habilidad")
			{
				int danioEspecial = Ataque * 2; 
				enemigo.RecibirDanio(danioEspecial, this);
			}
			
			TurnoDe = nombreEnemigo; 
			ActualizarInterfaz();
			enemigo.ActualizarInterfaz();
		}
	}

	// --- SISTEMA DE DAÑO ---
	public void RecibirDanio(int cantidad, EntidadRed atacante)
	{
		if (VidaActual <= 0) return;

		if (EstaDefendiendo)
		{
			cantidad = cantidad / 2;
			GD.Print($"¡Escudo activado! {IdPersonaje} solo recibe {cantidad} de daño.");
			EstaDefendiendo = false; 
		}

		VidaActual -= cantidad;
		if (VidaActual < 0) VidaActual = 0;

		if (VidaActual <= 0) 
		{
			Morir();
			if (atacante != null) atacante.CelebrarVictoria();
		}
		ActualizarInterfaz();
	}

	private void Morir()
	{
		if (_miLabel != null) { _miLabel.Text = "¡DERROTADO!"; _miLabel.Modulate = new Color(1, 0, 0); }
		Modulate = new Color(1, 0.2f, 0.2f); 
		
		var tween = GetTree().CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(this, "rotation", Mathf.DegToRad(90), 0.5f);
		tween.TweenProperty(this, "position", Position + new Vector2(0, 30), 0.5f);
	}

	public void CelebrarVictoria()
	{
		if (_chispas != null) 
		{
			_chispas.Emitting = true;
			GD.Print($"¡{IdPersonaje} ES EL GANADOR!");
		}
	}

	// --- ACTUALIZAR PANTALLA ---
	public void ActualizarInterfaz()
	{
		// Añadimos el MP al texto por si no quieres usar la barra gráfica
		if (_miLabel != null && VidaActual > 0) 
			_miLabel.Text = $"{IdPersonaje.ToUpper()}\nHP: {VidaActual} | MP: {ManaActual}\nTURNO DE: {TurnoDe}";
		
		if (_barraVida != null) _barraVida.Value = VidaActual;
		if (_barraMana != null) _barraMana.Value = ManaActual; // Actualizamos la barra azul

		if (_menuAcciones != null)
		{
			_menuAcciones.Visible = (TurnoDe == IdPersonaje && VidaActual > 0);
			
			// 💡 MAGIA EXTRA: Si no tienes maná, el botón de habilidad se apaga
			var btnHabilidad = _menuAcciones.GetNodeOrNull<Button>("BtnHabilidad");
			if (btnHabilidad != null)
			{
				btnHabilidad.Disabled = (ManaActual < CostoHabilidad);
			}
		}
	}
}
