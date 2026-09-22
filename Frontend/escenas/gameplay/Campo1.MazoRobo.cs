using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ══════════════════════════════════════════════════════════════════════
	// SISTEMA DE ROBO DE MANO POR TIPO
	// Reglas de diseño:
	//  • La mano ofrece 2 tácticos + 2 asesinos por turno.
	//  • Cada 3 turnos (turno 3, 6, 9…) aparece un coloso (rotando: Paper-Rex, Tanque…).
	//  • Una carta invocada NO vuelve a ofrecerse mientras su tropa siga viva; tras morir,
	//    reaparece una ronda después.
	//  • Cartas especiales (Peón, Soldado Cartoon, Soldado Real, Ka-Bar) reaparecen cada
	//    2 rondas sin importar si su tropa está viva o muerta.
	// ══════════════════════════════════════════════════════════════════════

	// Reglas de reparto: la mano alterna "2 tácticos + 1 asesino" y "2 asesinos + 1 táctico", y los
	// colosos recién entran cuando ya se gastaron 5 cartas de otro tipo (y nunca dos a la vez).
	private const int RONDA_MINIMA_COLOSO  = 3;
	private const int CARTAS_PARA_COLOSO   = 5;
	private int _manosRepartidas        = 0;
	private int _cartasNoColosoGastadas = 0;

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

	/// <summary>Cuenta una carta jugada por el jugador (para habilitar los colosos a las 5).</summary>
	public void RegistrarCartaGastada(int idxMazo)
	{
		if (_tipoIndice == null || idxMazo < 0 || idxMazo >= _tipoIndice.Length) return;
		if (_tipoIndice[idxMazo] != TipoTropa.Coloso) _cartasNoColosoGastadas++;
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

	private HashSet<int> IndicesDesplegados()
	{
		var s = new HashSet<int>();
		foreach (Node n in GetTree().GetNodesInGroup("tropas_jugador"))
			if (n is Node2D t && IsInstanceValid(t) && t.HasMeta("idx_mazo")) s.Add((int)t.GetMeta("idx_mazo"));
		return s;
	}

	private bool EsElegible(int i, HashSet<int> enMano, HashSet<int> desplegados)
	{
		if (i < 0 || i >= escenasTropas.Length) return false;
		if (enMano.Contains(i)) return false;
		// Mano nueva tras la bomba Nuclear: nada de lo que está en los carriles ni de lo que murió.
		if (_excluirRepartoNuclear != null && _excluirRepartoNuclear.Contains(i)) return false;
		if (_cooldownIndice[i] > 0) return false;
		if (desplegados.Contains(i) && !_esEspecialIndice[i]) return false;
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

	// Red de seguridad: nunca dejar un spot vacío. Ignora cooldown; evita duplicar desplegados.
	private int ElegirRelajado()
	{
		var enMano = IndicesEnMano();
		var desplegados = IndicesDesplegados();
		var cand = new List<int>();
		for (int i = 0; i < escenasTropas.Length; i++)
			if (!enMano.Contains(i) && !desplegados.Contains(i) && !(_excluirRepartoNuclear?.Contains(i) ?? false)) cand.Add(i);
		if (cand.Count == 0)
			for (int i = 0; i < escenasTropas.Length; i++) if (!enMano.Contains(i)) cand.Add(i);
		return cand.Count == 0 ? -1 : cand[random.Next(cand.Count)];
	}

	private int ElegirColosoRotacion(int turnoNum)
	{
		if (_colosoRotacion.Count == 0) return ElegirIndiceElegible(TipoTropa.Coloso);
		int k = turnoNum / 3; // 1 en el turno 3, 2 en el 6, 3 en el 9…
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

		int turnoNum = _turnosJugados / 2 + 1;

		// COLOSOS: no pueden salir de entrada. Hacen falta 3 rondas Y haber gastado 5 cartas de otro
		// tipo, y nunca puede haber dos colosos juntos en la mano (uno solo ya cambia la partida).
		bool colosoHabilitado = _idxColoso.Count > 0 && turnoNum >= RONDA_MINIMA_COLOSO
			&& _cartasNoColosoGastadas >= CARTAS_PARA_COLOSO && !HayColosoEnMano();
		bool colosoTurn = colosoHabilitado && (turnoNum % 3 == 0);

		// La mano arranca SIEMPRE con 2 tácticos + 1 asesino; la siguiente trae los del otro tipo
		// (2 asesinos + 1 táctico), y así alternando.
		bool manoDeAsesinos = _manosRepartidas % 2 == 1;
		int tgtTac = colosoTurn ? 1 : (manoDeAsesinos ? 1 : 2);
		int tgtAse = colosoTurn ? 1 : (manoDeAsesinos ? 2 : 1);
		int tgtCol = colosoTurn ? 1 : 0;
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
				if (idx >= 0) needCol--;
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
			string rutaColoso = poolColoso[random.Next(poolColoso.Count)];
			_manoVisualCPU.Remove(Array.IndexOf(escenasTropas, rutaColoso)); // si lo tenía en mano, lo gasta
			var pc = GD.Load<PackedScene>(rutaColoso);
			if (pc != null) return pc;
		}

		// El rival juega LAS CARTAS DE SU MANO (la que se ve en la pantalla de Robar): si le robás una,
		// deja de tenerla de verdad. La mano se le repone al empezar su turno.
		if (_manoVisualCPU.Count > 0)
		{
			var enMano = _manoVisualCPU.FindAll(i => i >= 0 && i < escenasTropas.Length && !yaEnCampo.Contains(escenasTropas[i]));
			if (enMano.Count == 0) enMano = _manoVisualCPU.FindAll(i => i >= 0 && i < escenasTropas.Length);
			if (enMano.Count > 0)
			{
				int idxElegido = enMano[random.Next(enMano.Count)];
				_manoVisualCPU.Remove(idxElegido); // la gasta: sale de su mano
				var deMano = GD.Load<PackedScene>(escenasTropas[idxElegido]);
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
	private static readonly Vector2[] LAYOUT_4_CENTROS = {
		new Vector2(-220, -55), new Vector2(-115, -81), new Vector2(-15, -81), new Vector2(90, -55)
	};
	private static readonly float[] LAYOUT_4_ROT = { -0.16f, -0.05f, 0.05f, 0.16f };
	private const float ESCALA_MANO_COMPACTA = 0.78f;
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

		if (cartas.Count >= 4)
		{
			Vector2 escCompacta = new Vector2(ESCALA_MANO_COMPACTA, ESCALA_MANO_COMPACTA);
			for (int i = 0; i < cartas.Count && i < 4; i++)
			{
				Vector2 localPos = LAYOUT_4_CENTROS[i] - (cartas[i].Size * escCompacta / 2f);
				cartas[i].ReubicarEnMano(localPos, escCompacta, LAYOUT_4_ROT[i]);
			}
			_enModoManoCompacta = true;
		}
		else
		{
			// 1-3 cartas: se reparten CENTRADAS sobre la línea de los 3 spots y, cuantas menos queden,
			// un poco más grandes — con 2 cartas van a los puntos medios y con 1 al centro justo. Así la
			// mano siempre se ve pareja y bien visible. La carta robada va siempre última ("mía, mía,
			// robada") y todas recuperan un NombreSpot real de Spot1/2/3, clave para que
			// RellenarManoObjetivo las cuente bien y no duplique ni deje huecos.
			if (_cartaRobada != null && (!IsInstanceValid(_cartaRobada) || !_cartaRobada.EstaEnMano || _cartaRobada.IsQueuedForDeletion()))
				_cartaRobada = null;
			if (_cartaRobada != null && cartas.Contains(_cartaRobada))
			{
				cartas.Remove(_cartaRobada);
				cartas.Add(_cartaRobada);
			}

			var spots = new List<Marker2D>();
			foreach (string nombre in SPOTS_MANO)
				if (contenedorMano.GetNodeOrNull<Marker2D>(nombre) is Marker2D m) spots.Add(m);
			if (spots.Count == 0) { _enModoManoCompacta = false; return; }

			float aumento = cartas.Count <= 1 ? 1.25f : cartas.Count == 2 ? 1.12f : 1f;
			Vector2 esc = new Vector2(ESCALA_MANO_NORMAL * aumento, ESCALA_MANO_NORMAL * aumento);
			float centro = (spots.Count - 1) / 2f;

			for (int i = 0; i < cartas.Count && i < SPOTS_MANO.Length; i++)
			{
				// t = "posición" sobre la línea de spots (0 = primero, spots.Count-1 = último).
				float t = Mathf.Clamp(centro + (i - (cartas.Count - 1) / 2f), 0f, spots.Count - 1);
				int baseIdx = Mathf.FloorToInt(t);
				int sigIdx  = Mathf.Min(baseIdx + 1, spots.Count - 1);
				float frac  = t - baseIdx;
				Vector2 posSpot = spots[baseIdx].Position.Lerp(spots[sigIdx].Position, frac);
				float rotSpot   = Mathf.LerpAngle(spots[baseIdx].Rotation, spots[sigIdx].Rotation, frac);

				cartas[i].NombreSpot = SPOTS_MANO[i];
				cartas[i].ReubicarEnMano(posSpot - (cartas[i].Size * esc / 2f), esc, rotSpot);
			}
			_enModoManoCompacta = false;
		}
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
