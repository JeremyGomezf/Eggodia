using Godot;

/// <summary>Interfaz de BtnOnline — pantalla "CARGANDO" con el mismo fondo de la pantalla de
/// carga inicial. IMPORTANTE: el juego todavía no tiene un servidor de matchmaking real, así que
/// tras la búsqueda simulada siempre termina en "SIN CONEXIÓN" — queda preparada para conectarse
/// a un backend real más adelante (reemplazar el timer por la llamada de red real).</summary>
public partial class MenuPrincipal : Control
{
	private CanvasLayer _capaOnline;

	private void MostrarPantallaOnline()
	{
		if (_capaOnline != null && IsInstanceValid(_capaOnline)) _capaOnline.QueueFree();

		var capa = new CanvasLayer();
		capa.Layer = 300;
		AddChild(capa);
		_capaOnline = capa;

		var fondo = new TextureRect();
		fondo.SetAnchorsPreset(LayoutPreset.FullRect);
		fondo.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
		fondo.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
		var texFondo = GD.Load<Texture2D>("res://imagenes/pantalladecargaprueba.png");
		if (texFondo != null) fondo.Texture = texFondo;
		capa.AddChild(fondo);

		var oscurecer = new ColorRect();
		oscurecer.Color = new Color(0, 0, 0, 0.6f);
		oscurecer.SetAnchorsPreset(LayoutPreset.FullRect);
		oscurecer.MouseFilter = Control.MouseFilterEnum.Stop;
		fondo.AddChild(oscurecer);

		var centro = new VBoxContainer();
		centro.SetAnchorsPreset(LayoutPreset.Center);
		centro.OffsetLeft = -320; centro.OffsetRight = 320;
		centro.OffsetTop  = -230; centro.OffsetBottom = 230;
		centro.Alignment = BoxContainer.AlignmentMode.Center;
		centro.AddThemeConstantOverride("separation", 28);
		oscurecer.AddChild(centro);

		// Spinner circular (puntos con pulso secuencial, estilo indicador de carga) — más grande.
		var spinner = new Control();
		spinner.CustomMinimumSize = new Vector2(130, 130);
		spinner.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		centro.AddChild(spinner);
		const int nPuntos = 8;
		const float radioSpinner = 52f;
		const float centroSpinner = 65f;
		for (int i = 0; i < nPuntos; i++)
		{
			float ang = i * Mathf.Tau / nPuntos;
			var punto = new Panel();
			punto.Size = new Vector2(20, 20);
			punto.Position = new Vector2(centroSpinner + Mathf.Cos(ang) * radioSpinner - 10, centroSpinner + Mathf.Sin(ang) * radioSpinner - 10);
			var sbPunto = new StyleBoxFlat();
			sbPunto.BgColor = new Color(0.95f, 0.15f, 0.15f); // rojo, look "cargando" tipo YouTube
			sbPunto.CornerRadiusTopLeft = sbPunto.CornerRadiusTopRight =
			sbPunto.CornerRadiusBottomLeft = sbPunto.CornerRadiusBottomRight = 10;
			punto.AddThemeStyleboxOverride("panel", sbPunto);
			spinner.AddChild(punto);

			Tween tw = punto.CreateTween().SetLoops();
			tw.TweenProperty(punto, "modulate:a", 0.15f, 0.5f).SetDelay(i * (0.9f / nPuntos));
			tw.TweenProperty(punto, "modulate:a", 1.0f, 0.5f);
		}

		var lblEstadoPrincipal = new Label();
		lblEstadoPrincipal.Text = "CARGANDO";
		lblEstadoPrincipal.AddThemeColorOverride("font_color", Colors.White);
		lblEstadoPrincipal.AddThemeColorOverride("font_outline_color", Colors.Black);
		lblEstadoPrincipal.AddThemeConstantOverride("outline_size", 5);
		lblEstadoPrincipal.AddThemeFontSizeOverride("font_size", 48);
		lblEstadoPrincipal.HorizontalAlignment = HorizontalAlignment.Center;
		centro.AddChild(lblEstadoPrincipal);

		var lblDetalle = new Label();
		lblDetalle.Text = "buscando jugadores...";
		lblDetalle.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
		lblDetalle.AddThemeFontSizeOverride("font_size", 24);
		lblDetalle.HorizontalAlignment = HorizontalAlignment.Center;
		centro.AddChild(lblDetalle);

		// CANCELAR: visible desde el primer instante, por si el jugador se equivocó de botón y
		// no quiere esperar el "sin conexión" para volver.
		var btnCancelar = new Button();
		btnCancelar.Text = "CANCELAR";
		btnCancelar.CustomMinimumSize = new Vector2(240, 64);
		btnCancelar.AddThemeFontSizeOverride("font_size", 24);
		var sbCancelar = new StyleBoxFlat();
		sbCancelar.BgColor = new Color(0.32f, 0.32f, 0.38f, 1f);
		sbCancelar.CornerRadiusTopLeft = sbCancelar.CornerRadiusTopRight =
		sbCancelar.CornerRadiusBottomLeft = sbCancelar.CornerRadiusBottomRight = 12;
		btnCancelar.AddThemeStyleboxOverride("normal", sbCancelar);
		var sbCancelarHover = new StyleBoxFlat();
		sbCancelarHover.BgColor = new Color(0.42f, 0.42f, 0.5f, 1f);
		sbCancelarHover.CornerRadiusTopLeft = sbCancelarHover.CornerRadiusTopRight =
		sbCancelarHover.CornerRadiusBottomLeft = sbCancelarHover.CornerRadiusBottomRight = 12;
		btnCancelar.AddThemeStyleboxOverride("hover", sbCancelarHover);
		btnCancelar.Pressed += () => { if (IsInstanceValid(_capaOnline)) _capaOnline.QueueFree(); };
		centro.AddChild(btnCancelar);

		var btnVolver = new Button();
		btnVolver.Text = "VOLVER";
		btnVolver.Visible = false;
		btnVolver.CustomMinimumSize = new Vector2(240, 64);
		btnVolver.AddThemeFontSizeOverride("font_size", 24);
		var sbVolver = new StyleBoxFlat();
		sbVolver.BgColor = new Color(0.62f, 0.16f, 0.16f, 1f);
		sbVolver.CornerRadiusTopLeft = sbVolver.CornerRadiusTopRight =
		sbVolver.CornerRadiusBottomLeft = sbVolver.CornerRadiusBottomRight = 12;
		btnVolver.AddThemeStyleboxOverride("normal", sbVolver);
		var sbVolverHover = new StyleBoxFlat();
		sbVolverHover.BgColor = new Color(0.78f, 0.22f, 0.22f, 1f);
		sbVolverHover.CornerRadiusTopLeft = sbVolverHover.CornerRadiusTopRight =
		sbVolverHover.CornerRadiusBottomLeft = sbVolverHover.CornerRadiusBottomRight = 12;
		btnVolver.AddThemeStyleboxOverride("hover", sbVolverHover);
		btnVolver.Pressed += () => { if (IsInstanceValid(_capaOnline)) _capaOnline.QueueFree(); };
		centro.AddChild(btnVolver);

		// No hay servidor de matchmaking real todavía — tras una breve búsqueda simulada, se
		// informa honestamente que no hay conexión en vez de dejar el spinner girando para siempre.
		GetTree().CreateTimer(2.4).Timeout += () =>
		{
			if (!IsInstanceValid(spinner)) return;
			spinner.Visible = false;
			lblEstadoPrincipal.Text = "SIN CONEXIÓN";
			lblEstadoPrincipal.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.45f));
			lblDetalle.Text = "No se pudo conectar al servidor";
			btnCancelar.Visible = false;
			btnVolver.Visible = true;
		};
	}
}
