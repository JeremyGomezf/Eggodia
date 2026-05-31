using Godot;
using System;

public partial class MazoPanel : Control
{
	[Export] private GridContainer _gridMazo; 
	[Export] private Button _btnRemover;
	[Export] private PackedScene _escenaCartaMini; 

	public event Action<CartaData> OnCartaSeleccionadaEnMazo;
	public event Action OnMazoCambiado;
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
			var cartaExistente = slot.GetNodeOrNull<CartaMini>("CartaMini") ?? slot.GetChildOrNull<CartaMini>(slot.GetChildCount() - 1);
			if (cartaExistente != null && cartaExistente.MisDatos.Nombre == datos.Nombre) 
			{
				GD.Print("Rechazado: ¡" + datos.Nombre + " ya está en tu mazo!");
				return; 
			}
		}

		// 2. Llenar hueco vacío
		foreach (Node slot in _gridMazo.GetChildren())
		{
			var cartaExistente = slot.GetNodeOrNull<CartaMini>("CartaMini") ?? slot.GetChildOrNull<CartaMini>(slot.GetChildCount() - 1);
			if (cartaExistente == null) 
			{
				CartaMini nuevaCarta = _escenaCartaMini.Instantiate<CartaMini>();
				nuevaCarta.Name = "CartaMini";
				slot.AddChild(nuevaCarta);
				
				// Ocultar fondo vacío
				var bg = slot.GetNodeOrNull<Control>("FondoVacio");
				if (bg != null) bg.Hide();

				nuevaCarta.CargarDatos(datos);
				nuevaCarta.SetModoMazo(true);
				
				// Animación de aparición (Pop-in)
				nuevaCarta.PivotOffset = new Vector2(42.5f, 65f); // Centro aproximado del slot
				nuevaCarta.Scale = new Vector2(0.3f, 0.3f);
				var tween = nuevaCarta.CreateTween();
				tween.TweenProperty(nuevaCarta, "scale", new Vector2(1.1f, 1.1f), 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
				tween.TweenProperty(nuevaCarta, "scale", Vector2.One, 0.1f).SetTrans(Tween.TransitionType.Sine);
				
				nuevaCarta.OnClickeada += SeleccionarCartaDelMazo;
				OnMazoCambiado?.Invoke();
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
			var slot = _cartaActualSeleccionada.GetParent();
			slot.RemoveChild(_cartaActualSeleccionada);
			_cartaActualSeleccionada.QueueFree(); 
			_cartaActualSeleccionada = null;
			GD.Print("¡Carta fulminada del mazo!");
			
			// Mostrar fondo vacío
			if (slot != null) 
			{
				var bg = slot.GetNodeOrNull<Control>("FondoVacio");
				if (bg != null) bg.Show();
			}

			OnMazoCambiado?.Invoke();
		}
		else
		{
			GD.Print("No borré nada porque no había ninguna carta oscurecida/seleccionada.");
		}
	}
}
