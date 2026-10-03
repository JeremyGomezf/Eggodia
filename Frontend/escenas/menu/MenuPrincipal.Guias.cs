using Godot;
using System;

/// <summary>Guías de primera vez del menú principal y de sus Ajustes (ver GuiaPasos).</summary>
public partial class MenuPrincipal : Control
{
	/// <summary>Recorrido por los botones del menú, la primera vez que se llega a él en este aparato.
	/// Espera un momento a que el menú termine de acomodarse; si en ese rato aparece el aviso de
	/// actualización obligatoria, la guía queda para la próxima.</summary>
	// Hijos que tenía el menú recién armado: lo que se agregue después y se vea es una ventana abierta
	// (perfil, skins, códigos, ranking, en línea...).
	private int _hijosMenuArmado = -1;

	private void ProgramarGuiaMenu()
	{
		if (Preferencias.GuiaVista("menu")) return;
		Callable.From(() => _hijosMenuArmado = GetChildCount()).CallDeferred();
		GetTree().CreateTimer(0.6, false).Timeout += () => IntentarGuiaMenu(60);
	}

	/// <summary>¿Hay alguna ventana abierta encima del menú? Si el jugador ya abrió algo (tocó rápido el
	/// huevo, su perfil...), la guía espera a que lo cierre en vez de explicar botones tapados.</summary>
	private bool HayVentanaAbiertaEnMenu()
	{
		if (_panelSettings != null && _panelSettings.Visible) return true;
		if (_hijosMenuArmado < 0) return false;
		for (int i = _hijosMenuArmado; i < GetChildCount(); i++)
		{
			Node h = GetChild(i);
			if (h == _capaSinConexion || h is GuiaPasos) continue; // el aviso sin conexión no tapa nada
			if (h is CanvasItem ci && ci.Visible) return true;
			if (h is CanvasLayer cl && cl.Visible) return true;
		}
		return false;
	}

	private void IntentarGuiaMenu(int intentosRestantes)
	{
		if (!IsInstanceValid(this) || !IsInsideTree() || Preferencias.GuiaVista("menu")) return;
		if (ChequeoActualizacion.HayActualizacionConocida()) return;
		if (HayVentanaAbiertaEnMenu())
		{
			if (intentosRestantes > 0)
				GetTree().CreateTimer(1.0, false).Timeout += () => IntentarGuiaMenu(intentosRestantes - 1);
			return;
		}

		Rect2? Foco(string ruta) => GuiaPasos.RectDe(GetNodeOrNull(ruta));
		GuiaPasos.Iniciar(this, "menu", new[]
		{
			new GuiaPasos.Paso("VS BOT: juega contra la computadora para practicar, probar tu mazo, " +
				"ganar monedas y subir de nivel.", () => Foco("BottomButtons/BtnVsBot")),
			new GuiaPasos.Paso("ONLINE: enfréntate a otros jugadores en tiempo real. Necesitas " +
				"conexión a internet.", () => Foco("BottomButtons/BtnOnline")),
			new GuiaPasos.Paso("CARTAS: aquí armas tu mazo con 8 tropas y 6 ardides. Es lo que " +
				"llevarás a cada batalla.", () => Foco("IslaContainer/CARTAS")),
			new GuiaPasos.Paso("TIENDA: usa tus monedas para comprar skins, tronos, tropas y " +
				"ardides nuevos.", () => Foco("IslaContainer/TIENDA")),
			new GuiaPasos.Paso("Este es tu huevo. Tócalo para elegir qué skin usar.",
				() => GuiaPasos.RectDeVarios(4f, _reyHuevoNode, _reyHuevoAnimado)),
			new GuiaPasos.Paso("Arriba ves tus monedas, tu nombre y tu nivel. Toca tu nombre para " +
				"ver tu perfil y tus estadísticas.", () => GuiaPasos.RectDeVarios(10f,
					GetNodeOrNull("TopHUD/CoinsPanel"), GetNodeOrNull("TopHUD/UserPanel"),
					GetNodeOrNull("TopHUD/LevelPanel"))),
			new GuiaPasos.Paso("Con la llave canjeas códigos promocionales para conseguir premios.",
				() => Foco("BtnDev")),
			new GuiaPasos.Paso("El trofeo abre el ranking: ahí ves a los jugadores con más trofeos " +
				"ganados en partidas.", () => Foco("BtnTrofeo")),
			new GuiaPasos.Paso("El engranaje abre los AJUSTES: volumen, música, efectos, vibración " +
				"y el tutorial para repasarlo cuando quieras.", () => Foco("BtnSettings")),
		});
	}

	/// <summary>Explica cada opción del panel de Ajustes la primera vez que se abre.</summary>
	private void ProgramarGuiaAjustes()
	{
		if (Preferencias.GuiaVista("ajustes") || _panelSettings == null) return;
		// Un instante después de abrir: el panel recién ahí tiene su tamaño y escala finales.
		GetTree().CreateTimer(0.35, false).Timeout += () =>
		{
			if (!IsInstanceValid(this) || _panelSettings == null || !_panelSettings.Visible) return;

			Rect2? Foco(string ruta) => GuiaPasos.RectDe(_panelSettings.GetNodeOrNull(ruta), 10f);
			var pasos = new System.Collections.Generic.List<GuiaPasos.Paso>
			{
				new("VOLUMEN: desliza la barra para subir o bajar el sonido de todo el juego.",
					() => Foco("Margin/VBox/HBoxVolumen")),
				new("MÚSICA y EFECTOS se prenden y apagan por separado. En color están encendidos; " +
					"en gris, apagados.", () => Foco("Margin/VBox/HBoxAudio")),
				new("VIBRACIÓN DE PANTALLA: activa o quita el temblor de la pantalla en los golpes " +
					"fuertes de la batalla.", () => Foco("Margin/VBox/HBoxVibracion")),
				new("TUTORIAL: vuelve a jugar el tutorial cuando quieras repasar lo básico.",
					() => Foco("Margin/VBox/BtnComoJugar")),
			};
			if (_panelSettings.GetNodeOrNull<Control>("Margin/VBox/BtnCerrarSesion") is Control cerrarSesion
				&& cerrarSesion.IsVisibleInTree())
			{
				pasos.Add(new("CERRAR SESIÓN: sales de tu cuenta en este celular. Tu progreso queda " +
					"guardado en tu cuenta.", () => Foco("Margin/VBox/BtnCerrarSesion")));
			}
			pasos.Add(new("CERRAR te devuelve al menú principal.", () => Foco("Margin/VBox/BtnCerrar")));
			GuiaPasos.Iniciar(this, "ajustes", pasos);
		};
	}
}
