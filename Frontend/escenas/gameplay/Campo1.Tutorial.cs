using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Campo1 : Node2D
{
	// ═══════════════════════════════════════════════════════════════════════
	// MODO TUTORIAL — ETAPA 2 de la construcción por partes:
	//   · Splash "BIENVENIDO AL TUTORIAL" (2s) antes de mostrar nada del campo.
	//   · Cuadro "Guia" (efectos/guiatexto.tscn, hecho por el usuario) con 3 mensajes de
	//     introducción (turnos/energía, botones/rendirse/tiempo límite, 20s por turno) — cada uno
	//     avanza con un click/touch en cualquier lado ("EsperarClickParaAvanzar").
	//   · Recién ahí aparece la mano (3 cartas fijas) y el aviso "Coloca tus 3 tropas...", que se
	//     resuelve solo cuando las 3 quedan puestas (reusa _faseApertura, que YA bloquea barajar/
	//     sacrificar/hechizos/ataque hasta ese momento en el juego normal — no hizo falta duplicar
	//     ese bloqueo acá).
	//   · Un cuarto mensaje de Guia explica el menú de tropa (ataque/defensa/habilidad); se oculta
	//     solo cuando el jugador abre ese menú por primera vez (ver MostrarMenuTropa).
	//   · Reloj congelado en 0:00, sin música, sin intro cinemática (ver Campo1.cs / Campo1.Turnos.cs
	//     / Campo1.Flujo.cs / Campo1.Nfc.cs para el gancho de click-para-avanzar).
	//
	// LO QUE FALTA (próxima etapa, no armado todavía): la secuencia de combate guionada turno por
	// turno (qué tropa ataca a cuál, el rival apareciendo con sus 3 tropas sin el aviso normal de
	// "rival listo"), la curación guiada arrastrando la carta de Curación, el desbloqueo/uso guiado
	// de la habilidad del Maguín forzando el objetivo (tanque rival → pez, permanente), el
	// sacrificio guiado del Soldado Real + elección de reemplazo, el remate final (atacar con todo)
	// y la pantalla especial "TUTORIAL APROBADO" (sin música/reloj/monedas, botón "Repetir tutorial"
	// que reinicia toda esta secuencia desde cero). También: escenario/música fijos en Medieval con
	// el trono del Sargento Huevo de rival, y que entre los ardides mostrados solo Curación pueda
	// usarse de verdad (los demás visibles pero sin efecto) durante esa etapa.
	// ═══════════════════════════════════════════════════════════════════════

	private const string RUTA_GOLEM_TUTORIAL       = "res://cartas prime/MEDIEVAL/Golem_prime.tscn";
	private const string RUTA_MAGUIN_TUTORIAL      = "res://cartas prime/MEDIEVAL/Maguin_prime.tscn";
	private const string RUTA_SOLDADOREAL_TUTORIAL = "res://cartas prime/MEDIEVAL/SoldadoReal_prime.tscn";

	/// <summary>Reemplaza el mazo de 8 cartas de la sesión por las 3 fijas del tutorial (1 táctico,
	/// 1 asesino, 1 coloso — Maguín, Soldado Real, Golem). Se llama ANTES de PrepararMazoSinRepetir/
	/// InicializarClasificacionMazo, así que el resto del juego (tipos, fan de la mano, invocación)
	/// funciona sin ningún cambio, como si el jugador hubiera armado ese mazo de verdad.</summary>
	private void ForzarMazoTutorial()
	{
		escenasTropas = new[] { RUTA_GOLEM_TUTORIAL, RUTA_MAGUIN_TUTORIAL, RUTA_SOLDADOREAL_TUTORIAL };
		imagenesCartas = new string[escenasTropas.Length];
		for (int i = 0; i < escenasTropas.Length; i++)
			imagenesCartas[i] = ClasificacionCartas.ImagenBatalla(escenasTropas[i]) ?? "";
		GD.Print("[Tutorial] Mazo fijo: Golem, Maguín, Soldado Real");
	}

	/// <summary>Pone las 3 cartas fijas DIRECTO en Spot1/2/3 — sin el sorteo por tipo de la partida
	/// normal. Ya NO se llama desde _Ready: la llama la propia secuencia del tutorial recién
	/// después de los 3 mensajes de introducción (ver PasoMostrarManoTutorial).</summary>
	private void IniciarManoTutorial()
	{
		for (int i = 0; i < SPOTS_MANO.Length && i < escenasTropas.Length; i++)
			CrearCartaConIndice(SPOTS_MANO[i], i);
	}

	// ── CAPA DEL TUTORIAL (splash + cuadro Guia) ──────────────────────────────────────────────
	private CanvasLayer _capaTutorial;

	private void IniciarInterfazTutorial()
	{
		_capaTutorial = new CanvasLayer { Name = "CapaTutorial", Layer = 500 }; // por encima de TODO
		AddChild(_capaTutorial);

		CrearGuiaTutorial();
		MostrarSplashBienvenida();
	}

	// ── SPLASH "BIENVENIDO AL TUTORIAL" (~4s, semioscuro, luego se revela el campo) ───────────
	private void MostrarSplashBienvenida()
	{
		var capa = new CanvasLayer { Name = "SplashBienvenidaTutorial", Layer = 600 };
		AddChild(capa);

		var fondo = new ColorRect { Color = new Color(0f, 0f, 0f, 0f) };
		fondo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		fondo.MouseFilter = Control.MouseFilterEnum.Stop; // no deja pasar clicks mientras dura
		capa.AddChild(fondo);

		// FullRect + alineación centrada (no el preset "Center", que depende del auto-tamaño del
		// Label) para que el texto quede siempre en el centro exacto de la pantalla, en cualquier
		// resolución — clave para que se vea bien también en celular.
		var lbl = new Label { Text = "BIENVENIDO AL TUTORIAL" };
		lbl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.VerticalAlignment = VerticalAlignment.Center;
		lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		lbl.Modulate = new Color(1, 1, 1, 0);
		EstiloUI.Texto(lbl, 64, EstiloUI.Dorado);
		capa.AddChild(lbl);

		var tw = CreateTween();
		tw.TweenProperty(fondo, "color", new Color(0.03f, 0.03f, 0.05f, 0.88f), 0.4f);
		tw.Parallel().TweenProperty(lbl, "modulate", new Color(1, 1, 1, 1), 0.4f);
		tw.TweenInterval(3.2f); // total con los fades: ~4s
		tw.TweenProperty(fondo, "color", new Color(0f, 0f, 0f, 0f), 0.4f);
		tw.Parallel().TweenProperty(lbl, "modulate", new Color(1, 1, 1, 0), 0.4f);
		tw.TweenCallback(Callable.From(() =>
		{
			capa.QueueFree();
			PasoGuiaIntro1();
		}));
	}

	// ── CUADRO "Guia" (efectos/guiatexto.tscn) ────────────────────────────────────────────────
	// Vive junto a la mano manual (ManoManual): es "el cuadro que nos indica", ya no el panel
	// armado por código de la etapa 1. Aparece con un efecto de chico a grande, y se corre solo
	// cuando el jugador arrastra una carta para invocar (OcultarGuiaTutorial/RestaurarGuiaTutorial,
	// llamados desde Carta.cs) o cuando abre el menú de acciones de una tropa (MostrarMenuTropa).
	private const string RUTA_GUIA_TUTORIAL = "res://efectos/guiatexto.tscn";
	private Node2D _guiaTutorial;
	private Label  _lblGuiaTutorial;
	private Tween  _tweenGuiaTutorial;
	private string _textoGuiaTutorialActual = "";

	private void CrearGuiaTutorial()
	{
		var escena = GD.Load<PackedScene>(RUTA_GUIA_TUTORIAL);
		if (escena == null) return;
		_guiaTutorial = escena.Instantiate<Node2D>();
		_capaTutorial.AddChild(_guiaTutorial);
		_lblGuiaTutorial = _guiaTutorial.GetNodeOrNull<Label>("Info-tutorial/LblHabilidad");

		// Más abajo (más cerca de la mano) que en la primera versión: antes quedaba muy arriba,
		// casi en el centro de la pantalla, lejos de las cartas que señala.
		var mano = GetNodeOrNull<Control>("ManoManual");
		Vector2 pos = mano != null ? mano.GlobalPosition + new Vector2(20f, -90f) : new Vector2(640f, 620f);
		_guiaTutorial.Position = pos;
		_guiaTutorial.Visible = false;
	}

	// Escala final del cuadro una vez terminada la animación de aparición — más grande que el
	// tamaño original del .tscn (pedido explícito: se veía chico).
	private static readonly Vector2 ESCALA_GUIA_TUTORIAL = new Vector2(1.45f, 1.45f);

	/// <summary>Muestra el cuadro Guia con un texto nuevo, con la animación de chico a grande.</summary>
	private void MostrarGuiaConTexto(string texto)
	{
		_textoGuiaTutorialActual = texto;
		if (_guiaTutorial == null) return;
		if (_lblGuiaTutorial != null) _lblGuiaTutorial.Text = texto;
		_guiaTutorial.Visible = true;
		_guiaTutorial.Scale = ESCALA_GUIA_TUTORIAL * 0.15f;
		_tweenGuiaTutorial?.Kill();
		_tweenGuiaTutorial = CreateTween();
		_tweenGuiaTutorial.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_tweenGuiaTutorial.TweenProperty(_guiaTutorial, "scale", ESCALA_GUIA_TUTORIAL, 0.35f);
	}

	/// <summary>Se llama desde Carta.cs apenas el jugador empieza a arrastrar una carta, y desde
	/// MostrarMenuTropa al abrir el menú de una tropa: el cuadro se corre para no tapar la vista.</summary>
	public void OcultarGuiaTutorial()
	{
		if (_guiaTutorial != null) _guiaTutorial.Visible = false;
	}

	/// <summary>Se llama desde Carta.cs cuando termina un arrastre (soltó o canceló) — vuelve a
	/// mostrar el cuadro con el texto vigente, si había uno.</summary>
	public void RestaurarGuiaTutorial()
	{
		if (_guiaTutorial != null) _guiaTutorial.Visible = !string.IsNullOrEmpty(_textoGuiaTutorialActual);
	}

	/// <summary>Oculta/muestra TODA la capa del tutorial (cuadro de guía "info") mientras el menú de
	/// pausa está abierto. La capa está en Layer 500 (por encima de todo), así que sin esto el "info"
	/// quedaba flotando ENCIMA del panel de pausa. Lo llama MenuPausa al pausar/reanudar. Fuera del
	/// tutorial _capaTutorial es null → no hace nada.</summary>
	public void OcultarCapaTutorialEnPausa(bool pausado)
	{
		if (_capaTutorial != null && IsInstanceValid(_capaTutorial))
			_capaTutorial.Visible = !pausado;
	}

	// ── "Dale click a cualquier lado para seguir" — mecanismo real de avance ─────────────────
	// (Antes el texto lo prometía pero nada estaba conectado; ahora si ModoTutorial y hay un paso
	// esperando, el primer click/touch en cualquier lado dispara el siguiente paso. Enganchado
	// desde el _UnhandledInput que ya existe en Campo1.Nfc.cs para no duplicar el override.)
	private bool   _esperandoClickTutorial = false;
	private Action _alAvanzarClickTutorial;

	private void EsperarClickParaAvanzar(Action alAvanzar)
	{
		_esperandoClickTutorial = true;
		_alAvanzarClickTutorial = alAvanzar;
	}

	/// <returns>true si el evento fue consumido acá (el llamador debe cortar su propio manejo).</returns>
	private bool ManejarClickAvanceTutorial(InputEvent @event)
	{
		if (!_esperandoClickTutorial) return false;
		bool esClick = (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			|| (@event is InputEventScreenTouch st && st.Pressed);
		if (!esClick) return false;

		_esperandoClickTutorial = false;
		var accion = _alAvanzarClickTutorial;
		_alAvanzarClickTutorial = null;
		accion?.Invoke();
		GetViewport().SetInputAsHandled();
		return true;
	}

	// ── SECUENCIA DE INTRODUCCIÓN (3 mensajes de Guia, luego la mano) ────────────────────────
	private void PasoGuiaIntro1()
	{
		MostrarGuiaConTexto("¡Hola! Vamos a guiarte en cómo funciona EGGODIA. Primero: este juego es " +
			"por turnos, y solo tienes 3 energías cada turno. Invocar cartas NO consume energía.");
		EsperarClickParaAvanzar(PasoGuiaIntro2);
	}

	private void PasoGuiaIntro2()
	{
		MostrarGuiaConTexto("Cada botón que veas consume energía, menos el de la esquina derecha (el " +
			"rojo), que es para rendirse. Cada partida dura 3 minutos: si se acaba el tiempo, gana " +
			"quien tenga más vida.");
		EsperarClickParaAvanzar(PasoGuiaIntro3);
	}

	private void PasoGuiaIntro3()
	{
		MostrarGuiaConTexto("En cada turno tienes 20 segundos para armar tu estrategia. Ahora te vamos " +
			"a presentar nuestras 3 cartas.");
		EsperarClickParaAvanzar(PasoMostrarManoTutorial);
	}

	private void PasoMostrarManoTutorial()
	{
		OcultarGuiaTutorial();
		IniciarManoTutorial();
		MostrarAviso("Coloca tus 3 tropas en los círculos azules", Colors.Gold);
		EsperarColocarTresTropasTutorial();
	}

	/// <summary>Sondea (con un Timer, no _Process — Campo1.SecuenciaDigital.cs ya usa ese override)
	/// hasta que las 3 tropas quedan puestas, y recién ahí explica el menú de acciones.</summary>
	private void EsperarColocarTresTropasTutorial()
	{
		var t = new Timer { WaitTime = 0.3, Autostart = true, OneShot = false };
		AddChild(t);
		t.Timeout += () =>
		{
			if (!TodosSpotsOcupados()) return;
			t.Stop();
			t.QueueFree();
			PasoExplicarMenuTropaTutorial();
		};
	}

	private void PasoExplicarMenuTropaTutorial()
	{
		MostrarGuiaConTexto("¡Bien hecho! Ahora presiona al personaje que quieras y vas a ver que tiene " +
			"3 funciones: ATAQUE, DEFENSA y la mejor, ¡HABILIDAD!");
		EsperarClickParaAvanzar(PasoTuTurnoCombateTutorial);
	}

	// ── RIVAL FIJO DEL TUTORIAL (Sargento Huevo) ──────────────────────────────────────────────
	// Aparece espejado por carril según qué tropa propia haya en cada uno (no según el orden en
	// que se armó la mano, porque el jugador puede arrastrar sus 3 cartas a cualquier carril):
	// donde esté el Soldado Real aparece el Soldado Cartoon, donde esté el Maguín aparece el
	// Campero, y donde esté el Gólem aparece el Tanque — así el resto de la secuencia guionada
	// (quién ataca a quién) siempre calza sin importar el orden en que el jugador invocó.
	private static readonly Dictionary<System.Type, string> RIVAL_FORZADO_TUTORIAL = new()
	{
		{ typeof(SoldadoRealPrime), "res://cartas prime/TOONS/Soldado_cartoon_prime.tscn" },
		{ typeof(MaguinPrime),      "res://cartas prime/TOONS/Campero_cartoon_prime.tscn" },
		{ typeof(GolemPrime),       "res://cartas prime/TOONS/Tanque_cartoon_prime.tscn" },
	};

	// Mientras es true, MostrarMenuTropa (Campo1.Flujo.cs) no abre nada y no muestra ningún aviso —
	// ni siquiera el de "coloca tus tropas". Se usa durante transiciones del tutorial (el rival
	// apareciendo, el mensaje "¡Bien hecho!...") donde tocar una tropa antes no debería hacer NADA,
	// ni interrumpir con un cartel — antes, tocar una tropa en esa ventana repetía el aviso de
	// "coloca tus tropas restantes", que ya no aplicaba (bug reportado).
	private bool _tutorialBloqueoTotal = false;

	private async void InvocarRivalTutorial()
	{
		// Se llama apenas el jugador termina de colocar su 3ª tropa (ver TropaInvocada en
		// Campo1.Flujo.cs). _faseApertura se apaga YA (no solo cuando arranca el combate guiado) —
		// _tutorialBloqueoTotal es quien de verdad impide tocar algo hasta que corresponda.
		_faseApertura = false;
		_tutorialBloqueoTotal = true;

		foreach (string nombre in new[] { "Mod1", "Mod2", "Mod3" })
		{
			Node2D zonaJugador = GetTree().Root.FindChild(nombre, true, false) as Node2D;
			Node ocupado = zonaJugador?.GetNodeOrNull("Ocupado");
			Node2D tropaJugador = (ocupado != null && ocupado.HasMeta("tropa_instanciada"))
				? (Node2D)ocupado.GetMeta("tropa_instanciada") : null;
			if (tropaJugador == null) continue;

			string ruta = null;
			foreach (var par in RIVAL_FORZADO_TUTORIAL)
			{
				if (par.Key.IsInstanceOfType(tropaJugador)) { ruta = par.Value; break; }
			}
			if (ruta == null) continue;

			// Aparecen en orden — ModRival1, después ModRival2, después ModRival3 — no las 3 juntas.
			Node2D zonaRival = GetTree().Root.FindChild("ModRival" + nombre.Substring(3), true, false) as Node2D;
			var escena = GD.Load<PackedScene>(ruta);
			if (zonaRival != null && escena != null)
			{
				InvocacionRival(zonaRival, escena);
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(0.55f), "timeout");
				if (juegoTerminado) return;
			}
		}
	}

	// ── PRIMER TURNO DE COMBATE GUIADO (3 acciones forzadas, una por tropa) ───────────────────
	private enum AccionForzadaTutorial { Ninguna, Atacar, Defender, Habilidad }
	private System.Type            _tropaForzadaTutorial;
	private AccionForzadaTutorial  _accionForzadaTutorial = AccionForzadaTutorial.Ninguna;
	private Action                 _alCompletarPasoForzadoTutorial;

	/// <summary>Deja habilitado en el menú de acciones SOLO el botón indicado, y solo para la tropa
	/// indicada (ver los ganchos en MostrarMenuTropa/_on_btn_*_pressed, Campo1.Flujo.cs).</summary>
	private void ForzarPasoTutorial(System.Type tipoTropa, AccionForzadaTutorial accion, Action alCompletar)
	{
		_tropaForzadaTutorial = tipoTropa;
		_accionForzadaTutorial = accion;
		_alCompletarPasoForzadoTutorial = alCompletar;
	}

	/// <summary>La llaman los handlers de ataque/defensa/habilidad al completar la acción. Si no
	/// coincide con el paso forzado vigente (o no hay ninguno), no hace nada.</summary>
	private void AvanzarPasoForzadoTutorial(AccionForzadaTutorial completada)
	{
		if (_accionForzadaTutorial == AccionForzadaTutorial.Ninguna || _accionForzadaTutorial != completada) return;
		_tropaForzadaTutorial = null;
		_accionForzadaTutorial = AccionForzadaTutorial.Ninguna;
		var siguiente = _alCompletarPasoForzadoTutorial;
		_alCompletarPasoForzadoTutorial = null;
		siguiente?.Invoke();
	}

	private void PasoTuTurnoCombateTutorial()
	{
		_tutorialBloqueoTotal = false; // recién ahora se puede abrir el menú de una tropa
		MostrarGuiaConTexto("Ahora es tu turno. Tú vas a poder decidir, pero por ahora te damos las " +
			"indicaciones: haz clic en el Soldado Real y elige ATAQUE.");
		ForzarPasoTutorial(typeof(SoldadoRealPrime), AccionForzadaTutorial.Atacar, PasoDefenderMaguinTutorial);
	}

	private void PasoDefenderMaguinTutorial()
	{
		MostrarGuiaConTexto("Ahora haz clic en el Maguín y elige DEFENSA.");
		ForzarPasoTutorial(typeof(MaguinPrime), AccionForzadaTutorial.Defender, PasoAtacarGolemTutorial);
	}

	private void PasoAtacarGolemTutorial()
	{
		MostrarGuiaConTexto("Por último, haz clic en el Gólem y elige ATAQUE.");
		ForzarPasoTutorial(typeof(GolemPrime), AccionForzadaTutorial.Atacar, PasoFinPrimerTurnoTutorial);
	}

	private void PasoFinPrimerTurnoTutorial()
	{
		// Gastaste las 3 energías del turno (una por acción) — el cambio de turno hacia el rival lo
		// maneja el flujo normal del juego (RegistrarGastoMovimiento -> CambiarTurno al llegar a 0),
		// que termina llamando a EjecutarTurnoCPU() → EjecutarTurnoCPUTutorial() acá abajo.
		OcultarGuiaTutorial();
	}

	// ── TURNO DE ATAQUE GUIONADO DEL RIVAL ────────────────────────────────────────────────────
	// Nada de IA real: ataca en este orden fijo, sin habilidad ni ardides ni invocar de más.
	// Soldado Cartoon → el Soldado Real (recién atacó, sin escudo). Campero → el Maguín (tiene
	// escudo por la DEFENSA del paso anterior, así que este golpe no le hace daño). Tanque → el
	// Gólem. El orden de tipos, no de carril, porque InvocarRivalTutorial ya garantizó que cada
	// tropa rival está en el MISMO carril que su contraparte fijada.
	private static readonly System.Type[] ORDEN_ATAQUE_RIVAL_TUTORIAL =
		{ typeof(SoldadoCartoonPrime), typeof(CamperoCartoonPrime), typeof(TanqueCartoonPrime) };

	private async Task EjecutarTurnoCPUTutorial()
	{
		foreach (var tipo in ORDEN_ATAQUE_RIVAL_TUTORIAL)
		{
			if (juegoTerminado) return;
			Node2D tropa = null;
			foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
			{
				if (n is Node2D n2 && IsInstanceValid(n2) && tipo.IsInstanceOfType(n2)) { tropa = n2; break; }
			}
			if (tropa == null) continue; // no debería faltar en este punto de la secuencia

			await EsperarTableroLibre();
			if (juegoTerminado || !IsInstanceValid(tropa)) return;
			if (BuscarObjetivoEnCarril(tropa, "tropas_jugador") != null)
				ProcesarCombateFrontal(tropa, "tropas_jugador");
			ActualizarInterfaz();
			await ToSignal(GetTree().CreateTimer(0.8f), "timeout");
			if (juegoTerminado) return;
		}

		if (!juegoTerminado)
		{
			CambiarTurno(); // vuelve el turno al jugador (turno 2)
			// Deja ver el cartel "TU TURNO" y arranca el paso guionado de Curación.
			GetTree().CreateTimer(1.6f).Timeout += () => { if (!juegoTerminado) PasoCurarSoldadoTutorial(); };
		}
	}

	// ═══════════════════════════════════════════════════════════════════════════
	// SEGUNDA MITAD GUIONADA: Curación → Habilidad Maguín → (sacrificio/remate/aprobado, próximas)
	// ═══════════════════════════════════════════════════════════════════════════

	// ── PIEZA 1: CURACIÓN GUIADA AL SOLDADO REAL ──────────────────────────────────────────────
	private bool _esperandoCuracionTutorial = false;

	private void PasoCurarSoldadoTutorial()
	{
		// Bloqueo de menús de tropa mientras se cura (el arrastre de la carta de Curación NO pasa por
		// MostrarMenuTropa, así que sigue permitido; esto solo evita que ataque una tropa por error).
		_tutorialBloqueoTotal = true;
		_esperandoCuracionTutorial = true;
		MostrarGuiaConTexto("El Soldado Real quedó herido por el ataque enemigo. Arrastra tu carta de " +
			"CURACIÓN sobre él (el círculo verde te marca dónde soltarla) para devolverle vida.");
	}

	/// <summary>La llama AplicarCuracion (Campo1.Hechizos.cs) cuando se cura una tropa en modo tutorial.
	/// Solo avanza si estábamos esperando la curación y fue sobre el Soldado Real.</summary>
	private void NotificarCuracionTutorial(Node2D objetivo)
	{
		if (!_esperandoCuracionTutorial || !(objetivo is SoldadoRealPrime)) return;
		_esperandoCuracionTutorial = false;
		_tutorialBloqueoTotal = false;
		CallDeferred(nameof(PasoMaguinTutorial));
	}

	// ── PIEZA 2: HABILIDAD DEL MAGUÍN SOBRE EL TANQUE (transformación permanente) ──────────────
	private void PasoMaguinTutorial()
	{
		MostrarGuiaConTexto("¡Bien hecho! Ahora usa la HABILIDAD del Maguín: transformará al Tanque " +
			"enemigo en un pez indefenso… ¡y en el tutorial es para siempre! Toca al Maguín y elige HABILIDAD.");
		// Solo se puede tocar el Maguín, y solo su botón HABILIDAD queda habilitado. Al usarla,
		// AvanzarPasoForzadoTutorial(Habilidad) dispara el siguiente paso.
		ForzarPasoTutorial(typeof(MaguinPrime), AccionForzadaTutorial.Habilidad, PasoTrasMaguinTutorial);
	}

	private void PasoTrasMaguinTutorial()
	{
		// PRÓXIMA PIEZA (aún no armada): sacrificio guiado del Soldado Real + elegir reemplazo,
		// remate final y pantalla "TUTORIAL APROBADO". Por ahora cierra el guion sin trabar nada.
		MostrarGuiaConTexto("¡Excelente! Transformaste al Tanque. (El tutorial continuará con el " +
			"sacrificio y el remate final.)");
	}
}
