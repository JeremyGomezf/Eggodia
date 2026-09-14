using Godot;
using System.Threading.Tasks;

/// <summary>
/// Interfaz "Ingresar Código" respaldada en el backend de Eggodia.
/// Canjea skins de huevo exclusivas (como Huevo Ecotec) y monedas, ligados a la cuenta del usuario.
/// </summary>
public partial class MenuPrincipal : Control
{
	private CanvasLayer _capaCodigos;
	private LineEdit    _txtCodigo;
	private Label       _lblResultadoCodigo;
	private Button      _btnCanjear;

	private void MostrarPantallaCodigos()
	{
		// Guardia de Invitado: no se permite canjear sin iniciar sesión
		if (SesionJuego.Instance == null || SesionJuego.Instance.UsuarioId <= 0)
		{
			MostrarAvisoModal("MODO INVITADO", "Debes iniciar sesión con una cuenta para poder canjear códigos promocionales.");
			return;
		}

		if (_capaCodigos != null && IsInstanceValid(_capaCodigos))
		{
			_capaCodigos.Visible = true;
			if (_txtCodigo != null) { _txtCodigo.Text = ""; _txtCodigo.GrabFocus(); }
			if (_lblResultadoCodigo != null) _lblResultadoCodigo.Text = "";
			return;
		}

		var capa = new CanvasLayer();
		capa.Layer = 300;
		AddChild(capa);
		_capaCodigos = capa;

		var fondo = new ColorRect();
		fondo.Color = new Color(0, 0, 0, 0.75f);
		fondo.SetAnchorsPreset(LayoutPreset.FullRect);
		fondo.MouseFilter = Control.MouseFilterEnum.Stop;
		capa.AddChild(fondo);

		var marco = new Control();
		marco.SetAnchorsPreset(LayoutPreset.Center);
		marco.OffsetLeft = -340; marco.OffsetRight = 340;
		marco.OffsetTop  = -290; marco.OffsetBottom = 290;
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
		vbox.AddThemeConstantOverride("separation", 20);
		panel.AddChild(vbox);

		var lblTitulo = new Label();
		lblTitulo.Text = "INGRESAR CÓDIGO";
		lblTitulo.AddThemeColorOverride("font_color", new Color(0.6f, 0.85f, 1f));
		lblTitulo.AddThemeFontSizeOverride("font_size", 36);
		lblTitulo.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(lblTitulo);

		var lblSub = new Label();
		lblSub.Text = "Canjea códigos por skins exclusivas o monedas";
		lblSub.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.85f));
		lblSub.AddThemeFontSizeOverride("font_size", 18);
		lblSub.HorizontalAlignment = HorizontalAlignment.Center;
		lblSub.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(lblSub);

		_txtCodigo = new LineEdit();
		_txtCodigo.PlaceholderText = "Ej: 133huevo375";
		_txtCodigo.CustomMinimumSize = new Vector2(0, 66);
		_txtCodigo.AddThemeFontSizeOverride("font_size", 28);
		_txtCodigo.Alignment = HorizontalAlignment.Center;
		_txtCodigo.MaxLength = 30;
		vbox.AddChild(_txtCodigo);

		_btnCanjear = new Button();
		_btnCanjear.Text = "CANJEAR";
		_btnCanjear.CustomMinimumSize = new Vector2(0, 64);
		_btnCanjear.AddThemeFontSizeOverride("font_size", 24);
		_btnCanjear.Pressed += () => _ = IntentarCanjearCodigoAsync();
		vbox.AddChild(_btnCanjear);
		_txtCodigo.TextSubmitted += (nuevoTexto) => { _ = IntentarCanjearCodigoAsync(); };

		_lblResultadoCodigo = new Label();
		_lblResultadoCodigo.Text = "";
		_lblResultadoCodigo.AddThemeFontSizeOverride("font_size", 20);
		_lblResultadoCodigo.HorizontalAlignment = HorizontalAlignment.Center;
		_lblResultadoCodigo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(_lblResultadoCodigo);

		var btnCerrar = new Button();
		btnCerrar.Text = "CERRAR";
		btnCerrar.CustomMinimumSize = new Vector2(0, 56);
		btnCerrar.AddThemeFontSizeOverride("font_size", 22);
		var sbCerrar = new StyleBoxFlat();
		sbCerrar.BgColor = new Color(0.3f, 0.3f, 0.36f, 1f);
		sbCerrar.CornerRadiusTopLeft = sbCerrar.CornerRadiusTopRight =
		sbCerrar.CornerRadiusBottomLeft = sbCerrar.CornerRadiusBottomRight = 12;
		btnCerrar.AddThemeStyleboxOverride("normal", sbCerrar);
		btnCerrar.Pressed += () => _capaCodigos.Visible = false;
		vbox.AddChild(btnCerrar);
	}

	private async Task IntentarCanjearCodigoAsync()
	{
		if (_txtCodigo == null || _lblResultadoCodigo == null) return;
		string texto = _txtCodigo.Text.Trim();
		if (string.IsNullOrEmpty(texto)) return;

		_btnCanjear.Disabled = true;
		_lblResultadoCodigo.Text = "Consultando servidor...";
		_lblResultadoCodigo.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.3f));

		int userId = SesionJuego.Instance != null ? SesionJuego.Instance.UsuarioId : 0;
		var resultado = await CodigosPromo.CanjearAsync(texto, userId, this);
		_btnCanjear.Disabled = false;

		switch (resultado.Tipo)
		{
			case CodigosPromo.TipoResultado.Canjeado:
				_lblResultadoCodigo.Text = resultado.Mensaje;
				_lblResultadoCodigo.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.45f));
				_txtCodigo.Text = "";

				if (resultado.TipoRecompensa == "skin")
				{
					_capaCodigos.Visible = false;
					MostrarPopupSkinDesbloqueada(resultado.NombreRecompensa, resultado.ValorRecompensa);
				}
				break;

			case CodigosPromo.TipoResultado.YaUsado:
				_lblResultadoCodigo.Text = resultado.Mensaje;
				_lblResultadoCodigo.AddThemeColorOverride("font_color", new Color(1f, 0.75f, 0.3f));
				break;

			case CodigosPromo.TipoResultado.RequiereLogin:
				_lblResultadoCodigo.Text = resultado.Mensaje;
				_lblResultadoCodigo.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.35f));
				break;

			default:
				_lblResultadoCodigo.Text = resultado.Mensaje;
				_lblResultadoCodigo.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
				break;
		}
	}

	private void MostrarPopupSkinDesbloqueada(string nombreSkin, string rutaImagen)
	{
		var capa = new CanvasLayer { Layer = 400 };
		AddChild(capa);

		var fondo = new ColorRect();
		fondo.Color = new Color(0.02f, 0.04f, 0.1f, 0.88f);
		fondo.SetAnchorsPreset(LayoutPreset.FullRect);
		capa.AddChild(fondo);

		var panel = new PanelContainer();
		panel.SetAnchorsPreset(LayoutPreset.Center);
		panel.CustomMinimumSize = new Vector2(500, 520);
		panel.OffsetLeft = -250; panel.OffsetRight = 250;
		panel.OffsetTop  = -260; panel.OffsetBottom = 260;

		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.08f, 0.12f, 0.22f, 0.98f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 3;
		sb.BorderColor = new Color(1f, 0.85f, 0.3f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 24;
		sb.ContentMarginLeft = sb.ContentMarginRight = 30;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 30;
		sb.ShadowColor = new Color(1f, 0.85f, 0.3f, 0.3f);
		sb.ShadowSize = 20;
		panel.AddThemeStyleboxOverride("panel", sb);
		capa.AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 16);
		vbox.Alignment = BoxContainer.AlignmentMode.Center;
		panel.AddChild(vbox);

		var lblHeader = new Label();
		lblHeader.Text = "¡NUEVO SKIN DE HUEVO DESBLOQUEADO!";
		lblHeader.HorizontalAlignment = HorizontalAlignment.Center;
		lblHeader.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		lblHeader.AddThemeColorOverride("font_color", new Color(1f, 0.88f, 0.3f));
		lblHeader.AddThemeFontSizeOverride("font_size", 24);
		vbox.AddChild(lblHeader);

		var texRect = new TextureRect();
		texRect.CustomMinimumSize = new Vector2(180, 240);
		texRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		texRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		texRect.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		if (ResourceLoader.Exists(rutaImagen))
		{
			texRect.Texture = GD.Load<Texture2D>(rutaImagen);
		}
		vbox.AddChild(texRect);

		var lblNombre = new Label();
		lblNombre.Text = nombreSkin;
		lblNombre.HorizontalAlignment = HorizontalAlignment.Center;
		lblNombre.AddThemeColorOverride("font_color", Colors.White);
		lblNombre.AddThemeFontSizeOverride("font_size", 28);
		vbox.AddChild(lblNombre);

		var btnEquipar = new Button();
		btnEquipar.Text = "¡GENIAL!";
		btnEquipar.CustomMinimumSize = new Vector2(220, 52);
		btnEquipar.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		btnEquipar.AddThemeFontSizeOverride("font_size", 22);
		btnEquipar.Pressed += () =>
		{
			capa.QueueFree();
			ActualizarHuevoMenu();
		};
		vbox.AddChild(btnEquipar);

		// Animación elástica de aparición
		panel.PivotOffset = new Vector2(250, 260);
		panel.Scale = Vector2.Zero;
		var tw = panel.CreateTween();
		tw.TweenProperty(panel, "scale", Vector2.One, 0.35f)
		  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	private void MostrarAvisoModal(string titulo, string mensaje)
	{
		var capa = new CanvasLayer { Layer = 400 };
		AddChild(capa);

		var fondo = new ColorRect();
		fondo.Color = new Color(0, 0, 0, 0.7f);
		fondo.SetAnchorsPreset(LayoutPreset.FullRect);
		capa.AddChild(fondo);

		var panel = new PanelContainer();
		panel.SetAnchorsPreset(LayoutPreset.Center);
		panel.CustomMinimumSize = new Vector2(480, 240);
		panel.OffsetLeft = -240; panel.OffsetRight = 240;
		panel.OffsetTop  = -120; panel.OffsetBottom = 120;
		capa.AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 16);
		vbox.Alignment = BoxContainer.AlignmentMode.Center;
		panel.AddChild(vbox);

		var lblTit = new Label();
		lblTit.Text = titulo;
		lblTit.HorizontalAlignment = HorizontalAlignment.Center;
		lblTit.AddThemeColorOverride("font_color", new Color(1f, 0.75f, 0.3f));
		lblTit.AddThemeFontSizeOverride("font_size", 26);
		vbox.AddChild(lblTit);

		var lblMsg = new Label();
		lblMsg.Text = mensaje;
		lblMsg.HorizontalAlignment = HorizontalAlignment.Center;
		lblMsg.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		lblMsg.AddThemeFontSizeOverride("font_size", 18);
		vbox.AddChild(lblMsg);

		var btnOk = new Button();
		btnOk.Text = "ENTENDIDO";
		btnOk.CustomMinimumSize = new Vector2(160, 48);
		btnOk.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		btnOk.Pressed += () => capa.QueueFree();
		vbox.AddChild(btnOk);
	}
}
