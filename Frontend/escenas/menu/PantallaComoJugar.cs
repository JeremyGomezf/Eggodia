using Godot;

public partial class PantallaComoJugar : Control
{
	public override void _Ready()
	{
		var btn = GetNodeOrNull<Button>("Root/Header/HBox/BtnVolver");
		if (btn != null)
			btn.Pressed += () => GetTree().ChangeSceneToFile("res://escenas/menu/menu_principal.tscn");
	}
}
