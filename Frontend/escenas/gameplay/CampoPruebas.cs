using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Campo de pruebas libre — sin turnos, sin IA, sin límite.
/// Permite invocar cualquier tropa, probar habilidades, hechizos y combate manualmente.
/// </summary>
public partial class CampoPruebas : Node2D
{
	private static readonly string[] EscenasTropas =
	{
		"res://cartas prime/Dragon_prime.tscn",
		"res://cartas prime/Golem_prime.tscn",
		"res://cartas prime/Maguin_prime.tscn",
		"res://cartas prime/SoldadoReal_prime.tscn",
		"res://cartas prime/TRex_prime.tscn",
		"res://cartas prime/Tiburon_prime.tscn",
		"res://cartas prime/Peon_prime.tscn",
		"res://cartas prime/Encebollado_prime.tscn",
		"res://cartas prime/CalamarG_prime.tscn",
		"res://cartas prime/Caballo_prime.tscn",
		"res://cartas prime/Dama_prime.tscn",
		"res://cartas prime/Torre_prime.tscn",
		"res://cartas prime/GUERRA CARTOONS/Soldado_cartoon_prime.tscn",
		"res://cartas prime/GUERRA CARTOONS/Campero_cartoon_prime.tscn",
		"res://cartas prime/GUERRA CARTOONS/Granadero_cartoon_prime.tscn",
		"res://cartas prime/GUERRA CARTOONS/Ka-Bar_cartoon_prime.tscn",
		"res://cartas prime/GUERRA CARTOONS/Tanque_cartoon_prime.tscn"
	};

	private static readonly string[] NombresTropas =
	{
		"Dragón", "Golem", "Maguín", "Soldado",
		"T-Rex",  "Tiburón", "Peón",  "Encebollado",
		"Calamar", "Caballo", "Dama", "Torre",
		"Soldado Cartoon", "Campero Cartoon", "Granadero Cartoon",
		"Ka-Bar Cartoon", "Tanque Cartoon"
	};

	private static readonly string[] NombresAliados  = { "Mod1",      "Mod2",      "Mod3"      };
	private static readonly string[] NombresEnemigos = { "ModRival1", "ModRival2", "ModRival3" };

	// Tropas en campo
	private TropaBase[] _aliadas   = new TropaBase[3];
	private TropaBase[] _enemigas  = new TropaBase[3];
	private Node2D[]    _slotsA    = new Node2D[3];
	private Node2D[]    _slotsE    = new Node2D[3];

	// Selectores UI
	private OptionButton _optTropaAliada;
	private OptionButton _optTropaEnemiga;
	private OptionButton _optAtacante;
	private OptionButton _optObjetivo;
	private OptionButton _optHechizo;
	private Label        _lblStats;
	private Label        _lblLog;

	// ══════════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		for (int i = 0; i < 3; i++)
		{
			_slotsA[i] = GetNodeOrNull<Node2D>(NombresAliados[i]);
			_slotsE[i] = GetNodeOrNull<Node2D>(NombresEnemigos[i]);
		}
		CrearInterfaz();
	}

	public override void _Process(double delta)
	{
		SincronizarSlots();
		ManejarMuerte();   // revisa cada frame: el daño de tropas con ráfaga (multi-hit
		                    // por fotograma) llega de forma asíncrona, no en el instante del clic
		ActualizarStats();
	}

	// ── CONSTRUCCIÓN DE INTERFAZ ───────────────────────────────────────────────

	private void CrearInterfaz()
	{
		var canvas = new CanvasLayer();
		canvas.Name = "UIOverlay";
		AddChild(canvas);

		// ── Panel izquierdo ──────────────────────────────────────────────────
		var panelIzq = CrearPanel(0, 0, 265, 720);
		canvas.AddChild(panelIzq);

		var scroll = new ScrollContainer();
		scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		panelIzq.AddChild(scroll);

		var vbox = new VBoxContainer();
		vbox.CustomMinimumSize = new Vector2(255, 0);
		vbox.AddThemeConstantOverride("separation", 4);
		scroll.AddChild(vbox);

		// Título
		var lblTitulo = MkLabel("⚔  CAMPO DE PRUEBAS", Colors.Gold, 15);
		lblTitulo.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(lblTitulo);

		// === ALIADOS ===
		Separador(vbox, "INVOCAR ALIADOS", Colors.LightGreen);
		_optTropaAliada = CrearOptionTropas();
		vbox.AddChild(_optTropaAliada);

		var hbxA = new HBoxContainer();
		vbox.AddChild(hbxA);
		for (int i = 0; i < 3; i++)
		{
			int idx = i;
			hbxA.AddChild(MkBtn($"Slot {i + 1}", () => InvocarAliado(idx, _optTropaAliada.Selected), new Color(0.2f, 0.7f, 0.3f)));
		}

		// === ENEMIGOS ===
		Separador(vbox, "INVOCAR ENEMIGOS", Colors.OrangeRed);
		_optTropaEnemiga = CrearOptionTropas();
		vbox.AddChild(_optTropaEnemiga);

		var hbxE = new HBoxContainer();
		vbox.AddChild(hbxE);
		for (int i = 0; i < 3; i++)
		{
			int idx = i;
			hbxE.AddChild(MkBtn($"Slot {i + 1}", () => InvocarEnemigo(idx, _optTropaEnemiga.Selected), new Color(0.8f, 0.2f, 0.2f)));
		}

		// === COMBATE ===
		Separador(vbox, "COMBATE", Colors.LightBlue);

		vbox.AddChild(MkLabel("Atacante:", Colors.LightGreen, 11));
		_optAtacante = new OptionButton();
		_optAtacante.CustomMinimumSize = new Vector2(255, 26);
		foreach (string s in new[] { "Aliado 1", "Aliado 2", "Aliado 3", "Enemigo 1", "Enemigo 2", "Enemigo 3" })
			_optAtacante.AddItem(s);
		vbox.AddChild(_optAtacante);

		vbox.AddChild(MkLabel("Objetivo:", Colors.OrangeRed, 11));
		_optObjetivo = new OptionButton();
		_optObjetivo.CustomMinimumSize = new Vector2(255, 26);
		foreach (string s in new[] { "Aliado 1", "Aliado 2", "Aliado 3", "Enemigo 1", "Enemigo 2", "Enemigo 3" })
			_optObjetivo.AddItem(s);
		_optObjetivo.Selected = 3;
		vbox.AddChild(_optObjetivo);

		vbox.AddChild(MkBtn("⚔  ATACAR", Atacar, Colors.Red));

		// Defensas
		var hbxDef = new HBoxContainer();
		vbox.AddChild(hbxDef);
		hbxDef.AddChild(MkBtn("🛡 Def Atac.", () => OrdenarDefensa(_optAtacante.Selected), Colors.Cyan));
		hbxDef.AddChild(MkBtn("🛡 Def Obj.", () => OrdenarDefensa(_optObjetivo.Selected), Colors.Cyan));

		// === HABILIDADES ===
		Separador(vbox, "HABILIDADES ESPECIALES", Colors.Yellow);

		vbox.AddChild(MkLabel("Usar habilidad del slot aliado:", Colors.LightGreen, 11));
		var hbxHabA = new HBoxContainer(); vbox.AddChild(hbxHabA);
		for (int i = 0; i < 3; i++) { int idx = i; hbxHabA.AddChild(MkBtn($"A{i + 1}", () => UsarHabilidad(idx, false), Colors.Yellow)); }

		vbox.AddChild(MkLabel("Usar habilidad del slot enemigo:", Colors.OrangeRed, 11));
		var hbxHabE = new HBoxContainer(); vbox.AddChild(hbxHabE);
		for (int i = 0; i < 3; i++) { int idx = i; hbxHabE.AddChild(MkBtn($"E{i + 1}", () => UsarHabilidad(idx, true), Colors.Yellow)); }

		vbox.AddChild(MkBtn("♻  Resetear habilidades (todos)", ResetearHabilidades, Colors.Gray));

		// === HECHIZOS ===
		Separador(vbox, "HECHIZOS", Colors.Magenta);

		vbox.AddChild(MkLabel("Objetivo del hechizo:", Colors.Cyan, 11));
		_optHechizo = new OptionButton();
		_optHechizo.CustomMinimumSize = new Vector2(255, 26);
		foreach (string s in new[] { "Aliado 1", "Aliado 2", "Aliado 3", "Enemigo 1", "Enemigo 2", "Enemigo 3" })
			_optHechizo.AddItem(s);
		vbox.AddChild(_optHechizo);

		var hbxH1 = new HBoxContainer(); vbox.AddChild(hbxH1);
		hbxH1.AddChild(MkBtn("🧅 Encebollado", UsarEncebollado, new Color(1f, 0.7f, 0.1f)));
		hbxH1.AddChild(MkBtn("💚 Curación",    UsarCuracion,    new Color(0.2f, 0.9f, 0.3f)));

		var hbxH2 = new HBoxContainer(); vbox.AddChild(hbxH2);
		hbxH2.AddChild(MkBtn("☠  Veneno",  UsarVeneno,  new Color(0.5f, 0.9f, 0.3f)));
		hbxH2.AddChild(MkBtn("🔒 Bloqueo", UsarBloqueo, new Color(0.3f, 0.5f, 1f)));
		hbxH2.AddChild(MkBtn("🃏 Robo",    UsarRobo,    new Color(0.3f, 0.7f, 1f)));

		// === CAMPO ===
		Separador(vbox, "CAMPO", new Color(0.8f, 0.8f, 0.8f));
		vbox.AddChild(MkBtn("🔄  RESETEAR CAMPO", ResetearCampo, Colors.Orange));
		vbox.AddChild(MkBtn("🏠  VOLVER AL MENÚ",  VolverAlMenu,  Colors.LightBlue));

		// ── Panel de stats (franja inferior) ───────────────────────────────
		var panelStats = CrearPanel(265, 596, 1015, 124);
		canvas.AddChild(panelStats);

		_lblStats = new Label();
		_lblStats.Position          = new Vector2(6, 4);
		_lblStats.Size              = new Vector2(1003, 116);
		_lblStats.AddThemeColorOverride("font_color", Colors.White);
		_lblStats.AddThemeFontSizeOverride("font_size", 12);
		_lblStats.AutowrapMode      = TextServer.AutowrapMode.Off;
		_lblStats.VerticalAlignment = VerticalAlignment.Top;
		panelStats.AddChild(_lblStats);

		// ── Log de mensajes (centro superior) ──────────────────────────────
		_lblLog = new Label();
		_lblLog.Position  = new Vector2(270, 8);
		_lblLog.Size      = new Vector2(730, 32);
		_lblLog.AddThemeColorOverride("font_color", Colors.Yellow);
		_lblLog.AddThemeFontSizeOverride("font_size", 15);
		canvas.AddChild(_lblLog);
	}

	// ── INVOCAR ──────────────────────────────────────────────────────────────

	private void InvocarAliado(int slot, int tropaIdx)
	{
		if (tropaIdx < 0 || tropaIdx >= EscenasTropas.Length) return;
		if (_slotsA[slot] == null) { Log("⚠ Slot aliado no encontrado en escena"); return; }
		LimpiarSlot(slot, false);

		var escena = GD.Load<PackedScene>(EscenasTropas[tropaIdx]);
		if (escena == null) { Log($"⚠ No se pudo cargar {EscenasTropas[tropaIdx]}"); return; }

		var tropa = escena.Instantiate() as TropaBase;
		if (tropa == null) { Log("⚠ La instancia no es TropaBase"); return; }

		AddChild(tropa);
		tropa.GlobalPosition = _slotsA[slot].GlobalPosition;
		tropa.AddToGroup("tropas_jugador");
		tropa.SetMeta("carril", NombresAliados[slot]);

		var marcador = new Node(); marcador.Name = "Ocupado";
		_slotsA[slot].AddChild(marcador);
		marcador.SetMeta("tropa_instanciada", tropa);

		_aliadas[slot] = tropa;
		Log($"✅ {NombresTropas[tropaIdx]} → Aliado Slot {slot + 1}");
	}

	private void InvocarEnemigo(int slot, int tropaIdx)
	{
		if (tropaIdx < 0 || tropaIdx >= EscenasTropas.Length) return;
		if (_slotsE[slot] == null) { Log("⚠ Slot enemigo no encontrado en escena"); return; }
		LimpiarSlot(slot, true);

		var escena = GD.Load<PackedScene>(EscenasTropas[tropaIdx]);
		if (escena == null) { Log($"⚠ No se pudo cargar {EscenasTropas[tropaIdx]}"); return; }

		var tropa = escena.Instantiate() as TropaBase;
		if (tropa == null) { Log("⚠ La instancia no es TropaBase"); return; }

		AddChild(tropa);
		tropa.GlobalPosition = _slotsE[slot].GlobalPosition;
		tropa.Scale          = new Vector2(-1, 1);
		tropa.AddToGroup("tropas_rival");
		tropa.SetMeta("carril", NombresEnemigos[slot]);

		var marcador = new Node(); marcador.Name = "Ocupado";
		_slotsE[slot].AddChild(marcador);
		marcador.SetMeta("tropa_instanciada", tropa);

		_enemigas[slot] = tropa;
		Log($"✅ {NombresTropas[tropaIdx]} → Enemigo Slot {slot + 1}");
	}

	private void LimpiarSlot(int slot, bool esEnemigo)
	{
		var tropa = esEnemigo ? _enemigas[slot] : _aliadas[slot];
		var sNode = esEnemigo ? _slotsE[slot]   : _slotsA[slot];

		if (tropa != null && IsInstanceValid(tropa)) tropa.QueueFree();
		sNode?.GetNodeOrNull("Ocupado")?.Free();

		if (esEnemigo) _enemigas[slot] = null;
		else           _aliadas[slot]  = null;
	}

	// ── COMBATE ───────────────────────────────────────────────────────────────

	private void Atacar()
	{
		int idxA = _optAtacante.Selected;
		int idxO = _optObjetivo.Selected;
		TropaBase atacante = idxA < 3 ? _aliadas[idxA]     : _enemigas[idxA - 3];
		TropaBase objetivo = idxO < 3 ? _aliadas[idxO]     : _enemigas[idxO - 3];

		if (atacante == null || !IsInstanceValid(atacante)) { Log("❌ Sin tropa atacante en ese slot"); return; }
		if (objetivo == null || !IsInstanceValid(objetivo)) { Log("❌ Sin tropa objetivo en ese slot"); return; }

		int daño = 0; try { daño = (int)atacante.Get("puntosAtaque"); } catch { }
		bool autogestionado = false;
		try { autogestionado = (bool)atacante.Call("AutogestionaDañoAtaque"); } catch { }

		atacante.Call("SetActivo", true);
		atacante.EjecutarAccion("atacar");

		// Tropas con ráfaga (p. ej. SoldadoCartoonPrime) aplican su propio daño por
		// fotograma dentro de EjecutarAccion("atacar"); aplicarlo aquí lo duplicaría.
		if (!autogestionado)
		{
			MostrarDaño(objetivo.GlobalPosition, daño);
			objetivo.Call("RecibirDaño", daño);
		}
		ManejarMuerte();
		Log(autogestionado
			? $"⚔  {TipoNombre(atacante)} dispara su ráfaga → {TipoNombre(objetivo)}"
			: $"⚔  {daño} daño de {TipoNombre(atacante)} → {TipoNombre(objetivo)}");
	}

	private void OrdenarDefensa(int idx)
	{
		TropaBase tropa = idx < 3 ? _aliadas[idx] : _enemigas[idx - 3];
		if (tropa == null || !IsInstanceValid(tropa)) { Log("❌ Slot vacío"); return; }
		tropa.EjecutarAccion("preparar_defensa");
		Log($"🛡 {TipoNombre(tropa)} en defensa");
	}

	// ── HABILIDADES ───────────────────────────────────────────────────────────

	private void UsarHabilidad(int slot, bool esEnemigo)
	{
		var tropa = esEnemigo ? _enemigas[slot] : _aliadas[slot];
		if (tropa == null || !IsInstanceValid(tropa)) { Log("❌ Slot vacío"); return; }
		tropa.Call("SetActivo", true);
		tropa.EjecutarAccion("usar_habilidad");
		ManejarMuerte();
		Log($"⚡ Habilidad: {TipoNombre(tropa)} ({(esEnemigo ? "Enemigo" : "Aliado")} {slot + 1})");
	}

	private void ResetearHabilidades()
	{
		for (int i = 0; i < 3; i++)
		{
			if (_aliadas[i]  != null && IsInstanceValid(_aliadas[i]))  try { _aliadas[i].Set("habilidadUsada", false);  } catch { }
			if (_enemigas[i] != null && IsInstanceValid(_enemigas[i])) try { _enemigas[i].Set("habilidadUsada", false); } catch { }
		}
		Log("♻  Habilidades reseteadas en todas las tropas");
	}

	// ── HECHIZOS ──────────────────────────────────────────────────────────────

	private TropaBase ObtenerObjetivoHechizo()
	{
		int idx = _optHechizo.Selected;
		return idx < 3 ? _aliadas[idx] : _enemigas[idx - 3];
	}

	private void UsarEncebollado()
	{
		var t = ObtenerObjetivoHechizo();
		if (t == null || !IsInstanceValid(t)) { Log("❌ Sin objetivo"); return; }
		int ata = Gi(t, "puntosAtaque"), esc = Gi(t, "escudoActual"), escMax = Gi(t, "escudoMaximo");
		try { t.Set("puntosAtaque", ata + 100); }                      catch { }
		try { t.Set("escudoActual", esc + 100); }                      catch { }
		try { t.Set("escudoMaximo", Mathf.Max(escMax, esc + 100)); }   catch { }
		MostrarDaño(t.GlobalPosition, 100, true);
		var tw = t.CreateTween();
		tw.TweenProperty(t, "modulate", new Color(1.6f, 1.3f, 0.2f), 0.2f);
		tw.TweenProperty(t, "modulate", Colors.White, 0.5f);
		Log($"🧅 Encebollado: +100 Atk/Esc en {TipoNombre(t)}");
	}

	private void UsarCuracion()
	{
		var t = ObtenerObjetivoHechizo();
		if (t == null || !IsInstanceValid(t)) { Log("❌ Sin objetivo"); return; }
		int vida = Gi(t, "vidaActual"), vidaMax = Gi(t, "vidaMaxima");
		int curado = Mathf.Min(200, vidaMax - vida);
		try { t.Set("vidaActual", vida + curado); } catch { }
		MostrarDaño(t.GlobalPosition, curado, true);
		var tw = t.CreateTween();
		tw.TweenProperty(t, "modulate", new Color(0.3f, 1.6f, 0.5f), 0.2f);
		tw.TweenProperty(t, "modulate", Colors.White, 0.5f);
		Log($"💚 Curación: +{curado} HP en {TipoNombre(t)}");
	}

	private void UsarVeneno()
	{
		var t = ObtenerObjetivoHechizo();
		if (t == null || !IsInstanceValid(t)) { Log("❌ Sin objetivo"); return; }
		t.SetMeta("envenenado",   true);
		t.SetMeta("dañoVeneno",   50);
		t.SetMeta("turnosVeneno", 3);
		t.Modulate = new Color(0.6f, 1f, 0.4f);
		// Tick inmediato de demostración
		MostrarDaño(t.GlobalPosition, 50);
		t.Call("RecibirDaño", 50);
		ManejarMuerte();
		Log($"☠  Veneno aplicado en {TipoNombre(t)} (50 dmg/turno x3)");
	}

	private void UsarBloqueo()
	{
		var t = ObtenerObjetivoHechizo();
		if (t == null || !IsInstanceValid(t)) { Log("❌ Sin objetivo"); return; }
		t.SetMeta("bloqueado",     true);
		t.SetMeta("turnosBloqueo", 2);
		t.Modulate = new Color(0.4f, 0.6f, 1.4f);
		Log($"🔒 Bloqueo aplicado en {TipoNombre(t)} (2 turnos)");
	}

	private void UsarRobo()
	{
		var t = ObtenerObjetivoHechizo();
		if (t == null || !IsInstanceValid(t)) { Log("❌ Sin objetivo"); return; }
		int atk = Gi(t, "puntosAtaque");
		try { t.Set("puntosAtaque", atk + 50); } catch { }
		Log($"🃏 Robo: +50 Atk en {TipoNombre(t)}");
	}

	// ── RESET / NAV ──────────────────────────────────────────────────────────

	private void ResetearCampo()
	{
		for (int i = 0; i < 3; i++) { LimpiarSlot(i, false); LimpiarSlot(i, true); }
		Log("🔄 Campo reseteado");
	}

	private void VolverAlMenu()
	{
		GetTree().ChangeSceneToFile("res://escenas/menu/menu_principal.tscn");
	}

	// ── MUERTE / SINCRONIZACIÓN ───────────────────────────────────────────────

	private void ManejarMuerte()
	{
		for (int i = 0; i < 3; i++)
		{
			VerificarMuerteTropa(ref _aliadas[i],  _slotsA[i], i, false);
			VerificarMuerteTropa(ref _enemigas[i], _slotsE[i], i, true);
		}
	}

	private void VerificarMuerteTropa(ref TropaBase tropa, Node2D slot, int idx, bool esEnemigo)
	{
		if (tropa == null) return;
		if (!IsInstanceValid(tropa)) { slot?.GetNodeOrNull("Ocupado")?.Free(); tropa = null; return; }
		if (Gi(tropa, "vidaActual") <= 0)
		{
			tropa.Call("ReproducirDerrota");
			var cap = tropa;
			GetTree().CreateTimer(1.2f).Timeout += () => { if (IsInstanceValid(cap)) cap.QueueFree(); };
			slot?.GetNodeOrNull("Ocupado")?.Free();
			Log($"💀 {TipoNombre(tropa)} eliminado ({(esEnemigo ? "Enemigo" : "Aliado")} {idx + 1})");
			tropa = null;
		}
	}

	private void SincronizarSlots()
	{
		for (int i = 0; i < 3; i++)
		{
			if (_aliadas[i]  != null && !IsInstanceValid(_aliadas[i]))  { _slotsA[i]?.GetNodeOrNull("Ocupado")?.Free(); _aliadas[i]  = null; }
			if (_enemigas[i] != null && !IsInstanceValid(_enemigas[i])) { _slotsE[i]?.GetNodeOrNull("Ocupado")?.Free(); _enemigas[i] = null; }
		}
	}

	// ── STATS ─────────────────────────────────────────────────────────────────

	private void ActualizarStats()
	{
		if (_lblStats == null) return;
		var sb = new System.Text.StringBuilder();

		sb.Append("ALIADOS:   ");
		for (int i = 0; i < 3; i++)
		{
			sb.Append($"[A{i + 1}] ");
			var t = _aliadas[i];
			if (t == null || !IsInstanceValid(t)) { sb.Append("vacío         "); continue; }
			sb.Append(FormatStats(t));
			sb.Append("   ");
		}

		sb.Append("\nENEMIGOS: ");
		for (int i = 0; i < 3; i++)
		{
			sb.Append($"[E{i + 1}] ");
			var t = _enemigas[i];
			if (t == null || !IsInstanceValid(t)) { sb.Append("vacío         "); continue; }
			sb.Append(FormatStats(t));
			sb.Append("   ");
		}

		_lblStats.Text = sb.ToString();
	}

	private string FormatStats(TropaBase t)
	{
		int vida    = Mathf.Max(0, Gi(t, "vidaActual")),  vidaMax  = Gi(t, "vidaMaxima");
		int esc     = Mathf.Max(0, Gi(t, "escudoActual")), escMax  = Gi(t, "escudoMaximo");
		int atk     = Gi(t, "puntosAtaque");
		bool habUsada = false; try { habUsada = (bool)t.Get("habilidadUsada"); } catch { }
		bool env    = t.HasMeta("envenenado") && (bool)t.GetMeta("envenenado");
		bool blo    = t.HasMeta("bloqueado")  && (bool)t.GetMeta("bloqueado");

		string nombre = t.GetType().Name.Replace("Prime", "");
		string estados = (env ? "☠" : "") + (blo ? "🔒" : "") + (habUsada ? "⚡" : "");
		return $"{nombre}  HP:{vida}/{vidaMax}  Esc:{esc}/{escMax}  Atk:{atk}{estados}";
	}

	// ── HELPERS VISUALES ──────────────────────────────────────────────────────

	private void MostrarDaño(Vector2 pos, int cantidad, bool curacion = false)
	{
		var lbl = new Label();
		lbl.Text = curacion ? $"+{cantidad}" : $"-{cantidad}";
		lbl.AddThemeColorOverride("font_color", curacion ? Colors.LightGreen : Colors.Red);
		lbl.AddThemeFontSizeOverride("font_size", 22);
		lbl.ZIndex          = 300;
		lbl.GlobalPosition  = pos + new Vector2(-20, -60);
		AddChild(lbl);
		var tw = CreateTween().SetParallel(true);
		tw.TweenProperty(lbl, "position:y", lbl.Position.Y - 55f, 0.85f);
		tw.TweenProperty(lbl, "modulate:a", 0.0f, 0.85f);
		tw.Finished += () => { if (IsInstanceValid(lbl)) lbl.QueueFree(); };
	}

	private void Log(string msg)
	{
		if (_lblLog == null) return;
		_lblLog.Text    = msg;
		_lblLog.Modulate = Colors.White;
		var tw = _lblLog.CreateTween();
		tw.TweenInterval(2.0f);
		tw.TweenProperty(_lblLog, "modulate:a", 0.0f, 0.5f);
		tw.Finished += () => { if (IsInstanceValid(_lblLog)) { _lblLog.Text = ""; _lblLog.Modulate = Colors.White; } };
	}

	// ── UI FACTORIES ──────────────────────────────────────────────────────────

	private Panel CrearPanel(float x, float y, float w, float h)
	{
		var p = new Panel();
		p.Position          = new Vector2(x, y);
		p.Size              = new Vector2(w, h);
		var style = new StyleBoxFlat();
		style.BgColor       = new Color(0.04f, 0.04f, 0.10f, 0.90f);
		style.BorderColor   = new Color(0.30f, 0.50f, 0.80f, 0.65f);
		style.SetBorderWidthAll(2);
		p.AddThemeStyleboxOverride("panel", style);
		return p;
	}

	private Label MkLabel(string texto, Color color, int size)
	{
		var l = new Label();
		l.Text = texto;
		l.AddThemeColorOverride("font_color", color);
		l.AddThemeFontSizeOverride("font_size", size);
		return l;
	}

	private Button MkBtn(string texto, Action onPress, Color? color = null)
	{
		var b = new Button();
		b.Text = texto;
		b.CustomMinimumSize = new Vector2(0, 26);
		b.AddThemeFontSizeOverride("font_size", 11);
		if (color.HasValue) b.AddThemeColorOverride("font_color", color.Value);
		b.Pressed += () => onPress();
		return b;
	}

	private void Separador(VBoxContainer vbox, string titulo, Color color)
	{
		vbox.AddChild(new HSeparator());
		var l = MkLabel($"── {titulo} ──", color, 11);
		l.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(l);
	}

	private OptionButton CrearOptionTropas()
	{
		var opt = new OptionButton();
		opt.CustomMinimumSize = new Vector2(255, 26);
		opt.AddThemeFontSizeOverride("font_size", 11);
		for (int i = 0; i < NombresTropas.Length; i++)
			opt.AddItem(NombresTropas[i]);
		return opt;
	}

	// ── UTILIDADES ────────────────────────────────────────────────────────────

	private int  Gi(Node2D n, string p) { try { return (int)n.Get(p); } catch { return 0; } }
	private string TipoNombre(Node2D n) => n?.GetType().Name.Replace("Prime", "") ?? "?";
}
