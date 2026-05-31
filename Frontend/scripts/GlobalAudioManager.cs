using Godot;

public partial class GlobalAudioManager : AudioStreamPlayer
{
	public static GlobalAudioManager Instance { get; private set; }

	private float _lastVolume = 0.5f;
	private bool _isMuted = false;

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
				CambiarVolumen(_lastVolume);
			}
		}
		else
		{
			QueueFree();
		}
	}

	public void CambiarVolumen(float valor)
	{
		_lastVolume = valor;
		if (!_isMuted)
		{
			VolumeDb = (float)Mathf.LinearToDb(valor);
		}
	}

	public void SetMute(bool isMuted)
	{
		_isMuted = isMuted;
		if (_isMuted) VolumeDb = -80f;
		else VolumeDb = (float)Mathf.LinearToDb(_lastVolume);
	}

	public float GetVolumen() => _lastVolume;
	public bool IsMuted() => _isMuted;
}
