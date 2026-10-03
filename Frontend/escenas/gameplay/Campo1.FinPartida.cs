using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ── ESTADÍSTICAS POR TROPA (MVT: Most Valuable Troop) ──────────────────
	private class RegistroTropa
	{
		public string Nombre;
		public string Ruta; // escena de la tropa: clave de los favoritos del perfil
		public bool EsJugador;
		public int Daño;
		public Texture2D Ilustracion;
	}
	private readonly Dictionary<ulong, RegistroTropa> _statsPorTropa = new();
	private Dictionary<string, Texture2D> _cacheIlustraciones;
	private Dictionary<string, string> _cacheNombresCompletos;

	/// <summary>Acumula el daño causado por una tropa individual, para el ranking de MVT al
	/// terminar la partida. Debe llamarse en todo punto donde una tropa aplique daño, tanto
	/// desde el combate central (Campo1.Combate.cs) como desde las tropas autogestionadas.</summary>
	public void RegistrarDañoTropa(Node2D atacante, int daño)
	{
		if (!IsInstanceValid(atacante) || daño <= 0) return;
		// Tortuga/Pez del Maguín: el daño cuenta para la TROPA ORIGINAL transformada (su carta, su
		// nombre y su ícono), no para un "personaje" aparte sin carta que dejaba el MVT sin ícono.
		if (atacante is TortugaYPescado animal && animal.tropaOriginal != null && IsInstanceValid(animal.tropaOriginal))
			atacante = animal.tropaOriginal;
		ulong id = atacante.GetInstanceId();
		if (!_statsPorTropa.TryGetValue(id, out var reg))
		{
			reg = new RegistroTropa
			{
				Nombre      = NombreCorto(atacante),
				Ruta        = atacante.SceneFilePath,
				EsJugador   = atacante.IsInGroup("tropas_jugador"),
				Ilustracion = ObtenerIlustracionTropa(atacante),
			};
			_statsPorTropa[id] = reg;
		}
		reg.Daño += daño;
	}

	// Empareja la escena de batalla de la tropa (SceneFilePath) con la ilustración
	// definida en su CartaData (res://DatosCartas/*.tres) para mostrarla en el MVT.
	private Texture2D ObtenerIlustracionTropa(Node2D tropa)
	{
		string ruta = tropa.SceneFilePath;
		if (string.IsNullOrEmpty(ruta)) return null;

		if (_cacheIlustraciones == null)
		{
			_cacheIlustraciones = new Dictionary<string, Texture2D>();
			using var dir = DirAccess.Open("res://DatosCartas");
			if (dir != null)
			{
				dir.ListDirBegin();
				string archivo = dir.GetNext();
				while (archivo != "")
				{
					// En el APK exportado los .tres aparecen como "x.tres.remap": sin quitar ese sufijo no se
					// encontraba NINGUNA carta en el celular (ni ícono ni nombre para el MVT).
					if (archivo.EndsWith(".remap")) archivo = archivo[..^".remap".Length];
					if (archivo.EndsWith(".tres"))
					{
						var datos = GD.Load<CartaData>($"res://DatosCartas/{archivo}");
						if (datos != null && !string.IsNullOrEmpty(datos.RutaEscena) && datos.Imagen != null)
							_cacheIlustraciones[datos.RutaEscena] = datos.Imagen;
					}
					archivo = dir.GetNext();
				}
			}
		}
		if (_cacheIlustraciones.TryGetValue(ruta, out var tex) && tex != null) return tex;

		// Escena sin carta propia pero que ES la misma carta que otra (p. ej. TRex_prime y Paper_Rex
		// son el Paper-Rex): se usa el ÍCONO de esa carta equivalente, no la imagen grande.
		string cartaPng = ClasificacionCartas.Clasificar(ruta).CartaPng;
		if (!string.IsNullOrEmpty(cartaPng))
			foreach (var par in _cacheIlustraciones)
				if (par.Value != null && ClasificacionCartas.Clasificar(par.Key).CartaPng == cartaPng)
					return par.Value;

		// Fallback: si la carta no tiene Imagen asignada en su .tres (o no se encontró match),
		// usamos la ilustración grande de batalla (la misma que la mano de cartas), NUNCA un frame
		// en vivo del sprite animado — eso es lo que causaba el ícono roto en Victoria/Derrota
		// (se agarraba el frame de ESE instante, que podía caer a mitad de un ataque).
		var info = ClasificacionCartas.Clasificar(ruta);
		if (!string.IsNullOrEmpty(info.CartaPng) && ResourceLoader.Exists(info.CartaPng))
			return GD.Load<Texture2D>(info.CartaPng);

		return null;
	}

	// Empareja la escena de batalla de la tropa (SceneFilePath) con el Nombre completo definido
	// en su CartaData — para que los avisos en pantalla digan "Dragón de Flama" y no "Dragon",
	// exactamente como aparece en el Menú Constructor.
	public string ObtenerNombreCompleto(Node2D tropa)
	{
		string ruta = tropa.SceneFilePath;
		if (string.IsNullOrEmpty(ruta)) return null;

		if (_cacheNombresCompletos == null)
		{
			_cacheNombresCompletos = new Dictionary<string, string>();
			using var dir = DirAccess.Open("res://DatosCartas");
			if (dir != null)
			{
				dir.ListDirBegin();
				string archivo = dir.GetNext();
				while (archivo != "")
				{
					// En el APK exportado los .tres aparecen como "x.tres.remap": sin quitar ese sufijo no se
					// encontraba NINGUNA carta en el celular (ni ícono ni nombre para el MVT).
					if (archivo.EndsWith(".remap")) archivo = archivo[..^".remap".Length];
					if (archivo.EndsWith(".tres"))
					{
						var datos = GD.Load<CartaData>($"res://DatosCartas/{archivo}");
						if (datos != null && !string.IsNullOrEmpty(datos.RutaEscena) && !string.IsNullOrEmpty(datos.Nombre))
							_cacheNombresCompletos[datos.RutaEscena] = datos.Nombre;
					}
					archivo = dir.GetNext();
				}
			}
		}
		return _cacheNombresCompletos.TryGetValue(ruta, out var nombre) ? nombre : null;
	}

	/// <summary>Tropa más valiosa del bando indicado (mayor daño total causado en la partida).</summary>
	private (string nombre, int daño, Texture2D ilustracion) ObtenerMVT(bool ladoJugador)
	{
		RegistroTropa mejor = null;
		foreach (var reg in _statsPorTropa.Values)
		{
			if (reg.EsJugador != ladoJugador) continue;
			if (mejor == null || reg.Daño > mejor.Daño) mejor = reg;
		}
		return mejor != null ? (mejor.Nombre, mejor.Daño, mejor.Ilustracion) : (null, 0, null);
	}

	private void RegistrarPartidaParaFavoritos()
	{
		var dañoPorTropa = new Dictionary<string, int>();
		foreach (var reg in _statsPorTropa.Values)
		{
			if (!reg.EsJugador || string.IsNullOrEmpty(reg.Ruta)) continue;
			dañoPorTropa[reg.Ruta] = (dañoPorTropa.TryGetValue(reg.Ruta, out int d) ? d : 0) + reg.Daño;
		}
		Preferencias.RegistrarPartidaParaFavoritos(dañoPorTropa);
	}

	// ── FIN DE PARTIDA ────────────────────────────────────────────────────
	private void DeterminarGanadorPorTiempo()
	{
		SincronizarVidaConBarra(); // que el resultado por tiempo use los mismos números que muestra la barra
		// En línea el resultado lo ARBITRA el servidor (no cada cliente por su cuenta, que causaba
		// que los dos se vieran ganando). Reportamos quién creemos que ganó; el server decide.
		if (EsOnline)
		{
			if (vidaJugador > vidaRival) EnviarResultadoOnline("yo");
			else if (vidaRival > vidaJugador) EnviarResultadoOnline("rival");
			else EnviarResultadoOnline("empate");
			return;
		}
		if (vidaJugador > vidaRival) FinalizarPartida("¡VICTORIA!");
		else if (vidaRival > vidaJugador) FinalizarPartida("¡DERROTA!");
		else FinalizarPartida("¡EMPATE!");
	}

	// Si la barra ya se ve VACÍA, el huevo tiene que estar muerto. Antes podía quedar con una miga de
	// vida (p. ej. 5 de 2000 = 0.25%): la barra se veía en cero pero la partida seguía, que es
	// justamente el fallo reportado. Con el 1% del total, cualquier resto invisible cuenta como muerte.
	private const float FRACCION_VIDA_VISIBLE = 0.01f;

	// Lo que se ve en la barra tiene que coincidir SIEMPRE con si el huevo está vivo o no. Con el
	// porcentaje crudo, quedarse con 40 de 2000 (2%) pintaba una tira de pocos píxeles que a simple
	// vista es "vacío", y sin embargo la partida seguía — ese era el fallo reportado. Ahora, mientras
	// quede aunque sea 1 de vida, la barra nunca baja de este mínimo bien visible; y cuando de verdad
	// está muerto, SincronizarVidaConBarra lo deja en 0 y la barra se ve vacía de verdad.
	private const float MINIMO_BARRA_VISIBLE = 5f; // % de la parte VISIBLE de la barra

	// La imagen del relleno (BarraVidaProgress.png) mide 398 px de ancho, pero el marco
	// (BarraVida.png) tapa los bordes: el relleno solo se VE entre los px 30 y 367. Godot reparte el
	// valor sobre los 398 px, así que con poca vida el relleno quedaba entero escondido detrás del
	// marco (el primer ~7,5 %) y la barra parecía vacía con el huevo todavía vivo. Por eso el
	// porcentaje de vida se reparte solo sobre la parte visible. Si se cambian esas imágenes, medir de
	// nuevo estos tres números.
	private const float ANCHO_IMAGEN_BARRA = 398f;
	private const float PX_VISIBLE_DESDE   = 30f;
	private const float PX_VISIBLE_HASTA   = 367f;

	/// <summary>Valor a pintar en la barra: 0 solo si está realmente muerto; si le queda algo de vida,
	/// el relleno siempre asoma fuera del marco (nunca menos del mínimo visible). Vale igual para el
	/// jugador y para el rival (cuya barra se llena de derecha a izquierda).</summary>
	private double PorcentajeBarraVida(int vida, bool esRival = false)
	{
		if (vida <= 0) return 0;
		// Tutorial: el huevo rival tiene poca vida de verdad (para que caiga al morir sus tropas),
		// pero SU barra se mide contra esa vida reducida — así se ve llena al empezar, en vez de a
		// un quinto, que daba la impresión de que ya venía golpeado.
		int maximo = (ModoTutorial && esRival) ? VIDA_RIVAL_TUTORIAL : vidaMaxJugador;
		float pct = Mathf.Clamp((float)vida / maximo * 100f, MINIMO_BARRA_VISIBLE, 100f);

		// La barra del rival se llena desde la derecha: su parte visible empieza a (ancho - 367) px.
		float desde = esRival ? ANCHO_IMAGEN_BARRA - PX_VISIBLE_HASTA : PX_VISIBLE_DESDE;
		float hasta = esRival ? ANCHO_IMAGEN_BARRA - PX_VISIBLE_DESDE : PX_VISIBLE_HASTA;
		float px = desde + pct / 100f * (hasta - desde);
		return px / ANCHO_IMAGEN_BARRA * 100f;
	}

	/// <summary>Redondea a 0 la vida que ya no se ve en la barra, para los DOS bandos (jugador y bot).
	/// Así lo que muestra la barra y lo que decide la partida nunca se contradicen.</summary>
	private void SincronizarVidaConBarra()
	{
		int minimoVisible = Mathf.CeilToInt(vidaMaxJugador * FRACCION_VIDA_VISIBLE);
		int minimoRival   = ModoTutorial ? Mathf.CeilToInt(VIDA_RIVAL_TUTORIAL * FRACCION_VIDA_VISIBLE) : minimoVisible;
		if (vidaJugador > 0 && vidaJugador <= minimoVisible) vidaJugador = 0;
		if (vidaRival   > 0 && vidaRival   <= minimoRival)   vidaRival   = 0;
	}

	private void CheckEstadoJuego()
	{
		SincronizarVidaConBarra();
		if (EsOnline)
		{
			if (vidaJugador <= 0) EnviarResultadoOnline("rival");     // mi huevo murió → ganó el rival
			else if (vidaRival <= 0) EnviarResultadoOnline("yo");     // huevo rival murió → gané yo
			return;
		}
		if (vidaJugador <= 0) FinalizarPartida("DERROTA");
		else if (vidaRival <= 0) FinalizarPartida("VICTORIA");
	}

	// Rendirse contra el bot (pausa → RENDIRSE): la partida termina como derrota, pero NO se cuenta en
	// las estadísticas de la cuenta ni da monedas de consuelo — es una práctica contra la computadora.
	private bool _rendidoContraBot;

	/// <summary>RENDIRSE del menú de pausa (solo contra el bot): cierra como derrota sin registrarla.</summary>
	public void RendirseContraBot()
	{
		if (juegoTerminado) return;
		_rendidoContraBot = true;
		FinalizarPartida("DERROTA");
	}

	public async void FinalizarPartida(string msg)
	{
		if (juegoTerminado) return;
		juegoTerminado = true;
		ReportarResultadoContraBot(msg); // contra un bot "en línea": el servidor registra el final
		// Favoritos del perfil: cuenta toda partida terminada (gane, pierda o empate), salvo el tutorial
		// y rendirse contra el bot. Va antes del premio, que ya manda los favoritos al servidor.
		if (!ModoTutorial && !_rendidoContraBot
			&& (msg.Contains("VICTORIA") || msg.Contains("DERROTA") || msg.Contains("EMPATE")))
			RegistrarPartidaParaFavoritos();
		CerrarConfirmacionRetirada();
		timerReloj.Stop();
		GetTree().Paused = false;
		CerrarPantallaRobo(); // por seguridad: nunca dejar la pantalla de robo abierta si la partida termina
		// El menú de acciones de tropa NO debe asomar por encima del cierre ni de Victoria/Derrota.
		if (menuAcciones != null && IsInstanceValid(menuAcciones)) menuAcciones.Visible = false;
		// El filtro "toon" (si aplica) NO se apaga acá: debe verse también en la frase de cierre y
		// en Victoria/Derrota. Solo se va al abandonar Campo1 (los botones de esas pantallas).

		// Actualizar historial de dificultad
		if (msg.Contains("VICTORIA")) _victoriasJugador++;
		else if (msg.Contains("DERROTA")) _derrotasJugador++;

		// ── Secuencia de cierre: 4s en el propio campo de batalla, con frase burlona/celebratoria
		// y el TiempoPanel mostrando una carita en vez del reloj, ANTES de abrir la pantalla final.
		if (msg.Contains("VICTORIA") || msg.Contains("DERROTA"))
		{
			bool esVictoria = msg.Contains("VICTORIA");
			string[] frases = esVictoria ? FRASES_VICTORIA : FRASES_DERROTA;
			string[] caras  = esVictoria ? CARAS_VICTORIA  : CARAS_DERROTA;
			Color colorFrase = esVictoria ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 0.42f, 0.38f);

			// Primero muere el huevo que perdió (se aplasta, brinca gritando, sale volando del mapa y
			// recién ahí se escucha el CRACK). La frase de cierre aparece DESPUÉS, no encima.
			// Si el que perdió es el bot, es exactamente lo mismo pero espejado hacia el otro lado.
			await AnimarMuerteHuevo(perdioElJugador: !esVictoria);
			if (!IsInstanceValid(this)) return;

			// Tutorial: no van las frases burlonas/celebratorias al azar — siempre el mismo cierre.
			string frase = (ModoTutorial && esVictoria) ? "TUTORIAL COMPLETADO" : frases[random.Next(frases.Length)];
			MostrarFraseFinPartida(frase, colorFrase);
			if (_lblTiempo != null) _lblTiempo.Text = caras[random.Next(caras.Length)];

			// El huevo que quedó vivo festeja mientras se lee la frase: se mece de lado a lado en su
			// trono. Si ganó el bot, baila el suyo — es el mismo efecto para los dos bandos.
			BailarHuevoGanador(ganoElJugador: esVictoria);

			// AnimarMuerteHuevo cortó la música para que se oyeran limpios el frenazo, el grito y el
			// CRACK. Acá vuelve a arrancar, justo con la frase: los Seek de abajo necesitan que esté
			// sonando, si no quedarían apuntando a un reproductor detenido.
			if (_reproductorMusica != null && _reproductorMusica.Stream != null && !_reproductorMusica.Playing)
				_reproductorMusica.Play();
			// Ya no es música de ambiente: suena a su volumen original (sin ducking de voces encima).
			DuckingMusica.Reiniciar();
			if (_reproductorMusica != null)
				_reproductorMusica.VolumeDb = VOLUMEN_MUSICA_FIN_DB + _volumenExtraEscenario;

			// En victoria: salta a los últimos 10s de la canción y quedan en loop (sigue sonando
			// en la pantalla de Victoria). En derrota: arranca desde el minuto específico de ESE
			// escenario (ver ESCENARIOS_BATALLA) y NO se repite — termina y se queda en silencio.
			//
			// Mapa "digital": comportamiento propio, pedido explícitamente — tanto en victoria como
			// en derrota la música salta al 2:46 (mismo segundo para ambas, guardado en
			// _segundoDerrotaMusica) y sigue sonando normal desde ahí hasta el final de la pista
			// (sin loop — CongelarSecuenciaDigital ya frenó la secuencia, así que el fondo se queda
			// fijo en FONDO1 si ganamos o en el apagón total si perdemos, aunque la música siga).
			if (EscenarioEsDigital)
			{
				CongelarSecuenciaDigital(esVictoria);
				if (_reproductorMusica != null && _reproductorMusica.Stream != null)
				{
					if (_reproductorMusica.Stream is AudioStreamMP3 mp3digital) mp3digital.Loop = false;
					float duracionDigital = (float)_reproductorMusica.Stream.GetLength();
					float destinoDigital = Mathf.Clamp(_segundoDerrotaMusica, 0f, Mathf.Max(0f, duracionDigital - 0.1f));
					_reproductorMusica.Seek(destinoDigital);
				}
			}
			else if (_reproductorMusica != null && _reproductorMusica.Stream != null)
			{
				if (_reproductorMusica.Stream is AudioStreamMP3 mp3) mp3.Loop = esVictoria;
				float duracion = (float)_reproductorMusica.Stream.GetLength();
				// Tutorial: al salir la frase de cierre suenan los ÚLTIMOS 15 SEGUNDOS de su música
				// (en la partida normal son los últimos 10).
				if (ModoTutorial)
				{
					float colaTutorial = Mathf.Max(0f, duracion - 15f);
					_reproductorMusica.Seek(colaTutorial);
				}
				else if (esVictoria)
				{
					if (duracion > 10f) _reproductorMusica.Seek(duracion - 10f);
				}
				else
				{
					float destino = Mathf.Clamp(_segundoDerrotaMusica, 0f, Mathf.Max(0f, duracion - 0.1f));
					_reproductorMusica.Seek(destino);
				}
			}

			await ToSignal(GetTree().CreateTimer(4.0), "timeout");
			if (!IsInstanceValid(this)) return;
		}

		if (msg.Contains("DERROTA"))
		{
			// El tutorial no paga: ni al ganar ni al (raro) perder. Con cuenta, el premio y la derrota los
			// guarda el servidor (antes las derrotas nunca llegaban al servidor). Rendirse contra el bot no
			// cuenta: ni derrota en la cuenta ni monedas.
			int monedasConsuelo = 0;
			if (!_rendidoContraBot)
			{
				if (!ModoTutorial) Preferencias.PartidasPerdidas++;
				monedasConsuelo = Economia.Instancia().RegistrarFinDePartida("derrota", 0, PareceOnline, ModoTutorial, _dañoTotalJugador);
			}
			var escenaDerrota = GD.Load<PackedScene>("res://escenas/gameplay/PantallaDerrota.tscn");
			if (escenaDerrota != null)
			{
				var pd = escenaDerrota.Instantiate();
				if (pd is PantallaDerrota pdScript)
				{
					pdScript.EsOnline         = PareceOnline; // decide REINTENTAR vs RE-ARMAR MAZO
					pdScript.MotivoFin        = _motivoFinOnline;
					pdScript.MonedasGanadas   = monedasConsuelo;
					pdScript.DañoInfligido    = _dañoTotalJugador;
					pdScript.BajasEnemigas    = _tropasEliminadasRival;
					var mvtRival = ObtenerMVT(false);
					pdScript.MvtNombre      = mvtRival.nombre;
					pdScript.MvtDaño        = mvtRival.daño;
					pdScript.MvtIlustracion = mvtRival.ilustracion;
				}
				AddChild(pd);
				SonidoUI.EngancharBotones(pd); // nace después de _Ready: se engancha acá
			}
			return;
		}

		if (msg.Contains("VICTORIA"))
		{
			// Se ve al instante; con cuenta, el servidor aplica la experiencia/victoria de verdad y el
			// nivel que devuelve es el que queda (el mismo en cualquier celular).
			if (!ModoTutorial)
			{
				Preferencias.PartidasGanadas++;
				Preferencias.AgregarExperiencia(Preferencias.XP_POR_VICTORIA);
			}
			// Tutorial ganado: recién ahora deja de ser obligatorio (ver MenuPrincipal._Ready).
			else Preferencias.TutorialPendiente = false;
			VerificarLogros(msg);
			var escenaVictoria = GD.Load<PackedScene>("res://escenas/gameplay/PantallaVictoria.tscn");
			int monedasGanadas = Economia.Instancia().RegistrarFinDePartida("victoria", _rachaVictorias + 1, PareceOnline, ModoTutorial, _dañoTotalJugador);
			if (escenaVictoria != null)
			{
				var pv = (PantallaVictoria)escenaVictoria.Instantiate();
				pv.EsOnline         = PareceOnline; // decide JUGAR DE NUEVO vs RE-ARMAR MAZO
				pv.MotivoFin        = _motivoFinOnline;
				pv.EsTutorial       = ModoTutorial; // sin monedas, sin auto-achicado, "REPETIR TUTORIAL"
				if (ModoTutorial) monedasGanadas = 0; // el tutorial no paga
				pv.DañoInfligido    = _dañoTotalJugador;
				pv.TropasEliminadas = _tropasEliminadasRival;
				pv.TurnosJugados    = _turnosJugados;
				pv.Racha            = _rachaVictorias + 1;
				pv.MonedasGanadas   = monedasGanadas;
				var mvtJugador = ObtenerMVT(true);
				pv.MvtNombre      = mvtJugador.nombre;
				pv.MvtDaño        = mvtJugador.daño;
				pv.MvtIlustracion = mvtJugador.ilustracion;
				AddChild(pv);
				SonidoUI.EngancharBotones(pv); // la pantalla nace después de _Ready: se engancha acá
			}

			if (SesionJuego.Instance != null)
			{
				SesionJuego.Instance.UltimoResultado    = "victoria";
				SesionJuego.Instance.DañoUltimaPartida  = _dañoTotalJugador;
			}
			return;
		}

		if (!HasNode("PantallaFinal")) return;
		var pantalla = GetNode<Control>("PantallaFinal");
		pantalla.Visible = true;

		// Resultado principal
		var lbl = GetNodeOrNull<Label>("PantallaFinal/MensajeResultado")
			   ?? GetNodeOrNull<Label>("PantallaFinal/LabelResultado");
		if (lbl != null)
		{
			lbl.Text     = msg;
			lbl.Modulate = msg.Contains("VICTORIA") ? Colors.Gold : msg.Contains("EMPATE") ? Colors.White : Colors.Red;
		}

		// ── Nombre del jugador y racha ────────────────────────────────────────
		string nombreJ = SesionJuego.Instance?.NombreJugador ?? "Jugador";
		var lblNombre = new Label();
		lblNombre.Text = nombreJ;
		lblNombre.AddThemeColorOverride("font_color", Colors.LightBlue);
		lblNombre.AddThemeFontSizeOverride("font_size", 17);
		lblNombre.Position = new Vector2(50, 55);
		pantalla.AddChild(lblNombre);

		if (_rachaVictorias > 1)
		{
			var lblRacha = new Label();
			lblRacha.Text = $"Racha: {_rachaVictorias} victorias seguidas";
			lblRacha.AddThemeColorOverride("font_color", Colors.OrangeRed);
			lblRacha.AddThemeFontSizeOverride("font_size", 16);
			lblRacha.Position = new Vector2(50, 80);
			pantalla.AddChild(lblRacha);
		}

		// ── Estadísticas ──────────────────────────────────────────────────────
		var lblStats = new Label();
		lblStats.Text = $"ESTADÍSTICAS\n" +
						$"Daño infligido:     {_dañoTotalJugador}\n" +
						$"Daño recibido:      {_dañoTotalRival}\n" +
						$"Tropas eliminadas:  {_tropasEliminadasRival}\n" +
						$"Tropas perdidas:    {_tropasEliminadasJugador}\n" +
						$"Turnos jugados:     {_turnosJugados}";
		lblStats.AddThemeColorOverride("font_color", Colors.White);
		lblStats.AddThemeFontSizeOverride("font_size", 15);
		lblStats.Position = new Vector2(50, 110);
		lblStats.AutowrapMode = TextServer.AutowrapMode.Word;
		pantalla.AddChild(lblStats);

		// ── Botones ───────────────────────────────────────────────────────────
		var btnReinicio = new Button();
		btnReinicio.Text              = "Jugar de nuevo";
		btnReinicio.Position          = new Vector2(50, 300);
		btnReinicio.CustomMinimumSize = new Vector2(190, 48);
		EstiloUI.Boton(btnReinicio, 20, accion: true); // botón principal también acá: turquesa, no gris
		btnReinicio.Pressed += () => { LimpiezaEfectos.LimpiarEfectosDeCampo(); GetTree().ReloadCurrentScene(); };
		// "Jugar de nuevo" NO tiene sentido en línea: recargar la escena volvería a leer el ContextoOnline
		// (MatchId/Semilla de la partida YA terminada) y re-entraría a la misma partida muerta. En online
		// solo se puede volver al menú y buscar un rival nuevo. Solo se muestra en partidas locales (vs bot).
		if (!PareceOnline) pantalla.AddChild(btnReinicio);

		var btnMenu = new Button();
		btnMenu.Text              = "Menú Principal";
		btnMenu.Position          = PareceOnline ? new Vector2(152, 300) : new Vector2(255, 300);
		btnMenu.CustomMinimumSize = new Vector2(190, 48);
		EstiloUI.Boton(btnMenu, 20);
		btnMenu.Pressed += () =>
		{
			LimpiezaEfectos.LimpiarEfectosDeCampo();
			// Al salir de una partida en línea terminada, borrar su contexto (MatchId/Semilla/rival) para
			// que no quede caché de la partida anterior colgando en memoria hasta el próximo emparejamiento.
			ContextoOnline.Limpiar();
			GetTree().ChangeSceneToFile("res://escenas/menu/menu_principal.tscn");
		};
		pantalla.AddChild(btnMenu);

		if (SesionJuego.Instance?.EstaLogueado == true)
		{
			var btnRanking = new Button();
			btnRanking.Text              = "Ver Ranking";
			btnRanking.Position          = new Vector2(50, 358);
			btnRanking.CustomMinimumSize = new Vector2(395, 44);
			EstiloUI.Boton(btnRanking, 18);
			btnRanking.AddThemeColorOverride("font_color", EstiloUI.Dorado); // acento dorado (es el destacado)
			btnRanking.Pressed += () =>
			{
				var panel = new PanelRanking();
				pantalla.AddChild(panel);
				panel.Mostrar();
			};
			pantalla.AddChild(btnRanking);
		}

		// Guardar resultado en sesión y enviar al backend
		string resultadoStr = msg.Contains("VICTORIA") ? "victoria"
							: msg.Contains("EMPATE")   ? "empate" : "derrota";
		VerificarLogros(msg);
		// Empate (y cualquier final por este camino): premio, estadística y, con cuenta, servidor.
		Economia.Instancia().RegistrarFinDePartida(resultadoStr, 0, PareceOnline, ModoTutorial, _dañoTotalJugador);
		if (SesionJuego.Instance != null)
		{
			SesionJuego.Instance.UltimoResultado    = resultadoStr;
			SesionJuego.Instance.DañoUltimaPartida  = _dañoTotalJugador;
		}
	}


	// ── CPU HECHIZOS ─────────────────────────────────────────────────────
	// El CPU ahora tiene acceso al mismo repertorio de hechizos que el jugador (antes solo alternaba
	// Veneno/Bloqueo). Reutiliza los mismos efectos que Campo1.Hechizos.cs aplica para el jugador
	// (AplicarCuracion/AplicarEncebollado/AplicarDesprotegido/AplicarEscudo/AplicarFuerza son
	// genéricos, no dependen de la mano del jugador) — pero NO reutiliza AplicarHechizoADestino ni
	// MarcarHechizoUsado, porque esas sí están atadas a las 2 cartas de hechizo visibles del jugador
	// (_tarjetasHechizoCarta/_cooldownHechizo); llamarlas desde acá le gastaría una carta de hechizo
	// AL JUGADOR por un hechizo que tiró el CPU.
	private static readonly string[] CPU_HECHIZOS_IDS =
		{ "veneno", "bloqueo", "curacion", "encebollado", "desprotegido", "escudo", "fuerza", "robar_carta", "debil" };

	private void CPUUsarHechizo()
	{
		if (_hechizoUsadoEsteTurno) return; // mismo límite de 1 hechizo/trampa por turno que el jugador

		string id = CPU_HECHIZOS_IDS[random.Next(CPU_HECHIZOS_IDS.Length)];
		bool esParaAliado = id is "curacion" or "encebollado" or "escudo" or "fuerza";

		if (id == "robar_carta")
		{
			if (!RobarCartaDelJugadorCPU()) return;
			_hechizoUsadoEsteTurno = true;
			_ardidesGastadosRival++; // al llegar a 2 se habilita su Nuclear
			return;
		}

		Node2D objetivo = esParaAliado ? BuscarAliadoRivalParaHechizo(id) : BuscarObjetivoJugadorMasFuerte();
		if (objetivo == null) return;

		// Misma regla que para el jugador: Escudo y Desprotegido no hacen nada sobre una tropa sin
		// escudo (Tanque, Paperex). Si le tocó ese objetivo, el bot NO gasta el hechizo ni el turno.
		if ((id == "escudo" || id == "desprotegido") && SinEscudo(objetivo)) return;

		_hechizoUsadoEsteTurno = true;
		_ardidesGastadosRival++; // al llegar a 2 se habilita su Nuclear

		switch (id)
		{
			case "veneno":
				objetivo.SetMeta("envenenado",   true);
				objetivo.SetMeta("danoVeneno",   50);
				objetivo.SetMeta("turnosVeneno", 3);
				objetivo.Modulate = COLOR_VENENO;
				break;
			case "bloqueo":
				if (objetivo.HasMethod("AlSerBloqueado")) objetivo.Call("AlSerBloqueado");
				objetivo.SetMeta("bloqueado",     true);
				objetivo.SetMeta("turnosBloqueo", 2);
				objetivo.Modulate = COLOR_BLOQUEO;
				break;
			case "curacion":      AplicarCuracion(objetivo);      break;
			case "encebollado":   AplicarEncebollado(objetivo);   break;
			case "desprotegido":  AplicarDesprotegido(objetivo);  break;
			case "debil":         AplicarDebil(objetivo);         break;
			case "escudo":        AplicarEscudo(objetivo);        break;
			case "fuerza":        AplicarFuerza(objetivo);        break;
		}
	}

	private Node2D BuscarObjetivoJugadorMasFuerte()
	{
		Node2D objetivo = null;
		int maxVida = 0;
		foreach (Node n in GetTree().GetNodesInGroup("tropas_jugador"))
		{
			if (!(n is Node2D t) || !IsInstanceValid(t)) continue;
			int v = Gi(t, "vidaActual");
			if (v > maxVida) { maxVida = v; objetivo = t; }
		}
		return objetivo;
	}

	/// <summary>Para Curación/Escudo prioriza al aliado más lastimado (más útil ahí); para
	/// Encebollado/Fuerza (buffs de ataque) prioriza al de más ataque actual, para reforzar a su
	/// pegador más fuerte. Si no hay ninguna tropa rival viva, no hay a quién aplicarlo.</summary>
	private Node2D BuscarAliadoRivalParaHechizo(string id)
	{
		bool priorizarVidaBaja = id is "curacion" or "escudo";
		Node2D mejor = null;
		float mejorPuntaje = priorizarVidaBaja ? float.MaxValue : float.MinValue;
		foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
		{
			if (!(n is Node2D t) || !IsInstanceValid(t)) continue;
			if (priorizarVidaBaja)
			{
				int vidaMax = Mathf.Max(1, Gi(t, "vidaMaxima"));
				float pct = (float)Gi(t, "vidaActual") / vidaMax;
				if (pct < mejorPuntaje) { mejorPuntaje = pct; mejor = t; }
			}
			else
			{
				int atk = Gi(t, "puntosAtaque");
				if (atk > mejorPuntaje) { mejorPuntaje = atk; mejor = t; }
			}
		}
		return mejor;
	}

	/// <summary>El CPU le roba una carta al azar de la mano visible del jugador (el propio CPU no
	/// tiene una "mano" real de la que jugar — elige tropa libremente de su mazo al invocar, ver
	/// ElegirTropaCPUDeck — así que robar solo tiene sentido como efecto disruptivo: te saca una
	/// carta de la mano, sin necesidad de dársela a nadie). Devuelve false sin gastar el hechizo si
	/// el jugador no tiene ninguna carta en mano para robar.</summary>
	private bool RobarCartaDelJugadorCPU()
	{
		if (contenedorMano == null) return false;
		var candidatas = new List<Carta>();
		foreach (Node n in contenedorMano.GetChildren())
			if (n is Carta c && c.EstaEnMano && !c.IsQueuedForDeletion()) candidatas.Add(c);
		if (candidatas.Count == 0) return false;

		var elegida = candidatas[random.Next(candidatas.Count)];
		// La carta pasa DE VERDAD a la mano del rival: la va a poder jugar, y vos se la podés robar
		// de vuelta (por eso su mano puede llegar a 4).
		// El índice de la carta robada es de MI mazo (escenasTropas); la mano del bot vive en su propio
		// espacio (_cartasCPU), así que hay que traducirlo o le aparecería una carta equivocada.
		if (elegida.IdCarta >= 0 && elegida.IdCarta < escenasTropas.Length)
		{
			int idxEnCPU = IndiceCPUDe(escenasTropas[elegida.IdCarta]);
			if (idxEnCPU >= 0 && !_manoVisualCPU.Contains(idxEnCPU)) _manoVisualCPU.Add(idxEnCPU);
		}
		elegida.NombreSpot = "X";
		elegida.QueueFree();
		// Colapsa de 4→3 cartas si correspondía (mismo criterio que al jugar cualquier carta) y dejar
		// a la vista solo las que realmente quedan — nunca "3 cartas pareciendo 4" ni al revés.
		ReacomodarManoTropas();
		MostrarAviso("¡El rival te robó una carta de la mano!", new Color(1f, 0.45f, 0.4f));
		return true;
	}

	// ── IA PRIORIZA TROPAS DÉBILES ────────────────────────────────────────
	private Node2D BuscarObjetivoDebilEnCarril(Node2D atacante, string grupo)
	{
		string carril = ((string)atacante.GetMeta("carril")).ToLower().Replace("modrival","").Replace("mod","");
		Node2D mejor = null; int min = int.MaxValue;
		foreach (Node n in GetTree().GetNodesInGroup(grupo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			if (((string)e.GetMeta("carril")).ToLower().Replace("modrival","").Replace("mod","") != carril) continue;
			int v = Gi(e, "vidaActual");
			if (v < min) { min = v; mejor = e; }
		}
		return mejor;
	}
}
