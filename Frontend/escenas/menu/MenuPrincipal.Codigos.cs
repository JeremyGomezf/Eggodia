using Godot;

/// <summary>Interfaz "Ingresar Código" — antes BtnDev llevaba al Campo de Pruebas; ahora abre este
/// panel central para canjear códigos promocionales (skins, monedas, etc.). Ver CodigosPromo.cs
/// para la validación y los 5 códigos de prueba.</summary>
public partial class MenuPrincipal : Control
{
	private CanvasLayer _capaCodigos;
	private LineEdit    _txtCodigo;
	private Label       _lblResultadoCodigo;

	private void MostrarPantallaCodigos()
	{
		if (_capaCodigos != null && IsInstanceValid(_capaCodigos)) { _capaCodigos.Visible = true; return; }

		var capa = new CanvasLayer();
		capa.Layer = 300;
		AddChild(capa);
		_capaCodigos = capa;

		var fondo = new ColorRect();
		fondo.Color = new Color(0, 0, 0, 0.75f);
		fondo.SetAnchorsPreset(LayoutPreset.FullRect);
		fondo.MouseFilter = Control.MouseFilterEnum.Stop;
		capa.AddChild(fondo);

		// Envoltorio SIN recorte (a diferencia de PanelContainer), tamaño más grande para que se
		// lea bien en móvil.
		var marco = new Control();
		marco.SetAnchorsPreset(LayoutPreset.Center);
		marco.OffsetLeft = -340; marco.OffsetRight = 340;
		marco.OffsetTop  = -280; marco.OffsetBottom = 280;
		fondo.AddChild(marco);

		var panel = new PanelContainer();
		panel.SetAnchorsPreset(LayoutPreset.FullRect);
		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.08f, 0.07f, 0.14f, 0.98f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 3;
		sb.BorderColor = new Color(0.55f, 0.75f, 1f, 0.8f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 20;
		sb.ContentMarginLeft = sb.ContentMarginRight = 36;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 30;
		sb.ShadowColor = new Color(0, 0, 0, 0.5f); sb.ShadowSize = 12;
		panel.AddThemeStyleboxOverride("panel", sb);
		marco.AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 22);
		panel.AddChild(vbox);

		var lblTitulo = new Label();
		lblTitulo.Text = "INGRESAR CÓDIGO";
		lblTitulo.AddThemeColorOverride("font_color", new Color(0.6f, 0.85f, 1f));
		lblTitulo.AddThemeFontSizeOverride("font_size", 38);
		lblTitulo.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(lblTitulo);

		var lblSub = new Label();
		lblSub.Text = "Canjea un código por monedas u objetos";
		lblSub.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.85f));
		lblSub.AddThemeFontSizeOverride("font_size", 19);
		lblSub.HorizontalAlignment = HorizontalAlignment.Center;
		lblSub.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(lblSub);

		_txtCodigo = new LineEdit();
		_txtCodigo.PlaceholderText = "Escribe tu código aquí";
		_txtCodigo.CustomMinimumSize = new Vector2(0, 66);
		_txtCodigo.AddThemeFontSizeOverride("font_size", 28);
		_txtCodigo.Alignment = HorizontalAlignment.Center;
		_txtCodigo.MaxLength = 24;
		vbox.AddChild(_txtCodigo);

		var btnCanjear = new Button();
		btnCanjear.Text = "CANJEAR";
		btnCanjear.CustomMinimumSize = new Vector2(0, 68);
		btnCanjear.AddThemeFontSizeOverride("font_size", 26);
		btnCanjear.Pressed += IntentarCanjearCodigo;
		vbox.AddChild(btnCanjear);
		_txtCodigo.TextSubmitted += _ => IntentarCanjearCodigo();

		_lblResultadoCodigo = new Label();
		_lblResultadoCodigo.Text = "";
		_lblResultadoCodigo.AddThemeFontSizeOverride("font_size", 24);
		_lblResultadoCodigo.HorizontalAlignment = HorizontalAlignment.Center;
		_lblResultadoCodigo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(_lblResultadoCodigo);

		// En vez de una X: un botón CERRAR bien visible abajo del todo.
		var btnCerrar = new Button();
		btnCerrar.Text = "CERRAR";
		btnCerrar.CustomMinimumSize = new Vector2(0, 60);
		btnCerrar.AddThemeFontSizeOverride("font_size", 22);
		var sbCerrar = new StyleBoxFlat();
		sbCerrar.BgColor = new Color(0.3f, 0.3f, 0.36f, 1f);
		sbCerrar.CornerRadiusTopLeft = sbCerrar.CornerRadiusTopRight =
		sbCerrar.CornerRadiusBottomLeft = sbCerrar.CornerRadiusBottomRight = 12;
		btnCerrar.AddThemeStyleboxOverride("normal", sbCerrar);
		btnCerrar.Pressed += () => _capaCodigos.Visible = false;
		vbox.AddChild(btnCerrar);
	}

	// Botón "X" rojo circular para cerrar paneles emergentes — compartido por Códigos y Trofeos.
	private Button CrearBotonCerrarRojo()
	{
		var btn = new Button();
		btn.Text = "X";
		btn.CustomMinimumSize = new Vector2(44, 44);
		btn.AddThemeFontSizeOverride("font_size", 20);
		btn.AddThemeColorOverride("font_color", Colors.White);
		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.75f, 0.18f, 0.18f, 1f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 22;
		btn.AddThemeStyleboxOverride("normal", sb);
		var sbHover = new StyleBoxFlat();
		sbHover.BgColor = new Color(0.9f, 0.25f, 0.25f, 1f);
		sbHover.CornerRadiusTopLeft = sbHover.CornerRadiusTopRight =
		sbHover.CornerRadiusBottomLeft = sbHover.CornerRadiusBottomRight = 22;
		btn.AddThemeStyleboxOverride("hover", sbHover);
		return btn;
	}

	private void IntentarCanjearCodigo()
	{
		if (_txtCodigo == null || _lblResultadoCodigo == null) return;
		var (resultado, monedas) = CodigosPromo.Canjear(_txtCodigo.Text);

		switch (resultado)
		{
			case CodigosPromo.Resultado.Canjeado:
				_lblResultadoCodigo.Text = $"¡CÓDIGO CANJEADO! +{monedas} monedas";
				_lblResultadoCodigo.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.45f));
				_txtCodigo.Text = "";
				break;
			case CodigosPromo.Resultado.YaUsado:
				_lblResultadoCodigo.Text = "CÓDIGO YA USADO";
				_lblResultadoCodigo.AddThemeColorOverride("font_color", new Color(1f, 0.75f, 0.3f));
				break;
			case CodigosPromo.Resultado.Expirado:
				_lblResultadoCodigo.Text = "CÓDIGO EXPIRADO O CADUCADO";
				_lblResultadoCodigo.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.35f));
				break;
			default:
				_lblResultadoCodigo.Text = "CÓDIGO ERRÓNEO";
				_lblResultadoCodigo.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
				break;
		}
	}
}
