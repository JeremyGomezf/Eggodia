using Godot;
using System;
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

	// Cola de envío: HttpRequest solo procesa UNA petición a la vez, así que las acciones rápidas
	// (p. ej. 3 invocaciones en la apertura) se encolan y se mandan de a una, sin perderse.
	private readonly Queue<string> _colaAcciones = new();
	private bool _enviandoAccion = false;

	private HttpRequest _httpLatido;
	private Timer _timerLatido;
	private bool _ocupadoLatido = false;

	// WebSocket para el TIEMPO REAL (push). Aditivo: cuando conecta, avisa al instante que el rival
	// jugó y se lee la acción de inmediato (sin esperar el sondeo). Si el WS no está disponible
	// (p. ej. Nginx sin 'Connection upgrade'), NO pasa nada: el sondeo REST de 0.35s cubre todo.
	private WebSocketPeer _ws;
	private Timer _timerWs;
	private bool _wsConectado = false;

	// Arbitraje de resultado: el fin de partida se decide en el servidor, no localmente.
	private HttpRequest _httpResultado;
	private bool _finOnlineEnviado = false;

	private void ConfigurarModoOnline()
	{
		EsOnline = ContextoOnline.Activo;
		if (!EsOnline) return; // el nombre del rival ya sale sobre su barra (ver Campo1.Extra.cs)

		_httpAccion  = new HttpRequest(); AddChild(_httpAccion);
		_httpAccion.RequestCompleted += OnAccionEnviada; // vacía la cola de envío de a una
		_httpPollAcc = new HttpRequest(); AddChild(_httpPollAcc);
		_httpPollAcc.RequestCompleted += OnRespuestaAcciones;

		// 0.35s: las jugadas del rival aparecen ~2× más rápido que con 0.6s (más "tiempo real")
		// sin cambiar el transporte. Para push instantáneo real haría falta migrar a WebSockets.
		_timerPollAcc = new Timer { WaitTime = 0.35, OneShot = false };
		AddChild(_timerPollAcc);
		_timerPollAcc.Timeout += SondearAcciones;

		// Heartbeat: late cada 3s. Si el rival deja de latir 12s (tras haber entrado), este gana.
		_httpLatido = new HttpRequest(); AddChild(_httpLatido);
		_httpLatido.RequestCompleted += OnRespuestaLatido;
		_timerLatido = new Timer { WaitTime = 3.0, OneShot = false };
		AddChild(_timerLatido);
		_timerLatido.Timeout += EnviarLatido;
		_timerLatido.Start();

		// WebSocket (tiempo real). Intenta conectar; si no puede, todo sigue por sondeo.
		_ws = new WebSocketPeer();
		_ws.ConnectToUrl(ApiConfig.WsMatch(ContextoOnline.MatchId, ContextoOnline.JugadorId));
		_timerWs = new Timer { WaitTime = 0.05, OneShot = false };
		AddChild(_timerWs);
		_timerWs.Timeout += PollWs;
		_timerWs.Start();

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
			{ "autor", ContextoOnline.Asiento }, // "A"/"B": para que el rival no reproduzca mis propias acciones
			{ "datos", datos ?? new Godot.Collections.Dictionary() },
			{ "snapshot", SerializarTablero() }   // foto del tablero JUSTO tras esta acción (verdad)
		};
		string accion = Json.Stringify(dic);
		string cuerpo = JsonSerializer.Serialize(new { jugadorId = ContextoOnline.JugadorId, accion });
		_colaAcciones.Enqueue(cuerpo);
		BombearColaAcciones();
	}

	// Envía la siguiente acción encolada. Como HttpRequest es de una sola petición a la vez, solo
	// se dispara si no hay otra en curso; el resto sale en OnAccionEnviada.
	private void BombearColaAcciones()
	{
		if (_enviandoAccion || _colaAcciones.Count == 0 || _httpAccion == null) return;
		string cuerpo = _colaAcciones.Peek(); // no se quita hasta confirmar el envío (preserva orden)
		string[] headers = { "Content-Type: application/json" };
		if (_httpAccion.Request($"{ApiConfig.Base}/api/match/{ContextoOnline.MatchId}/accion", headers, HttpClient.Method.Post, cuerpo) == Error.Ok)
			_enviandoAccion = true;
		// Si falló el disparo, la acción queda en la cola y se reintenta (próximo Emitir o latido).
	}

	private void OnAccionEnviada(long result, long code, string[] headers, byte[] body)
	{
		_enviandoAccion = false;
		bool ok = result == (long)HttpRequest.Result.Success && (code == 200 || code == 201);
		if (ok && _colaAcciones.Count > 0) _colaAcciones.Dequeue(); // enviada: la sacamos de la cola

		// Empujón instantáneo al rival por WebSocket: la acción YA quedó guardada en el backend; el
		// WS solo AVISA "hay algo nuevo, léelo ya" (sin esperar tu sondeo de 0.35s). El rival lo lee
		// SIEMPRE por REST (única fuente de verdad, con dedup por índice serializado). No mandamos la
		// acción por el WS a propósito: si el WS y el REST reprodujeran en paralelo, una carrera entre
		// ambos duplicaba acciones y adelantaba el contador, y podía SALTARSE el 'fin_turno' → el turno
		// no se pasaba y el rival no podía jugar. Con el WS como simple empujón eso no puede pasar.
		if (ok && _ws != null && _ws.GetReadyState() == WebSocketPeer.State.Open) _ws.SendText("n");
		// Si falló, se mantiene al frente para reintentar en el siguiente bombeo.
		BombearColaAcciones();
	}

	// Sondea el WebSocket (~20 Hz). Un paquete del rival = "hay algo nuevo" → leer sus acciones YA por
	// REST, sin esperar el timer de 0.35s. Es lo que hace que el multijugador se sienta en tiempo real,
	// manteniendo el REST como ÚNICA fuente de verdad (sin carreras ni duplicados).
	private void PollWs()
	{
		if (_ws == null) return;
		_ws.Poll();
		var estado = _ws.GetReadyState();
		if (estado == WebSocketPeer.State.Open)
		{
			_wsConectado = true;
			bool aviso = false;
			while (_ws.GetAvailablePacketCount() > 0) { _ws.GetPacket(); aviso = true; }
			if (aviso && !esTurnoJugador && !juegoTerminado) SondearAcciones();
		}
		else if (estado == WebSocketPeer.State.Closed)
		{
			_wsConectado = false; // el sondeo REST de 0.35s sigue cubriendo todo (respaldo)
		}
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

	// Corre una reproducción en modo "solo visual" (sin mutar estado) y SIEMPRE resetea el flag, aunque
	// falle. Así una habilidad rara del rival no puede crashear ni dejar el flag pegado (que bloquearía
	// el daño real en el resto de la partida).
	private void EjecutarVisualOnline(Action accion)
	{
		SoloVisualOnline = true;
		try { accion(); }
		catch (Exception e) { GD.PrintErr($"[Online] Error reproduciendo efecto visual: {e.Message}"); }
		finally { SoloVisualOnline = false; }
	}

	private void ReproducirAccion(string accionJson)
	{
		if (string.IsNullOrEmpty(accionJson)) return;
		JsonElement acc;
		try { acc = JsonSerializer.Deserialize<JsonElement>(accionJson); } catch { return; }

		// El log de acciones es COMPARTIDO: al empezar a sondear también me traigo las mías. Se saltan
		// (el contador ya avanzó en OnRespuestaAcciones); solo reproduzco las del rival.
		string autor = acc.TryGetProperty("autor", out var au) ? (au.GetString() ?? "") : "";
		if (autor == ContextoOnline.Asiento) return;

		string tipo = acc.TryGetProperty("tipo", out var t) ? (t.GetString() ?? "") : "";
		JsonElement datos = acc.TryGetProperty("datos", out var d) ? d : default;

		// Reproducción VISUAL de la jugada del rival: se ejecuta la MISMA acción real (animación +
		// efectos: proyectiles, fuego, misiles, etc.) pero en modo "solo visual" (SoloVisualOnline) →
		// no aplica daño ni muerte; los números autoritativos llegan por el snapshot. Así el online se
		// ve como VS BOT (ataques y habilidades con sus efectos) sin duplicar el daño.
		if (tipo == "atacar" && datos.ValueKind == JsonValueKind.Object &&
			datos.TryGetProperty("carrilAtacante", out var ca))
		{
			string carrilMio = EspejarCarril(ca.GetString() ?? "");
			if (TropaEnCarril(carrilMio) is TropaBase atk && IsInstanceValid(atk))
				EjecutarVisualOnline(() => atk.EjecutarAccion("atacar"));
		}
		else if (tipo == "habilidad" && datos.ValueKind == JsonValueKind.Object &&
			datos.TryGetProperty("carrilHab", out var ch))
		{
			string carrilMio = EspejarCarril(ch.GetString() ?? "");
			if (TropaEnCarril(carrilMio) is TropaBase hab && IsInstanceValid(hab))
				EjecutarVisualOnline(() => hab.EjecutarAccion("usar_habilidad"));
		}
		else if (tipo == "hechizo" && datos.ValueKind == JsonValueKind.Object &&
			datos.TryGetProperty("hechizoId", out var hid) && datos.TryGetProperty("carrilObjetivo", out var cobj))
		{
			string carrilMio = EspejarCarril(cobj.GetString() ?? "");
			if (TropaEnCarril(carrilMio) is Node2D obj && IsInstanceValid(obj))
				EjecutarVisualOnline(() => ReproducirHechizoVisual(hid.GetString() ?? "", obj));
		}
		else if (tipo == "defensa" && datos.ValueKind == JsonValueKind.Object &&
			datos.TryGetProperty("carrilDef", out var cd))
		{
			string carrilMio = EspejarCarril(cd.GetString() ?? "");
			if (TropaEnCarril(carrilMio) is TropaBase def && IsInstanceValid(def))
				EjecutarVisualOnline(() => def.EjecutarAccion("preparar_defensa"));
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
				if (actual != null) MatarTropaVisualOnline(carril); // murió: "derrota" + desvanecer
				continue;
			}

			string escenaDeseada = td.GetProperty("escena").GetString() ?? "";
			bool miLado = !carril.StartsWith("ModRival");

			if (actual != null && IsInstanceValid(actual) && actual.SceneFilePath == escenaDeseada)
			{
				// Misma tropa: solo actualizar stats (preserva su animación en curso).
				int vidaAntes = actual.vidaActual;
				actual.FijarStats(
					td.GetProperty("vida").GetInt32(), td.GetProperty("vidaMax").GetInt32(),
					td.GetProperty("escudo").GetInt32(), td.GetProperty("escudoMax").GetInt32(),
					td.GetProperty("turnoCarta").GetInt32(), td.GetProperty("habUsada").GetBoolean());
				if (td.TryGetProperty("ataque", out var atkTd)) actual.puntosAtaque = atkTd.GetInt32();

				// Reacción de golpe: si perdió vida desde la última foto, animación de daño + número
				// flotante (cubre ataques, veneno, cualquier fuente — no solo "atacar").
				int golpe = vidaAntes - actual.vidaActual;
				if (golpe > 0)
				{
					actual.EjecutarAccion("recibir_daño"); // reproduce "daño" y vuelve a idle
					MostrarDañoFlotante(actual.GlobalPosition, golpe);
				}
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

		// Fin de partida: NO se decide localmente (los dos podrían verse ganando). Se reporta al
		// servidor y se muestra el resultado autoritativo (ver EnviarResultadoOnline).
		if (vidaJugador <= 0) EnviarResultadoOnline("rival");     // mi huevo murió → ganó el rival
		else if (vidaRival <= 0) EnviarResultadoOnline("yo");     // el huevo rival murió → gané yo
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
					{ "ataque", tb.puntosAtaque }, // incluye buffs (p. ej. hechizo Fuerza)
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
		if (!EsOnline || juegoTerminado) return;
		BombearColaAcciones(); // red de seguridad: reintenta acciones que no hayan salido
		if (_ocupadoLatido) return;
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
			else if (rivalCaido)
			{
				MostrarAviso("El rival se desconectó. ¡Ganaste!", new Color(0.5f, 1f, 0.6f));
				ResolverResultadoOnline("gano_" + ContextoOnline.Asiento);
			}
		}
		catch { }
	}

	// Muestra el resultado AUTORITATIVO (viene del servidor). "gano_<miAsiento>" = gané; "empate";
	// cualquier otro = perdí. Se usa tanto para desconexión (latido) como para fin normal (arbitraje).
	private void ResolverResultadoOnline(string resultado)
	{
		if (juegoTerminado) return;
		if (_timerLatido != null && !_timerLatido.IsStopped()) _timerLatido.Stop();
		if (_timerPollAcc != null && !_timerPollAcc.IsStopped()) _timerPollAcc.Stop();
		if (_timerWs != null && !_timerWs.IsStopped()) _timerWs.Stop();
		if (_ws != null && _ws.GetReadyState() == WebSocketPeer.State.Open) _ws.Close();

		if (resultado == "empate") FinalizarPartida("¡EMPATE!");
		else if (resultado == "gano_" + ContextoOnline.Asiento) FinalizarPartida("¡VICTORIA!");
		else FinalizarPartida("¡DERROTA!");
	}

	// Reporta el fin de partida NORMAL (huevo a 0 o por tiempo) al servidor, que ARBITRA (el primer
	// reporte gana), y muestra el resultado autoritativo → ambos clientes ven lo mismo, nunca "los dos
	// ganan". quien: "yo" gané | "rival" ganó (perdí) | "empate". Si la red falla, cae al local.
	private void EnviarResultadoOnline(string quien)
	{
		if (!EsOnline || _finOnlineEnviado || juegoTerminado) return;
		_finOnlineEnviado = true;

		string ganador = quien == "yo"    ? "gano_" + ContextoOnline.Asiento
					   : quien == "rival" ? "gano_" + (ContextoOnline.SoyPrimero ? "B" : "A")
					   :                     "empate";

		if (_timerPollAcc != null && !_timerPollAcc.IsStopped()) _timerPollAcc.Stop();

		_httpResultado = new HttpRequest();
		AddChild(_httpResultado);
		_httpResultado.RequestCompleted += (long r, long c, string[] h, byte[] b) =>
		{
			string autoritativo = ganador; // respaldo si el server no responde
			if (r == (long)HttpRequest.Result.Success && c == 200)
			{
				try
				{
					var doc = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(b));
					if (doc.TryGetProperty("resultado", out var rr))
					{
						string s = rr.GetString() ?? "";
						if (!string.IsNullOrEmpty(s)) autoritativo = s;
					}
				}
				catch { }
			}
			ResolverResultadoOnline(autoritativo);
		};
		string cuerpo = JsonSerializer.Serialize(new { jugadorId = ContextoOnline.JugadorId, ganador });
		string[] headers = { "Content-Type: application/json" };
		if (_httpResultado.Request($"{ApiConfig.Base}/api/match/{ContextoOnline.MatchId}/resultado", headers, HttpClient.Method.Post, cuerpo) != Error.Ok)
			ResolverResultadoOnline(ganador);
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

	// Muerte SOLO visual para la reconciliación: reproduce "derrota", libera el carril y desvanece la
	// tropa (igual que EjecutarMuerteTropaSacrificada) pero SIN tocar la vida de la base ni los
	// contadores — eso ya viene aplicado en el snapshot del rival, aplicarlo aquí lo duplicaría.
	private void MatarTropaVisualOnline(string carril)
	{
		Node2D zona = GetTree().Root.FindChild(carril, true, false) as Node2D;
		var ocup = zona?.GetNodeOrNull("Ocupado");
		if (ocup == null) return;
		Node2D tropa = ocup.HasMeta("tropa_instanciada") ? ocup.GetMeta("tropa_instanciada").AsGodotObject() as Node2D : null;
		ocup.Free(); // el carril queda libre de inmediato para futuras reconciliaciones
		if (tropa == null || !IsInstanceValid(tropa)) return;

		var animSprite = tropa.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		bool yaEnDerrota = animSprite != null && ((string)animSprite.Animation).Contains("derrota");
		if (!yaEnDerrota && tropa.HasMethod("ReproducirDerrota")) tropa.Call("ReproducirDerrota");

		// Mismo criterio que EjecutarMuerteTropaSacrificada: desvanecer desde el frame 17 de
		// "derrota" (19 para Ka-Bar), no con un timer fijo — ver ese método para el por qué.
		int frameDesvanecer = tropa is KaBarCartoonPrime ? 19 : 17;
		TropaBase.DesvanecerTrasFrameDerrota(tropa, animSprite, frameDesvanecer, 0.6f);
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
