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
	public static void Boton(Button b, int fontSize = 0, bool rojo = false)
	{
		if (b == null) return;
		Color baseCol   = rojo ? new Color(0.42f, 0.16f, 0.16f, 1f) : new Color(0.16f, 0.22f, 0.32f, 1f);
		Color hoverCol  = rojo ? new Color(0.58f, 0.22f, 0.22f, 1f) : new Color(0.24f, 0.32f, 0.46f, 1f);
		Color bordeCol  = rojo ? new Color(0.9f, 0.5f, 0.5f, 0.8f)  : new Color(0.4f, 0.55f, 0.75f, 0.7f);
		Color bordeHov  = rojo ? new Color(1f, 0.65f, 0.65f, 0.95f) : new Color(0.65f, 0.8f, 1f, 0.9f);
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

	/// <summary>Título del juego (fuente + dorado + tamaño).</summary>
	public static void Titulo(Label l, int fontSize) => Texto(l, fontSize, Dorado);
}
