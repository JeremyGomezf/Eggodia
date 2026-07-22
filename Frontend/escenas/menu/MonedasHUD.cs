using Godot;

/// <summary>
/// Chip visual que muestra el saldo de monedas. Se actualiza solo vía la señal
/// Economia.MonedasCambiaron. Úsalo en menús, tienda, pantalla de resultados, etc.
///
/// Uso por código:  var hud = MonedasHUD.Crear(); parent.AddChild(hud);
/// </summary>
public partial class MonedasHUD : PanelContainer
{
	private Label _lbl;

	public static MonedasHUD Crear()
	{
		var hud = new MonedasHUD();
		return hud;
	}

	public override void _Ready()
	{
		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.10f, 0.12f, 0.06f, 0.92f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.BorderColor = new Color(0.95f, 0.78f, 0.25f, 0.8f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 18;
		sb.ContentMarginLeft = sb.ContentMarginRight = 14;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 6;
		AddThemeStyleboxOverride("panel", sb);

		var hb = new HBoxContainer();
		hb.AddThemeConstantOverride("separation", 8);
		AddChild(hb);

		// "Moneda" — círculo dorado con símbolo
		var moneda = new Panel();
		moneda.CustomMinimumSize = new Vector2(22, 22);
		var msb = new StyleBoxFlat();
		msb.BgColor = new Color(1f, 0.82f, 0.25f);
		msb.BorderWidthLeft = msb.BorderWidthTop = msb.BorderWidthRight = msb.BorderWidthBottom = 2;
		msb.BorderColor = new Color(0.75f, 0.55f, 0.1f);
		msb.CornerRadiusTopLeft = msb.CornerRadiusTopRight =
		msb.CornerRadiusBottomLeft = msb.CornerRadiusBottomRight = 11;
		moneda.AddThemeStyleboxOverride("panel", msb);
		var simbolo = new Label();
		simbolo.Text = "$";
		simbolo.AddThemeColorOverride("font_color", new Color(0.4f, 0.28f, 0.05f));
		simbolo.AddThemeFontSizeOverride("font_size", 14);
		simbolo.HorizontalAlignment = HorizontalAlignment.Center;
		simbolo.VerticalAlignment   = VerticalAlignment.Center;
		simbolo.AnchorRight = 1; simbolo.AnchorBottom = 1;
		moneda.AddChild(simbolo);
		hb.AddChild(moneda);

		_lbl = new Label();
		_lbl.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.6f));
		_lbl.AddThemeFontSizeOverride("font_size", 18);
		_lbl.VerticalAlignment = VerticalAlignment.Center;
		hb.AddChild(_lbl);

		var eco = Economia.Instancia();
		eco.MonedasCambiaron += OnMonedasCambiaron;
		Refrescar(eco.Monedas);
	}

	public override void _ExitTree()
	{
		if (Economia.Instance != null)
			Economia.Instance.MonedasCambiaron -= OnMonedasCambiaron;
	}

	private void OnMonedasCambiaron(int total)
	{
		Refrescar(total);
		// Pequeño pop al cambiar
		PivotOffset = Size / 2;
		var tw = CreateTween();
		tw.TweenProperty(this, "scale", new Vector2(1.12f, 1.12f), 0.08f);
		tw.TweenProperty(this, "scale", Vector2.One, 0.12f);
	}

	private void Refrescar(int total) { if (_lbl != null) _lbl.Text = total.ToString(); }
}
