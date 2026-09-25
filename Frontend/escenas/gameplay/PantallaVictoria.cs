using Godot;

public partial class PantallaVictoria : CanvasLayer
{
	public int DañoInfligido    { get; set; }
	public int TropasEliminadas { get; set; }
	public int TurnosJugados    { get; set; }
	public int Racha            { get; set; }
	public int MonedasGanadas   { get; set; }

	public string    MvtNombre      { get; set; }
	public int       MvtDaño        { get; set; }
	public Texture2D MvtIlustracion { get; set; }

	public override void _Ready()
	{
		var lblD  = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelStats/StatsGrid/LblDañoV");
		var lblE  = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelStats/StatsGrid/LblElimV");
		var lblT  = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelStats/StatsGrid/LblTurnosV");
		var lblR  = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelStats/StatsGrid/LblRachaV");
		if (lblD != null) lblD.Text = DañoInfligido.ToString();
		if (lblE != null) lblE.Text = TropasEliminadas.ToString();
		if (lblT != null) lblT.Text = TurnosJugados.ToString();
		if (lblR != null) lblR.Text = Racha > 1 ? $"{Racha} victorias seguidas" : $"{Racha}";

		MostrarMVT();

		var btnJugar = GetNodeOrNull<Button>("Overlay/CentroVBox/VBox/BtnJugarDeNuevo");
		var btnMenu  = GetNodeOrNull<Button>("Overlay/CentroVBox/VBox/BtnMenu");
		if (btnJugar != null) btnJugar.Pressed += () => { LimpiezaEfectos.LimpiarEfectosDeCampo(); GetTree().ReloadCurrentScene(); };
		if (btnMenu  != null) btnMenu.Pressed  += () => { LimpiezaEfectos.LimpiarEfectosDeCampo(); GetTree().ChangeSceneToFile("res://escenas/menu/menu_principal.tscn"); };

		// Estilo del juego: botones, marcos (paneles) y título con nuestra paleta/fuente.
		EstiloUI.Boton(btnJugar);
		EstiloUI.Boton(btnMenu);
		EstiloUI.Titulo(GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/Titulo"), 0);
		var pStats = GetNodeOrNull<PanelContainer>("Overlay/CentroVBox/VBox/PanelStats");
		if (pStats != null) pStats.AddThemeStyleboxOverride("panel", EstiloUI.CuadroDorado());
		var pMvt = GetNodeOrNull<PanelContainer>("Overlay/CentroVBox/VBox/PanelMVT");
		if (pMvt != null) pMvt.AddThemeStyleboxOverride("panel", EstiloUI.CuadroDorado());

		MostrarRecompensa();
		AnimarEntrada();
		CallDeferred(nameof(AjustarParticulasAnchoPantalla));
	}

	// El confeti/lluvia usaba un ancho fijo (±700 px) centrado en x=640, así que en pantallas anchas
	// (el juego usa "keep_height", el ancho visible cambia por dispositivo) solo cubría una parte.
	// Aquí se recalcula al ancho REAL visible para que caiga en toda la pantalla, en cualquier celular.
	private void AjustarParticulasAnchoPantalla()
	{
		var p = GetNodeOrNull<CpuParticles2D>("Overlay/Confetti");
		if (p == null) return;
		var overlay = GetNodeOrNull<Control>("Overlay");
		float w = (overlay != null && overlay.Size.X > 100f) ? overlay.Size.X : GetViewport().GetVisibleRect().Size.X;
		if (w < 100f) w = 1080f;
		p.Position = new Vector2(w / 2f, p.Position.Y);
		p.EmissionRectExtents = new Vector2(w / 2f + 140f, p.EmissionRectExtents.Y);
		p.Amount = Mathf.Max(p.Amount, (int)(w / 10f)); // densidad acorde al ancho
	}

	private void MostrarRecompensa()
	{
		var vbox = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox");
		var panelStats = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox/PanelStats");
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

	private void MostrarMVT()
	{
		if (string.IsNullOrEmpty(MvtNombre)) return;
		var panel = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox/PanelMVT");
		if (panel == null) return;
		panel.Visible = true;

		var lblNombre = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelMVT/MVTBox/MVTInfo/MVTNombre");
		var lblStat   = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelMVT/MVTBox/MVTInfo/MVTStat");
		var foto      = GetNodeOrNull<TextureRect>("Overlay/CentroVBox/VBox/PanelMVT/MVTBox/MVTFoto");
		if (lblNombre != null) lblNombre.Text = MvtNombre;
		if (lblStat   != null) lblStat.Text   = $"{MvtDaño} de daño causado";
		if (foto != null && MvtIlustracion != null) foto.Texture = MvtIlustracion;
	}

	private void AnimarEntrada()
	{
		var vbox = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox");
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
