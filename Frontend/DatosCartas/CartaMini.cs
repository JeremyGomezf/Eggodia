using Godot;
using System;

public partial class CartaMini : Control // (O el nodo que uses de raíz, como VBoxContainer)
{
	// 1. Las referencias a la parte visual de tu carta
	[Export] private TextureRect _fotoCarta;
	[Export] private Label _nombreTexto;

	// 2. Los datos y el evento
	public CartaData MisDatos { get; private set; }
	public event Action<CartaMini> OnClickeada; 

	// 3. ¡LA FUNCIÓN QUE FALTABA! Esta es la que recibe los datos y pinta la carta
	public void CargarDatos(CartaData datos)
	{
		MisDatos = datos; // Guardamos los datos en la carta

		// Actualizamos la imagen y el texto
		if (_fotoCarta != null) _fotoCarta.Texture = datos.Imagen;
		if (_nombreTexto != null) _nombreTexto.Text = datos.Nombre;
	}

	// 4. La función que detecta cuando le das clic con el mouse
	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent)
		{
			if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed)
			{
				// Dispara el evento avisando "¡Ey, me hicieron clic!"
				OnClickeada?.Invoke(this); 
			}
		}
	}
}
