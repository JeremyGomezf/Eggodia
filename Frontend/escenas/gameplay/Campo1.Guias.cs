using Godot;

/// <summary>
/// Guías de primera vez dentro de la partida (ver GuiaPasos), después de la intro:
///   · VS BOT: el botón de PAUSA y, con la pausa abierta, CONTINUAR / OPCIONES / RENDIRSE. El juego
///     queda en pausa mientras se explica, así el reloj no corre.
///   · EN LÍNEA: el botón de RETIRADA (no hay pausa) y qué pasa si te desconectas o no juegas. Aquí
///     el juego NO se pausa (el rival sigue), por eso es corta.
/// </summary>
public partial class Campo1 : Node2D
{
	// Cada cuánto se revisa si ya es buen momento para abrir la guía, y cuánto se espera como mucho.
	private const double INTERVALO_GUIA_PARTIDA = 0.4;
	private const int    INTENTOS_GUIA_PARTIDA  = 150; // ~1 minuto

	private void ProgramarGuiaPartida()
	{
		// TerminarIntro también corre si la escena se está cerrando: sin árbol no hay nada que programar.
		if (!IsInsideTree() || ModoTutorial || juegoTerminado) return;
		if (Preferencias.GuiaVista(PareceOnline ? "retirada" : "pausa")) return;
		// Un respiro tras la intro, para que no salte encima del primer "TU TURNO".
		GetTree().CreateTimer(0.7, false).Timeout += () => EsperarMomentoGuia(INTENTOS_GUIA_PARTIDA);
	}

	/// <summary>La guía se abre en un momento que no le cueste nada al jugador:
	///   · contra el bot, en TU turno (el juego se pausa, así que el reloj no corre; en el turno del bot
	///     se cortaría su jugada a la mitad);
	///   · en línea, en el turno del RIVAL (no se pausa: así no te come tiempo de tu turno).</summary>
	private void EsperarMomentoGuia(int intentosRestantes)
	{
		if (intentosRestantes <= 0 || !IsInstanceValid(this) || !IsInsideTree() || juegoTerminado) return;
		bool momento = !IntroEnCurso && !HayAnimacionEnCurso
			&& (PareceOnline ? !esTurnoJugador : esTurnoJugador);
		if (!momento)
		{
			GetTree().CreateTimer(INTERVALO_GUIA_PARTIDA, false).Timeout += () => EsperarMomentoGuia(intentosRestantes - 1);
			return;
		}
		if (PareceOnline) IniciarGuiaRetirada();
		else IniciarGuiaPausa();
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
			new GuiaPasos.Paso("RENDIRSE termina la partida y cuenta como derrota. Úsalo solo si de " +
				"verdad quieres abandonar.", () => BotonPausa("BtnExit")),
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

	private void IniciarGuiaRetirada()
	{
		var capa = CapaHUD();
		var btnRetirada = capa?.FindChild("RetiradaButton", true, false) as Control;
		if (btnRetirada == null || !btnRetirada.IsVisibleInTree()) return;
		var turno = capa.GetNodeOrNull<Control>("TurnoPanel");

		var guia = GuiaPasos.Iniciar(this, "retirada", new[]
		{
			new GuiaPasos.Paso("En línea no hay pausa: tu rival sigue jugando. Este huevo roto es la " +
				"RETIRADA: si la usas, pierdes y el rival gana.", () => GuiaPasos.RectDe(btnRetirada, 8f)),
			new GuiaPasos.Paso("Ojo: si te desconectas o dejas pasar varios turnos sin jugar, también " +
				"pierdes la partida.", () => GuiaPasos.RectDe(turno, 8f)),
		});
		if (guia != null) guia.CerrarSi = () => juegoTerminado;
	}
}
