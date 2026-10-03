using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ══════════════════════════════════════════════════════════════════════
	// SISTEMA DE ROBO DE MANO POR TIPO
	// Reglas de diseño:
	//  • Hasta la ronda 3, la mano alterna "2 tácticos + 1 asesino" y "2 asesinos + 1 táctico". Desde
	//    la ronda 4 el reparto es libre (desordenado), con un tope en TODAS las rondas: como mucho 2
	//    cartas del mismo tipo (nunca 3 iguales) y como mucho 1 coloso (nunca 2).
	//  • Desde la ronda 3, una vez cada 3 rondas (3, 6, 9…) aparece un coloso (rotando: Paper-Rex,
	//    Tanque…). Si esa ronda no se pudo dar (mano llena, colosos en el campo), se da en el
	//    siguiente reparto: no se pierde. Nunca dos colosos juntos en la mano.
	//  • Una carta invocada NO vuelve a ofrecerse mientras su tropa siga viva en el campo (ninguna,
	//    tampoco las "especiales"), salvo que esa tropa ya haya sobrevivido 3 rondas: ahí su carta ya
	//    puede volver a salir. Tras morir, reaparece una ronda después.
	//  • Cartas especiales (Peón, Soldado Cartoon, Soldado Real, Ka-Bar): además, tras ofrecerse o
	//    morir esperan 2 rondas para volver.
	// ══════════════════════════════════════════════════════════════════════

	private const int RONDA_MINIMA_COLOSO  = 3;
	private const int PRIMERA_RONDA_REPARTO_LIBRE = 4; // desde acá la mano trae cualquier tipo

	/// <summary>Cuántas cartas de un tipo puede haber a la vez en una mano: 1 coloso, 2 de los demás.</summary>
	private static int MaximoEnMano(TipoTropa tipo) => tipo == TipoTropa.Coloso ? 1 : 2;

	/// <summary>¿Ya se llegó al tope de ese tipo en la mano del jugador?</summary>
	private bool TipoLlenoEnMano(TipoTropa tipo, HashSet<int> enMano)
	{
		if (ModoTutorial) return false; // el tutorial tiene su propio reparto fijo
		int n = 0;
		foreach (int i in enMano)
			if (i >= 0 && i < _tipoIndice.Length && _tipoIndice[i] == tipo) n++;
		return n >= MaximoEnMano(tipo);
	}
	private int _manosRepartidas        = 0;
	// Último "ciclo" de 3 rondas (ronda / 3) en el que ya se te dio un coloso.
	private int _cicloColosoEntregado   = 0;

	/// <summary>Ronda en la que vas a USAR la mano que se está repartiendo. La mano se repone a los 2 s
	/// de empezar el turno del RIVAL, para tu turno siguiente: contar la ronda actual (como antes)
	/// atrasaba todo una ronda, y el coloso de la ronda 3 recién llegaba en la 4.</summary>
	private int RondaDeLaManoJugador() => _turnosJugados / 2 + (esTurnoJugador ? 1 : 2);

	/// <summary>¿Ya hay un coloso esperando en la mano? Solo se permite uno a la vez.</summary>
	private bool HayColosoEnMano()
	{
		if (contenedorMano == null || _tipoIndice == null) return false;
		foreach (Node n in contenedorMano.GetChildren())
			if (n is Carta c && c.EstaEnMano && !c.IsQueuedForDeletion()
				&& c.IdCarta >= 0 && c.IdCarta < _tipoIndice.Length && _tipoIndice[c.IdCarta] == TipoTropa.Coloso)
				return true;
		return false;
	}

	private const int COOLDOWN_NORMAL   = 1; // rondas para reaparecer tras morir
	private const int COOLDOWN_ESPECIAL = 2; // cadencia fija de reaparición

	private TipoTropa[] _tipoIndice;
	private bool[]      _esEspecialIndice;
	private int[]       _cooldownIndice;
	private List<int>   _idxTactico = new();
	private List<int>   _idxAsesino = new();
	private List<int>   _idxColoso  = new();
	private List<int>   _colosoRotacion = new();

	// Mazo del CPU (rival): 3 tácticos, 3 asesinos + 2 colosos aparte.
	private List<string> _mazoCPU    = new();
	private List<string> _colososCPU = new();
	private bool _cpuColosoPendiente = false;

	// Catálogo COMPLETO de las 17 tropas, fijo — el CPU arma su mazo de acá, no de "escenasTropas"
	// (que para cuando corre InicializarMazoCPU ya fue reemplazado por TU mazo de 8 cartas, ver
	// Campo1.cs _Ready). Antes el bot terminaba con exactamente tu mismo mazo por eso. El CPU
	// también ignora a propósito cualquier candado de tienda/desbloqueo — tiene acceso a las 17
	// tropas y los 8 hechizos siempre, para que tenga más variedad que vos y no sea un espejo.
	private static readonly string[] TODAS_LAS_TROPAS_CPU = {
		"res://cartas prime/MEDIEVAL/Dragon_prime.tscn",   "res://cartas prime/MEDIEVAL/Golem_prime.tscn",
		"res://cartas prime/MEDIEVAL/Maguin_prime.tscn",   "res://cartas prime/MEDIEVAL/SoldadoReal_prime.tscn",
		"res://cartas prime/PAPEL/Paper_Rex.tscn",      "res://cartas prime/PACIFICO/Tiburon_prime.tscn",
		"res://cartas prime/AJEDREZ/Peon_prime.tscn",  "res://cartas prime/TOONS/Tanque_cartoon_prime.tscn",
		"res://cartas prime/PACIFICO/CalamarG_prime.tscn", "res://cartas prime/AJEDREZ/Caballo_prime.tscn",
		"res://cartas prime/AJEDREZ/Dama_prime.tscn",      "res://cartas prime/AJEDREZ/Torre_prime.tscn",
		"res://cartas prime/TOONS/Soldado_cartoon_prime.tscn",
		"res://cartas prime/TOONS/Campero_cartoon_prime.tscn",
		"res://cartas prime/AJEDREZ/Arfil_prime.tscn", "res://cartas prime/TOONS/Ka-Bar_cartoon_prime.tscn",
		"res://cartas prime/TOONS/Granadero_cartoon_prime.tscn",
		"res://cartas prime/MEDIEVAL/Machi_prime.tscn"
	};

	// ── INICIALIZACIÓN ────────────────────────────────────────────────────
	private void InicializarClasificacionMazo()
	{
		int n = escenasTropas.Length;
		_tipoIndice       = new TipoTropa[n];
		_esEspecialIndice = new bool[n];
		_cooldownIndice   = new int[n];
		_idxTactico.Clear(); _idxAsesino.Clear(); _idxColoso.Clear();

		for (int i = 0; i < n; i++)
		{
			var info = ClasificacionCartas.Clasificar(escenasTropas[i]);
			_tipoIndice[i] = info.Tipo;
			string norm = ClasificacionCartas.Normalizar(escenasTropas[i]);
			_esEspecialIndice[i] = norm.Contains("peon") || norm.Contains("soldadocartoon")
								|| norm.Contains("soldadoreal") || norm.Contains("kabar");
			switch (info.Tipo)
			{
				case TipoTropa.Tactico: _idxTactico.Add(i); break;
				case TipoTropa.Asesino: _idxAsesino.Add(i); break;
				case TipoTropa.Coloso:  _idxColoso.Add(i);  break;
			}
		}
		_colosoRotacion = OrdenarColosos(_idxColoso);
	}

	// Orden preferido de aparición de colosos: Paper-Rex, Tanque, Gólem, Calamar, Torre.
	private List<int> OrdenarColosos(List<int> colosos)
	{
		string[] pref = { "rex", "tanque", "golem", "calamar", "torre" };
		var res = new List<int>();
		foreach (string k in pref)
			foreach (int i in colosos)
				if (!res.Contains(i) && ClasificacionCartas.Normalizar(escenasTropas[i]).Contains(k))
					res.Add(i);
		foreach (int i in colosos) if (!res.Contains(i)) res.Add(i);
		return res;
	}

	// ── CARTAS DEL RIVAL, SEPARADAS DE LAS MÍAS ───────────────────────────
	// La mano visible del bot (la que se ve en "Robar Carta") se indexaba sobre escenasTropas, que es
	// MI mazo: por eso al rival le salían siempre MIS mismas 8 cartas. Ahora se indexa sobre este
	// arreglo, que son las 18 tropas del juego, y el bot reparte SOLO desde el mazo que le tocó a él.
	private string[]  _cartasCPU;        // espacio de índices propio del rival (las 18 tropas)
	private TipoTropa[] _tipoIndiceCPU;  // tipo de cada carta de _cartasCPU
	private List<int> _idxTacticoCPU = new(), _idxAsesinoCPU = new(), _idxColosoCPU = new();

	/// <summary>Índice dentro de _cartasCPU de una escena concreta (-1 si no está).</summary>
	private int IndiceCPUDe(string rutaEscena) =>
		_cartasCPU == null || string.IsNullOrEmpty(rutaEscena) ? -1 : Array.IndexOf(_cartasCPU, rutaEscena);

	/// <summary>Clasifica el mazo QUE LE TOCÓ AL BOT dentro de su propio espacio de índices. Solo
	/// entran sus cartas, así que su mano nunca puede repetir el mazo del jugador salvo coincidencia.</summary>
	private void InicializarClasificacionCPU()
	{
		_cartasCPU = TODAS_LAS_TROPAS_CPU;
		_tipoIndiceCPU = new TipoTropa[_cartasCPU.Length];
		for (int i = 0; i < _cartasCPU.Length; i++)
			_tipoIndiceCPU[i] = ClasificacionCartas.TipoDe(_cartasCPU[i]);

		_idxTacticoCPU.Clear(); _idxAsesinoCPU.Clear(); _idxColosoCPU.Clear();
		foreach (string ruta in _mazoCPU)
		{
			int i = IndiceCPUDe(ruta);
			if (i < 0) continue;
			if (_tipoIndiceCPU[i] == TipoTropa.Tactico) _idxTacticoCPU.Add(i);
			else if (_tipoIndiceCPU[i] == TipoTropa.Asesino) _idxAsesinoCPU.Add(i);
		}
		foreach (string ruta in _colososCPU)
		{
			int i = IndiceCPUDe(ruta);
			if (i >= 0) _idxColosoCPU.Add(i);
		}
	}

	private void InicializarMazoCPU()
	{
		_mazoCPU.Clear(); _colososCPU.Clear();
		var tac = new List<string>(); var ase = new List<string>(); var col = new List<string>();
		foreach (string ruta in TODAS_LAS_TROPAS_CPU)
		{
			switch (ClasificacionCartas.TipoDe(ruta))
			{
				case TipoTropa.Tactico: tac.Add(ruta); break;
				case TipoTropa.Asesino: ase.Add(ruta); break;
				case TipoTropa.Coloso:  col.Add(ruta); break;
			}
		}
		BarajarLista(tac); BarajarLista(ase); BarajarLista(col);
		TomarHasta(_mazoCPU, tac, 3);
		TomarHasta(_mazoCPU, ase, 3);
		TomarHasta(_colososCPU, col, 2);
		// Red de seguridad: si el pool no tuvo suficientes de un tipo, completa con lo que haya.
		if (_mazoCPU.Count == 0)
			foreach (string r in TODAS_LAS_TROPAS_CPU) if (ClasificacionCartas.TipoDe(r) != TipoTropa.Coloso) _mazoCPU.Add(r);
		if (_colososCPU.Count == 0)
			foreach (string r in TODAS_LAS_TROPAS_CPU) if (ClasificacionCartas.TipoDe(r) == TipoTropa.Coloso) _colososCPU.Add(r);

		InicializarClasificacionCPU(); // su mano visible se reparte desde ESTE mazo, no desde el mío
	}

	private void BarajarLista(List<string> l)
	{
		for (int i = 0; i < l.Count; i++) { int r = random.Next(i, l.Count); (l[i], l[r]) = (l[r], l[i]); }
	}

	private static void TomarHasta(List<string> destino, List<string> fuente, int cantidad)
	{
		for (int i = 0; i < cantidad && i < fuente.Count; i++) destino.Add(fuente[i]);
	}

	// ── ESTADO DINÁMICO ───────────────────────────────────────────────────
	private HashSet<int> IndicesEnMano()
	{
		var s = new HashSet<int>();
		if (contenedorMano != null)
			foreach (Node n in contenedorMano.GetChildren())
				if (n is Carta c && !c.IsQueuedForDeletion()) s.Add(c.IdCarta);
		return s;
	}

	// Rondas que una tropa tiene que sobrevivir en el campo para que su carta pueda volver a la mano.
	private const int RONDAS_SOBREVIVIDAS_PARA_REPETIR = 3;

	/// <summary>Cartas cuya tropa está en el campo y todavía NO pueden volver a salir. Las que ya
	/// sobrevivieron RONDAS_SOBREVIVIDAS_PARA_REPETIR rondas quedan fuera de la lista (ya pueden salir),
	/// salvo con <paramref name="todas"/> (la mano tras la Nuclear no repite nada de los carriles).</summary>
	private HashSet<int> IndicesDesplegados(bool todas = false)
	{
		var s = new HashSet<int>();
		int ronda = RondaDeLaManoJugador();
		foreach (Node n in GetTree().GetNodesInGroup("tropas_jugador"))
		{
			if (n is not Node2D t || !IsInstanceValid(t) || !t.HasMeta("idx_mazo")) continue;
			bool yaSobrevivio = t.HasMeta("ronda_invocada")
				&& ronda - (int)t.GetMeta("ronda_invocada") >= RONDAS_SOBREVIVIDAS_PARA_REPETIR;
			if (todas || ModoTutorial || !yaSobrevivio) s.Add((int)t.GetMeta("idx_mazo"));
		}
		return s;
	}

	private bool EsElegible(int i, HashSet<int> enMano, HashSet<int> desplegados)
	{
		if (i < 0 || i >= escenasTropas.Length) return false;
		if (enMano.Contains(i)) return false;
		// Mano nueva tras la bomba Nuclear: nada de lo que está en los carriles ni de lo que murió.
		if (_excluirRepartoNuclear != null && _excluirRepartoNuclear.Contains(i)) return false;
		if (_cooldownIndice[i] > 0) return false;
		// Nunca una carta cuya tropa sigue viva en el campo (antes las especiales sí se repetían).
		if (desplegados.Contains(i)) return false;
		// Nunca 3 del mismo tipo ni 2 colosos en la mano.
		if (TipoLlenoEnMano(_tipoIndice[i], enMano)) return false;
		return true;
	}

	private int ElegirIndiceElegible(TipoTropa tipo)
	{
		var enMano = IndicesEnMano();
		var desplegados = IndicesDesplegados();
		List<int> pool = tipo switch
		{
			TipoTropa.Tactico => _idxTactico,
			TipoTropa.Asesino => _idxAsesino,
			TipoTropa.Coloso  => _idxColoso,
			_                 => null
		};
		var candidatos = new List<int>();
		if (pool != null)
		{
			foreach (int i in pool) if (EsElegible(i, enMano, desplegados)) candidatos.Add(i);
		}
		else
		{
			for (int i = 0; i < escenasTropas.Length; i++) if (EsElegible(i, enMano, desplegados)) candidatos.Add(i);
		}
		return candidatos.Count == 0 ? -1 : candidatos[random.Next(candidatos.Count)];
	}

	// Cualquier índice elegible sin importar el tipo.
	private int ElegirIndiceParaSpot() => ElegirIndiceElegible(TipoTropa.Desconocido);

	// Red de seguridad: ignora el cooldown para no dejar un spot vacío, pero NUNCA repite una carta
	// que ya está en la mano o cuya tropa está en el campo (antes, como último recurso, sí la repetía).
	private int ElegirRelajado()
	{
		var enMano = IndicesEnMano();
		var desplegados = IndicesDesplegados();
		var cand = new List<int>();
		for (int i = 0; i < escenasTropas.Length; i++)
			if (!enMano.Contains(i) && !desplegados.Contains(i) && !(_excluirRepartoNuclear?.Contains(i) ?? false)
				&& !TipoLlenoEnMano(_tipoIndice[i], enMano)) cand.Add(i);
		return cand.Count == 0 ? -1 : cand[random.Next(cand.Count)];
	}

	private int ElegirColosoRotacion(int turnoNum)
	{
		if (_colosoRotacion.Count == 0) return ElegirIndiceElegible(TipoTropa.Coloso);
		int k = Math.Max(1, turnoNum / 3); // 1 en el turno 3, 2 en el 6, 3 en el 9…
		var enMano = IndicesEnMano();
		var desplegados = IndicesDesplegados();
		for (int off = 0; off < _colosoRotacion.Count; off++)
		{
			int i = _colosoRotacion[(k - 1 + off) % _colosoRotacion.Count];
			if (EsElegible(i, enMano, desplegados)) return i;
		}
		return ElegirIndiceElegible(TipoTropa.Coloso);
	}

	// ── RELLENO DE LA MANO ────────────────────────────────────────────────
	// Nombres de los 3 spots visibles de la mano (solicitado: 3 cartas en partida).
	private static readonly string[] SPOTS_MANO = { "Spot1", "Spot2", "Spot3" };

	// Tamaño base de las cartas de mano (antes 1.05, se pidió bajarlo un poco). La carta robada
	// (Spot4, ver Campo1.RoboCarta.cs) usa un valor algo menor para distinguirse del resto.
	private const float ESCALA_MANO_NORMAL = 0.92f;
	private const float ESCALA_MANO_ROBADA = 0.8f;

	private void RellenarManoObjetivo()
	{
		// Tutorial: la mano NO se repone sola. Se juegan las 3 cartas iniciales y no vuelve a
		// aparecer ninguna carta de invocación hasta el sacrificio del Maguín, que es cuando la
		// secuencia guiada ofrece el reemplazo (ver PermitirManoTutorial en Campo1.Tutorial.cs).
		if (ModoTutorial && _tutorialManoBloqueada) return;
		if (contenedorMano == null || _tipoIndice == null) return;
		string[] spots = SPOTS_MANO;

		var vacios = new List<string>();
		int ocupTac = 0, ocupAse = 0, ocupCol = 0;
		foreach (string s in spots)
		{
			Carta enSpot = null;
			foreach (Node n in contenedorMano.GetChildren())
				if (n is Carta c && c.NombreSpot == s && !c.IsQueuedForDeletion()) { enSpot = c; break; }
			if (enSpot == null) { vacios.Add(s); continue; }
			if (enSpot.IdCarta >= 0 && enSpot.IdCarta < _tipoIndice.Length)
			{
				switch (_tipoIndice[enSpot.IdCarta])
				{
					case TipoTropa.Tactico: ocupTac++; break;
					case TipoTropa.Asesino: ocupAse++; break;
					case TipoTropa.Coloso:  ocupCol++; break;
				}
			}
		}
		if (vacios.Count == 0) return;

		int turnoNum = RondaDeLaManoJugador();

		// COLOSOS: desde la ronda 3, uno por cada ciclo de 3 rondas. Si en su ronda no se pudo dar,
		// queda pendiente para el próximo reparto. Nunca dos colosos juntos en la mano.
		int ciclo = turnoNum / 3;
		// (El tutorial queda fuera: su mano es guionada.)
		bool colosoTurn = !ModoTutorial && _idxColoso.Count > 0 && turnoNum >= RONDA_MINIMA_COLOSO
			&& ciclo > _cicloColosoEntregado && !HayColosoEnMano();

		// La mano arranca SIEMPRE con 2 tácticos + 1 asesino; la siguiente trae los del otro tipo
		// (2 asesinos + 1 táctico), y así alternando.
		bool manoDeAsesinos = _manosRepartidas % 2 == 1;
		int tgtTac = colosoTurn ? 1 : (manoDeAsesinos ? 1 : 2);
		int tgtAse = colosoTurn ? 1 : (manoDeAsesinos ? 2 : 1);
		int tgtCol = colosoTurn ? 1 : 0;
		// Desde la ronda 4, reparto libre: los spots se llenan con cualquier tipo (respetando el tope
		// de 2 iguales / 1 coloso). Solo se mantiene el coloso pendiente del ciclo, si toca.
		if (!ModoTutorial && turnoNum >= PRIMERA_RONDA_REPARTO_LIBRE) { tgtTac = 0; tgtAse = 0; }
		_manosRepartidas++;

		int needTac = Math.Max(0, tgtTac - ocupTac);
		int needAse = Math.Max(0, tgtAse - ocupAse);
		int needCol = Math.Max(0, tgtCol - ocupCol);

		foreach (string s in vacios)
		{
			int idx = -1;
			if (needCol > 0)
			{
				idx = colosoTurn ? ElegirColosoRotacion(turnoNum) : ElegirIndiceElegible(TipoTropa.Coloso);
				if (idx >= 0) { needCol--; _cicloColosoEntregado = ciclo; }
			}
			if (idx < 0 && needTac > 0) { idx = ElegirIndiceElegible(TipoTropa.Tactico); if (idx >= 0) needTac--; }
			if (idx < 0 && needAse > 0) { idx = ElegirIndiceElegible(TipoTropa.Asesino); if (idx >= 0) needAse--; }
			if (idx < 0) idx = ElegirIndiceParaSpot();
			if (idx < 0) idx = ElegirRelajado();
			if (idx >= 0) CrearCartaConIndice(s, idx);
		}
		ReacomodarManoTropas(); // las nuevas se acomodan según cuántas quedaron en la mano
	}

	// ── CREACIÓN DE CARTA CON ÍNDICE ──────────────────────────────────────
	private Carta CrearCartaConIndice(string id, int idx, float escalaBase = ESCALA_MANO_NORMAL)
	{
		if (juegoTerminado || escenaCartaBase == null || contenedorMano == null) return null;
		if (idx < 0 || idx >= escenasTropas.Length) return null;
		Marker2D spot = contenedorMano.GetNodeOrNull<Marker2D>(id); if (spot == null) return null;
		Carta n = (Carta)escenaCartaBase.Instantiate(); n.NombreSpot = id; contenedorMano.AddChild(n);
		n.Rotation = spot.Rotation;
		Vector2 esc = new Vector2(escalaBase, escalaBase); n.Scale = esc;
		n.GlobalPosition = spot.GlobalPosition - (n.Size * esc / 2);
		n.GuardarEstadoOriginal();
		n.AsignarDatos(imagenesCartas[idx], escenasTropas[idx], idx);

		// Cartas especiales: fijar su cadencia de reaparición al ofrecerse.
		if (_esEspecialIndice != null && idx < _esEspecialIndice.Length && _esEspecialIndice[idx])
			_cooldownIndice[idx] = COOLDOWN_ESPECIAL;
		return n;
	}

	// ── HOOKS DE TURNO / MUERTE ───────────────────────────────────────────
	private void AvanzarCooldownsJugador()
	{
		if (_cooldownIndice == null) return;
		for (int i = 0; i < _cooldownIndice.Length; i++) if (_cooldownIndice[i] > 0) _cooldownIndice[i]--;
	}

	private void NotificarMuerteIndiceJugador(Node2D tropa)
	{
		if (_cooldownIndice == null || tropa == null || !tropa.HasMeta("idx_mazo")) return;
		int idx = (int)tropa.GetMeta("idx_mazo");
		if (idx < 0 || idx >= _cooldownIndice.Length) return;
		_cooldownIndice[idx] = _esEspecialIndice[idx] ? COOLDOWN_ESPECIAL : COOLDOWN_NORMAL;
	}

	// ── CPU: aparición de colosos cada 3 turnos ───────────────────────────
	// El CPU no puede tener la misma tropa repetida dos veces en su propio campo (se sentía
	// injusto: dos Gólem o dos Caballeros al mismo tiempo del lado rival). Se filtra por ruta de
	// escena contra las tropas rivales vivas ahora mismo; si alguna vez se diera el caso de que
	// TODAS las opciones disponibles ya están repetidas, se cae al pool completo sin filtrar antes
	// que dejar un carril sin poder invocar nada.
	private HashSet<string> TropasEnCampoRival()
	{
		var enCampo = new HashSet<string>();
		foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
			if (n is Node2D t && IsInstanceValid(t) && !string.IsNullOrEmpty(t.SceneFilePath))
				enCampo.Add(t.SceneFilePath);
		return enCampo;
	}

	private PackedScene ElegirTropaCPUDeck()
	{
		var yaEnCampo = TropasEnCampoRival();

		// Forzar coloso en los turnos de coloso del CPU (evitando repetir uno ya en el campo).
		if (_cpuColosoPendiente && _colososCPU.Count > 0)
		{
			_cpuColosoPendiente = false;
			var colososLibres = _colososCPU.FindAll(ruta => !yaEnCampo.Contains(ruta));
			var poolColoso = colososLibres.Count > 0 ? colososLibres : _colososCPU;
			// El elegido de antemano (ya cargado en segundo plano), si sigue siendo válido.
			string rutaColoso = !string.IsNullOrEmpty(_colosoPrecargadoCPU) && poolColoso.Contains(_colosoPrecargadoCPU)
				? _colosoPrecargadoCPU
				: poolColoso[random.Next(poolColoso.Count)];
			_colosoPrecargadoCPU = null;
			int idxColosoJugado = IndiceCPUDe(rutaColoso);
			_manoVisualCPU.Remove(idxColosoJugado); // si lo tenía en mano, lo gasta
			var pc = GD.Load<PackedScene>(rutaColoso);
			if (pc != null) return pc;
		}

		// El rival juega LAS CARTAS DE SU MANO (la que se ve en la pantalla de Robar): si le robás una,
		// deja de tenerla de verdad. La mano se le repone al empezar su turno.
		if (_manoVisualCPU.Count > 0)
		{
			var enMano = _manoVisualCPU.FindAll(i => i >= 0 && i < _cartasCPU.Length && !yaEnCampo.Contains(_cartasCPU[i]));
			if (enMano.Count == 0) enMano = _manoVisualCPU.FindAll(i => i >= 0 && i < _cartasCPU.Length);
			if (enMano.Count > 0)
			{
				int idxElegido = enMano[random.Next(enMano.Count)];
				_manoVisualCPU.Remove(idxElegido); // la gasta: sale de su mano
				var deMano = GD.Load<PackedScene>(_cartasCPU[idxElegido]);
				if (deMano != null) return deMano;
			}
		}

		var fuenteBase = _mazoCPU.Count > 0 ? _mazoCPU : new List<string>(TODAS_LAS_TROPAS_CPU);
		var fuenteLibre = fuenteBase.FindAll(ruta => !yaEnCampo.Contains(ruta));
		var fuente = fuenteLibre.Count > 0 ? fuenteLibre : fuenteBase;

		var packed = GD.Load<PackedScene>(fuente[random.Next(fuente.Count)]);
		if (packed != null) return packed;
		return GD.Load<PackedScene>(TODAS_LAS_TROPAS_CPU[random.Next(TODAS_LAS_TROPAS_CPU.Length)]);
	}

	// ── REACOMODO DE LA MANO (3 ↔ 4 cartas) ───────────────────────────────
	// Al robar una carta al rival (Spot4, ver Campo1.RoboCarta.cs) hay momentáneamente 4 cartas
	// en mano: las 4 se ven un poco más juntas y chicas para no tapar nada en pantalla. En cuanto
	// se juega cualquiera de las 4 (sin importar cuál) y quedan 3, TODAS vuelven a la disposición
	// y tamaño normales — nunca se queda "como 4 pegadas pareciendo 3".
	// Desplazado un poco arriba e izquierda respecto al centro de ManoManual, para que las 4
	// cartas queden mejor centradas en pantalla (antes se veían corridas a la derecha/abajo).
	// ── ABANICO SIMÉTRICO CENTRADO (RearrangeHand) ─────────────────────────
	// Recalcula posición Y rotación de cada carta cada vez que cambia la cantidad en la mano (jugar,
	// gastar o robar). Una sola fórmula para 1 a 4 cartas, centrada en Spot2 (el punto medio de la
	// zona de mano ya calibrado en el editor — hace de "centro X de pantalla en la zona inferior"):
	//   offset = i - (n-1)/2        →  0 con 1 carta; ±0.5 con 2; -1,0,1 con 3; -1.5,-0.5,0.5,1.5 con 4
	//   posición.x = centro.x + offset * ESPACIADO
	//   rotación   = offset * ROTACION_PASO           (abanico: negativo a la izquierda, positivo a la derecha)
	// Un solo abanico (mismo solape marcado en todas las cantidades, como en la referencia): los
	// valores base son los que ya estaban afinados a mano para 4 cartas, y se escalan junto con el
	// tamaño de la carta (escalaMult) para que el solape se vea igual de parejo con 1, 2, 3 o 4.
	private const float ABANICO_ESPACIADO_BASE = 103f;  // separación horizontal entre cartas
	private const float ABANICO_ROTACION_PASO  = 0.103f; // radianes de giro por "paso" de offset
	private const float ABANICO_ARCO_Y_BASE    = 26f;   // el centro del abanico queda más arriba
	private const float ABANICO_DURACION       = 0.25f; // Tween, EaseOut/Cubic — ver ReubicarEnMano en Carta.cs
	private const float ESCALA_MANO_COMPACTA   = 0.78f; // 4 cartas
	private bool _enModoManoCompacta = false;

	private void ReacomodarManoTropas()
	{
		if (contenedorMano == null) return;
		var cartas = new List<Carta>();
		foreach (Node n in contenedorMano.GetChildren())
			if (n is Carta c && c.EstaEnMano && !c.IsQueuedForDeletion()) cartas.Add(c);

		// Orden estable IZQUIERDA→DERECHA por su spot ACTUAL (no por orden de creación): sin esto, una
		// carta nueva (que Godot agrega siempre como ÚLTIMO hijo, sin importar qué Spot le tocó) podía
		// terminar reasignada a la posición de otra carta y las dos "saltaban" de lugar al reacomodar.
		cartas.Sort((a, b) => Array.IndexOf(SPOTS_MANO, a.NombreSpot).CompareTo(Array.IndexOf(SPOTS_MANO, b.NombreSpot)));

		// La carta robada (si sigue en mano) va siempre última ("mía, mía, robada"). Se rastrea por
		// REFERENCIA, no por spot, así "Robar Carta" sigue bloqueada hasta que esa carta se juegue de
		// verdad (ver ActualizarEstadoCartaRobada).
		if (_cartaRobada != null && (!IsInstanceValid(_cartaRobada) || !_cartaRobada.EstaEnMano || _cartaRobada.IsQueuedForDeletion()))
			_cartaRobada = null;
		if (_cartaRobada != null && cartas.Contains(_cartaRobada))
		{
			cartas.Remove(_cartaRobada);
			cartas.Add(_cartaRobada);
		}

		int cantidad = cartas.Count;
		if (cantidad == 0) { _enModoManoCompacta = false; return; }

		Marker2D centro = contenedorMano.GetNodeOrNull<Marker2D>("Spot2");
		Vector2 puntoCentral = centro?.Position ?? Vector2.Zero;

		bool compacto = cantidad >= 4;
		// Más grande cuantas menos cartas quedan (da más visibilidad con 1-2), más chica y compacta con 4.
		float escalaMult = compacto ? ESCALA_MANO_COMPACTA / ESCALA_MANO_NORMAL
			: cantidad == 1 ? 1.25f : cantidad == 2 ? 1.12f : 1f;
		Vector2 esc = new Vector2(ESCALA_MANO_NORMAL * escalaMult, ESCALA_MANO_NORMAL * escalaMult);
		// El espaciado y el arco escalan junto con el tamaño de carta: mismo solape marcado (como en
		// la referencia) sin importar si hay 1, 2, 3 o 4 cartas.
		float espaciado = ABANICO_ESPACIADO_BASE * escalaMult;
		float rotPaso   = ABANICO_ROTACION_PASO;
		float arcoY     = ABANICO_ARCO_Y_BASE * escalaMult;

		float maxOffset = Mathf.Max(0.5f, (cantidad - 1) / 2f); // evita dividir por 0 con 1 sola carta
		for (int i = 0; i < cantidad; i++)
		{
			float offset = i - (cantidad - 1) / 2f;
			float t = offset / maxOffset; // -1..1, para el arco del abanico (0 si cantidad==1)

			Vector2 puntoCarta = puntoCentral + new Vector2(offset * espaciado, -arcoY * (1f - t * t));
			float rotacion = offset * rotPaso;

			string spotDestino = i < SPOTS_MANO.Length ? SPOTS_MANO[i] : "Spot4";
			cartas[i].NombreSpot = spotDestino;
			cartas[i].ZIndex = 10 + i; // superposiciócantidad del abanico: la de más a la derecha, encima
			cartas[i].ReubicarEnMano(puntoCarta - (cartas[i].Size * esc / 2f), esc, rotacion, ABANICO_DURACION);
		}
		_enModoManoCompacta = compacto;
	}

	// ── LIMPIEZA DE CARRILES FANTASMA ─────────────────────────────────────
	// Libera cualquier "Ocupado" cuyo tropa_instanciada ya no exista, evitando
	// carriles que parecen ocupados pero están vacíos (tropa invisible/ausente).
	private void LimpiarCarrilesFantasma()
	{
		string[] zonas = { "Mod1", "Mod2", "Mod3", "ModRival1", "ModRival2", "ModRival3" };
		foreach (string nombre in zonas)
		{
			Node2D zona = GetTree().Root.FindChild(nombre, true, false) as Node2D;
			Node ocup = zona?.GetNodeOrNull("Ocupado");
			if (ocup == null) continue;
			bool valido = false;
			if (ocup.HasMeta("tropa_instanciada"))
			{
				var obj = ocup.GetMeta("tropa_instanciada").AsGodotObject();
				if (obj is Node2D t && IsInstanceValid(t) && !t.IsQueuedForDeletion()) valido = true;
			}
			if (!valido) ocup.Free();
		}
	}
}
