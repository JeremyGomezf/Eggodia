using Godot;

public partial class PantallaComoJugar : Control
{
	// La guía es una pantalla de teléfono: agrandamos TODAS las letras para que se lean bien.
	[Export] public float EscalaFuenteMovil = 1.4f;

	// Modo CAPA: cuando la guía se abre DESDE una partida en curso (menú de pausa), no se cambia de
	// escena (eso destruiría la partida y al volver caías al menú). Se muestra encima de la partida
	// pausada y "volver" solo cierra la capa → se regresa a la partida. AlVolver define ese cierre.
	public bool EnModoCapa = false;
	public System.Action AlVolver;

	public override void _Ready()
	{
		EscalarFuentes(this);

		var btn = GetNodeOrNull<Button>("Root/Header/HBox/BtnVolver");
		if (btn != null)
		{
			if (EnModoCapa)
				btn.Pressed += () => AlVolver?.Invoke();     // volver = cerrar la capa, seguir en la partida
			else
				btn.Pressed += () => GetTree().ChangeSceneToFile("res://escenas/menu/menu_principal.tscn");
		}
	}

	// En modo capa, el botón "atrás"/Escape también cierra la guía (y no deja que el menú de pausa de
	// abajo reaccione al mismo evento). Fuera de modo capa no hace nada especial.
	public override void _UnhandledInput(InputEvent @event)
	{
		if (EnModoCapa && @event.IsActionPressed("ui_cancel"))
		{
			GetViewport().SetInputAsHandled();
			AlVolver?.Invoke();
		}
	}

	// Tipografía del juego (la misma del menú/login). Se aplica a toda la pantalla para que "Cómo Jugar"
	// combine con el resto del juego en vez de usar la fuente por defecto de Godot.
	private static readonly Font FuenteJuego =
		ResourceLoader.Exists("res://Almendra-Bold.ttf") ? GD.Load<Font>("res://Almendra-Bold.ttf") : null;

	// Aplica la fuente del juego y agranda las letras de todos los controles con texto (para móvil).
	private void EscalarFuentes(Node nodo)
	{
		if (nodo is Label || nodo is Button)
		{
			var c = (Control)nodo;
			if (FuenteJuego != null) c.AddThemeFontOverride("font", FuenteJuego); // misma tipografía del juego
			int actual = c.GetThemeFontSize("font_size");
			if (actual > 0)
				c.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(actual * EscalaFuenteMovil));
		}
		foreach (var hijo in nodo.GetChildren())
			EscalarFuentes(hijo);
	}
}
