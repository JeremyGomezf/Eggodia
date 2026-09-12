using Godot;

public partial class PantallaComoJugar : Control
{
	// La guía es una pantalla de teléfono: agrandamos TODAS las letras para que se lean bien.
	[Export] public float EscalaFuenteMovil = 1.4f;

	public override void _Ready()
	{
		EscalarFuentes(this);

		var btn = GetNodeOrNull<Button>("Root/Header/HBox/BtnVolver");
		if (btn != null)
			btn.Pressed += () => GetTree().ChangeSceneToFile("res://escenas/menu/menu_principal.tscn");
	}

	// Agranda las letras de todos los controles con texto (labels y botones) para móvil.
	private void EscalarFuentes(Node nodo)
	{
		if (nodo is Label || nodo is Button)
		{
			var c = (Control)nodo;
			int actual = c.GetThemeFontSize("font_size");
			if (actual > 0)
				c.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(actual * EscalaFuenteMovil));
		}
		foreach (var hijo in nodo.GetChildren())
			EscalarFuentes(hijo);
	}
}
