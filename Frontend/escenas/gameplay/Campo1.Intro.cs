using Godot;
using System;
using System.Threading.Tasks;

public partial class Campo1 : Node2D
{
	// ══════════════════════════════════════════════════════════════════════
	// INTRO CINEMÁTICA DE BATALLA (VS BOT y online)
	//  · Arranca con la cámara YA en primer plano sobre MI trono, sin barras todavía: se ve el
	//    trono VACÍO un instante.
	//  · Mi huevo cae desde arriba (fuera de cámara) y aterriza con un rebote tipo "boing". Recién
	//    AHÍ entran las barras de cine (arriba fina, abajo gruesa) y mi nombre, en un cartel centrado
	//    dentro de la barra gruesa de abajo.
	//  · La cámara viaja (con las barras puestas) hasta el trono del rival, también vacío; su huevo
	//    cae igual, con su propio rebote, y aparece SU nombre en el MISMO cartel de abajo.
	//  · Barras afuera, la cámara vuelve a la vista completa del campo — recién ahí aparece TODO lo
	//    demás (energía, mano, círculos de invocación, nombres del HUD).
	//  · Cuenta regresiva 3, 2, 1 con pitido en cada número, y la frase final ("¡¡INVOCA YA!!" o
	//    "¡¡EL RIVAL INVOCA PRIMERO!!") con un sonido de arranque de 1.5s.
	//
	// OJO CÁMARA: Camera2D en campo_1.tscn tiene rotation≈180° + scale=(0.83,0.735) no uniforme —
	// un truco ya existente que el juego necesita para verse bien y que NO se toca. Esta combinación
	// (rotación + escala propia del nodo) hace que el comportamiento del zoom NO sea el habitual de
	// Godot. Estos dos valores NO salieron de la documentación ni de prueba y error a ciegas: los
	// medí de verdad, renderizando esta MISMA cámara (con sus mismos valores de posición/rotation/
	// scale) fuera de la partida y comparando en píxeles reales cuánto ocupaba en pantalla un
	// personaje del tamaño real del huevo (ReyHuevo.png a su escala real, ~81x132 unidades):
	//    zoom base (x1)  -> el personaje ocupa   20% de la altura de pantalla (esto es "se ve lejos")
	//    zoom x3         -> el personaje ocupa   59% de la altura de pantalla
	//    zoom x4.5       -> el personaje ocupa   89% de la altura de pantalla
	//    zoom x6 u x8    -> el personaje ya no entra completo (se corta)
	// Con ESTA cámara en particular, multiplicar el zoom por algo MAYOR a 1 ACERCA (al revés de lo
	// habitual en Godot); por eso ZOOM_INTRO es 3.5, no un valor menor a 1.
	private const float ZOOM_INTRO               = 3.5f;  // >1 ACERCA con esta cámara en particular
	                                                        // (medido). Subilo para acercar más, bajalo
	                                                        // (nunca por debajo de 1) para alejar.
	// Mismo método: medí que, a este zoom, mover el punto de cámara HACIA ARRIBA en el mundo (Y
	// MENOR) es lo que hace aparecer al personaje más abajo en la pantalla (con esta cámara, NO se
	// invierte por la rotación de 180°, se comporta normal). Por eso PuntoFoco RESTA este valor.
	// El vacío que se veía en los primeros intentos NO era "falta empujar más": era la cámara
	// mostrando MÁS ALLÁ del borde del fondo (FONDO/ESCENARIO miden 2304x1296px a escala 0.7, o sea
	// 1612.8x907.2 unidades de mundo, centrados en (806.4,453.6) — el borde real del mapa va de
	// (0,0) a (1612.8,907.2), medido directo del .tscn). En vez de adivinar un desplazamiento, PuntoFoco
	// directamente NO DEJA que la cámara se pase de ese borde al zoom de la intro — así nunca puede
	// volver a aparecer vacío, sea cual sea el trono o el mapa que se sorteó esa partida.
	private static readonly Vector2 FONDO_MUNDO_MIN = Vector2.Zero;
	private static readonly Vector2 FONDO_MUNDO_MAX = new Vector2(1612.8f, 907.2f);
	// Preferencia de encuadre DENTRO de lo permitido: el concepto pide el huevo MÁS ARRIBA en el
	// cuadro (más aire de fondo abajo, antes del cartel), así que se SUMA en vez de restar. Si no
	// entra sin mostrar vacío, se recorta solo (ver el Clamp de abajo).
	private const float OFFSET_VERTICAL_PREFERIDO = -22f;

	// Barras ASIMÉTRICAS (pedido): la de ARRIBA fina, la de ABAJO gruesa — ahí adentro, centrado,
	// va el cartel con el nombre (mío o del rival, el mismo cartel para los dos, solo cambia el
	// texto según de quién sea el turno de presentación).
	private const float ALTO_BARRA_ARRIBA = 0.13f;
	private const float ALTO_BARRA_ABAJO  = 0.30f;
	private const float SEG_BARRAS           = 0.4f;
	private const float SEG_ESPERA_TRONO_VACIO = 0.35f; // se ve el trono vacío antes de que caiga el huevo
	private const float SEG_HOLD_NOMBRE      = 1.5f;    // cuánto se queda cada nombre en pantalla (pedido: 1.5s)
	private const float SEG_VIAJE_AL_RIVAL   = 1.1f;
	private const float SEG_VOLVER           = 0.85f;
	private const float SEG_POR_NUMERO       = 1.05f;   // más lento que antes (pedido explícito)
	private const float SEG_FRASE            = 1.5f;

	// ── HUEVO CAYENDO DEL CIELO ────────────────────────────────────────────
	private const float ALTURA_CAIDA_HUEVO = 750f;  // px por encima de su posición final (fuera de cámara)
	private const float SEG_CAIDA_HUEVO    = 0.5f;
	private const float SEG_BOING_IMPACTO  = 0.08f; // aplastado rápido al tocar el piso
	private const float SEG_BOING_REBOTE   = 0.28f; // vuelve a su forma normal
	private const float BOING_ESCALA_X     = 1.025f; // qué tan "ancho" se pone al aplastarse — mínimo, "poquito nomas"
	private const float BOING_ESCALA_Y     = 0.975f; // qué tan "chato" se pone al aplastarse — mínimo, "poquito nomas".
	                                                  // Más cerca de 1.0 = rebote más suave/ligero (pedido).

	/// <summary>True mientras corre la intro: nadie puede jugar ni corre el reloj.</summary>
	public bool IntroEnCurso { get; private set; } = false;

	private CanvasLayer _capaIntro;
	private ColorRect   _barraArriba, _barraAbajo;
	private Label       _lblNombreIntro, _lblCuentaIntro;
	private Tween       _tweenCuentaIntro; // se mata antes de cada número: si no, dos tweens compiten
	                                        // por el mismo modulate y el texto no llega a mostrarse

	// ── ARRANQUE ──────────────────────────────────────────────────────────
	private async void IniciarIntroCinematica()
	{
		if (IntroEnCurso || juegoTerminado) return;
		IntroEnCurso = true;

		var camara = GetNodeOrNull<Camera2D>("Camera2D");
		Vector2 posOriginal  = camara?.GlobalPosition ?? Vector2.Zero;
		Vector2 zoomOriginal = camara?.Zoom ?? Vector2.One;
		var capaHud = CapaHUD();
		bool hudVisiblePrevio = capaHud.Visible;

		// TODO el armado (ocultar HUD, preparar huevos, crear la capa) va DENTRO del try: antes
		// corría afuera, y si algo tiraba una excepción acá (p. ej. un trono nulo) la cámara nunca
		// llegaba a moverse — la partida arrancaba con el zoom normal de todo el tablero en vez del
		// primer plano, y encima quedaba trabada en "intro en curso" para siempre. Con todo adentro,
		// cualquier error se atrapa y el finally deja el juego jugable igual.
		try
		{
			capaHud.Visible = false; // nada del HUD (energía, mano, nombres, avisos) hasta el final
			MostrarCirculosInvocacion(false);

			// Los dos huevos arrancan YA fuera de cámara (arriba): ningún trono se ve "ocupado" hasta
			// que a cada uno le toca caer. Se hace ANTES de mover la cámara para que no se alcance a
			// ver ni un frame del huevo en su posición final.
			PrepararHuevoParaCaida(tronoJugador);
			PrepararHuevoParaCaida(tronoRival);

			CrearCapaIntro();

			// 1) Cámara YA en primer plano sobre MI trono, sin barras: se ve vacío un instante.
			Vector2 zoomIntro = zoomOriginal * ZOOM_INTRO;
			EnfocarCamaraInstantaneo(camara, PuntoFoco(tronoJugador, zoomIntro), zoomIntro);
			if (!await EsperarIntro(SEG_ESPERA_TRONO_VACIO)) return;

			// 2) Mi huevo cae y rebota.
			await CaerHuevoConBoing(tronoJugador);
			if (!IsInstanceValid(this) || juegoTerminado) return;

			// 3) Recién ahora entran las barras; el nombre espera a que TERMINEN de crecer antes de
			//    aparecer (antes salían juntos y, como el cartel se ubica ya en su lugar final
			//    mientras la barra todavía está creciendo, se veía "la letra antes que la barra").
			MostrarBarrasCine(true);
			if (!await EsperarIntro(SEG_BARRAS)) return;
			string miNombre = SesionJuego.Instance?.NombreJugador ?? "Jugador";
			MostrarNombreIntro(miNombre, tronoJugador, true);
			if (!await EsperarIntro(SEG_HOLD_NOMBRE)) return;
			MostrarNombreIntro(miNombre, tronoJugador, false);

			// 4) Viaje (con las barras puestas) hasta el trono del rival, también vacío.
			EnfocarCamara(camara, PuntoFoco(tronoRival, zoomIntro), zoomIntro, SEG_VIAJE_AL_RIVAL);
			if (!await EsperarIntro(SEG_VIAJE_AL_RIVAL + SEG_ESPERA_TRONO_VACIO)) return;

			// 5) Huevo del rival cae y rebota; aparece su nombre (abajo-derecha).
			await CaerHuevoConBoing(tronoRival);
			if (!IsInstanceValid(this) || juegoTerminado) return;
			string nombreRival = ContextoOnline.Activo ? ContextoOnline.RivalNombre : _nombreCPUElegido;
			MostrarNombreIntro(nombreRival, tronoRival, true);
			if (!await EsperarIntro(SEG_HOLD_NOMBRE)) return;
			MostrarNombreIntro(nombreRival, tronoRival, false);

			// 6) Barras afuera, cámara de vuelta a la vista completa del campo.
			MostrarBarrasCine(false);
			EnfocarCamara(camara, posOriginal, zoomOriginal, SEG_VOLVER);
			if (!await EsperarIntro(SEG_VOLVER)) return;

			// 7) Recién ahora aparece TODO lo demás: energía, mano, círculos, nombres del HUD.
			capaHud.Visible = hudVisiblePrevio;
			MostrarCirculosInvocacion(true);
			ActualizarInterfaz();

			// 8) Cuenta regresiva 3, 2, 1 con su pitido.
			for (int n = 3; n >= 1; n--)
			{
				MostrarNumeroIntro(n.ToString(), new Color(1f, 0.95f, 0.4f));
				ReproducirPitidoIntro();
				if (!await EsperarIntro(SEG_POR_NUMERO)) return;
			}

			// 9) Frase final + sonido de arranque (1.5s).
			bool empiezoYo = !ContextoOnline.Activo || esTurnoJugador;
			MostrarNumeroIntro(empiezoYo ? "¡¡INVOCA YA!!" : "¡¡EL RIVAL INVOCA PRIMERO!!",
				empiezoYo ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.6f, 0.45f));
			ReproducirSonidoInicio();
			if (!await EsperarIntro(SEG_FRASE)) return;
		}
		finally
		{
			if (IsInstanceValid(this)) TerminarIntro(camara, posOriginal, zoomOriginal, hudVisiblePrevio);
		}
	}

	private void TerminarIntro(Camera2D camara, Vector2 posOriginal, Vector2 zoomOriginal, bool hudVisiblePrevio)
	{
		if (camara != null && IsInstanceValid(camara)) { camara.GlobalPosition = posOriginal; camara.Zoom = zoomOriginal; }
		if (_capaIntro != null && IsInstanceValid(_capaIntro)) _capaIntro.QueueFree();
		_capaIntro = null;
		var capaHud = CapaHUD();
		if (capaHud != null && IsInstanceValid(capaHud)) capaHud.Visible = hudVisiblePrevio;
		MostrarCirculosInvocacion(true);
		IntroEnCurso = false;
		ActualizarInterfaz();
	}

	private async Task<bool> EsperarIntro(double segundos)
	{
		if (!IsInstanceValid(this) || !IsInsideTree()) return false;
		await ToSignal(GetTree().CreateTimer(segundos, false), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && IsInsideTree() && !juegoTerminado;
	}

	// ── CÍRCULOS DE INVOCACIÓN ─────────────────────────────────────────────
	// El resto del HUD (energía, mano, avisos, nombres) vive todo dentro de CapaHUD y se oculta/
	// muestra con un solo toggle; los círculos de invocación son Marker2D del mundo (no son parte
	// de esa capa), así que se ocultan aparte.
	private void MostrarCirculosInvocacion(bool visible)
	{
		foreach (string grupo in new[] { "zonas_invocacion", "zonas_invocacion_rival" })
			foreach (Node2D zona in GetTree().GetNodesInGroup(grupo))
				if (zona.GetNodeOrNull<Control>("IndicadorMejorado") is Control panel && IsInstanceValid(panel))
					panel.Visible = visible && zona.GetNodeOrNull("Ocupado") == null;
	}

	// ── HUEVO: CAÍDA DESDE EL CIELO + "BOING" AL ATERRIZAR ─────────────────
	// Deja el huevo YA arriba, fuera de cámara, sin animación (para que el trono se vea vacío desde
	// el primer frame, sin un "pop" del huevo apareciendo de la nada).
	private void PrepararHuevoParaCaida(TronoCampo trono)
	{
		Node2D huevo = trono?.Huevo;
		if (huevo == null || !IsInstanceValid(huevo)) return;
		huevo.Position = new Vector2(huevo.Position.X, -ALTURA_CAIDA_HUEVO);
	}

	private async Task CaerHuevoConBoing(TronoCampo trono)
	{
		Node2D huevo = trono?.Huevo;
		if (huevo == null || !IsInstanceValid(huevo)) return;
		Vector2 escalaBase = huevo.Scale; // ya viene con FlipH (X negativo) si es el huevo rival

		Tween caida = huevo.CreateTween();
		caida.TweenProperty(huevo, "position:y", 0f, SEG_CAIDA_HUEVO)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In); // acelera como si cayera
		await ToSignal(caida, Tween.SignalName.Finished);
		if (!IsInstanceValid(huevo)) return;

		// "Boing" mínimo (pedido: "que rebote poquito nomás", ya no como pelota): un solo aplastón
		// chiquito al tocar el piso y vuelta suave a su forma normal — sin rebotes extra.
		Tween boing = huevo.CreateTween();
		boing.TweenProperty(huevo, "scale", new Vector2(escalaBase.X * BOING_ESCALA_X, escalaBase.Y * BOING_ESCALA_Y), SEG_BOING_IMPACTO)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out); // aplastón chiquito (el golpe)
		boing.TweenProperty(huevo, "scale", escalaBase, SEG_BOING_REBOTE)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut); // vuelve suave, sin pasarse
		await ToSignal(boing, Tween.SignalName.Finished);
		if (IsInstanceValid(huevo)) huevo.Scale = escalaBase; // el Elastic puede pasarse un pelo: fija el valor final exacto
	}

	// ── CÁMARA ────────────────────────────────────────────────────────────
	// Punto de cámara sobre un trono: el Marker2D "PosicionHuevo" (no el origen del StaticBody2D,
	// que no está centrado en el personaje) + el ajuste vertical y horizontal de la nota de arriba.
	//
	// El desplazamiento horizontal se ESPEJA según el trono: mi trono (SpawnTrono1, X≈190) y el del
	// rival (SpawnTrono2, X≈1426) están a la MISMA distancia pero a lados OPUESTOS del centro de la
	// cámara (X≈806) — el mío queda pegado al borde IZQUIERDO del fondo, el del rival al borde
	// DERECHO. Por eso, si a mí me hace falta correr el encuadre hacia la derecha, al rival le hace
	// falta exactamente lo contrario (hacia la izquierda) — aplicar el mismo lado a los dos tapaba
	// el vacío de un trono pero lo empeoraba en el otro.
	private Vector2 PuntoFoco(TronoCampo trono, Vector2 zoomIntro)
	{
		if (trono == null) return Vector2.Zero;
		Vector2 baseFoco = trono.GetNodeOrNull<Marker2D>("PosicionHuevo")?.GlobalPosition ?? trono.GlobalPosition;

		// Cuánto mundo entra a cada lado del centro con ESTE zoom (medido: para esta cámara en
		// particular, más zoom = más cerca, así que acá se MULTIPLICA por el zoom, no se divide).
		Rect2 vis = GetViewport().GetVisibleRect();
		Vector2 mitadMundoVisible = new Vector2(
			(vis.Size.X / 2f) / zoomIntro.X,
			(vis.Size.Y / 2f) / zoomIntro.Y);

		Vector2 deseado = baseFoco - new Vector2(0, OFFSET_VERTICAL_PREFERIDO); // preferencia: un poco más abajo
		Vector2 minCentro = FONDO_MUNDO_MIN + mitadMundoVisible;
		Vector2 maxCentro = FONDO_MUNDO_MAX - mitadMundoVisible;
		// Si el mapa fuera más chico que la ventana visible (no debería pasar), Clamp con min>max
		// tiraría el mayor de los dos — se usa el centro real del fondo como respaldo en ese caso.
		Vector2 centroFondo = (FONDO_MUNDO_MIN + FONDO_MUNDO_MAX) / 2f;
		float x = minCentro.X <= maxCentro.X ? Mathf.Clamp(deseado.X, minCentro.X, maxCentro.X) : centroFondo.X;
		float y = minCentro.Y <= maxCentro.Y ? Mathf.Clamp(deseado.Y, minCentro.Y, maxCentro.Y) : centroFondo.Y;
		return new Vector2(x, y);
	}


	private void EnfocarCamaraInstantaneo(Camera2D camara, Vector2 destino, Vector2 zoom)
	{
		if (camara == null || !IsInstanceValid(camara)) return;
		camara.GlobalPosition = destino;
		camara.Zoom = zoom;
	}

	private void EnfocarCamara(Camera2D camara, Vector2 destino, Vector2 zoom, float duracion)
	{
		if (camara == null || !IsInstanceValid(camara)) return;
		Tween tw = camara.CreateTween().SetParallel(true);
		tw.TweenProperty(camara, "global_position", destino, duracion)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tw.TweenProperty(camara, "zoom", zoom, duracion)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	// ── CAPA DE LA INTRO (barras, cartel de nombre y textos) ──────────────
	private void CrearCapaIntro()
	{
		_capaIntro = new CanvasLayer { Name = "CapaIntro", Layer = 90 }; // sobre el HUD (vuelve a capa 1), bajo la pausa (100)
		AddChild(_capaIntro);
		Rect2 vis = GetViewport().GetVisibleRect();

		_barraArriba = CrearBarraCine(new Vector2(vis.Position.X, vis.Position.Y));
		_barraAbajo  = CrearBarraCine(new Vector2(vis.Position.X, vis.End.Y)); // arranca pegada al borde, alto=0

		// UN solo cartel, centrado dentro de la barra de abajo — se reutiliza para mi nombre primero y
		// para el del rival después (solo cambia el texto), tal cual el concepto.
		_lblNombreIntro = CrearEtiquetaNombre();

		// Cuenta regresiva / frase: bien grande (más que Victoria/Derrota), centrada.
		_lblCuentaIntro = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment   = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = new Color(1, 1, 1, 0),
		};
		var fuente = GD.Load<Font>("res://Almendra-Bold.ttf");
		if (fuente != null) _lblCuentaIntro.AddThemeFontOverride("font", fuente);
		_lblCuentaIntro.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.02f, 0f));
		_lblCuentaIntro.AddThemeConstantOverride("outline_size", 24);
		_capaIntro.AddChild(_lblCuentaIntro);
		_lblCuentaIntro.Size     = new Vector2(vis.Size.X * 0.94f, 260f);
		_lblCuentaIntro.Position = new Vector2(vis.Position.X + vis.Size.X * 0.03f, vis.GetCenter().Y - 130f);
	}

	private ColorRect CrearBarraCine(Vector2 posicionBorde)
	{
		var barra = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
		_capaIntro.AddChild(barra);
		Rect2 vis = GetViewport().GetVisibleRect();
		barra.Size = new Vector2(vis.Size.X, 0f); // arranca con alto 0: MostrarBarrasCine la hace crecer
		barra.Position = posicionBorde;
		return barra;
	}

	// Cajita blanca con el nombre en oscuro (arriba-izquierda para mí, abajo-derecha para el rival),
	// igual que las referencias. Arranca invisible y sin texto; se ubica recién al mostrarse porque
	// necesita conocer el ancho real del nombre (varía según el jugador).
	// Mismo formato de letra que el label de turno ("turno o avisos": Almendra-Bold, blanco, con
	// borde — ver campo_1.tscn) — sin caja/fondo blanco detrás, solo el texto con su borde.
	private Label CrearEtiquetaNombre()
	{
		var lbl = new Label
		{
			Text = "", MouseFilter = Control.MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment   = VerticalAlignment.Center,
			Modulate = new Color(1, 1, 1, 0),
		};
		var fuente = GD.Load<Font>("res://Almendra-Bold.ttf");
		if (fuente != null) lbl.AddThemeFontOverride("font", fuente);
		lbl.AddThemeFontSizeOverride("font_size", 145);
		lbl.AddThemeColorOverride("font_color", Colors.White);
		lbl.AddThemeColorOverride("font_outline_color", Colors.Black);
		lbl.AddThemeConstantOverride("outline_size", 22);
		_capaIntro.AddChild(lbl);
		return lbl;
	}

	private void MostrarBarrasCine(bool mostrar)
	{
		if (_barraArriba == null || !IsInstanceValid(_barraArriba)) return;
		Rect2 vis = GetViewport().GetVisibleRect();
		// Asimétricas: arriba fina, abajo gruesa (ahí va el cartel del nombre).
		float altoArriba = mostrar ? vis.Size.Y * ALTO_BARRA_ARRIBA : 0f;
		float altoAbajo  = mostrar ? vis.Size.Y * ALTO_BARRA_ABAJO  : 0f;

		Tween twA = _barraArriba.CreateTween();
		twA.TweenProperty(_barraArriba, "size:y", altoArriba, SEG_BARRAS).SetTrans(Tween.TransitionType.Sine);

		if (_barraAbajo == null || !IsInstanceValid(_barraAbajo)) return;
		Tween twB = _barraAbajo.CreateTween().SetParallel(true);
		twB.TweenProperty(_barraAbajo, "size:y", altoAbajo, SEG_BARRAS).SetTrans(Tween.TransitionType.Sine);
		twB.TweenProperty(_barraAbajo, "position:y", vis.End.Y - altoAbajo, SEG_BARRAS).SetTrans(Tween.TransitionType.Sine);
	}

	// Un solo cartel, SIEMPRE reutilizado — se centra bajo la posición REAL en pantalla del huevo
	// que corresponda (no bajo el centro de la ventana: con el zoom/offset de la intro el huevo no
	// cae siempre exacto en el centro, así que se proyecta su posición real a través de la cámara).
	private void MostrarNombreIntro(string nombre, TronoCampo trono, bool mostrar)
	{
		if (_lblNombreIntro == null || !IsInstanceValid(_lblNombreIntro)) return;

		if (mostrar)
		{
			_lblNombreIntro.Text = nombre;
			// Necesita un frame para que el Label recalcule su ancho con el nombre nuevo antes de poder
			// centrarlo — si no, usaría el ancho de la vez anterior.
			Callable.From(() => UbicarEtiquetaNombre(trono)).CallDeferred();
		}

		Tween tw = _lblNombreIntro.CreateTween();
		tw.TweenProperty(_lblNombreIntro, "modulate:a", mostrar ? 1f : 0f, 0.3f);
	}

	private void UbicarEtiquetaNombre(TronoCampo trono)
	{
		if (!IsInstanceValid(_lblNombreIntro)) return;
		Rect2 vis = GetViewport().GetVisibleRect();
		float altoAbajo = vis.Size.Y * ALTO_BARRA_ABAJO;

		// Centro horizontal = la posición del huevo, proyectada de verdad a través de la cámara actual
		// (posición + zoom + rotación ya incluidos) — así el cartel queda pegado a SU huevo en los dos
		// lados, en vez de asumir que el huevo cae siempre en el medio de la pantalla.
		Vector2 puntoHuevo = trono?.GetNodeOrNull<Marker2D>("PosicionHuevo")?.GlobalPosition ?? trono?.GlobalPosition ?? vis.GetCenter();
		float centroX = (GetViewport().GetCanvasTransform() * puntoHuevo).X;

		// Medido en tu referencia (no a ojo): la caja empieza al 23% de la barra de abajo, pegada
		// arriba de la barra pero con un margen chico — no centrada en el medio de todo ese espacio.
		const float INICIO_EN_BARRA = 0.234f;
		_lblNombreIntro.Size = _lblNombreIntro.GetMinimumSize();
		_lblNombreIntro.Position = new Vector2(
			centroX - _lblNombreIntro.Size.X / 2f,
			(vis.End.Y - altoAbajo) + altoAbajo * INICIO_EN_BARRA);
	}

	// Números "3", "2", "1" y la frase final. Mata el tween anterior antes de crear uno nuevo: dos
	// tweens sueltos escribiendo el mismo "modulate:a" competían entre sí y el número casi no
	// llegaba a mostrarse (el de "3" seguía apagando el alfa mientras ya estaba puesto el "2").
	private void MostrarNumeroIntro(string texto, Color color)
	{
		if (_lblCuentaIntro == null || !IsInstanceValid(_lblCuentaIntro)) return;
		_tweenCuentaIntro?.Kill();

		_lblCuentaIntro.Text = texto;
		_lblCuentaIntro.AddThemeColorOverride("font_color", color);
		// Los números salen enormes; la frase entra un poco más chica para no desbordar el ancho.
		_lblCuentaIntro.AddThemeFontSizeOverride("font_size", texto.Length <= 2 ? 240 : 108);
		_lblCuentaIntro.PivotOffset = _lblCuentaIntro.Size / 2f;
		_lblCuentaIntro.Scale = new Vector2(1.5f, 1.5f);
		_lblCuentaIntro.Modulate = new Color(1, 1, 1, 1);

		_tweenCuentaIntro = _lblCuentaIntro.CreateTween();
		_tweenCuentaIntro.TweenProperty(_lblCuentaIntro, "scale", Vector2.One, 0.3f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_tweenCuentaIntro.Parallel().TweenProperty(_lblCuentaIntro, "modulate:a", 1f, 0.01f); // asegura visible ya
		_tweenCuentaIntro.Chain().TweenInterval(SEG_POR_NUMERO * 0.35f);
		_tweenCuentaIntro.Chain().TweenProperty(_lblCuentaIntro, "modulate:a", 0f, SEG_POR_NUMERO * 0.35f);
	}

	// ── SONIDOS ─────────────────────────────────────────────────────────────
	// Los dos se sintetizan por código (igual que el clic de los botones), pero si algún día se pone
	// un archivo .wav/.ogg/.mp3 con el mismo nombre en estas rutas, se usa ese en su lugar sin tocar
	// código. El graznido de gallina (CC-BY, descargado de internet) no gustó — se sacó del todo, ni
	// siquiera queda como archivo en el proyecto.
	private const string RUTA_SFX_PITIDO  = "res://efectos/sonidos/countdown_beep";
	private const string RUTA_SFX_INICIO  = "res://efectos/sonidos/partida_inicio";
	private static AudioStream _sfxPitido, _sfxInicio;

	private void ReproducirPitidoIntro()
	{
		_sfxPitido ??= CargarOGenerar(RUTA_SFX_PITIDO, GenerarPitido);
		ReproducirSfxIntro(_sfxPitido, -6f);
	}

	// Suena junto con la frase final ("¡¡INVOCA YA!!" / "¡¡EL RIVAL INVOCA PRIMERO!!"): un sonido
	// de "arrancó la partida", corto (1.5s), en vez del graznido.
	private void ReproducirSonidoInicio()
	{
		_sfxInicio ??= CargarOGenerar(RUTA_SFX_INICIO, GenerarSonidoInicio);
		ReproducirSfxIntro(_sfxInicio, -4f);
	}

	private void ReproducirSfxIntro(AudioStream stream, float volumenDb)
	{
		if (stream == null) return;
		var player = new AudioStreamPlayer { Stream = stream, VolumeDb = volumenDb };
		AddChild(player);
		player.Finished += () => { if (IsInstanceValid(player)) player.QueueFree(); };
		player.Play();
	}

	private static AudioStream CargarOGenerar(string rutaSinExtension, Func<AudioStreamWav> generar)
	{
		foreach (string ext in new[] { ".wav", ".ogg", ".mp3" })
			if (ResourceLoader.Exists(rutaSinExtension + ext))
				return GD.Load<AudioStream>(rutaSinExtension + ext);
		return generar();
	}

	/// <summary>Pitido corto y seco de cuenta regresiva (0.18s, 880 Hz). Sin archivo equivalente en
	/// el proyecto — se genera siempre por código.</summary>
	private static AudioStreamWav GenerarPitido()
	{
		const int MUESTREO = 22050;
		int total = (int)(MUESTREO * 0.18f);
		var datos = new byte[total * 2];
		for (int i = 0; i < total; i++)
		{
			float t = (float)i / MUESTREO;
			float envolvente = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-t * 14f);
			float onda = Mathf.Sin(Mathf.Tau * 880f * t) * 0.7f + Mathf.Sin(Mathf.Tau * 1760f * t) * 0.3f;
			EscribirMuestra(datos, i, onda * envolvente * 0.85f);
		}
		return ArmarWav(datos, MUESTREO);
	}

	/// <summary>Sonido de "arrancó la partida": 3 campanadas ascendentes tipo fanfarria (Do-Mi-Sol,
	/// ~523/659/784 Hz), cada una entrando encima de la anterior, total 1.5s. Brillante y corto, sin
	/// depender de ningún archivo descargado.</summary>
	private static AudioStreamWav GenerarSonidoInicio()
	{
		const int MUESTREO = 22050;
		const float DURACION_TOTAL = 1.5f;
		int total = (int)(MUESTREO * DURACION_TOTAL);
		var datos = new byte[total * 2];

		// Cada nota entra un poco después de la anterior (arpegio) y se apaga sola con su propia cola.
		float[] frecuencias  = { 523.25f, 659.25f, 783.99f }; // Do5, Mi5, Sol5
		float[] inicioSeg    = { 0.00f,   0.16f,   0.32f };
		float[] duracionSeg  = { 1.10f,   1.05f,   1.00f };

		for (int i = 0; i < total; i++)
		{
			float t = (float)i / MUESTREO;
			float muestra = 0f;
			for (int n = 0; n < frecuencias.Length; n++)
			{
				float tNota = t - inicioSeg[n];
				if (tNota < 0f || tNota > duracionSeg[n]) continue;
				float ataque    = Mathf.Min(1f, tNota / 0.012f);
				float caida     = Mathf.Exp(-tNota * 3.2f);
				float envolvente = ataque * caida;
				// Fundamental + un armónico suave para que suene a campanita, no a pitido plano.
				float onda = Mathf.Sin(Mathf.Tau * frecuencias[n] * tNota) * 0.75f
						   + Mathf.Sin(Mathf.Tau * frecuencias[n] * 2f * tNota) * 0.25f;
				muestra += onda * envolvente;
			}
			EscribirMuestra(datos, i, Mathf.Tanh(muestra * 0.8f) * 0.85f);
		}
		return ArmarWav(datos, MUESTREO);
	}

	private static void EscribirMuestra(byte[] datos, int i, float valor)
	{
		short pcm = (short)(Mathf.Clamp(valor, -1f, 1f) * short.MaxValue);
		datos[i * 2]     = (byte)(pcm & 0xFF);
		datos[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
	}

	private static AudioStreamWav ArmarWav(byte[] datos, int muestreo) => new()
	{
		Format  = AudioStreamWav.FormatEnum.Format16Bits,
		MixRate = muestreo,
		Stereo  = false,
		Data    = datos,
	};
}
