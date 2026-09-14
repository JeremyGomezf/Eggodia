using Godot;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

/// <summary>
/// Multijugador en línea dentro de la batalla (Fase 2, Milestone B).
/// Modelo "snapshot": el jugador activo juega su turno localmente y, al terminarlo, envía una foto
/// del tablero (POST /api/match/{id}/turno). El rival la baja sondeando (GET .../turno?desde=N),
/// reconstruye el tablero ESPEJADO (mi lado ↔ su lado, ambos se ven a la izquierda) y le toca jugar.
/// </summary>
public partial class Campo1 : Node2D
{
	private static readonly string[] ZONAS_ONLINE = { "Mod1", "Mod2", "Mod3", "ModRival1", "ModRival2", "ModRival3" };

	private int _turnoOnline = 0;
	private bool _ocupadoSondeo = false;
	private HttpRequest _httpEnviar, _httpSondeo;
	private Timer _timerSondeoTurno;

	private HttpRequest _httpLatido;
	private Timer _timerLatido;
	private bool _ocupadoLatido = false;

	private void ConfigurarModoOnline()
	{
		EsOnline = ContextoOnline.Activo;
		if (!EsOnline) return; // el nombre del rival ya sale sobre su barra (ver Campo1.Extra.cs)

		_httpEnviar = new HttpRequest(); AddChild(_httpEnviar);
		_httpSondeo = new HttpRequest(); AddChild(_httpSondeo);
		_httpSondeo.RequestCompleted += OnRespuestaTurno;

		_timerSondeoTurno = new Timer { WaitTime = 1.2, OneShot = false };
		AddChild(_timerSondeoTurno);
		_timerSondeoTurno.Timeout += SondearTurno;

		// Heartbeat: late cada 3s toda la partida. Si el rival deja de latir 12s (se desconecta,
		// cierra la app o queda inactivo), este cliente gana; si ambos, empate.
		_httpLatido = new HttpRequest(); AddChild(_httpLatido);
		_httpLatido.RequestCompleted += OnRespuestaLatido;
		_timerLatido = new Timer { WaitTime = 3.0, OneShot = false };
		AddChild(_timerLatido);
		_timerLatido.Timeout += EnviarLatido;
		_timerLatido.Start();

		// El asiento "A" empieza; el "B" espera el primer turno del rival.
		if (!ContextoOnline.SoyPrimero)
		{
			esTurnoJugador = false;
			movimientosRestantes = 0;
			AnunciarTurno();
			IniciarEsperaOnline();
		}
	}

	// Llamado cuando termina MI turno (la CPU está gateada en online, ver EjecutarTurnoCPU):
	// envío la foto del tablero y me pongo a esperar la del rival.
	private void EsperarRivalOnline()
	{
		if (!EsOnline) return;
		EnviarSnapshotOnline();
		IniciarEsperaOnline();
	}

	private void IniciarEsperaOnline()
	{
		MostrarAviso("Esperando al rival…", new Color(1f, 0.85f, 0.4f));
		if (_timerSondeoTurno != null && _timerSondeoTurno.IsStopped()) _timerSondeoTurno.Start();
	}

	private void SondearTurno()
	{
		if (!EsOnline || esTurnoJugador || juegoTerminado || _ocupadoSondeo) return;
		_ocupadoSondeo = true;
		if (_httpSondeo.Request($"{ApiConfig.Base}/api/match/{ContextoOnline.MatchId}/turno?desde={_turnoOnline}") != Error.Ok)
			_ocupadoSondeo = false;
	}

	private void OnRespuestaTurno(long result, long code, string[] headers, byte[] body)
	{
		_ocupadoSondeo = false;
		if (result != (long)HttpRequest.Result.Success || code != 200) return;
		try
		{
			var doc = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(body));
			int turno = doc.TryGetProperty("turno", out var t) ? t.GetInt32() : 0;
			string estado = doc.TryGetProperty("estado", out var e) ? (e.GetString() ?? "") : "";
			if (turno > _turnoOnline && !string.IsNullOrEmpty(estado))
			{
				_turnoOnline = turno;
				if (!_timerSondeoTurno.IsStopped()) _timerSondeoTurno.Stop();
				AdoptarSnapshot(estado);
				if (!juegoTerminado) IniciarMiTurnoOnline();
			}
		}
		catch { }
	}

	private void EnviarSnapshotOnline()
	{
		string tablero = SerializarTablero();
		int turno = _turnoOnline + 1;
		_turnoOnline = turno;
		string cuerpo = JsonSerializer.Serialize(new { jugadorId = ContextoOnline.JugadorId, turno, estado = tablero });
		string[] headers = { "Content-Type: application/json" };
		_httpEnviar.Request($"{ApiConfig.Base}/api/match/{ContextoOnline.MatchId}/turno", headers, HttpClient.Method.Post, cuerpo);
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
		if (_timerSondeoTurno != null && !_timerSondeoTurno.IsStopped()) _timerSondeoTurno.Stop();

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
		else
		{
			FinalizarPartida("¡DERROTA!");
		}
	}

	// ── SERIALIZAR (foto del tablero desde mi perspectiva) ────────────────────
	private string SerializarTablero()
	{
		var tropas = new List<object>();
		foreach (string z in ZONAS_ONLINE)
		{
			if (TropaEnCarril(z) is TropaBase tb && IsInstanceValid(tb))
				tropas.Add(new
				{
					carril = z,
					escena = tb.SceneFilePath,
					vida = tb.vidaActual,
					vidaMax = tb.vidaMaxima,
					escudo = tb.escudoActual,
					escudoMax = tb.escudoMaximo,
					turnoCarta = tb.turnoActualCarta,
					habUsada = tb.habilidadUsada
				});
		}
		return JsonSerializer.Serialize(new
		{
			vidaJugador,
			vidaRival,
			faseApertura = _faseApertura,
			tropas
		});
	}

	// ── ADOPTAR (reconstruir el tablero espejado con lo que envió el rival) ────
	private void AdoptarSnapshot(string json)
	{
		JsonElement doc;
		try { doc = JsonSerializer.Deserialize<JsonElement>(json); } catch { return; }

		// Espejo: lo que para el emisor es "su" huevo, para mí es el del rival, y viceversa.
		if (doc.TryGetProperty("vidaRival", out var vr)) vidaJugador = vr.GetInt32();
		if (doc.TryGetProperty("vidaJugador", out var vj)) vidaRival = vj.GetInt32();

		// Limpiar los 6 carriles antes de reconstruir.
		foreach (string z in ZONAS_ONLINE) QuitarTropaDeCarril(z);

		if (doc.TryGetProperty("tropas", out var arr) && arr.ValueKind == JsonValueKind.Array)
		{
			foreach (var tr in arr.EnumerateArray())
			{
				string carrilEmisor = tr.GetProperty("carril").GetString() ?? "";
				string carrilDestino = EspejarCarril(carrilEmisor); // su Mod ↔ mi ModRival
				if (string.IsNullOrEmpty(carrilDestino)) continue;
				bool miLado = !carrilDestino.StartsWith("ModRival");
				ColocarTropaAdoptada(
					carrilDestino,
					tr.GetProperty("escena").GetString() ?? "",
					tr.GetProperty("vida").GetInt32(),
					tr.GetProperty("vidaMax").GetInt32(),
					tr.GetProperty("escudo").GetInt32(),
					tr.GetProperty("escudoMax").GetInt32(),
					tr.GetProperty("turnoCarta").GetInt32(),
					tr.GetProperty("habUsada").GetBoolean(),
					miLado);
			}
		}

		// La apertura termina cuando ya están llenos los 6 carriles (ambos completaron su formación).
		if (TodosLosSeisLlenos()) _faseApertura = false;

		ActualizarInterfaz();

		// Fin de partida sincronizado por la vida de los huevos.
		if (vidaJugador <= 0) FinalizarPartida("DERROTA");
		else if (vidaRival <= 0) FinalizarPartida("VICTORIA");
	}

	private void ColocarTropaAdoptada(string carril, string escena, int vida, int vidaMax, int escudo, int escudoMax, int turnoCarta, bool habUsada, bool miLado)
	{
		if (string.IsNullOrEmpty(escena) || !ResourceLoader.Exists(escena)) return;
		Node2D zona = GetTree().Root.FindChild(carril, true, false) as Node2D;
		if (zona == null) return;
		var packed = GD.Load<PackedScene>(escena);
		if (packed == null) return;

		Node2D t = (Node2D)packed.Instantiate();
		AddChild(t); // aquí corre su _Ready (pone stats por defecto), luego los sobreescribimos
		t.AddToGroup(miLado ? "tropas_jugador" : "tropas_rival");

		if (t is TropaBase tb)
		{
			tb.vidaMaxima = vidaMax; tb.vidaActual = vida;
			tb.escudoMaximo = escudoMax; tb.escudoActual = escudo;
			tb.turnoActualCarta = turnoCarta; tb.habilidadUsada = habUsada;
			tb.ColocarPorCentroColision(zona.GlobalPosition);
			tb.RefrescarBarras();
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

	// ── INICIAR MI TURNO (tras adoptar el snapshot del rival) ─────────────────
	private void IniciarMiTurnoOnline()
	{
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
