using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Campo1 : Node2D
{
	// ── ENROQUE TÁCTICO (habilidad de la Torre) ──────────────────────────────
	// Intercambia la Torre con un aliado de un carril adyacente (o se mueve sola
	// si ese carril está vacío). Sin matriz/diccionario central: sigue el mismo
	// patrón descentralizado que ya usa el resto del juego (meta "carril" en la
	// tropa + nodo hijo "Ocupado" en la zona), igual que ReemplazarTropaTransformada.

	// Parpadeo amarillo semitransparente (alfa 0.4 → 1.0 en bucle), sin botones de UI.
	private static readonly Color COLOR_ENROQUE = new Color(1f, 0.9f, 0.2f, 1.0f);
	private const float ALFA_ENROQUE_MIN = 0.4f;

	private TropaBase _torreEnroque;
	private readonly List<string> _carrilesEnroqueValidos = new();
	private readonly Dictionary<CanvasItem, Tween> _tweensResaltadoEnroque = new();

	private static string NormalizarCarril(string carril) =>
		carril.ToLower().Replace("modrival", "").Replace("mod", "").Trim();

	// Mayor coordenada Y = carril visualmente más frontal → ZIndex mayor.
	// Mismo esquema que ya usan TropaInvocada/InvocacionRival (Mod3/ModRival3 = 100).
	private static int TierZIndexCarril(string carril) =>
		NormalizarCarril(carril) switch { "3" => 100, "2" => 50, _ => 10 };

	// ── SELECCIÓN POR CLIC (JUGADOR) ─────────────────────────────────────────

	public void IniciarSeleccionEnroque(TropaBase torre)
	{
		if (!IsInstanceValid(torre) || !torre.HasMeta("carril")) return;
		CancelarSeleccionEnroque(); // por si quedó una selección anterior a medias

		var candidatos = new List<string>(CarrilesAdyacentes(torre));
		if (candidatos.Count == 0) return;

		// Torre en carril 1 o 3: solo hay un carril adyacente (el 2, centro) — selección
		// automática, sin esperar clic. Torre en carril 2: hay dos candidatos, se elige.
		if (candidatos.Count == 1)
		{
			_ = EjecutarEnroqueDesdeCarril(torre, candidatos[0]);
			return;
		}

		foreach (string nombreZona in candidatos)
		{
			Node2D zona = GetTree().Root.FindChild(nombreZona, true, false) as Node2D;
			if (zona == null) continue;
			_carrilesEnroqueValidos.Add(nombreZona);
			ResaltarCarrilEnroque(zona, true);
		}

		if (_carrilesEnroqueValidos.Count == 0) return;

		_torreEnroque = torre;
		if (_lblInstruccion != null)
		{
			_lblInstruccion.Text    = "Elige el carril\nde destino";
			_lblInstruccion.Visible = true;
		}
	}

	/// <summary>Adyacencia real de tablero: el carril 2 (centro) es adyacente a 1 y 3;
	/// los carriles 1 y 3 son adyacentes ÚNICAMENTE al 2 (no entre sí).</summary>
	private IEnumerable<string> CarrilesAdyacentes(TropaBase torre)
	{
		bool esRival = torre.IsInGroup("tropas_rival");
		string Nombre(string n) => esRival ? $"ModRival{n}" : $"Mod{n}";
		string miCarril = NormalizarCarril((string)torre.GetMeta("carril"));

		if (miCarril == "2") { yield return Nombre("1"); yield return Nombre("3"); }
		else if (miCarril == "1" || miCarril == "3") { yield return Nombre("2"); }
	}

	private async Task EjecutarEnroqueDesdeCarril(TropaBase torre, string carrilDestino)
	{
		Node2D zonaDestino = GetTree().Root.FindChild(carrilDestino, true, false) as Node2D;
		if (zonaDestino == null) return;
		await EjecutarEnroque(torre, AliadoEnZona(zonaDestino), carrilDestino);
	}

	/// <summary>Parpadeo amarillo semitransparente (alfa 0.4↔1.0 en bucle) sobre la tropa
	/// aliada del carril, o el indicador visual de la zona si está vacía. Sin botones de UI.</summary>
	private void ResaltarCarrilEnroque(Node2D zona, bool activar)
	{
		CanvasItem objetivo = AliadoEnZona(zona);
		if (objetivo == null || !IsInstanceValid(objetivo))
			objetivo = zona.GetNodeOrNull<ColorRect>("Indicador");
		if (objetivo == null) return;

		if (_tweensResaltadoEnroque.TryGetValue(objetivo, out Tween previo) && previo.IsValid())
			previo.Kill();
		_tweensResaltadoEnroque.Remove(objetivo);

		if (!activar) { objetivo.Modulate = Colors.White; return; }

		Color colorAlfaMin = COLOR_ENROQUE; colorAlfaMin.A = ALFA_ENROQUE_MIN;
		Tween tw = objetivo.CreateTween().SetLoops();
		tw.TweenProperty(objetivo, "modulate", colorAlfaMin, 0.4f);
		tw.TweenProperty(objetivo, "modulate", COLOR_ENROQUE, 0.4f);
		_tweensResaltadoEnroque[objetivo] = tw;
	}

	private static TropaBase AliadoEnZona(Node2D zona)
	{
		Node ocupado = zona.GetNodeOrNull("Ocupado");
		if (ocupado == null || !ocupado.HasMeta("tropa_instanciada")) return null;
		var tropa = ocupado.GetMeta("tropa_instanciada").As<TropaBase>();
		// Una tropa muriendo sigue ocupando su carril hasta que termina su derrota: no cuenta como aliado.
		return IsInstanceValid(tropa) && !tropa.IsQueuedForDeletion() && !tropa.EstaMuerta ? tropa : null;
	}

	/// <summary>Llamado desde Campo1._Input (Campo1.Hechizos.cs) mientras hay una selección
	/// de enroque activa. Devuelve true si el clic cayó sobre un carril válido.</summary>
	private bool IntentarClicEnroque(Vector2 posicionMundo)
	{
		if (_torreEnroque == null) return false;
		foreach (string carril in _carrilesEnroqueValidos)
		{
			Node2D zona = GetTree().Root.FindChild(carril, true, false) as Node2D;
			if (zona == null) continue;
			if (zona.GlobalPosition.DistanceTo(posicionMundo) < 90f)
			{
				ConfirmarEnroque(carril);
				return true;
			}
		}
		return false;
	}

	private void ConfirmarEnroque(string carrilDestino)
	{
		TropaBase torre = _torreEnroque;
		LimpiarResaltadoEnroque();
		if (_lblInstruccion != null) _lblInstruccion.Visible = false;
		_torreEnroque = null;

		if (torre == null || !IsInstanceValid(torre)) return;
		Node2D zonaDestino = GetTree().Root.FindChild(carrilDestino, true, false) as Node2D;
		if (zonaDestino == null) return;

		_ = EjecutarEnroque(torre, AliadoEnZona(zonaDestino), carrilDestino);
	}

	private void LimpiarResaltadoEnroque()
	{
		foreach (string carril in _carrilesEnroqueValidos)
		{
			Node2D zona = GetTree().Root.FindChild(carril, true, false) as Node2D;
			if (zona != null) ResaltarCarrilEnroque(zona, false);
		}
		_carrilesEnroqueValidos.Clear();
	}

	public void CancelarSeleccionEnroque()
	{
		LimpiarResaltadoEnroque();
		_torreEnroque = null;
		if (_lblInstruccion != null) _lblInstruccion.Visible = false;
	}

	// ── EJECUCIÓN DEL INTERCAMBIO (compartida jugador / IA) ──────────────────

	private async Task EjecutarEnroque(TropaBase torre, TropaBase aliado, string carrilDestino)
	{
		if (!IsInstanceValid(torre) || !torre.HasMeta("carril")) return;

		string carrilOrigen = (string)torre.GetMeta("carril");
		Node2D zonaOrigen  = GetTree().Root.FindChild(carrilOrigen, true, false) as Node2D;
		Node2D zonaDestino = GetTree().Root.FindChild(carrilDestino, true, false) as Node2D;
		if (zonaDestino == null) return;

		// Centro de colisión, no el origen crudo: Torre y aliado pueden tener CollisionShape2D
		// en posiciones locales distintas dentro de su propia escena. El aliado debe terminar
		// con SU centro de colisión donde estaba el centro de colisión de la Torre, no en su
		// GlobalPosition crudo.
		Vector2 centroOrigenTorre = torre.PosicionCentroColision;
		Vector2 posDestino       = torre.DestinoGlobalParaCentro(zonaDestino.GlobalPosition);
		Vector2 posDestinoAliado = aliado != null ? aliado.DestinoGlobalParaCentro(centroOrigenTorre) : Vector2.Zero;

		// 1) State Lock — anti-spam / anti-doble clic en ambas tropas involucradas, más el
		//    bloqueo global de tablero para que la IA no dispare otra acción mientras dura.
		torre.estaProcesandoHabilidad = true;
		if (aliado != null) aliado.estaProcesandoHabilidad = true;
		IniciarBloqueoTablero();

		// 2) Z-Index por profundidad real: cada tropa toma el tier de SU carril destino
		//    (mayor Y = más frontal = ZIndex mayor). Ya queda correcto al terminar el
		//    arrastre, sin necesidad de restaurar ningún valor original.
		torre.ZIndex = TierZIndexCarril(carrilDestino);
		if (aliado != null) aliado.ZIndex = TierZIndexCarril(carrilOrigen);

		// 3) Arrastre paralelo sin teletransporte — mismo tween de regreso real que usan
		//    Arfil/Caballo/Dama (Sine / InOut / 0.38s). La reasignación de datos NO ocurre
		//    hasta que este Tween termina al 100%.
		Tween tw = CreateTween().SetParallel(true);
		tw.TweenProperty(torre, "global_position", posDestino, 0.38f)
		  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		if (aliado != null)
			tw.TweenProperty(aliado, "global_position", posDestinoAliado, 0.38f)
			  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		await ToSignal(tw, Tween.SignalName.Finished);

		if (!IsInstanceValid(torre))
		{
			// La Torre murió en pleno Enroque: el aliado tiene que quedar destrabado. Antes se quedaba
			// marcado como "procesando habilidad" para siempre y ya no respondía a los clics.
			if (aliado != null && IsInstanceValid(aliado)) aliado.estaProcesandoHabilidad = false;
			FinalizarBloqueoTablero();
			return;
		}

		// 4) Reasignación lógica del "tablero" — solo tras terminar el Tween al 100%.
		//    Se intercambia el carril y se reapuntan (nunca se destruyen) los
		//    marcadores "Ocupado", igual que hace ReemplazarTropaTransformada.
		torre.SetMeta("carril", carrilDestino);
		Node ocupadoOrigen  = zonaOrigen?.GetNodeOrNull("Ocupado");
		Node ocupadoDestino = zonaDestino.GetNodeOrNull("Ocupado");

		if (aliado != null && IsInstanceValid(aliado))
		{
			aliado.SetMeta("carril", carrilOrigen);
			ocupadoOrigen?.SetMeta("tropa_instanciada", aliado);
			ocupadoDestino?.SetMeta("tropa_instanciada", torre);
			aliado.estaProcesandoHabilidad = false;
		}
		else
		{
			// Carril destino vacío: el marcador "Ocupado" viaja con la Torre.
			ocupadoOrigen?.Free();
			if (ocupadoDestino == null)
			{
				ocupadoDestino = new Node(); ocupadoDestino.Name = "Ocupado";
				zonaDestino.AddChild(ocupadoDestino);
			}
			ocupadoDestino.SetMeta("tropa_instanciada", torre);
		}

		// 5) Nada de lo anterior tocó vidaActual/escudoActual ni metas de buffs,
		//    veneno o bloqueo — quedan exactamente igual que antes del Enroque.

		torre.estaProcesandoHabilidad = false;
		torre.habilidadUsada = true;
		FinalizarBloqueoTablero();
	}

	// ── DECISIÓN Y EJECUCIÓN AUTOMÁTICA (IA) ─────────────────────────────────

	/// <summary>Evalúa si conviene un Enroque para esta Torre de la IA: prioriza salvar a un
	/// aliado adyacente amenazado (poca vida/escudo restante, o un enemigo enfrente capaz de
	/// matarlo en su próximo golpe), poniendo a la Torre — más resistente — en su lugar.
	/// Si decide actuar, aplica el destello azul (0.2s) y ejecuta el mismo arrastre asíncrono
	/// que el jugador, esperado con await antes de que la IA siga con su turno.</summary>
	public async Task<bool> IntentarEnroqueIA(TropaBase torre)
	{
		if (!IsInstanceValid(torre) || !torre.HasMeta("carril") || torre.HabilidadBloqueada()) return false;

		bool esRival = torre.IsInGroup("tropas_rival");
		string grupoEnemigo = esRival ? "tropas_jugador" : "tropas_rival";

		string  mejorCarril = null;
		TropaBase mejorAliado = null;
		float   peorRatio   = 0.4f; // umbral: solo actúa si hay una tropa realmente amenazada

		foreach (string nombreZona in CarrilesAdyacentes(torre))
		{
			Node2D zona = GetTree().Root.FindChild(nombreZona, true, false) as Node2D;
			TropaBase aliado = zona != null ? AliadoEnZona(zona) : null;
			if (aliado == null || !IsInstanceValid(aliado)) continue;

			int vida    = Gi(aliado, "vidaActual");
			int vidaMax = Mathf.Max(1, Gi(aliado, "vidaMaxima"));
			int esc     = Gi(aliado, "escudoActual");
			float ratio = (float)(vida + esc) / vidaMax;

			Node2D enemigo  = BuscarObjetivoEnCarril(aliado, grupoEnemigo);
			bool amenazado  = enemigo != null && Gi(enemigo, "puntosAtaque") >= vida + esc;

			if ((amenazado || ratio < 0.4f) && ratio < peorRatio + 0.0001f)
			{
				peorRatio   = amenazado ? -1f : ratio; // una amenaza de muerte directa siempre gana
				mejorCarril = nombreZona;
				mejorAliado = aliado;
			}
		}

		if (mejorCarril == null) return false;

		Node2D zonaElegida = GetTree().Root.FindChild(mejorCarril, true, false) as Node2D;
		if (zonaElegida != null) ResaltarCarrilEnroque(zonaElegida, true);
		await ToSignal(GetTree().CreateTimer(0.2), "timeout");
		if (zonaElegida != null) ResaltarCarrilEnroque(zonaElegida, false);

		if (!IsInstanceValid(torre)) return false;
		await EjecutarEnroque(torre, mejorAliado, mejorCarril);
		return true;
	}
}
