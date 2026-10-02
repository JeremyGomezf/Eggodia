using Godot;
using System.Collections.Generic;

/// <summary>
/// Precarga en segundo plano de las tropas del bot (VS BOT).
///
/// Antes el bot cargaba la escena de su tropa (GD.Load) en el MISMO instante de invocarla: con las
/// hojas de sprites grandes eso congelaba la partida 0,3–0,8 s cada vez que aparecía una tropa nueva
/// (y hasta 3,5 s en un celular con poca RAM libre). Ahora cada carta que entra a la mano del bot se
/// empieza a cargar en otro hilo apenas la recibe, y el coloso de cada 3 turnos se elige un turno
/// antes; cuando el bot la juega, GD.Load la encuentra ya en memoria y sale al instante.
///
/// Solo se retienen las cartas que el bot tiene AHORA en la mano (y el próximo coloso): si se la
/// roban o la juega, se suelta, así la memoria no crece con cartas que ya no va a usar.
/// </summary>
public partial class Campo1 : Node2D
{
	private readonly Dictionary<string, Resource> _precargadasCPU = new();
	private readonly HashSet<string> _pedidasCPU = new();
	private Timer _timerPrecargaCPU;
	private string _colosoPrecargadoCPU; // coloso elegido de antemano para el próximo turno de coloso

	private void IniciarPrecargaCPU()
	{
		// En línea no hay bot; en el tutorial el bot sigue un guion (no juega de su mano).
		if (EsOnline || ModoTutorial) return;
		_timerPrecargaCPU = new Timer { WaitTime = 0.25, OneShot = false, Autostart = true };
		AddChild(_timerPrecargaCPU);
		_timerPrecargaCPU.Timeout += ActualizarPrecargaCPU;
	}

	private void ActualizarPrecargaCPU()
	{
		// Cargas en curso: al terminar se retienen si la carta sigue en la mano; si no, se recogen igual
		// (LoadThreadedGet) para que el cargador no se quede con ellas.
		var deseadas = CartasAPrecargarCPU();
		foreach (string ruta in new List<string>(_pedidasCPU))
		{
			var estado = ResourceLoader.LoadThreadedGetStatus(ruta);
			if (estado == ResourceLoader.ThreadLoadStatus.InProgress) continue;
			_pedidasCPU.Remove(ruta);
			if (estado != ResourceLoader.ThreadLoadStatus.Loaded) continue; // falló: GD.Load lo intentará igual
			var res = ResourceLoader.LoadThreadedGet(ruta);
			if (res != null && deseadas.Contains(ruta) && !juegoTerminado) _precargadasCPU[ruta] = res;
		}

		if (juegoTerminado)
		{
			_precargadasCPU.Clear();
			if (_pedidasCPU.Count == 0) _timerPrecargaCPU?.Stop();
			return;
		}

		foreach (string ruta in deseadas)
		{
			if (_precargadasCPU.ContainsKey(ruta) || _pedidasCPU.Contains(ruta)) continue;
			if (ResourceLoader.LoadThreadedRequest(ruta) == Error.Ok) _pedidasCPU.Add(ruta);
		}

		foreach (string ruta in new List<string>(_precargadasCPU.Keys))
			if (!deseadas.Contains(ruta)) _precargadasCPU.Remove(ruta);
	}

	private HashSet<string> CartasAPrecargarCPU()
	{
		var rutas = new HashSet<string>();
		if (_cartasCPU != null)
			foreach (int i in _manoVisualCPU)
				if (i >= 0 && i < _cartasCPU.Length) rutas.Add(_cartasCPU[i]);
		if (!string.IsNullOrEmpty(_colosoPrecargadoCPU)) rutas.Add(_colosoPrecargadoCPU);
		return rutas;
	}

	/// <summary>Elige YA el coloso que el bot va a sacar en su próximo turno de coloso (uno que no tenga
	/// en el campo), para que se cargue durante el turno del jugador.</summary>
	private void PrepararColosoCPU()
	{
		if (EsOnline || ModoTutorial || _colososCPU.Count == 0) return;
		var yaEnCampo = TropasEnCampoRival();
		var libres = _colososCPU.FindAll(ruta => !yaEnCampo.Contains(ruta));
		var pool = libres.Count > 0 ? libres : _colososCPU;
		_colosoPrecargadoCPU = pool[random.Next(pool.Count)];
	}
}
