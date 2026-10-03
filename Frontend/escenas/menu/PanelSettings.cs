using Godot;
using System;

public partial class PanelSettings : PanelContainer
{
	private const string RUTA_COMO_JUGAR = "res://escenas/menu/PantallaComoJugar.tscn";
	// Desde el MENÚ PRINCIPAL este botón ya no abre la guía de texto: lanza el tutorial jugable.
	// Dentro de una partida (Ajustes desde el menú de pausa) sigue siendo "Cómo jugar" como siempre,
	// porque ahí no se puede arrancar otra partida encima.
	private const string RUTA_TUTORIAL_JUGABLE = "res://escenas/gameplay/campo_tutorial.tscn";

	/// <summary>True si este panel de Ajustes es el del menú principal (no el de la pausa en partida).</summary>
	private bool EstoyEnMenuPrincipal() => GetTree()?.CurrentScene is MenuPrincipal;
	private const string RUTA_LOGIN      = "res://escenas/menu/PanelLogin.tscn";

	private HSlider _sliderVolumen;
	private Button _btnMute;
	private Button _btnCerrar;
	private Button _btnComoJugar;
	private Button _btnCerrarSesion;
	private CheckButton _chkScreenShake;

	public static bool ScreenShakeEnabled { get; set; } = true;

	// Se invoca al cerrar el panel (botón CERRAR / Ocultar). Lo usa el menú de pausa para volver a
	// mostrar sus botones (CONTINUAR/OPCIONES/RENDIRSE), que se ocultan mientras se ven los ajustes.
	public System.Action AlCerrar;

	// Escala visual del panel (1 = tamaño normal). El menú principal lo pone un poco más chico; el
	// menú de pausa (VS BOT) lo deja en 1. Se aplica centrado para no descolocar el panel.
	private float _escala = 1f;

	public void FijarEscala(float s)
	{
		_escala = s;
		AplicarEscalaCentrada();
		// Reaplicar tras el primer layout (cuando ya hay Size real), por si se fijó estando oculto.
		Callable.From(AplicarEscalaCentrada).CallDeferred();
	}

	private void AplicarEscalaCentrada()
	{
		if (!IsInsideTree()) return;
		PivotOffset = Size / 2f;                 // pivote al centro (tras el layout) → no se descentra
		Scale = new Vector2(_escala, _escala);
	}

	public override void _Ready()
	{
		// Clic de interfaz en los botones de esta pantalla (se crea aparte de Campo1/MenuPrincipal).
		Callable.From(() => SonidoUI.EngancharBotones(this)).CallDeferred();

		// Reaplicar la escala cuando el panel se dimensiona o se muestra (ahí ya tiene Size real y el
		// pivote queda bien centrado).
		Resized += AplicarEscalaCentrada;
		VisibilityChanged += AplicarEscalaCentrada;

		_sliderVolumen   = GetNodeOrNull<HSlider>("Margin/VBox/HBoxVolumen/SliderVolumen");
		_btnMute         = GetNodeOrNull<Button>("Margin/VBox/BtnMute");
		_btnCerrar       = GetNodeOrNull<Button>("Margin/VBox/BtnCerrar");
		_btnComoJugar    = GetNodeOrNull<Button>("Margin/VBox/BtnComoJugar");
		_btnCerrarSesion = GetNodeOrNull<Button>("Margin/VBox/BtnCerrarSesion");
		_chkScreenShake  = GetNodeOrNull<CheckButton>("Margin/VBox/HBoxVibracion/MarcoSwitch/ChkScreenShake");
		_marcoSwitch     = GetNodeOrNull<Control>("Margin/VBox/HBoxVibracion/MarcoSwitch");

		// Marco DORADO del juego aplicado por CÓDIGO (además del de la escena): garantiza que se vea
		// aunque el editor tenga la .tscn cacheada. padTop alto para que el título quede bajo la gema.
		// Marco propio de CONFIGURACIÓN (login_panel2). El de login_panel.png queda reservado para la
		// escena de login; acá y en el panel de pausa va este otro.
		// padBottom 100: el borde dorado de la imagen ocupa ~92px abajo. Con los 24 de antes, el botón
		// CERRAR se montaba sobre el marco y parecía que la interfaz "no llegaba". Ahora el marco se
		// extiende por debajo del último botón y queda bien encuadrado.
		EstiloUI.MarcoDorado(this, padX: 24, padTop: 118, padBottom: 100, textura: EstiloUI.MarcoAjustes);

		// Estilo del juego (fuente Almendra + botones nuestros) sobre la UI de la escena, que venía con
		// la fuente/estilo por defecto de Godot. Solo apariencia: no cambia la lógica.
		CrearBotonesAudio(); // MÚSICA (izquierda) + EFECTOS (derecha), los dos en amarillo, en lugar de SILENCIAR
		EstiloUI.Boton(_btnComoJugar, 34);
		EstiloUI.Boton(_btnCerrar, 36);
		EstiloUI.Boton(_btnCerrarSesion, 34, rojo: true);
		EstiloUI.Titulo(GetNodeOrNull<Label>("Margin/VBox/Titulo"), 54);
		EstiloUI.Texto(GetNodeOrNull<Label>("Margin/VBox/HBoxVolumen/LabelVolumen"), 34, EstiloUI.TextoClaro);
		// El texto de la vibración ya NO vive dentro del CheckButton: es un Label aparte, con la misma
		// fuente y el mismo tamaño (34) que "Volumen:" y el resto. Así nunca lo afecta la escala que se
		// le aplica al toggle, que era el motivo por el que se veía diminuto.
		EstiloUI.Texto(GetNodeOrNull<Label>("Margin/VBox/HBoxVibracion/LblVibracion"), 34, EstiloUI.TextoClaro);

		if (_btnCerrar != null) _btnCerrar.Pressed += Ocultar;

		// Disponibles sin importar si la sesión es de invitado o de una cuenta real.
		// IMPORTANTE: este panel también se abre desde el menú de PAUSA (VS BOT), donde el árbol está
		// PAUSADO (GetTree().Paused = true). Si se cambia de escena sin despausar, la escena nueva nace
		// congelada y no responde a nada. Y "Cómo jugar" desde una partida NO debe cambiar de escena
		// (destruiría la partida y al volver caías al menú): se muestra como CAPA encima. Ver AbrirComoJugar.
		if (_btnComoJugar != null)
		{
			_btnComoJugar.Pressed += AbrirComoJugar;
			// El texto se decide diferido: al correr _Ready, CurrentScene puede no estar asignada
			// todavía y no se sabría si este panel es el del menú o el de la pausa.
			Callable.From(AjustarTextoBotonComoJugar).CallDeferred();
		}

		if (_btnCerrarSesion != null)
			_btnCerrarSesion.Pressed += () =>
			{
				SesionJuego.Instance?.CerrarSesion();
				GetTree().Paused = false;
				GetTree().ChangeSceneToFile(RUTA_LOGIN);
			};

		var audioManager = GlobalAudioManager.Instance;
		if (audioManager != null)
		{
			if (_sliderVolumen != null)
			{
				_sliderVolumen.Value = audioManager.GetVolumen();
				_sliderVolumen.ValueChanged += (v) => audioManager.CambiarVolumen((float)v);
			}
			if (_btnMusica != null)
				_btnMusica.Pressed += () => { audioManager.SetMusicaMuteada(!audioManager.IsMusicaMuteada()); ActualizarUI(); };
			if (_btnEfectos != null)
				_btnEfectos.Pressed += () => { audioManager.SetEfectosMuteados(!audioManager.IsEfectosMuteados()); ActualizarUI(); };
			ActualizarUI();
		}

		if (_chkScreenShake != null)
		{
			EstilizarSwitch(_chkScreenShake);
			_chkScreenShake.ButtonPressed = ScreenShakeEnabled;
			_chkScreenShake.Toggled += (on) => ScreenShakeEnabled = on;
			// Se reaplica al redimensionarse para tener el tamaño real. Aplica a menú principal y VS BOT.
			_chkScreenShake.Resized += AgrandarSwitchVibracion;
			Callable.From(AgrandarSwitchVibracion).CallDeferred();
		}
	}

	// Abre "Cómo jugar". Si venimos de una partida en curso (árbol pausado, p. ej. VS BOT desde la
	// pausa), la muestra como CAPA encima de la partida SIN destruirla: "volver" cierra la capa y sigues
	// en la partida. Desde el menú principal (no pausado) se comporta como antes: cambia de escena.
	private void AjustarTextoBotonComoJugar()
	{
		if (_btnComoJugar == null || !IsInstanceValid(_btnComoJugar)) return;
		if (EstoyEnMenuPrincipal()) _btnComoJugar.Text = "TUTORIAL";
	}

	private void AbrirComoJugar()
	{
		// Menú principal: arranca el tutorial jugable directamente.
		if (!GetTree().Paused && EstoyEnMenuPrincipal())
		{
			ContextoOnline.Limpiar(); // el tutorial es local: no arrastrar un contexto online previo
			Preferencias.TutorialVisto = true;
			GetTree().ChangeSceneToFile(RUTA_TUTORIAL_JUGABLE);
			return;
		}

		var escena = GD.Load<PackedScene>(RUTA_COMO_JUGAR);

		if (GetTree().Paused && escena != null)
		{
			// Capa que procesa AUNQUE el juego esté pausado (Always), por encima de todo.
			var capa = new CanvasLayer { Layer = 400, ProcessMode = Node.ProcessModeEnum.Always };
			var guia = escena.Instantiate<PantallaComoJugar>();
			guia.EnModoCapa = true;
			guia.AlVolver   = () => { if (Godot.GodotObject.IsInstanceValid(capa)) capa.QueueFree(); };
			capa.AddChild(guia);
			// Se cuelga de la escena actual (la partida): así, si la partida se cierra por lo que sea,
			// la capa se libera con ella y no queda huérfana. Layer alto = por encima de todo.
			Node destino = GetTree().CurrentScene ?? GetTree().Root;
			destino.AddChild(capa);
			return;
		}

		// Menú principal (sin partida): navegación normal.
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile(RUTA_COMO_JUGAR);
	}

	// ── BOTONES DE AUDIO: MÚSICA y EFECTOS por separado ─────────────────────
	// Antes había un solo "SILENCIAR" que apagaba todo (bus Master). Ahora son dos, lado a lado:
	// MÚSICA (izquierda) silencia solo la música de menú/batalla, y EFECTOS (derecha)
	// solo los sonidos del juego (botones, tropas, efectos). Se crean por código en el mismo lugar del
	// botón viejo, que queda oculto.
	private Button _btnMusica;
	private Button _btnEfectos;
	private static readonly Color AMARILLO_AUDIO = new(0.92f, 0.74f, 0.16f);
	private static readonly Color GRIS_APAGADO   = new(0.28f, 0.29f, 0.32f);

	private void CrearBotonesAudio()
	{
		var vbox = GetNodeOrNull<VBoxContainer>("Margin/VBox");
		if (vbox == null) return;

		var fila = new HBoxContainer { Name = "HBoxAudio", CustomMinimumSize = new Vector2(0, 84) };
		fila.AddThemeConstantOverride("separation", 18);
		_btnMusica  = new Button { Name = "BtnMusica",  SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 84) };
		_btnEfectos = new Button { Name = "BtnEfectos", SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 84) };
		fila.AddChild(_btnMusica);   // izquierda
		fila.AddChild(_btnEfectos);  // derecha

		// Mismo lugar que el SILENCIAR de la escena (que se oculta, no se borra: la escena sigue intacta).
		int idx = _btnMute != null ? _btnMute.GetIndex() : vbox.GetChildCount();
		vbox.AddChild(fila);
		vbox.MoveChild(fila, idx);
		if (_btnMute != null) _btnMute.Visible = false;
	}

	private static void PintarBotonAudio(Button b, string nombre, bool encendido, Color color, Color colorTexto)
	{
		if (b == null) return;
		b.Text = $"{nombre}: {(encendido ? "SÍ" : "NO")}";
		Color fondo = encendido ? color : GRIS_APAGADO;
		StyleBoxFlat Caja(Color bg, float borde)
		{
			var sb = new StyleBoxFlat { BgColor = bg, BorderColor = encendido ? color.Lightened(0.35f) : new Color(0.5f, 0.5f, 0.55f) };
			sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = (int)borde;
			sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 12;
			sb.ContentMarginLeft = sb.ContentMarginRight = 16;
			return sb;
		}
		b.AddThemeStyleboxOverride("normal",  Caja(fondo, 2));
		b.AddThemeStyleboxOverride("hover",   Caja(fondo.Lightened(0.12f), 3));
		b.AddThemeStyleboxOverride("pressed", Caja(fondo.Darkened(0.15f), 3));
		b.AddThemeStyleboxOverride("focus",   new StyleBoxEmpty());
		if (EstiloUI.Fuente != null) b.AddThemeFontOverride("font", EstiloUI.Fuente);
		b.AddThemeFontSizeOverride("font_size", 30);
		Color txt = encendido ? colorTexto : new Color(0.75f, 0.75f, 0.78f);
		foreach (var n in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
			b.AddThemeColorOverride(n, txt);
	}

	private void ActualizarUI()
	{
		var am = GlobalAudioManager.Instance;
		if (am == null) return;
		PintarBotonAudio(_btnMusica,  "MÚSICA",  !am.IsMusicaMuteada(),  AMARILLO_AUDIO, new Color(0.18f, 0.12f, 0.02f));
		PintarBotonAudio(_btnEfectos, "EFECTOS", !am.IsEfectosMuteados(), AMARILLO_AUDIO, new Color(0.18f, 0.12f, 0.02f));
	}

	// Oculta el botón "CERRAR SESIÓN". Lo usa el menú de pausa (VS BOT): no tiene sentido cerrar sesión
	// a mitad de una partida. En el menú principal el botón sigue visible.
	// El switch de Godot no se puede agrandar por tema (icon_max_width solo achica), así que la única
	// vía es escalar el nodo. Ahora el CheckButton NO tiene texto —el texto es un Label aparte—, así
	// que se puede escalar fuerte sin que las letras se deformen ni crezcan con él.
	// Vale para el panel del menú principal y para el de la pausa de campo_1: es la MISMA escena.
	// El switch ya se dibuja grande (ver IconoSwitch): no hace falta escalarlo.
	private const float ESCALA_SWITCH_VIBRACION = 1f;
	private Control _marcoSwitch;                     // hueco que le reserva el sitio al toggle escalado
	private Vector2 _tamNaturalSwitch = Vector2.Zero; // tamaño del toggle SIN escalar (se mide una vez)

	// El CheckButton se escala desde su esquina (0,0) DENTRO de un Control que le reserva el hueco ya
	// agrandado. Antes se escalaba estando suelto en el HBox: el contenedor calculaba el layout con el
	// tamaño sin escalar, así que el toggle se montaba sobre el texto y encima quedaba descolocado.
	private void AgrandarSwitchVibracion()
	{
		if (_chkScreenShake == null || _marcoSwitch == null) return;

		// Se mide una sola vez: después de escalar, releer el tamaño devolvería el valor ya inflado y
		// el switch crecería un poco más en cada pasada.
		if (_tamNaturalSwitch == Vector2.Zero)
		{
			Vector2 natural = _chkScreenShake.GetCombinedMinimumSize();
			if (natural == Vector2.Zero) return; // todavía sin layout: se reintenta en el próximo Resized
			_tamNaturalSwitch = natural;
		}

		_marcoSwitch.CustomMinimumSize = _tamNaturalSwitch * ESCALA_SWITCH_VIBRACION;
		_chkScreenShake.Position       = Vector2.Zero;
		_chkScreenShake.PivotOffset    = Vector2.Zero; // crece hacia la derecha/abajo, sin descolocarse
		_chkScreenShake.Scale          = new Vector2(ESCALA_SWITCH_VIBRACION, ESCALA_SWITCH_VIBRACION);
	}

	// ── SWITCH PROPIO ─────────────────────────────────────────────────────
	// El de Godot por defecto era chico y, al tocarlo, se oscurecía su fondo y quedaba solo un círculo
	// negro sobre negro. Este es una pista (amarilla = prendido, gris = apagado) con una perilla BLANCA
	// grande, igual de grande en los dos estados, y sin ningún fondo al tocarlo.
	private const int ANCHO_SWITCH = 132, ALTO_SWITCH = 72;
	private static ImageTexture _switchOn, _switchOff;

	private static void EstilizarSwitch(CheckButton chk)
	{
		_switchOn  ??= IconoSwitch(true);
		_switchOff ??= IconoSwitch(false);
		foreach (var n in new[] { "checked", "checked_mirrored", "checked_disabled", "checked_disabled_mirrored" })
			chk.AddThemeIconOverride(n, _switchOn);
		foreach (var n in new[] { "unchecked", "unchecked_mirrored", "unchecked_disabled", "unchecked_disabled_mirrored" })
			chk.AddThemeIconOverride(n, _switchOff);
		foreach (var n in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
			chk.AddThemeStyleboxOverride(n, new StyleBoxEmpty());
		foreach (var n in new[] { "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color", "icon_focus_color" })
			chk.AddThemeColorOverride(n, Colors.White); // el ícono nunca se oscurece al tocarlo
	}

	private static ImageTexture IconoSwitch(bool prendido)
	{
		const int W = ANCHO_SWITCH, H = ALTO_SWITCH;
		float cy = H / 2f;
		float radioPerilla = H / 2f - 3f;           // la perilla ocupa casi todo el alto
		float radioPista   = radioPerilla * 0.72f;  // pista más fina que la perilla
		float x0 = radioPerilla + 3f, x1 = W - radioPerilla - 3f;
		float cxPerilla = prendido ? x1 : x0;
		Color pista = prendido ? AMARILLO_AUDIO : new Color(0.38f, 0.40f, 0.46f);
		Color borde = new Color(0.10f, 0.12f, 0.18f);

		var img = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
		for (int y = 0; y < H; y++)
		for (int x = 0; x < W; x++)
		{
			var p = new Vector2(x + 0.5f, y + 0.5f);
			float dPista = p.DistanceTo(new Vector2(Mathf.Clamp(p.X, x0, x1), cy)) - radioPista;
			float dPerilla = p.DistanceTo(new Vector2(cxPerilla, cy)) - radioPerilla;
			Color c = new Color(pista, Mathf.Clamp(0.5f - dPista, 0f, 1f));
			// Perilla blanca con un aro fino oscuro para que se despegue de la pista.
			float aPerilla = Mathf.Clamp(0.5f - dPerilla, 0f, 1f);
			Color perilla = Colors.White.Lerp(borde, Mathf.Clamp(dPerilla + 3.5f, 0f, 1f));
			c = c.Blend(new Color(perilla, aPerilla));
			img.SetPixel(x, y, c);
		}
		return ImageTexture.CreateFromImage(img);
	}

	public void OcultarCerrarSesion()
	{
		if (_btnCerrarSesion == null)
			_btnCerrarSesion = GetNodeOrNull<Button>("Margin/VBox/BtnCerrarSesion");
		if (_btnCerrarSesion != null) _btnCerrarSesion.Visible = false;
	}

	public void Mostrar()
	{
		Visible = true;
	}

	public void Ocultar()
	{
		Visible = false;
		AlCerrar?.Invoke();
	}
}
