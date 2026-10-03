using Godot;

/// <summary>
/// Estilo visual UNIFICADO del juego, para la UI creada por CÓDIGO (que antes usaba el look gris por
/// defecto de Godot). Reusa la misma tipografía (Almendra-Bold) y los mismos StyleBox/colores que las
/// pantallas ya diseñadas (login, ajustes, cómo jugar…) para que todo combine.
///
/// Uso: EstiloUI.Boton(miBoton); EstiloUI.Panel(miPanel); EstiloUI.Titulo(miLabel);
/// </summary>
public static class EstiloUI
{
	public static readonly Font Fuente =
		ResourceLoader.Exists("res://Almendra-Bold.ttf") ? GD.Load<Font>("res://Almendra-Bold.ttf") : null;

	// Paleta del juego (misma que usan las pantallas diseñadas).
	public static readonly Color TextoClaro = new(0.95f, 0.97f, 1f);
	public static readonly Color Acento     = new(0.65f, 0.80f, 1f);   // azul claro (bordes/deco)
	public static readonly Color Dorado     = new(1f, 0.82f, 0.30f);
	public static readonly Color Peligro    = new(1f, 0.42f, 0.38f);
	// Paleta del MARCO DORADO (login): azul marino profundo del interior + oro del borde.
	public static readonly Color NavyMarco  = new(0.09f, 0.13f, 0.24f, 1f);
	public static readonly Color OroBorde   = new(0.83f, 0.65f, 0.22f, 1f);
	public static readonly Color VerdeOk    = new(0.42f, 1f, 0.45f);   // "desbloqueado/equipado"

	// ── MARCOS DE TEXTURA REALES DEL JUEGO (nine-patch) ───────────────────────
	// Reutilizan imágenes que ya están en el juego, así que no suman peso al APK.
	private static readonly Texture2D _texMarcoDorado =
		ResourceLoader.Exists("res://imagenes/login/login_panel.png")
			? GD.Load<Texture2D>("res://imagenes/login/login_panel.png") : null;

	/// <summary>Marco EXCLUSIVO del panel de configuración/ajustes (menú y pausa). Mide igual que
	/// login_panel.png (978×1175), así que usa el mismo nine-patch. La escena de login NO lo usa:
	/// ahí se mantiene login_panel.png a propósito.</summary>
	public static readonly Texture2D MarcoAjustes =
		ResourceLoader.Exists("res://imagenes/login/login_panel2.png")
			? GD.Load<Texture2D>("res://imagenes/login/login_panel2.png") : null;
	private static readonly Texture2D _texMarcoCristal =
		ResourceLoader.Exists("res://imagenes/botonescampo1/ContadorTurno.png")
			? GD.Load<Texture2D>("res://imagenes/botonescampo1/ContadorTurno.png") : null;

	/// <summary>¿Hay marco dorado disponible? (para decidir padding del contenido).</summary>
	public static bool HayMarcoDorado => _texMarcoDorado != null;

	/// <summary>Marco DORADO del login (con la gema de huevo) como fondo de un PanelContainer, con
	/// nine-patch: el borde de oro no se deforma al crecer, solo se estira el interior azul. Para MENÚS
	/// (ajustes, ranking, tienda…). Si falta la imagen, cae al panel "vidrio" de código.</summary>
	public static void MarcoDorado(PanelContainer p, int padX = 52, int padTop = 150, int padBottom = 60,
		Texture2D textura = null)
	{
		if (p == null) return;
		var tex = textura ?? _texMarcoDorado;
		if (tex == null) { Panel(p); return; }
		var st = new StyleBoxTexture { Texture = tex };
		// Nine-patch (px de la imagen 978×1175 que NO se estiran): esquinas de oro y la gema de arriba.
		st.TextureMarginLeft = st.TextureMarginRight = 92;
		st.TextureMarginTop  = 150;   // incluye la gema de huevo + borde superior
		st.TextureMarginBottom = 92;
		// Padding: el contenido cae dentro del azul, sin pisar el oro ni la gema (por eso padTop mayor).
		st.ContentMarginLeft = st.ContentMarginRight = padX;
		st.ContentMarginTop  = padTop; st.ContentMarginBottom = padBottom;
		p.AddThemeStyleboxOverride("panel", st);
	}

	/// <summary>Marco de CRISTAL azul del HUD (ContadorTurno.png) como fondo de un PanelContainer, con
	/// nine-patch. Para elementos DENTRO de la batalla (p. ej. popup NFC), para que combine con el combate.</summary>
	public static void MarcoCristal(PanelContainer p, int padX = 46, int padY = 30)
	{
		if (p == null) return;
		if (_texMarcoCristal == null) { Panel(p); return; }
		var st = new StyleBoxTexture { Texture = _texMarcoCristal };
		st.TextureMarginLeft = st.TextureMarginRight = 60;
		st.TextureMarginTop  = 46; st.TextureMarginBottom = 50;
		st.ContentMarginLeft = st.ContentMarginRight = padX;
		st.ContentMarginTop  = padY; st.ContentMarginBottom = padY;
		p.AddThemeStyleboxOverride("panel", st);
	}

	/// <summary>StyleBoxFlat con la PALETA DORADA nuestra (azul marino + borde de oro), para tarjetas/
	/// cuadros chicos donde poner la textura completa sería recargado (p. ej. ítems de la tienda).
	/// Si "resaltado" (poseído/equipado) usa borde verde.</summary>
	public static StyleBoxFlat CuadroDorado(bool resaltado = false)
	{
		var sb = new StyleBoxFlat();
		// Fondo azul marino BIEN oscuro para que el borde de oro resalte (antes se confundía con el gris).
		sb.BgColor = resaltado ? new Color(0.06f, 0.16f, 0.09f, 0.98f) : new Color(0.05f, 0.08f, 0.16f, 0.98f);
		sb.BorderColor = resaltado ? VerdeOk : new Color(1f, 0.78f, 0.28f, 1f); // oro brillante y opaco
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 5; // más grueso: se nota
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 14;
		sb.ContentMarginLeft = sb.ContentMarginRight =
		sb.ContentMarginTop  = sb.ContentMarginBottom = 14;
		sb.ShadowColor = new Color(0.6f, 0.45f, 0.1f, 0.35f); sb.ShadowSize = 8; // leve halo dorado
		return sb;
	}

	private static StyleBoxFlat SB(Color bg, Color borde, int radio, int bordeGrosor, int mx, int my)
	{
		var sb = new StyleBoxFlat { BgColor = bg, BorderColor = borde };
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = bordeGrosor;
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = radio;
		sb.ContentMarginLeft = sb.ContentMarginRight = mx;
		sb.ContentMarginTop  = sb.ContentMarginBottom = my;
		return sb;
	}

	/// <summary>Aplica el estilo de botón del juego (fondo/hover/pressed, borde, fuente y texto claro).
	/// Si "rojo" es true usa el tono de peligro (rendirse/cerrar sesión).</summary>
	/// <summary>Estilo de botón del juego. <paramref name="rojo"/> para los destructivos (rendirse,
	/// cerrar sesión) y <paramref name="accion"/> para el botón PRINCIPAL de una pantalla (reintentar,
	/// jugar de nuevo, re-armar mazo): va en turquesa vivo, el mismo del BATALLAR del constructor, para
	/// que se distinga del azul apagado del resto y no parezca un botón gris más.</summary>
	public static void Boton(Button b, int fontSize = 0, bool rojo = false, bool accion = false)
	{
		if (b == null) return;
		Color baseCol   = accion ? new Color(0.07f, 0.52f, 0.62f, 1f)
						: rojo   ? new Color(0.42f, 0.16f, 0.16f, 1f) : new Color(0.16f, 0.22f, 0.32f, 1f);
		Color hoverCol  = accion ? new Color(0.12f, 0.70f, 0.84f, 1f)
						: rojo   ? new Color(0.58f, 0.22f, 0.22f, 1f) : new Color(0.24f, 0.32f, 0.46f, 1f);
		Color bordeCol  = accion ? new Color(0.55f, 0.95f, 1f, 0.85f)
						: rojo   ? new Color(0.9f, 0.5f, 0.5f, 0.8f)  : new Color(0.4f, 0.55f, 0.75f, 0.7f);
		Color bordeHov  = accion ? new Color(0.85f, 1f, 1f, 1f)
						: rojo   ? new Color(1f, 0.65f, 0.65f, 0.95f) : new Color(0.65f, 0.8f, 1f, 0.9f);
		var hov = SB(hoverCol, bordeHov, 10, 1, 22, 11);
		b.AddThemeStyleboxOverride("normal",  SB(baseCol, bordeCol, 10, 1, 22, 11));
		b.AddThemeStyleboxOverride("hover",   hov);
		b.AddThemeStyleboxOverride("pressed", hov);
		b.AddThemeStyleboxOverride("focus",   new StyleBoxEmpty());
		if (Fuente != null) b.AddThemeFontOverride("font", Fuente);
		b.AddThemeColorOverride("font_color", TextoClaro);
		if (fontSize > 0) b.AddThemeFontSizeOverride("font_size", fontSize);
	}

	/// <summary>Aplica el "panel vidrio oscuro con borde" del juego a un PanelContainer.</summary>
	public static void Panel(PanelContainer p)
	{
		if (p == null) return;
		var sb = SB(new Color(0.10f, 0.12f, 0.18f, 0.98f), new Color(0.40f, 0.60f, 0.90f, 0.6f), 18, 2, 28, 24);
		sb.ShadowColor = new Color(0.4f, 0.2f, 0.8f, 0.4f); sb.ShadowSize = 12;
		p.AddThemeStyleboxOverride("panel", sb);
	}

	/// <summary>Fuente del juego + tamaño/color opcionales para un Label.</summary>
	public static void Texto(Label l, int fontSize = 0, Color? color = null)
	{
		if (l == null) return;
		if (Fuente != null) l.AddThemeFontOverride("font", Fuente);
		if (fontSize > 0) l.AddThemeFontSizeOverride("font_size", fontSize);
		if (color.HasValue) l.AddThemeColorOverride("font_color", color.Value);
	}

	/// <summary>Pantallas de Victoria/Derrota: el resumen (estadísticas + MVT) se leía muy chico en el
	/// celular. Se agranda la letra y la foto del MVT, y se aprovecha el ANCHO (la columna era de solo
	/// ~650 px) para no sumar altura: esas pantallas se achican enteras si el bloque no entra en el alto,
	/// así que crecer hacia abajo dejaría todo igual de chico. <paramref name="columnasStats"/> = 4 pone
	/// las estadísticas de a dos por fila.</summary>
	public static void AgrandarResumenFinPartida(Control vbox, int columnasStats = 2)
	{
		if (vbox == null) return;
		vbox.CustomMinimumSize = new Vector2(Mathf.Max(vbox.CustomMinimumSize.X, 1000f), vbox.CustomMinimumSize.Y);

		if (vbox.GetNodeOrNull<GridContainer>("PanelStats/StatsGrid") is GridContainer grid)
		{
			grid.Columns = columnasStats;
			grid.AddThemeConstantOverride("h_separation", 36);
			grid.AddThemeConstantOverride("v_separation", 10);
			foreach (Node n in grid.GetChildren())
				if (n is Label l) l.AddThemeFontSizeOverride("font_size", l.Name.ToString().EndsWith("V") ? 36 : 32);
		}

		if (vbox.GetNodeOrNull<TextureRect>("PanelMVT/MVTBox/MVTFoto") is TextureRect foto)
			foto.CustomMinimumSize = new Vector2(120, 120);
		if (vbox.GetNodeOrNull<Control>("PanelMVT/MVTBox") is HBoxContainer caja)
			caja.AddThemeConstantOverride("separation", 22);
		void Letra(string ruta, int tam) { if (vbox.GetNodeOrNull<Label>(ruta) is Label l) l.AddThemeFontSizeOverride("font_size", tam); }
		Letra("PanelMVT/MVTBox/MVTInfo/MVTTitulo", 24);
		Letra("PanelMVT/MVTBox/MVTInfo/MVTNombre", 38);
		Letra("PanelMVT/MVTBox/MVTInfo/MVTStat",   28);
	}

	/// <summary>Título del juego (fuente + dorado + tamaño).</summary>
	public static void Titulo(Label l, int fontSize) => Texto(l, fontSize, Dorado);
}
