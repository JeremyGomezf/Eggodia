using Godot;
using System;
using System.Collections.Generic;

public partial class Tienda : Control
{
	[Export] public string RutaMenuPrincipal = "res://escenas/menu/menu_principal.tscn";

	private Label _lblMonedas;

	// Catálogo de ítems (nombre, descripción, precio, icono)
	private static readonly (string nombre, string desc, int precio, string icono)[] ITEMS = {
		("Hechizo Extra",     "+1 uso de hechizo por partida",    150, "✦"),
		("Baraja Especial",   "Cartas raras en tu mazo",          300, "🃏"),
		("Amuleto de Vida",   "+200 HP al inicio de partida",     200, "❤"),
		("Energía Adicional", "+1 movimiento por turno",          400, "⚡"),
		("Carta Legendaria",  "Invoca una tropa legendaria",      500, "⭐"),
		("Escudo Mágico",     "Absorbe el primer daño recibido",  250, "🛡"),
	};

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
		// Fondo oscuro
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

		var sep = new HSeparator();
		sep.SetAnchorsPreset(LayoutPreset.TopWide);
		sep.OffsetTop = 64; sep.OffsetBottom = 66;
		AddChild(sep);

		// Grid con scroll
		var scroll = new ScrollContainer();
		scroll.SetAnchorsPreset(LayoutPreset.FullRect);
		scroll.OffsetTop = 72; scroll.OffsetLeft = 24;
		scroll.OffsetRight = -24; scroll.OffsetBottom = -20;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		AddChild(scroll);

		var grid = new GridContainer();
		grid.Columns = 3;
		grid.AddThemeConstantOverride("h_separation", 16);
		grid.AddThemeConstantOverride("v_separation", 16);
		grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(grid);

		foreach (var item in ITEMS)
			grid.AddChild(CrearItemTienda(item.nombre, item.desc, item.precio, item.icono));
	}

	private Control CrearItemTienda(string nombre, string desc, int precio, string icono)
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(200, 185);

		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.09f, 0.11f, 0.22f, 0.96f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.BorderColor = new Color(0.35f, 0.55f, 0.90f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 10;
		sb.ContentMarginLeft = sb.ContentMarginRight =
		sb.ContentMarginTop  = sb.ContentMarginBottom = 12;
		sb.ShadowColor = new Color(0, 0, 0, 0.35f); sb.ShadowSize = 5;
		panel.AddThemeStyleboxOverride("panel", sb);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 8);

		var lblIcono = new Label();
		lblIcono.Text = icono;
		lblIcono.AddThemeFontSizeOverride("font_size", 38);
		lblIcono.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(lblIcono);

		var lblNombre = new Label();
		lblNombre.Text = nombre;
		lblNombre.AddThemeColorOverride("font_color", new Color(0.92f, 0.94f, 1f));
		lblNombre.AddThemeFontSizeOverride("font_size", 14);
		lblNombre.HorizontalAlignment = HorizontalAlignment.Center;
		lblNombre.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(lblNombre);

		var lblDesc = new Label();
		lblDesc.Text = desc;
		lblDesc.AddThemeColorOverride("font_color", new Color(0.62f, 0.70f, 0.85f));
		lblDesc.AddThemeFontSizeOverride("font_size", 11);
		lblDesc.HorizontalAlignment = HorizontalAlignment.Center;
		lblDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		lblDesc.SizeFlagsVertical = SizeFlags.ExpandFill;
		vbox.AddChild(lblDesc);

		var btnComprar = new Button();
		btnComprar.Text = $"🪙 {precio}";
		btnComprar.CustomMinimumSize = new Vector2(0, 36);
		btnComprar.AddThemeFontSizeOverride("font_size", 14);
		var btnCaptura = btnComprar;
		btnComprar.Pressed += () => IntentarCompra(nombre, precio, btnCaptura);
		vbox.AddChild(btnComprar);

		panel.AddChild(vbox);
		return panel;
	}

	private void IntentarCompra(string nombre, int precio, Button btn)
	{
		var eco = Economia.Instancia();
		if (eco == null) return;

		if (!eco.Gastar(precio))
		{
			MostrarMensaje("Monedas insuficientes", new Color(1f, 0.45f, 0.35f));
			return;
		}

		btn.Text     = "✓ Comprado";
		btn.Disabled = true;
		MostrarMensaje($"¡{nombre} adquirido!", Colors.Gold);
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
