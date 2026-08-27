using Godot;
using System.Collections.Generic;

/// <summary>
/// CaballoPrime — Pieza Caballo de Ajedrez.
/// Ataca en "L": 
/// - Desde Carril 1 -> Carril Rival 2.
/// - Desde Carril 3 -> Carril Rival 2.
/// - Desde Carril 2 -> Requiere Clic para elegir Carril Rival 1 o 3 (o IA Táctica).
/// Hace Daño Base (200) + 100 Extra = 300 de Daño Total.
/// </summary>
public partial class CaballoPrime : TropaBase
{
	public override string Tipo => Tipos.METAL;
	protected override int TurnoDesbloqueoHabilidad => 2;

	// ── ESTADOS Y SELECCIÓN POR CLIC ──────────────────────────────────────────
	private bool   _esperandoSeleccion = false;
	private bool   _esAtaqueHabilidad  = false;
	private Tween  _tweenAviso;
	private Vector2 _posicionOriginal;
	private Node2D  _objetivoAtaque;
	private bool    _volando         = false;
	private int     _zIndexOriginal;
	private const int FRAME_DESPEGUE = 3; // Elevarse y volar en Frame 1 (Solo en habilidad)
	private const int FRAME_IMPACTO  = 5; // Frame del golpe/daño

	// ══════════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		if (vidaMaxima == 0) 
		{ 
			vidaActual = vidaMaxima = 230; 
			escudoActual = escudoMaximo = 250; 
			puntosAtaque = 200; // Daño Base
		}
		base._Ready();

		_anim.FrameChanged      += OnFrameChanged;
		_anim.AnimationFinished += OnAnimationFinished;
	}

	public override bool AutogestionaDañoAtaque() => true;

	public void RefrescarUI()
	{
		if (_contenedorStats != null) { _contenedorStats.Visible = true; ActualizarBarrasUI(); }
	}

	// ── DETECCIÓN DE CLIC EN PANTALLA (SOLO CUANDO ESTÁ EN CARRIL 2) ───────────
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_esperandoSeleccion || _estaMuerto) return;

		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			Vector2 posClic = GetGlobalMousePosition();
			Node2D enemigoClickeado = DetectarEnemigoElegibleEnL(posClic);

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
				_esAtaqueHabilidad = false; // 🛑 Ataque normal: Se queda en su casilla
				_objetivoAtaque    = BuscarObjetivoEnCarril();
				IniciarAtaqueFlotante();
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

	// ── HABILIDAD PROPIA EN "L" ───────────────────────────────────────────────
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada || _estaMuerto) return;
		if (!HasMeta("carril")) { GD.PrintErr("CaballoPrime: falta meta 'carril'"); return; }

		string miCarril = ((string)GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();

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

		// SI ESTÁ EN EL CENTRO (CARRIL 2)
		if (miCarril == "2")
		{
			if (esRivalTropa)
			{
				// 🧠 IA TÁCTICA: Evalúa carril 1 y 3 para atacar al enemigo de mayor amenaza
				_objetivoAtaque = BuscarMejorObjetivoEnL_IA();
				EjecutarHabilidadConfirmada();
			}
			else
			{
				// 👤 El Jugador Humano selecciona mediante clic entre Carril Rival 1 y 3
				_esperandoSeleccion = true;
				_tweenAviso = CreateTween().SetLoops();
				_tweenAviso.TweenProperty(this, "modulate", new Color(1.8f, 1.5f, 0.6f), 0.3f);
				_tweenAviso.TweenProperty(this, "modulate", Colors.White, 0.3f);
			}
		}
		else
		{
			// SI ESTÁ EN CARRIL 1 O 3: Ataca automáticamente al Carril Rival 2
			_objetivoAtaque = BuscarEnemigoEnCarrilEspecifico("2");
			EjecutarHabilidadConfirmada();
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
		_esAtaqueHabilidad = true; // ⚡ Activa movimiento de salto en L +100 de daño extra
		IniciarAtaqueFlotante();
	}

	private void IniciarAtaqueFlotante()
	{
		_posicionOriginal = GlobalPosition;
		_volando          = false;
		_zIndexOriginal   = ZIndex;
		_anim.Play("ataque");
	}

	// ── CONTROL DEL FRAME: ELEVACIÓN (FRAME 1) Y DAÑO ────────────────────────
	private void OnFrameChanged()
	{
		if (_anim.Animation != "ataque") return;

		int frameActual = _anim.Frame;

		// --- PASO 1: FRAME 1 (SOLO VUELA/DESPLAZA SI ES HABILIDAD) ---
		if (frameActual == FRAME_DESPEGUE && !_volando && _esAtaqueHabilidad)
		{
			_volando = true;

			if (_objetivoAtaque == null || !IsInstanceValid(_objetivoAtaque)) return;

			ZIndex = 100; // Se muestra por encima de la tropa objetivo durante el salto

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
					_anim.Play(); // Reanuda la animación para dar el golpe
				}
			};
		}

		// --- PASO 2: FRAME DE IMPACTO (APLICA DAÑO BASE O BASE + 100 EXTRA) ---
		if (frameActual == FRAME_IMPACTO)
		{
			// Si es Habilidad -> 200 + 100 = 300 de daño
			int dañoAplica = _esAtaqueHabilidad ? (puntosAtaque + 100) : puntosAtaque;
			AplicarDañoDirecto(_objetivoAtaque, dañoAplica);
		}
	}

	// ── CONTROL DE FIN DE ANIMACIÓN: RETROCESO FLOTANDO ──────────────────────
	private void OnAnimationFinished()
	{
		if (_anim.Animation == "ataque")
		{
			// Solo regresa con Tween si se desplazó usando la Habilidad
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
				// Si fue Ataque Normal, se queda quieto en su casilla
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

	// ── BÚSQUEDA Y SELECCIÓN TÁCTICA ──────────────────────────────────────────
	private Node2D BuscarMejorObjetivoEnL_IA()
	{
		Node2D obj1 = BuscarEnemigoEnCarrilEspecifico("1");
		Node2D obj3 = BuscarEnemigoEnCarrilEspecifico("3");

		int vida1 = 0;
		int vida3 = 0;

		if (obj1 != null && IsInstanceValid(obj1)) try { vida1 = (int)obj1.Get("vidaActual"); } catch { }
		if (obj3 != null && IsInstanceValid(obj3)) try { vida3 = (int)obj3.Get("vidaActual"); } catch { }

		// La IA compara la vida de ambos objetivos en "L" y prioriza golpear al de mayor vida
		if (obj1 != null && obj3 != null)
		{
			return (vida1 >= vida3) ? obj1 : obj3;
		}

		return obj1 ?? obj3 ?? BuscarEnemigoEnCarrilEspecifico("1");
	}

	private Node2D DetectarEnemigoElegibleEnL(Vector2 posClic)
	{
		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		Node2D objetivoCercano = null;
		float distanciaMinima   = 120.0f;

		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;

			string c = ((string)e.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			// Solo permite seleccionar Carril 1 o Carril 3 (Movimiento L desde el carril 2)
			if (c != "1" && c != "3") continue;

			float dist = e.GlobalPosition.DistanceTo(posClic);
			if (dist < distanciaMinima)
			{
				distanciaMinima = dist;
				objetivoCercano = e;
			}
		}

		return objetivoCercano;
	}

	private Node2D BuscarEnemigoEnCarrilEspecifico(string carrilTarget)
	{
		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			string c = ((string)e.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			if (c == carrilTarget) return e;
		}
		return null;
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
