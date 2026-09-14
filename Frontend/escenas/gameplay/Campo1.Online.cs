using Godot;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

/// <summary>
/// Multijugador en línea dentro de la batalla (Fase 2 — ACCIONES EN VIVO).
///
/// Modelo: el jugador activo hace su jugada localmente (con toda su lógica y animaciones) y publica
/// una ACCIÓN (invocar/atacar/habilidad/hechizo/fin_turno). Cada acción lleva además un SNAPSHOT del
/// tablero como "verdad". El rival sondea las acciones (~0.6s), reproduce la animación de cada una y
/// RECONCILIA los números con el snapshot — así ve la pelea en vivo y nunca se desincroniza.
///
/// El tablero se refleja: mi Mod ↔ su ModRival, así ambos se ven a la izquierda.
/// </summary>
public partial class Campo1 : Node2D
{
	private static readonly string[] ZONAS_ONLINE = { "Mod1", "Mod2", "Mod3", "ModRival1", "ModRival2", "ModRival3" };

	private int _accionesVistas = -1;
	private bool _ocupadoAcciones = false;
	private HttpRequest _httpAccion, _httpPollAcc;
	private Timer _timerPollAcc;

	private HttpRequest _httpLatido;
	private Timer _timerLatido;
	private bool _ocupadoLatido = false;

	private void ConfigurarModoOnline()
	{
		EsOnline = ContextoOnline.Activo;
		if (!EsOnline) return; // el nombre del rival ya sale sobre su barra (ver Campo1.Extra.cs)

		_httpAccion  = new HttpRequest(); AddChild(_httpAccion);
		_httpPollAcc = new HttpRequest(); AddChild(_httpPollAcc);
		_httpPollAcc.RequestCompleted += OnRespuestaAcciones;

		_timerPollAcc = new Timer { WaitTime = 0.6, OneShot = false };
		AddChild(_timerPollAcc);
		_timerPollAcc.Timeout += SondearAcciones;

		// Heartbeat: late cada 3s. Si el rival deja de latir 12s (tras haber entrado), este gana.
		_httpLatido = new HttpRequest(); AddChild(_httpLatido);
		_httpLatido.RequestCompleted += OnRespuestaLatido;
		_timerLatido = new Timer { WaitTime = 3.0, OneShot = false };
		AddChild(_timerLatido);
		_timerLatido.Timeout += EnviarLatido;
		_timerLatido.Start();

		// El asiento "A" empieza; el "B" espera y reproduce las acciones del rival.
		if (!ContextoOnline.SoyPrimero)
		{
			esTurnoJugador = false;
			movimientosRestantes = 0;
			AnunciarTurno();
			IniciarEsperaOnline();
		}
	}

	// ── EMITIR ACCIÓN (yo, jugador activo) ────────────────────────────────────
	// Cada acción lleva un snapshot del tablero como verdad para la reconciliación del rival.
	public void EmitirAccionOnline(string tipo, Godot.Collections.Dictionary datos = null)
	{
		if (!EsOnline) return;
		var dic = new Godot.Collections.Dictionary
		{
			{ "tipo", tipo },
			{ "datos", datos ?? new Godot.Collections.Dictionary() },
			{ "snapshot", SerializarTablero() }
		};
		string accion = Json.Stringify(dic);
		string cuerpo = JsonSerializer.Serialize(new { jugadorId = ContextoOnline.JugadorId, accion });
		string[] headers = { "Content-Type: application/json" };
		_httpAccion.Request($"{ApiConfig.Base}/api/match/{ContextoOnline.MatchId}/accion", headers, HttpClient.Method.Post, cuerpo);
	}

	// Llamado cuando termina MI turno (la CPU está gateada en online, ver EjecutarTurnoCPU).
	private void EsperarRivalOnline()
	{
		if (!EsOnline) return;
		EmitirAccionOnline("fin_turno");
		IniciarEsperaOnline();
	}

	private void IniciarEsperaOnline()
	{
		MostrarAviso("Turno del rival…", new Color(1f, 0.85f, 0.4f));
		if (_timerPollAcc != null && _timerPollAcc.IsStopped()) _timerPollAcc.Start();
	}

	// ── SONDEAR Y REPRODUCIR ACCIONES DEL RIVAL ───────────────────────────────
	private void SondearAcciones()
	{
		if (!EsOnline || esTurnoJugador || juegoTerminado || _ocupadoAcciones) return;
		_ocupadoAcciones = true;
		if (_httpPollAcc.Request($"{ApiConfig.Base}/api/match/{ContextoOnline.MatchId}/acciones?desde={_accionesVistas}") != Error.Ok)
			_ocupadoAcciones = false;
	}

	private void OnRespuestaAcciones(long result, long code, string[] headers, byte[] body)
	{
		_ocupadoAcciones = false;
		if (result != (long)HttpRequest.Result.Success || code != 200) return;
		try
		{
			var doc = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(body));
			if (!doc.TryGetProperty("acciones", out var arr) || arr.ValueKind != JsonValueKind.Array) return;
			foreach (var a in arr.EnumerateArray())
			{
				_accionesVistas++;
				ReproducirAccion(a.GetString() ?? "");
				if (juegoTerminado) return;
			}
		}
		catch { }
	}

	private void ReproducirAccion(string accionJson)
	{
		if (string.IsNullOrEmpty(accionJson)) return;
		JsonElement acc;
		try { acc = JsonSerializer.Deserialize<JsonElement>(accionJson); } catch { return; }

		string tipo = acc.TryGetProperty("tipo", out var t) ? (t.GetString() ?? "") : "";
		JsonElement datos = acc.TryGetProperty("datos", out var d) ? d : default;

		// Animación específica del rival según la acción (el daño real llega en el snapshot).
		if (tipo == "atacar" && datos.ValueKind == JsonValueKind.Object &&
			datos.TryGetProperty("carrilAtacante", out var ca))
		{
			string carrilMio = EspejarCarril(ca.GetString() ?? "");
			if (TropaEnCarril(carrilMio) is TropaBase atk && IsInstanceValid(atk))
				atk.ReproducirSoloAnimacion("ataque");
		}

		// Reconciliar el tablero con el snapshot (la verdad): actualiza vidas, tropas y huevos.
		if (acc.TryGetProperty("snapshot", out var snap) && snap.ValueKind == JsonValueKind.String)
			ReconciliarLigero(snap.GetString() ?? "");

		if (tipo == "fin_turno" && !juegoTerminado)
			IniciarMiTurnoOnline();
	}

	// ── RECONCILIAR (sin destruir lo que no cambió: evita el efecto "bots") ────
	private void ReconciliarLigero(string json)
	{
		JsonElement doc;
		try { doc = JsonSerializer.Deserialize<JsonElement>(json); } catch { return; }

		// Espejo de la vida de los huevos.
		if (doc.TryGetProperty("vidaRival", out var vr)) vidaJugador = vr.GetInt32();
		if (doc.TryGetProperty("vidaJugador", out var vj)) vidaRival = vj.GetInt32();

		// Mapa carril-destino (mi frame) → datos de la tropa que debería estar ahí.
		var deseado = new Dictionary<string, JsonElement>();
		if (doc.TryGetProperty("tropas", out var arr) && arr.ValueKind == JsonValueKind.Array)
			foreach (var tr in arr.EnumerateArray())
			{
				string dest = EspejarCarril(tr.GetProperty("carril").GetString() ?? "");
				if (!string.IsNullOrEmpty(dest)) deseado[dest] = tr;
			}

		foreach (string carril in ZONAS_ONLINE)
		{
			var actual = TropaEnCarril(carril) as TropaBase;
			bool hayDeseado = deseado.TryGetValue(carril, out var td);

			if (!hayDeseado)
			{
				if (actual != null) QuitarTropaDeCarril(carril); // murió
				continue;
			}

			string escenaDeseada = td.GetProperty("escena").GetString() ?? "";
			bool miLado = !carril.StartsWith("ModRival");

			if (actual != null && IsInstanceValid(actual) && actual.SceneFilePath == escenaDeseada)
			{
				// Misma tropa: solo actualizar stats (preserva su animación en curso).
				actual.FijarStats(
					td.GetProperty("vida").GetInt32(), td.GetProperty("vidaMax").GetInt32(),
					td.GetProperty("escudo").GetInt32(), td.GetProperty("escudoMax").GetInt32(),
					td.GetProperty("turnoCarta").GetInt32(), td.GetProperty("habUsada").GetBoolean());
			}
			else
			{
				if (actual != null) QuitarTropaDeCarril(carril);
				ColocarTropaAdoptada(carril, escenaDeseada,
					td.GetProperty("vida").GetInt32(), td.GetProperty("vidaMax").GetInt32(),
					td.GetProperty("escudo").GetInt32(), td.GetProperty("escudoMax").GetInt32(),
					td.GetProperty("turnoCarta").GetInt32(), td.GetProperty("habUsada").GetBoolean(), miLado);
			}
		}

		if (TodosLosSeisLlenos()) _faseApertura = false;
		ActualizarInterfaz();

		if (vidaJugador <= 0) FinalizarPartida("DERROTA");
		else if (vidaRival <= 0) FinalizarPartida("VICTORIA");
	}

	private void ColocarTropaAdoptada(string carril, string escena, int vida, int vidaMax, int escudo, int escudoMax, int turnoCarta, bool habUsada, bool miLado)
	{
		if (string.IsNullOrEmpty(escena) || !ResourceLoader.Exists(escena)) return;
		Node2D zona = GetTree().Root.FindChild(carril, true, false) as Node2D;
		if (zona == null || zona.GetNodeOrNull("Ocupado") != null) return;
		var packed = GD.Load<PackedScene>(escena);
		if (packed == null) return;

		Node2D t = (Node2D)packed.Instantiate();
		AddChild(t);
		t.AddToGroup(miLado ? "tropas_jugador" : "tropas_rival");
		if (t is TropaBase tb)
		{
			tb.FijarStats(vida, vidaMax, escudo, escudoMax, turnoCarta, habUsada);
			tb.ColocarPorCentroColision(zona.GlobalPosition);
		}
		else t.GlobalPosition = zona.GlobalPosition;

		t.SetMeta("carril", carril);
		t.ZIndex = carril is "Mod3" or "ModRival3" ? 100 : (carril is "Mod2" or "ModRival2" ? 50 : 10);
		t.Visible = true; t.Modulate = Colors.White;
		if (!miLado) AsegurarOrientacionRival(t);

		var m = new Node { Name = "Ocupado" };
		zona.AddChild(m);
		m.SetMeta("tropa_instanciada", t);
	}

	// ── INICIAR MI TURNO (tras recibir "fin_turno" del rival) ─────────────────
	private void IniciarMiTurnoOnline()
	{
		if (_timerPollAcc != null && !_timerPollAcc.IsStopped()) _timerPollAcc.Stop();

		esTurnoJugador = true;
		_turnoFinalizando = false;
		tiempoTurnoActual = DURACION_TURNO_SEG;
		movimientosRestantes = ENERGIA_MAXIMA;
		usosBarajar = 0; usosSacrificio = 0;
		faseInvocacion = _faseApertura ? !TodosSpotsOcupados() : false;
		_hechizoUsadoEsteTurno = false;

		AvanzarCooldownsJugador();
		foreach (Node n in GetTree().GetNodesInGroup("tropas_jugador"))
		{
			if (n.HasMethod("SetActivo")) n.Call("SetActivo", true);
			if (n.HasMethod("AvanzarTurnoTropa")) n.Call("AvanzarTurnoTropa");
		}
		CompletarManoAlInicio();

		AnunciarTurno();
		ActualizarInterfaz();
	}

	// ── SERIALIZAR (foto del tablero desde mi perspectiva) ────────────────────
	private string SerializarTablero()
	{
		var tropas = new Godot.Collections.Array();
		foreach (string z in ZONAS_ONLINE)
		{
			if (TropaEnCarril(z) is TropaBase tb && IsInstanceValid(tb))
				tropas.Add(new Godot.Collections.Dictionary
				{
					{ "carril", z }, { "escena", tb.SceneFilePath },
					{ "vida", tb.vidaActual }, { "vidaMax", tb.vidaMaxima },
					{ "escudo", tb.escudoActual }, { "escudoMax", tb.escudoMaximo },
					{ "turnoCarta", tb.turnoActualCarta }, { "habUsada", tb.habilidadUsada }
				});
		}
		var dic = new Godot.Collections.Dictionary
		{
			{ "vidaJugador", vidaJugador }, { "vidaRival", vidaRival },
			{ "faseApertura", _faseApertura }, { "tropas", tropas }
		};
		return Json.Stringify(dic);
	}

	// ── LATIDO / DESCONEXIÓN ──────────────────────────────────────────────────
	private void EnviarLatido()
	{
		if (!EsOnline || juegoTerminado || _ocupadoLatido) return;
		_ocupadoLatido = true;
		string cuerpo = JsonSerializer.Serialize(new { jugadorId = ContextoOnline.JugadorId });
		string[] headers = { "Content-Type: application/json" };
		if (_httpLatido.Request($"{ApiConfig.Base}/api/match/{ContextoOnline.MatchId}/latido", headers, HttpClient.Method.Post, cuerpo) != Error.Ok)
			_ocupadoLatido = false;
	}

	private void OnRespuestaLatido(long result, long code, string[] headers, byte[] body)
	{
		_ocupadoLatido = false;
		if (result != (long)HttpRequest.Result.Success || code != 200) return;
		try
		{
			var doc = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(body));
			string resultado = doc.TryGetProperty("resultado", out var r) ? (r.GetString() ?? "") : "";
			bool rivalCaido = doc.TryGetProperty("rivalCaido", out var rc) && rc.GetBoolean();
			if (!string.IsNullOrEmpty(resultado)) ResolverResultadoOnline(resultado);
			else if (rivalCaido) ResolverResultadoOnline("gano_" + ContextoOnline.Asiento);
		}
		catch { }
	}

	private void ResolverResultadoOnline(string resultado)
	{
		if (juegoTerminado) return;
		if (_timerLatido != null && !_timerLatido.IsStopped()) _timerLatido.Stop();
		if (_timerPollAcc != null && !_timerPollAcc.IsStopped()) _timerPollAcc.Stop();

		if (resultado == "empate")
		{
			MostrarAviso("Empate: ambos abandonaron", Colors.White);
			FinalizarPartida("¡EMPATE!");
		}
		else if (resultado == "gano_" + ContextoOnline.Asiento)
		{
			MostrarAviso("El rival se desconectó. ¡Ganaste!", new Color(0.5f, 1f, 0.6f));
			FinalizarPartida("¡VICTORIA!");
		}
		else FinalizarPartida("¡DERROTA!");
	}

	// ── HELPERS ───────────────────────────────────────────────────────────────
	private Node2D TropaEnCarril(string carril)
	{
		Node2D zona = GetTree().Root.FindChild(carril, true, false) as Node2D;
		var ocup = zona?.GetNodeOrNull("Ocupado");
		if (ocup != null && ocup.HasMeta("tropa_instanciada"))
		{
			var obj = ocup.GetMeta("tropa_instanciada").AsGodotObject();
			if (obj is Node2D t && IsInstanceValid(t) && !t.IsQueuedForDeletion()) return t;
		}
		return null;
	}

	private void QuitarTropaDeCarril(string carril)
	{
		Node2D zona = GetTree().Root.FindChild(carril, true, false) as Node2D;
		var ocup = zona?.GetNodeOrNull("Ocupado");
		if (ocup == null) return;
		if (ocup.HasMeta("tropa_instanciada"))
		{
			var obj = ocup.GetMeta("tropa_instanciada").AsGodotObject();
			if (obj is Node2D t && IsInstanceValid(t)) t.QueueFree();
		}
		ocup.Free();
	}

	private static string EspejarCarril(string c)
	{
		if (c.StartsWith("ModRival")) return "Mod" + c.Substring("ModRival".Length);
		if (c.StartsWith("Mod")) return "ModRival" + c.Substring("Mod".Length);
		return "";
	}

	private bool TodosLosSeisLlenos()
	{
		foreach (string z in ZONAS_ONLINE)
			if (TropaEnCarril(z) == null) return false;
		return true;
	}
}
