using Godot;

/// <summary>
/// "Ducking" de la música: mientras habla un personaje, el bus de MÚSICA baja de volumen y vuelve
/// solo a su nivel normal en cuanto la voz termina. Es lo que hace que en juegos como Clash Royale
/// la música nunca tape los efectos ni las voces.
///
/// Funciona con un CONTADOR de voces activas (no con un simple on/off): si se encadenan varias
/// voces, la música recién sube cuando termina la última. Cada Tomar() debe tener su Soltar().
///
/// El nivel al que baja es relativo al volumen que el bus tenga puesto por el jugador desde
/// Ajustes: se guarda el valor real la primera vez y se restaura ese mismo, así nunca se pisa la
/// configuración de volumen del usuario.
/// </summary>
public static class DuckingMusica
{
	/// <summary>Cuánto baja la música mientras hay una voz sonando.</summary>
	private const float REDUCCION_DB = -12f;
	private const float SEG_BAJAR    = 0.12f; // baja rápido, para no comerse el arranque de la voz
	private const float SEG_SUBIR    = 0.45f; // vuelve suave, sin que se note el salto

	private static int   _vocesActivas;
	private static float _volumenNormalDb;
	private static bool  _volumenGuardado;
	private static Tween _tween;

	private static int IndiceBus() => AudioServer.GetBusIndex(GlobalAudioManager.BUS_MUSICA);

	/// <summary>Una voz empieza a sonar: baja la música (si no estaba ya bajada).</summary>
	public static void Tomar(Node contexto)
	{
		int bus = IndiceBus();
		if (bus < 0) return;

		if (!_volumenGuardado)
		{
			_volumenNormalDb = AudioServer.GetBusVolumeDb(bus);
			_volumenGuardado = true;
		}

		_vocesActivas++;
		if (_vocesActivas == 1) AnimarHasta(contexto, _volumenNormalDb + REDUCCION_DB, SEG_BAJAR);
	}

	/// <summary>Una voz terminó: si era la última, la música vuelve a su volumen normal.</summary>
	public static void Soltar(Node contexto = null)
	{
		if (_vocesActivas <= 0) return;
		_vocesActivas--;
		if (_vocesActivas > 0) return;

		int bus = IndiceBus();
		if (bus < 0 || !_volumenGuardado) return;
		AnimarHasta(contexto, _volumenNormalDb, SEG_SUBIR);
	}

	private static void AnimarHasta(Node contexto, float destinoDb, float segundos)
	{
		int bus = IndiceBus();
		if (bus < 0) return;

		var tree = (contexto != null && GodotObject.IsInstanceValid(contexto)) ? contexto.GetTree() : null;
		if (tree == null) { AudioServer.SetBusVolumeDb(bus, destinoDb); return; } // sin árbol: salto seco

		_tween?.Kill();
		_tween = tree.CreateTween();
		_tween.SetPauseMode(Tween.TweenPauseMode.Process); // también con el juego pausado
		float desde = AudioServer.GetBusVolumeDb(bus);
		_tween.TweenMethod(Callable.From<float>(db => AudioServer.SetBusVolumeDb(bus, db)),
			desde, destinoDb, segundos);
	}

	/// <summary>Corta cualquier ducking en curso y deja la música en su volumen normal. Se llama al
	/// cambiar de escena, por si una voz quedó a medias y nadie soltó su retención.</summary>
	public static void Reiniciar()
	{
		_vocesActivas = 0;
		int bus = IndiceBus();
		if (bus >= 0 && _volumenGuardado)
		{
			_tween?.Kill();
			_tween = null;
			AudioServer.SetBusVolumeDb(bus, _volumenNormalDb);
		}
	}
}
