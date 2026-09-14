using Godot;

/// <summary>
/// CamperoCartoonPrime — Francotirador que hereda de TropaBase.
/// Habilidad instantánea: Dispara en frame 1 y 7, transiciona vía animación "defensa" para volver a idle.
/// </summary>
public partial class CamperoCartoonPrime : TropaBase
{
	public override string Tipo => Tipos.METAL;
	protected override int TurnoDesbloqueoHabilidad => 2;

	// ── ESTADO HABILIDAD ──────────────────────────────────────────────────────
	private bool   _habilidadActiva = false;
	private bool   _derrotaIniciada = false;

	// ── OBJETIVOS ─────────────────────────────────────────────────────────────
	private Node2D _objetivoAtaque;
	private Node2D _objetivoHabilidad1;
	private Node2D _objetivoHabilidad2;

	// ══════════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		if (vidaMaxima == 0)
		{
			vidaActual   = vidaMaxima   = 200;
			escudoActual = escudoMaximo = 300;
			puntosAtaque = 250;
		}
		base._Ready();

		_anim.FrameChanged      += OnFrameChanged;
		_anim.AnimationFinished += OnAnimationFinished;
	}

	public override bool AutogestionaDañoAtaque() => true;

	// ── ACCIONES ──────────────────────────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (_estaMuerto) return;
		switch (accion)
		{
			case "atacar":
				_yaActuo        = true;
				_objetivoAtaque = BuscarObjetivoEnCarril(ObtenerMiCarril());
				ReproducirAtaque();
				break;

			case "preparar_defensa":
				_yaActuo = true;
				_anim.Play("pre defensa");
				break;

			case "recibir_daño":
				_anim.Play("daño");
				break;

			case "defender":
				ReproducirDefensa(); // Método de TropaBase (reproduce la animación "defensa")
				break;

			case "usar_habilidad":
				UsarHabilidadPropia();
				break;
		}
	}

	// ── HABILIDAD ESPECIAL ────────────────────────────────────────────────────
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada || _habilidadActiva || escudoActual <= 0) return;

		string miCarril     = ObtenerMiCarril();
		_objetivoHabilidad1 = null;
		_objetivoHabilidad2 = null;

		if (miCarril == "1")
		{
			_objetivoHabilidad1 = BuscarObjetivoEnCarril("1");
			_objetivoHabilidad2 = BuscarObjetivoEnCarril("2");
		}
		else if (miCarril == "2")
		{
			_objetivoHabilidad1 = BuscarObjetivoEnCarril("1");
			_objetivoHabilidad2 = BuscarObjetivoEnCarril("3");
		}
		else if (miCarril == "3")
		{
			_objetivoHabilidad1 = BuscarObjetivoEnCarril("3");
			_objetivoHabilidad2 = BuscarObjetivoEnCarril("2");
		}

		if (_objetivoHabilidad1 == null && _objetivoHabilidad2 == null) return;

		_habilidadActiva = true;
		habilidadUsada   = true;
		_yaActuo         = true;

		// Paso 1: Agacharse vía "pre defensa"
		_anim.Play("pre defensa");
	}

	// ── RECIBIR DAÑO ──────────────────────────────────────────────────────────
	public override void RecibirDaño(int cantidad)
	{
		if (_estaMuerto) return;

		// Si le pegan agachado (en habilidad o en pre defensa)
		if (_habilidadActiva || _anim.Animation == "pre defensa")
		{
			EfectoGolpe();

			int dañoAplicar = (int)(cantidad * 0.5f);

			if (escudoActual > 0)
			{
				if (dañoAplicar <= escudoActual)
				{
					escudoActual -= dañoAplicar;
					dañoAplicar = 0;
				}
				else
				{
					dañoAplicar -= escudoActual;
					escudoActual = 0;
				}
			}

			if (dañoAplicar > 0)
			{
				vidaActual = Mathf.Max(0, vidaActual - dañoAplicar);
			}

			ActualizarBarrasUI();

			if (vidaActual <= 0)
			{
				_estaMuerto = true;
				_anim.Play("derrota");
			}
			else
			{
				_habilidadActiva = false;
				ReproducirDefensa(); // Ejecuta animación "defensa" limpia
			}
			return;
		}

		base.RecibirDaño(cantidad);
	}

	// ── SEÑAL: CAMBIO DE FRAME ────────────────────────────────────────────────
	private void OnFrameChanged()
	{
		string anim  = (string)_anim.Animation;
		int    frame = _anim.Frame;

		if (anim == "ataque" && frame == 7)
		{
			AplicarDañoDirecto(_objetivoAtaque, puntosAtaque);
		}

		if (anim == "habilidad" && _habilidadActiva)
		{
			int danioHabilidad = Mathf.Max(10, puntosAtaque - 35);
			if (frame == 1) AplicarDañoDirecto(_objetivoHabilidad1, danioHabilidad);
			if (frame == 7) AplicarDañoDirecto(_objetivoHabilidad2, danioHabilidad);
		}

		if (anim == "derrota" && frame == 18 && !_derrotaIniciada)
		{
			_derrotaIniciada = true;
			Tween tw = CreateTween();
			tw.TweenProperty(this, "modulate:a", 0.0f, 0.4f);
			tw.Finished += () => { if (IsInstanceValid(this)) QueueFree(); };
		}
	}

	// ── SEÑAL: FIN DE ANIMACIÓN ───────────────────────────────────────────────
	private void OnAnimationFinished()
	{
		switch ((string)_anim.Animation)
		{
			case "pre defensa":
				if (_habilidadActiva)
				{
					_anim.Play("habilidad");
				}
				else
				{
					_anim.Pause(); // Se congela agachado esperando golpe
				}
				break;

			case "habilidad":
				if (_habilidadActiva)
				{
					_habilidadActiva = false;
					// Termina los 2 disparos -> Ejecuta "defensa" para levantarse
					ReproducirDefensa();
				}
				break;

			case "daño":
				if (!_estaMuerto) ReproducirIdle();
				break;
		}
	}

	// ── HELPER DE DAÑO (CORREGIDO) ────────────────────────────────────────────
	private void AplicarDañoDirecto(Node2D objetivo, int cantidad)
	{
		if (objetivo != null && IsInstanceValid(objetivo))
		{
			// SOLO llamamos a RecibirDaño.
			// No forzamos "recibir_daño" para no romper la defensa de la víctima.
			objetivo.Call("RecibirDaño", cantidad);
			var campo = GetTree().Root.FindChild("Campo1", true, false);
			if (campo != null) campo.Call("RegistrarDañoTropa", this, cantidad);
		}
	}

	// ── HELPER DE CARRIL ──────────────────────────────────────────────────────
	private string ObtenerMiCarril()
	{
		if (!HasMeta("carril")) return "1";
		return ((string)GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
	}

	private Node2D BuscarObjetivoEnCarril(string carrilTarget)
	{
		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		string grupoMuro    = IsInGroup("tropas_jugador") ? "muros_rival"  : "muros_jugador";

		foreach (Node n in GetTree().GetNodesInGroup(grupoMuro))
		{
			if (!(n is Node2D m) || !IsInstanceValid(m) || !m.HasMeta("carril")) continue;
			string cm = ((string)m.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			if (cm == carrilTarget) return m;
		}

		Node2D mejor = null;
		int min = int.MaxValue;

		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;

			string c = ((string)e.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			if (c != carrilTarget) continue;

			int v = 0;
			try { v = (int)e.Get("vidaActual"); } catch { }
			if (v < min) { min = v; mejor = e; }
		}
		return mejor;
	}
}
