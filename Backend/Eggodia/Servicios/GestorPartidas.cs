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
        public string Semilla { get; set; } = new Random().Next(1, int.MaxValue).ToString(); // RNG compartido (Fase 2)
        public DateTime ActualizadaUtc { get; set; } = DateTime.UtcNow;

        // Sincronización de turnos (Fase 2 Milestone B): el jugador activo sube el snapshot del
        // tablero con un número de turno creciente; el otro lo baja sondeando.
        public int TurnoActual { get; set; } = 0;
        public string EstadoJson { get; set; } = "";

        // Detección de desconexión (latido por asiento) y resultado por abandono/inactividad.
        public DateTime VistoA { get; set; } = DateTime.UtcNow;
        public DateTime VistoB { get; set; } = DateTime.UtcNow;
        public string Resultado { get; set; } = ""; // "" | gano_A | gano_B | empate
    }

    private const int SEG_DESCONEXION = 12;

    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, Partida> _partidas = new();

    public Partida EntrarACola(string jugadorId, string nombre)
    {
        lock (_lock)
        {
            LimpiarViejas();

            // Reingreso (doble toque / reintento): si este jugador ya tiene una partida activa, devolverla.
            var propia = _partidas.Values.FirstOrDefault(p =>
                p.JugadorAId == jugadorId || p.JugadorBId == jugadorId);
            if (propia != null) { propia.ActualizadaUtc = DateTime.UtcNow; return propia; }

            // Hay alguien esperando (de otro jugador) → emparejar.
            var esperando = _partidas.Values.FirstOrDefault(p =>
                p.Estado == "esperando" && p.JugadorAId != jugadorId);
            if (esperando != null)
            {
                esperando.JugadorBId = jugadorId;
                esperando.JugadorBNombre = nombre;
                esperando.Estado = "emparejado";
                esperando.ActualizadaUtc = DateTime.UtcNow;
                esperando.VistoA = esperando.VistoB = DateTime.UtcNow; // arranca el latido de ambos
                return esperando;
            }

            // Nadie esperando → crear partida nueva y quedar a la espera.
            var nueva = new Partida { JugadorAId = jugadorId, JugadorANombre = nombre };
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
        if (soyA) p.VistoA = ahora; else if (soyB) p.VistoB = ahora;
        p.ActualizadaUtc = ahora;

        bool caidoA = (ahora - p.VistoA).TotalSeconds > SEG_DESCONEXION;
        bool caidoB = (ahora - p.VistoB).TotalSeconds > SEG_DESCONEXION;

        if (string.IsNullOrEmpty(p.Resultado) && p.Estado == "emparejado")
        {
            if (caidoA && caidoB) p.Resultado = "empate";
            else if (caidoA)      p.Resultado = "gano_B";
            else if (caidoB)      p.Resultado = "gano_A";
        }

        bool rivalCaido = soyA ? caidoB : (soyB ? caidoA : false);
        return (rivalCaido, p.Resultado);
    }

    private void LimpiarViejas()
    {
        // 90s: tolera turnos largos (el sondeo del rival mantiene viva la partida igual).
        var limite = DateTime.UtcNow.AddSeconds(-90);
        foreach (var kv in _partidas)
            if (kv.Value.ActualizadaUtc < limite) _partidas.TryRemove(kv.Key, out _);
    }
}
