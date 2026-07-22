using Godot;

public partial class PantallaComoJugar : Control
{
	public override void _Ready()
	{
		var btn = GetNodeOrNull<Button>("Root/Header/HBox/BtnVolver");
		if (btn != null)
			btn.Pressed += () => GetTree().ChangeSceneToFile("res://escenas/menu/menu_principal.tscn");

		var chips = GetNodeOrNull<HBoxContainer>("Root/Scroll/Margin/Contenido/Grid/Card3/VBox/ChipsRow");
		if (chips != null)
		{
			foreach (string t in new[] { Tipos.FUEGO, Tipos.AGUA, Tipos.NATURALEZA, Tipos.METAL, Tipos.SOMBRA })
				chips.AddChild(MakeChip(t));
		}
	}

	private Control MakeChip(string tipo)
	{
		var chip = new PanelContainer();
		chip.CustomMinimumSize = new Vector2(84, 26);
		var sb = new StyleBoxFlat();
		sb.BgColor = Tipos.Color(tipo);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 8;
		sb.ContentMarginLeft = sb.ContentMarginRight = 6;
		chip.AddThemeStyleboxOverride("panel", sb);
		var l = new Label();
		l.Text = Tipos.Etiqueta(tipo);
		l.AddThemeColorOverride("font_color", Colors.White);
		l.AddThemeFontSizeOverride("font_size", 12);
		l.HorizontalAlignment = HorizontalAlignment.Center;
		l.VerticalAlignment   = VerticalAlignment.Center;
		chip.AddChild(l);
		return chip;
	}
}
