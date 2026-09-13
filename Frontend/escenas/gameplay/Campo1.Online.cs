using Godot;

/// <summary>
/// Multijugador en línea dentro de la batalla (Fase 2).
/// Milestone A: al entrar desde el emparejamiento (ContextoOnline.Activo), la CPU se apaga y se
/// muestra el nombre del rival. La sincronización real de turnos (enviar/recibir el estado del
/// tablero) llega en el Milestone B.
/// </summary>
public partial class Campo1 : Node2D
{
	private void ConfigurarModoOnline()
	{
		EsOnline = ContextoOnline.Activo;
		if (!EsOnline) return;

		// Banner superior con el nombre del rival.
		var capa = new CanvasLayer { Layer = 90 };
		AddChild(capa);

		var lbl = new Label
		{
			Text = $"🌐 EN LÍNEA — vs {ContextoOnline.RivalNombre}",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		lbl.AddThemeFontSizeOverride("font_size", 34);
		lbl.AddThemeColorOverride("font_color", new Color(0.6f, 0.9f, 1f));
		lbl.AddThemeColorOverride("font_outline_color", Colors.Black);
		lbl.AddThemeConstantOverride("outline_size", 6);
		lbl.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		lbl.OffsetTop = 12; lbl.OffsetBottom = 60;
		capa.AddChild(lbl);

		MostrarAviso($"Partida en línea contra {ContextoOnline.RivalNombre}", new Color(0.6f, 0.9f, 1f));
	}

	// La CPU está apagada en online (ver el gate en EjecutarTurnoCPU). En el Milestone A el turno del
	// rival aún no se sincroniza; solo se avisa que se espera. El Milestone B pondrá aquí el sondeo
	// del estado del tablero enviado por el otro jugador.
	private void EsperarRivalOnline()
	{
		MostrarAviso("Esperando al rival…", new Color(1f, 0.85f, 0.4f));
	}
}
