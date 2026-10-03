using Godot;

/// <summary>
/// Guías de primera vez dentro de la partida (ver GuiaPasos), después de la intro:
///   · VS BOT: el botón de PAUSA y, con la pausa abierta, CONTINUAR / OPCIONES / RENDIRSE. El juego
///     queda en pausa mientras se explica, así el reloj no corre.
///   · EN LÍNEA: ninguna (el rival sigue jugando y no se debe tapar nada).
/// </summary>
public partial class Campo1 : Node2D
{
	// Cada cuánto se revisa si ya es buen momento para abrir la guía, y cuánto se espera como mucho.
	private const double INTERVALO_GUIA_PARTIDA = 0.4;
	private const int    INTENTOS_GUIA_PARTIDA  = 150; // ~1 minuto

	private void ProgramarGuiaPartida()
	{
		// TerminarIntro también corre si la escena se está cerrando: sin árbol no hay nada que programar.
		// En línea no se abre ninguna guía: ahí cada segundo cuenta y no debe taparse nada.
		if (!IsInsideTree() || ModoTutorial || juegoTerminado || PareceOnline) return;
		if (Preferencias.GuiaVista("pausa")) return;
		// Un respiro tras la intro, para que no salte encima del primer "TU TURNO".
		GetTree().CreateTimer(0.7, false).Timeout += () => EsperarMomentoGuia(INTENTOS_GUIA_PARTIDA);
	}

	/// <summary>La guía se abre en TU turno, que no le cuesta nada al jugador: el juego se pausa, así que
	/// el reloj no corre (en el turno del bot se cortaría su jugada a la mitad).</summary>
	private void EsperarMomentoGuia(int intentosRestantes)
	{
		if (intentosRestantes <= 0 || !IsInstanceValid(this) || !IsInsideTree() || juegoTerminado) return;
		bool momento = !IntroEnCurso && !HayAnimacionEnCurso && esTurnoJugador;
		if (!momento)
		{
			GetTree().CreateTimer(INTERVALO_GUIA_PARTIDA, false).Timeout += () => EsperarMomentoGuia(intentosRestantes - 1);
			return;
		}
		IniciarGuiaPausa();
	}

	private void IniciarGuiaPausa()
	{
		var btnPausa = CapaHUD()?.GetNodeOrNull<Control>("PausaButton");
		var menu = GetNodeOrNull<MenuPausa>("MenuPausa");
		if (btnPausa == null || menu == null || !btnPausa.IsVisibleInTree()) return;

		bool laGuiaAbrioLaPausa = false;
		Rect2? BotonPausa(string nombre) => GuiaPasos.RectDe(menu.GetNodeOrNull("Overlay/VBox/" + nombre), 10f);

		var guia = GuiaPasos.Iniciar(this, "pausa", new[]
		{
			new GuiaPasos.Paso("Este es el botón de PAUSA. Detiene la partida contra el bot: el tiempo se " +
				"congela hasta que vuelvas.", () => GuiaPasos.RectDe(btnPausa, 8f)),
			new GuiaPasos.Paso("CONTINUAR te devuelve a la batalla justo donde la dejaste.",
				() => BotonPausa("BtnContinue"),
				() => { if (!menu.EstaAbierta) { menu.Pausar(); laGuiaAbrioLaPausa = true; } }),
			new GuiaPasos.Paso("OPCIONES: cambia el volumen, la música, los efectos y la vibración sin " +
				"salir de la partida.", () => BotonPausa("BtnSettings")),
			new GuiaPasos.Paso("RENDIRSE termina la partida. Contra el bot no suma derrota en tu " +
				"cuenta, pero tampoco da monedas.", () => BotonPausa("BtnExit")),
		},
		alTerminar: () =>
		{
			// Se deja todo como estaba: si la pausa la abrió la guía, se cierra; si el jugador ya la tenía
			// abierta (p. ej. volvió de otra app), queda abierta y en pausa.
			if (laGuiaAbrioLaPausa && menu.EstaAbierta) menu.Reanudar();
			else if (!menu.EstaAbierta) GetTree().Paused = false;
		},
		pausarJuego: true);

		if (guia != null) guia.CerrarSi = () => juegoTerminado;
	}
}
