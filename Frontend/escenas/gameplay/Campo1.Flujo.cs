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
		if (tropaSeleccionada == tropa && menuAcciones.Visible) { _menuTropaRecienAbiertoEsteClic = true; return; }
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
		_menuTropaRecienAbiertoEsteClic = true;
	}

	public void _on_btn_ataque_pressed()
	{
		if (tropaSeleccionada == null || !IsInstanceValid(tropaSeleccionada)) return;
		if (EstaBlockeada(tropaSeleccionada)) { menuAcciones.Visible = false; return; }

		string carrilAtk = tropaSeleccionada.HasMeta("carril") ? (string)tropaSeleccionada.GetMeta("carril") : "";
		ProcesarCombateFrontal(tropaSeleccionada, "tropas_rival");
		tropaSeleccionada.Call("SetActivo", false);
		menuAcciones.Visible = false;
		RegistrarGastoMovimiento();
		if (EsOnline) EmitirAccionOnline("atacar", new Godot.Collections.Dictionary { { "carrilAtacante", carrilAtk } });
	}

	public void _on_btn_defensa_pressed()
	{
		if (tropaSeleccionada == null || !IsInstanceValid(tropaSeleccionada)) return;
		if (EstaBlockeada(tropaSeleccionada) || Gi(tropaSeleccionada, "escudoActual") <= 0)
		{ menuAcciones.Visible = false; return; }
		string carrilDef = tropaSeleccionada.HasMeta("carril") ? (string)tropaSeleccionada.GetMeta("carril") : "";
		tropaSeleccionada.Call("EjecutarAccion", "preparar_defensa");
		tropaSeleccionada.Call("SetActivo", false);
		menuAcciones.Visible = false;
		RegistrarGastoMovimiento();
		if (EsOnline) EmitirAccionOnline("defensa", new Godot.Collections.Dictionary { { "carrilDef", carrilDef } });
	}

	public void _on_btn_habilidad_pressed()
	{
		if (tropaSeleccionada == null || !IsInstanceValid(tropaSeleccionada)) return;
		if (HabilidadUsada(tropaSeleccionada)) { menuAcciones.Visible = false; return; }
		// Chequeo servidor-side independiente del estado visual del botón: ninguna tropa puede
		// usar su habilidad antes de cumplir su turno propio de desbloqueo, sin excepciones.
		if (HabilidadBloqueadaTurno(tropaSeleccionada)) { menuAcciones.Visible = false; return; }
		string carrilHab = tropaSeleccionada.HasMeta("carril") ? (string)tropaSeleccionada.GetMeta("carril") : "";
		tropaSeleccionada.Call("EjecutarAccion", "usar_habilidad");
		tropaSeleccionada.Call("SetActivo", false);
		menuAcciones.Visible = false;
		RegistrarGastoMovimiento();
		if (EsOnline) EmitirAccionOnline("habilidad", new Godot.Collections.Dictionary { { "carrilHab", carrilHab } });
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

	// Tropa que ya recibió el primer clic en modo sacrificio: hace falta un SEGUNDO clic sobre
	// ELLA MISMA para confirmar y matarla — evita que un clic apurado/accidental mate a la tropa
	// equivocada sin darse cuenta. Tocar cualquier OTRA tropa reinicia la selección a esa nueva.
	private Node2D _candidatoSacrificio = null;

	public void _on_sacrificar_pressed()
	{
		if (!esTurnoJugador || movimientosRestantes <= 0 || usosSacrificio >= MAX_SACRIFICIO || vidaJugador <= 500 || _faseApertura)
		{ if (modoSacrificioActivo) CancelarSacrificio(); return; }
		modoSacrificioActivo = !modoSacrificioActivo;
		Input.SetCustomMouseCursor(modoSacrificioActivo ? iconoCursorSacrificio : null);
		ActualizarEscalaBotonSacrificio();
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

			if (_candidatoSacrificio == t)
			{
				// Segundo clic sobre la MISMA tropa: confirma.
				usosSacrificio++; EjecutarMuerteTropaSacrificada(t); CancelarSacrificio(); RegistrarGastoMovimiento();
			}
			else
			{
				// Primer clic (o clic sobre una tropa distinta a la ya marcada): solo la marca,
				// pide el segundo clic para confirmar — con un destello rojo de aviso.
				_candidatoSacrificio = t;
				MostrarAviso("Tocá de nuevo para confirmar el sacrificio", Colors.OrangeRed);
				Tween tw = t.CreateTween();
				tw.TweenProperty(t, "modulate", new Color(3f, 0.3f, 0.3f, 1f), 0.15f);
				tw.TweenProperty(t, "modulate", Colors.White, 0.25f);
			}
			break;
		}
	}

	private void CancelarSacrificio()
	{
		modoSacrificioActivo = false;
		_candidatoSacrificio = null;
		Input.SetCustomMouseCursor(null);
		ActualizarEscalaBotonSacrificio();
	}

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
		// Estadística de "carta más usada" (perfil): se guarda por ruta de escena, no por nombre
		// corto, para poder cruzarla luego con CartaData y mostrar su imagen real.
		if (!string.IsNullOrEmpty(t.SceneFilePath)) Preferencias.RegistrarUsoCarta(t.SceneFilePath);
		if (idxMazo >= 0) t.SetMeta("idx_mazo", idxMazo); // para el sistema de reaparición
		t.ZIndex = (string)puntoMod.Name switch { "Mod3" => 100, "Mod2" => 50, _ => 10 };
		Node marc = new Node(); marc.Name = "Ocupado"; puntoMod.AddChild(marc); marc.SetMeta("tropa_instanciada", t);

		// Activar inmediatamente para que se pueda usar en el mismo turno
		if (t.HasMethod("SetActivo")) t.Call("SetActivo", true);

		tropasInvocadasTurno++;
		faseInvocacion = false;
		ReacomodarManoTropas(); // por si se jugó la carta robada (Spot4) y hay que volver a 3

		// Nota: la reposición de mano NO se hace acá al instante (se probó y se sentía como un bug —
		// una carta nueva aparecía de golpe apenas jugabas una sola carta, en medio del turno). Se
		// programa para el segundo 5 del turno en el que corresponda — ver CambiarTurno() en
		// Campo1.Turnos.cs.

		// En línea: avisar al rival que invoqué esta tropa (la reproduce en su carril espejado).
		if (EsOnline) EmitirAccionOnline("invocar", new Godot.Collections.Dictionary { { "carril", (string)puntoMod.Name } });

		// Fase de apertura: pasar turno automáticamente al llenar los 3 carriles
		if (_faseApertura && TodosSpotsOcupados())
		{
			string quien = EsOnline ? "El rival" : "La CPU";
			MostrarAviso($"Tropas listas. {quien} prepara sus fuerzas...", Colors.LightGreen);
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
		t.AddToGroup("tropas_rival"); // antes del centrado: OffsetCentroColision() espeja X según este grupo
		if (t is TropaBase tbRival) tbRival.ColocarPorCentroColision(puntoMod.GlobalPosition);
		else                        t.GlobalPosition = puntoMod.GlobalPosition;
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
		// En reproducción visual (online) no se mata nada: quién vive o muere lo decide el snapshot
		// (ReconciliarLigero). Así una habilidad reproducida no elimina una tropa que sigue viva.
		if (SoloVisualOnline) return;

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

		// Desvanecer desde el frame 17 de "derrota" (no un timer fijo): así la animación de
		// derrota se aprecia completa antes de desaparecer, y el carril no queda "libre" tan
		// rápido como para que la próxima tropa invocada choque visualmente con la anterior
		// todavía desvaneciéndose. Ka-Bar es la única excepción pedida: su derrota usa el frame 19.
		int frameDesvanecer = tropa is KaBarCartoonPrime ? 19 : 17;
		TropaBase.DesvanecerTrasFrameDerrota(tropa, animSprite, frameDesvanecer, 0.6f);
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

		tronoJugador = (TronoCampo)escenaTronoRef.Instantiate(); AddChild(tronoJugador);
		tronoJugador.GlobalPosition = m1.GlobalPosition;
		// Antes esto solo miraba Preferencias.RutaSkinActiva (las 8 skins estándar) — si tenías
		// equipada una skin EXCLUSIVA (de dev o por código), acá se ignoraba por completo y en
		// batalla siempre aparecía Rey Huevo. SkinExclusivaEquipadaEscena() prueba primero la
		// exclusiva equipada; si no hay ninguna (o no tiene escena de batalla mapeada), cae a la
		// estándar de siempre.
		var skinJugador = SkinExclusivaEquipadaEscena() ?? SkinEstandarEquipada();
		tronoJugador.CargarHuevo(skinJugador ?? escenaReyHuevoRef, false);
		tronoJugador.CambiarTrono(GD.Load<Texture2D>(Preferencias.RutaTronoActiva));

		tronoRival = (TronoCampo)escenaTronoRef.Instantiate(); AddChild(tronoRival);
		tronoRival.GlobalPosition = m2.GlobalPosition;
		// En línea, el "rival" es un jugador real: se muestra su skin/trono REALES (sincronizados al
		// emparejar), no un sorteo — ConfigurarModoOnline() recién fija EsOnline al final de _Ready,
		// así que acá se consulta ContextoOnline.Activo directamente (ya está fijado antes del cambio
		// de escena, en MatchmakingOnline).
		bool esOnlineAhora = ContextoOnline.Activo;
		// VS BOT (offline): si el nombre sorteado del bot coincide con un dev/código conocido
		// (Jeremy/Carlos/Gonzalo/Ec0tec), usa esa skin — si no, sorteo normal entre las estándar.
		var skinRival = esOnlineAhora ? SkinRivalOnline() : (SkinPorNombreCPU() ?? SkinAleatoria());
		tronoRival.CargarHuevo(skinRival ?? escenaDinoHuevoRef, true);
		tronoRival.CambiarTrono(GD.Load<Texture2D>(esOnlineAhora ? TronoRivalOnline() : TronoAleatorio()), true);
	}

	/// <summary>Escena de la skin EXCLUSIVA equipada (dev o por código), si hay alguna equipada y
	/// tiene una escena de batalla mapeada en Preferencias.SKINS_EXCLUSIVAS_ESCENAS. Null si no hay
	/// exclusiva equipada o no matchea ninguna (cae a la estándar).</summary>
	private PackedScene SkinExclusivaEquipadaEscena()
	{
		string exclusiva = Preferencias.SkinExclusivaActiva;
		if (string.IsNullOrEmpty(exclusiva)) return null;
		foreach (var (textura, escena) in Preferencias.SKINS_EXCLUSIVAS_ESCENAS)
			if (textura == exclusiva && ResourceLoader.Exists(escena)) return GD.Load<PackedScene>(escena);
		return null;
	}

	private PackedScene SkinEstandarEquipada()
	{
		string skinPath = Preferencias.RutaSkinActiva;
		return ResourceLoader.Exists(skinPath) ? GD.Load<PackedScene>(skinPath) : escenaReyHuevoRef;
	}

	/// <summary>Skin de Huevo aleatoria entre todas las disponibles en la tienda — solo para el
	/// rival/IA (vs Bot); el jugador usa la skin que tiene seleccionada en el menú (Preferencias.RutaSkinActiva).</summary>
	private PackedScene SkinAleatoria()
	{
		string skinPath = Preferencias.SKIN_ESCENAS[random.Next(Preferencias.SKIN_ESCENAS.Length)];
		return ResourceLoader.Exists(skinPath) ? GD.Load<PackedScene>(skinPath) : null;
	}

	/// <summary>Trono aleatorio entre todos los disponibles en la tienda — solo para el rival/IA
	/// (vs Bot); el jugador usa el trono que tiene equipado en el menú (Preferencias.RutaTronoActiva).</summary>
	private string TronoAleatorio() => Preferencias.TRONO_TEXTURAS[random.Next(Preferencias.TRONO_TEXTURAS.Length)];

	/// <summary>Skin real del rival humano, sincronizada al emparejar (ContextoOnline.RivalSkinIdx).</summary>
	private PackedScene SkinRivalOnline()
	{
		// El índice puede apuntar tanto a una skin de tienda como a una EXCLUSIVA (dev o por
		// código) — ver Preferencias.IndiceSkinParaOnline/EscenaSkinDesdeIndiceOnline.
		string skinPath = Preferencias.EscenaSkinDesdeIndiceOnline(ContextoOnline.RivalSkinIdx);
		return ResourceLoader.Exists(skinPath) ? GD.Load<PackedScene>(skinPath) : null;
	}

	/// <summary>Trono real del rival humano, sincronizado al emparejar (ContextoOnline.RivalTronoIdx).</summary>
	private string TronoRivalOnline()
	{
		int idx = Mathf.Clamp(ContextoOnline.RivalTronoIdx, 0, Preferencias.TRONO_TEXTURAS.Length - 1);
		return Preferencias.TRONO_TEXTURAS[idx];
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

		if (_barraHPJugador != null) _barraHPJugador.Value = (float)vidaJugador / vidaMaxJugador * 100;
		if (_barraHPRival   != null) _barraHPRival.Value   = (float)vidaRival   / vidaMaxJugador * 100;

		if (!juegoTerminado && _lblTiempo != null) { int m = tiempoTotalPartida / 60, s = tiempoTotalPartida % 60; _lblTiempo.Text = $"{m}:{s:00}"; }

		ActualizarContadorTurno();
		ActualizarEnergiaHUD();
	}

	// ── PANEL DE ENERGÍA (EnergiaPanel dentro de TextureProgressBar_User/_Rival) ──────
	// Turno activo: verde → blanco a medida que se gasta la energía (al llegar a EP:0/3 el
	// panel sigue "activo"/blanco durante los 2s de gracia de RegistrarGastoMovimiento, porque
	// esTurnoJugador recién cambia cuando CambiarTurno() se ejecuta de verdad). Turno rival:
	// el panel del jugador (y viceversa) se atenúa, simulando bloqueo.
	//
	// `movimientosRestantes` es un contador COMPARTIDO — en realidad representa "energía del
	// que tiene el turno ahora". _epMostradoJugador/_epMostradoRival lo aíslan por bando: cada
	// uno solo se actualiza durante SU PROPIO turno y conserva su último valor real el resto
	// del tiempo, para que el bando inactivo no se vea bajar a 0 por el gasto del otro.
	private static readonly Color ENERGIA_COLOR_LLENA = new Color(0.4285f, 0.99f, 0.1683f);
	private void ActualizarEnergiaHUD()
	{
		if (esTurnoJugador) _epMostradoJugador = movimientosRestantes;
		else                _epMostradoRival   = movimientosRestantes;

		AplicarPanelEnergia(_lblEnergiaUsuario, _panelEnergiaUsuario, _epMostradoJugador, esTurnoJugador);
		AplicarPanelEnergia(_lblEnergiaRival,   _panelEnergiaRival,   _epMostradoRival,   !esTurnoJugador);
	}

	private void AplicarPanelEnergia(Label lbl, Control panel, int epMostrado, bool activo)
	{
		float pct = ENERGIA_MAXIMA > 0 ? (float)epMostrado / ENERGIA_MAXIMA : 0f;
		Color colorSegunGasto = ENERGIA_COLOR_LLENA.Lerp(Colors.White, 1f - pct);

		if (lbl != null)
		{
			lbl.Text = $"EP: {epMostrado}/{ENERGIA_MAXIMA}";
			lbl.AddThemeColorOverride("font_color", activo ? colorSegunGasto : Colors.White);
		}
		if (panel != null)
			panel.Modulate = activo ? Colors.White : new Color(0.5f, 0.5f, 0.5f, 0.6f);
	}
}
