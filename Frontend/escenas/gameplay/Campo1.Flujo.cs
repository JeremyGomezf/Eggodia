using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ── HELPERS DE FASE ──────────────────────────────────────────────────

	private bool TodosSpotsOcupados()
	{
		foreach (string nombre in new[] { "Mod1", "Mod2", "Mod3" })
		{
			Node2D zona = GetTree().Root.FindChild(nombre, true, false) as Node2D;
			if (zona != null && zona.GetNodeOrNull("Ocupado") == null) return false;
		}
		return true;
	}

	private int ContarSpotsLibresJugador()
	{
		int libres = 0;
		foreach (string nombre in new[] { "Mod1", "Mod2", "Mod3" })
		{
			Node2D zona = GetTree().Root.FindChild(nombre, true, false) as Node2D;
			if (zona != null && zona.GetNodeOrNull("Ocupado") == null) libres++;
		}
		return libres;
	}

	private async void MostrarAvisoApertura()
	{
		await ToSignal(GetTree().CreateTimer(0.5f), "timeout");
		if (juegoTerminado) return;

		var host = new CenterContainer();
		host.SetAnchorsPreset(Control.LayoutPreset.VcenterWide);
		host.OffsetTop = -60; host.OffsetBottom = 60;
		host.MouseFilter = Control.MouseFilterEnum.Ignore;
		host.ZIndex = 160;

		var lbl = new Label();
		lbl.Text = "FASE DE APERTURA\nColoca tus 3 tropas para iniciar la batalla";
		lbl.AddThemeColorOverride("font_color", Colors.Gold);
		lbl.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
		lbl.AddThemeConstantOverride("shadow_offset_y", 2);
		lbl.AddThemeConstantOverride("outline_size", 5);
		lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
		lbl.AddThemeFontSizeOverride("font_size", 24);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		host.AddChild(lbl);
		CapaHUD().AddChild(host);

		Tween tw = CreateTween().SetParallel(true);
		tw.TweenProperty(host, "modulate:a", 0.0f, 0.8f).SetDelay(2.8f);
		tw.Finished += () => { if (IsInstanceValid(host)) host.QueueFree(); };
	}

	// ── MENÚ TROPA ────────────────────────────────────────────────────────
	public void MostrarMenuTropa(Node2D tropa)
	{
		if (!esTurnoJugador || movimientosRestantes <= 0 || juegoTerminado || tropa.IsInGroup("tropas_rival")) return;

		// Fase de apertura: obligatorio colocar 3 cartas antes de poder atacar
		if (_faseApertura)
		{
			int faltan = ContarSpotsLibresJugador();
			MostrarAviso($"Coloca tus {faltan} tropa(s) restante(s)", Colors.Gold);
			return;
		}

		// Fuera de apertura: bloquear ataque si no ha invocado aún
		if (faseInvocacion)
		{
			MostrarAviso("Invoca una tropa primero", Colors.Yellow);
			return;
		}

		// Tropa bloqueada (hechizo o atrapada por tentáculos): no se muestra el menú de
		// acciones, solo sus barras de vida/escudo (ya visibles por el propio clic).
		if (EstaBlockeada(tropa))
		{
			MostrarAviso("Esta tropa no puede actuar ahora mismo", Colors.OrangeRed);
			return;
		}

		// Evitar refresco redundante si el menú ya está abierto para esta misma tropa
		if (tropaSeleccionada == tropa && menuAcciones.Visible) return;
		tropaSeleccionada = tropa;

		// Botón Ataque: siempre visible y habilitado
		Button btnA = menuAcciones?.GetNodeOrNull<Button>("HBoxContainer/BtnAtaque");
		if (btnA != null) { btnA.Visible = true; btnA.Disabled = false; btnA.Modulate = Colors.White; }

		// Botón Defensa: puede ocultarse completamente si la tropa no lo soporta
		Button btnD = menuAcciones?.GetNodeOrNull<Button>("HBoxContainer/BtnDefensa");
		if (btnD != null)
		{
			bool mostrarD = true;
			try { mostrarD = (bool)tropa.Call("MostrarBotonDefensa"); } catch { }
			if (!mostrarD)
			{
				btnD.Visible = false;
			}
			else
			{
				bool sinEscudo    = Gi(tropa, "escudoActual") <= 0;
				bool tieneDefensa = true;
				try { tieneDefensa = (bool)tropa.Call("TienePosturaDefensiva"); } catch { }
				bool bloquear = sinEscudo || !tieneDefensa;
				btnD.Visible  = true;
				btnD.Disabled = bloquear;
				btnD.Modulate = bloquear ? new Color(1, 1, 1, 0.4f) : Colors.White;
			}
		}

		// Botón Habilidad: puede ocultarse completamente, bloquearse por carril, o deshabilitarse si ya usó
		if (btnHabilidad != null)
		{
			bool mostrarH = true;
			try { mostrarH = (bool)tropa.Call("MostrarBotonHabilidad"); } catch { }
			if (!mostrarH)
			{
				btnHabilidad.Visible = false;
			}
			else
			{
				bool tieneH = false;
				try { tieneH = (bool)tropa.Call("TieneHabilidadEspecial"); } catch { }
				bool usada    = HabilidadUsada(tropa);
				bool bloqueada = false;
				try { bloqueada = (bool)tropa.Call("HabilidadBloqueada"); } catch { }
				bool deshabilitar = !tieneH || usada || bloqueada;
				btnHabilidad.Visible  = true;
				btnHabilidad.Disabled = deshabilitar;
				btnHabilidad.Modulate = deshabilitar ? new Color(1, 1, 1, 0.4f) : Colors.White;
			}
		}
		menuAcciones.GlobalPosition = tropa.GetGlobalTransformWithCanvas().Origin + new Vector2(-50, -110);
		menuAcciones.Visible = true;
	}

	public void _on_btn_ataque_pressed()
	{
		if (tropaSeleccionada == null || !IsInstanceValid(tropaSeleccionada)) return;
		if (EstaBlockeada(tropaSeleccionada)) { menuAcciones.Visible = false; return; }

		ProcesarCombateFrontal(tropaSeleccionada, "tropas_rival");
		tropaSeleccionada.Call("SetActivo", false);
		menuAcciones.Visible = false;
		RegistrarGastoMovimiento();
	}

	public void _on_btn_defensa_pressed()
	{
		if (tropaSeleccionada == null || !IsInstanceValid(tropaSeleccionada)) return;
		if (EstaBlockeada(tropaSeleccionada) || Gi(tropaSeleccionada, "escudoActual") <= 0)
		{ menuAcciones.Visible = false; return; }
		tropaSeleccionada.Call("EjecutarAccion", "preparar_defensa");
		tropaSeleccionada.Call("SetActivo", false);
		menuAcciones.Visible = false;
		RegistrarGastoMovimiento();
	}

	public void _on_btn_habilidad_pressed()
	{
		if (tropaSeleccionada == null || !IsInstanceValid(tropaSeleccionada)) return;
		if (HabilidadUsada(tropaSeleccionada)) { menuAcciones.Visible = false; return; }
		// Chequeo servidor-side independiente del estado visual del botón: ninguna tropa puede
		// usar su habilidad antes de cumplir su turno propio de desbloqueo, sin excepciones.
		if (HabilidadBloqueadaTurno(tropaSeleccionada)) { menuAcciones.Visible = false; return; }
		tropaSeleccionada.Call("EjecutarAccion", "usar_habilidad");
		tropaSeleccionada.Call("SetActivo", false);
		menuAcciones.Visible = false;
		RegistrarGastoMovimiento();
	}

	private bool HabilidadUsada(Node2D t) { try { return (bool)t.Get("habilidadUsada"); } catch { return false; } }
	private bool HabilidadBloqueadaTurno(Node2D t) { try { return (bool)t.Call("HabilidadBloqueada"); } catch { return false; } }

	// ── BARAJAR / SACRIFICIO ──────────────────────────────────────────────
	public void _on_barajar_pressed()
	{
		if (!esTurnoJugador || movimientosRestantes <= 0 || usosBarajar >= MAX_BARAJAR || _faseApertura) return;
		usosBarajar++; EjecutarBarajadoLogico(); RegistrarGastoMovimiento();
	}

	private void EjecutarBarajadoLogico()
	{
		foreach (Node n in contenedorMano.GetChildren()) if (n is Carta c) { c.NombreSpot = "X"; c.QueueFree(); }
		GetTree().CreateTimer(0.1f).Timeout += () => { PrepararMazoSinRepetir(); BarajarMazoInicial(); };
	}

	private void CompletarManoAlInicio()
	{
		// Rellena los spots vacíos respetando la composición objetivo del turno
		// (2 tácticos + 2 asesinos, o coloso cada 3 turnos).
		RellenarManoObjetivo();
	}

	public void _on_sacrificar_pressed()
	{
		if (!esTurnoJugador || movimientosRestantes <= 0 || usosSacrificio >= MAX_SACRIFICIO || vidaJugador <= 500 || _faseApertura)
		{ if (modoSacrificioActivo) CancelarSacrificio(); return; }
		modoSacrificioActivo = !modoSacrificioActivo;
		Input.SetCustomMouseCursor(modoSacrificioActivo ? iconoCursorSacrificio : null);
	}

	private void VerificarSacrificioEnCampo(Vector2 p)
	{
		foreach (Node2D punto in GetTree().GetNodesInGroup("zonas_invocacion"))
		{
			if (punto.GlobalPosition.DistanceTo(p) >= 110f) continue;
			Node m = punto.GetNodeOrNull("Ocupado");
			if (m == null || !m.HasMeta("tropa_instanciada")) continue;
			Node2D t = (Node2D)m.GetMeta("tropa_instanciada");
			if (!IsInstanceValid(t)) continue;
			usosSacrificio++; EjecutarMuerteTropaSacrificada(t); CancelarSacrificio(); RegistrarGastoMovimiento(); break;
		}
	}

	private void CancelarSacrificio() { modoSacrificioActivo = false; Input.SetCustomMouseCursor(null); }

	// ── INVOCACIÓN Y TRANSFORMACIÓN ───────────────────────────────────────
	public bool TropaInvocada(Node2D puntoMod, PackedScene escenaTropa, int idxMazo = -1)
	{
		if (juegoTerminado || !esTurnoJugador || !puntoMod.IsInGroup("zonas_invocacion")) return false;
		if (puntoMod.GetNodeOrNull("Ocupado") != null || escenaTropa == null) return false;
		Node2D t = (Node2D)escenaTropa.Instantiate();
		AddChild(t);
		if (t is TropaBase tbInv) tbInv.ColocarPorCentroColision(puntoMod.GlobalPosition);
		else                      t.GlobalPosition = puntoMod.GlobalPosition;
		t.AddToGroup("tropas_jugador"); t.SetMeta("carril", puntoMod.Name);
		t.Visible = true; t.Modulate = Colors.White; // seguro anti-invisible
		if (idxMazo >= 0) t.SetMeta("idx_mazo", idxMazo); // para el sistema de reaparición
		t.ZIndex = (string)puntoMod.Name switch { "Mod3" => 100, "Mod2" => 50, _ => 10 };
		Node marc = new Node(); marc.Name = "Ocupado"; puntoMod.AddChild(marc); marc.SetMeta("tropa_instanciada", t);

		// Activar inmediatamente para que se pueda usar en el mismo turno
		if (t.HasMethod("SetActivo")) t.Call("SetActivo", true);

		tropasInvocadasTurno++;
		faseInvocacion = false;

		// Fase de apertura: pasar turno automáticamente al llenar los 3 carriles
		if (_faseApertura && TodosSpotsOcupados())
		{
			MostrarAviso("Tropas listas. La CPU prepara sus fuerzas...", Colors.LightGreen);
			GetTree().CreateTimer(1.2f).Timeout += () => { if (!juegoTerminado) CambiarTurno(); };
			return true;
		}

		return true;
	}

	public void InvocacionRival(Node2D puntoMod, PackedScene escenaTropa)
	{
		if (escenaTropa == null || puntoMod == null) return;
		Node2D t = (Node2D)escenaTropa.Instantiate();
		AddChild(t);
		if (t is TropaBase tbRival) tbRival.ColocarPorCentroColision(puntoMod.GlobalPosition);
		else                        t.GlobalPosition = puntoMod.GlobalPosition;
		t.AddToGroup("tropas_rival");
		t.SetMeta("carril", puntoMod.Name);
		t.ZIndex = (string)puntoMod.Name switch { "ModRival3" => 100, "ModRival2" => 50, _ => 10 };

		// Garantizar visibilidad: evita tropas rivales que aparecen invisibles por
		// un modulate/visible heredado de la escena.
		t.Visible  = true;
		t.Modulate = Colors.White;

		// Corregir orientación sin romper escala ni rotaciones
		AsegurarOrientacionRival(t);

		Node m = puntoMod.GetNodeOrNull("Ocupado");
		if (m == null)
		{
			m = new Node(); 
			m.Name = "Ocupado"; 
			puntoMod.AddChild(m); 
		}
		m.SetMeta("tropa_instanciada", t);
	}

	// 🔄 SISTEMA DE TRANSFORMACIÓN (Ejemplo: Promoción del Peón)
	public void ReemplazarTropaTransformada(Node2D tropaOriginal, PackedScene nuevaEscena)
	{
		if (!IsInstanceValid(tropaOriginal) || nuevaEscena == null) return;

		bool esRival = tropaOriginal.IsInGroup("tropas_rival");
		string carril = tropaOriginal.HasMeta("carril") ? (string)tropaOriginal.GetMeta("carril") : "";

		// Instanciar nueva tropa
		Node2D nuevaTropa = (Node2D)nuevaEscena.Instantiate();
		AddChild(nuevaTropa);
		nuevaTropa.GlobalPosition = tropaOriginal.GlobalPosition;
		// La nueva pieza hereda el Z-Index exacto del carril que ocupaba el Peón (profundidad
		// visual por carril: Mod1/ModRival1=1, Mod2/ModRival2=5, Mod3/ModRival3=10), en vez de
		// quedarse con el ZIndex por defecto de su propia escena.
		nuevaTropa.ZIndex = tropaOriginal.ZIndex;

		if (esRival)
		{
			nuevaTropa.AddToGroup("tropas_rival");
			AsegurarOrientacionRival(nuevaTropa);
		}
		else
		{
			nuevaTropa.AddToGroup("tropas_jugador");
		}

		// La pieza promovida puede seguir actuando (atacar/defender) en el mismo turno, sin
		// perder su acción — simétrico para jugador y rival. Su habilidad, en cambio, NO se
		// habilita de inmediato: es una tropa nueva y debe cumplir su propio turno de
		// desbloqueo desde 0, igual que cualquier otra invocación (sin excepciones).
		if (nuevaTropa.HasMethod("SetActivo")) nuevaTropa.Call("SetActivo", true);

		if (!string.IsNullOrEmpty(carril))
		{
			nuevaTropa.SetMeta("carril", carril);
			Node2D zona = GetTree().Root.FindChild(carril, true, false) as Node2D;
			Node ocupado = zona?.GetNodeOrNull("Ocupado");
			if (ocupado != null)
			{
				ocupado.SetMeta("tropa_instanciada", nuevaTropa);
			}
		}

		// Limpiar la versión anterior del Peón
		tropaOriginal.QueueFree();
	}

	// ── MUERTE ────────────────────────────────────────────────────────────
	public void EjecutarMuerteTropaSacrificada(Node2D tropa)
	{
		if (!IsInstanceValid(tropa)) return;

		// Si esta tropa estaba atrapada por los tentáculos del Calamar, se liberan de
		// inmediato al morir (sin esperar al próximo tick de daño cada 10s).
		if (tropa.HasMeta("tentaculo_activo"))
		{
			var tentRef = tropa.GetMeta("tentaculo_activo").AsGodotObject() as Node;
			if (tentRef != null && IsInstanceValid(tentRef) && tentRef.HasMethod("Liberar"))
				tentRef.Call("Liberar");
		}

		// Algunas tropas (p. ej. Granadero) ya reproducen su propia animación de derrota
		// antes de notificar aquí — no reiniciarla si ya está en curso.
		var animSprite = tropa.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		bool yaEnDerrota = animSprite != null && ((string)animSprite.Animation).Contains("derrota");
		if (!yaEnDerrota) tropa.Call("ReproducirDerrota");

		// Colosos/especiales: castigo de daño masivo fijo al eliminarlos, en vez del
		// castigo genérico por vidaMaxima.
		int castigo = tropa switch
		{
			TRexPrime            => 550,
			TanqueCartoonPrime   => 500,
			GranaderoCartoonPrime => 370,
			_                     => Gi(tropa, "vidaMaxima"),
		};

		if (tropa.IsInGroup("tropas_rival"))
		{
			vidaRival -= castigo; if (vidaRival < 0) vidaRival = 0;
			_tropasEliminadasRival++;
		}
		else
		{
			vidaJugador -= castigo; if (vidaJugador < 0) vidaJugador = 0;
			_tropasEliminadasJugador++;
			// Registrar cooldown de reaparición de la carta del jugador
			NotificarMuerteIndiceJugador(tropa);
		}

		if (tropa.HasMeta("carril"))
		{
			string carril = (string)tropa.GetMeta("carril");
			Node2D zona = GetTree().Root.FindChild(carril, true, false) as Node2D;
			zona?.GetNodeOrNull("Ocupado")?.Free();
		}

		Tween tw = CreateTween(); tw.TweenInterval(0.8f); tw.TweenProperty(tropa, "modulate:a", 0.0f, 0.6f);
		tw.Finished += () => { if (IsInstanceValid(tropa)) tropa.QueueFree(); };
		CheckEstadoJuego(); ActualizarInterfaz();
	}

	// ── MAZO ──────────────────────────────────────────────────────────────
	private void PrepararMazoSinRepetir()
	{
		mazoIndices.Clear();
		for (int i = 0; i < imagenesCartas.Length; i++) mazoIndices.Add(i);
		for (int i = 0; i < mazoIndices.Count; i++)
		{ int r = random.Next(i, mazoIndices.Count); (mazoIndices[i], mazoIndices[r]) = (mazoIndices[r], mazoIndices[i]); }
		proximoIndiceMazo = 0;
	}

	public void BarajarMazoInicial()
	{
		// Mano de apertura: 2 tácticos + 2 asesinos (turno 1 no es de coloso).
		RellenarManoObjetivo();
		MostrarTutorialInicio();
	}

	private async void MostrarTutorialInicio()
	{
		if (_turnosJugados > 0) return;

		await ToSignal(GetTree().CreateTimer(1.0f), "timeout");

		string[] pasos = {
			"Bienvenido a Age of Cards",
			"Arrastra una carta al campo para invocar tu tropa",
			"Haz clic en tu tropa para atacar o defender",
			"Usa hechizos para potenciar tus tropas o dañar al rival"
		};

		for (int i = 0; i < pasos.Length; i++)
		{
			MostrarAviso(pasos[i], Colors.White);
			await ToSignal(GetTree().CreateTimer(2.5f), "timeout");
		}
	}

	// Roba UNA carta elegible a un spot concreto (usado por el hechizo "Robar carta").
	private void CrearNuevaCartaEnSpot(string id)
	{
		int idx = ElegirIndiceParaSpot();
		if (idx < 0) idx = ElegirRelajado();
		if (idx >= 0) CrearCartaConIndice(id, idx);
	}

	private void CrearEscenaDeBatalla()
	{
		Marker2D m1 = GetNodeOrNull<Marker2D>("SpawnTrono1"), m2 = GetNodeOrNull<Marker2D>("SpawnTrono2");
		if (m1 == null || m2 == null || escenaTronoRef == null) return;

		tronoJugador = (tronocampo)escenaTronoRef.Instantiate(); AddChild(tronoJugador);
		tronoJugador.GlobalPosition = m1.GlobalPosition;
		string skinPath = Preferencias.RutaSkinActiva;
		var skinJugador = ResourceLoader.Exists(skinPath) ? GD.Load<PackedScene>(skinPath) : escenaReyHuevoRef;
		tronoJugador.CargarHuevo(skinJugador ?? escenaReyHuevoRef, false);

		tronoRival = (tronocampo)escenaTronoRef.Instantiate(); AddChild(tronoRival);
		tronoRival.GlobalPosition = m2.GlobalPosition;
		tronoRival.CargarHuevo(SkinAleatoria() ?? escenaDinoHuevoRef, true);
	}

	/// <summary>Skin de Huevo aleatoria entre todas las disponibles en la tienda — solo para el
	/// rival/IA; el jugador usa la skin que tiene seleccionada en el menú (Preferencias.RutaSkinActiva).</summary>
	private PackedScene SkinAleatoria()
	{
		string skinPath = Preferencias.SKIN_ESCENAS[random.Next(Preferencias.SKIN_ESCENAS.Length)];
		return ResourceLoader.Exists(skinPath) ? GD.Load<PackedScene>(skinPath) : null;
	}

	private void ColocarTropasIniciales()
	{
		Node2D zonaJugador = GetTree().Root.FindChild("Mod2", true, false) as Node2D;
		if (zonaJugador != null && zonaJugador.GetNodeOrNull("Ocupado") == null)
		{
			int idx = mazoIndices[proximoIndiceMazo % mazoIndices.Count];
			proximoIndiceMazo++;
			var escena = GD.Load<PackedScene>(escenasTropas[idx]);
			if (escena != null)
			{
				Node2D t = (Node2D)escena.Instantiate();
				AddChild(t); t.GlobalPosition = zonaJugador.GlobalPosition;
				t.AddToGroup("tropas_jugador"); t.SetMeta("carril", "Mod2");
				Node m = new Node(); m.Name = "Ocupado"; zonaJugador.AddChild(m); m.SetMeta("tropa_instanciada", t);
				if (t.HasMethod("SetActivo")) t.Call("SetActivo", true);
			}
		}

		Node2D zonaRival = GetTree().Root.FindChild("ModRival2", true, false) as Node2D;
		if (zonaRival != null && zonaRival.GetNodeOrNull("Ocupado") == null)
		{
			var escena = ElegirTropaCPU();
			if (escena != null)
			{
				InvocacionRival(zonaRival, escena);
			}
		}
	}

	// ── INTERFAZ ──────────────────────────────────────────────────────────
	private void ActualizarInterfaz()
	{
		if (btnBarajar != null)    { bool b = !esTurnoJugador || usosBarajar >= MAX_BARAJAR || movimientosRestantes <= 0; btnBarajar.Disabled = b; btnBarajar.Modulate = b ? new Color(1, 1, 1, 0.4f) : Colors.White; }
		if (btnSacrificio != null) { bool s = !esTurnoJugador || usosSacrificio >= MAX_SACRIFICIO || vidaJugador <= 500 || movimientosRestantes <= 0; btnSacrificio.Disabled = s; btnSacrificio.Modulate = s ? new Color(1, 1, 1, 0.4f) : Colors.White; }
		if (_lblVida1 != null) _lblVida1.Text = $"HP {vidaJugador}/{vidaMaxJugador}";
		if (_lblVida2 != null) _lblVida2.Text = $"HP {vidaRival}/{vidaMaxJugador}";
		if (_barraHPJugador != null) _barraHPJugador.Value = (float)vidaJugador / vidaMaxJugador * 100;
		if (_barraHPRival   != null) _barraHPRival.Value   = (float)vidaRival   / vidaMaxJugador * 100;
		if (_lblTiempo != null) { int m = tiempoTotalPartida / 60, s = tiempoTotalPartida % 60; _lblTiempo.Text = $"Tiempo {m}:{s:00}"; }
		if (_lblTurnoInfo != null)
		{
			var l = _lblTurnoInfo;
			string dif     = _dificultadCPU == 0 ? "Fácil" : _dificultadCPU == 1 ? "Normal" : "Difícil";
			bool urgente   = esTurnoJugador && tiempoTurnoActual <= 8;
			string timer   = urgente ? $"{tiempoTurnoActual}s!" : $"{tiempoTurnoActual}s";
			int maxEnergy  = ENERGIA_MAXIMA;
			int turnoNum   = _turnosJugados / 2 + 1;
			l.Text = $"Turno {turnoNum}  ·  {dif}\nEnergía {movimientosRestantes}/{maxEnergy}\n{(esTurnoJugador ? "TU TURNO" : "TURNO CPU")}  {timer}";
			l.Modulate = Colors.White;
			Color acento = urgente         ? new Color(1f, 0.4f, 0.35f)
						 : esTurnoJugador ? new Color(0.5f, 1f, 0.6f)
						 :                  new Color(1f, 0.55f, 0.5f);
			l.AddThemeColorOverride("font_color", acento);
		}
	}
}
