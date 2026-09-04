-- ============================================================================
-- Eggodia — Esquema de referencia de la base de datos
-- ----------------------------------------------------------------------------
-- Motor real en uso: SQLite (Backend/Eggodia/cards.db)
-- La BD la crea automáticamente Entity Framework (AppDbContext) al arrancar el
-- backend (Program.cs -> EnsureCreated). Este archivo es solo DOCUMENTACIÓN /
-- referencia del esquema y de los datos semilla; no hace falta ejecutarlo.
-- ============================================================================

-- ── TABLA: usuarios (cuentas + estadísticas de ranking) ─────────────────────
CREATE TABLE IF NOT EXISTS usuarios (
    Id            INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    Nombre        TEXT    NOT NULL,
    Email         TEXT    NOT NULL,
    Password      TEXT    NOT NULL,            -- hash BCrypt (nunca texto plano)
    Victorias     INTEGER NOT NULL DEFAULT 0,
    Derrotas      INTEGER NOT NULL DEFAULT 0,
    Empates       INTEGER NOT NULL DEFAULT 0,
    DañoTotal     INTEGER NOT NULL DEFAULT 0,
    FechaRegistro TEXT    NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_usuarios_Email ON usuarios (Email);

-- ── TABLA: cartas (catálogo de las 17 tropas) ───────────────────────────────
CREATE TABLE IF NOT EXISTS cartas (
    Id           INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    Nombre       TEXT    NOT NULL,
    Era          INTEGER NOT NULL DEFAULT 1,   -- legado
    VidaMaxima   INTEGER NOT NULL,
    EscudoMaximo INTEGER NOT NULL,
    PuntosAtaque INTEGER NOT NULL,
    RutaImagen   TEXT    NOT NULL,             -- CartasPng (imagen grande de mano)
    RutaEscena   TEXT    NOT NULL,             -- escena .tscn de la tropa
    Habilidad    TEXT,
    Tipo         TEXT    NOT NULL DEFAULT 'Normal', -- legado (serie elemental)
    Rol          TEXT    NOT NULL DEFAULT '',  -- Tactico | Asesino | Coloso
    Serie        TEXT    NOT NULL DEFAULT ''   -- Ajedrez | Toon | Medieval | Pacifico | Papeleo
);

-- ── DATOS SEMILLA: cartas ───────────────────────────────────────────────────
INSERT INTO cartas (Id, Nombre, Rol, Serie, Era, VidaMaxima, EscudoMaximo, PuntosAtaque, RutaImagen, RutaEscena, Habilidad, Tipo) VALUES
-- AJEDREZ
(1,  'Peón',            'Tactico', 'Ajedrez',  2, 150, 150, 100, 'res://imagenes/CartasPng/PeonCart.png',       'res://cartas prime/AJEDREZ/Peon_prime.tscn',            'Sacrificio',                          'Ajedrez'),
(2,  'Torre',           'Coloso',  'Ajedrez',  2, 500, 450, 350, 'res://imagenes/CartasPng/TorreCart.png',      'res://cartas prime/AJEDREZ/Torre_prime.tscn',           'Enroque táctico / forma gigante',     'Ajedrez'),
(3,  'Arfil',           'Tactico', 'Ajedrez',  2, 280, 270, 240, 'res://imagenes/CartasPng/ArfilCart.png',      'res://cartas prime/AJEDREZ/Arfil_prime.tscn',           'Salto diagonal',                      'Ajedrez'),
(4,  'Caballo',         'Tactico', 'Ajedrez',  2, 230, 250, 200, 'res://imagenes/CartasPng/CaballoCart.png',    'res://cartas prime/AJEDREZ/Caballo_prime.tscn',         'Salto en L: x2 daño a 2 carriles',    'Ajedrez'),
(5,  'Dama',            'Asesino', 'Ajedrez',  2, 350, 300, 350, 'res://imagenes/CartasPng/DamaCart.png',       'res://cartas prime/AJEDREZ/Dama_prime.tscn',            'Vuelo con inspiración aliada',        'Ajedrez'),
-- TOON
(6,  'Soldado Cartoon', 'Asesino', 'Toon',     3, 220, 350,  50, 'res://imagenes/CartasPng/SoldCartoonCart.png','res://cartas prime/TOONS/Soldado_cartoon_prime.tscn',   'Soldado explosivo',                   'Toon'),
(7,  'Tanque',          'Coloso',  'Toon',     3, 820,   0, 350, 'res://imagenes/CartasPng/TanqueCart.png',     'res://cartas prime/TOONS/Tanque_cartoon_prime.tscn',    'Doble disparo de misil',              'Toon'),
(8,  'Granadero',       'Asesino', 'Toon',     3,  50, 600, 230, 'res://imagenes/CartasPng/GranaderoCart.png',  'res://cartas prime/TOONS/Granadero_cartoon_prime.tscn', 'Escudo masivo',                       'Toon'),
(9,  'Ka-Bar',          'Asesino', 'Toon',     3, 250, 360, 100, 'res://imagenes/CartasPng/FantasmaCart.png',   'res://cartas prime/TOONS/Ka-Bar_cartoon_prime.tscn',    'Resucita como fantasma',              'Toon'),
(10, 'Campero',         'Tactico', 'Toon',     3, 200, 300, 250, 'res://imagenes/CartasPng/CamperoCart.png',    'res://cartas prime/TOONS/Campero_cartoon_prime.tscn',   'Postura defensiva',                   'Toon'),
-- MEDIEVAL
(11, 'Soldado Real',    'Tactico', 'Medieval', 2, 250, 300, 150, 'res://imagenes/CartasPng/SoldRealCart.png',   'res://cartas prime/MEDIEVAL/SoldadoReal_prime.tscn',    'Guardia real',                        'Medieval'),
(12, 'Maguín',          'Tactico', 'Medieval', 2, 200, 220, 250, 'res://imagenes/CartasPng/MaguinCart.png',     'res://cartas prime/MEDIEVAL/Maguin_prime.tscn',         'Transmutación',                       'Medieval'),
(13, 'Dragón',          'Asesino', 'Medieval', 2, 350, 250, 280, 'res://imagenes/CartasPng/DragonCart.png',     'res://cartas prime/MEDIEVAL/Dragon_prime.tscn',         'Fuego eterno',                        'Medieval'),
(14, 'Golem',           'Coloso',  'Medieval', 2, 450, 500, 350, 'res://imagenes/CartasPng/GolemCart.png',      'res://cartas prime/MEDIEVAL/Golem_prime.tscn',          'Rocas escudo: +200 ESC a aliados',    'Medieval'),
-- PACIFICO
(15, 'Tiburón',         'Asesino', 'Pacifico', 1, 300, 200, 230, 'res://imagenes/CartasPng/TiburonCart.png',    'res://cartas prime/PACIFICO/Tiburon_prime.tscn',        'Ataque en profundidad',               'Pacifico'),
(16, 'Calamar Gigante', 'Coloso',  'Pacifico', 1, 400, 380, 370, 'res://imagenes/CartasPng/CalamarGCart.png',   'res://cartas prime/PACIFICO/CalamarG_prime.tscn',       'Tinta bloqueadora',                   'Pacifico'),
-- PAPELEO
(17, 'Paper-Rex',       'Coloso',  'Papeleo',  1, 850,   0, 370, 'res://imagenes/CartasPng/PaperReXCart.png',   'res://cartas prime/PAPEL/TRex_prime.tscn',              'Rugido primordial: -30% ataque enemigo','Papeleo');
