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
        public string Resultado { get; set; } = ""; // "" | gano_A | gano_B | empate | cancelada
        // Por qué terminó: normal | desconexion | inactividad | rendicion | rival_no_entro. El cliente lo
        // usa para explicar el resultado ("perdiste por inactividad", "el rival se rindió"...).
        public string Motivo { get; set; } = "";
        public DateTime EmparejadaUtc { get; set; } = DateTime.MinValue;

        // Idempotencia de acciones: "autor:seq" → índice ya guardado. Si un POST se reintenta tras un
        // corte (llegó al servidor pero la respuesta se perdió), no se duplica la jugada.
        public Dictionary<string, int> IndicePorSecuencia { get; } = new();

        // Inactividad: acciones hechas en el turno en curso y turnos seguidos que se acabaron por
        // tiempo sin jugar nada, por asiento.
        public int AccionesTurnoA { get; set; }
        public int AccionesTurnoB { get; set; }
        public int TurnosInactivosA { get; set; }
        public int TurnosInactivosB { get; set; }

        // De quién es el turno (empieza A; cambia con cada fin_turno) y desde cuándo. Red de seguridad
        // para un rival conectado pero trabado (su reloj de turno no corre): ver SEG_TURNO_MAX.
        public string TurnoDe { get; set; } = "A";
        public DateTime InicioTurnoUtc { get; set; } = DateTime.MinValue;

        // Rival BOT: si nadie más busca partida, a los BotTrasSegundos el servidor le pone un rival
        // con nombre/skin/trono de jugador común. El cliente lo juega contra la CPU (el bot del VS BOT), pero se ve y
        // se cobra como online. El asiento B es el bot (nunca se cae ni juega "inactivo").
        public bool EsBot { get; set; } = false;
        public DateTime CreadaUtc { get; set; } = DateTime.UtcNow;
    }

    // Segundos sin latido para dar por caído a un jugador. Cubre salir un momento de la app, cambiar
    // de WiFi a datos o un túnel corto sin perder la partida.
    private const int SEG_DESCONEXION = 20;
    // Tiempo máximo para que AMBOS entren a la partida tras emparejar (un celular lento tarda ~10-15s
    // en cargar). Si solo uno entró, la partida se cancela sin ganador.
    private const int SEG_ENTRADA = 45;
    // Un jugador en cola que no sondea hace más de esto cerró la app o perdió la conexión mientras
    // buscaba: no se lo empareja con nadie (antes el otro quedaba esperando a un "fantasma").
    private const int SEG_COLA_VIVA = 8;
    // Turnos SEGUIDOS que se acaban por tiempo sin jugar nada → pierde por inactividad.
    private const int TURNOS_INACTIVO_MAX = 3;
    // Un turno dura 20 s en el cliente. Si uno lleva más de esto sin pasar el turno (sigue latiendo
    // pero no juega ni termina el turno: cliente trabado), pierde por inactividad y el rival no queda
    // esperando para siempre. Holgado a propósito: intro, animaciones, la Nuclear, reconexiones.
    private const int SEG_TURNO_MAX = 90;

    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, Partida> _partidas = new();

    // Segundos de espera en la cola antes de darle un rival bot (appsettings Online:BotTrasSegundos;
    // 0 = sin bots). Un jugador real que entre antes siempre tiene prioridad: se empareja con él.
    private readonly int _botTrasSegundos;
    private readonly Random _azar = new();

    public GestorPartidas(IConfiguration cfg)
    {
        _botTrasSegundos = int.TryParse(cfg["Online:BotTrasSegundos"], out var s) ? Math.Max(0, s) : 8;
    }

    // Nombres de jugador "comunes" para el bot (estilo de los nombres reales del juego). Se combinan
    // con terminaciones al azar para que no se repitan siempre iguales.
    private static readonly string[] NOMBRES_BOT =
    {
        "Mateo", "valen", "Sofi", "CrisRex", "nico", "LunaEgg", "Juanchi", "andre", "Kevo", "Dani",
        "maria.jose", "Santi", "Fer", "lucho", "Pao", "Emi", "Joaco", "camila", "Tomi", "Ari",
        "bruno", "Vale", "Seba", "Martu", "Gabo", "isa", "Rodri", "Mica", "pipe", "Leo",
    };
    private static readonly string[] SUFIJOS_BOT =
    {
        "", "", "_", "07", "10", "22", "99", "_ec", "gg", "xd", "_pro", "123", "2008", "_yt", "uwu", "777",
    };

    private string NombreBot()
    {
        string nombre = NOMBRES_BOT[_azar.Next(NOMBRES_BOT.Length)] + SUFIJOS_BOT[_azar.Next(SUFIJOS_BOT.Length)];
        return nombre.EndsWith("_") ? nombre + _azar.Next(1, 100) : nombre;
    }

    /// <summary>Si el jugador lleva BotTrasSegundos esperando y no apareció nadie real, le asigna un
    /// rival bot. Se llama en cada sondeo de la cola.</summary>
    public void AsignarBotSiTocaEsperar(Partida p)
    {
        if (_botTrasSegundos <= 0) return;
        lock (_lock)
        {
            if (p.Estado != "esperando" || (DateTime.UtcNow - p.CreadaUtc).TotalSeconds < _botTrasSegundos) return;
            var ahora = DateTime.UtcNow;
            p.EsBot = true;
            p.JugadorBId = "bot_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            p.JugadorBNombre = NombreBot();
            // Skins/tronos de la tienda (0..7), con más peso los básicos como en la gente real.
            p.SkinIdxB = _azar.Next(3) == 0 ? 0 : _azar.Next(0, 8);
            p.TronoIdxB = _azar.Next(3) == 0 ? 0 : _azar.Next(0, 8);
            p.Estado = "emparejado";
            p.EmparejadaUtc = ahora;
            p.VistoA = p.VistoB = ahora;
            p.EntroB = true; // el bot "ya está" en la partida
        }
    }

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

            // Hay alguien esperando (de otro jugador) y sigue vivo (sondeó hace poco) → emparejar.
            var ahora = DateTime.UtcNow;
            var esperando = _partidas.Values.FirstOrDefault(p =>
                p.Estado == "esperando" && p.JugadorAId != jugadorId &&
                (ahora - p.ActualizadaUtc).TotalSeconds <= SEG_COLA_VIVA);
            if (esperando != null)
            {
                esperando.JugadorBId = jugadorId;
                esperando.JugadorBNombre = nombre;
                esperando.SkinIdxB = skinIdx;
                esperando.TronoIdxB = tronoIdx;
                esperando.Estado = "emparejado";
                esperando.ActualizadaUtc = ahora;
                esperando.EmparejadaUtc = ahora;
                esperando.VistoA = esperando.VistoB = ahora; // arranca el latido de ambos
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
    public record EstadoLatido(bool RivalCaido, string Resultado, string Motivo, int RivalSilencioSeg);

    // Cada cliente late cada pocos segundos. Si el rival no late en SEG_DESCONEXION (desconexión,
    // cierre de app o celular apagado), el que sigue latiendo gana. RivalSilencioSeg (segundos desde el
    // último latido del rival) le permite al cliente avisar "el rival perdió la conexión…" ANTES de que
    // se decida el resultado.
    public EstadoLatido LatidoYEstado(string id, string jugadorId)
    {
        if (!_partidas.TryGetValue(id, out var p)) return new EstadoLatido(false, "", "", 0);
        lock (_lock)
        {
            var ahora = DateTime.UtcNow;
            bool soyA = p.JugadorAId == jugadorId;
            bool soyB = p.JugadorBId == jugadorId;
            // Contra un bot no hay acciones en el servidor (la IA corre en el cliente) y nadie más late:
            // lo único que cuenta es que el jugador no haya desaparecido más de SEG_DESCONEXION (se mira
            // su latido ANTERIOR, cuando vuelve). El bot nunca se cae.
            if (p.EsBot)
            {
                if (soyA && string.IsNullOrEmpty(p.Resultado) && p.EntroA &&
                    (ahora - p.VistoA).TotalSeconds > SEG_DESCONEXION)
                {
                    p.Resultado = "gano_B"; p.Motivo = "desconexion";
                }
                if (soyA) { p.VistoA = ahora; p.EntroA = true; }
                p.VistoB = ahora;
                p.ActualizadaUtc = ahora;
                return new EstadoLatido(false, p.Resultado, p.Motivo, 0);
            }

            if (soyA) { p.VistoA = ahora; p.EntroA = true; } else if (soyB) { p.VistoB = ahora; p.EntroB = true; }
            p.ActualizadaUtc = ahora;

            if (p.EntroA && p.EntroB && p.InicioTurnoUtc == DateTime.MinValue) p.InicioTurnoUtc = ahora;

            // Solo es "caído" quien YA entró (latió alguna vez) y luego se calló. El que aún carga la
            // escena no cuenta como caído — así el jugador lento no pierde apenas entra.
            bool caidoA = p.EntroA && (ahora - p.VistoA).TotalSeconds > SEG_DESCONEXION;
            bool caidoB = p.EntroB && (ahora - p.VistoB).TotalSeconds > SEG_DESCONEXION;

            if (string.IsNullOrEmpty(p.Resultado) && p.Estado == "emparejado")
            {
                // El resultado por abandono solo se decide cuando AMBOS ya entraron a la partida.
                if (p.EntroA && p.EntroB)
                {
                    if (caidoA && caidoB) { p.Resultado = "empate"; p.Motivo = "desconexion"; }
                    else if (caidoA)      { p.Resultado = "gano_B"; p.Motivo = "desconexion"; }
                    else if (caidoB)      { p.Resultado = "gano_A"; p.Motivo = "desconexion"; }
                    else if ((ahora - p.InicioTurnoUtc).TotalSeconds > SEG_TURNO_MAX)
                    {
                        p.Resultado = p.TurnoDe == "A" ? "gano_B" : "gano_A";
                        p.Motivo = "inactividad";
                    }
                }
                // Uno entró y el otro nunca llegó (cerró la app mientras cargaba, se quedó sin internet
                // al emparejar...): se cancela sin ganador en vez de dejar al que entró esperando para
                // siempre.
                else if (p.EmparejadaUtc != DateTime.MinValue &&
                         (ahora - p.EmparejadaUtc).TotalSeconds > SEG_ENTRADA)
                {
                    p.Resultado = "cancelada"; p.Motivo = "rival_no_entro";
                }
            }

            bool rivalCaido = soyA ? caidoB : (soyB ? caidoA : false);
            bool rivalEntro = soyA ? p.EntroB : (soyB ? p.EntroA : false);
            var vistoRival = soyA ? p.VistoB : p.VistoA;
            int silencio = rivalEntro ? (int)(ahora - vistoRival).TotalSeconds : 0;
            return new EstadoLatido(rivalCaido, p.Resultado, p.Motivo, silencio);
        }
    }

    /// <summary>Resultado ya decidido de la partida (o "" si sigue / no existe) y el asiento del
    /// jugador. Lo usa el premio de fin de partida para no creerle al cliente un resultado online.</summary>
    public (string resultado, string asiento)? ResultadoPara(string id, string jugadorId)
    {
        if (!_partidas.TryGetValue(id, out var p)) return null;
        string asiento = p.JugadorAId == jugadorId ? "A" : (p.JugadorBId == jugadorId ? "B" : "");
        return (p.Resultado, asiento);
    }

    // Un cliente reporta el ganador de una partida terminada NORMALMENTE (huevo a 0 o por tiempo) o por
    // RENDICIÓN. El PRIMER reporte gana el arbitraje (first-write-wins): así ambos clientes convergen al
    // MISMO resultado y nunca pasa que los dos se ven ganando. Devuelve (resultado, motivo) finales.
    public (string resultado, string motivo) ReportarResultado(string id, string jugadorId, string ganador, string motivo)
    {
        if (!_partidas.TryGetValue(id, out var p)) return ("", "");
        lock (_lock)
        {
            // Solo los dos jugadores de la partida pueden decidirla.
            string asiento = p.JugadorAId == jugadorId ? "A" : (p.JugadorBId == jugadorId ? "B" : "");
            bool valido = ganador == "gano_A" || ganador == "gano_B" || ganador == "empate";
            // Rendirse solo puede dar la victoria AL OTRO.
            if (motivo == "rendicion" && ganador == "gano_" + asiento) valido = false;

            if (string.IsNullOrEmpty(p.Resultado) && valido && asiento != "")
            {
                p.Resultado = ganador;
                p.Motivo = motivo == "rendicion" ? "rendicion" : "normal";
            }
            p.ActualizadaUtc = DateTime.UtcNow;
            return (p.Resultado, p.Motivo);
        }
    }

    // ── Acciones en vivo (invocar/atacar/habilidad/hechizo/fin_turno) ─────
    // Devuelve el índice (0-based) de la acción guardada. Si la acción trae "seq" y ya se había recibido
    // (reintento tras un corte), devuelve el índice original sin duplicarla.
    public int AgregarAccion(string id, string accionJson)
    {
        if (!_partidas.TryGetValue(id, out var p)) return -1;
        accionJson ??= "";
        string autor = "", tipo = "";
        int seq = -1;
        bool porTiempo = false;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(accionJson);
            var r = doc.RootElement;
            if (r.TryGetProperty("autor", out var a)) autor = a.GetString() ?? "";
            if (r.TryGetProperty("tipo", out var t)) tipo = t.GetString() ?? "";
            if (r.TryGetProperty("seq", out var s) && s.TryGetInt32(out var sv)) seq = sv;
            if (r.TryGetProperty("datos", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.Object &&
                d.TryGetProperty("porTiempo", out var pt) && pt.ValueKind == System.Text.Json.JsonValueKind.True)
                porTiempo = true;
        }
        catch { /* acción no-JSON: se guarda tal cual, sin idempotencia ni conteo de inactividad */ }

        lock (_lock)
        {
            p.ActualizadaUtc = DateTime.UtcNow;
            string clave = autor + ":" + seq;
            if (seq >= 0 && autor != "" && p.IndicePorSecuencia.TryGetValue(clave, out var yaGuardada))
                return yaGuardada;

            p.Acciones.Add(accionJson);
            int indice = p.Acciones.Count - 1;
            if (seq >= 0 && autor != "") p.IndicePorSecuencia[clave] = indice;

            if (autor == "A" || autor == "B")
            {
                bool esA = autor == "A";
                if (tipo == "fin_turno")
                {
                    // Turno perdido = se acabó el tiempo SIN haber hecho nada (ni invocar). Pasar el turno
                    // gastando la energía, o jugando algo, reinicia la cuenta.
                    bool sinJugar = (esA ? p.AccionesTurnoA : p.AccionesTurnoB) == 0;
                    int inactivos = porTiempo && sinJugar ? (esA ? p.TurnosInactivosA : p.TurnosInactivosB) + 1 : 0;
                    if (esA) { p.TurnosInactivosA = inactivos; p.AccionesTurnoA = 0; }
                    else     { p.TurnosInactivosB = inactivos; p.AccionesTurnoB = 0; }
                    p.TurnoDe = esA ? "B" : "A";
                    p.InicioTurnoUtc = DateTime.UtcNow;

                    if (inactivos >= TURNOS_INACTIVO_MAX && string.IsNullOrEmpty(p.Resultado) && p.Estado == "emparejado")
                    {
                        p.Resultado = esA ? "gano_B" : "gano_A";
                        p.Motivo = "inactividad";
                    }
                }
                else if (tipo != "nuclear_impacto") // el impacto de la Nuclear es automático, no una jugada
                {
                    if (esA) p.AccionesTurnoA++; else p.AccionesTurnoB++;
                }
            }
            return indice;
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
        // 90s: tolera turnos largos (el sondeo del rival mantiene viva la partida igual). Un boleto de
        // cola abandonado (nadie lo sondea) se borra antes: ya no se lo empareja con nadie igual.
        var ahora = DateTime.UtcNow;
        foreach (var kv in _partidas)
        {
            double inactiva = (ahora - kv.Value.ActualizadaUtc).TotalSeconds;
            if (inactiva > 90 || (kv.Value.Estado == "esperando" && inactiva > 30))
                _partidas.TryRemove(kv.Key, out _);
        }
    }
}
