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
	
	// Ruta de la escena para instanciar en batalla
	[Export(PropertyHint.File, "*.tscn")] public string RutaEscena = "";
	
	// Stats principales
	[Export] public int Costo = 0;
	[Export] public int Ataque = 0;
	[Export] public int Defensa = 0;
	[Export] public int Vida = 0;
	[Export] public string Velocidad = "MEDIA";
	[Export] public string Rango = "CORTO";
	
	// Fortalezas y debilidades
	[Export] public string FuerteContra = "";
	[Export] public string DebilContra = "";
	
	[Export(PropertyHint.MultilineText)]
	public string Descripcion = "";
}
