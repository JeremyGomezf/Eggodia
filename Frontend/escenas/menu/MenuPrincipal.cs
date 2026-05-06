using Godot;

public partial class MenuPrincipal : Control
{
	[Export] public string RutaEscenaJuego     = "res://escenas/gameplay/campo_1.tscn";
	[Export] public string RutaConstructorMazo = "res://DatosCartas/MenuConstructor.tscn";

	public override void _Ready()
	{
		var btnJugar    = GetNodeOrNull<Button>("VBoxContainer/JUGAR");
		var btnOpciones = GetNodeOrNull<Button>("VBoxContainer/OPCIONES");
		var btnSalir    = GetNodeOrNull<Button>("VBoxContainer/SALIR");

		if (btnJugar    != null) btnJugar.Pressed    += () => GetTree().ChangeSceneToFile(RutaEscenaJuego);
		if (btnOpciones != null) btnOpciones.Pressed += () => GetTree().ChangeSceneToFile(RutaConstructorMazo);
		if (btnSalir    != null) btnSalir.Pressed    += () => GetTree().Quit();

		// Botón Ranking — se añade dinámicamente al VBoxContainer
		var vbox = GetNodeOrNull<VBoxContainer>("VBoxContainer");
		if (vbox != null)
		{
			var btnRanking = new Button();
			btnRanking.Text                = "🏆 Ranking";
			btnRanking.CustomMinimumSize   = new Vector2(250, 55);
			btnRanking.AddThemeFontSizeOverride("font_size", 22);
			btnRanking.SelfModulate        = Colors.Gold;
			btnRanking.Pressed            += MostrarRanking;
			vbox.AddChild(btnRanking);
		}

		MostrarInfoJugador();
	}

	// Muestra nombre, racha y estado de sesión en la parte superior
	private void MostrarInfoJugador()
	{
		var sesion = SesionJuego.Instance;
		if (sesion == null) return;

		string texto;
		Color  color;

		if (sesion.EstaLogueado)
		{
			texto = sesion.RachaActual > 1
				? $"👤 {sesion.NombreJugador}   🔥 Racha: {sesion.RachaActual}"
				: $"👤 {sesion.NombreJugador}";
			color = Colors.Gold;
		}
		else
		{
			texto = "🎮 Jugando como Invitado  —  Inicia sesión para guardar tu progreso";
			color = new Color(0.8f, 0.8f, 0.8f);
		}

		var lbl = new Label();
		lbl.Text = texto;
		lbl.AddThemeColorOverride("font_color", color);
		lbl.AddThemeFontSizeOverride("font_size", 18);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		lbl.OffsetTop    = 18;
		lbl.OffsetLeft   = 0;
		lbl.OffsetRight  = 0;
		lbl.OffsetBottom = 50;
		AddChild(lbl);
	}

	private void MostrarRanking()
	{
		// Evitar abrir dos veces
		if (GetNodeOrNull("PanelRanking") != null) return;
		var panel = new PanelRanking();
		panel.Name = "PanelRanking";
		AddChild(panel);
		panel.Mostrar();
	}
}
