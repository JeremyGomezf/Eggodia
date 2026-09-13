using Godot;
using System.Collections.Generic;

public partial class PantallaBestiario : Control
{
	private record TropaInfo(string Nombre, string Era, string Tipo, int HP, int Ataque, int Escudo, string Habilidad);

	private static readonly List<TropaInfo> _tropas = new()
	{
		new("Dragón de Flama",  "Mística",    Tipos.FUEGO,      350, 280, 250, "Aliento de fuego: daño AoE al 40% del ataque"),
		new("Golem Pedregal",   "Mística",    Tipos.METAL,      450, 350, 500, "Muralla: absorbe daño masivo"),
		new("Maguín",           "Mística",    Tipos.AGUA,       220, 200, 150, "Ventisca: 100 daño de hielo y bloqueo 1 turno"),
		new("Soldado Real",     "Medieval",   Tipos.METAL,      280, 200, 300, "Formación: +150 escudo a todos los aliados"),
		new("T-Rex Z",          "Primordial", Tipos.NATURALEZA, 400, 400, 350, "Rugido primordial: -30% ataque enemigo 2 turnos"),
		new("Tiburón",          "Primordial", Tipos.AGUA,       260, 320, 180, "Mordida feroz: siguiente ataque hace x2 daño"),
		new("Peón",             "Medieval",   Tipos.NATURALEZA, 150, 100, 150, "Sacrificio heroico: cura al aliado más débil"),
		new("Calamar Gigante",  "Primordial", Tipos.SOMBRA,     400, 370, 380, "Depredador acuático"),
		new("Caballo",          "Medieval",   Tipos.METAL,      300, 250, 200, "Carga rápida"),
		new("Dama",             "Medieval",   Tipos.SOMBRA,     220, 180, 200, "Inspiración real: +100 ATK aliados 2 turnos"),
		new("Torre",            "Medieval",   Tipos.METAL,      350, 220, 400, "Forma gigante: se agranda para tanquear"),
		new("Soldado Moderno",  "Moderna",    Tipos.METAL,      260, 200, 250, "Ráfaga: 4 balas + cobertura sostenida"),
	};

	public override void _Ready()
	{
		var btn = GetNodeOrNull<Button>("Root/Header/HBox/BtnVolver");
		if (btn != null)
			btn.Pressed += () => GetTree().ChangeSceneToFile("res://escenas/menu/menu_principal.tscn");

		var grid = GetNodeOrNull<GridContainer>("Root/Scroll/Margin/Grid");
		if (grid == null) return;
		foreach (var t in _tropas)
			grid.AddChild(ConstruirCard(t));
	}

	private Control ConstruirCard(TropaInfo t)
	{
		var card = new PanelContainer();
		card.CustomMinimumSize = new Vector2(280, 200);

		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.09f, 0.12f, 0.18f, 0.95f);
		sb.BorderWidthLeft = 4;
		sb.BorderColor = Tipos.Color(t.Tipo);
		sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 1;
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 12;
		sb.ContentMarginLeft = sb.ContentMarginRight = 14;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 12;
		sb.ShadowColor = new Color(0, 0, 0, 0.4f);
		sb.ShadowSize = 5;
		card.AddThemeStyleboxOverride("panel", sb);

		var vb = new VBoxContainer();
		vb.AddThemeConstantOverride("separation", 6);
		card.AddChild(vb);

		// Header: nombre + chip de tipo
		var head = new HBoxContainer();
		head.AddThemeConstantOverride("separation", 8);
		var nombre = new Label();
		nombre.Text = t.Nombre;
		nombre.AddThemeColorOverride("font_color", new Color(0.98f, 0.99f, 1f));
		nombre.AddThemeFontSizeOverride("font_size", 18);
		nombre.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		head.AddChild(nombre);
		head.AddChild(ChipTipo(t.Tipo));
		vb.AddChild(head);

		var era = new Label();
		era.Text = "Era " + t.Era;
		era.AddThemeColorOverride("font_color", new Color(0.55f, 0.65f, 0.82f));
		era.AddThemeFontSizeOverride("font_size", 13);
		vb.AddChild(era);

		var sep = new HSeparator();
		vb.AddChild(sep);

		// Stats grid
		var stats = new GridContainer();
		stats.Columns = 3;
		stats.AddThemeConstantOverride("h_separation", 12);
		vb.AddChild(stats);
		stats.AddChild(StatBox("HP",     t.HP.ToString(),      new Color(0.4f, 1f, 0.5f)));
		stats.AddChild(StatBox("ATK",    t.Ataque.ToString(),  new Color(1f, 0.7f, 0.3f)));
		stats.AddChild(StatBox("Escudo", t.Escudo.ToString(),  new Color(0.5f, 0.7f, 1f)));

		// Habilidad
		var hab = new Label();
		hab.Text = t.Habilidad;
		hab.AutowrapMode = TextServer.AutowrapMode.Word;
		hab.AddThemeColorOverride("font_color", new Color(0.85f, 0.88f, 0.94f));
		hab.AddThemeFontSizeOverride("font_size", 13);
		vb.AddChild(hab);

		return card;
	}

	private Control StatBox(string label, string value, Color acento)
	{
		var vb = new VBoxContainer();
		var l = new Label();
		l.Text = label;
		l.AddThemeColorOverride("font_color", new Color(0.6f, 0.7f, 0.85f));
		l.AddThemeFontSizeOverride("font_size", 11);
		vb.AddChild(l);
		var v = new Label();
		v.Text = value;
		v.AddThemeColorOverride("font_color", acento);
		v.AddThemeFontSizeOverride("font_size", 17);
		vb.AddChild(v);
		return vb;
	}

	private Control ChipTipo(string tipo)
	{
		var chip = new PanelContainer();
		chip.CustomMinimumSize = new Vector2(88, 24);
		var sb = new StyleBoxFlat();
		sb.BgColor = Tipos.Color(tipo);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 6;
		sb.ContentMarginLeft = sb.ContentMarginRight = 6;
		chip.AddThemeStyleboxOverride("panel", sb);
		var l = new Label();
		l.Text = Tipos.Etiqueta(tipo);
		l.AddThemeColorOverride("font_color", Colors.White);
		l.AddThemeFontSizeOverride("font_size", 11);
		l.HorizontalAlignment = HorizontalAlignment.Center;
		l.VerticalAlignment   = VerticalAlignment.Center;
		chip.AddChild(l);
		return chip;
	}
}
