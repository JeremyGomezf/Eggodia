using Godot;
using System;

public partial class MenuPrincipal : Control
{
	// Esta variable aparecerá en el Inspector a la derecha. 
	// Ahí debes arrastrar tu archivo 'tablero_duelo.tscn'.
	[Export] public string RutaEscenaJuego; 

	public override void _Ready()
	{
		// 1. Buscamos los botones dentro del VBoxContainer
		var botonJugar = GetNode<Button>("VBoxContainer/JUGAR");
		var botonSalir = GetNode<Button>("VBoxContainer/SALIR");
		
		// 2. Conectamos los clics a las funciones
		botonJugar.Pressed += AlPresionarJugar;
		botonSalir.Pressed += AlPresionarSalir;
		
		GD.Print("Líder usuario, sistema de Menú de juegocards en línea.");
	}

	private void AlPresionarJugar()
	{
		if (string.IsNullOrEmpty(RutaEscenaJuego))
		{
			GD.PrintErr("¡Error! Jeremy, olvidas arrastrar la escena del tablero al Inspector.");
			return;
		}

		GD.Print("Iniciando Duelo... ¡Activando reglas de 2000 HP!");
		// Cambiamos a la escena del combate
		GetTree().ChangeSceneToFile(RutaEscenaJuego);
	}

	private void AlPresionarSalir()
	{
		GD.Print("Cerrando juegocards. ¡Hasta la próxima, Campeon!");
		GetTree().Quit(); // Esto cierra el juego completamente
	}
}
