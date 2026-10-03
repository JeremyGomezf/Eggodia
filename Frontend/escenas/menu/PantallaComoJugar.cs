using Godot;

/// <summary>
/// "Cómo jugar": 5 bloques cortos con lo justo para jugar (objetivo, invocar, el turno, habilidades y
/// los botones de la partida), en UNA columna con letra grande para leerse cómodo en el celular. El
/// contenido se arma por código; de la escena se usan el fondo, la cabecera y la lista desplazable.
/// </summary>
public partial class PantallaComoJugar : Control
{
	// Modo CAPA: cuando la guía se abre DESDE una partida en curso (menú de pausa), no se cambia de
	// escena (eso destruiría la partida y al volver caías al menú). Se muestra encima de la partida
	// pausada y "volver" solo cierra la capa → se regresa a la partida. AlVolver define ese cierre.
	public bool EnModoCapa = false;
	public System.Action AlVolver;

	private const int LETRA_TITULO_PANTALLA = 60;
	private const int LETRA_TITULO_BLOQUE   = 46;
	private const int LETRA_TEXTO_BLOQUE    = 36;
	private const int LETRA_NUMERO          = 64;
	private const float ANCHO_MAX_BLOQUE    = 1500f; // en pantallas muy anchas no se estira de lado a lado

	private static readonly (string titulo, string texto)[] BLOQUES =
	{
		("El objetivo",
		 "Baja a cero la vida del huevo rival antes de que él baje la tuya. Si se acaban los 3 minutos, " +
		 "gana quien tenga más vida."),
		("Invoca tus tropas",
		 "Arrastra una carta de tu mano a uno de los 3 carriles. Invocar es gratis. Al empezar, llena los " +
		 "3 carriles."),
		("Tu turno",
		 "Tienes 3 de energía y 20 segundos. Toca una tropa tuya y elige ATACAR o DEFENDER: cada acción " +
		 "gasta energía."),
		("Habilidades",
		 "Cada tropa tiene una HABILIDAD especial que se desbloquea si sobrevive varios turnos (en CARTAS " +
		 "ves cuántos necesita cada una). Mientras tanto aparece bloqueada: ¡cuida a tus tropas!"),
		("Botones de la partida",
		 "ARDIDES: arrástralos sobre una tropa (curar, veneno…). BARAJAR cambia tu mano una vez por " +
		 "partida. El botón rojo cambia tus ardides gratis. SACRIFICIO retira una tropa tuya para liberar " +
		 "su carril."),
	};

	public override void _Ready()
	{
		EstilizarCabecera();
		ConstruirBloques();

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

	private void EstilizarCabecera()
	{
		var deco = GetNodeOrNull<Label>("Root/Header/HBox/Deco");
		if (deco != null) deco.Visible = false;
		var titulo = GetNodeOrNull<Label>("Root/Header/HBox/Titulo");
		EstiloUI.Titulo(titulo, LETRA_TITULO_PANTALLA);
		// Pista chica al lado del título: la lista sigue hacia abajo y se desliza con el dedo.
		if (titulo != null)
		{
			var pista = new Label { Text = "(arrastra para ver más)", VerticalAlignment = VerticalAlignment.Center };
			EstiloUI.Texto(pista, 26, new Color(0.75f, 0.8f, 0.92f));
			titulo.GetParent().AddChild(pista);
			titulo.GetParent().MoveChild(pista, titulo.GetIndex() + 1);
		}
		if (GetNodeOrNull<Button>("Root/Header/HBox/BtnVolver") is Button volver)
		{
			EstiloUI.Boton(volver, 40);
			volver.CustomMinimumSize = new Vector2(280, 96);
		}
	}

	private void ConstruirBloques()
	{
		var contenido = GetNodeOrNull<VBoxContainer>("Root/Scroll/Margin/Contenido");
		if (contenido == null) return;
		// Fuera el contenido viejo de la escena (intro larga + 12 tarjetas en dos columnas).
		foreach (Node hijo in contenido.GetChildren()) hijo.QueueFree();
		contenido.AddThemeConstantOverride("separation", 24);
		contenido.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		contenido.CustomMinimumSize = new Vector2(Mathf.Min(ANCHO_MAX_BLOQUE, GetViewportRect().Size.X - 120f), 0);

		for (int i = 0; i < BLOQUES.Length; i++)
			contenido.AddChild(CrearBloque(i + 1, BLOQUES[i].titulo, BLOQUES[i].texto));
	}

	private static PanelContainer CrearBloque(int numero, string titulo, string texto)
	{
		var tarjeta = new PanelContainer { MouseFilter = MouseFilterEnum.Pass };
		var sb = new StyleBoxFlat { BgColor = new Color(0.05f, 0.08f, 0.16f, 0.95f), BorderColor = EstiloUI.OroBorde };
		sb.BorderWidthLeft = 8;
		sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 18;
		sb.ContentMarginLeft = 30; sb.ContentMarginRight = 36;
		sb.ContentMarginTop = sb.ContentMarginBottom = 24;
		sb.ShadowColor = new Color(0, 0, 0, 0.4f); sb.ShadowSize = 8;
		tarjeta.AddThemeStyleboxOverride("panel", sb);

		var fila = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
		fila.AddThemeConstantOverride("separation", 28);
		tarjeta.AddChild(fila);

		var lblNumero = new Label
		{
			Text = numero.ToString(),
			CustomMinimumSize = new Vector2(70, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
		};
		EstiloUI.Texto(lblNumero, LETRA_NUMERO, EstiloUI.Dorado);
		fila.AddChild(lblNumero);

		var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass };
		col.AddThemeConstantOverride("separation", 8);
		fila.AddChild(col);

		var lblTitulo = new Label { Text = titulo };
		EstiloUI.Texto(lblTitulo, LETRA_TITULO_BLOQUE, new Color(0.55f, 0.85f, 1f));
		col.AddChild(lblTitulo);

		var lblTexto = new Label { Text = texto, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		EstiloUI.Texto(lblTexto, LETRA_TEXTO_BLOQUE, EstiloUI.TextoClaro);
		col.AddChild(lblTexto);

		return tarjeta;
	}
}
