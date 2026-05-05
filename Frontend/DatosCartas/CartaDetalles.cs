using Godot;
using System;

public partial class CartaDetalles : Control
{
	[Export] private Label _nombreLabel;
	[Export] private TextureRect _imagenRect;
	[Export] private Label _ataqueLabel;
	[Export] private Label _defensaLabel;
	[Export] private Label _vidaLabel;
	[Export] private RichTextLabel _descripcionLabel; 

	public void MostrarDatos(CartaData datos)
	{
		if (datos == null) return;

		if (_nombreLabel != null) 
		{
			_nombreLabel.Text = datos.Nombre;
			// ¡NUEVO! Obligamos por código a que la palabra salte a la siguiente línea si no cabe
			_nombreLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		}

		if (_imagenRect != null) _imagenRect.Texture = datos.Imagen;
		if (_ataqueLabel != null) _ataqueLabel.Text = datos.Ataque.ToString();
		if (_defensaLabel != null) _defensaLabel.Text = datos.Defensa.ToString();
		if (_vidaLabel != null) _vidaLabel.Text = datos.Vida.ToString();
		if (_descripcionLabel != null) _descripcionLabel.Text = datos.Descripcion; 
	}
}
