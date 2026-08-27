using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// ArfilPrime — Pieza Arfil de Ajedrez.
/// Stats Intermedios entre Caballo y Torre.
/// Habilidad Diagonal Extrema:
/// - Desde Carril 1 -> Ataca Carril Rival 3 (+50 de daño extra).
/// - Desde Carril 3 -> Ataca Carril Rival 1 (+50 de daño extra).
/// - Desde Carril 2 (Centro) -> No puede usar Habilidad.
/// Frame de Despegue: Frame 3.
/// </summary>
public partial class ArfilPrime : TropaBase
{
	public override string Tipo => Tipos.SOMBRA;
	protected override int TurnoDesbloqueoHabilidad => 2;

	// ── ESTADOS Y CONTROL DE MOVIMIENTO ──────────────────────────────────────
	private bool    _esAtaqueHabilidad = false;
	private Vector2 _posicionOriginal;
	private Node2D  _objetivoAtaque;
	private bool    _volando         = false;
	private int     _zIndexOriginal;

	private const int FRAME_DESPEGUE = 3; // 🎯 Despegue/Salto en Frame 3
	private const int FRAME_IMPACTO  = 5; // Frame del golpe/daño

	// ══════════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		if (vidaMaxima == 0) 
		{ 
			vidaActual = vidaMaxima = 280;   // Más vida que el Caballo (230)
			escudoActual = escudoMaximo = 270;
			puntosAtaque = 240;              // Ataque intermedio
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

	// ── MÉTODOS DE CONSULTA PARA LA UI (CAMPO1 Y CAMPOPRUEBA) ────────────────
	public new bool TieneHabilidadEspecial() => true;

	// Campo1 llama HabilidadBloqueada() para deshabilitar el botón en carril 2
	public override bool HabilidadBloqueada() => !PuedeUsarHabilidad();

	public bool PuedeUsarHabilidad()
	{
		if (habilidadUsada || _estaMuerto) return false;
		if (!HasMeta("carril")) return false;

		string miCarril = ((string)GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
		
		// ⛔ Si está en el Centro (Carril 2), el botón debe estar bloqueado
		return miCarril == "1" || miCarril == "3";
	}

	// ── ACCIONES PRINCIPALES ──────────────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (_estaMuerto) return;

		switch (accion)
		{
			case "atacar":
				_yaActuo           = true;
				_esAtaqueHabilidad = false; // 🛑 Ataque normal: Se queda en su sitio
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

	// ── HABILIDAD PROPIA EN DIAGONAL (SOLO EXTREMOS) ─────────────────────────
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada || _estaMuerto) return;
		if (!HasMeta("carril")) { GD.PrintErr("ArfilPrime: Falta la meta 'carril'"); return; }

		string miCarril = ((string)GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();

		// ⛔ REGLA: Si está en el Centro (Carril 2), NO puede usar la habilidad
		if (miCarril == "2")
		{
			GD.Print("El Arfil está en el centro (Carril 2), no puede usar habilidad en diagonal.");
			return;
		}

		// 🎯 DIAGONAL: Carril 1 -> Rival 3 | Carril 3 -> Rival 1
		string carrilObjetivo = (miCarril == "1") ? "3" : "1";
		_objetivoAtaque = BuscarEnemigoEnCarrilEspecifico(carrilObjetivo);

		if (_objetivoAtaque != null && IsInstanceValid(_objetivoAtaque))
		{
			_yaActuo           = true;
			habilidadUsada     = true;
			_esAtaqueHabilidad = true; // ⚡ Activa el salto en diagonal y el +50 de daño
			IniciarAtaque();
		}
		else
		{
			GD.Print("No hay objetivo en el carril rival diagonal (" + carrilObjetivo + ")");
		}
	}

	private void IniciarAtaque()
	{
		_posicionOriginal = GlobalPosition;
		_volando          = false;
		_zIndexOriginal   = ZIndex;
		_anim.Play("ataque");
	}

	// ── CONTROL DEL FRAME: SALTO (FRAME 3) Y DAÑO ─────────────────────────────
	private void OnFrameChanged()
	{
		if (_anim.Animation != "ataque") return;

		int frameActual = _anim.Frame;

		// ✈️ PASO 1: SALTO EN DIAGONAL SOLO SI ES HABILIDAD (EN FRAME 3)
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

			tweenIda.TweenProperty(this, "global_position:y", posDestino.Y - 40.0f, 0.19f)
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

		// 💥 PASO 2: IMPACTO (DAÑO BASE O BASE + 50 EXTRA)
		if (frameActual == FRAME_IMPACTO)
		{
			// Si es Habilidad -> 240 + 50 = 290 de daño
			int dañoAplica = _esAtaqueHabilidad ? (puntosAtaque + 50) : puntosAtaque;
			AplicarDañoDirecto(_objetivoAtaque, dañoAplica);
		}
	}

	// ── FIN DE ANIMACIÓN Y REGRESO ───────────────────────────────────────────
	private void OnAnimationFinished()
	{
		if (_anim.Animation == "ataque")
		{
			// Si saltó por la habilidad, hace el viaje de regreso por Tween
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
				// Si fue ataque normal, finaliza quieto en su lugar
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
		ReproducirIdle();
	}

	// ── BÚSQUEDA DE OBJETIVOS ────────────────────────────────────────────────
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
