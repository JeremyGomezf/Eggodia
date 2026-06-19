using Godot;
using System;

public partial class PantallaDerrota : CanvasLayer
{
	public override void _Ready()
	{
		var btnReintentar = GetNodeOrNull<Button>("Overlay/VBox/BtnReintentar");
		var btnMenu       = GetNodeOrNull<Button>("Overlay/VBox/BtnMenu");

		if (btnReintentar != null)
			btnReintentar.Pressed += () =>
			{
				GetTree().Paused = false;
				GetTree().ChangeSceneToFile("res://DatosCartas/MenuConstructor.tscn");
			};

		if (btnMenu != null)
			btnMenu.Pressed += () =>
			{
				GetTree().Paused = false;
				GetTree().ChangeSceneToFile("res://escenas/menu/menu_principal.tscn");
			};

		// Animación de aparición
		var overlay = GetNode<ColorRect>("Overlay");
		overlay.Modulate = new Color(1, 1, 1, 0);
		Tween tw = CreateTween();
		tw.TweenProperty(overlay, "modulate:a", 1.0f, 1.5f);
	}
}
