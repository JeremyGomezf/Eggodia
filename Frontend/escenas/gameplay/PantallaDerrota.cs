using Godot;
using System;

public partial class PantallaDerrota : CanvasLayer
{
	public int MonedasGanadas { get; set; }

	public override void _Ready()
	{
		var btnReintentar = GetNodeOrNull<Button>("Overlay/VBox/BtnReintentar");
		var btnMenu       = GetNodeOrNull<Button>("Overlay/VBox/BtnMenu");

		MostrarRecompensa();

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

	private void MostrarRecompensa()
	{
		if (MonedasGanadas <= 0) return;
		var vbox = GetNodeOrNull<Control>("Overlay/VBox");
		var mensaje = GetNodeOrNull<Control>("Overlay/VBox/Mensaje");
		if (vbox == null) return;

		var chip = new PanelContainer();
		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.12f, 0.10f, 0.03f, 0.9f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.BorderColor = new Color(0.85f, 0.72f, 0.3f, 0.7f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 12;
		sb.ContentMarginLeft = sb.ContentMarginRight = 18;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 8;
		chip.AddThemeStyleboxOverride("panel", sb);
		chip.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

		var l = new Label();
		l.Text = $"+ {MonedasGanadas} monedas";
		l.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.45f));
		l.AddThemeFontSizeOverride("font_size", 18);
		l.HorizontalAlignment = HorizontalAlignment.Center;
		chip.AddChild(l);

		int idx = mensaje != null ? mensaje.GetIndex() + 1 : 1;
		vbox.AddChild(chip);
		vbox.MoveChild(chip, idx);
	}
}
