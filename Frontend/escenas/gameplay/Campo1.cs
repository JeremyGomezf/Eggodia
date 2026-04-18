using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	[Export] private int vidaJugador = 2000;
	[Export] private int vidaRival = 2000;
	private int tiempoRestante = 120; // 2 MINUTOS
	public bool juegoTerminado = false; // Pública para que la Carta la lea

	[Export] private Texture2D iconoCursorSacrificio;
	[Export] private PackedScene escenaCartaBase; 
	[Export] private Control contenedorMano; 

	private List<int> mazoIndices = new List<int>();
	private int proximoIndiceMazo = 0;
	private bool modoSacrificioActivo = false;
	private Random random = new Random();

	[Export] private PackedScene escenaTronoRef = GD.Load<PackedScene>("res://escenas/gameplay/tronocampo.tscn");
	[Export] private PackedScene escenaReyHuevoRef = GD.Load<PackedScene>("res://escenas/personajes/reyhuevo1.tscn");
	[Export] private PackedScene escenaDinoHuevoRef = GD.Load<PackedScene>("res://escenas/personajes/dinohuevo1.tscn");

	private string[] imagenesCartas = {
		"res://imagenes/CartasPng/DragonCart.png", "res://imagenes/CartasPng/GolemCart.png",
		"res://imagenes/CartasPng/MaguinCart.png", "res://imagenes/CartasPng/SoldRealCart.png",
		"res://imagenes/CartasPng/TReXCart.png", "res://imagenes/CartasPng/TiburonCart.png",
		"res://imagenes/CartasPng/PeonCart.png", "res://imagenes/CartasPng/EncebolladoCart.png"
	};

	private string[] escenasTropas = {
		"res://cartas prime/Dragon_prime.tscn", "res://cartas prime/Golem_prime.tscn",
		"res://cartas prime/Maguin_prime.tscn", "res://cartas prime/SoldadoReal_prime.tscn",
		"res://cartas prime/TRex_prime.tscn", "res://cartas prime/Tiburon_prime.tscn",
		"res://cartas prime/Peon_prime.tscn", "res://cartas prime/Encebollado_prime.tscn"
	};

	private tronocampo tronoJugador, tronoRival; 

	public override void _Ready()
	{
		PrepararMazoSinRepetir();
		CrearEscenaDeBatalla();
		BarajarMazoInicial();
		ActualizarInterfaz();
		GetNode<Timer>("Timer").Start();
	}

	private void PrepararMazoSinRepetir()
	{
		mazoIndices.Clear();
		for (int i = 0; i < imagenesCartas.Length; i++) mazoIndices.Add(i);
		for (int i = 0; i < mazoIndices.Count; i++)
		{
			int r = random.Next(i, mazoIndices.Count);
			int temp = mazoIndices[i]; mazoIndices[i] = mazoIndices[r]; mazoIndices[r] = temp;
		}
		proximoIndiceMazo = 0;
	}

	public void BarajarMazoInicial() { for (int i = 0; i < 3; i++) CrearNuevaCarta(); }

	public void CrearNuevaCarta()
	{
		if (juegoTerminado || escenaCartaBase == null || contenedorMano == null) return;
		for (int i = 1; i <= 3; i++)
		{
			string idSpot = "Spot" + i;
			bool ocupado = false;
			foreach (Node n in contenedorMano.GetChildren())
				if (n is Carta c && c.NombreSpot == idSpot && !c.IsQueuedForDeletion()) { ocupado = true; break; }

			if (!ocupado)
			{
				Marker2D spot = contenedorMano.GetNodeOrNull<Marker2D>(idSpot);
				if (spot != null)
				{
					Carta nueva = (Carta)escenaCartaBase.Instantiate();
					nueva.NombreSpot = idSpot;
					contenedorMano.AddChild(nueva);
					
					Vector2 escalaGiga = new Vector2(6.5f, 6.5f); // ESCALA GIGANTE
					nueva.Scale = escalaGiga;
					nueva.GlobalPosition = spot.GlobalPosition - (nueva.Size * escalaGiga / 2);
					nueva.GuardarEstadoOriginal();
					
					if (proximoIndiceMazo >= mazoIndices.Count) PrepararMazoSinRepetir();
					int idx = mazoIndices[proximoIndiceMazo++];
					nueva.AsignarDatos(imagenesCartas[idx], escenasTropas[idx], idx);
					return;
				}
			}
		}
	}

	public void TropaInvocada(Node2D puntoMod, PackedScene escenaTropa)
	{
		if (juegoTerminado) return; // SEGURO INVOCACIÓN

		if (escenaTropa != null)
		{
			Node2D nuevaTropa = (Node2D)escenaTropa.Instantiate();
			AddChild(nuevaTropa);
			nuevaTropa.GlobalPosition = puntoMod.GlobalPosition;
			Node marcador = new Node(); marcador.Name = "Ocupado";
			puntoMod.AddChild(marcador);
			marcador.SetMeta("tropa_instanciada", nuevaTropa); 
		}
		GetTree().CreateTimer(0.3f).Timeout += () => { if (!juegoTerminado) CrearNuevaCarta(); };
	}

	public void _on_barajar_pressed()
	{
		if (juegoTerminado) return; // SEGURO BARAJAR
		foreach (Node n in contenedorMano.GetChildren()) if (n is Carta c) { c.NombreSpot = "X"; c.QueueFree(); }
		GetTree().CreateTimer(0.15f).Timeout += () => { PrepararMazoSinRepetir(); BarajarMazoInicial(); };
	}

	public void _on_sacrificar_pressed()
	{
		if (juegoTerminado) return; // SEGURO SACRIFICIO
		modoSacrificioActivo = !modoSacrificioActivo;
		if (modoSacrificioActivo && iconoCursorSacrificio != null)
			Input.SetCustomMouseCursor(iconoCursorSacrificio, Input.CursorShape.Arrow, new Vector2(16, 16));
		else
			Input.SetCustomMouseCursor(null);
	}

	public override void _Input(InputEvent @event)
	{
		if (juegoTerminado) return;
		if (modoSacrificioActivo && @event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			VerificarSacrificioEnCampo(GetGlobalMousePosition());
	}

	private void VerificarSacrificioEnCampo(Vector2 posClick)
	{
		foreach (Node2D punto in GetTree().GetNodesInGroup("zonas_invocacion"))
		{
			if (punto.GlobalPosition.DistanceTo(posClick) < 110)
			{
				Node marcador = punto.GetNodeOrNull("Ocupado");
				if (marcador != null)
				{
					vidaJugador -= 500;
					if (marcador.HasMeta("tropa_instanciada")) ((Node)marcador.GetMeta("tropa_instanciada")).QueueFree();
					marcador.QueueFree();
					ActualizarInterfaz();
					_on_sacrificar_pressed();
					CheckEstadoJuego();
					break;
				}
			}
		}
	}

	public void _on_timer_timeout() 
	{ 
		if (juegoTerminado) return;
		
		tiempoRestante--; 
		if (tiempoRestante <= 0) 
		{
			tiempoRestante = 0; // BLOQUEO EN CERO
			ActualizarInterfaz();
			DeterminarGanadorPorTiempo();
		}
		else 
		{
			ActualizarInterfaz();
		}
	}

	private void DeterminarGanadorPorTiempo()
	{
		if (vidaJugador > vidaRival) FinalizarPartida("¡VICTORIA POR SALUD!");
		else if (vidaRival > vidaJugador) FinalizarPartida("¡DERROTA POR SALUD!");
		else FinalizarPartida("¡EMPATE!");
	}

	private void ActualizarInterfaz()
	{
		GetNode<Label>("Vida1").Text = $"Vida: {vidaJugador}";
		GetNode<Label>("Vida2").Text = $"Vida: {vidaRival}";
		GetNode<Label>("Tiempo").Text = $"Tiempo: {tiempoRestante}";
	}

	private void CheckEstadoJuego() 
	{ 
		if (vidaJugador <= 0) FinalizarPartida("DERROTA"); 
		else if (vidaRival <= 0) FinalizarPartida("VICTORIA"); 
	}

	private void FinalizarPartida(string m)
	{
		if (juegoTerminado) return;
		juegoTerminado = true;
		
		GetNode<Timer>("Timer").Stop(); // DETENER TIMER FÍSICAMENTE
		GetNode<Control>("CanvasLayer/PantallaFinal").Visible = true;
		GetNode<Label>("CanvasLayer/PantallaFinal/LabelResultado").Text = m;

		// ANIMACIÓN DE MUERTE SEGÚN RESULTADO
		if (vidaRival <= 0 || (tiempoRestante == 0 && vidaRival < vidaJugador))
			if (tronoRival != null) tronoRival.EfectoMuerteMinecraft();
			
		if (vidaJugador <= 0 || (tiempoRestante == 0 && vidaJugador < vidaRival))
			if (tronoJugador != null) tronoJugador.EfectoMuerteMinecraft();
	}

	private void CrearEscenaDeBatalla()
	{
		Marker2D m1 = GetNodeOrNull<Marker2D>("SpawnTrono1");
		Marker2D m2 = GetNodeOrNull<Marker2D>("SpawnTrono2");
		if (m1 != null && m2 != null) 
		{
			tronoJugador = (tronocampo)escenaTronoRef.Instantiate(); AddChild(tronoJugador);
			tronoJugador.GlobalPosition = m1.GlobalPosition; tronoJugador.CargarHuevo(escenaReyHuevoRef, false);
			tronoRival = (tronocampo)escenaTronoRef.Instantiate(); AddChild(tronoRival);
			tronoRival.GlobalPosition = m2.GlobalPosition; tronoRival.CargarHuevo(escenaDinoHuevoRef, true);
		}
	}
}
