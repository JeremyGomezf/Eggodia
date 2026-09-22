using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Campo1 : Node2D
{
	// ══════════════════════════════════════════════════════════════════════
	// ARDID "NUCLEAR" — solo afecta al RIVAL de quien la lanza.
	//  1. Se suelta sobre el HUEVO rival (aro amarillo, igual que Robar). Cuesta 2 de energía.
	//  2. Contador de 45s para prepararse. NO bloquea nada: los dos siguen jugando normal.
	//     La carta recién aparece en la mano después de gastar 2 ardides en la partida (es la 3ª).
	//  3. En 0 la bomba aparece arriba, FUERA de cámara (como el misil del Granadero), y cae RÁPIDO
	//     con "cayendo" hasta el centro del campo (entre el carril 2 propio y el carril 2 rival).
	//  4. Al tocar el piso: "bomba_nuclear_explosion" (frames 0-27):
	//       · frame 26 → la pantalla se pone casi blanca;
	//       · frame 27 → 100% blanca (menos Sacrificar/Barajar/Ardid/Pausa/Tiempo). 300 de daño a
	//         las tropas del rival (salvo muro del Gólem / pre-defensa del Calamar; el Soldado Real
	//         en Parada se cubre y queda sin escudo). Las que mueren no hacen su derrota: desaparecen
	//         y en su lugar queda el polvo QUIETO. La mano del rival se vuelve negro mate y se va.
	//  5. Termina la explosión → la bomba desaparece y el blanco se disipa de a poco. Recién cuando
	//     no queda nada de blanco, el polvo reproduce su animación; al terminar, se libera el carril.
	//     A los 3s del impacto el rival recibe una mano nueva.
	// ══════════════════════════════════════════════════════════════════════

	private const string RUTA_BOMBA_NUCLEAR     = "res://efectos/nuclear_cartoon.tscn";
	private const string RUTA_POLVO_MUERTO      = "res://efectos/polvo_muerto.tscn";
	private const string ANIM_CAIDA_NUCLEAR     = "cayendo";
	private const string ANIM_EXPLOSION_NUCLEAR = "bomba_nuclear_explosion";
	private const string ANIM_POLVO             = "polvo_efecto";
	private const string META_MUERTE_NUCLEAR       = "muerte_nuclear";
	private const string META_MUERTE_PROCESADA     = "muerte_procesada"; // ver EjecutarMuerteTropaSacrificada

	private const int   COSTO_NUCLEAR           = 2;
	private const int   DAÑO_NUCLEAR            = 300;
	private const int   SEGUNDOS_CUENTA_NUCLEAR = 45;
	private const float DURACION_CAIDA_NUCLEAR  = 0.45f; // cae rápido, recién cuando el contador llega a 0
	private const float ALTURA_APARICION_BOMBA  = -900f; // misma altura "fuera de cámara" que el misil del Granadero
	private const int   FRAME_INICIO_DESTELLO   = 26;
	private const int   FRAME_BLANCO_TOTAL      = 27;
	private const float SEGUNDOS_BLANCO_TOTAL   = 0.3f;  // el blanco se sostiene un instante antes de irse
	private const float SEGUNDOS_DISIPAR_BLANCO = 1.6f;
	private const float SEGUNDOS_SIN_MANO       = 3f;
	private const int   COOLDOWN_NUCLEAR_CPU    = 6;     // cambios de turno (3 rondas)
	private const int   Z_CARRIL_2              = 50;    // ZIndex de las tropas de Mod2/ModRival2 (Mod3 = 100)

	// Los 5 elementos del HUD que quedan POR ENCIMA del destello blanco.
	private static readonly string[] HUD_SOBRE_DESTELLO =
		{ "SacrificioButton", "BarajarButton", "ArdidBarButton", "PausaButton", "TiempoPanel" };

	// Cada jugador puede usar SU Nuclear UNA sola vez por partida (que yo la use no le quita la suya al
	// rival). Lo que nunca pasa es que haya dos bombas a la vez (_nuclearEnCurso).
	private bool _nuclearUsadaJugador     = false;
	private bool _nuclearUsadaRival       = false;

	// La Nuclear no está disponible de entrada: cada jugador tiene que gastar antes 2 ardides en la
	// partida. Recién entonces aparece, como su 3er ardid (vale igual para el jugador, el bot y el online).
	private const int ARDIDES_PARA_NUCLEAR = 2;
	private int  _ardidesGastadosJugador  = 0;
	private int  _ardidesGastadosRival    = 0;
	private bool _nuclearYaOfrecida       = false; // la primera vez que se habilita, sale sí o sí

	private bool NuclearHabilitadaJugador => !_nuclearUsadaJugador && _ardidesGastadosJugador >= ARDIDES_PARA_NUCLEAR;
	// Puede haber DOS bombas en cuenta regresiva a la vez (una por bando). Lo que nunca se solapa es la
	// parte visual: mientras una está cayendo/explotando (_bombaEnVuelo), la otra cuenta se CONGELA y
	// sigue en cuanto termina, así los dos sprites no chocan.
	private int  _nuclearesEnCurso        = 0;
	private bool _bombaEnVuelo            = false;
	private bool _nuclearEnCurso => _nuclearesEnCurso > 0;
	private bool _impactoNuclearHecho     = false;
	private bool _destelloNuclearIniciado = false;
	private bool _destelloNuclearActivo   = false; // hay blanco en pantalla (el polvo espera quieto)
	private int  _polvosNuclearActivos    = 0;
	private int  _cooldownNuclearCPU      = 0;
	private readonly List<Action> _polvosEsperandoBlanco = new();

	private CanvasLayer _capaNuclear;     // cartas negras de MI mano (por encima del blanco)
	private ColorRect   _destelloNuclear; // vive dentro de la capa del HUD
	private readonly List<(Control nodo, int indice)> _hudSobreDestello = new();

	// Reparto posterior: lo que murió por la bomba no puede volver en la mano nueva.
	private readonly HashSet<int>    _indicesMuertosNuclearJugador = new();
	private readonly HashSet<string> _escenasMuertasNuclearRival   = new();
	private HashSet<int> _excluirRepartoNuclear;    // consultado por EsElegible (mano del jugador)
	private HashSet<int> _excluirRepartoNuclearCPU; // consultado por RellenarManoVisualCPUSiFalta

	// ── ONLINE ────────────────────────────────────────────────────────────
	// Como el contador no bloquea, la bomba puede caer en el turno de cualquiera de los dos. El daño
	// REAL lo aplica solo quien tiene el turno en ese momento (es quien manda la verdad del tablero);
	// el otro solo ve la secuencia y recibe el resultado con la acción "nuclear_impacto".
	private string _snapshotNuclearPendiente;        // resultado que llegó antes que MI explosión
	private bool   _impactoNuclearRemotoRecibido;    // el otro ya aplicó el daño de esta bomba
	private string _grupoVictimaNuclearPendiente;    // nadie lo aplicó todavía: lo aplico al empezar mi turno

	// ── LANZAMIENTO DEL JUGADOR ───────────────────────────────────────────
	private bool ResolverSueltaNuclearDesdeCarta(int slotIdx, int pi)
	{
		Vector2 mouseMundo = GetGlobalMousePosition();
		bool sobreHuevoRival = tronoRival != null && IsInstanceValid(tronoRival)
			&& tronoRival.GlobalPosition.DistanceTo(mouseMundo) < 160f;
		if (!sobreHuevoRival) return false;
		// Se puede lanzar aunque el rival tenga su cuenta corriendo (cada uno ve la suya en su panel).
		if (_nuclearUsadaJugador) { MostrarAvisoNuclearYaUsada(); return false; }
		if (movimientosRestantes < COSTO_NUCLEAR)
		{
			MostrarAvisoEnergiaNuclear();
			return false;
		}

		_nuclearUsadaJugador = true; // antes de reponer la carta: la mano nueva ya no puede traer otra
		Preferencias.RegistrarUsoHechizo(_poolActivo[pi].Nombre);
		MarcarHechizoUsado(pi);
		_hechizoUsadoEsteTurno = true;
		AutoReemplazarHechizo(slotIdx);
		if (EsOnline)
			EmitirAccionOnline("hechizo", new Godot.Collections.Dictionary {
				{ "hechizoId", "nuclear" }, { "carrilObjetivo", "" } });

		_ = EjecutarSecuenciaNuclear(lanzaJugador: true);
		RegistrarGastoMovimiento(COSTO_NUCLEAR);
		return true;
	}

	private void MostrarAvisoEnergiaNuclear() =>
		MostrarAviso($"Nuclear necesita {COSTO_NUCLEAR} de energía", new Color(1f, 0.55f, 0.25f));

	private void MostrarAvisoNuclearYaUsada() =>
		MostrarAviso("Ya usaste tu bomba nuclear en esta partida", new Color(1f, 0.55f, 0.25f));

	private void MostrarAvisoBombaEnCamino() =>
		MostrarAviso("Ya hay una bomba en camino", new Color(1f, 0.55f, 0.25f));

	/// <summary>Gasta la Nuclear del bando que la lanzó (una por jugador y por partida). Si fue la mía y
	/// todavía tenía la carta en la mano de ardides, se cambia por otra (ya no sirve).</summary>
	private void MarcarNuclearUsada(bool lanzaJugador)
	{
		if (!lanzaJugador) { _nuclearUsadaRival = true; return; }
		_nuclearUsadaJugador = true;
		for (int slot = 0; slot < _tarjetasHechizoCarta.Length; slot++)
		{
			var carta = _tarjetasHechizoCarta[slot];
			int pi = _hechizosMano[slot];
			if (carta != null && IsInstanceValid(carta) && pi >= 0 && pi < _poolActivo.Length
				&& _poolActivo[pi].Id == "nuclear")
				AutoReemplazarHechizo(slot);
		}
	}

	// ── LANZAMIENTO DEL BOT ───────────────────────────────────────────────
	/// <summary>El bot tira la Nuclear cuando le conviene: el jugador tiene tropas que la bomba mataría
	/// o dañaría y no está en enfriamiento. Igual que al jugador, el contador no lo frena: sigue su
	/// turno con la energía que le queda.</summary>
	private bool CPUIntentarNuclear()
	{
		if (EsOnline || _faseApertura || juegoTerminado || _nuclearUsadaRival || _hechizoUsadoEsteTurno) return false;
		if (_ardidesGastadosRival < ARDIDES_PARA_NUCLEAR) return false; // antes tiene que gastar 2 ardides
		if (_cooldownNuclearCPU > 0 || movimientosRestantes < COSTO_NUCLEAR) return false;

		if (PuntajeNuclear("tropas_jugador") < 3) return false;
		int unoEn = _dificultadCPU == 0 ? 4 : _dificultadCPU == 1 ? 3 : 2;
		if (random.Next(unoEn) != 0) return false;

		_hechizoUsadoEsteTurno = true;
		_cooldownNuclearCPU    = COOLDOWN_NUCLEAR_CPU;
		movimientosRestantes  -= COSTO_NUCLEAR;
		ActualizarInterfaz();
		MostrarAviso("¡El rival lanzó una bomba NUCLEAR!", new Color(1f, 0.45f, 0.2f));
		_ = EjecutarSecuenciaNuclear(lanzaJugador: false);
		return true;
	}

	// Cuánto le "rinde" la bomba contra un bando: 3 por tropa que mataría, 1 por tropa que solo daña.
	private int PuntajeNuclear(string grupo)
	{
		int puntos = 0;
		foreach (Node n in GetTree().GetNodesInGroup(grupo))
		{
			if (n is not TropaBase t || !IsInstanceValid(t) || t.EstaMuerta || t.vidaActual <= 0 || EstaProtegidaDeNuclear(t)) continue;
			puntos += t.vidaActual <= DAÑO_NUCLEAR ? 3 : 1;
		}
		return puntos;
	}

	// ── SECUENCIA COMPLETA ────────────────────────────────────────────────
	/// <param name="lanzaJugador">true = la tiró el jugador de ESTA pantalla (la sufre el rival);
	/// false = la tiró el bot o el rival en línea (la sufro yo: mis tropas y mi mano).</param>
	private async Task EjecutarSecuenciaNuclear(bool lanzaJugador)
	{
		MarcarNuclearUsada(lanzaJugador);
		_nuclearesEnCurso++;
		_impactoNuclearHecho          = false;
		_destelloNuclearIniciado      = false;
		_impactoNuclearRemotoRecibido = false;
		_snapshotNuclearPendiente     = null;

		Node2D bomba = null;
		bool tableroBloqueado = false;
		try
		{
			// 1) Contador para prepararse (no bloquea nada).
			CrearCapaNuclear();
			for (int s = SEGUNDOS_CUENTA_NUCLEAR; s > 0; s--)
			{
				// Si la otra bomba está cayendo o explotando, esta cuenta se congela hasta que termine.
				while (_bombaEnVuelo) { if (!await EsperarNuclear(0.2)) return; }
				// Últimos 15s: el bando que VA A RECIBIR la bomba no pierde la guardia por un golpe
				// (salvo que le bajen el escudo a 0). El que la lanzó no tiene esa ventaja.
				GuardiaNuclearActiva = s <= SEGUNDOS_GUARDIA_NUCLEAR;
				GrupoGuardiaNuclear  = lanzaJugador ? "tropas_rival" : "tropas_jugador";
				ActualizarCuentaNuclear(s, esMia: lanzaJugador);
				if (!await EsperarNuclear(1.0)) return;
				if (juegoTerminado) return;
			}
			OcultarCuentaNuclear();

			while (_bombaEnVuelo) { if (!await EsperarNuclear(0.2)) return; } // espera su turno de caer
			_bombaEnVuelo = true;

			// 2) Recién ahora aparece la bomba y cae rápido. Desde acá hasta que se va el blanco, la IA
			//    espera (no ataca bajo la pantalla blanca).
			IniciarBloqueoTablero();
			tableroBloqueado = true;
			bomba = CrearBombaNuclear(out Tween caida);
			if (caida != null && caida.IsValid()) await ToSignal(caida, Tween.SignalName.Finished);
			if (!IsInstanceValid(this) || juegoTerminado) return;

			// 3) Explosión + blanco + impacto.
			await ExplotarBombaNuclear(bomba, lanzaJugador);
			if (!IsInstanceValid(this)) return;
			GuardiaNuclearActiva = false; // la bomba ya cayó: vuelve la regla normal de la defensa
			if (IsInstanceValid(bomba)) bomba.QueueFree(); // la bomba ya cumplió: desaparece

			// 4) El blanco se sostiene un instante y se va de a poco.
			if (!await EsperarNuclear(SEGUNDOS_BLANCO_TOTAL)) return;
			DisiparDestelloNuclear(SEGUNDOS_DISIPAR_BLANCO);

			// 5) Mano nueva para quien la sufrió, a los 3s del impacto.
			if (!await EsperarNuclear(SEGUNDOS_SIN_MANO - SEGUNDOS_BLANCO_TOTAL)) return;
			if (!juegoTerminado)
			{
				if (!lanzaJugador)  RepartirManoNuevaJugador();
				else if (!EsOnline) RepartirManoNuevaCPU();
			}

			// Para esta altura el blanco ya se fue: el tablero vuelve a estar libre.
			for (double t = 0; _destelloNuclearActivo && t < 3.0; t += 0.1)
				if (!await EsperarNuclear(0.1)) return;
			FinalizarBloqueoTablero();
			tableroBloqueado = false;
			_bombaEnVuelo = false; // ya no hay nada volando: la otra cuenta (si hay) sigue

			// Los polvos siguen solos (cada uno libera su carril al terminar); se espera a que terminen
			// solo para no permitir otra Nuclear mientras quedan restos de esta.
			for (double t = 0; _polvosNuclearActivos > 0 && t < 8.0; t += 0.1)
				if (!await EsperarNuclear(0.1)) return;
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				if (tableroBloqueado) FinalizarBloqueoTablero();
				_bombaEnVuelo = false;
				TerminarSecuenciaNuclear(bomba);
			}
		}
	}

	private void TerminarSecuenciaNuclear(Node2D bomba)
	{
		if (IsInstanceValid(bomba)) bomba.QueueFree();
		QuitarDestelloNuclear();
		if (_capaNuclear != null && IsInstanceValid(_capaNuclear)) _capaNuclear.QueueFree();
		_capaNuclear      = null;
		OcultarCuentaNuclear();
		GuardiaNuclearActiva = false;
		_nuclearesEnCurso = Math.Max(0, _nuclearesEnCurso - 1);
		ActualizarInterfaz();
	}

	/// <summary>Espera respetando la pausa (el timer no corre con el juego pausado). Devuelve false si
	/// la escena ya no existe (salió al menú a mitad de la bomba).</summary>
	private async Task<bool> EsperarNuclear(double segundos)
	{
		if (!IsInstanceValid(this) || !IsInsideTree()) return false;
		if (segundos > 0)
			await ToSignal(GetTree().CreateTimer(segundos, false), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this) && IsInsideTree();
	}

	// ── BOMBA: APARECE FUERA DE CÁMARA Y CAE RÁPIDO ───────────────────────
	private Node2D CrearBombaNuclear(out Tween caida)
	{
		caida = null;
		if (!ResourceLoader.Exists(RUTA_BOMBA_NUCLEAR)) return null;
		var bomba = GD.Load<PackedScene>(RUTA_BOMBA_NUCLEAR)?.Instantiate<Node2D>();
		if (bomba == null) return null;
		if (bomba is Area2D area) { area.InputPickable = false; area.Monitoring = false; area.Monitorable = false; }

		// Aterriza en el centro del escenario: entre el carril 2 propio y el carril 2 rival.
		var slot2    = GetTree().Root.FindChild("Mod2", true, false) as Node2D;
		var slot2Riv = GetTree().Root.FindChild("ModRival2", true, false) as Node2D;
		Transform2D canvas = GetViewport().GetCanvasTransform();
		Rect2 visible = GetViewport().GetVisibleRect();
		Vector2 impacto = slot2 != null && slot2Riv != null
			? (slot2.GlobalPosition + slot2Riv.GlobalPosition) / 2f
			: canvas.AffineInverse() * visible.GetCenter();

		// Aparece justo arriba del punto de impacto, fuera de lo que ve la Camera2D (igual que el misil
		// del Granadero). Por si la cámara mostrara más arriba de -900, se toma el borde superior real
		// de la pantalla pasado al mundo, con margen.
		float bordeSuperiorMundo = (canvas.AffineInverse() * visible.Position).Y;
		float yInicio = Mathf.Min(ALTURA_APARICION_BOMBA, bordeSuperiorMundo - 400f);

		AddChild(bomba);
		bomba.GlobalPosition = new Vector2(impacto.X, yInicio);
		// Visualmente pertenece al carril 2 (ZIndex 50): va por delante de las tropas del carril 1 y 2,
		// pero POR DETRÁS de las del carril 3 (ZIndex 100), así no se ve atravesándolas al caer.
		bomba.ZIndex = Z_CARRIL_2 + 1;

		var anim = bomba.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (anim?.SpriteFrames != null && anim.SpriteFrames.HasAnimation(ANIM_CAIDA_NUCLEAR)) anim.Play(ANIM_CAIDA_NUCLEAR);

		caida = bomba.CreateTween();
		caida.TweenProperty(bomba, "global_position", impacto, DURACION_CAIDA_NUCLEAR)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		return bomba;
	}

	// ── BOMBA: EXPLOSIÓN ──────────────────────────────────────────────────
	private async Task ExplotarBombaNuclear(Node2D bomba, bool lanzaJugador)
	{
		var anim = IsInstanceValid(bomba) ? bomba.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D") : null;
		if (anim?.SpriteFrames != null && anim.SpriteFrames.HasAnimation(ANIM_EXPLOSION_NUCLEAR))
		{
			anim.FrameChanged += () =>
			{
				if (!IsInstanceValid(anim) || anim.Animation != ANIM_EXPLOSION_NUCLEAR) return;
				if (anim.Frame >= FRAME_INICIO_DESTELLO) IniciarDestelloNuclear();
				if (anim.Frame >= FRAME_BLANCO_TOTAL)
				{
					PonerPantallaBlancaNuclear();
					ImpactoNuclear(lanzaJugador);
				}
			};
			anim.Play(ANIM_EXPLOSION_NUCLEAR);
			anim.Frame = 0;
			anim.FrameProgress = 0f;
			await ToSignal(anim, AnimatedSprite2D.SignalName.AnimationFinished);
			if (!IsInstanceValid(this)) return;
		}

		// Red de seguridad (sin escena/animación, o si algún frame se saltó): nunca queda sin impacto.
		IniciarDestelloNuclear();
		PonerPantallaBlancaNuclear();
		ImpactoNuclear(lanzaJugador);
	}

	private void ImpactoNuclear(bool lanzaJugador)
	{
		if (_impactoNuclearHecho) return;
		_impactoNuclearHecho = true;


		// La sufre SOLO el rival de quien la lanzó: sus tropas y su mano.
		string grupoVictima = lanzaJugador ? "tropas_rival" : "tropas_jugador";
		DestruirManoPorNuclear(lanzaJugador);

		if (!EsOnline)
		{
			AplicarDañoNuclear(grupoVictima);
			return;
		}

		// ONLINE
		if (_impactoNuclearRemotoRecibido)
		{
			// El otro ya aplicó el daño: solo falta mostrar su resultado (si no lo pisó otra jugada).
			if (_snapshotNuclearPendiente != null)
			{
				string foto = _snapshotNuclearPendiente;
				_snapshotNuclearPendiente = null;
				ReconciliarLigero(foto);
			}
		}
		else if (esTurnoJugador)
		{
			// Tengo el turno: yo mando la verdad del tablero → aplico el daño y aviso al rival.
			AplicarDañoNuclear(grupoVictima);
			_grupoVictimaNuclearPendiente = null;
			EmitirAccionOnline("nuclear_impacto");
		}
		else
		{
			// Turno del otro: él aplica el daño. Si por un cruce de turnos nadie lo aplica, lo hago yo
			// apenas empiece mi turno (ver AplicarNuclearPendienteAlEmpezarTurno).
			_grupoVictimaNuclearPendiente = grupoVictima;
		}
	}

	/// <summary>Online: llegó "nuclear_impacto" del rival (ya aplicó el daño). Si mi explosión todavía no
	/// llegó al blanco, guardo su foto para mostrarla en ese instante. Devuelve true si la guardó.</summary>
	private bool RecibirImpactoNuclearOnline(string foto)
	{
		_impactoNuclearRemotoRecibido = true;
		_grupoVictimaNuclearPendiente = null;
		if (!_nuclearEnCurso || _impactoNuclearHecho || string.IsNullOrEmpty(foto)) return false;
		_snapshotNuclearPendiente = foto;
		return true;
	}

	/// <summary>Online: cualquier jugada posterior del rival trae una foto más nueva, que ya incluye el
	/// resultado de la bomba: la guardada deja de hacer falta.</summary>
	private void DescartarSnapshotNuclearPendiente() => _snapshotNuclearPendiente = null;

	/// <summary>Online: la bomba cayó cuando no era mi turno y el rival nunca mandó su resultado (pasó
	/// el turno justo antes de que cayera). Como ahora el turno es mío, lo aplico yo.</summary>
	private void AplicarNuclearPendienteAlEmpezarTurno()
	{
		if (_grupoVictimaNuclearPendiente == null || juegoTerminado) return;
		string grupo = _grupoVictimaNuclearPendiente;
		_grupoVictimaNuclearPendiente = null;
		AplicarDañoNuclear(grupo);
		EmitirAccionOnline("nuclear_impacto");
	}

	// ── DAÑO (solo al bando que la sufre) ─────────────────────────────────
	private void AplicarDañoNuclear(string grupoVictima)
	{
		_indicesMuertosNuclearJugador.Clear();
		_escenasMuertasNuclearRival.Clear();

		var objetivos = new List<TropaBase>();
		foreach (Node n in GetTree().GetNodesInGroup(grupoVictima))
			if (n is TropaBase t && IsInstanceValid(t) && !t.IsQueuedForDeletion() && !t.EstaMuerta && t.vidaActual > 0)
				objetivos.Add(t);

		foreach (TropaBase t in objetivos)
		{
			// Pudo morir en el medio (p. ej. por la muerte de otra tropa de la lista).
			if (!IsInstanceValid(t) || t.IsQueuedForDeletion() || t.EstaMuerta || t.vidaActual <= 0) continue;
			if (EstaProtegidaDeNuclear(t)) continue;
			if (t is SoldadoRealPrime soldado && soldado.CubrirseDeNuclear()) continue;

			t.SetMeta(META_MUERTE_NUCLEAR, true);
			t.RecibirDaño(DAÑO_NUCLEAR);
			if (!IsInstanceValid(t)) continue;
			MostrarDañoFlotante(t.GlobalPosition, DAÑO_NUCLEAR);

			if (t.vidaActual <= 0 && t is KaBarCartoonPrime kabar)
			{
				// Ka-Bar tiene su propia muerte (fantasma): también se va en polvo, pero al terminar el
				// polvo no se borra, aparece su fantasma como siempre.
				ReemplazarPorPolvoNuclear(kabar, liberarCarril: true, despuesDelPolvo: kabar.AparecerComoFantasmaTrasPolvo);
			}
			else if (t.vidaActual <= 0)
			{
				// Hay tropas (Granadero) que recién avisan su muerte al TERMINAR su propia animación de
				// derrota: bajo la bomba se procesa ya mismo, para que desaparezcan en el blanco y quede
				// el polvo (el aviso tardío después se ignora, ver EjecutarMuerteTropaSacrificada).
				if (!t.HasMeta(META_MUERTE_PROCESADA)) EjecutarMuerteTropaSacrificada(t);
			}
			else t.RemoveMeta(META_MUERTE_NUCLEAR);
		}

		CheckEstadoJuego();
		ActualizarInterfaz();
	}

	/// <summary>Cubiertas de la bomba: el Calamar con su pre-defensa de tentáculos activa, y cualquier
	/// tropa con un muro del Gólem de su mismo bando delante (mismo carril).</summary>
	private bool EstaProtegidaDeNuclear(TropaBase t)
	{
		if (t.HasMeta("en_postura_permanente")) return true;
		if (!t.HasMeta("carril")) return false;
		string grupoMuro = t.IsInGroup("tropas_jugador") ? "muros_jugador" : "muros_rival";
		string carril = NumeroDeCarril((string)t.GetMeta("carril"));
		foreach (Node n in GetTree().GetNodesInGroup(grupoMuro))
			if (n is Node2D m && IsInstanceValid(m) && m.HasMeta("carril")
				&& NumeroDeCarril((string)m.GetMeta("carril")) == carril)
				return true;
		return false;
	}

	private static string NumeroDeCarril(string carril) =>
		carril.ToLower().Replace("modrival", "").Replace("mod", "").Trim();

	/// <summary>La tropa muerta por la bomba desaparece (sin su animación de derrota) y en su lugar, sobre
	/// su "efecto_secundario_slot", queda el polvo. Mientras haya blanco en pantalla el polvo queda
	/// QUIETO; cuando el blanco se fue del todo reproduce su animación, y al terminar se borra la tropa
	/// y (si <paramref name="liberarCarril"/>) se habilita su carril.</summary>
	/// <param name="despuesDelPolvo">Si se pasa, al terminar el polvo se ejecuta esto EN LUGAR de borrar
	/// la tropa (Ka-Bar: aparece su fantasma).</param>
	private void ReemplazarPorPolvoNuclear(Node2D tropa, bool liberarCarril, Action despuesDelPolvo = null)
	{
		if (!IsInstanceValid(tropa)) return;
		// El bando se saca del carril (Ka-Bar ya se quitó de su grupo al morir).
		bool esDelJugador = tropa.HasMeta("carril")
			? !((string)tropa.GetMeta("carril")).StartsWith("ModRival")
			: tropa.IsInGroup("tropas_jugador");
		if (esDelJugador && tropa.HasMeta("idx_mazo"))
			_indicesMuertosNuclearJugador.Add((int)tropa.GetMeta("idx_mazo"));
		else if (!esDelJugador && !string.IsNullOrEmpty(tropa.SceneFilePath))
			_escenasMuertasNuclearRival.Add(tropa.SceneFilePath);

		Vector2 pos = tropa is TropaBase tb ? tb.ObtenerSlotEfectoSecundario() : tropa.GlobalPosition;
		int zIndex  = tropa.ZIndex;
		tropa.Visible = false;
		_polvosNuclearActivos++;

		bool terminado = false;
		void Terminar()
		{
			if (terminado) return;
			terminado = true;
			_polvosNuclearActivos = Math.Max(0, _polvosNuclearActivos - 1);
			if (!IsInstanceValid(tropa)) return;
			// Recién ahora, con el polvo terminado, el círculo del carril vuelve a aparecer.
			if (liberarCarril) TropaBase.LiberarCarrilDe(tropa);
			if (despuesDelPolvo != null) despuesDelPolvo();
			else tropa.QueueFree();
		}

		Node2D polvo = ResourceLoader.Exists(RUTA_POLVO_MUERTO)
			? GD.Load<PackedScene>(RUTA_POLVO_MUERTO)?.Instantiate<Node2D>() : null;
		var anim = polvo?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (polvo == null || anim?.SpriteFrames == null || !anim.SpriteFrames.HasAnimation(ANIM_POLVO))
		{
			polvo?.QueueFree();
			Terminar();
			return;
		}

		if (polvo is Area2D area) { area.InputPickable = false; area.Monitoring = false; area.Monitorable = false; }
		// Las tropas grandes dejan una nube más grande (sin exagerar); el desfase del .tscn se escala
		// igual para que la nube siga apoyada en el mismo lugar.
		float escalaPolvo = EsTropaGrandeParaPolvo(tropa) ? ESCALA_POLVO_GRANDE : 1f;
		Vector2 desfaseEscena = polvo.Position * escalaPolvo; // ajuste propio del .tscn respecto del slot
		AddChild(polvo);
		polvo.Scale = polvo.Scale * escalaPolvo;
		polvo.GlobalPosition = pos + desfaseEscena;
		polvo.ZIndex = zIndex;
		// Del lado rival el polvo se ve espejado (hacia el lado opuesto), igual que el resto de efectos.
		if (!esDelJugador) AsegurarOrientacionRival(polvo);

		// Quieto en su primer frame hasta que se vaya el blanco.
		anim.Animation = ANIM_POLVO;
		anim.Frame = 0;
		anim.Stop();

		anim.AnimationFinished += () =>
		{
			Terminar();
			if (!IsInstanceValid(polvo)) return;
			Tween tw = polvo.CreateTween();
			tw.TweenProperty(polvo, "modulate:a", 0f, 0.25f);
			tw.Finished += () => { if (IsInstanceValid(polvo)) polvo.QueueFree(); };
		};

		void Reproducir()
		{
			if (!IsInstanceValid(anim)) { Terminar(); return; }
			anim.Play(ANIM_POLVO);
			anim.Frame = 0;
			// Red de seguridad: si por algo el polvo nunca termina, el carril se libera igual.
			GetTree().CreateTimer(6.0, false).Timeout += () =>
			{
				Terminar();
				if (IsInstanceValid(polvo)) polvo.QueueFree();
			};
		}

		if (_destelloNuclearActivo) _polvosEsperandoBlanco.Add(Reproducir);
		else Reproducir();
	}

	// ── SONIDO: BOOM ATÓMICO (2s) ─────────────────────────────────────────
	// Si existe un audio propio en esta ruta (.wav u .ogg) se usa ese; si no, se genera uno por código
	// (igual que el clic de los botones en GlobalAudioManager), así funciona sin ningún archivo.
	private const string RUTA_SONIDO_NUCLEAR = "res://efectos/sonidos/explosion_nuclear";
	private const float  SEGUNDOS_SONIDO_NUCLEAR = 2f;
	private static AudioStream _sonidoNuclearCache;

	private void ReproducirSonidoNuclear()
	{
		_sonidoNuclearCache ??= CargarSonidoNuclear();
		var player = new AudioStreamPlayer { Stream = _sonidoNuclearCache, VolumeDb = -1f };
		AddChild(player);
		player.Finished += () => { if (IsInstanceValid(player)) player.QueueFree(); };
		player.Play();
	}

	private static AudioStream CargarSonidoNuclear()
	{
		foreach (string ext in new[] { ".wav", ".ogg" })
			if (ResourceLoader.Exists(RUTA_SONIDO_NUCLEAR + ext))
				return GD.Load<AudioStream>(RUTA_SONIDO_NUCLEAR + ext);
		return GenerarSonidoNuclear();
	}

	/// <summary>Explosión atómica sintetizada: golpe grave que cae de tono (el "boom"), un chasquido
	/// de estallido al inicio y un retumbo de ruido grave que se va apagando en 2 segundos.</summary>
	private static AudioStreamWav GenerarSonidoNuclear()
	{
		const int MUESTREO = 22050;
		int total = (int)(MUESTREO * SEGUNDOS_SONIDO_NUCLEAR);
		var datos = new byte[total * 2];
		var rng = new Random(1945);
		float ruidoGrave = 0f, faseBoom = 0f;

		for (int i = 0; i < total; i++)
		{
			float t = (float)i / MUESTREO;
			float blanco = (float)(rng.NextDouble() * 2.0 - 1.0);
			ruidoGrave = (ruidoGrave + 0.03f * blanco) / 1.03f; // ruido "marrón": grave, tipo retumbo

			// Boom: onda grave que baja de 75 Hz a 28 Hz.
			float frecuencia = 28f + 47f * Mathf.Exp(-t * 3f);
			faseBoom += Mathf.Tau * frecuencia / MUESTREO;
			float boom = Mathf.Sin(faseBoom) * Mathf.Exp(-t * 2.2f);

			float estallido = blanco * Mathf.Exp(-t * 22f);                    // chasquido inicial
			float ataque    = Mathf.Min(1f, t / 0.015f);
			float retumbo   = ruidoGrave * 9f * ataque * Mathf.Exp(-t * 1.5f); // cola que se apaga

			float muestra = 0.6f * boom + 0.45f * retumbo + 0.35f * estallido;
			muestra = Mathf.Tanh(muestra * 1.8f);                              // saturación suave, más "pesado"
			float cola = SEGUNDOS_SONIDO_NUCLEAR - t;
			if (cola < 0.3f) muestra *= cola / 0.3f;                           // termina sin corte seco

			short pcm = (short)(Mathf.Clamp(muestra, -1f, 1f) * short.MaxValue * 0.92f);
			datos[i * 2]     = (byte)(pcm & 0xFF);
			datos[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
		}

		return new AudioStreamWav
		{
			Format  = AudioStreamWav.FormatEnum.Format16Bits,
			MixRate = MUESTREO,
			Stereo  = false,
			Data    = datos,
		};
	}

	// ── SACUDONES DE PANTALLA ─────────────────────────────────────────────
	// Mueven el campo y TAMBIÉN los botones del HUD (su capa no la mueve la cámara, por eso se sacude
	// aparte), de lado a lado y cada vez más suave hasta quedar quietos. Solo con la vibración de
	// pantalla activada en Ajustes.
	private const float SEGUNDOS_SACUDON_NUCLEAR = 1f;    // golpe duro, al quedar la pantalla en blanco
	private const float FUERZA_SACUDON_MUNDO     = 42f;   // píxeles
	private const float FUERZA_SACUDON_BOTONES   = 30f;
	private const float SEGUNDOS_SACUDON_LEVE    = 0.7f;  // impacto de misil (Tanque, Granadero)
	private const float FUERZA_LEVE_MUNDO        = 18f;
	private const float FUERZA_LEVE_BOTONES      = 12f;

	private bool    _sacudiendo = false;
	private float   _fuerzaSacudonActual;
	private Vector2 _origenSacudonMundo, _origenSacudonHud;
	private Tween   _twSacudonMundo, _twSacudonHud;

	private void SacudonNuclear() => Sacudir(SEGUNDOS_SACUDON_NUCLEAR, FUERZA_SACUDON_MUNDO, FUERZA_SACUDON_BOTONES);

	/// <summary>Sacudida de misil (más suave que la de la bomba): la usan los misiles del Tanque y del Granadero al
	/// impactar (se llama por Call desde sus scripts).</summary>
	public void SacudonLeve() => Sacudir(SEGUNDOS_SACUDON_LEVE, FUERZA_LEVE_MUNDO, FUERZA_LEVE_BOTONES);

	private void Sacudir(float segundos, float fuerzaMundo, float fuerzaBotones)
	{
		if (!PanelSettings.ScreenShakeEnabled) return;
		var capaHud = CapaHUD();
		if (_sacudiendo)
		{
			// Uno más leve no corta a uno más fuerte (un misil no apaga el golpe de la bomba).
			if (fuerzaMundo < _fuerzaSacudonActual) return;
			// Se reemplaza el que estaba, partiendo del mismo origen (así el campo nunca queda corrido).
			_twSacudonMundo?.Kill();
			_twSacudonHud?.Kill();
		}
		else
		{
			_origenSacudonMundo = Position;
			_origenSacudonHud   = capaHud.Offset;
		}
		_sacudiendo = true;
		_fuerzaSacudonActual = fuerzaMundo;

		const int PASOS_POR_SEGUNDO = 25;
		int pasos = Mathf.Max(6, Mathf.RoundToInt(segundos * PASOS_POR_SEGUNDO));
		float paso = segundos / pasos;
		_twSacudonMundo = CreateTween();
		_twSacudonHud   = capaHud.CreateTween();
		for (int i = 0; i < pasos; i++)
		{
			float fuerza = 1f - (float)i / pasos;    // arranca fuerte y se va apagando
			float lado   = i % 2 == 0 ? 1f : -1f;    // de lado a lado
			_twSacudonMundo.TweenProperty(this, "position", _origenSacudonMundo + new Vector2(
				lado * fuerzaMundo * fuerza,
				(float)GD.RandRange(-0.4, 0.4) * fuerzaMundo * fuerza), paso);
			_twSacudonHud.TweenProperty(capaHud, "offset", _origenSacudonHud + new Vector2(
				lado * fuerzaBotones * fuerza,
				(float)GD.RandRange(-0.3, 0.3) * fuerzaBotones * fuerza), paso);
		}
		_twSacudonMundo.TweenProperty(this, "position", _origenSacudonMundo, paso);
		_twSacudonHud.TweenProperty(capaHud, "offset", _origenSacudonHud, paso);
		_twSacudonMundo.Finished += () => { _sacudiendo = false; _fuerzaSacudonActual = 0f; };
	}

	private const float ESCALA_POLVO_GRANDE = 1.5f;

	// Tanque, Dama, Calamar, Arfil, Paperex, Granadero, Gólem y Dragón: su polvo sale más grande.
	private static bool EsTropaGrandeParaPolvo(Node2D tropa) => tropa is TanqueCartoonPrime or DamaPrime
		or CalamarGPrime or ArfilPrime or TRexPrime or GranaderoCartoonPrime or GolemPrime or DragonPrime;

	// ── DESTELLO BLANCO ───────────────────────────────────────────────────
	// Va DENTRO de la capa del HUD (así tapa el campo, las tropas, las manos y el resto del HUD), y
	// los 5 elementos de HUD_SOBRE_DESTELLO se mueven por encima de él mientras dura.
	private void IniciarDestelloNuclear()
	{
		if (_destelloNuclearIniciado) return;
		_destelloNuclearIniciado = true;
		_destelloNuclearActivo   = true;
		// Frame 26: arrancan juntos el BOOM, la vibración y el blanco.
		ReproducirSonidoNuclear();
		SacudonNuclear();

		var capaHud = CapaHUD();
		_destelloNuclear = new ColorRect
		{
			Name        = "DestelloNuclear",
			Color       = Colors.White,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate    = new Color(1, 1, 1, 0),
		};
		capaHud.AddChild(_destelloNuclear);
		// AndOffsets: con SetAnchorsPreset solo, al llamarlo ya dentro del árbol Godot conserva el tamaño
		// actual (0x0) y el blanco no se ve.
		_destelloNuclear.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		// Se pasa un poco de los bordes: con el sacudón la capa del HUD se mueve de lado a lado y, sin
		// este margen, se vería una franja del juego en el costado.
		const float MARGEN = 80f;
		_destelloNuclear.OffsetLeft  = -MARGEN;
		_destelloNuclear.OffsetTop   = -MARGEN;
		_destelloNuclear.OffsetRight =  MARGEN;
		_destelloNuclear.OffsetBottom = MARGEN;

		_hudSobreDestello.Clear();
		foreach (string nombre in HUD_SOBRE_DESTELLO)
		{
			if (capaHud.GetNodeOrNull<Control>(nombre) is not Control c) continue;
			_hudSobreDestello.Add((c, c.GetIndex()));
			capaHud.MoveChild(c, -1);
		}

		// Frame 26: la pantalla se pone casi blanca en lo que dura ese frame (1/10.5 s).
		_destelloNuclear.CreateTween().TweenProperty(_destelloNuclear, "modulate:a", 0.85f, 0.09f);
	}

	// Frame 27: blanco total.
	private void PonerPantallaBlancaNuclear()
	{
		if (_destelloNuclear == null || !IsInstanceValid(_destelloNuclear)) return;
		_destelloNuclear.Modulate = Colors.White;
	}

	private void DisiparDestelloNuclear(float duracion)
	{
		if (_destelloNuclear == null || !IsInstanceValid(_destelloNuclear)) { QuitarDestelloNuclear(); return; }
		Tween tw = _destelloNuclear.CreateTween();
		tw.TweenProperty(_destelloNuclear, "modulate:a", 0f, duracion)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		tw.Finished += QuitarDestelloNuclear;
	}

	private void QuitarDestelloNuclear()
	{
		var capaHud = CapaHUD();
		if (_destelloNuclear != null && IsInstanceValid(_destelloNuclear))
		{
			_destelloNuclear.GetParent()?.RemoveChild(_destelloNuclear);
			_destelloNuclear.QueueFree();
		}
		_destelloNuclear = null;

		// Devuelve los 5 elementos a su orden original dentro del HUD.
		foreach (var (nodo, indice) in _hudSobreDestello.OrderBy(h => h.indice))
			if (IsInstanceValid(nodo) && nodo.GetParent() == capaHud)
				capaHud.MoveChild(nodo, Math.Min(indice, capaHud.GetChildCount() - 1));
		_hudSobreDestello.Clear();

		// Ya no queda blanco: recién ahora los polvos reproducen su animación.
		_destelloNuclearActivo = false;
		var pendientes = _polvosEsperandoBlanco.ToArray();
		_polvosEsperandoBlanco.Clear();
		foreach (Action reproducir in pendientes) reproducir();
	}

	// ── CONTADOR (en el AvisoArdidPanel de cada bando) ────────────────────
	// Capa propia SOLO para las cartas negras de MI mano (por encima del blanco, debajo de la pausa).
	private void CrearCapaNuclear()
	{
		if (_capaNuclear != null && IsInstanceValid(_capaNuclear)) _capaNuclear.QueueFree();
		_capaNuclear = new CanvasLayer { Name = "CapaNuclear", Layer = 5 };
		AddChild(_capaNuclear);
	}

	// La cuenta se muestra en el panel de avisos del bando que lanzó la bomba (el mío al lado de mi
	// barra de vida; el del rival, espejado del suyo). Ver PrepararAvisosDeBomba en Campo1.Extra.cs.
	private void ActualizarCuentaNuclear(int segundos, bool esMia) => MostrarCuentaBombaEnPanel(esMia, segundos);

	private void OcultarCuentaNuclear()
	{
		MostrarCuentaBombaEnPanel(true, 0);
		MostrarCuentaBombaEnPanel(false, 0);
	}

	// ── MANO DEL QUE LA SUFRE: NEGRO MATE → DESAPARECE → MANO NUEVA A LOS 3s ─
	// Las cartas negras solo se ven en la MANO MANUAL de quien sufre la bomba (desde su pantalla).
	// Quien la lanza no ve ninguna carta negra: al bot se le vacía la mano sin mostrar nada, y en línea
	// el rival ve lo suyo en su propia pantalla.
	private const float SEGUNDOS_CARTA_NEGRA  = 2.5f;  // la carta se queda en negro mate...
	private const float SEGUNDOS_DESVANECER_CARTA = 0.35f; // ...y después se desvanece rápido

	private void DestruirManoPorNuclear(bool lanzaJugador)
	{
		if (lanzaJugador)
		{
			if (!EsOnline) _manoVisualCPU.Clear(); // se le repone a los 3s (RepartirManoNuevaCPU)
			return;
		}

		if (contenedorMano == null) return;
		// Todas las cartas de tropa de la mano: las 3 normales y también la 4ª (la robada con "Robar").
		foreach (Node n in contenedorMano.GetChildren())
		{
			if (n is not Carta c || c.IsQueuedForDeletion()) continue;
			var sil = CrearSiluetaDeCarta(c);
			c.QueueFree();
			if (sil == null) continue;

			// Negro mate 2.5s y después se desvanece rápido hasta desaparecer.
			Tween tw = sil.CreateTween();
			tw.TweenInterval(SEGUNDOS_CARTA_NEGRA);
			tw.TweenProperty(sil, "modulate:a", 0f, SEGUNDOS_DESVANECER_CARTA).SetEase(Tween.EaseType.In);
			tw.Finished += () => { if (IsInstanceValid(sil)) sil.QueueFree(); };
		}
		// Si la 4ª era la carta robada, "Robar Carta" vuelve a quedar disponible.
		ActualizarEstadoCartaRobada();
	}

	// Copia exacta (forma, posición, giro y escala en pantalla) de una carta de MI mano, en negro,
	// dibujada por encima del destello blanco.
	private Control CrearSiluetaDeCarta(Carta carta)
	{
		if (_capaNuclear == null || !IsInstanceValid(_capaNuclear)) return null;
		var foto = carta.GetNodeOrNull<TextureRect>("foto");
		Control fuente = foto != null && foto.Texture != null ? foto : carta;
		Transform2D xf = fuente.GetGlobalTransformWithCanvas();

		Control sil;
		if (fuente == foto)
			sil = new TextureRect
			{
				Texture     = foto.Texture,
				ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			};
		else
		{
			var estilo = new StyleBoxFlat { BgColor = Colors.Black };
			estilo.SetCornerRadiusAll(14);
			var panel = new Panel();
			panel.AddThemeStyleboxOverride("panel", estilo);
			sil = panel;
		}

		sil.MouseFilter = Control.MouseFilterEnum.Ignore;
		sil.Modulate    = new Color(0f, 0f, 0f, 1f);
		_capaNuclear.AddChild(sil);
		sil.Size     = fuente.Size;
		sil.Position = xf.Origin;
		sil.Rotation = xf.Rotation;
		sil.Scale    = xf.Scale;
		return sil;
	}

	// Mano nueva del jugador: sin repetir las cartas de las tropas que tiene en sus carriles ni las que
	// murieron por la bomba (las especiales tampoco, aunque normalmente sí podrían repetirse).
	private void RepartirManoNuevaJugador()
	{
		var excluir = IndicesDesplegados();
		excluir.UnionWith(_indicesMuertosNuclearJugador);
		_excluirRepartoNuclear = excluir;
		try { RellenarManoObjetivo(); }
		finally { _excluirRepartoNuclear = null; }
	}

	// Mano nueva del bot, con la misma regla.
	private void RepartirManoNuevaCPU()
	{
		var excluir = new HashSet<int>();
		foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
			if (n is Node2D t && IsInstanceValid(t))
			{
				int i = Array.IndexOf(escenasTropas, t.SceneFilePath);
				if (i >= 0) excluir.Add(i);
			}
		foreach (string escena in _escenasMuertasNuclearRival)
		{
			int i = Array.IndexOf(escenasTropas, escena);
			if (i >= 0) excluir.Add(i);
		}
		_excluirRepartoNuclearCPU = excluir;
		try { RellenarManoVisualCPUSiFalta(); }
		finally { _excluirRepartoNuclearCPU = null; }
	}
}
