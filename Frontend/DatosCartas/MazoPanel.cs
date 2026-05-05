using Godot;
using System;

public partial class MazoPanel : Control
{
	[Export] private GridContainer _gridMazo; 
	[Export] private Button _btnRemover;
	[Export] private PackedScene _escenaCartaMini; 

	public event Action<CartaData> OnCartaSeleccionadaEnMazo;
	private CartaMini _cartaActualSeleccionada = null;

	public override void _Ready()
	{
		// VERIFICACIÓN A PRUEBA DE FALLOS
		if (_btnRemover != null) 
		{
			_btnRemover.Pressed += RemoverCarta;
			GD.Print("EXITO: El Botón Remover está conectado y listo.");
		}
		else
		{
			GD.PrintErr("¡ALERTA ROJA! La casilla 'Btn Remover' está vacía en el Inspector de MazoPanel. ¡Arrastra el botón ahí!");
		}
	}

	public void AgregarCartaAlMazo(CartaData datos)
	{
		// 1. Candado Anti-Duplicados
		foreach (Node slot in _gridMazo.GetChildren())
		{
			if (slot.GetChildCount() > 0) 
			{
				CartaMini cartaExistente = slot.GetChild<CartaMini>(0);
				if (cartaExistente.MisDatos.Nombre == datos.Nombre) 
				{
					GD.Print("Rechazado: ¡" + datos.Nombre + " ya está en tu mazo!");
					return; 
				}
			}
		}

		// 2. Llenar hueco vacío
		foreach (Node slot in _gridMazo.GetChildren())
		{
			if (slot.GetChildCount() == 0) 
			{
				CartaMini nuevaCarta = _escenaCartaMini.Instantiate<CartaMini>();
				slot.AddChild(nuevaCarta);
				nuevaCarta.CargarDatos(datos);
				
				nuevaCarta.OnClickeada += SeleccionarCartaDelMazo;
				return; 
			}
		}
	}

	private void SeleccionarCartaDelMazo(CartaMini carta)
	{
		// Regresamos la carta anterior a su color normal
		if (_cartaActualSeleccionada != null && IsInstanceValid(_cartaActualSeleccionada))
		{
			_cartaActualSeleccionada.Modulate = new Color(1, 1, 1, 1); 
		}

		// Seleccionamos la nueva y la oscurecemos
		_cartaActualSeleccionada = carta;
		_cartaActualSeleccionada.Modulate = new Color(0.7f, 0.7f, 0.7f, 1); 

		GD.Print("Has seleccionado: " + carta.MisDatos.Nombre + ". ¡Ya puedes borrarla!");
		OnCartaSeleccionadaEnMazo?.Invoke(carta.MisDatos); 
	}

	private void RemoverCarta()
	{
		GD.Print("¡Clic en el botón Remover detectado!"); // Chismoso

		if (_cartaActualSeleccionada != null && IsInstanceValid(_cartaActualSeleccionada))
		{
			_cartaActualSeleccionada.QueueFree(); 
			_cartaActualSeleccionada = null;
			GD.Print("¡Carta fulminada del mazo!");
		}
		else
		{
			GD.Print("No borré nada porque no había ninguna carta oscurecida/seleccionada.");
		}
	}
}
