using Godot;
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
	private const string RUTA_GUARDADO = "user://economia.cfg";
	private const string SECCION       = "jugador";

	public static Economia Instance { get; private set; }

	[Signal] public delegate void MonedasCambiaronEventHandler(int nuevoTotal);

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
	public const int BONO_POR_RACHA             = 25;

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

	public void Agregar(int cantidad)
	{
		if (cantidad <= 0) return;
		_monedas += cantidad;
		Guardar();
		EnviarSaldoAlServidor();
		EmitSignal(SignalName.MonedasCambiaron, _monedas);
	}

	public bool TieneSuficiente(int costo) => _monedas >= costo;

	/// <summary>Intenta gastar. Devuelve true si había saldo suficiente.</summary>
	public bool Gastar(int costo)
	{
		if (costo <= 0 || _monedas < costo) return false;
		_monedas -= costo;
		Guardar();
		EnviarSaldoAlServidor();
		EmitSignal(SignalName.MonedasCambiaron, _monedas);
		return true;
	}

	/// <summary>Calcula y otorga la recompensa de una partida. Devuelve lo ganado.</summary>
	public int RecompensarPartida(string resultado, int racha, bool esOnline = false)
	{
		int ganado = 0;
		if (resultado == "victoria")
		{
			ganado = esOnline ? RECOMPENSA_ONLINE_VICTORIA : RECOMPENSA_BOT_VICTORIA;
			if (esOnline && racha > 1)
			{
				ganado += Mathf.Max(0, racha - 1) * BONO_POR_RACHA;
			}
		}
		else if (resultado == "derrota")
		{
			ganado = esOnline ? RECOMPENSA_ONLINE_DERROTA : RECOMPENSA_BOT_DERROTA;
		}
		else // empate
		{
			ganado = esOnline ? 15 : RECOMPENSA_EMPATE;
		}

		Agregar(ganado);
		return ganado;
	}

	public int RecompensarPartida(string resultado, int racha) => RecompensarPartida(resultado, racha, false);

	// ── PERSISTENCIA ─────────────────────────────────────────────────────

	private void Cargar()
	{
		var cfg = new ConfigFile();
		Error err = cfg.Load(RUTA_GUARDADO);
		if (err == Error.Ok)
			_monedas = (int)cfg.GetValue(SECCION, "monedas", 0);
		else
			_monedas = 0;   // primera vez
	}

	private void Guardar()
	{
		var cfg = new ConfigFile();
		cfg.SetValue(SECCION, "monedas", _monedas);
		cfg.Save(RUTA_GUARDADO);
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
}
