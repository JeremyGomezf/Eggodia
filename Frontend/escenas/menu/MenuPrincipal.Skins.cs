using Godot;
using System;
using System.Collections.Generic;

/// <summary>Selector "SELECCIONA TU HUEVO" (tocando el huevo del menú) y datos de las skins exclusivas
/// que también usa el perfil (ver MenuPrincipal.Perfil.cs).</summary>
public partial class MenuPrincipal : Control
{
	// Skins exclusivas (no se venden): por código (Dorado, Ecotec) o de cada dev, SOLO por el Id fijo de
	// su cuenta (igual que el servidor, ver Backend CuentasDev). Antes también se aceptaba un nombre
	// parecido ("gonzalito" veía Gonza Huevo) y eso le quitaba lo exclusivo. "clave" sirve para
	// reconocer la ruta que guarda el servidor, que para las de dev es otra imagen
	// (PersonajesPng/GonzaHuevo.png): se muestra siempre el render.
	private static readonly (string nombre, string render, int devId, string clave)[] SKINS_EXCLUSIVAS =
	{
		("Huevo Dorado", "res://imagenes/RendersTropa/Huevo render/HuevoDorado_Render.png", -1, "dorado"),
		("Huevo Ecotec", "res://imagenes/RendersTropa/Huevo render/HuevoEcotec_Render.png", -1, "ecotec"),
		("Jeremi Huevo", "res://imagenes/RendersTropa/Huevo render/JeremyHuevo_Render.png",  1, "jerem"),  // Jeremy_dev
		("Carlos Huevo", "res://imagenes/RendersTropa/Huevo render/CarlosHuevo_Render.png",  4, "carlos"), // kankox_dev
		("Gonza Huevo",  "res://imagenes/RendersTropa/Huevo render/GonzaHuevo_Render.png",   2, "gonza"),  // SrGonza
	};

	/// <summary>Si la skin equipada es la de un dev y esta cuenta NO es ese dev (se la había equipado
	/// cuando bastaba un nombre parecido), vuelve a su huevo normal.</summary>
	private void QuitarSkinDevAjena()
	{
		int i = IndiceSkinExclusiva(Preferencias.SkinExclusivaActiva);
		if (i < 0 || SKINS_EXCLUSIVAS[i].devId <= 0) return;
		if ((SesionJuego.Instance?.UsuarioId ?? 0) == SKINS_EXCLUSIVAS[i].devId) return;
		Preferencias.SkinExclusivaActiva = "";
		ActualizarHuevoMenu();
	}

	/// <summary>Exclusiva a la que corresponde una ruta cualquiera (render o imagen del servidor), o -1.</summary>
	private static int IndiceSkinExclusiva(string ruta)
	{
		if (string.IsNullOrEmpty(ruta)) return -1;
		string r = ruta.ToLowerInvariant();
		for (int i = 0; i < SKINS_EXCLUSIVAS.Length; i++)
			if (r.Contains(SKINS_EXCLUSIVAS[i].clave)) return i;
		return -1;
	}

	private void MostrarSkinGanadaPendiente()
	{
		if (!IsInstanceValid(this) || !IsInsideTree()) return;
		if (Economia.Instancia()?.TomarSkinGanadaPendiente() is (string nombre, string ruta))
			PopupSkinDesbloqueada.Mostrar(this, nombre, ruta, ActualizarHuevoMenu, "¡Le ganaste a un desarrollador!");
	}

	private void AbrirSelectorSkin()
	{
		// Evitar doble apertura
		if (GetNodeOrNull("SelectorSkin") != null) return;

		var overlay = new ColorRect
		{
			Name = "SelectorSkin",
			Color = new Color(0, 0, 0, 0.78f),
			ZIndex = 200,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(overlay);
		void Cerrar() { if (IsInstanceValid(overlay)) overlay.QueueFree(); }

		// El panel se ajusta a su contenido (antes tenía un alto fijo y quedaba una franja vacía abajo).
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(1100, 0) };
		panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		panel.GrowHorizontal = Control.GrowDirection.Both;
		panel.GrowVertical   = Control.GrowDirection.Both;
		var sb = new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.08f, 0.16f, 0.98f),
			BorderColor = new Color(1f, 0.80f, 0.25f),
			ShadowColor = new Color(0, 0, 0, 0.6f),
			ShadowSize = 10,
		};
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 14;
		sb.ContentMarginLeft = sb.ContentMarginRight = 22;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 18;
		panel.AddThemeStyleboxOverride("panel", sb);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 14);
		panel.AddChild(vbox);

		// Cabecera
		var header = new HBoxContainer();
		var lblTitulo = new Label { Text = "SELECCIONA TU HUEVO", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
		EstiloUI.Titulo(lblTitulo, 44);
		header.AddChild(lblTitulo);
		var btnX = new Button { Text = "✕", CustomMinimumSize = new Vector2(68, 68) };
		EstiloUI.Boton(btnX, 32, rojo: true);
		btnX.Pressed += Cerrar;
		header.AddChild(btnX);
		vbox.AddChild(header);

		// Una sola fila que se desliza con el dedo; tan alta como las tarjetas (sin sobrante).
		var scrollSkins = new ScrollTactil
		{
			VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
			CustomMinimumSize = new Vector2(0, TAM_TARJETA_SKIN.Y + 4),
		};
		vbox.AddChild(scrollSkins);

		var fila = new HBoxContainer();
		fila.AddThemeConstantOverride("separation", 14);
		scrollSkins.AddChild(fila);

		string exclusivaActiva = Preferencias.SkinExclusivaActiva;

		// Skins normales: solo las que ya tienes (la Tienda muestra el catálogo completo).
		for (int i = 0; i < Preferencias.SKIN_NOMBRES.Length; i++)
		{
			if (!Preferencias.TieneSkin(i)) continue;
			int capI = i;
			bool activa = string.IsNullOrEmpty(exclusivaActiva) && Preferencias.SkinActivaIdx == i;
			fila.AddChild(CrearTarjetaSkin(Preferencias.SKIN_IMAGENES[i], Preferencias.SKIN_NOMBRES[i], exclusiva: false, activa,
				() =>
				{
					Preferencias.SkinExclusivaActiva = "";
					Preferencias.SkinActivaIdx = capI;
					Cerrar();
					ActualizarHuevoMenu();
				}));
		}

		// Exclusivas: las de código si las canjeaste, y la de cada dev solo para SU cuenta (por Id).
		int userIdActual = SesionJuego.Instance?.UsuarioId ?? 0;
		foreach (var (nombreExc, rutaExc, devId, _) in SKINS_EXCLUSIVAS)
		{
			bool esDev = devId > 0 && userIdActual == devId;
			if (devId > 0 && !esDev) continue; // la skin de un dev nunca la ve otra cuenta
			if (!esDev && !Preferencias.TieneSkinExclusiva(rutaExc)) continue;
			string r = rutaExc;
			fila.AddChild(CrearTarjetaSkin(rutaExc, nombreExc, exclusiva: true, exclusivaActiva == rutaExc,
				() =>
				{
					Preferencias.SkinExclusivaActiva = r;
					Cerrar();
					ActualizarHuevoMenu();
				}));
		}

		overlay.AddChild(panel);

		// Animación de entrada desde el centro.
		panel.Resized += () => { if (IsInstanceValid(panel)) panel.PivotOffset = panel.Size / 2f; };
		panel.Scale = new Vector2(0.7f, 0.7f);
		panel.CreateTween().TweenProperty(panel, "scale", Vector2.One, 0.22f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	/// <summary>Tarjeta de una skin. Se equipa tocando CUALQUIER parte de la tarjeta (la imagen, el
	/// nombre o el botón EQUIPAR); deslizar la fila no equipa nada sin querer.</summary>
	private Control CrearTarjetaSkin(string rutaImagen, string nombre, bool exclusiva, bool activa, Action equipar)
	{
		var tarjeta = new PanelContainer { CustomMinimumSize = TAM_TARJETA_SKIN, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
		var sb = new StyleBoxFlat
		{
			BgColor = activa ? new Color(0.12f, 0.22f, 0.10f)
				: exclusiva ? new Color(0.12f, 0.08f, 0.22f, 0.95f) : new Color(0.08f, 0.10f, 0.20f, 0.95f),
			BorderColor = activa ? new Color(0.4f, 1f, 0.4f) : exclusiva ? new Color(1f, 0.85f, 0.25f) : new Color(0.85f, 0.65f, 0.2f),
		};
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = activa ? 3 : 2;
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 10;
		sb.ContentMarginLeft = sb.ContentMarginRight = sb.ContentMarginTop = sb.ContentMarginBottom = 8;
		tarjeta.AddThemeStyleboxOverride("panel", sb);

		var col = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		col.AddThemeConstantOverride("separation", 6);
		tarjeta.AddChild(col);

		var tex = new TextureRect
		{
			CustomMinimumSize = TAM_HUEVO_SKIN,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
		};
		if (ResourceLoader.Exists(rutaImagen)) tex.Texture = GD.Load<Texture2D>(rutaImagen);
		col.AddChild(tex);

		var lblN = new Label { Text = nombre, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		EstiloUI.Texto(lblN, exclusiva ? 20 : 22, exclusiva ? new Color(1f, 0.85f, 0.3f) : new Color(0.95f, 0.92f, 0.80f));
		col.AddChild(lblN);

		if (activa)
		{
			var lbl = new Label { Text = "✓ EQUIPADA", HorizontalAlignment = HorizontalAlignment.Center };
			EstiloUI.Texto(lbl, 22, new Color(0.4f, 1f, 0.45f));
			col.AddChild(lbl);
		}
		else
		{
			var btn = new Button { Text = "EQUIPAR", CustomMinimumSize = new Vector2(0, 50) };
			EstiloUI.Boton(btn, 22);
			btn.Pressed += equipar;
			col.AddChild(btn);

			// Tocar la tarjeta (no solo el botón) también equipa. Se decide al SOLTAR y solo si no fue un
			// arrastre de la fila.
			tarjeta.GuiInput += (ev) =>
			{
				if (ev is not InputEventMouseButton mb || mb.ButtonIndex != MouseButton.Left || mb.Pressed) return;
				if (ScrollTactil.UltimoGestoFueArrastre || !tarjeta.GetGlobalRect().HasPoint(mb.GlobalPosition)) return;
				SonidoUI.Reproducir(this);
				equipar();
			};
		}
		return tarjeta;
	}
}
