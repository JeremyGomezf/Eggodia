using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

/// <summary>
/// ECONOMÍA — Singleton persistente en disco (user://economia.cfg).
/// ───────────────────────────────────────────────────────────────
/// Maneja las monedas del jugador. Sobrevive al cierre del juego.
/// Base para tienda, desbloqueo de cartas, cosméticos, etc. (a futuro).
///
/// REGISTRAR EN GODOT (recomendado):
/// Proyecto → Configuración → AutoLoad
/// Ruta: res://escenas/gameplay/Economia.cs   ·   Nombre: Economia
///
/// Si no se registra como AutoLoad, Instancia() crea una instancia
/// temporal automáticamente (sin persistencia garantizada entre escenas).
/// </summary>
public partial class Economia : Node
{
	// Las monedas viven en el archivo del PERFIL activo (invitado o cuenta), ver Preferencias.RutaPerfil.
	private const string SECCION = "jugador";

	public static Economia Instance { get; private set; }

	[Signal] public delegate void MonedasCambiaronEventHandler(int nuevoTotal);

	// Se emite cuando llega (y se aplica) el inventario de la cuenta desde el servidor, para que las
	// pantallas abiertas (ej. el huevo del menú) se refresquen con lo equipado de verdad.
	[Signal] public delegate void InventarioAplicadoEventHandler();

	// Cambió el progreso de la cuenta traído del servidor (experiencia → nivel, victorias, derrotas).
	[Signal] public delegate void ProgresoCambiadoEventHandler();

	// Resultado de la última vez que se habló con el servidor: false = sin conexión (el menú lo avisa
	// con un cartel y un botón de reintentar).
	[Signal] public delegate void ConexionCambiadaEventHandler(bool hayConexion);
	public bool HayConexion { get; private set; } = true;

	private int _monedas = 0;
	public int Monedas => _monedas;

	// Cuenta a la que pertenece este saldo (>0 = logueado). Con cuenta, las monedas se sincronizan con
	// el servidor (saldo por CUENTA, que el admin puede ajustar). Sin cuenta (invitado) quedan locales.
	private int _usuarioId = -1;

	// ── Recompensas según modo Online vs Bot ───────────────────────────
	public const int RECOMPENSA_ONLINE_VICTORIA = 120;
	public const int RECOMPENSA_ONLINE_DERROTA  = 30;
	public const int RECOMPENSA_BOT_VICTORIA    = 20;
	public const int RECOMPENSA_BOT_DERROTA     = 5;
	public const int RECOMPENSA_EMPATE          = 8;
	public const int RECOMPENSA_ONLINE_EMPATE   = 15;
	public const int BONO_POR_RACHA             = 25;
	public const int RACHA_MAXIMA               = 10; // tope del bono (el servidor aplica el mismo)

	public override void _Ready()
	{
		Instance = this;
		Cargar();
	}

	/// <summary>Devuelve la instancia global; si no existe (no es AutoLoad), crea una temporal.</summary>
	public static Economia Instancia()
	{
		if (Instance != null) return Instance;
		var eco = new Economia();
		eco.Name = "Economia";
		((SceneTree)Engine.GetMainLoop()).Root.CallDeferred("add_child", eco);
		Instance = eco;
		eco.Cargar();
		return eco;
	}

	// ── OPERACIONES ──────────────────────────────────────────────────────
	// Agregar/Gastar son LOCALES (invitado). Con cuenta, las monedas solo cambian en el servidor (premio
	// de partida, compra, código) y el cliente adopta el saldo que este devuelve: nunca se manda un
	// saldo total, que pisaba lo que el admin regalaba con el juego abierto.

	public void Agregar(int cantidad)
	{
		if (cantidad <= 0) return;
		_monedas += cantidad;
		Guardar();
		EmitSignal(SignalName.MonedasCambiaron, _monedas);
	}

	/// <summary>Solo para un servidor viejo (sin los endpoints nuevos): suma local y sube el saldo total
	/// como hacían las versiones ≤ 1.1.1.</summary>
	public void AgregarModoViejo(int cantidad)
	{
		Agregar(cantidad);
		EnviarSaldoAlServidor();
	}

	public bool TieneSuficiente(int costo) => _monedas >= costo;

	/// <summary>Intenta gastar. Devuelve true si había saldo suficiente.</summary>
	public bool Gastar(int costo)
	{
		if (costo <= 0 || _monedas < costo) return false;
		_monedas -= costo;
		Guardar();
		EmitSignal(SignalName.MonedasCambiaron, _monedas);
		return true;
	}

	private int CuentaActiva => SesionJuego.Instance?.UsuarioId ?? _usuarioId;

	/// <summary>Fin de partida: premio (monedas), experiencia y estadísticas. Devuelve las monedas
	/// ganadas para mostrarlas YA en la pantalla de victoria/derrota.
	///   • Invitado: todo local, como siempre.
	///   • Cuenta: se suma al instante en pantalla y el SERVIDOR aplica el premio de verdad
	///     (POST /recompensa); al responder, se adopta su saldo/nivel. En línea el resultado lo valida el
	///     servidor con el arbitraje de la partida. Sin internet, queda pendiente y se reintenta después
	///     (sin cobrarse dos veces: cada partida tiene su id).</summary>
	public int RegistrarFinDePartida(string resultado, int racha, bool esOnline, bool esTutorial, int dañoHecho)
	{
		int ganado = esTutorial ? 0 : CalcularRecompensa(resultado, racha, esOnline);
		int cuenta = CuentaActiva;
		if (cuenta <= 0)
		{
			Agregar(ganado);
			return ganado;
		}

		if (ganado > 0)
		{
			_monedas += ganado; // a la vista ya; el servidor confirma (o corrige) al responder
			Guardar();
			EmitSignal(SignalName.MonedasCambiaron, _monedas);
		}

		string partidaId = esOnline && !string.IsNullOrEmpty(ContextoOnline.MatchId)
			? ContextoOnline.MatchId
			: Guid.NewGuid().ToString("N");
		string cuerpo = JsonSerializer.Serialize(new Dictionary<string, object>
		{
			{ "partidaId", partidaId },
			{ "modo", esTutorial ? "tutorial" : esOnline ? "online" : "bot" },
			{ "resultado", resultado },
			{ "racha", racha },
			{ "dañoHecho", dañoHecho },
		});
		Preferencias.GuardarPremioPendiente(partidaId, cuerpo); // por si se corta antes de confirmar
		EnviarPremio(cuenta, partidaId, cuerpo);
		return ganado;
	}

	private int _premiosEnVuelo = 0;

	private void EnviarPremio(int cuenta, string partidaId, string cuerpo)
	{
		var h = new HttpRequest { Timeout = 15 };
		AddChild(h);
		_premiosEnVuelo++;
		h.RequestCompleted += (long r, long c, string[] hd, byte[] b) =>
		{
			_premiosEnVuelo--;
			if (IsInstanceValid(h)) h.QueueFree();
			bool sigueSiendoSuya = CuentaActiva == cuenta;

			if (r != (long)HttpRequest.Result.Success || c >= 500)
			{
				MarcarConexion(r == (long)HttpRequest.Result.Success); // queda pendiente: se reintenta luego
				return;
			}
			MarcarConexion(true);
			Preferencias.QuitarPremioPendiente(partidaId);
			if (!sigueSiendoSuya) return;

			if (c == 200)
			{
				AdoptarRespuestaCuenta(Encoding.UTF8.GetString(b));
			}
			else if (c == 404 && b.Length == 0)
			{
				// Servidor viejo (todavía sin /recompensa): se usa el camino de antes.
				EnviarSaldoAlServidor();
				EnviarResultadoModoViejo(cuenta, cuerpo);
			}
			else
			{
				// 400/409: el servidor no lo aceptó (p. ej. partida online cancelada) → se trae el saldo
				// real para deshacer lo que se sumó a la vista.
				RefrescarCuenta(forzar: true);
			}
		};
		string[] hdr = { "Content-Type: application/json" };
		if (h.Request($"{ApiConfig.Usuarios}/{cuenta}/recompensa", hdr, HttpClient.Method.Post, cuerpo) != Error.Ok)
		{
			_premiosEnVuelo--;
			if (IsInstanceValid(h)) h.QueueFree();
		}
	}

	private void EnviarResultadoModoViejo(int cuenta, string cuerpoPremio)
	{
		try
		{
			var p = JsonSerializer.Deserialize<JsonElement>(cuerpoPremio);
			string json = JsonSerializer.Serialize(new Dictionary<string, object>
			{
				{ "UsuarioId", cuenta },
				{ "Resultado", p.GetProperty("resultado").GetString() ?? "" },
				{ "DañoHecho", p.GetProperty("dañoHecho").GetInt32() },
			});
			var h = new HttpRequest();
			AddChild(h);
			h.RequestCompleted += (long r, long c, string[] hd, byte[] b) => { if (IsInstanceValid(h)) h.QueueFree(); };
			string[] hdr = { "Content-Type: application/json" };
			if (h.Request(ApiConfig.Resultado, hdr, HttpClient.Method.Post, json) != Error.Ok && IsInstanceValid(h)) h.QueueFree();
		}
		catch { }
	}

	/// <summary>Adopta lo que devuelve el servidor sobre la cuenta (monedas, experiencia, victorias,
	/// derrotas — los campos que vengan) y avisa a las pantallas abiertas.</summary>
	private void AdoptarRespuestaCuenta(string json)
	{
		JsonElement doc;
		try { doc = JsonSerializer.Deserialize<JsonElement>(json); } catch { return; }
		if (doc.ValueKind != JsonValueKind.Object) return;

		if (doc.TryGetProperty("monedas", out var m) && m.TryGetInt32(out var monedas))
		{
			_monedas = Mathf.Max(0, monedas);
			Guardar();
			EmitSignal(SignalName.MonedasCambiaron, _monedas);
		}
		if (doc.TryGetProperty("experiencia", out var xp) && xp.TryGetInt32(out var experiencia))
		{
			int victorias = doc.TryGetProperty("victorias", out var v) && v.TryGetInt32(out var vi) ? vi : Preferencias.PartidasGanadas;
			int derrotas  = doc.TryGetProperty("derrotas", out var d) && d.TryGetInt32(out var de) ? de : Preferencias.PartidasPerdidas;
			Preferencias.AdoptarProgresoDeServidor(experiencia, victorias, derrotas);
			EmitSignal(SignalName.ProgresoCambiado);
		}
	}

	private void MarcarConexion(bool hay)
	{
		if (HayConexion == hay) return;
		HayConexion = hay;
		EmitSignal(SignalName.ConexionCambiada, hay);
	}

	private ulong _ultimoRefrescoMs = 0;
	private bool _refrescando = false;

	/// <summary>Trae de nuevo el saldo y el progreso de la cuenta (y reenvía los premios que quedaron
	/// pendientes sin internet). Se llama al entrar al menú y al volver a la app: así las monedas que
	/// el admin regala aparecen sin tener que cerrar sesión. Sin forzar, como mucho cada 10 s.</summary>
	public void RefrescarCuenta(bool forzar = false)
	{
		int cuenta = CuentaActiva;
		if (cuenta <= 0 || _refrescando) return;
		ulong ahora = Time.GetTicksMsec();
		if (!forzar && _ultimoRefrescoMs != 0 && ahora - _ultimoRefrescoMs < 10_000) return;
		_ultimoRefrescoMs = ahora;

		foreach (string pendiente in Preferencias.PremiosPendientes())
		{
			try
			{
				var p = JsonSerializer.Deserialize<JsonElement>(pendiente);
				EnviarPremio(cuenta, p.GetProperty("partidaId").GetString() ?? "", pendiente);
			}
			catch { }
		}
		// Si hay premios viajando, su respuesta ya trae el saldo nuevo: no se pide otra vez (evita que
		// el número "baje y vuelva a subir" en pantalla).
		if (_premiosEnVuelo > 0) return;

		_refrescando = true;
		var h = new HttpRequest { Timeout = 10 };
		AddChild(h);
		h.RequestCompleted += (long r, long c, string[] hd, byte[] b) =>
		{
			_refrescando = false;
			if (IsInstanceValid(h)) h.QueueFree();
			MarcarConexion(r == (long)HttpRequest.Result.Success);
			if (r == (long)HttpRequest.Result.Success && c == 200 && CuentaActiva == cuenta)
				AdoptarRespuestaCuenta(Encoding.UTF8.GetString(b));
		};
		if (h.Request($"{ApiConfig.Usuarios}/{cuenta}") != Error.Ok)
		{
			_refrescando = false;
			if (IsInstanceValid(h)) h.QueueFree();
		}
	}

	/// <summary>Monedas que da una partida (mismos valores que aplica el servidor, ver
	/// UsuariosController.Recompensa).</summary>
	public static int CalcularRecompensa(string resultado, int racha, bool esOnline)
	{
		int ganado = 0;
		if (resultado == "victoria")
		{
			ganado = esOnline ? RECOMPENSA_ONLINE_VICTORIA : RECOMPENSA_BOT_VICTORIA;
			if (esOnline && racha > 1)
			{
				ganado += Mathf.Max(0, Mathf.Min(racha, RACHA_MAXIMA) - 1) * BONO_POR_RACHA;
			}
		}
		else if (resultado == "derrota")
		{
			ganado = esOnline ? RECOMPENSA_ONLINE_DERROTA : RECOMPENSA_BOT_DERROTA;
		}
		else // empate
		{
			ganado = esOnline ? RECOMPENSA_ONLINE_EMPATE : RECOMPENSA_EMPATE;
		}
		return ganado;
	}

	// ── PERSISTENCIA ─────────────────────────────────────────────────────

	private void Cargar()
	{
		var cfg = new ConfigFile();
		Error err = cfg.Load(Preferencias.RutaPerfil);
		if (err == Error.Ok)
			_monedas = (int)cfg.GetValue(SECCION, "monedas", 0);
		else
			_monedas = 0;   // primera vez
	}

	private void Guardar()
	{
		// El archivo del perfil guarda también skins/tronos/progreso: hay que cargarlo antes de
		// escribir, o se pisaría todo lo demás con un archivo que solo tiene las monedas.
		string ruta = Preferencias.RutaPerfil;
		var cfg = new ConfigFile();
		cfg.Load(ruta);
		cfg.SetValue(SECCION, "monedas", _monedas);
		cfg.Save(ruta);
	}

	/// <summary>Cambio de perfil (login, invitado o cerrar sesión): toma el saldo guardado en el
	/// archivo del perfil NUEVO. No sube nada al servidor.</summary>
	public void RecargarPerfil(int usuarioId)
	{
		_usuarioId = usuarioId;
		Cargar();
		EmitSignal(SignalName.MonedasCambiaron, _monedas);
	}

	/// <summary>Solo para pruebas/reset.</summary>
	public void Reiniciar()
	{
		_monedas = 0;
		Guardar();
		EnviarSaldoAlServidor();
		EmitSignal(SignalName.MonedasCambiaron, _monedas);
	}

	// ── SINCRONIZACIÓN CON EL SERVIDOR (saldo por cuenta) ─────────────────────

	/// <summary>Al iniciar sesión: adopta el saldo del servidor como verdad (viene en la respuesta de
	/// login). Así las monedas que el admin dio/quitó se reflejan al entrar.</summary>
	public void AdoptarDeServidor(int usuarioId, int monedasServidor)
	{
		_usuarioId = usuarioId;
		_monedas   = Mathf.Max(0, monedasServidor);
		Guardar();
		EmitSignal(SignalName.MonedasCambiaron, _monedas);
	}

	/// <summary>Trae el saldo del servidor (para sesión persistente al reabrir la app). Si falla la
	/// red, deja el saldo local como está — nunca borra monedas por un fallo de conexión.</summary>
	public void SincronizarDesdeServidor(int usuarioId)
	{
		_usuarioId = usuarioId;
		if (usuarioId <= 0) return;
		var h = new HttpRequest();
		AddChild(h);
		h.RequestCompleted += (long result, long code, string[] headers, byte[] body) =>
		{
			if (result == (long)HttpRequest.Result.Success && code == 200)
			{
				try
				{
					var doc = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(body));
					if (doc.TryGetProperty("monedas", out var m))
					{
						_monedas = Mathf.Max(0, m.GetInt32());
						Guardar();
						EmitSignal(SignalName.MonedasCambiaron, _monedas);
					}
				}
				catch { }
			}
			if (IsInstanceValid(h)) h.QueueFree();
		};
		if (h.Request($"{ApiConfig.Usuarios}/{usuarioId}") != Error.Ok && IsInstanceValid(h)) h.QueueFree();
	}

	/// <summary>Sube el saldo actual al servidor (tras ganar/gastar). Fire-and-forget: si falla, el
	/// próximo cambio o el próximo login vuelven a sincronizar. Solo para cuentas (invitado = local).</summary>
	private void EnviarSaldoAlServidor()
	{
		if (_usuarioId <= 0) return;
		var h = new HttpRequest();
		AddChild(h);
		h.RequestCompleted += (long result, long code, string[] headers, byte[] body) =>
		{
			if (IsInstanceValid(h)) h.QueueFree();
		};
		string cuerpo = JsonSerializer.Serialize(new { monedas = _monedas });
		string[] hdr = { "Content-Type: application/json" };
		if (h.Request($"{ApiConfig.Usuarios}/{_usuarioId}/monedas", hdr, HttpClient.Method.Post, cuerpo) != Error.Ok && IsInstanceValid(h))
			h.QueueFree();
	}

	// ── INVENTARIO POR CUENTA ─────────────────────────────────────────────────

	// ── EQUIPADO (skin / exclusiva / trono) → servidor ────────────────────────
	private bool _subidaEquipadoPendiente = false;
	private bool _aplicandoInventario     = false;

	/// <summary>Pide subir lo equipado a la cuenta. Se agrupa al final del frame: equipar una skin
	/// cambia dos valores seguidos (exclusiva + índice) y así viaja un solo pedido con ambos.</summary>
	public void SolicitarSubirEquipado()
	{
		// Los valores que llegan DEL servidor no se le devuelven; el invitado no tiene cuenta.
		if (_aplicandoInventario || _subidaEquipadoPendiente) return;
		if ((SesionJuego.Instance?.UsuarioId ?? -1) <= 0) return;
		_subidaEquipadoPendiente = true;
		CallDeferred(nameof(SubirEquipado));
	}

	private void SubirEquipado()
	{
		_subidaEquipadoPendiente = false;
		int usuarioId = SesionJuego.Instance?.UsuarioId ?? -1;
		if (usuarioId <= 0) return;
		var h = new HttpRequest();
		AddChild(h);
		h.RequestCompleted += (long r, long c, string[] hd, byte[] b) =>
		{
			if (r != (long)HttpRequest.Result.Success || c != 200)
				GD.PrintErr($"[Economia] No se pudo guardar lo equipado en la cuenta (HTTP {c}).");
			if (IsInstanceValid(h)) h.QueueFree();
		};
		string cuerpo = JsonSerializer.Serialize(new
		{
			skinIdx       = Preferencias.SkinActivaIdx,
			skinExclusiva = Preferencias.SkinExclusivaActiva,
			tronoIdx      = Preferencias.TronoActivoIdx,
		});
		string[] hdr = { "Content-Type: application/json" };
		if (h.Request($"{ApiConfig.Usuarios}/{usuarioId}/equipar", hdr, HttpClient.Method.Post, cuerpo) != Error.Ok && IsInstanceValid(h))
			h.QueueFree();
	}

	/// <summary>Carga el inventario COMPLETO de la cuenta desde el servidor (monedas + skins + tronos +
	/// ítems + equipado) y lo aplica localmente. Se llama al iniciar sesión / reabrir la app: primero
	/// limpia lo local (para no heredar la cuenta anterior) y luego pone lo que de verdad tiene ESTA
	/// cuenta. Así "toda cuenta es distinta" — no se traspasa nada entre cuentas del mismo dispositivo.</summary>
	public void CargarInventarioCuenta(int usuarioId)
	{
		_usuarioId = usuarioId;
		if (usuarioId <= 0) return;
		var h = new HttpRequest();
		AddChild(h);
		h.RequestCompleted += (long r, long c, string[] hd, byte[] b) =>
		{
			// Si mientras viajaba el pedido el jugador cambió de perfil (cerró sesión / entró con
			// otra cuenta), esta respuesta ya no es suya: se descarta para no escribir en otro perfil.
			bool sigueSiendoSuya = (SesionJuego.Instance?.UsuarioId ?? -1) == usuarioId;
			MarcarConexion(r == (long)HttpRequest.Result.Success);
			if (sigueSiendoSuya && r == (long)HttpRequest.Result.Success && c == 200)
			{
				_aplicandoInventario = true;
				try { AplicarInventario(Encoding.UTF8.GetString(b)); }
				catch (Exception e) { GD.PrintErr($"[Economia] Inventario inválido: {e.Message}"); }
				finally { _aplicandoInventario = false; }
				EmitSignal(SignalName.InventarioAplicado);
			}
			if (IsInstanceValid(h)) h.QueueFree();
		};
		if (h.Request($"{ApiConfig.Usuarios}/{usuarioId}/inventario") != Error.Ok && IsInstanceValid(h)) h.QueueFree();
	}

	private void AplicarInventario(string json)
	{
		var doc = JsonSerializer.Deserialize<JsonElement>(json);

		// Lo equipado en este aparato ANTES de limpiar (ver más abajo por qué se necesita).
		int    skinLocal      = Preferencias.SkinActivaIdx;
		string exclusivaLocal = Preferencias.SkinExclusivaActiva;
		int    tronoLocal     = Preferencias.TronoActivoIdx;

		// Pizarra limpia: borra skins/tronos/ítems locales antes de poner los de ESTA cuenta.
		Preferencias.LimpiarDatosDeCuenta();

		if (doc.TryGetProperty("monedas", out var m))
		{
			_monedas = Mathf.Max(0, m.GetInt32());
			Guardar();
			EmitSignal(SignalName.MonedasCambiaron, _monedas);
		}

		// Nivel/experiencia y victorias/derrotas de la CUENTA (servidor ≥ 1.1.2): el mismo nivel en
		// cualquier celular donde se inicie sesión.
		if (doc.TryGetProperty("experiencia", out var xpInv) && xpInv.TryGetInt32(out var experiencia))
		{
			int victorias = doc.TryGetProperty("victorias", out var vInv) && vInv.TryGetInt32(out var vi) ? vi : Preferencias.PartidasGanadas;
			int derrotas  = doc.TryGetProperty("derrotas", out var dInv) && dInv.TryGetInt32(out var de) ? de : Preferencias.PartidasPerdidas;
			Preferencias.AdoptarProgresoDeServidor(experiencia, victorias, derrotas);
			EmitSignal(SignalName.ProgresoCambiado);
		}

		// Mazo guardado en la cuenta (si la cuenta todavía no tiene uno, se le sube el de este celular).
		if (doc.TryGetProperty("mazo", out var mazoInv) && mazoInv.ValueKind == JsonValueKind.String)
			SesionJuego.Instance?.AdoptarMazoDeServidor(mazoInv.GetString() ?? "");

		if (doc.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
			foreach (var it in items.EnumerateArray())
			{
				string tipo   = it.TryGetProperty("tipo", out var tp) ? (tp.GetString() ?? "") : "";
				string itemId = it.TryGetProperty("itemId", out var ii) ? (ii.GetString() ?? "") : "";
				switch (tipo)
				{
					case "skin":    if (int.TryParse(itemId, out var si)) Preferencias.DesbloquearSkin(si);  break;
					case "trono":   if (int.TryParse(itemId, out var ti)) Preferencias.DesbloquearTrono(ti); break;
					case "tropa":   Preferencias.DesbloquearTropa(itemId);   break;
					case "hechizo": Preferencias.DesbloquearHechizo(itemId); break;
				}
			}

		if (doc.TryGetProperty("skinsExclusivas", out var exs) && exs.ValueKind == JsonValueKind.Array)
			foreach (var e in exs.EnumerateArray())
				Preferencias.DesbloquearSkinExclusiva(e.GetString() ?? "");

		int    skinServidor      = doc.TryGetProperty("equipSkinIdx", out var esi)       ? esi.GetInt32()          : 0;
		int    tronoServidor     = doc.TryGetProperty("equipTronoIdx", out var eti)      ? eti.GetInt32()          : 0;
		string exclusivaServidor = doc.TryGetProperty("equipSkinExclusiva", out var ese) ? (ese.GetString() ?? "") : "";

		// Hasta esta versión el cliente NUNCA subía lo equipado, así que toda cuenta tiene en el
		// servidor los valores por defecto (Rey Huevo / trono 0). Si el servidor está en el defecto
		// pero este aparato tenía algo equipado que la cuenta SÍ posee, se respeta lo local y se sube
		// (arregla a todos los jugadores actuales sin que pierdan su skin).
		bool servidorEnDefecto = skinServidor == 0 && tronoServidor == 0 && exclusivaServidor == "";
		bool localPropio =
			(skinLocal == 0 || Preferencias.TieneSkin(skinLocal)) &&
			(tronoLocal == 0 || Preferencias.TieneTrono(tronoLocal)) &&
			(exclusivaLocal == "" || Preferencias.TieneSkinExclusiva(exclusivaLocal));
		bool localDistinto = skinLocal != 0 || tronoLocal != 0 || exclusivaLocal != "";

		if (servidorEnDefecto && localDistinto && localPropio)
		{
			Preferencias.SkinActivaIdx       = skinLocal;
			Preferencias.TronoActivoIdx      = tronoLocal;
			Preferencias.SkinExclusivaActiva = exclusivaLocal;
			_aplicandoInventario = false; // este SÍ hay que subirlo
			SolicitarSubirEquipado();
			_aplicandoInventario = true;
		}
		else
		{
			Preferencias.SkinActivaIdx       = skinServidor;
			Preferencias.TronoActivoIdx      = tronoServidor;
			Preferencias.SkinExclusivaActiva = exclusivaServidor;
		}

		// Cartas físicas/NFC reclamadas por ESTA cuenta: se traen aparte (tabla UserCards del server)
		// y se desbloquean localmente. Va después de limpiar/aplicar el resto para que no se borren.
		if (_usuarioId > 0) CargarCartasFisicas(_usuarioId);
	}

	/// <summary>Trae las cartas físicas/NFC que la cuenta reclamó (GET /api/cartas/usuario/{id}) y las
	/// desbloquea localmente con la misma clave que usa el terminal → persisten entre sesiones y no se
	/// pierden al cambiar de cuenta. Es lo que faltaba: antes el canje NFC solo guardaba en el dispositivo
	/// (y el aislamiento por cuenta lo borraba); ahora la verdad está en el servidor y se restaura al entrar.</summary>
	public void CargarCartasFisicas(int usuarioId)
	{
		if (usuarioId <= 0) return;
		var h = new HttpRequest();
		AddChild(h);
		h.RequestCompleted += (long r, long c, string[] hd, byte[] b) =>
		{
			if (r == (long)HttpRequest.Result.Success && c == 200)
			{
				try
				{
					var arr = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(b));
					if (arr.ValueKind == JsonValueKind.Array)
						foreach (var carta in arr.EnumerateArray())
							if (carta.TryGetProperty("cardId", out var cid))
							{
								string cardId = cid.GetString() ?? "";
								if (!string.IsNullOrEmpty(cardId)) Preferencias.DesbloquearTropa(cardId);
							}
				}
				catch { }
			}
			if (IsInstanceValid(h)) h.QueueFree();
		};
		if (h.Request($"{ApiConfig.Base}/api/cartas/usuario/{usuarioId}") != Error.Ok && IsInstanceValid(h)) h.QueueFree();
	}
}
