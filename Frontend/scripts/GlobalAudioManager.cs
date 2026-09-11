using Godot;

public partial class GlobalAudioManager : AudioStreamPlayer
{
	public static GlobalAudioManager Instance { get; private set; }

	private float _lastVolume = 0.5f;
	private bool _isMuted = false;

	private AudioStreamWav _clickSound;

	public override void _Ready()
	{
		if (Instance == null)
		{
			Instance = this;
			ProcessMode = ProcessModeEnum.Always; // Keep playing when paused

			var stream = ResourceLoader.Load<AudioStream>("res://musica/Tide_of_the_First_King.mp3");
			if (stream != null)
			{
				if (stream is AudioStreamMP3 mp3) mp3.Loop = true;
				Stream = stream;
				Autoplay = true;
				Play();
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
		// Solo click sound: el juego es para móvil y MouseEntered no aplica en touch
		if (node is BaseButton btn)
			btn.ButtonDown += () => PlayClickSound();
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

	public void CambiarVolumen(float valor)
	{
		_lastVolume = valor;
		if (!_isMuted)
			AudioServer.SetBusVolumeDb(0, (float)Mathf.LinearToDb(valor));
	}

	public void SetMute(bool isMuted)
	{
		_isMuted = isMuted;
		// Bus Master (índice 0) — afecta TODO el audio del juego
		AudioServer.SetBusVolumeDb(0, _isMuted ? -80f : (float)Mathf.LinearToDb(_lastVolume));
	}

	public float GetVolumen() => _lastVolume;
	public bool IsMuted() => _isMuted;
}
