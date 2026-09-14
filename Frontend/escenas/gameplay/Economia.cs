using Godot;

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

	// ── Recompensas base por resultado (ajustables) ──────────────────────
	public const int RECOMPENSA_VICTORIA = 100;
	public const int RECOMPENSA_EMPATE   = 8;   // empate (incl. ambos se desconectan en online)
	public const int RECOMPENSA_DERROTA  = 0;   // perder o rendirse no da oro
	public const int BONO_POR_RACHA      = 25;   // extra por cada victoria en racha

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
		EmitSignal(SignalName.MonedasCambiaron, _monedas);
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

	/// <summary>Calcula y otorga la recompensa de una partida. Devuelve lo ganado.</summary>
	public int RecompensarPartida(string resultado, int racha)
	{
		int ganado = resultado switch
		{
			"victoria" => RECOMPENSA_VICTORIA + Mathf.Max(0, racha - 1) * BONO_POR_RACHA,
			"empate"   => RECOMPENSA_EMPATE,
			_          => RECOMPENSA_DERROTA,
		};
		Agregar(ganado);
		return ganado;
	}

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
		EmitSignal(SignalName.MonedasCambiaron, _monedas);
	}
}
