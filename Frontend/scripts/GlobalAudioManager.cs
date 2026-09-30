using Godot;

public partial class GlobalAudioManager : AudioStreamPlayer
{
	public static GlobalAudioManager Instance { get; private set; }

	// ── BUSES SEPARADOS ─────────────────────────────────────────────────────
	// Antes TODO sonaba por el bus Master, así que silenciar "la música" apagaba también los botones
	// y las tropas. Ahora hay dos buses hijos de Master: "Musica" (menú + batalla) y "Efectos" (todo lo
	// demás). Cada botón de Ajustes silencia solo el suyo; el slider de volumen sigue siendo general.
	public const string BUS_MUSICA  = "Musica";
	public const string BUS_EFECTOS = "Efectos";
	private const string RUTA_CONFIG_AUDIO = "user://audio.cfg";

	private float _lastVolume = 0.5f;
	private bool _musicaMuteada  = false;
	private bool _efectosMuteados = false;

	private AudioStreamWav _clickSound;

	public override void _Ready()
	{
		if (Instance == null)
		{
			Instance = this;
			ProcessMode = ProcessModeEnum.Always; // Keep playing when paused
			CrearBuses();
			CargarPreferenciasAudio();
			Bus = BUS_MUSICA; // la música del menú es MÚSICA, no un efecto

			var stream = ResourceLoader.Load<AudioStream>("res://musica/Tide_of_the_First_King.mp3");
			if (stream != null)
			{
				if (stream is AudioStreamMP3 mp3) mp3.Loop = true;
				Stream = stream;
				Autoplay = true;
				// Antes saltaba de golpe a volumen normal; ahora entra bajito y sube a lo largo de
				// 2.5s — solo la música del menú principal, nada más.
				VolumeDb = -40f;
				Play();
				Tween twFadeIn = CreateTween();
				twFadeIn.TweenProperty(this, "volume_db", 0f, 2.5f);
			}

			// Aplicar volumen inicial al bus Master para que afecte todo el audio
			AudioServer.SetBusVolumeDb(0, (float)Mathf.LinearToDb(_lastVolume));

			// Hook automatically to any new button added to the tree to play a click sound
			GetTree().NodeAdded += OnNodeAdded;
		}
		else
		{
			QueueFree();
		}
	}

	private const int SFX_POOL_SIZE = 3;
	private AudioStreamPlayer[] _sfxPool = new AudioStreamPlayer[SFX_POOL_SIZE];
	private int _nextPoolIndex = 0;

	private const int HOVER_POOL_SIZE = 3;
	private AudioStreamPlayer[] _hoverPool = new AudioStreamPlayer[HOVER_POOL_SIZE];
	private int _nextHoverIndex = 0;

	private AudioStreamWav _hoverSound;

	private void OnNodeAdded(Node node)
	{
		// Cualquier reproductor que entre al árbol sin bus propio (Master) es un EFECTO: botones,
		// tropas, terminal de invocación, cuenta regresiva, explosiones… La música se crea ya con
		// Bus = BUS_MUSICA, así que no se toca.
		if (node is AudioStreamPlayer p && p.Bus == "Master")     p.Bus = BUS_EFECTOS;
		else if (node is AudioStreamPlayer2D p2 && p2.Bus == "Master") p2.Bus = BUS_EFECTOS;
		else if (node is AudioStreamPlayer3D p3 && p3.Bus == "Master") p3.Bus = BUS_EFECTOS;

		// Solo click sound: el juego es para móvil y MouseEntered no aplica en touch
		if (node is BaseButton btn)
		{
			// Evitar doble conexión: al re-parentar un botón (RemoveChild+AddChild,
			// p.ej. MoverACanvasInmune) NodeAdded se dispara otra vez para el mismo
			// botón. Conectar solo si aún no está conectado.
			var callable = Callable.From(PlayClickSound);
			if (!btn.IsConnected(BaseButton.SignalName.ButtonDown, callable))
				btn.Connect(BaseButton.SignalName.ButtonDown, callable);
		}
	}

	public void PlayClickSound()
	{
		if (_clickSound == null)
		{
			_clickSound = GenerateClickSound();
		}

		var player = _sfxPool[_nextPoolIndex];
		if (player == null || !IsInstanceValid(player))
		{
			player = new AudioStreamPlayer();
			player.Stream = _clickSound;
			player.VolumeDb = -4.0f; // Clear audible volume
			player.ProcessMode = ProcessModeEnum.Always;
			AddChild(player);
			_sfxPool[_nextPoolIndex] = player;
		}

		player.Play();
		_nextPoolIndex = (_nextPoolIndex + 1) % SFX_POOL_SIZE;
	}

	public void PlayHoverSound()
	{
		if (_hoverSound == null)
		{
			_hoverSound = GenerateHoverSound();
		}

		var player = _hoverPool[_nextHoverIndex];
		if (player == null || !IsInstanceValid(player))
		{
			player = new AudioStreamPlayer();
			player.Stream = _hoverSound;
			player.VolumeDb = -12.0f; // Soft click/tick sound
			player.ProcessMode = ProcessModeEnum.Always;
			AddChild(player);
			_hoverPool[_nextHoverIndex] = player;
		}

		player.Play();
		_nextHoverIndex = (_nextHoverIndex + 1) % HOVER_POOL_SIZE;
	}

	private AudioStreamWav GenerateClickSound()
	{
		int sampleRate = 44100;
		float duration = 0.08f;
		int numSamples = (int)(sampleRate * duration);
		byte[] data = new byte[numSamples * 2];

		for (int i = 0; i < numSamples; i++)
		{
			float t = (float)i / sampleRate;
			float freq = 500f - (t / duration) * 250f;
			float angle = 2f * Mathf.Pi * freq * t;
			float amplitude = Mathf.Sin(angle);

			float envelope = 1.0f - (float)i / numSamples;
			amplitude *= envelope * envelope;

			short sampleVal = (short)(amplitude * 32767);
			data[i * 2] = (byte)(sampleVal & 0xFF);
			data[i * 2 + 1] = (byte)((sampleVal >> 8) & 0xFF);
		}

		AudioStreamWav stream = new AudioStreamWav();
		stream.Format = AudioStreamWav.FormatEnum.Format16Bits;
		stream.MixRate = sampleRate;
		stream.Data = data;
		stream.Stereo = false;

		return stream;
	}

	private AudioStreamWav GenerateHoverSound()
	{
		int sampleRate = 44100;
		float duration = 0.03f; // 30ms very short soft tick
		int numSamples = (int)(sampleRate * duration);
		byte[] data = new byte[numSamples * 2];

		for (int i = 0; i < numSamples; i++)
		{
			float t = (float)i / sampleRate;
			float freq = 800f; // Higher pitch soft tick
			float angle = 2f * Mathf.Pi * freq * t;
			float amplitude = Mathf.Sin(angle);

			float envelope = 1.0f - (float)i / numSamples;
			amplitude *= envelope * envelope;

			short sampleVal = (short)(amplitude * 16384);
			data[i * 2] = (byte)(sampleVal & 0xFF);
			data[i * 2 + 1] = (byte)((sampleVal >> 8) & 0xFF);
		}

		AudioStreamWav stream = new AudioStreamWav();
		stream.Format = AudioStreamWav.FormatEnum.Format16Bits;
		stream.MixRate = sampleRate;
		stream.Data = data;
		stream.Stereo = false;

		return stream;
	}

	/// <summary>Reanuda la música global si quedó detenida (p. ej. al volver de una partida en
	/// Campo1, que la pausa explícitamente vía SilenciarOtrasMusicas). Llamar en el _Ready de
	/// cualquier pantalla de menú a la que se pueda volver tras una batalla.</summary>
	public void AsegurarReproduccion()
	{
		if (Stream != null && !Playing) Play();
	}

	/// <summary>Volumen GENERAL (bus Master): afecta música y efectos por igual.</summary>
	public void CambiarVolumen(float valor)
	{
		_lastVolume = valor;
		AudioServer.SetBusVolumeDb(0, (float)Mathf.LinearToDb(valor));
		GuardarPreferenciasAudio();
	}

	public float GetVolumen() => _lastVolume;

	// ── SILENCIO POR SEPARADO ────────────────────────────────────────────────
	public bool IsMusicaMuteada()  => _musicaMuteada;
	public bool IsEfectosMuteados() => _efectosMuteados;

	public void SetMusicaMuteada(bool muteada)
	{
		_musicaMuteada = muteada;
		AplicarMute(BUS_MUSICA, muteada);
		GuardarPreferenciasAudio();
	}

	public void SetEfectosMuteados(bool muteados)
	{
		_efectosMuteados = muteados;
		AplicarMute(BUS_EFECTOS, muteados);
		GuardarPreferenciasAudio();
	}

	/// <summary>Compatibilidad: silencia/activa TODO (música y efectos a la vez).</summary>
	public void SetMute(bool isMuted) { SetMusicaMuteada(isMuted); SetEfectosMuteados(isMuted); }
	public bool IsMuted() => _musicaMuteada && _efectosMuteados;

	private static void AplicarMute(string bus, bool mute)
	{
		int idx = AudioServer.GetBusIndex(bus);
		if (idx >= 0) AudioServer.SetBusMute(idx, mute);
	}

	/// <summary>Crea los buses "Musica" y "Efectos" (hijos de Master) si todavía no existen.</summary>
	private static void CrearBuses()
	{
		foreach (string nombre in new[] { BUS_MUSICA, BUS_EFECTOS })
		{
			if (AudioServer.GetBusIndex(nombre) >= 0) continue;
			AudioServer.AddBus();
			int idx = AudioServer.BusCount - 1;
			AudioServer.SetBusName(idx, nombre);
			AudioServer.SetBusSend(idx, "Master");
		}
	}

	// Se recuerdan entre sesiones: si apagaste la música, al volver a abrir el juego sigue apagada.
	private void CargarPreferenciasAudio()
	{
		var cfg = new ConfigFile();
		if (cfg.Load(RUTA_CONFIG_AUDIO) == Error.Ok)
		{
			_lastVolume      = (float)cfg.GetValue("audio", "volumen", _lastVolume);
			_musicaMuteada   = (bool)cfg.GetValue("audio", "musica_muteada", false);
			_efectosMuteados = (bool)cfg.GetValue("audio", "efectos_muteados", false);
		}
		AplicarMute(BUS_MUSICA, _musicaMuteada);
		AplicarMute(BUS_EFECTOS, _efectosMuteados);
	}

	private void GuardarPreferenciasAudio()
	{
		var cfg = new ConfigFile();
		cfg.SetValue("audio", "volumen", _lastVolume);
		cfg.SetValue("audio", "musica_muteada", _musicaMuteada);
		cfg.SetValue("audio", "efectos_muteados", _efectosMuteados);
		cfg.Save(RUTA_CONFIG_AUDIO);
	}
}
