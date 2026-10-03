using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Eggodia.API.Data;
using Eggodia.API.model;

[ApiController]
[Route("api/[controller]")]
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly GestorPartidas _partidas;
    public UsuariosController(AppDbContext db, GestorPartidas partidas) { _db = db; _partidas = partidas; }

    // Premios por partida: los MISMOS valores que Economia.cs del cliente (que los usa para mostrarlos
    // al instante); acá se aplican de verdad. Si cambian, cambiarlos en los dos lados.
    private const int RECOMPENSA_ONLINE_VICTORIA = 120;
    private const int RECOMPENSA_ONLINE_DERROTA  = 30;
    private const int RECOMPENSA_ONLINE_EMPATE   = 15;
    private const int RECOMPENSA_BOT_VICTORIA    = 20;
    private const int RECOMPENSA_BOT_DERROTA     = 5;
    private const int RECOMPENSA_BOT_EMPATE      = 8;
    private const int BONO_POR_RACHA             = 25;
    private const int RACHA_MAXIMA               = 10;
    private const int XP_POR_VICTORIA            = 150;
    /// <summary>Monedas con las que arranca toda cuenta nueva (las ya existentes recibieron lo mismo
    /// una sola vez, ver Program.cs).</summary>
    public const int MONEDAS_INICIALES           = 500;

    // Premios contra el bot: el resultado lo informa el celular (no hay forma de verificarlo), así que
    // se pone un techo que un jugador real no alcanza (una partida dura varios minutos). Pasado el
    // techo la partida se registra igual pero no suma monedas, experiencia ni estadísticas.
    private const int MAX_PREMIOS_BOT_POR_HORA   = 20;
    private const int SEG_MIN_ENTRE_PREMIOS_BOT  = 45;
    private const int MAX_DAÑO_POR_PARTIDA       = 20000;

    // Pedidos de los APK viejos (sin sesión) que suman: como mucho uno cada tanto por cuenta, y con
    // tope por pedido. Es solo para la transición; con Seguridad:ExigirToken quedan cerrados.
    private static readonly ConcurrentDictionary<string, DateTime> _ultimoPedidoViejo = new();
    private const int SEG_ENTRE_PEDIDOS_VIEJOS = 45;
    private const int MAX_SUBIDA_MONEDAS_VIEJA = 150;

    private static bool PermitirPedidoViejo(string tipo, int usuarioId)
    {
        string clave = tipo + ":" + usuarioId;
        var ahora = DateTime.UtcNow;
        if (_ultimoPedidoViejo.TryGetValue(clave, out var antes) && (ahora - antes).TotalSeconds < SEG_ENTRE_PEDIDOS_VIEJOS)
            return false;
        _ultimoPedidoViejo[clave] = ahora;
        return true;
    }

    // POST: api/usuarios/registro
    [HttpPost("registro")]
    [EnableRateLimiting("registro")]
    public async Task<IActionResult> Registro([FromBody] RegistroRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Normalizar entradas (evita duplicados por espacios o mayúsculas).
        string nombre     = (req.Nombre ?? "").Trim();
        string email      = (req.Email  ?? "").Trim();
        string nombreLower = nombre.ToLowerInvariant();
        string emailLower  = email.ToLowerInvariant();

        if (nombre.Length < 3)
            return Conflict(new { mensaje = "El nombre debe tener al menos 3 caracteres." });

        // Nombres reservados (staff / impersonación): nadie los puede registrar.
        var reservados = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "dev", "admin", "administrador", "moderador", "mod", "staff", "eggodia", "jeremy_dev", "gonza_dev", "soporte", "sistema" };
        if (reservados.Contains(nombreLower))
            return Conflict(new { mensaje = "Ese nombre está reservado. Elige otro." });

        // Email único (insensible a mayúsculas).
        if (await _db.Usuarios.AnyAsync(u => u.Email.ToLower() == emailLower))
            return Conflict(new { mensaje = "El email ya está registrado." });

        // Nombre de usuario ÚNICO (insensible a mayúsculas): evita nombres repetidos o "similares"
        // por capitalización (p. ej. "Jeremy_dev" vs "jeremy_dev").
        if (await _db.Usuarios.AnyAsync(u => u.Nombre.ToLower() == nombreLower))
            return Conflict(new { mensaje = "Ese nombre de usuario ya está en uso. Elige otro." });

        var usuario = new Usuario
        {
            Nombre   = nombre,
            Email    = email,
            Password = BCrypt.Net.BCrypt.HashPassword(req.Password), // hash seguro (nunca texto plano)
            Monedas  = MONEDAS_INICIALES
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        // 11 personajes base con los que inicia todo jugador al registrarse
        var personajesBase = new[]
        {
            "peon",
            "torre",
            "arfil",
            "caballo",
            "dama",
            "soldado_real",
            "maguin",
            "machi",
            "dragon",
            "golem",
            "paper_rex"
        };

        // Exclusión estricta de las 7 cartas físicas exclusivas
        var cartasExclusivasExcluidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tiburon", "calamar_gigante", "soldado_cartoon", "campero", "granadero", "tanque", "kabar"
        };

        var mazoInicial = personajesBase
            .Where(cardId => !cartasExclusivasExcluidas.Contains(cardId))
            .Select(cardId => new UserCard
            {
                UserId = usuario.Id,
                CardId = cardId,
                AcquiredAt = DateTime.UtcNow
            });

        _db.UserCards.AddRange(mazoInicial);
        await _db.SaveChangesAsync();

        var dto = ToDto(usuario);
        dto.Token = await Sesiones.Crear(_db, usuario.Id);
        return CreatedAtAction(nameof(GetUsuario), new { id = usuario.Id }, dto);
    }

    // POST: api/usuarios/login
    [HttpPost("login")]
    [EnableRateLimiting("login")] // adivinar contraseñas a ciegas queda frenado
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (usuario == null)
            return Unauthorized(new { mensaje = "Email o contraseña incorrectos." });

        bool ok;
        if (EsHashBCrypt(usuario.Password))
        {
            ok = BCrypt.Net.BCrypt.Verify(req.Password, usuario.Password);
        }
        else
        {
            // Cuenta antigua con contraseña en texto plano: comparar y migrar a hash al vuelo.
            ok = usuario.Password == req.Password;
            if (ok)
            {
                usuario.Password = BCrypt.Net.BCrypt.HashPassword(req.Password);
                await _db.SaveChangesAsync();
            }
        }

        if (!ok)
            return Unauthorized(new { mensaje = "Email o contraseña incorrectos." });

        var dto = ToDto(usuario);
        dto.Token = await Sesiones.Crear(_db, usuario.Id);
        return Ok(dto);
    }

    // Detecta si un valor ya es un hash BCrypt ($2a$/$2b$/$2y$...).
    private static bool EsHashBCrypt(string valor) =>
        !string.IsNullOrEmpty(valor) && valor.StartsWith("$2") && valor.Length >= 55;

    // GET: api/usuarios/5
    [HttpGet("{id}")]
    [CuentaPropia("id")]
    public async Task<IActionResult> GetUsuario(int id)
    {
        var u = await _db.Usuarios.FindAsync(id);
        if (u == null) return NotFound();
        var dto = ToDto(u);
        dto.Email = ""; // el correo solo se ve al iniciar sesión (antes cualquiera lo leía con el número)
        return Ok(dto);
    }

    // POST api/usuarios/{id}/recompensa
    // Fin de partida (versión 1.1.2+): el SERVIDOR suma monedas, experiencia y estadísticas — el cliente
    // ya no manda su saldo total (antes, si el admin regalaba monedas con el juego abierto, la siguiente
    // victoria pisaba el regalo con el saldo viejo del celular). Idempotente por PartidaId: un reintento
    // tras un corte de red no premia dos veces. En línea el resultado NO se le cree al cliente: se toma
    // el que arbitró el servidor para esa partida.
    public class RecompensaRequest
    {
        public string PartidaId { get; set; } = ""; // bot: id único generado por el cliente; online: matchId
        public string Modo      { get; set; } = ""; // "bot" | "online" | "tutorial"
        public string Resultado { get; set; } = ""; // "victoria" | "derrota" | "empate"
        public int    Racha     { get; set; }       // victorias seguidas contando esta (bono online)
        public int    DañoHecho { get; set; }
        public string TropaFavorita { get; set; } = ""; // opcional: favoritos para el perfil público
        public string ArdidFavorito { get; set; } = "";
    }

    [HttpPost("{id}/recompensa")]
    [CuentaPropia("id")]
    public async Task<IActionResult> Recompensa(int id, [FromBody] RecompensaRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.PartidaId) || req.PartidaId.Length > 64)
            return BadRequest(new { ok = false, mensaje = "Partida inválida." });
        var u = await _db.Usuarios.FindAsync(id);
        if (u == null) return NotFound(new { ok = false, mensaje = "Usuario no encontrado." });

        string modo = req.Modo == "online" ? "online" : req.Modo == "tutorial" ? "tutorial" : "bot";
        string resultado = (req.Resultado ?? "").ToLowerInvariant();
        if (resultado != "victoria" && resultado != "derrota" && resultado != "empate")
            return BadRequest(new { ok = false, mensaje = "Resultado inválido." });

        bool victoriaVerificadaOnline = false;
        if (modo == "online")
        {
            // El resultado online lo decide el servidor (GestorPartidas). Si la partida ya no está en
            // memoria (se limpió o el servidor se reinició) solo se acepta reportar una derrota.
            var r = _partidas.ResultadoPara(req.PartidaId, "u" + id);
            if (r == null)
            {
                if (resultado != "derrota")
                    return Conflict(new { ok = false, mensaje = "No se pudo verificar la partida." });
            }
            else
            {
                var (decidido, asiento) = r.Value;
                if (asiento == "") return Conflict(new { ok = false, mensaje = "No jugaste esa partida." });
                if (decidido == "" || decidido == "cancelada")
                    return Conflict(new { ok = false, mensaje = "La partida no tiene un resultado válido." });
                resultado = decidido == "empate" ? "empate" : decidido == "gano_" + asiento ? "victoria" : "derrota";
                victoriaVerificadaOnline = resultado == "victoria";
            }
        }

        int racha = Math.Clamp(req.Racha, 0, RACHA_MAXIMA);
        int monedas = modo == "tutorial" ? 0 : resultado switch
        {
            "victoria" => modo == "online" ? RECOMPENSA_ONLINE_VICTORIA + Math.Max(0, racha - 1) * BONO_POR_RACHA
                                           : RECOMPENSA_BOT_VICTORIA,
            "derrota"  => modo == "online" ? RECOMPENSA_ONLINE_DERROTA : RECOMPENSA_BOT_DERROTA,
            _          => modo == "online" ? RECOMPENSA_ONLINE_EMPATE  : RECOMPENSA_BOT_EMPATE,
        };
        int xp = resultado == "victoria" && modo != "tutorial" ? XP_POR_VICTORIA : 0;

        // Techo de partidas contra el bot (ver MAX_PREMIOS_BOT_POR_HORA). Un reintento de una partida
        // ya premiada no cuenta: lo resuelve el INSERT OR IGNORE de abajo.
        bool limitada = false;
        if (modo == "bot")
        {
            string haceUnaHora = DateTime.UtcNow.AddHours(-1).ToString("o");
            var recientes = await _db.Database.SqlQuery<string>($@"
                SELECT FechaUtc AS ""Value"" FROM partidas_jugadas
                WHERE UserId = {id} AND Modo = 'bot' AND FechaUtc > {haceUnaHora} AND PartidaId <> {req.PartidaId}")
                .ToListAsync();
            string? ultima = recientes.Count > 0 ? recientes.Max() : null;
            bool muyPegada = ultima != null
                && DateTime.TryParse(ultima, null, System.Globalization.DateTimeStyles.RoundtripKind, out var fUltima)
                && (DateTime.UtcNow - fUltima).TotalSeconds < SEG_MIN_ENTRE_PREMIOS_BOT;
            limitada = recientes.Count >= MAX_PREMIOS_BOT_POR_HORA || muyPegada;
            if (limitada) { monedas = 0; xp = 0; }
        }

        // Marca la partida como premiada; si ya lo estaba (reintento), no se vuelve a sumar nada.
        int filas = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT OR IGNORE INTO partidas_jugadas (UserId, PartidaId, Modo, Resultado, Monedas, Xp, FechaUtc)
            VALUES ({id}, {req.PartidaId}, {modo}, {resultado}, {monedas}, {xp}, {DateTime.UtcNow.ToString("o")});");
        bool yaAplicada = filas == 0;

        // Favoritos del perfil público: se guardan aunque el premio ya se hubiera aplicado (es solo
        // información de la cuenta, no suma nada). Vacío = todavía no hay partidas suficientes.
        if (!string.IsNullOrWhiteSpace(req.TropaFavorita) && req.TropaFavorita.Length <= 200)
            u.TropaFavorita = req.TropaFavorita;
        if (!string.IsNullOrWhiteSpace(req.ArdidFavorito) && req.ArdidFavorito.Length <= 60)
            u.ArdidFavorito = req.ArdidFavorito;
        if (yaAplicada) await _db.SaveChangesAsync();

        // Le ganaste en línea a una cuenta DEV (Jeremy, SrGonza, Carlos): te llevas el Huevo Dorado, si
        // todavía no lo tenías. Solo con una victoria que arbitró el servidor (no se le cree al cliente).
        string? skinGanadaRuta = null;
        if (victoriaVerificadaOnline)
        {
            string? rival = _partidas.RivalDe(req.PartidaId, "u" + id);
            if (rival != null && rival.StartsWith("u") && int.TryParse(rival[1..], out int rivalId)
                && rivalId != id && CuentasDev.Es(rivalId)
                && !await _db.UserSkins.AnyAsync(s => s.UserId == id && s.SkinRuta == CuentasDev.RUTA_HUEVO_DORADO))
            {
                _db.UserSkins.Add(new UserSkin
                {
                    UserId = id,
                    SkinRuta = CuentasDev.RUTA_HUEVO_DORADO,
                    Nombre = CuentasDev.NOMBRE_HUEVO_DORADO,
                    FechaDesbloqueo = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
                skinGanadaRuta = CuentasDev.RUTA_HUEVO_DORADO;
            }
        }

        if (!yaAplicada && !limitada)
        {
            u.Monedas += monedas;
            u.Experiencia += xp;
            if (modo != "tutorial")
            {
                switch (resultado)
                {
                    case "victoria": u.Victorias++; break;
                    case "derrota":  u.Derrotas++;  break;
                    case "empate":   u.Empates++;   break;
                }
                u.DañoTotal += Math.Clamp(req.DañoHecho, 0, MAX_DAÑO_POR_PARTIDA);
            }
            await _db.SaveChangesAsync();
        }

        return Ok(new
        {
            ok = true,
            yaAplicada,
            limitada,
            resultado,
            ganado = yaAplicada ? 0 : monedas,
            xpGanada = yaAplicada ? 0 : xp,
            monedas = u.Monedas,
            experiencia = u.Experiencia,
            victorias = u.Victorias,
            derrotas = u.Derrotas,
            empates = u.Empates,
            skinGanadaRuta,
            skinGanadaNombre = skinGanadaRuta != null ? CuentasDev.NOMBRE_HUEVO_DORADO : null
        });
    }

    // POST api/usuarios/{id}/mazo { mazo }  → guarda el mazo armado en la cuenta (JSON tal cual lo
    // guarda el cliente en su archivo). Se devuelve en /inventario al iniciar sesión en otro celular.
    public class MazoRequest { public string Mazo { get; set; } = ""; }

    [HttpPost("{id}/mazo")]
    [CuentaPropia("id")]
    public async Task<IActionResult> GuardarMazo(int id, [FromBody] MazoRequest req)
    {
        if (req == null || req.Mazo == null || req.Mazo.Length > 16000)
            return BadRequest(new { ok = false, mensaje = "Mazo inválido." });
        var u = await _db.Usuarios.FindAsync(id);
        if (u == null) return NotFound(new { ok = false, mensaje = "Usuario no encontrado." });
        u.MazoJson = req.Mazo;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    // POST: api/usuarios/resultado
    // Llamado al terminar una partida para actualizar stats (versiones ≤ 1.1.1; las nuevas usan
    // /{id}/recompensa). Se mantiene para que los APK viejos sigan funcionando.
    [HttpPost("resultado")]
    [CuentaPropia("req.UsuarioId", SoloSinToken = true)]
    public async Task<IActionResult> GuardarResultado([FromBody] ResultadoPartidaRequest req)
    {
        if (req == null) return BadRequest(new { mensaje = "Faltan datos." });
        var u = await _db.Usuarios.FindAsync(req.UsuarioId);
        if (u == null) return NotFound(new { mensaje = "Usuario no encontrado." });
        // APK viejo: como mucho una partida cada SEG_ENTRE_PEDIDOS_VIEJOS por cuenta (antes se podían
        // sumar victorias sin límite y trepar el ranking).
        if (!PermitirPedidoViejo("resultado", u.Id)) return Ok(ToDto(u));

        switch (req.Resultado.ToLower())
        {
            case "victoria": u.Victorias++;  break;
            case "derrota":  u.Derrotas++;   break;
            case "empate":   u.Empates++;    break;
        }
        u.DañoTotal += Math.Clamp(req.DañoHecho, 0, MAX_DAÑO_POR_PARTIDA);

        await _db.SaveChangesAsync();
        return Ok(ToDto(u));
    }

    // Jugadores que muestra la tabla competitiva.
    private const int TOPE_RANKING = 50;

    // GET: api/usuarios/ranking
    // Solo entra quien jugó al menos una partida: antes las cuentas en 0/0/0 (nunca jugaron) ocupaban
    // puestos y dejaban afuera a jugadores que sí tenían partidas.
    [HttpGet("ranking")]
    public async Task<IActionResult> GetRanking()
    {
        var top = await _db.Usuarios
            .Where(u => u.Victorias + u.Derrotas + u.Empates > 0)
            .OrderByDescending(u => u.Victorias)
            .ThenByDescending(u => u.DañoTotal)
            .Take(TOPE_RANKING)
            .Select(u => new {
                u.Id, u.Nombre,
                u.Victorias, u.Derrotas, u.Empates,
                u.DañoTotal,
                WinRate = u.Victorias + u.Derrotas + u.Empates > 0
                    ? (int)((float)u.Victorias / (u.Victorias + u.Derrotas + u.Empates) * 100)
                    : 0
            })
            .ToListAsync();

        return Ok(top);
    }

    // GET: api/usuarios/{id}/perfil
    // Perfil PÚBLICO de un jugador, para verlo al tocarlo en el ranking (como en Clash). Solo datos de
    // juego: nada de email ni monedas.
    [HttpGet("{id}/perfil")]
    public async Task<IActionResult> PerfilPublico(int id)
    {
        var u = await _db.Usuarios.FindAsync(id);
        if (u == null) return NotFound(new { mensaje = "Usuario no encontrado." });
        return Ok(new
        {
            id = u.Id,
            nombre = u.Nombre,
            experiencia = u.Experiencia,
            victorias = u.Victorias,
            derrotas = u.Derrotas,
            empates = u.Empates,
            dañoTotal = u.DañoTotal,
            equipSkinIdx = u.EquipSkinIdx,
            equipSkinExclusiva = u.EquipSkinExclusiva,
            equipTronoIdx = u.EquipTronoIdx,
            tropaFavorita = u.TropaFavorita,
            ardidFavorito = u.ArdidFavorito
        });
    }

    // POST: api/usuarios/{id}/monedas   { monedas }
    // El cliente sincroniza su saldo (tras ganar/gastar). El servidor guarda el valor absoluto.
    // Solo lo usan los APK ≤ 1.1.1 (los nuevos nunca mandan un saldo: el servidor suma y resta).
    public class MonedasSyncRequest { public int Monedas { get; set; } }

    [HttpPost("{id}/monedas")]
    [CuentaPropia("id", SoloSinToken = true)]
    public async Task<IActionResult> SincronizarMonedas(int id, [FromBody] MonedasSyncRequest req)
    {
        var u = await _db.Usuarios.FindAsync(id);
        if (u == null) return NotFound(new { mensaje = "Usuario no encontrado." });
        int pedido = Math.Max(0, req?.Monedas ?? 0);
        // Antes cualquiera podía poner el saldo que quisiera a cualquier cuenta. Para los APK viejos
        // se acepta bajar (compras) y subir solo lo que da una partida, como mucho una vez cada tanto.
        if (pedido > u.Monedas)
        {
            if (!PermitirPedidoViejo("monedas", u.Id)) return Ok(new { u.Id, u.Monedas });
            pedido = Math.Min(pedido, u.Monedas + MAX_SUBIDA_MONEDAS_VIEJA);
        }
        u.Monedas = pedido;
        await _db.SaveChangesAsync();
        return Ok(new { u.Id, u.Monedas });
    }

    // ── INVENTARIO POR CUENTA (server-side; ver UserItem) ─────────────────────
    // GET api/usuarios/{id}/inventario  → todo lo que el jugador posee, para cargar al iniciar sesión.
    [HttpGet("{id}/inventario")]
    [CuentaPropia("id")]
    public async Task<IActionResult> Inventario(int id)
    {
        var u = await _db.Usuarios.FindAsync(id);
        if (u == null) return NotFound(new { mensaje = "Usuario no encontrado." });

        var items = await _db.UserItems.Where(x => x.UserId == id)
            .Select(x => new { tipo = x.Tipo, itemId = x.ItemId }).ToListAsync();
        var exclusivas = await _db.UserSkins.Where(s => s.UserId == id)
            .Select(s => s.SkinRuta).ToListAsync();

        return Ok(new
        {
            monedas = u.Monedas,
            experiencia = u.Experiencia,
            victorias = u.Victorias,
            derrotas = u.Derrotas,
            mazo = u.MazoJson,
            equipSkinIdx = u.EquipSkinIdx,
            equipSkinExclusiva = u.EquipSkinExclusiva,
            equipTronoIdx = u.EquipTronoIdx,
            items,
            skinsExclusivas = exclusivas
        });
    }

    // POST api/usuarios/{id}/comprar { tipo, itemId, costo }
    // Compra SERVER-AUTORITATIVA: el servidor descuenta las monedas y registra el ítem en la cuenta.
    // Idempotente: si ya lo tiene, no cobra de nuevo. Evita trampas de cliente (monedas/ítems locales).
    [HttpPost("{id}/comprar")]
    [CuentaPropia("id")]
    public async Task<IActionResult> Comprar(int id, [FromBody] ComprarRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Tipo) || string.IsNullOrWhiteSpace(req.ItemId))
            return BadRequest(new { ok = false, mensaje = "Datos de compra incompletos." });

        var u = await _db.Usuarios.FindAsync(id);
        if (u == null) return NotFound(new { ok = false, mensaje = "Usuario no encontrado." });

        bool yaTiene = await _db.UserItems.AnyAsync(x => x.UserId == id && x.Tipo == req.Tipo && x.ItemId == req.ItemId);
        if (yaTiene) return Ok(new { ok = true, yaTenia = true, monedas = u.Monedas });

        int costo = Math.Max(0, req.Costo);
        if (u.Monedas < costo)
            return BadRequest(new { ok = false, mensaje = "Monedas insuficientes.", monedas = u.Monedas });

        u.Monedas -= costo;
        _db.UserItems.Add(new UserItem { UserId = id, Tipo = req.Tipo, ItemId = req.ItemId });
        await _db.SaveChangesAsync();
        return Ok(new { ok = true, monedas = u.Monedas });
    }

    // POST api/usuarios/{id}/equipar { skinIdx?, skinExclusiva?, tronoIdx? }
    // Guarda el cosmético equipado en la CUENTA (solo se actualizan los campos enviados).
    [HttpPost("{id}/equipar")]
    [CuentaPropia("id")]
    public async Task<IActionResult> Equipar(int id, [FromBody] EquiparRequest req)
    {
        var u = await _db.Usuarios.FindAsync(id);
        if (u == null) return NotFound(new { ok = false, mensaje = "Usuario no encontrado." });
        if (req == null) return BadRequest(new { ok = false });

        if (req.SkinIdx.HasValue)      u.EquipSkinIdx = req.SkinIdx.Value;
        if (req.SkinExclusiva != null) u.EquipSkinExclusiva = req.SkinExclusiva;
        if (req.TronoIdx.HasValue)     u.EquipTronoIdx = req.TronoIdx.Value;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true, u.EquipSkinIdx, u.EquipSkinExclusiva, u.EquipTronoIdx });
    }

    private static UsuarioDto ToDto(Usuario u) => new()
    {
        Id        = u.Id,
        Nombre    = u.Nombre,
        Email     = u.Email,
        Monedas   = u.Monedas,
        EquipSkinIdx       = u.EquipSkinIdx,
        EquipSkinExclusiva = u.EquipSkinExclusiva,
        EquipTronoIdx      = u.EquipTronoIdx,
        Experiencia = u.Experiencia,
        Victorias = u.Victorias,
        Derrotas  = u.Derrotas,
        Empates   = u.Empates,
        DañoTotal = u.DañoTotal
    };
}
