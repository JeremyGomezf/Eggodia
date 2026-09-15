using Godot;

public partial class Campo1 : Node2D
{
	// ── MAPA "DIGITAL": SECUENCIA DE FONDO SINCRONIZADA CON LA MÚSICA ─────────────────────
	// Solo corre cuando el escenario sorteado es "digital" (ver ESCENARIOS_BATALLA / EscenarioEsDigital
	// en Campo1.cs). En vez de un FONDO fijo, este mapa cambia de imagen (y a veces se voltea, dando
	// un efecto de "glitch") en momentos puntuales de MUSICA DIGITAL.mp3, transcritos del guion que
	// dio el usuario minuto a minuto. Los tiempos son aproximados: puede necesitar un ajuste fino en
	// el editor de Godot una vez se escuche la pista real.
	//
	// FONDO1 = digitalFONDO.png (default) | FONDO2 = digitalFONDO2.png | FONDO3 = digitalFONDO3.png
	// "Oscuro" = sin textura, deja ver el color de fondo del viewport (Zona Fuera de Límites de Godot).
	private const string DIGITAL_FONDO1 = "res://imagenes/Escenarios/digitalFONDO.png";
	private const string DIGITAL_FONDO2 = "res://imagenes/Escenarios/digitalFONDO2.png";
	private const string DIGITAL_FONDO3 = "res://imagenes/Escenarios/digitalFONDO3.png";
	private const string DIGITAL_GLITCH_SHADER = "res://shaders/glitch_fondo.gdshader";

	// Antes probamos un número fijo de corrección (1 segundo) y el resultado fue inconsistente
	// (pasó de ir "adelantada" a quedar "atrasada" 1.5-2s) — GetPlaybackPosition() por sí solo no
	// refleja el momento exacto que se está escuchando, le falta compensar la latencia real del
	// mezclador de audio. AhoraCompensada() usa el método que recomienda Godot para sincronía fina
	// (sumar el tiempo desde el último mix, restar la latencia de salida) en vez de un número
	// adivinado. Con eso ya casi sincronizado, seguía adelantada un poco más — este resto sí es un
	// ajuste a mano; si hiciera falta afinarlo más, es el único número a tocar.
	private const float OFFSET_SYNC_DIGITAL = 0.9f;

	private float AhoraCompensada()
	{
		double pos = _reproductorMusica.GetPlaybackPosition();
		pos += AudioServer.GetTimeSinceLastMix();
		pos -= AudioServer.GetOutputLatency();
		return (float)pos - OFFSET_SYNC_DIGITAL;
	}

	private struct EventoDigital
	{
		public float T;      // segundo de la pista en que se aplica este estado
		public int Fondo;    // 0 = oscuro (sin textura), 1/2/3 = FONDO1/2/3
		public bool FlipV, FlipH;
		public EventoDigital(float t, int fondo, bool flipV = false, bool flipH = false)
		{ T = t; Fondo = fondo; FlipV = flipV; FlipH = flipH; }
	}

	// Timeline completa, en el orden en que se describió: fundido inicial en FONDO1, ráfagas cortas
	// de "glitch" (FONDO2/3 con flips) en momentos puntuales, y un apagón total a los 2:35-2:39.
	// Las ráfagas van con pasos de ~0.08s (no 0.2-0.4s como antes) — se pidió explícitamente que el
	// cambio de imágenes dentro de cada ráfaga sea rapidísimo, casi un flash, no una transición lenta.
	private static readonly EventoDigital[] EVENTOS_DIGITAL =
	{
		new EventoDigital(0f,             1),
		new EventoDigital(16f,            2),
		new EventoDigital(16.08f,         2, flipV: true),
		new EventoDigital(16.16f,         2),
		new EventoDigital(17f,            1),
		new EventoDigital(19f,            2, flipV: true, flipH: true),
		new EventoDigital(19.08f,         2),
		new EventoDigital(19.16f,         2, flipV: true, flipH: true),
		new EventoDigital(19.24f,         2),
		new EventoDigital(20f,            1),
		new EventoDigital(22f,            2),
		new EventoDigital(22.08f,         2, flipV: true),
		new EventoDigital(22.16f,         3),
		new EventoDigital(22.24f,         1), // FONDO3 dura lo mismo que FONDO2 (rápido), no se atrasa
		new EventoDigital(44f,            2, flipV: true, flipH: true),
		new EventoDigital(44.08f,         2),
		new EventoDigital(44.16f,         2, flipV: true, flipH: true),
		new EventoDigital(44.24f,         2),
		new EventoDigital(45f,            1), // hasta 1:05
		new EventoDigital(66f,            2, flipH: true),
		new EventoDigital(66.08f,         2, flipV: true),
		new EventoDigital(66.16f,         3),
		new EventoDigital(66.24f,         2),
		new EventoDigital(66.32f,         3), // finaliza en FONDO3
		new EventoDigital(66.4f,          1), // hasta 1:50
		new EventoDigital(111f,           2),
		new EventoDigital(111.08f,        2, flipV: true),
		new EventoDigital(111.16f,        2),
		new EventoDigital(112f,           1), // hasta 2:12
		new EventoDigital(133f,           2, flipV: true),
		new EventoDigital(133.08f,        3), // llegando a 2:14
		new EventoDigital(133.16f,        1), // hasta 2:29
		new EventoDigital(150f,           3),
		new EventoDigital(150.08f,        1), // hasta 2:31
		new EventoDigital(155f,           0), // 2:35 — apagón total
		new EventoDigital(160f,           1), // 2:40 — hasta 2:56
		new EventoDigital(177f,           2), // 2:57
		new EventoDigital(178f,           1), // 2:58 — hasta 3:00
	};

	private int  _cursorEventoDigital;
	private bool _secuenciaDigitalCongelada;
	private Sprite2D _fondoDigitalNode;
	private ShaderMaterial _glitchMaterialFondo;
	private Tween _tweenGlitchFondo;

	/// <summary>Arranca el sondeo de la secuencia — llamado desde IniciarMusicaPartida() cuando el
	/// escenario sorteado es "digital". No hace nada fuera de ese mapa.</summary>
	private void IniciarSecuenciaDigital()
	{
		if (!EscenarioEsDigital) return;
		_fondoDigitalNode = GetNodeOrNull<Sprite2D>("FONDO");
		_cursorEventoDigital = 0;
		_secuenciaDigitalCongelada = false;

		if (ResourceLoader.Exists(DIGITAL_GLITCH_SHADER))
		{
			_glitchMaterialFondo = new ShaderMaterial { Shader = GD.Load<Shader>(DIGITAL_GLITCH_SHADER) };
			_glitchMaterialFondo.SetShaderParameter("strength", 0.0f);
		}

		// La pista no debe hacer loop "crudo" (eso desincronizaría el audio de la secuencia visual,
		// que se quedaría congelada en el último evento): en vez de eso, escuchamos el fin real de
		// la reproducción y reiniciamos TODO junto — música y secuencia — desde el principio.
		if (_reproductorMusica != null)
		{
			if (_reproductorMusica.Stream is AudioStreamMP3 mp3) mp3.Loop = false;
			_reproductorMusica.Finished += OnMusicaDigitalTerminada;
		}
	}

	private void OnMusicaDigitalTerminada()
	{
		// Si ya terminó la partida (victoria/derrota congeló la secuencia), no reiniciar nada —
		// se queda tal como pide el usuario ("ahí se queda").
		if (!EscenarioEsDigital || _secuenciaDigitalCongelada || juegoTerminado) return;

		_cursorEventoDigital = 0;
		AplicarEventoDigital(new EventoDigital(0f, 1));
		_reproductorMusica.Play(0f);
	}

	public override void _Process(double delta)
	{
		if (!EscenarioEsDigital || _secuenciaDigitalCongelada) return;
		if (_reproductorMusica == null || _fondoDigitalNode == null) return;
		if (!_reproductorMusica.Playing) return;

		float pos = AhoraCompensada();
		// Aplica en orden todos los eventos cuyo tiempo ya se cruzó — un cursor, no se re-escanea
		// la lista completa cada frame.
		while (_cursorEventoDigital < EVENTOS_DIGITAL.Length && pos >= EVENTOS_DIGITAL[_cursorEventoDigital].T)
		{
			AplicarEventoDigital(EVENTOS_DIGITAL[_cursorEventoDigital]);
			_cursorEventoDigital++;
		}
	}

	private void AplicarEventoDigital(EventoDigital ev)
	{
		if (_fondoDigitalNode == null) return;
		_fondoDigitalNode.Texture = ev.Fondo switch
		{
			1 => ResourceLoader.Exists(DIGITAL_FONDO1) ? GD.Load<Texture2D>(DIGITAL_FONDO1) : null,
			2 => ResourceLoader.Exists(DIGITAL_FONDO2) ? GD.Load<Texture2D>(DIGITAL_FONDO2) : null,
			3 => ResourceLoader.Exists(DIGITAL_FONDO3) ? GD.Load<Texture2D>(DIGITAL_FONDO3) : null,
			_ => null, // 0 = apagón: sin textura, se ve el color de fondo del viewport
		};
		_fondoDigitalNode.FlipV = ev.FlipV;
		_fondoDigitalNode.FlipH = ev.FlipH;

		// Glitch SOLO cuando el fondo activo es FONDO2 (sin importar si además está en flip V/H) —
		// nunca toca el nodo "ESCENARIO" (primer plano), solo el "FONDO".
		if (ev.Fondo == 2) DispararGlitchFondo();
		else DetenerGlitchFondo();
	}

	private void DispararGlitchFondo()
	{
		if (_fondoDigitalNode == null || _glitchMaterialFondo == null) return;
		_fondoDigitalNode.Material = _glitchMaterialFondo;
		_glitchMaterialFondo.SetShaderParameter("time_seed", (float)Time.GetTicksMsec() / 1000f);

		// Rapidísimo, como pidió el usuario — antes (0.08s+0.25s) se sentía como si frenara el
		// resto de la secuencia de cambios de fondo. Ahora es un flash corto, casi instantáneo.
		_tweenGlitchFondo?.Kill();
		_tweenGlitchFondo = CreateTween();
		_tweenGlitchFondo.TweenMethod(
			Callable.From<float>(v => _glitchMaterialFondo?.SetShaderParameter("strength", v)),
			0.0f, 1.0f, 0.02f);
		_tweenGlitchFondo.TweenMethod(
			Callable.From<float>(v => _glitchMaterialFondo?.SetShaderParameter("strength", v)),
			1.0f, 0.0f, 0.06f);
	}

	private void DetenerGlitchFondo()
	{
		_tweenGlitchFondo?.Kill();
		if (_fondoDigitalNode != null && _fondoDigitalNode.Material == _glitchMaterialFondo)
			_fondoDigitalNode.Material = null;
	}

	/// <summary>Congela la secuencia en el estado de victoria (FONDO1 fijo) o derrota (FONDO3 fijo
	/// — antes era el apagón total, se pidió cambiarlo por FONDO3 en vez de nada), llamado desde
	/// FinalizarPartida() justo antes de fijar la música en el segundo de cierre — evita que la
	/// secuencia se siga moviendo por debajo de la pantalla de resultado.</summary>
	private void CongelarSecuenciaDigital(bool victoria)
	{
		if (!EscenarioEsDigital) return;
		_secuenciaDigitalCongelada = true;
		DetenerGlitchFondo();
		AplicarEventoDigital(new EventoDigital(0f, victoria ? 1 : 3));
	}
}
