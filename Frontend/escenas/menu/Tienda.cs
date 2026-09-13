using Godot;
using System;
using System.Collections.Generic;

public partial class Tienda : Control
{
	[Export] public string RutaMenuPrincipal = "res://escenas/menu/menu_principal.tscn";

	// Mismo ícono/marco de moneda que usa el HUD de menú principal (TopHUD/CoinsPanel) — antes
	// la Tienda usaba el emoji 🪙, ahora usa la imagen real para que se vea igual en todo el juego.
	private const string RUTA_ICONO_MONEDA  = "res://imagenes/MenuNuevo/Moneda_icon.png";
	private const string RUTA_CUADRO_MONEDA = "res://imagenes/MenuNuevo/cuadro_moneda.png";
	private const string RUTA_FUENTE_ALMENDRA = "res://Almendra-Bold.ttf";

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

		// Barra superior — agrandada para móvil: antes botón/textos/moneda quedaban minúsculos.
		var topMargin = new MarginContainer();
		topMargin.SetAnchorsPreset(LayoutPreset.TopWide);
		topMargin.OffsetBottom = 104;
		topMargin.AddThemeConstantOverride("margin_left",   24);
		topMargin.AddThemeConstantOverride("margin_right",  24);
		topMargin.AddThemeConstantOverride("margin_top",    14);
		topMargin.AddThemeConstantOverride("margin_bottom", 14);
		AddChild(topMargin);

		var topBar = new HBoxContainer();
		topBar.AddThemeConstantOverride("separation", 20);
		topMargin.AddChild(topBar);

		var fuenteAlmendra = GD.Load<Font>(RUTA_FUENTE_ALMENDRA);

		var btnVolver = new Button();
		btnVolver.Text = "← VOLVER";
		btnVolver.CustomMinimumSize = new Vector2(200, 78);
		if (fuenteAlmendra != null) btnVolver.AddThemeFontOverride("font", fuenteAlmendra);
		btnVolver.AddThemeFontSizeOverride("font_size", 26);
		btnVolver.Pressed += () => GetTree().ChangeSceneToFile(RutaMenuPrincipal);
		topBar.AddChild(btnVolver);

		var lblTitulo = new Label();
		lblTitulo.Text = "TIENDA";
		lblTitulo.AddThemeColorOverride("font_color", new Color(1.1f, 0.9f, 0.4f));
		lblTitulo.AddThemeColorOverride("font_shadow_color", new Color(1f, 0.83f, 0.28f, 0.55f));
		lblTitulo.AddThemeConstantOverride("shadow_offset_x", 0);
		lblTitulo.AddThemeConstantOverride("shadow_offset_y", 0);
		lblTitulo.AddThemeConstantOverride("shadow_outline_size", 10); // efecto de brillo suave
		if (fuenteAlmendra != null) lblTitulo.AddThemeFontOverride("font", fuenteAlmendra);
		lblTitulo.AddThemeFontSizeOverride("font_size", 50);
		lblTitulo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		lblTitulo.HorizontalAlignment = HorizontalAlignment.Center;
		lblTitulo.VerticalAlignment   = VerticalAlignment.Center;
		topBar.AddChild(lblTitulo);

		var chipMonedas = new TextureRect();
		chipMonedas.CustomMinimumSize = new Vector2(210, 74);
		chipMonedas.Texture = GD.Load<Texture2D>(RUTA_CUADRO_MONEDA);
		chipMonedas.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		chipMonedas.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;

		var hboxMonedas = new HBoxContainer();
		hboxMonedas.SetAnchorsPreset(LayoutPreset.FullRect);
		hboxMonedas.OffsetLeft = 16; hboxMonedas.OffsetRight = -16;
		hboxMonedas.GrowHorizontal = GrowDirection.Both;
		hboxMonedas.GrowVertical   = GrowDirection.Both;
		hboxMonedas.Alignment = BoxContainer.AlignmentMode.Center;
		hboxMonedas.AddThemeConstantOverride("separation", 10);
		chipMonedas.AddChild(hboxMonedas);

		var iconoMoneda = new TextureRect();
		iconoMoneda.CustomMinimumSize = new Vector2(38, 28);
		iconoMoneda.Texture = GD.Load<Texture2D>(RUTA_ICONO_MONEDA);
		iconoMoneda.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		iconoMoneda.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		iconoMoneda.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		hboxMonedas.AddChild(iconoMoneda);

		_lblMonedas = new Label();
		_lblMonedas.Text = "0";
		_lblMonedas.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.45f));
		if (fuenteAlmendra != null) _lblMonedas.AddThemeFontOverride("font", fuenteAlmendra);
		_lblMonedas.AddThemeFontSizeOverride("font_size", 34);
		_lblMonedas.VerticalAlignment = VerticalAlignment.Center;
		hboxMonedas.AddChild(_lblMonedas);
		topBar.AddChild(chipMonedas);

		// Scroll container principal
		var scroll = new ScrollContainer();
		scroll.SetAnchorsPreset(LayoutPreset.FullRect);
		scroll.OffsetTop = 112; scroll.OffsetLeft = 24;
		scroll.OffsetRight = -24; scroll.OffsetBottom = -16;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		AddChild(scroll);

		_contenidoScroll = new VBoxContainer();
		_contenidoScroll.AddThemeConstantOverride("separation", 32);
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
		lbl.AddThemeFontSizeOverride("font_size", 26);
		lbl.AddThemeConstantOverride("outline_size", 4);
		lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
		_contenidoScroll.AddChild(lbl);
		var sep = new HSeparator(); _contenidoScroll.AddChild(sep);
	}

	private void AgregarGridSkins()
	{
		// Fila continua [huevo][huevo][huevo]... que envuelve sola al llegar al borde (en vez de
		// una grilla de columnas fijas) — HFlowContainer calcula cuántas entran por fila según el
		// ancho disponible y baja el resto solo, como pediste.
		var flow = new HFlowContainer();
		flow.AddThemeConstantOverride("h_separation", 20);
		flow.AddThemeConstantOverride("v_separation", 20);
		flow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_contenidoScroll.AddChild(flow);

		for (int i = 0; i < Preferencias.SKIN_NOMBRES.Length; i++)
			flow.AddChild(CrearItemSkin(i));
	}

	private void AgregarGridSkinsCartas()
	{
		var grid = new GridContainer();
		grid.Columns = 3;
		grid.AddThemeConstantOverride("h_separation", 18);
		grid.AddThemeConstantOverride("v_separation", 18);
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
		panel.CustomMinimumSize = new Vector2(230, 320);

		var sbNormal = new StyleBoxFlat();
		sbNormal.BgColor = activa
			? new Color(0.14f, 0.22f, 0.10f, 0.97f)
			: new Color(0.09f, 0.11f, 0.22f, 0.96f);
		sbNormal.BorderWidthLeft = sbNormal.BorderWidthTop = sbNormal.BorderWidthRight = sbNormal.BorderWidthBottom = 2;
		sbNormal.BorderColor = activa ? new Color(0.4f, 1f, 0.4f) : new Color(0.70f, 0.55f, 0.25f);
		sbNormal.CornerRadiusTopLeft = sbNormal.CornerRadiusTopRight =
		sbNormal.CornerRadiusBottomLeft = sbNormal.CornerRadiusBottomRight = 12;
		sbNormal.ContentMarginLeft = sbNormal.ContentMarginRight =
		sbNormal.ContentMarginTop  = sbNormal.ContentMarginBottom = 14;
		sbNormal.ShadowColor = new Color(0,0,0,0.4f); sbNormal.ShadowSize = 6;
		panel.AddThemeStyleboxOverride("panel", sbNormal);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 10);

		// Imagen del personaje
		var tex = new TextureRect();
		tex.CustomMinimumSize = new Vector2(180, 180);
		tex.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		tex.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
		tex.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		var textura = GD.Load<Texture2D>(Preferencias.SKIN_IMAGENES[idx]);
		if (textura != null) tex.Texture = textura;
		vbox.AddChild(tex);

		var lblNombre = new Label();
		lblNombre.Text = Preferencias.SKIN_NOMBRES[idx];
		lblNombre.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.80f));
		lblNombre.AddThemeFontSizeOverride("font_size", 18);
		lblNombre.HorizontalAlignment = HorizontalAlignment.Center;
		lblNombre.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(lblNombre);

		if (activa)
		{
			var lblActiva = new Label();
			lblActiva.Text = "✓ EQUIPADA";
			lblActiva.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.45f));
			lblActiva.AddThemeFontSizeOverride("font_size", 17);
			lblActiva.HorizontalAlignment = HorizontalAlignment.Center;
			vbox.AddChild(lblActiva);
		}
		else if (poseida)
		{
			var btnEquipar = new Button();
			btnEquipar.Text = "EQUIPAR";
			btnEquipar.CustomMinimumSize = new Vector2(0, 56);
			btnEquipar.AddThemeFontSizeOverride("font_size", 18);
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
			btnEquipar.CustomMinimumSize = new Vector2(0, 56);
			btnEquipar.AddThemeFontSizeOverride("font_size", 18);
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
			btnComprar.Text = precio.ToString();
			btnComprar.Icon = GD.Load<Texture2D>(RUTA_ICONO_MONEDA);
			btnComprar.ExpandIcon = false;
			btnComprar.CustomMinimumSize = new Vector2(0, 56);
			btnComprar.AddThemeFontSizeOverride("font_size", 18);
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
		panel.CustomMinimumSize = new Vector2(260, 320);

		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.09f, 0.11f, 0.22f, 0.96f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.BorderColor = new Color(0.45f, 0.45f, 0.55f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 12;
		sb.ContentMarginLeft = sb.ContentMarginRight =
		sb.ContentMarginTop  = sb.ContentMarginBottom = 14;
		sb.ShadowColor = new Color(0, 0, 0, 0.35f); sb.ShadowSize = 6;
		panel.AddThemeStyleboxOverride("panel", sb);
		panel.Modulate = new Color(0.6f, 0.6f, 0.6f);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 10);

		// Intentar cargar imagen de la escena como ícono (muestra 🃏 si no hay imagen)
		var lblIcono = new Label();
		lblIcono.Text = "🃏";
		lblIcono.AddThemeFontSizeOverride("font_size", 70);
		lblIcono.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(lblIcono);

		var lblNombre = new Label();
		lblNombre.Text = nombre;
		lblNombre.AddThemeColorOverride("font_color", new Color(0.80f, 0.80f, 0.85f));
		lblNombre.AddThemeFontSizeOverride("font_size", 19);
		lblNombre.HorizontalAlignment = HorizontalAlignment.Center;
		lblNombre.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(lblNombre);

		var lblNoDisp = new Label();
		lblNoDisp.Text = "🔒 NO DISPONIBLE";
		lblNoDisp.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.35f));
		lblNoDisp.AddThemeFontSizeOverride("font_size", 18);
		lblNoDisp.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(lblNoDisp);

		var lblProxi = new Label();
		lblProxi.Text = "Próximamente";
		lblProxi.AddThemeColorOverride("font_color", new Color(0.55f, 0.65f, 0.75f));
		lblProxi.AddThemeFontSizeOverride("font_size", 15);
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
		lbl.AddThemeFontSizeOverride("font_size", 28);
		lbl.AddThemeConstantOverride("outline_size", 5);
		lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
		lbl.SetAnchorsPreset(LayoutPreset.Center);
		lbl.OffsetLeft = -260; lbl.OffsetRight = 260;
		lbl.OffsetTop  = -36;  lbl.OffsetBottom = 36;
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
		if (_lblMonedas != null) _lblMonedas.Text = total.ToString();
	}
}
