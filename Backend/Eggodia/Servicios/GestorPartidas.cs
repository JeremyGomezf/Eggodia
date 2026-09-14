using System.Collections.Concurrent;

/// <summary>
/// Matchmaking en memoria (Fase 1). Empareja jugadores 1v1 por sondeo (polling REST):
/// el primero que entra queda "esperando"; el segundo lo toma y ambos pasan a "emparejado".
/// Las partidas sin actividad (nadie sondea) se limpian solas a los 30s.
///
/// Es un singleton (ver Program.cs). En memoria a propósito: si el servicio se reinicia, las colas
/// se vacían — aceptable para emparejamiento. La lógica de partida real vendrá en la Fase 2.
/// </summary>
public class GestorPartidas
{
    public class Partida
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string JugadorAId { get; set; } = "";
        public string JugadorANombre { get; set; } = "";
        public string? JugadorBId { get; set; }
        public string? JugadorBNombre { get; set; }
        public string Estado { get; set; } = "esperando"; // esperando | emparejado

        // Skin de huevo y trono equipados por cada jugador (índices de Preferencias.SKIN_*/
        // TRONO_* en el cliente) — se mandan una sola vez al entrar a la cola, ya que no cambian
        // a mitad de partida, y se usan para que el rival vea la skin/trono REAL, no uno sorteado.
        public int SkinIdxA { get; set; } = 0;
        public int TronoIdxA { get; set; } = 0;
        public int SkinIdxB { get; set; } = 0;
        public int TronoIdxB { get; set; } = 0;
        public string Semilla { get; set; } = new Random().Next(1, int.MaxValue).ToString(); // RNG compartido (Fase 2)
        public DateTime ActualizadaUtc { get; set; } = DateTime.UtcNow;

        // Sincronización de turnos (Fase 2 Milestone B): el jugador activo sube el snapshot del
        // tablero con un número de turno creciente; el otro lo baja sondeando.
        public int TurnoActual { get; set; } = 0;
        public string EstadoJson { get; set; } = "";

        // Fase 2 (acciones en vivo): lista ordenada de acciones (JSON) que el rival reproduce.
        public List<string> Acciones { get; } = new();

        // Detección de desconexión (latido por asiento) y resultado por abandono/inactividad.
        public DateTime VistoA { get; set; } = DateTime.UtcNow;
        public DateTime VistoB { get; set; } = DateTime.UtcNow;
        // Un asiento "entró" cuando mandó su PRIMER latido (ya terminó de cargar la escena). Solo se
        // puede considerar "caído" a quien ya entró; así el jugador que aún carga no pierde por error.
        public bool EntroA { get; set; } = false;
        public bool EntroB { get; set; } = false;
        public string Resultado { get; set; } = ""; // "" | gano_A | gano_B | empate
    }

    private const int SEG_DESCONEXION = 12;

    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, Partida> _partidas = new();

    public Partida EntrarACola(string jugadorId, string nombre, int skinIdx = 0, int tronoIdx = 0)
    {
        lock (_lock)
        {
            LimpiarViejas();

            // Reingreso a la COLA (doble toque / reintento del matchmaking): si este jugador ya tiene
            // una partida propia TODAVÍA "esperando" rival, devolver esa misma. NO se reingresa a una
            // partida ya "emparejado": si abandonó una anterior (o quedó a medias en una prueba), esa
            // tiene su VistoX viejo y lo declararía caído al instante → derrota falsa apenas entra.
            // Esas partidas viejas se resuelven solas (latido del rival) o se limpian a los 90s.
            var propia = _partidas.Values.FirstOrDefault(p =>
                p.Estado == "esperando" && p.JugadorAId == jugadorId);
            if (propia != null) { propia.ActualizadaUtc = DateTime.UtcNow; return propia; }

            // Hay alguien esperando (de otro jugador) → emparejar.
            var esperando = _partidas.Values.FirstOrDefault(p =>
                p.Estado == "esperando" && p.JugadorAId != jugadorId);
            if (esperando != null)
            {
                esperando.JugadorBId = jugadorId;
                esperando.JugadorBNombre = nombre;
                esperando.SkinIdxB = skinIdx;
                esperando.TronoIdxB = tronoIdx;
                esperando.Estado = "emparejado";
                esperando.ActualizadaUtc = DateTime.UtcNow;
                esperando.VistoA = esperando.VistoB = DateTime.UtcNow; // arranca el latido de ambos
                return esperando;
            }

            // Nadie esperando → crear partida nueva y quedar a la espera.
            var nueva = new Partida { JugadorAId = jugadorId, JugadorANombre = nombre, SkinIdxA = skinIdx, TronoIdxA = tronoIdx };
            _partidas[nueva.Id] = nueva;
            return nueva;
        }
    }

    public Partida? Obtener(string id)
    {
        if (_partidas.TryGetValue(id, out var p))
        {
            p.ActualizadaUtc = DateTime.UtcNow; // el sondeo mantiene viva la partida
            return p;
        }
        return null;
    }

    public void Cancelar(string id, string jugadorId)
    {
        lock (_lock)
        {
            if (_partidas.TryGetValue(id, out var p) && p.Estado == "esperando" && p.JugadorAId == jugadorId)
                _partidas.TryRemove(id, out _);
        }
    }

    // ── Sincronización de turnos (snapshot del tablero) ──────────────────
    public bool GuardarTurno(string id, int turno, string estadoJson)
    {
        if (!_partidas.TryGetValue(id, out var p)) return false;
        if (turno > p.TurnoActual) { p.TurnoActual = turno; p.EstadoJson = estadoJson ?? ""; }
        p.ActualizadaUtc = DateTime.UtcNow;
        return true;
    }

    public (int turno, string estado)? ObtenerTurno(string id)
    {
        if (!_partidas.TryGetValue(id, out var p)) return null;
        p.ActualizadaUtc = DateTime.UtcNow; // el sondeo mantiene viva la partida durante el juego
        return (p.TurnoActual, p.EstadoJson);
    }

    // ── Latido / detección de desconexión ────────────────────────────────
    // Cada cliente late cada pocos segundos. Si el rival no late en 12s (desconexión, cierre de app
    // o inactividad total), el que sigue latiendo gana. Devuelve (rivalCaido, resultado).
    public (bool rivalCaido, string resultado) LatidoYEstado(string id, string jugadorId)
    {
        if (!_partidas.TryGetValue(id, out var p)) return (false, "");
        var ahora = DateTime.UtcNow;
        bool soyA = p.JugadorAId == jugadorId;
        bool soyB = p.JugadorBId == jugadorId;
        if (soyA) { p.VistoA = ahora; p.EntroA = true; } else if (soyB) { p.VistoB = ahora; p.EntroB = true; }
        p.ActualizadaUtc = ahora;

        // Solo es "caído" quien YA entró (latió alguna vez) y luego se calló >12s. El que aún carga
        // la escena no cuenta como caído — así el jugador lento no pierde apenas entra.
        bool caidoA = p.EntroA && (ahora - p.VistoA).TotalSeconds > SEG_DESCONEXION;
        bool caidoB = p.EntroB && (ahora - p.VistoB).TotalSeconds > SEG_DESCONEXION;

        // El resultado por abandono solo se decide cuando AMBOS ya entraron a la partida.
        if (string.IsNullOrEmpty(p.Resultado) && p.Estado == "emparejado" && p.EntroA && p.EntroB)
        {
            if (caidoA && caidoB) p.Resultado = "empate";
            else if (caidoA)      p.Resultado = "gano_B";
            else if (caidoB)      p.Resultado = "gano_A";
        }

        bool rivalCaido = soyA ? caidoB : (soyB ? caidoA : false);
        return (rivalCaido, p.Resultado);
    }

    // ── Acciones en vivo (invocar/atacar/habilidad/hechizo/fin_turno) ─────
    // Devuelve el índice (0-based) de la acción recién agregada.
    public int AgregarAccion(string id, string accionJson)
    {
        if (!_partidas.TryGetValue(id, out var p)) return -1;
        lock (_lock)
        {
            p.Acciones.Add(accionJson ?? "");
            p.ActualizadaUtc = DateTime.UtcNow;
            return p.Acciones.Count - 1;
        }
    }

    // Devuelve las acciones con índice > desde (las que el rival aún no reprodujo).
    public (int total, List<string> nuevas)? AccionesDesde(string id, int desde)
    {
        if (!_partidas.TryGetValue(id, out var p)) return null;
        p.ActualizadaUtc = DateTime.UtcNow;
        lock (_lock)
        {
            var nuevas = new List<string>();
            for (int i = desde + 1; i < p.Acciones.Count; i++) nuevas.Add(p.Acciones[i]);
            return (p.Acciones.Count, nuevas);
        }
    }

    private void LimpiarViejas()
    {
        // 90s: tolera turnos largos (el sondeo del rival mantiene viva la partida igual).
        var limite = DateTime.UtcNow.AddSeconds(-90);
        foreach (var kv in _partidas)
            if (kv.Value.ActualizadaUtc < limite) _partidas.TryRemove(kv.Key, out _);
    }
}
