using Godot;

/// <summary>Guía de primera vez del constructor de mazo (ver GuiaPasos): cartas, filtros, buscador,
/// el mazo, la ficha de datos y los botones de ayuda / NFC / volver.</summary>
public partial class MenuConstructor : Control
{
	private void ProgramarGuia()
	{
		if (Preferencias.GuiaVista("mazo")) return;
		GetTree().CreateTimer(0.6, false).Timeout += () =>
		{
			if (!IsInstanceValid(this) || !IsInsideTree()) return;

			Rect2? Foco(string ruta, float margen = 10f) => GuiaPasos.RectDe(GetNodeOrNull(ruta), margen);
			GuiaPasos.Iniciar(this, "mazo", new[]
			{
				new GuiaPasos.Paso("Aquí están todas tus cartas, en TROPAS y ARDID. Toca una para " +
					"sumarla a tu mazo. Las grises aún están bloqueadas.",
					() => Foco("SelectorContainer", 0f)),
				new GuiaPasos.Paso("Filtra por tipo: azul TÁCTICO, rojo ASESINO y amarillo COLOSO. El " +
					"verde quita el filtro.", () => GuiaPasos.RectDeVarios(8f,
						GetNodeOrNull("minis botones mazo tropa/tacticoboton"),
						GetNodeOrNull("minis botones mazo tropa/regresarboton"))),
				new GuiaPasos.Paso("¿Buscas una carta en especial? Escribe su nombre aquí.",
					() => Foco("BuscadorContainer", 4f)),
				new GuiaPasos.Paso("Este es tu mazo: 8 tropas (3 tácticos, 3 asesinos y 2 colosos). " +
					"Toca una carta del mazo para quitarla.", () => Foco("MazoContainer", 0f)),
				new GuiaPasos.Paso("El contador muestra cuántas cartas llevas y LIMPIAR vacía la " +
					"pestaña actual. Tu mazo se guarda solo.", () => GuiaPasos.RectDeVarios(8f,
						GetNodeOrNull("MazoContainer/BarraAccionesMazo/ContadorBox"),
						GetNodeOrNull("MazoContainer/BarraAccionesMazo/BtnBatallar"))),
				new GuiaPasos.Paso("Al tocar una carta ves aquí sus datos: vida, ataque, defensa y su " +
					"habilidad.", () => Foco("InfoContainer", 0f)),
				new GuiaPasos.Paso("¿Dudas? Este botón abre la ayuda con las reglas para armar tu mazo.",
					() => Foco("Boton_Duda", 6f)),
				new GuiaPasos.Paso("Con este botón invocas tus cartas físicas: acerca la carta NFC al " +
					"celular o escribe su código para desbloquearla.", () => Foco("Boton_NFC", 6f)),
				new GuiaPasos.Paso("VOLVER te lleva de nuevo al menú principal.",
					() => Foco("MazoContainer/BarraAccionesMazo/BtnVolver", 8f)),
			});
		};
	}
}
