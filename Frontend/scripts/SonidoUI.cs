using Godot;

/// <summary>
/// Sonido de "clic" para los botones de toda la interfaz.
///
/// El proyecto no trae ningún archivo de efecto para esto (solo hay música), así que el clic se
/// SINTETIZA por código, igual que ya se hacía con el pitido de la cuenta regresiva y la fanfarria
/// de inicio (ver Campo1.Intro.cs). Si algún día se agrega un archivo en
/// res://efectos/sfx/click_boton.(wav|ogg|mp3), se usa ese en vez del generado.
///
/// Uso: <c>SonidoUI.EngancharBotones(this);</c> una sola vez, pasando el nodo raíz de la pantalla.
/// Recorre el árbol y le pone el sonido a todos los botones (Button, TextureButton, etc.) de golpe,
/// sin tener que tocarlos uno por uno.
/// </summary>
public static class SonidoUI
{
	private const string RUTA_SFX_CLICK = "res://efectos/sfx/click_boton";
	private const float  VOLUMEN_DB     = -12f; // discreto: acompaña, no tapa la música

	private static AudioStream _click;

	private static AudioStream Click()
	{
		if (_click != null) return _click;
		foreach (string ext in new[] { ".wav", ".ogg", ".mp3" })
			if (ResourceLoader.Exists(RUTA_SFX_CLICK + ext))
				return _click = GD.Load<AudioStream>(RUTA_SFX_CLICK + ext);
		return _click = GenerarClick();
	}

	/// <summary>Clic corto y seco (0.07s): un golpe de aire con dos tonos altos que se apagan de
	/// inmediato. Suena a "tap" de interfaz, no a pitido musical.</summary>
	private static AudioStreamWav GenerarClick()
	{
		const int MUESTREO = 22050;
		int total = (int)(MUESTREO * 0.07f);
		var datos = new byte[total * 2];
		var rnd = new System.Random(1337); // semilla fija: el clic suena siempre igual

		for (int i = 0; i < total; i++)
		{
			float t = (float)i / MUESTREO;
			// Ataque casi instantáneo y caída muy rápida.
			float envolvente = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-t * 70f);
			float tono  = Mathf.Sin(Mathf.Tau * 1200f * t) * 0.55f
						+ Mathf.Sin(Mathf.Tau * 2400f * t) * 0.25f;
			// Pizca de ruido solo en el arranque: le da el "clac" del plástico.
			float ruido = ((float)rnd.NextDouble() * 2f - 1f) * 0.20f * Mathf.Exp(-t * 220f);
			EscribirMuestra(datos, i, (tono + ruido) * envolvente * 0.9f);
		}

		return new AudioStreamWav
		{
			Format  = AudioStreamWav.FormatEnum.Format16Bits,
			MixRate = MUESTREO,
			Stereo  = false,
			Data    = datos,
		};
	}

	private static void EscribirMuestra(byte[] datos, int i, float valor)
	{
		short pcm = (short)(Mathf.Clamp(valor, -1f, 1f) * short.MaxValue);
		datos[i * 2]     = (byte)(pcm & 0xFF);
		datos[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
	}

	/// <summary>Reproduce el clic una vez. El reproductor se autodestruye al terminar, así que se
	/// pueden encadenar toques rápidos sin que uno corte al anterior.</summary>
	public static void Reproducir(Node contexto)
	{
		if (contexto == null || !GodotObject.IsInstanceValid(contexto)) return;
		var tree = contexto.GetTree();
		if (tree == null) return;

		var player = new AudioStreamPlayer
		{
			Stream   = Click(),
			VolumeDb = VOLUMEN_DB,
			Bus      = "Master",
			// Suena también con el juego pausado (menú de pausa, diálogos modales).
			ProcessMode = Node.ProcessModeEnum.Always,
		};
		(tree.CurrentScene ?? tree.Root).AddChild(player);
		player.Finished += player.QueueFree;
		player.Play();
	}

	/// <summary>Le pone el clic a TODOS los botones que cuelgan de "raiz" (incluidos los de escenas
	/// instanciadas). Se puede llamar más de una vez sin miedo: cada botón se engancha una sola vez,
	/// marcado con metadatos.</summary>
	public static void EngancharBotones(Node raiz)
	{
		if (raiz == null || !GodotObject.IsInstanceValid(raiz)) return;

		if (raiz is BaseButton btn && !btn.HasMeta("sfx_click"))
		{
			btn.SetMeta("sfx_click", true);
			btn.Pressed += () => Reproducir(btn);
		}

		foreach (Node hijo in raiz.GetChildren())
			EngancharBotones(hijo);
	}
}
