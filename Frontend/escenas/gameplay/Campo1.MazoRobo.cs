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
		foreach (string ruta in escenasTropas)
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
			foreach (string r in escenasTropas) if (ClasificacionCartas.TipoDe(r) != TipoTropa.Coloso) _mazoCPU.Add(r);
		if (_colososCPU.Count == 0)
			foreach (string r in escenasTropas) if (ClasificacionCartas.TipoDe(r) == TipoTropa.Coloso) _colososCPU.Add(r);
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
			if (!enMano.Contains(i) && !desplegados.Contains(i)) cand.Add(i);
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
	private void RellenarManoObjetivo()
	{
		if (contenedorMano == null || _tipoIndice == null) return;
		string[] spots = { "Spot1", "Spot2", "Spot3", "Spot4" };

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
		bool colosoTurn = (turnoNum % 3 == 0) && _idxColoso.Count > 0;
		int tgtTac = 2;
		int tgtAse = colosoTurn ? 1 : 2;
		int tgtCol = colosoTurn ? 1 : 0;

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
	}

	// ── CREACIÓN DE CARTA CON ÍNDICE ──────────────────────────────────────
	private void CrearCartaConIndice(string id, int idx)
	{
		if (juegoTerminado || escenaCartaBase == null || contenedorMano == null) return;
		if (idx < 0 || idx >= escenasTropas.Length) return;
		Marker2D spot = contenedorMano.GetNodeOrNull<Marker2D>(id); if (spot == null) return;
		Carta n = (Carta)escenaCartaBase.Instantiate(); n.NombreSpot = id; contenedorMano.AddChild(n);
		n.Rotation = spot.Rotation;
		Vector2 esc = new Vector2(1.05f, 1.05f); n.Scale = esc;
		n.GlobalPosition = spot.GlobalPosition - (n.Size * esc / 2);
		n.GuardarEstadoOriginal();
		n.AsignarDatos(imagenesCartas[idx], escenasTropas[idx], idx);

		// Cartas especiales: fijar su cadencia de reaparición al ofrecerse.
		if (_esEspecialIndice != null && idx < _esEspecialIndice.Length && _esEspecialIndice[idx])
			_cooldownIndice[idx] = COOLDOWN_ESPECIAL;
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
	private PackedScene ElegirTropaCPUDeck()
	{
		// Forzar coloso en los turnos de coloso del CPU.
		if (_cpuColosoPendiente && _colososCPU.Count > 0)
		{
			_cpuColosoPendiente = false;
			var pc = GD.Load<PackedScene>(_colososCPU[random.Next(_colososCPU.Count)]);
			if (pc != null) return pc;
		}
		var fuente = _mazoCPU.Count > 0 ? _mazoCPU : new List<string>(escenasTropas);
		var packed = GD.Load<PackedScene>(fuente[random.Next(fuente.Count)]);
		if (packed != null) return packed;
		return GD.Load<PackedScene>(escenasTropas[random.Next(escenasTropas.Length)]);
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
