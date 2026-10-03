using Godot;
using System.Text;
using System.Text.Json;

/// <summary>Perfil de un jugador: el tuyo (tocando tu nombre arriba) o el de otro (tocándolo en el
/// ranking, como en Clash). Es el mismo panel con los datos de quien sea: huevo equipado, tropa
/// favorita y estadísticas. Se abre en su propia capa, por encima del ranking, así al cerrarlo con la
/// X se vuelve al ranking tal cual estaba.</summary>
public partial class MenuPrincipal : Control
{
	private const string NOMBRE_CAPA_PERFIL = "PerfilJugador";

	private sealed class DatosPerfil
	{
		public string Nombre = "";
		public int    Experiencia;
		public int?   Monedas;            // solo en el propio: las monedas de otro no se muestran
		public int    Ganadas, Perdidas;
		public int    SkinIdx;
		public string SkinExclusiva = "";
		public string RutaTropaFavorita;  // null = todavía sin favoritos
		public string ArdidFavorito;
		public string AvisoSinFavoritos = "";
	}

	private void AbrirPerfil()
	{
		int partidas = Preferencias.PartidasParaFavoritos;
		MostrarPanelPerfil(new DatosPerfil
		{
			Nombre            = SesionJuego.Instance?.NombreJugador ?? "Invitado",
			Experiencia       = Preferencias.ExperienciaTotal,
			Monedas           = Economia.Instancia()?.Monedas ?? 0,
			Ganadas           = Preferencias.PartidasGanadas,
			Perdidas          = Preferencias.PartidasPerdidas,
			SkinIdx           = Preferencias.SkinActivaIdx,
			SkinExclusiva     = Preferencias.SkinExclusivaActiva,
			RutaTropaFavorita = Preferencias.TropaFavorita(),
			ArdidFavorito     = Preferencias.ArdidFavorito(),
			AvisoSinFavoritos = $"Se revela tras {Preferencias.PARTIDAS_PARA_FAVORITOS} partidas " +
				$"({Mathf.Min(partidas, Preferencias.PARTIDAS_PARA_FAVORITOS)}/{Preferencias.PARTIDAS_PARA_FAVORITOS})",
		}, desdeRanking: false);
	}

	/// <summary>Perfil de otro jugador del ranking: se pide al servidor (solo datos públicos).</summary>
	private void AbrirPerfilDeJugador(int usuarioId)
	{
		if (usuarioId <= 0) return;
		var h = new HttpRequest { Timeout = 10 };
		AddChild(h);
		h.RequestCompleted += (long r, long c, string[] hd, byte[] b) =>
		{
			if (IsInstanceValid(h)) h.QueueFree();
			if (!IsInstanceValid(this)) return;
			if (r != (long)HttpRequest.Result.Success || c != 200)
			{
				MostrarAvisoGrande("Sin conexión", "No se pudo cargar el perfil de este jugador. Inténtalo de nuevo.",
					("ENTENDIDO", COLOR_BOTON_AVISO_PRINCIPAL, COLOR_TEXTO_BOTON_PRINCIPAL, null));
				return;
			}
			try
			{
				var d = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(b));
				int I(string k) => d.TryGetProperty(k, out var v) && v.TryGetInt32(out int n) ? n : 0;
				string S(string k) => d.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
				string tropa = S("tropaFavorita");
				string ardid = S("ardidFavorito");
				MostrarPanelPerfil(new DatosPerfil
				{
					Nombre            = S("nombre"),
					Experiencia       = I("experiencia"),
					Ganadas           = I("victorias"),
					Perdidas          = I("derrotas"),
					SkinIdx           = I("equipSkinIdx"),
					SkinExclusiva     = S("equipSkinExclusiva"),
					RutaTropaFavorita = tropa == "" ? null : tropa,
					ArdidFavorito     = ardid == "" ? null : ardid,
					AvisoSinFavoritos = "Todavía sin datos",
				}, desdeRanking: true);
			}
			catch (System.Exception e) { GD.PushWarning($"[Perfil] respuesta inválida: {e.Message}"); }
		};
		if (h.Request($"{ApiConfig.Usuarios}/{usuarioId}/perfil") != Error.Ok && IsInstanceValid(h)) h.QueueFree();
	}

	private void MostrarPanelPerfil(DatosPerfil d, bool desdeRanking)
	{
		GetNodeOrNull(NOMBRE_CAPA_PERFIL)?.QueueFree();

		// Capa propia por encima del ranking (300): al cerrarla, el ranking queda debajo como estaba.
		var capa = new CanvasLayer { Name = NOMBRE_CAPA_PERFIL, Layer = 310 };
		AddChild(capa);
		void Cerrar() { if (IsInstanceValid(capa)) capa.QueueFree(); }

		// Velo: atrapa el toque de afuera para cerrar. Desde el ranking oscurece más, para separarlo.
		var overlay = new ColorRect { Color = new Color(0, 0, 0, desdeRanking ? 0.45f : 0.12f), MouseFilter = Control.MouseFilterEnum.Stop };
		overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.GuiInput += (ev) => { if (ev is InputEventMouseButton mb && mb.Pressed) Cerrar(); };
		capa.AddChild(overlay);

		var panel = new PanelContainer();
		// Centrado de verdad: anclado al centro y creciendo PAREJO hacia los cuatro lados según su
		// contenido (con una caja fija más baja que el contenido, sobraba hacia abajo).
		panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		panel.GrowHorizontal = Control.GrowDirection.Both;
		panel.GrowVertical   = Control.GrowDirection.Both;
		panel.MouseFilter = Control.MouseFilterEnum.Stop;
		panel.GuiInput += (ev) => { if (ev is InputEventMouseButton) AcceptEvent(); };

		// Azul oscuro OPACO con el borde neón.
		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.05f, 0.08f, 0.17f, 1f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 3;
		sb.BorderColor = new Color(0.3f, 0.9f, 1.0f, 0.85f);
		sb.ShadowColor = new Color(0.2f, 0.9f, 1.0f, 0.35f);
		sb.ShadowSize = 14;
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 22;
		sb.ContentMarginLeft = sb.ContentMarginRight = 40;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 26;
		panel.AddThemeStyleboxOverride("panel", sb);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 16);
		panel.AddChild(vbox);

		// Cabecera: nombre + X
		var header = new HBoxContainer();
		var lblNombre = new Label { Text = d.Nombre, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
		EstiloUI.Texto(lblNombre, 48, new Color(0.4f, 0.95f, 1.0f));
		header.AddChild(lblNombre);
		var btnX = new Button { Text = "✕", CustomMinimumSize = new Vector2(76, 76) };
		EstiloUI.Boton(btnX, 36);
		btnX.Pressed += Cerrar;
		header.AddChild(btnX);
		vbox.AddChild(header);

		// Cuerpo apaisado (celular acostado): huevo y tropa favorita a la izquierda, estadísticas a la derecha.
		var cuerpo = new HBoxContainer();
		cuerpo.AddThemeConstantOverride("separation", 36);
		vbox.AddChild(cuerpo);

		cuerpo.AddChild(CrearColumnaHuevo(d));
		cuerpo.AddChild(CrearColumnaTropaFavorita(d));
		cuerpo.AddChild(new VSeparator());

		var stats = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(520, 0) };
		stats.AddThemeConstantOverride("separation", 14);
		cuerpo.AddChild(stats);

		void Fila(string etiqueta, string valor)
		{
			var fila = new HBoxContainer();
			fila.AddThemeConstantOverride("separation", 24);
			var k = new Label { Text = etiqueta, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			EstiloUI.Texto(k, 34, new Color(0.75f, 0.8f, 0.92f));
			var v = new Label { Text = valor };
			EstiloUI.Texto(v, 34, new Color(1f, 0.92f, 0.5f));
			fila.AddChild(k); fila.AddChild(v);
			stats.AddChild(fila);
		}

		Fila("Nivel",            Preferencias.NivelDeExperiencia(d.Experiencia).ToString());
		Fila("Experiencia",      $"{d.Experiencia} XP");
		if (d.Monedas.HasValue) Fila("Monedas", d.Monedas.Value.ToString());
		Fila("Partidas ganadas",  d.Ganadas.ToString());
		Fila("Partidas perdidas", d.Perdidas.ToString());
		Fila("Ardid favorito",    d.ArdidFavorito ?? "—");

		capa.AddChild(panel);
		// El tamaño final recién se conoce al acomodarse: el "pop" de entrada crece desde su centro.
		panel.Resized += () => { if (IsInstanceValid(panel)) panel.PivotOffset = panel.Size / 2; };
		panel.PivotOffset = panel.Size / 2;
		panel.Scale = new Vector2(0.7f, 0.7f);
		panel.CreateTween().TweenProperty(panel, "scale", Vector2.One, 0.22f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	private Control CrearColumnaHuevo(DatosPerfil d)
	{
		var col = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		col.AddThemeConstantOverride("separation", 8);

		string ruta; string nombre; float escala = 1f;
		int idxExclusiva = IndiceSkinExclusiva(d.SkinExclusiva);
		if (idxExclusiva >= 0)
		{
			// Siempre el render (el servidor guarda las de dev con otra imagen).
			ruta = SKINS_EXCLUSIVAS[idxExclusiva].render;
			nombre = SKINS_EXCLUSIVAS[idxExclusiva].nombre;
		}
		else if (!string.IsNullOrEmpty(d.SkinExclusiva) && ResourceLoader.Exists(d.SkinExclusiva))
		{
			ruta = d.SkinExclusiva;
			nombre = NombreSkinExclusiva(d.SkinExclusiva);
		}
		else
		{
			int idx = d.SkinIdx >= 0 && d.SkinIdx < Preferencias.SKIN_IMAGENES.Length ? d.SkinIdx : 0;
			ruta = idx == 0 ? RUTA_REY_HUEVO_CORONADO : Preferencias.SKIN_IMAGENES[idx];
			nombre = idx < Preferencias.SKIN_NOMBRES.Length ? Preferencias.SKIN_NOMBRES[idx] : "";
			escala = idx < SKIN_ESCALA_EXTRA.Length ? SKIN_ESCALA_EXTRA[idx] : 1f;
		}

		var tex = new TextureRect
		{
			CustomMinimumSize = TAMAÑO_HUEVO_REFERENCIA,
			ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
		};
		if (GD.Load<Texture2D>(ruta) is Texture2D tx) tex.Texture = tx;
		tex.PivotOffset = tex.CustomMinimumSize / 2f;
		tex.Scale = new Vector2(escala, escala);
		col.AddChild(tex);

		var lbl = new Label { Text = nombre, HorizontalAlignment = HorizontalAlignment.Center };
		EstiloUI.Texto(lbl, 30, new Color(0.85f, 0.9f, 1f));
		col.AddChild(lbl);
		return col;
	}

	/// <summary>"HuevoDorado_Render.png" → "Huevo Dorado".</summary>
	private static string NombreSkinExclusiva(string ruta)
	{
		string archivo = ruta.GetFile().GetBaseName().Replace("_Render", "");
		var sb = new StringBuilder();
		foreach (char c in archivo)
		{
			if (char.IsUpper(c) && sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
			sb.Append(c);
		}
		return sb.ToString();
	}

	// Tropa favorita: su imagen tal cual (ya trae su propio marco), del tamaño y la forma de la imagen.
	// Antes de tener partidas suficientes se muestra un "?" con cuántas faltan.
	private Control CrearColumnaTropaFavorita(DatosPerfil d)
	{
		const float ANCHO_CARTA = 220f;
		var col = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		col.AddThemeConstantOverride("separation", 8);

		var titulo = new Label { Text = "Tropa favorita", HorizontalAlignment = HorizontalAlignment.Center };
		EstiloUI.Texto(titulo, 26, EstiloUI.Dorado);
		col.AddChild(titulo);

		var (nombreCarta, imgCarta) = ResolverCartaPorRuta(d.RutaTropaFavorita);
		if (imgCarta != null)
		{
			Vector2 tamImg = imgCarta.GetSize();
			float alto = tamImg.X > 0 ? ANCHO_CARTA * tamImg.Y / tamImg.X : ANCHO_CARTA;
			col.AddChild(new TextureRect
			{
				ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				CustomMinimumSize = new Vector2(ANCHO_CARTA, alto),
				Texture = imgCarta,
			});
		}
		else
		{
			var lblVacio = new Label
			{
				Text = "?",
				CustomMinimumSize = new Vector2(ANCHO_CARTA, ANCHO_CARTA),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment   = VerticalAlignment.Center,
			};
			EstiloUI.Texto(lblVacio, 90, new Color(0.5f, 0.55f, 0.7f));
			col.AddChild(lblVacio);
		}

		var lblCarta = new Label
		{
			Text = nombreCarta ?? d.AvisoSinFavoritos,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(ANCHO_CARTA + 40, 0),
		};
		EstiloUI.Texto(lblCarta, nombreCarta != null ? 30 : 24, new Color(0.85f, 0.9f, 1f));
		col.AddChild(lblCarta);
		return col;
	}
}
