using Godot;
using System;
using System.Collections.Generic;

public partial class Tienda : Control
{
	[Export] public string RutaMenuPrincipal = "res://escenas/menu/menu_principal.tscn";

	private Label _lblMonedas;
	private VBoxContainer _contenidoScroll;


	public override void _Ready()
	{
		ConstruirUI();
		var eco = Economia.Instancia();
		if (eco != null)
		{
			eco.MonedasCambiaron += ActualizarMonedas;
			ActualizarMonedas(eco.Monedas);
		}
	}

	public override void _ExitTree()
	{
		if (Economia.Instance != null)
			Economia.Instance.MonedasCambiaron -= ActualizarMonedas;
	}

	private void ConstruirUI()
	{
		var fondo = new ColorRect();
		fondo.SetAnchorsPreset(LayoutPreset.FullRect);
		fondo.Color = new Color(0.04f, 0.05f, 0.13f);
		AddChild(fondo);

		// Barra superior
		var topMargin = new MarginContainer();
		topMargin.SetAnchorsPreset(LayoutPreset.TopWide);
		topMargin.OffsetBottom = 64;
		topMargin.AddThemeConstantOverride("margin_left",   20);
		topMargin.AddThemeConstantOverride("margin_right",  20);
		topMargin.AddThemeConstantOverride("margin_top",    10);
		topMargin.AddThemeConstantOverride("margin_bottom", 10);
		AddChild(topMargin);

		var topBar = new HBoxContainer();
		topBar.AddThemeConstantOverride("separation", 16);
		topMargin.AddChild(topBar);

		var btnVolver = new Button();
		btnVolver.Text = "← VOLVER";
		btnVolver.CustomMinimumSize = new Vector2(130, 40);
		btnVolver.AddThemeFontSizeOverride("font_size", 15);
		btnVolver.Pressed += () => GetTree().ChangeSceneToFile(RutaMenuPrincipal);
		topBar.AddChild(btnVolver);

		var lblTitulo = new Label();
		lblTitulo.Text = "TIENDA";
		lblTitulo.AddThemeColorOverride("font_color", new Color(1f, 0.83f, 0.28f));
		lblTitulo.AddThemeFontSizeOverride("font_size", 28);
		lblTitulo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		lblTitulo.HorizontalAlignment = HorizontalAlignment.Center;
		lblTitulo.VerticalAlignment   = VerticalAlignment.Center;
		topBar.AddChild(lblTitulo);

		_lblMonedas = new Label();
		_lblMonedas.Text = "🪙 0";
		_lblMonedas.AddThemeColorOverride("font_color", new Color(1f, 0.83f, 0.28f));
		_lblMonedas.AddThemeFontSizeOverride("font_size", 18);
		_lblMonedas.VerticalAlignment = VerticalAlignment.Center;
		topBar.AddChild(_lblMonedas);

		// Scroll container principal
		var scroll = new ScrollContainer();
		scroll.SetAnchorsPreset(LayoutPreset.FullRect);
		scroll.OffsetTop = 72; scroll.OffsetLeft = 16;
		scroll.OffsetRight = -16; scroll.OffsetBottom = -12;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		AddChild(scroll);

		_contenidoScroll = new VBoxContainer();
		_contenidoScroll.AddThemeConstantOverride("separation", 24);
		_contenidoScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(_contenidoScroll);

		// Sección 1: Skins de huevo
		AgregarSeccion("SKINS DE HUEVO 🥚", new Color(1f, 0.65f, 0.25f));
		AgregarGridSkins();

		// Sección 2: Skins de cartas
		AgregarSeccion("SKINS DE CARTAS 🃏", new Color(0.55f, 0.85f, 1f));
		AgregarGridSkinsCartas();
	}

	private void AgregarSeccion(string titulo, Color color)
	{
		var lbl = new Label();
		lbl.Text = titulo;
		lbl.AddThemeColorOverride("font_color", color);
		lbl.AddThemeFontSizeOverride("font_size", 18);
		lbl.AddThemeConstantOverride("outline_size", 3);
		lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
		_contenidoScroll.AddChild(lbl);
		var sep = new HSeparator(); _contenidoScroll.AddChild(sep);
	}

	private void AgregarGridSkins()
	{
		var grid = new GridContainer();
		grid.Columns = 4;
		grid.AddThemeConstantOverride("h_separation", 12);
		grid.AddThemeConstantOverride("v_separation", 12);
		grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_contenidoScroll.AddChild(grid);

		for (int i = 0; i < Preferencias.SKIN_NOMBRES.Length; i++)
			grid.AddChild(CrearItemSkin(i));
	}

	private void AgregarGridSkinsCartas()
	{
		var grid = new GridContainer();
		grid.Columns = 3;
		grid.AddThemeConstantOverride("h_separation", 16);
		grid.AddThemeConstantOverride("v_separation", 16);
		grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_contenidoScroll.AddChild(grid);
		grid.AddChild(CrearItemCartaNoDisponible("T-Rex Prime", "res://cartas prime/PAPEL/TRex_prime.tscn"));
	}

	private Control CrearItemSkin(int idx)
	{
		bool esDefault = idx == 0;
		bool poseida   = Preferencias.TieneSkin(idx);
		bool activa    = Preferencias.SkinActivaIdx == idx;

		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(160, 220);

		var sbNormal = new StyleBoxFlat();
		sbNormal.BgColor = activa
			? new Color(0.14f, 0.22f, 0.10f, 0.97f)
			: new Color(0.09f, 0.11f, 0.22f, 0.96f);
		sbNormal.BorderWidthLeft = sbNormal.BorderWidthTop = sbNormal.BorderWidthRight = sbNormal.BorderWidthBottom = 2;
		sbNormal.BorderColor = activa ? new Color(0.4f, 1f, 0.4f) : new Color(0.70f, 0.55f, 0.25f);
		sbNormal.CornerRadiusTopLeft = sbNormal.CornerRadiusTopRight =
		sbNormal.CornerRadiusBottomLeft = sbNormal.CornerRadiusBottomRight = 10;
		sbNormal.ContentMarginLeft = sbNormal.ContentMarginRight =
		sbNormal.ContentMarginTop  = sbNormal.ContentMarginBottom = 10;
		sbNormal.ShadowColor = new Color(0,0,0,0.4f); sbNormal.ShadowSize = 5;
		panel.AddThemeStyleboxOverride("panel", sbNormal);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);

		// Imagen del personaje
		var tex = new TextureRect();
		tex.CustomMinimumSize = new Vector2(120, 120);
		tex.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		tex.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
		tex.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		var textura = GD.Load<Texture2D>(Preferencias.SKIN_IMAGENES[idx]);
		if (textura != null) tex.Texture = textura;
		vbox.AddChild(tex);

		var lblNombre = new Label();
		lblNombre.Text = Preferencias.SKIN_NOMBRES[idx];
		lblNombre.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.80f));
		lblNombre.AddThemeFontSizeOverride("font_size", 13);
		lblNombre.HorizontalAlignment = HorizontalAlignment.Center;
		lblNombre.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(lblNombre);

		if (activa)
		{
			var lblActiva = new Label();
			lblActiva.Text = "✓ EQUIPADA";
			lblActiva.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.45f));
			lblActiva.AddThemeFontSizeOverride("font_size", 12);
			lblActiva.HorizontalAlignment = HorizontalAlignment.Center;
			vbox.AddChild(lblActiva);
		}
		else if (poseida)
		{
			var btnEquipar = new Button();
			btnEquipar.Text = "EQUIPAR";
			btnEquipar.CustomMinimumSize = new Vector2(0, 34);
			btnEquipar.AddThemeFontSizeOverride("font_size", 13);
			btnEquipar.Pressed += () => {
				Preferencias.SkinActivaIdx = idx;
				GetTree().ReloadCurrentScene();
			};
			vbox.AddChild(btnEquipar);
		}
		else if (esDefault)
		{
			var btnEquipar = new Button();
			btnEquipar.Text = "EQUIPAR";
			btnEquipar.CustomMinimumSize = new Vector2(0, 34);
			btnEquipar.AddThemeFontSizeOverride("font_size", 13);
			btnEquipar.Pressed += () => {
				Preferencias.SkinActivaIdx = 0;
				GetTree().ReloadCurrentScene();
			};
			vbox.AddChild(btnEquipar);
		}
		else
		{
			int precio = Preferencias.SKIN_PRECIOS[idx];
			var btnComprar = new Button();
			btnComprar.Text = $"🪙 {precio}";
			btnComprar.CustomMinimumSize = new Vector2(0, 34);
			btnComprar.AddThemeFontSizeOverride("font_size", 13);
			int capIdx = idx;
			btnComprar.Pressed += () => IntentarComprarSkin(capIdx, btnComprar);
			vbox.AddChild(btnComprar);
		}

		panel.AddChild(vbox);
		return panel;
	}

	private void IntentarComprarSkin(int idx, Button btn)
	{
		var eco = Economia.Instancia();
		if (eco == null) return;
		int precio = Preferencias.SKIN_PRECIOS[idx];
		if (!eco.Gastar(precio))
		{ MostrarMensaje("Monedas insuficientes", new Color(1f, 0.45f, 0.35f)); return; }

		Preferencias.DesbloquearSkin(idx);
		Preferencias.SkinActivaIdx = idx;
		MostrarMensaje($"¡{Preferencias.SKIN_NOMBRES[idx]} desbloqueada!", Colors.Gold);
		GetTree().CreateTimer(1.2f).Timeout += () => GetTree().ReloadCurrentScene();
	}

	private Control CrearItemCartaNoDisponible(string nombre, string rutaEscena)
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(200, 220);

		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.09f, 0.11f, 0.22f, 0.96f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.BorderColor = new Color(0.45f, 0.45f, 0.55f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 10;
		sb.ContentMarginLeft = sb.ContentMarginRight =
		sb.ContentMarginTop  = sb.ContentMarginBottom = 12;
		sb.ShadowColor = new Color(0, 0, 0, 0.35f); sb.ShadowSize = 5;
		panel.AddThemeStyleboxOverride("panel", sb);
		panel.Modulate = new Color(0.6f, 0.6f, 0.6f);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 8);

		// Intentar cargar imagen de la escena como ícono (muestra 🃏 si no hay imagen)
		var lblIcono = new Label();
		lblIcono.Text = "🃏";
		lblIcono.AddThemeFontSizeOverride("font_size", 50);
		lblIcono.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(lblIcono);

		var lblNombre = new Label();
		lblNombre.Text = nombre;
		lblNombre.AddThemeColorOverride("font_color", new Color(0.80f, 0.80f, 0.85f));
		lblNombre.AddThemeFontSizeOverride("font_size", 14);
		lblNombre.HorizontalAlignment = HorizontalAlignment.Center;
		lblNombre.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(lblNombre);

		var lblNoDisp = new Label();
		lblNoDisp.Text = "🔒 NO DISPONIBLE";
		lblNoDisp.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.35f));
		lblNoDisp.AddThemeFontSizeOverride("font_size", 13);
		lblNoDisp.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(lblNoDisp);

		var lblProxi = new Label();
		lblProxi.Text = "Próximamente";
		lblProxi.AddThemeColorOverride("font_color", new Color(0.55f, 0.65f, 0.75f));
		lblProxi.AddThemeFontSizeOverride("font_size", 11);
		lblProxi.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(lblProxi);

		panel.AddChild(vbox);
		return panel;
	}

	private async void MostrarMensaje(string texto, Color color)
	{
		var lbl = new Label();
		lbl.Text = texto;
		lbl.AddThemeColorOverride("font_color", color);
		lbl.AddThemeFontSizeOverride("font_size", 20);
		lbl.AddThemeConstantOverride("outline_size", 4);
		lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
		lbl.SetAnchorsPreset(LayoutPreset.Center);
		lbl.OffsetLeft = -220; lbl.OffsetRight = 220;
		lbl.OffsetTop  = -30;  lbl.OffsetBottom = 30;
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.ZIndex = 100;
		AddChild(lbl);
		var tw = lbl.CreateTween();
		tw.TweenProperty(lbl, "modulate:a", 0f, 1.4f).SetDelay(0.8f);
		await ToSignal(tw, "finished");
		if (IsInstanceValid(lbl)) lbl.QueueFree();
	}

	private void ActualizarMonedas(int total)
	{
		if (_lblMonedas != null) _lblMonedas.Text = $"🪙 {total}";
	}
}
