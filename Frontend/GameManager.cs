using Godot;
using System;

public partial class GameManager : Node
{
	public enum FaseJuego { Construccion, Combate }
	public FaseJuego faseActual = FaseJuego.Construccion;

	public int turnoActual = 1;
	public int energiaJugador = 5;

	// 🔥 Ajustado a tu juego (9x10)
	private Node3D[,] matrizTablero = new Node3D[9, 10];

	public override void _Ready()
	{
		GD.Print("--- JUEGOCARDS: INICIANDO ---");
	}

	// 🔥 FUNCIÓN PRINCIPAL (la llamaremos desde el mapa)
	public bool PuedeColocar(int x, int y)
	{
		if (faseActual != FaseJuego.Construccion)
		{
			GD.Print("No estás en fase de construcción");
			return false;
		}

		if (y < 6) // zona enemigo / puente / agua
		{
			GD.Print("Zona no válida");
			return false;
		}

		if (matrizTablero[x, y] != null)
		{
			GD.Print("Celda ocupada");
			return false;
		}

		return true;
	}

	public void ColocarEnMatriz(int x, int y, Node3D objeto)
	{
		matrizTablero[x, y] = objeto;
		GD.Print($"Objeto colocado en {x},{y}");
	}
}
