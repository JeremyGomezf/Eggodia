using Godot;
using System;
using System.Threading.Tasks;

public partial class Campo1 : Node2D
{
	// ── MUERTE DEL HUEVO ──────────────────────────────────────────────────
	// Secuencia pedida, igual para el jugador y para el bot (solo cambia el lado):
	//   1. El huevo se aplasta contra el trono como si fuera de goma.
	//   2. Rebota y pega un brinco — ahí suelta el grito caricaturesco.
	//   3. Sale volando HACIA ATRÁS (el nuestro a la izquierda, el del bot a la derecha) girando,
	//      hasta irse por completo del escenario.
	//   4. Recién cuando ya salió de pantalla se escucha el CRACK del huevo rompiéndose.
	//   5. Después de eso sigue el resto del cierre (la frase de victoria/derrota).

	// Valores medidos para que el brinco se VEA: es un briquito corto y suave, no un salto violento,
	// y la salida es cortita — antes se iba tan lejos y tan alto que no se alcanzaba a ver nada.
	private const float SEG_APLASTE   = 0.30f; // se achata contra el trono
	private const float SEG_BRINCO    = 0.34f; // brinco corto y vivo (acá entra el grito)
	private const float SEG_SALIDA    = 0.72f; // se va del escenario (se pidió más rápido que 1.05)
	private const float GIRO_SALIDA   = 3.4f;  // vueltas al salir: más giro, también a pedido
	private const float ALTURA_BRINCO = 130f;  // un poco más alto que el saltito mínimo, sin pasarse
	// CALCULADO, no a ojo: para que la velocidad horizontal no pegue un salto entre el brinco y la
	// salida, tiene que ser la misma en las dos fases. La salida recorre 520px en 0.72s = 722 px/s,
	// así que el brinco (0.34s) tiene que cubrir 722 × 0.34 ≈ 245px. Con esto el huevo sale en
	// diagonal desde el principio en vez de subir recto y recién después irse al costado.
	private const float AVANCE_BRINCO = 245f;
	private const float DISTANCIA_SALIDA = 520f; // lo justo para salir de cuadro, sin desaparecer de golpe
	private const float CAIDA_SALIDA     = 300f; // cuánto baja mientras se va

	private const string RUTA_SFX_GRITO = "res://efectos/sonidos/huevo_grito";
	private const string RUTA_SFX_CRACK = "res://efectos/sonidos/huevo_crack";
	private const string RUTA_SFX_STOP  = "res://efectos/sonidos/frenada_stop";
	private static AudioStream _sfxGrito, _sfxCrack, _sfxStop;

	// Antes de que el huevo se vaya hay una pausa para que se ENTIENDA quién murió: la barra ya quedó
	// vacía, suena el frenazo y recién después arranca la animación. Sin esto pasaba tan rápido que ni
	// se notaba el detalle.
	private const float SEG_ANTES_DEL_STOP  = 0.45f; // primero se ve la barra vacía
	private const float SEG_DESPUES_DEL_STOP = 2.2f; // el frenazo suena y recién ahí se va

	/// <summary>Anima la muerte del huevo que perdió y no vuelve hasta que terminó (incluido el CRACK).
	/// <paramref name="perdioElJugador"/>: true si el huevo que revienta es el nuestro.</summary>
	private async Task AnimarMuerteHuevo(bool perdioElJugador)
	{
		TronoCampo trono = perdioElJugador ? tronoJugador : tronoRival;
		if (trono == null || !IsInstanceValid(trono)) return;

		Node2D huevo = trono.Huevo;
		if (huevo == null || !IsInstanceValid(huevo)) return;

		trono.DetenerIdle(); // si no, la respiración en bucle pelea con el aplastado

		// La música se CORTA acá: durante toda la muerte solo se escuchan los efectos (frenazo, grito
		// y CRACK). La de victoria/derrota arranca después, ya con la frase (ver FinalizarPartida).
		_reproductorMusica?.Stop();

		// La barra del que perdió se vacía a la vista, no de golpe: pase lo que pase (rendirse, que se
		// acabe el tiempo o morir en combate) se ve caer hasta cero. Vale igual para el jugador y el bot.
		VaciarBarraVida(perdioElJugador);

		// PAUSA DRAMÁTICA: barra vaciándose → frenazo → recién ahí se va. ~2.65s en total, para que se
		// entienda bien quién perdió antes de que empiece el movimiento.
		await EsperarSegurosMuerte(SEG_ANTES_DEL_STOP);
		if (!IsInstanceValid(huevo)) return;
		ReproducirStopHuevo();
		await EsperarSegurosMuerte(SEG_DESPUES_DEL_STOP);
		if (!IsInstanceValid(huevo)) return;

		// Se lo saca de la jerarquía del trono (TopLevel) para poder volarlo en coordenadas de mundo
		// sin que lo recorte ni lo arrastre el trono, y por encima de todo lo del tablero.
		Vector2 posMundo = huevo.GlobalPosition;
		Vector2 escalaBase = huevo.Scale;
		huevo.TopLevel = true;
		huevo.GlobalPosition = posMundo;
		huevo.ZIndex = 400;

		// El nuestro sale hacia la izquierda; el del bot, espejado hacia la derecha.
		float sentido = perdioElJugador ? -1f : 1f;

		// 1) APLASTE: se achata como goma (ancho ↑, alto ↓), como una pelota contra el piso.
		var twAplaste = CreateTween().SetParallel(true);
		twAplaste.TweenProperty(huevo, "scale", new Vector2(escalaBase.X * 1.25f, escalaBase.Y * 0.72f), SEG_APLASTE)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		twAplaste.TweenProperty(huevo, "global_position:y", posMundo.Y + 14f, SEG_APLASTE)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		await ToSignal(twAplaste, Tween.SignalName.Finished);
		if (!IsInstanceValid(huevo)) return;

		// 2) BRINCO: un saltito suave, estirándose apenas al despegar. Acá grita.
		ReproducirGritoHuevo();
		var twBrinco = CreateTween().SetParallel(true);
		// Quad/Out: despega de golpe y desacelera al llegar arriba, como un salto de verdad. Con
		// Sine/Out subía a velocidad pareja y por eso se veía robótico y lento.
		twBrinco.TweenProperty(huevo, "scale", new Vector2(escalaBase.X * 0.92f, escalaBase.Y * 1.12f), SEG_BRINCO * 0.5f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		// X e Y van POR SEPARADO a propósito. Antes iban juntas en un solo TweenProperty con Quad/Out,
		// así que al llegar arriba el huevo se frenaba en los DOS ejes y quedaba colgado un instante;
		// recién después arrancaba la salida y ahí se iba de golpe al costado. Eso era el "cae recto y
		// después se va a la izquierda". Ahora la X avanza LINEAL (velocidad constante, como cualquier
		// cosa lanzada al aire) y solo la Y frena arriba, que es lo correcto: el arco queda continuo.
		twBrinco.TweenProperty(huevo, "global_position:x", posMundo.X + sentido * AVANCE_BRINCO, SEG_BRINCO)
			.SetTrans(Tween.TransitionType.Linear);
		twBrinco.TweenProperty(huevo, "global_position:y", posMundo.Y - ALTURA_BRINCO, SEG_BRINCO)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		twBrinco.TweenProperty(huevo, "rotation", sentido * 0.35f, SEG_BRINCO)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		await ToSignal(twBrinco, Tween.SignalName.Finished);
		if (!IsInstanceValid(huevo)) return;

		// 3) SALIDA: se va de cuadro girando, cayendo. Se pidió un poco más rápida y con más giro.
		var twSalida = CreateTween().SetParallel(true);
		// Misma idea que en el brinco: la X sigue LINEAL, con la misma velocidad que traía (ver
		// AVANCE_BRINCO), así no hay ningún salto de velocidad al pasar de subir a caer. La Y sí
		// acelera hacia abajo (Quad/In), que es la gravedad. Las dos fases se leen como un solo arco.
		twSalida.TweenProperty(huevo, "global_position:x",
				posMundo.X + sentido * (AVANCE_BRINCO + DISTANCIA_SALIDA), SEG_SALIDA)
			.SetTrans(Tween.TransitionType.Linear);
		twSalida.TweenProperty(huevo, "global_position:y", posMundo.Y + CAIDA_SALIDA, SEG_SALIDA)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		twSalida.TweenProperty(huevo, "rotation", sentido * GIRO_SALIDA, SEG_SALIDA)
			.SetTrans(Tween.TransitionType.Sine);
		twSalida.TweenProperty(huevo, "scale", escalaBase, SEG_SALIDA);
		twSalida.Chain().TweenProperty(huevo, "modulate:a", 0f, 0.18f); // se apaga justo al salir
		await ToSignal(twSalida, Tween.SignalName.Finished);

		// 4) Ya salió del escenario: ahora sí, el huevo se rompe.
		if (IsInstanceValid(huevo)) huevo.QueueFree();
		ReproducirCrackHuevo();
		await EsperarSegurosMuerte(0.45f); // que el CRACK se escuche antes de la frase de cierre
	}

	// ── SACUDÓN DEL HUEVO CUANDO LE MATAN UNA TROPA ───────────────────────
	// Cuanto más pesada la tropa, más se mueve el huevo de su dueño. Es solo de izquierda a derecha,
	// sin saltar, y vuelve solo a su sitio.
	private const float TEMBLOR_TACTICO = 5f;   // poquito
	private const float TEMBLOR_ASESINO = 10f;  // medio poco
	private const float TEMBLOR_COLOSO  = 18f;  // ahí sí se nota
	private const float SEG_TEMBLOR     = 0.085f; // cada ida/vuelta

	/// <summary>Tiembla el huevo del bando al que le acaban de matar una tropa.</summary>
	private void SacudirHuevoPorMuerteDeTropa(Node2D tropa)
	{
		if (tropa == null || !IsInstanceValid(tropa)) return;

		float fuerza = ClasificacionCartas.Clasificar(tropa.SceneFilePath).Tipo switch
		{
			TipoTropa.Coloso  => TEMBLOR_COLOSO,
			TipoTropa.Asesino => TEMBLOR_ASESINO,
			_                 => TEMBLOR_TACTICO,
		};
		TemblarHuevo(tropa.IsInGroup("tropas_jugador"), fuerza);
	}

	private void TemblarHuevo(bool esDelJugador, float fuerza)
	{
		TronoCampo trono = esDelJugador ? tronoJugador : tronoRival;
		Node2D huevo = trono != null && IsInstanceValid(trono) ? trono.Huevo : null;
		if (huevo == null || !IsInstanceValid(huevo)) return;

		float baseX = huevo.Position.X;
		Tween tw = huevo.CreateTween();
		// Zigzag que se va apagando y termina SIEMPRE en la posición original.
		foreach (float f in new[] { 1f, -0.75f, 0.5f, -0.3f })
			tw.TweenProperty(huevo, "position:x", baseX + fuerza * f, SEG_TEMBLOR)
			  .SetTrans(Tween.TransitionType.Sine);
		tw.TweenProperty(huevo, "position:x", baseX, SEG_TEMBLOR).SetTrans(Tween.TransitionType.Sine);
	}

	// ── BAILE DEL GANADOR ─────────────────────────────────────────────────
	private const float BAILE_DESPLAZAMIENTO = 9f;    // apenas se mece de lado a lado
	private const float BAILE_INCLINACION    = 0.045f; // se ladea un pelín, casi imperceptible
	private const float SEG_BAILE_PASO       = 0.5f;   // lento = calmado (antes 0.30, se sacudía)
	private const float SEG_BAILE_TOTAL      = 20f;    // festeja 20s y vuelve solo a la normalidad
	private const float SEG_BAILE_RAPIDO     = 5f;     // los primeros 5s va más animado
	// Valores EXACTOS de la primera versión del baile (la que gustó): ni más rápida ni más amplia.
	// Después de SEG_BAILE_RAPIDO pasa a los valores calmados de arriba.
	private const float DESPLAZAMIENTO_PRIMERA_VERSION = 16f;
	private const float INCLINACION_PRIMERA_VERSION    = 0.10f;
	private const float SEG_PASO_PRIMERA_VERSION       = 0.30f;

	/// <summary>El huevo que GANÓ festeja: se mece de lado a lado, inclinándose, en el sitio.
	/// Arranca junto con la frase de cierre, una vez que el perdedor ya se fue.</summary>
	private void BailarHuevoGanador(bool ganoElJugador)
	{
		TronoCampo trono = ganoElJugador ? tronoJugador : tronoRival;
		Node2D huevo = trono != null && IsInstanceValid(trono) ? trono.Huevo : null;
		if (huevo == null || !IsInstanceValid(huevo)) return;

		trono.DetenerIdle(); // la respiración en bucle pelearía con el baile

		float baseX = huevo.Position.X;
		float baseRot = huevo.Rotation;

		// Una vuelta completa (derecha + izquierda) dura 2 pasos, así que 20s son SEG_BAILE_TOTAL
		// dividido eso. Es un número de repeticiones concreto, NO un bucle infinito: al terminar el
		// huevo queda quieto en su sitio de siempre.
		// Dos tramos: arranca EUFÓRICO (rápido y amplio, como festejando de verdad) los primeros
		// segundos y después se va calmando hasta quedar meciéndose despacio.
		// El baile NO cambia de velocidad de golpe: arranca como la primera versión (la que gustó) y se
		// va frenando paso a paso hasta quedar meciéndose despacio al final de los 20s. Cada vaivén
		// dura un poco más y se mueve un poco menos que el anterior, así la desaceleración no se nota
		// como un corte sino como que se va calmando solo.
		Tween tw = huevo.CreateTween();
		float transcurrido = 0f;
		int paso = 0;
		while (transcurrido < SEG_BAILE_TOTAL)
		{
			// 0 al principio → 1 al final. Los primeros SEG_BAILE_RAPIDO segundos se mantiene al ritmo
			// original; recién después empieza a frenar.
			float t = Mathf.Clamp(
				(transcurrido - SEG_BAILE_RAPIDO) / Mathf.Max(0.01f, SEG_BAILE_TOTAL - SEG_BAILE_RAPIDO),
				0f, 1f);

			float segPaso  = Mathf.Lerp(SEG_PASO_PRIMERA_VERSION, SEG_BAILE_PASO, t);
			float desplaz  = Mathf.Lerp(DESPLAZAMIENTO_PRIMERA_VERSION, BAILE_DESPLAZAMIENTO, t);
			float inclina  = Mathf.Lerp(INCLINACION_PRIMERA_VERSION, BAILE_INCLINACION, t);
			int   dir      = (paso % 2 == 0) ? 1 : -1;

			tw.TweenProperty(huevo, "position:x", baseX + desplaz * dir, segPaso)
			  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			tw.Parallel().TweenProperty(huevo, "rotation", baseRot + inclina * dir, segPaso)
			  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

			transcurrido += segPaso;
			paso++;
		}

		// Al cumplirse los 20s vuelve exactamente a su posición e inclinación originales.
		tw.Finished += () =>
		{
			if (!IsInstanceValid(huevo)) return;
			Tween fin = huevo.CreateTween().SetParallel(true);
			fin.TweenProperty(huevo, "position:x", baseX, SEG_BAILE_PASO).SetTrans(Tween.TransitionType.Sine);
			fin.TweenProperty(huevo, "rotation", baseRot, SEG_BAILE_PASO).SetTrans(Tween.TransitionType.Sine);
		};
	}

	private const float SEG_VACIAR_BARRA = 0.55f; // rapidito, pero se ve bajar
	private const float SEG_MOVER_BARRA  = 0.35f; // golpes y curaciones durante la partida

	// Último valor pedido a cada barra y su animación en curso.
	private readonly System.Collections.Generic.Dictionary<TextureProgressBar, (double objetivo, Tween tween)> _animBarras = new();

	// Barras del huevo que ya perdió: quedan en 0 hasta el final. Al rendirse, al acabarse el tiempo o
	// con una retirada en línea, la vida NO llega a 0 de verdad; la animación de muerte vacía la barra
	// igual, y cualquier refresco posterior de la interfaz la volvía a subir ("se vacía y regresa").
	private readonly System.Collections.Generic.HashSet<TextureProgressBar> _barrasTrabadasEnCero = new();

	/// <summary>Lleva la barra de vida a su valor nuevo ANIMADA (sube o baja a la vista), nunca de
	/// golpe. Así, al llegar a 0 la barra se ve vaciarse en vez de saltar a vacía (antes se ponía en 0
	/// de una y la animación de vaciado de la muerte ya no tenía nada que mostrar). Solo arranca una
	/// animación nueva si el valor pedido cambió: la interfaz se refresca a cada rato.</summary>
	private void MoverBarraVida(TextureProgressBar barra, double objetivo)
	{
		if (barra == null || !IsInstanceValid(barra)) return;
		if (_barrasTrabadasEnCero.Contains(barra)) objetivo = 0.0; // el huevo que perdió no "revive"
		if (_animBarras.TryGetValue(barra, out var previo) && Mathf.IsEqualApprox(previo.objetivo, objetivo)) return;
		if (previo.tween != null && previo.tween.IsValid()) previo.tween.Kill();
		if (!IsInsideTree() || Mathf.IsEqualApprox(barra.Value, objetivo))
		{
			barra.Value = objetivo;
			_animBarras[barra] = (objetivo, null);
			return;
		}
		float seg = objetivo <= 0 ? SEG_VACIAR_BARRA : SEG_MOVER_BARRA;
		Tween tw = CreateTween();
		tw.TweenProperty(barra, "value", objetivo, seg).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		_animBarras[barra] = (objetivo, tw);
	}

	/// <summary>Baja a cero la barra de vida del bando indicado con una animación corta, en vez del
	/// salto seco de siempre. Se usa en TODOS los finales (rendirse, tiempo agotado, muerte real) y
	/// para los dos bandos, así que el bot se ve igual que el jugador.</summary>
	private void VaciarBarraVida(bool esDelJugador)
	{
		TextureProgressBar barra = esDelJugador ? _barraHPJugador : _barraHPRival;
		if (barra != null) _barrasTrabadasEnCero.Add(barra);
		MoverBarraVida(barra, 0.0); // si ya venía bajando a 0, la sigue (no la corta ni la salta)
	}

	private async Task EsperarSegurosMuerte(float segundos)
	{
		if (!IsInsideTree()) return;
		await ToSignal(GetTree().CreateTimer(segundos), SceneTreeTimer.SignalName.Timeout);
	}

	private void ReproducirGritoHuevo()
	{
		_sfxGrito ??= CargarOGenerarMuerte(RUTA_SFX_GRITO, GenerarGritoHuevo);
		ReproducirSfxMuerte(_sfxGrito, -4f);
	}

	private void ReproducirCrackHuevo()
	{
		_sfxCrack ??= CargarOGenerarMuerte(RUTA_SFX_CRACK, GenerarCrackHuevo);
		ReproducirSfxMuerte(_sfxCrack, -2f);
	}

	private void ReproducirStopHuevo()
	{
		_sfxStop ??= CargarOGenerarMuerte(RUTA_SFX_STOP, GenerarFrenadaStop);
		ReproducirSfxMuerte(_sfxStop, -3f);
	}

	private static AudioStream CargarOGenerarMuerte(string rutaSinExtension, Func<AudioStreamWav> generar)
	{
		foreach (string ext in new[] { ".wav", ".ogg", ".mp3" })
			if (ResourceLoader.Exists(rutaSinExtension + ext))
				return GD.Load<AudioStream>(rutaSinExtension + ext);
		return generar();
	}

	private void ReproducirSfxMuerte(AudioStream stream, float volumenDb)
	{
		if (stream == null) return;
		var player = new AudioStreamPlayer { Stream = stream, VolumeDb = volumenDb };
		AddChild(player);
		player.Finished += () => { if (IsInstanceValid(player)) player.QueueFree(); };
		player.Play();
	}

	// Grito caricaturesco: un "wiii-uuu" que sube rápido y se desploma, con vibrato, tipo dibujito.
	// Generado por código (como el resto de los SFX del juego) para no sumar un archivo al APK; si
	// algún día se pone un .wav en efectos/sonidos/huevo_grito, ese gana automáticamente.
	private static AudioStreamWav GenerarGritoHuevo()
	{
		const int muestreo = 22050;
		const float dur = 0.75f;
		int total = (int)(muestreo * dur);
		var datos = new byte[total * 2];

		for (int i = 0; i < total; i++)
		{
			float t = (float)i / muestreo;
			float p = t / dur;                                   // 0 → 1
			// Sube fuerte hasta el 35% y después se cae en picada (el clásico gag de caída).
			float freq = p < 0.35f
				? Mathf.Lerp(420f, 1150f, p / 0.35f)
				: Mathf.Lerp(1150f, 240f, (p - 0.35f) / 0.65f);
			freq += Mathf.Sin(t * 38f) * 55f;                    // vibrato: le da el tono "cartoon"
			float env = Mathf.Sin(Mathf.Pi * Mathf.Clamp(p, 0f, 1f)); // entra y sale sin chasquidos
			float onda = Mathf.Sin(2f * Mathf.Pi * freq * t);
			onda += 0.32f * Mathf.Sin(4f * Mathf.Pi * freq * t);  // armónico: más nasal, menos "pito"
			short s = (short)(Mathf.Clamp(onda * env * 0.42f, -1f, 1f) * short.MaxValue);
			datos[i * 2]     = (byte)(s & 0xFF);
			datos[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
		}
		return ArmarWavMuerte(datos, muestreo);
	}

	// FRENADA / "disco rayado": ese efecto de corte que usan los youtubers cuando frenan todo en seco.
	// Se arma con un tono que cae de golpe en picada (como el vinilo perdiendo revoluciones) más un
	// raspado de ruido encima que se apaga junto con él.
	private static AudioStreamWav GenerarFrenadaStop()
	{
		const int muestreo = 22050;
		const float dur = 1.05f;
		int total = (int)(muestreo * dur);
		var datos = new byte[total * 2];
		var rnd = new Random(20260926);

		float fase = 0f;
		for (int i = 0; i < total; i++)
		{
			float t = (float)i / muestreo;
			float p = t / dur;

			// La frecuencia se desploma de 620 Hz a ~40: el "wooomp" del disco frenando.
			float freq = Mathf.Lerp(620f, 40f, Mathf.Pow(p, 0.55f));
			fase += 2f * Mathf.Pi * freq / muestreo;

			// Raspado: ruido modulado al mismo ritmo, que es lo que le da el "rrrr" del surco.
			float raspado = (float)(rnd.NextDouble() * 2.0 - 1.0) * (0.35f * (1f - p));
			float wobble  = 0.5f + 0.5f * Mathf.Sin(fase * 0.5f);

			float env  = Mathf.Pow(1f - p, 1.5f);           // se apaga hacia el final
			float onda = Mathf.Sin(fase) * 0.75f + raspado * wobble;
			short s = (short)(Mathf.Clamp(onda * env * 0.7f, -1f, 1f) * short.MaxValue);
			datos[i * 2]     = (byte)(s & 0xFF);
			datos[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
		}
		return ArmarWavMuerte(datos, muestreo);
	}

	// CRACK: golpe seco de cáscara partiéndose — ruido blanco con caída muy rápida y un "crujido"
	// grave encima, para que suene a huevo reventado y no a explosión.
	private static AudioStreamWav GenerarCrackHuevo()
	{
		const int muestreo = 22050;
		const float dur = 0.38f;
		int total = (int)(muestreo * dur);
		var datos = new byte[total * 2];
		var rnd = new Random(20260925);

		for (int i = 0; i < total; i++)
		{
			float t = (float)i / muestreo;
			float p = t / dur;
			float env = Mathf.Pow(1f - p, 5f);                   // ataque instantáneo, cola cortísima
			float ruido = (float)(rnd.NextDouble() * 2.0 - 1.0); // la cáscara astillándose
			float cuerpo = Mathf.Sin(2f * Mathf.Pi * 165f * t);  // el "tock" grave del golpe
			float onda = ruido * 0.78f + cuerpo * 0.35f;
			short s = (short)(Mathf.Clamp(onda * env * 0.85f, -1f, 1f) * short.MaxValue);
			datos[i * 2]     = (byte)(s & 0xFF);
			datos[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
		}
		return ArmarWavMuerte(datos, muestreo);
	}

	private static AudioStreamWav ArmarWavMuerte(byte[] datos, int muestreo) => new()
	{
		Format  = AudioStreamWav.FormatEnum.Format16Bits,
		MixRate = muestreo,
		Stereo  = false,
		Data    = datos,
	};
}
