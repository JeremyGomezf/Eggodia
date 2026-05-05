using Godot;
using System;

// ¡NUEVO! Creamos las categorías posibles
public enum CategoriaCarta {
	Unidad,
	Hechizo,
	Estructura
}

[GlobalClass]
public partial class CartaData : Resource
{
	[Export] public string Nombre = "";
	[Export] public Texture2D Imagen;
	
	// ¡NUEVO! Variable que mostrará un menú desplegable en el Inspector
	[Export] public CategoriaCarta Categoria = CategoriaCarta.Unidad; 
	
	// Stats principales
	[Export] public int Ataque = 0;
	[Export] public int Defensa = 0;
	[Export] public int Vida = 0;
	
	[Export(PropertyHint.MultilineText)]
	public string Descripcion = "";
}
