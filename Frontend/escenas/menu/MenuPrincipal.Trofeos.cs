using Godot;

/// <summary>Interfaz de BtnTrofeo — tabla de mejores jugadores por victorias ("trofeos huevo").
/// IMPORTANTE: el juego no tiene todavía un servidor de rankings online, así que esta lista solo
/// puede mostrar los datos LOCALES del propio dispositivo (no hay forma honesta de mostrar otros
/// jugadores reales sin backend) — se deja preparada para conectarse a un ranking real más
/// adelante.</summary>
public partial class MenuPrincipal : Control
{
	private CanvasLayer _capaTrofeos;

	private void ConectarBtnTrofeo()
	{
		var btnTrofeo = GetNodeOrNull<BaseButton>("BtnTrofeo");
		if (btnTrofeo == null) return;

		AgregarAnimacionHover(btnTrofeo);
		// Abre el ranking REAL del servidor (PanelRanking baja el top 20 y resalta al jugador). Antes
		// abría una pantalla placeholder que solo te mostraba a ti y decía "próximamente". Ahora se ve
		// para todos (invitados incluidos, aunque un invitado no aparece porque no tiene cuenta).
		btnTrofeo.Pressed += () =>
		{
			// En su propia CAPA (CanvasLayer) para garantizar que se dibuje ENCIMA de todo el menú y
			// reciba los clics. Como Control suelto en la base podía quedar tapado por otras capas del
			// menú (por eso "no se veía/no abría"). La capa se libera sola cuando el panel se cierra.
			var capa = new CanvasLayer { Layer = 200 };
			var panel = new PanelRanking();
			panel.TreeExited += () => { if (GodotObject.IsInstanceValid(capa)) capa.QueueFree(); };
			capa.AddChild(panel);
			AddChild(capa);
			panel.Mostrar();
		};

		// Difuminado morado claro detrás del botón + un par de estrellitas titilando delante,
		// indicando "acá hay competencia".
		var padre = btnTrofeo.GetParent();
		if (padre == null) return;

		// Cuadrado como el botón, chico y fino — antes seguía tapando cosas de alrededor.
		var halo = new Panel();
		halo.Name = "HaloTrofeo";
		halo.MouseFilter = Control.MouseFilterEnum.Ignore;
		halo.Size = new Vector2(btnTrofeo.Size.X + 8, btnTrofeo.Size.Y + 8);
		halo.Position = btnTrofeo.Position - new Vector2(4, 4);
		var sbHalo = new StyleBoxFlat();
		sbHalo.BgColor = new Color(0.55f, 0.35f, 0.95f, 0.18f);
		sbHalo.CornerRadiusTopLeft = sbHalo.CornerRadiusTopRight =
		sbHalo.CornerRadiusBottomLeft = sbHalo.CornerRadiusBottomRight = 12;
		sbHalo.ShadowColor = new Color(0.6f, 0.4f, 1f, 0.25f);
		sbHalo.ShadowSize = 6;
		halo.AddThemeStyleboxOverride("panel", sbHalo);
		padre.AddChild(halo);
		padre.MoveChild(halo, btnTrofeo.GetIndex()); // justo detrás del botón

		Tween twHalo = halo.CreateTween().SetLoops();
		twHalo.TweenProperty(halo, "modulate:a", 0.5f, 1.0f);
		twHalo.TweenProperty(halo, "modulate:a", 1.0f, 1.0f);

		Vector2[] posEstrellas = { new Vector2(-18, -14), new Vector2(btnTrofeo.Size.X + 6, -10), new Vector2(btnTrofeo.Size.X - 14, btnTrofeo.Size.Y + 8) };
		foreach (var pos in posEstrellas)
		{
			var estrella = new Label();
			estrella.Text = "✦";
			estrella.AddThemeColorOverride("font_color", new Color(0.85f, 0.75f, 1f));
			estrella.AddThemeFontSizeOverride("font_size", 24);
			estrella.MouseFilter = Control.MouseFilterEnum.Ignore;
			estrella.Position = btnTrofeo.Position + pos;
			padre.AddChild(estrella);
			padre.MoveChild(estrella, btnTrofeo.GetIndex() + 1); // delante del botón

			Tween twEstrella = estrella.CreateTween().SetLoops();
			float retardo = (float)GD.RandRange(0.0, 1.2);
			twEstrella.TweenProperty(estrella, "modulate:a", 0.15f, 0.6f).SetDelay(retardo);
			twEstrella.TweenProperty(estrella, "modulate:a", 1.0f, 0.6f);
		}

		// Efecto "presionado": achica un toque al soltar el clic.
		btnTrofeo.ButtonDown += () => {
			var tw = btnTrofeo.CreateTween();
			btnTrofeo.PivotOffset = btnTrofeo.Size / 2f;
			tw.TweenProperty(btnTrofeo, "scale", new Vector2(0.92f, 0.92f), 0.08f);
		};
		btnTrofeo.ButtonUp += () => {
			var tw = btnTrofeo.CreateTween();
			tw.TweenProperty(btnTrofeo, "scale", Vector2.One, 0.12f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		};
	}

	private void MostrarPantallaTrofeos()
	{
		if (_capaTrofeos != null && IsInstanceValid(_capaTrofeos)) { _capaTrofeos.Visible = true; return; }

		var capa = new CanvasLayer();
		capa.Layer = 300;
		AddChild(capa);
		_capaTrofeos = capa;

		var fondo = new ColorRect();
		fondo.Color = new Color(0, 0, 0, 0.75f);
		fondo.SetAnchorsPreset(LayoutPreset.FullRect);
		fondo.MouseFilter = Control.MouseFilterEnum.Stop;
		capa.AddChild(fondo);

		// Envoltorio SIN recorte para anclar la X en su esquina real, sin depender de coordenadas
		// de toda la pantalla (antes la X se posicionaba relativa al fondo completo y quedaba
		// lejos del panel real según la resolución).
		var marco = new Control();
		marco.SetAnchorsPreset(LayoutPreset.Center);
		marco.OffsetLeft = -370; marco.OffsetRight = 370;
		marco.OffsetTop  = -320; marco.OffsetBottom = 320;
		fondo.AddChild(marco);

		var panel = new PanelContainer();
		panel.SetAnchorsPreset(LayoutPreset.FullRect);
		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.10f, 0.07f, 0.18f, 0.98f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 3;
		sb.BorderColor = new Color(0.65f, 0.55f, 1f, 0.85f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 20;
		sb.ContentMarginLeft = sb.ContentMarginRight = 32;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 26;
		sb.ShadowColor = new Color(0.4f, 0.2f, 0.8f, 0.4f); sb.ShadowSize = 14;
		panel.AddThemeStyleboxOverride("panel", sb);
		marco.AddChild(panel);

		var btnCerrar = CrearBotonCerrarRojo();
		btnCerrar.CustomMinimumSize = new Vector2(56, 56);
		btnCerrar.AddThemeFontSizeOverride("font_size", 26);
		btnCerrar.SetAnchorsPreset(LayoutPreset.TopRight);
		btnCerrar.OffsetLeft = -24; btnCerrar.OffsetTop = -24;
		btnCerrar.OffsetRight = 32; btnCerrar.OffsetBottom = 32;
		btnCerrar.Pressed += () => _capaTrofeos.Visible = false;
		marco.AddChild(btnCerrar);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 20);
		panel.AddChild(vbox);

		var lblTitulo = new Label();
		lblTitulo.Text = "EL MEJOR JUGADOR";
		lblTitulo.AddThemeColorOverride("font_color", new Color(0.75f, 0.65f, 1f));
		lblTitulo.AddThemeFontSizeOverride("font_size", 38);
		lblTitulo.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(lblTitulo);

		var lblSub = new Label();
		lblSub.Text = "Trofeos huevo ganados en partidas";
		lblSub.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 1f));
		lblSub.AddThemeFontSizeOverride("font_size", 19);
		lblSub.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(lblSub);

		var scroll = new ScrollContainer();
		scroll.CustomMinimumSize = new Vector2(0, 340);
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		vbox.AddChild(scroll);

		var lista = new VBoxContainer();
		lista.AddThemeConstantOverride("separation", 10);
		lista.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(lista);

		// Los trofeos son solo para jugadores registrados (no invitados) — un invitado no tiene
		// cuenta donde guardar/mostrar un ranking, así que la tabla queda vacía para él.
		bool registrado = SesionJuego.Instance?.EstaLogueado ?? false;
		if (registrado)
		{
			string nombre = SesionJuego.Instance.NombreJugador;
			lista.AddChild(CrearFilaTrofeo(1, nombre + " (tú)", Preferencias.TrofeosHuevo, true));
		}

		var lblAviso = new Label();
		lblAviso.Text = registrado
			? "Los rankings con otros jugadores necesitan conexión en línea (próximamente)."
			: "Inicia sesión con una cuenta para tener trofeos y aparecer en la tabla.";
		lblAviso.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.7f));
		lblAviso.AddThemeFontSizeOverride("font_size", 16);
		lblAviso.HorizontalAlignment = HorizontalAlignment.Center;
		lblAviso.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(lblAviso);
	}

	private Control CrearFilaTrofeo(int puesto, string nombre, int trofeos, bool esPropia)
	{
		var fila = new PanelContainer();
		var sb = new StyleBoxFlat();
		sb.BgColor = esPropia ? new Color(0.35f, 0.25f, 0.6f, 0.6f) : new Color(0.18f, 0.15f, 0.3f, 0.5f);
		sb.BorderWidthLeft = 3;
		sb.BorderColor = new Color(0.6f, 0.85f, 1f, 0.8f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 10;
		sb.ContentMarginLeft = sb.ContentMarginRight = 16;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 10;
		fila.AddThemeStyleboxOverride("panel", sb);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 14);
		fila.AddChild(hbox);

		var lblPuesto = new Label();
		lblPuesto.Text = $"#{puesto}";
		lblPuesto.AddThemeColorOverride("font_color", new Color(0.85f, 0.75f, 1f));
		lblPuesto.AddThemeFontSizeOverride("font_size", 23);
		lblPuesto.CustomMinimumSize = new Vector2(50, 0);
		hbox.AddChild(lblPuesto);

		var lblNombre = new Label();
		lblNombre.Text = nombre;
		lblNombre.AddThemeColorOverride("font_color", Colors.White);
		lblNombre.AddThemeFontSizeOverride("font_size", 23);
		lblNombre.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		hbox.AddChild(lblNombre);

		var lblTrofeos = new Label();
		lblTrofeos.Text = $"🏆 {trofeos}";
		lblTrofeos.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
		lblTrofeos.AddThemeFontSizeOverride("font_size", 23);
		hbox.AddChild(lblTrofeos);

		return fila;
	}

	private static Button CrearBotonCerrarRojo()
	{
		var btn = new Button();
		btn.Text = "✕";
		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.85f, 0.2f, 0.2f, 0.95f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 12;
		btn.AddThemeStyleboxOverride("normal", sb);
		var sbH = new StyleBoxFlat();
		sbH.BgColor = new Color(1f, 0.3f, 0.3f, 1f);
		sbH.CornerRadiusTopLeft = sbH.CornerRadiusTopRight = sbH.CornerRadiusBottomLeft = sbH.CornerRadiusBottomRight = 12;
		btn.AddThemeStyleboxOverride("hover", sbH);
		return btn;
	}
}
