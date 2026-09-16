using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Net.WebSockets;
using System.Text;

/// <summary>
/// Endpoint WebSocket del multijugador en tiempo real:
///
///   ws(s)://HOST/ws/match?matchId=XXXX&jugadorId=uNN
///
/// El cliente abre el socket al entrar a la partida; cada acción que envía se retransmite al
/// instante al rival de la misma sala (matchId). Es ADITIVO: si el WS falla, el cliente sigue
/// usando el sondeo REST (/api/match/{id}/acciones) como respaldo.
///
/// NOTA DE DESPLIEGUE: para que el WS pase por Cloudflare Tunnel + Nginx, Nginx debe reenviar el
/// upgrade — en el location del proxy: `proxy_set_header Upgrade $http_upgrade;` y
/// `proxy_set_header Connection "upgrade";` (hoy está en "keep-alive"). Sin eso, /ws/ da 400/502.
/// </summary>
[ApiController]
public class MatchSocketController : ControllerBase
{
    private readonly SalasWebSocket _salas;
    public MatchSocketController(SalasWebSocket salas) { _salas = salas; }

    [HttpGet("/ws/match")]
    public async Task Conectar([FromQuery] string matchId = "", [FromQuery] string jugadorId = "")
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest) { HttpContext.Response.StatusCode = 400; return; }
        if (string.IsNullOrWhiteSpace(matchId))         { HttpContext.Response.StatusCode = 400; return; }

        using var ws = await HttpContext.WebSockets.AcceptWebSocketAsync();
        var conexion = new SalasWebSocket.Conexion { JugadorId = jugadorId, Socket = ws };
        _salas.Unir(matchId, conexion);

        var buffer = new byte[16 * 1024];
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;

                // Reunir el mensaje completo (puede llegar fragmentado en varios frames).
                using var ms = new MemoryStream();
                ms.Write(buffer, 0, result.Count);
                while (!result.EndOfMessage)
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    ms.Write(buffer, 0, result.Count);
                }

                string mensaje = Encoding.UTF8.GetString(ms.ToArray());
                if (!string.IsNullOrEmpty(mensaje))
                    await _salas.Retransmitir(matchId, conexion, mensaje);
            }
        }
        catch { /* desconexión abrupta: se maneja en finally */ }
        finally
        {
            _salas.Salir(matchId, conexion);
            if (ws.State == WebSocketState.Open)
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); } catch { }
        }
    }
}
