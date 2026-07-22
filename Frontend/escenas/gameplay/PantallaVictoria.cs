using Godot;

public partial class PantallaVictoria : CanvasLayer
{
	public int DañoInfligido    { get; set; }
	public int TropasEliminadas { get; set; }
	public int TurnosJugados    { get; set; }
	public int Racha            { get; set; }
	public int MonedasGanadas   { get; set; }

	public override void _Ready()
	{
		var lblD  = GetNodeOrNull<Label>("Overlay/VBox/PanelStats/StatsGrid/LblDañoV");
		var lblE  = GetNodeOrNull<Label>("Overlay/VBox/PanelStats/StatsGrid/LblElimV");
		var lblT  = GetNodeOrNull<Label>("Overlay/VBox/PanelStats/StatsGrid/LblTurnosV");
		var lblR  = GetNodeOrNull<Label>("Overlay/VBox/PanelStats/StatsGrid/LblRachaV");
		if (lblD != null) lblD.Text = DañoInfligido.ToString();
		if (lblE != null) lblE.Text = TropasEliminadas.ToString();
		if (lblT != null) lblT.Text = TurnosJugados.ToString();
		if (lblR != null) lblR.Text = Racha > 1 ? $"{Racha} victorias seguidas" : $"{Racha}";

		var btnJugar = GetNodeOrNull<Button>("Overlay/VBox/BtnJugarDeNuevo");
		var btnMenu  = GetNodeOrNull<Button>("Overlay/VBox/BtnMenu");
		if (btnJugar != null) btnJugar.Pressed += () => GetTree().ReloadCurrentScene();
		if (btnMenu  != null) btnMenu.Pressed  += () => GetTree().ChangeSceneToFile("res://escenas/menu/menu_principal.tscn");

		MostrarRecompensa();
		AnimarEntrada();
	}

	private void MostrarRecompensa()
	{
		var vbox = GetNodeOrNull<Control>("Overlay/VBox");
		var panelStats = GetNodeOrNull<Control>("Overlay/VBox/PanelStats");
		if (vbox == null || MonedasGanadas <= 0) return;

		var chip = new PanelContainer();
		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.12f, 0.10f, 0.03f, 0.9f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.BorderColor = new Color(0.95f, 0.78f, 0.25f, 0.85f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 12;
		sb.ContentMarginLeft = sb.ContentMarginRight = 18;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 10;
		chip.AddThemeStyleboxOverride("panel", sb);

		var l = new Label();
		l.Text = $"+ {MonedasGanadas} monedas";
		l.AddThemeColorOverride("font_color", new Color(1f, 0.88f, 0.4f));
		l.AddThemeFontSizeOverride("font_size", 20);
		l.HorizontalAlignment = HorizontalAlignment.Center;
		chip.AddChild(l);

		int idx = panelStats != null ? panelStats.GetIndex() + 1 : 1;
		vbox.AddChild(chip);
		vbox.MoveChild(chip, idx);
	}

	private void AnimarEntrada()
	{
		var vbox = GetNodeOrNull<Control>("Overlay/VBox");
		if (vbox == null) return;
		vbox.Modulate = new Color(1, 1, 1, 0);
		vbox.Scale    = new Vector2(0.85f, 0.85f);
		vbox.PivotOffset = vbox.Size / 2;
		var tw = CreateTween().SetParallel(true);
		tw.TweenProperty(vbox, "modulate:a", 1.0f, 0.5f);
		tw.TweenProperty(vbox, "scale", Vector2.One, 0.55f)
		  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}
}
