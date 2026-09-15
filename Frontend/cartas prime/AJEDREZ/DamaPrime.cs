using Godot;

/// <summary>
/// DamaPrime — Pieza Reina de Ajedrez.
/// Ataque Normal: Se queda en su lugar haciendo la animación de ataque.
/// Habilidad: Luz de aviso, selección por clic (o IA Táctica) y vuelo por los aires hacia el
/// objetivo, al que ataca donde sea que esté (cualquier carril) por Ataque+150 fijo. Ya no
/// da ningún bono a sus aliados.
/// </summary>
public partial class DamaPrime : TropaBase
{
	public override string Tipo => Tipos.SOMBRA;
	protected override int TurnoDesbloqueoHabilidad => 4;

	// ── ESTADOS Y SELECCIÓN POR CLIC ──────────────────────────────────────────
	private bool   _esperandoSeleccion = false;
	private bool   _esAtaqueHabilidad  = false;
	private Tween  _tweenAviso;

	// ── CONTROL DE MOVIMIENTO AJEDREZ (FLOTAR SOLO EN HABILIDAD) ──────────────
	private Vector2 _posicionOriginal;
	private Node2D  _objetivoAtaque;
	private bool    _volando         = false;
	private int     _zIndexOriginal;
	private const int FRAME_DESPEGUE = 2; // Frame donde despega (solo en habilidad)
	private const int FRAME_IMPACTO  = 5; // Frame del golpe/daño

	// ══════════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		if (vidaMaxima == 0) 
		{ 
			vidaActual = vidaMaxima = 380;
			escudoActual = escudoMaximo = 300;
			puntosAtaque = 350;
		}
		base._Ready();

		_anim.FrameChanged      += OnFrameChanged;
		_anim.AnimationFinished += OnAnimationFinished;
	}

	public override bool AutogestionaDañoAtaque() => true;

	// ── DETECCIÓN DE CLIC EN PANTALLA (SOLO HABILIDAD) ────────────────────────
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_esperandoSeleccion || _estaMuerto) return;

		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			Vector2 posClic = GetGlobalMousePosition();
			Node2D enemigoClickeado = DetectarEnemigoEnPosicion(posClic);

			if (enemigoClickeado != null)
			{
				_esperandoSeleccion = false;
				_objetivoAtaque     = enemigoClickeado;
				GetViewport().SetInputAsHandled();

				DetenerEfectoAviso();
				EjecutarHabilidadConfirmada();
			}
		}
	}

	// ── ACCIONES PRINCIPALES ──────────────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (_estaMuerto) return;

		switch (accion)
		{
			case "atacar":
				_yaActuo           = true;
				_esAtaqueHabilidad = false; // 🛑 Marca que es un ataque normal
				_objetivoAtaque    = BuscarObjetivoEnCarril();
				IniciarAtaque();
				break;

			case "usar_habilidad":
				UsarHabilidadPropia();
				break;

			case "preparar_defensa":
				_yaActuo = true;
				_anim.Play("pre defensa");
				break;

			case "defender":
				ReproducirDefensa();
				break;

			case "recibir_daño":
				if (_anim.Animation != "pre defensa" && _anim.Animation != "defensa")
					_anim.Play("daño");
				break;
		}
	}

	// ── HABILIDAD PROPIA ──────────────────────────────────────────────────────
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada || _estaMuerto) return;

		// 🤖 Evaluar si la tropa le pertenece a la IA / Rival
		bool esRivalTropa = IsInGroup("tropas_rival");
		try
		{
			Variant valRival = Get("esRival");
			if (valRival.VariantType != Variant.Type.Nil && valRival.AsBool())
			{
				esRivalTropa = true;
			}
		}
		catch { }

		if (esRivalTropa)
		{
			// 🧠 IA TÁCTICA: Selecciona al enemigo con MÁS VIDA del tablero para usar el 1.5x de daño
			_objetivoAtaque = BuscarEnemigoMasFuerteIA();
			EjecutarHabilidadConfirmada();
		}
		else
		{
			// 👤 Si es del jugador humano, activa la luz de aviso y espera el clic
			_esperandoSeleccion = true;

			// Activa la luz amarilla intermitente
			_tweenAviso = CreateTween().SetLoops();
			_tweenAviso.TweenProperty(this, "modulate", new Color(1.8f, 1.5f, 0.6f), 0.3f);
			_tweenAviso.TweenProperty(this, "modulate", Colors.White, 0.3f);
		}
	}

	private void DetenerEfectoAviso()
	{
		if (_tweenAviso != null && _tweenAviso.IsValid())
		{
			_tweenAviso.Kill();
		}
		Modulate = Colors.White;
	}

	private void EjecutarHabilidadConfirmada()
	{
		_yaActuo           = true;
		habilidadUsada     = true;
		_esAtaqueHabilidad = true; // ⚡ Marca que ES ataque de habilidad con vuelo

		IniciarAtaque();
	}

	private void IniciarAtaque()
	{
		_posicionOriginal = GlobalPosition;
		_volando          = false;
		_zIndexOriginal   = ZIndex;
		_anim.Play("ataque");
	}

	// ── CONTROL DEL FRAME: VUELO Y DAÑO ───────────────────────────────────────
	private void OnFrameChanged()
	{
		if (_anim.Animation != "ataque") return;

		int frameActual = _anim.Frame;

		// ✈️ SOLO VUELA SI ES EL ATAQUE DE HABILIDAD
		if (frameActual == FRAME_DESPEGUE && !_volando && _esAtaqueHabilidad)
		{
			_volando = true;

			if (_objetivoAtaque == null || !IsInstanceValid(_objetivoAtaque)) return;

			ZIndex = 100; // Se muestra por encima de la tropa objetivo durante el vuelo

			Vector2 posDestino = ObtenerDestinoAtaque(_objetivoAtaque);

			_anim.Pause();

			Tween tweenIda = CreateTween().SetParallel(true);

			tweenIda.TweenProperty(this, "global_position", posDestino, 0.38f)
					.SetTrans(Tween.TransitionType.Sine)
					.SetEase(Tween.EaseType.Out);

			tweenIda.TweenProperty(this, "global_position:y", posDestino.Y - 30.0f, 0.19f)
					.SetTrans(Tween.TransitionType.Sine)
					.SetEase(Tween.EaseType.Out);

			tweenIda.Chain().TweenProperty(this, "global_position:y", posDestino.Y, 0.19f)
					.SetTrans(Tween.TransitionType.Sine)
					.SetEase(Tween.EaseType.In);

			tweenIda.Finished += () =>
			{
				if (IsInstanceValid(this))
				{
					_anim.Play();
				}
			};
		}

		// 💥 APLICA DAÑO EN EL FRAME CORRESPONDIENTE (Tanto normal como habilidad)
		if (frameActual == FRAME_IMPACTO)
		{
			// Habilidad: Ataque + 150 fijo (350+150=500). Ya no multiplica ni da buffs a aliados.
			int dañoAplica = _esAtaqueHabilidad ? (puntosAtaque + 150) : puntosAtaque;
			AplicarDañoDirecto(_objetivoAtaque, dañoAplica);
		}
	}

	// ── FIN DE ANIMACIÓN ──────────────────────────────────────────────────────
	private void OnAnimationFinished()
	{
		if (_anim.Animation == "ataque")
		{
			// Si voló por la habilidad, hace el viaje de regreso
			if (_esAtaqueHabilidad && _volando)
			{
				Tween tweenRegreso = CreateTween();
				tweenRegreso.TweenProperty(this, "global_position", ObtenerPosicionCarrilPropio(_posicionOriginal), 0.38f)
							.SetTrans(Tween.TransitionType.Sine)
							.SetEase(Tween.EaseType.InOut);

				tweenRegreso.Finished += FinalizarTurnoAtaque;
			}
			else
			{
				// Si fue un ataque normal, termina de inmediato en su sitio
				FinalizarTurnoAtaque();
			}
		}
		else if (_anim.Animation == "daño")
		{
			if (!_estaMuerto) ReproducirIdle();
		}
	}

	private void FinalizarTurnoAtaque()
	{
		_volando           = false;
		_esAtaqueHabilidad = false;
		ZIndex              = _zIndexOriginal;
		DetenerEfectoAviso();
		ReproducirIdle();
	}

	// ── HELPERS & SELECCIÓN TÁCTICA ──────────────────────────────────────────
	private Node2D BuscarEnemigoMasFuerteIA()
	{
		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		Node2D objetivoMasFuerte = null;
		int maxVida = -1;

		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (n is Node2D e && IsInstanceValid(e))
			{
				int vida = 0;
				try { vida = (int)e.Get("vidaActual"); } catch { }
				if (vida > maxVida)
				{
					maxVida = vida;
					objetivoMasFuerte = e;
				}
			}
		}

		// Fallback: si no encontró ninguno por vida, buscar objetivo por carril normal
		return objetivoMasFuerte ?? BuscarObjetivoEnCarril();
	}

	private Node2D DetectarEnemigoEnPosicion(Vector2 posClic)
	{
		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		Node2D objetivoCercano = null;
		float distanciaMinima   = 120.0f;

		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e)) continue;

			float dist = e.GlobalPosition.DistanceTo(posClic);
			if (dist < distanciaMinima)
			{
				distanciaMinima = dist;
				objetivoCercano = e;
			}
		}

		return objetivoCercano;
	}



	private void AplicarDañoDirecto(Node2D objetivo, int cantidad)
	{
		if (objetivo != null && IsInstanceValid(objetivo))
		{
			if (objetivo.HasMethod("RecibirDañoDe"))
				objetivo.Call("RecibirDañoDe", cantidad, this);
			else
				objetivo.Call("RecibirDaño", cantidad);
			var campo = GetTree().Root.FindChild("Campo1", true, false);
			if (campo != null) campo.Call("RegistrarDañoTropa", this, cantidad);
		}
	}
}
