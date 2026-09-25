using Godot;
using System;

public partial class PantallaDerrota : CanvasLayer
{
	public int MonedasGanadas { get; set; }
	public int DañoInfligido  { get; set; }
	public int BajasEnemigas  { get; set; }

	public string    MvtNombre      { get; set; }
	public int       MvtDaño        { get; set; }
	public Texture2D MvtIlustracion { get; set; }

	public override void _Ready()
	{
		var btnReintentar = GetNodeOrNull<Button>("Overlay/CentroVBox/VBox/BtnReintentar");
		var btnMenu       = GetNodeOrNull<Button>("Overlay/CentroVBox/VBox/BtnMenu");

		MostrarRecompensa();
		MostrarStats();
		MostrarMVT();

		if (btnReintentar != null)
			btnReintentar.Pressed += () =>
			{
				LimpiezaEfectos.LimpiarEfectosDeCampo();
				GetTree().Paused = false;
				GetTree().ChangeSceneToFile("res://escenas/menu/MenuConstructor.tscn");
			};

		if (btnMenu != null)
			btnMenu.Pressed += () =>
			{
				LimpiezaEfectos.LimpiarEfectosDeCampo();
				GetTree().Paused = false;
				GetTree().ChangeSceneToFile("res://escenas/menu/menu_principal.tscn");
			};

		// Estilo del juego: botones, marcos (paneles) y título (en rojo) con nuestra paleta/fuente.
		EstiloUI.Boton(btnReintentar);
		EstiloUI.Boton(btnMenu);
		EstiloUI.Texto(GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/Titulo"), 0, EstiloUI.Peligro);
		var pStats = GetNodeOrNull<PanelContainer>("Overlay/CentroVBox/VBox/PanelStats");
		if (pStats != null) pStats.AddThemeStyleboxOverride("panel", EstiloUI.CuadroDorado());
		var pMvt = GetNodeOrNull<PanelContainer>("Overlay/CentroVBox/VBox/PanelMVT");
		if (pMvt != null) pMvt.AddThemeStyleboxOverride("panel", EstiloUI.CuadroDorado());

		// Animación de aparición
		var overlay = GetNode<ColorRect>("Overlay");
		overlay.Modulate = new Color(1, 1, 1, 0);
		Tween tw = CreateTween();
		tw.TweenProperty(overlay, "modulate:a", 1.0f, 1.5f);

		CallDeferred(nameof(AjustarParticulasAnchoPantalla));
	}

	// La lluvia usaba ancho fijo (±700 px) centrada en x=640: en pantallas anchas ("keep_height")
	// solo cubría parte. Se recalcula al ancho REAL visible para que caiga en toda la pantalla.
	private void AjustarParticulasAnchoPantalla()
	{
		var p = GetNodeOrNull<CpuParticles2D>("Overlay/Lluvia");
		if (p == null) return;
		var overlay = GetNodeOrNull<Control>("Overlay");
		float w = (overlay != null && overlay.Size.X > 100f) ? overlay.Size.X : GetViewport().GetVisibleRect().Size.X;
		if (w < 100f) w = 1080f;
		p.Position = new Vector2(w / 2f, p.Position.Y);
		p.EmissionRectExtents = new Vector2(w / 2f + 140f, p.EmissionRectExtents.Y);
		p.Amount = Mathf.Max(p.Amount, (int)(w / 8f));
	}

	private void MostrarStats()
	{
		var lblD = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelStats/StatsGrid/LblDañoV");
		var lblE = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelStats/StatsGrid/LblElimV");
		if (lblD != null) lblD.Text = DañoInfligido.ToString();
		if (lblE != null) lblE.Text = BajasEnemigas.ToString();
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

	private void MostrarRecompensa()
	{
		if (MonedasGanadas <= 0) return;
		var vbox = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox");
		var mensaje = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox/Mensaje");
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
