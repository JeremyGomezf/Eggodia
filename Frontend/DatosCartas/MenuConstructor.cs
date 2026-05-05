using Godot;
using System;

public partial class MenuConstructor : Control
{
	[Export] private ColeccionPanel _panelColeccion;
	[Export] private MazoPanel _panelMazo;
	[Export] private CartaDetalles _panelDetalles;

	public override void _Ready()
	{
		// 1. Cuando se elige una carta en la colección -> Va al Mazo
		_panelColeccion.OnCartaElegidaParaMazo += _panelMazo.AgregarCartaAlMazo;
		
		// 2. Cuando se elige en la colección -> También se muestra en Detalles (Opcional)
		_panelColeccion.OnCartaElegidaParaMazo += _panelDetalles.MostrarDatos;

		// 3. Cuando seleccionas una carta que YA ESTÁ en el mazo -> Muestra Detalles
		_panelMazo.OnCartaSeleccionadaEnMazo += _panelDetalles.MostrarDatos;
	}
}
