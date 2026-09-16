using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

/// <summary>
/// Relay de WebSockets para el multijugador en TIEMPO REAL (push, sin sondeo).
///
/// Agrupa las conexiones por matchId ("sala"). Cuando un jugador envía un mensaje, se reenvía al
/// instante a los demás de la misma sala. Es ADITIVO: convive con el sondeo REST (/api/match/...),
/// que sigue funcionando como respaldo si el WebSocket no está disponible.
///
/// Singleton (ver Program.cs). El protocolo de mensajes es el MISMO JSON de acción que ya usa el
/// cliente ({tipo, autor, datos, snapshot}), así se reutiliza toda la lógica de reproducción.
/// </summary>
public class SalasWebSocket
{
    public class Conexion
    {
        public string JugadorId = "";
        public WebSocket Socket = null!;
    }

    private readonly ConcurrentDictionary<string, List<Conexion>> _salas = new();

    public void Unir(string matchId, Conexion c)
    {
        var lista = _salas.GetOrAdd(matchId, _ => new List<Conexion>());
        lock (lista) lista.Add(c);
    }

    public void Salir(string matchId, Conexion c)
    {
        if (_salas.TryGetValue(matchId, out var lista))
        {
            lock (lista) lista.Remove(c);
            lock (lista) { if (lista.Count == 0) _salas.TryRemove(matchId, out _); }
        }
    }

    /// <summary>Cuántos están conectados por WS en esta sala (0, 1 o 2).</summary>
    public int Conectados(string matchId) =>
        _salas.TryGetValue(matchId, out var lista) ? lista.Count : 0;

    /// <summary>Reenvía 'mensaje' a todos en la sala EXCEPTO al emisor (relay 1→otros).</summary>
    public async Task Retransmitir(string matchId, Conexion emisor, string mensaje)
    {
        if (!_salas.TryGetValue(matchId, out var lista)) return;
        Conexion[] destinos;
        lock (lista)
            destinos = lista.Where(x => x != emisor && x.Socket.State == WebSocketState.Open).ToArray();

        var bytes = Encoding.UTF8.GetBytes(mensaje);
        foreach (var d in destinos)
        {
            try { await d.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None); }
            catch { /* conexión rota: se limpia sola al cerrarse su bucle de recepción */ }
        }
    }
}
