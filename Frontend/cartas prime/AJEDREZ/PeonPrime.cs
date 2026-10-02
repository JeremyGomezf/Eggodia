using Godot;
using System.Reflection;

/// <summary>
/// PeonPrime — Promoción de Ajedrez con Inteligencia Táctica para la IA
/// y actualización forzada de referencias internas.
/// </summary>
public partial class PeonPrime : TropaBase
{
	public override string Tipo => Tipos.NATURALEZA;
	protected override int TurnoDesbloqueoHabilidad => 3;

	private const string RUTA_TORRE   = "res://cartas prime/AJEDREZ/Torre_prime.tscn";
	private const string RUTA_DAMA    = "res://cartas prime/AJEDREZ/Dama_prime.tscn";
	private const string RUTA_CABALLO = "res://cartas prime/AJEDREZ/Caballo_prime.tscn";
	private const string RUTA_ARFIL   = "res://cartas prime/AJEDREZ/Arfil_prime.tscn";

	private CanvasLayer _uiPromocionLayer;

	public override void _Ready()
	{
		if (vidaMaxima == 0) 
		{ 
			vidaActual = vidaMaxima = 150;
			escudoActual = escudoMaximo = 150;
			puntosAtaque = 150;
		}
		base._Ready();
	}

	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada || _estaMuerto) return;

		// 🤖 Evaluar si esta tropa le pertenece a la IA/Rival (C# Godot 4 Variant fix)
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
			// 🧠 IA TÁCTICA: Decide la mejor pieza según el estado del tablero
			string seleccionIA = SeleccionarPromocionInteligenteIA();
			TransformarEnPieza(seleccionIA);
		}
		else
		{
			// 👤 Si es del jugador humano, despliega el panel de opciones
			CrearUIPromocion();
		}
	}

	// ── LÓGICA DE DECISIÓN INTELIGENTE PARA LA IA ─────────────────────────────
	private string SeleccionarPromocionInteligenteIA()
	{
		// 1. Evaluar si la tropa está muy lastimada (Necesita tanque/resistencia)
		float porcentajeVida = (float)vidaActual / Mathf.Max(1, vidaMaxima);
		if (porcentajeVida <= 0.4f)
		{
			// Si tiene 40% de vida o menos, promociona a Torre para absorber daño
			return RUTA_TORRE;
		}

		// 2. Verificar presencia de enemigos en su carril
		bool enemigoEnCarril = HayEnemigoEnMiCarril();
		if (enemigoEnCarril)
		{
			// Si tiene a alguien de frente, saca a la Dama para reventarlo con máximo daño
			return RUTA_DAMA;
		}

		// 3. Verificar si está en el carril 2 (Centro) y le conviene Caballo para saltar a las esquinas
		if (HasMeta("carril"))
		{
			string miCarril = ((string)GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			if (miCarril == "2")
			{
				// Si no hay nadie en el centro pero hay enemigos en carriles 1 o 3, el Caballo es ideal
				return RUTA_CABALLO;
			}
		}

		// 4. Decisión por defecto en situaciones neutras: Dama o Arfil
		return (GD.Randi() % 2 == 0) ? RUTA_DAMA : RUTA_ARFIL;
	}

	private bool HayEnemigoEnMiCarril()
	{
		if (!HasMeta("carril")) return false;

		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		string miCarril = ((string)GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();

		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (n is Node2D e && IsInstanceValid(e) && e.HasMeta("carril"))
			{
				string c = ((string)e.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
				if (c == miCarril)
				{
					int v = 0;
					try { v = (int)e.Get("vidaActual"); } catch { }
					if (v > 0) return true;
				}
			}
		}
		return false;
	}

	// ── INTERFAZ JUGADOR ──────────────────────────────────────────────────────
	private void CrearUIPromocion()
	{
		if (_uiPromocionLayer != null && IsInstanceValid(_uiPromocionLayer))
		{
			_uiPromocionLayer.QueueFree();
		}

		_uiPromocionLayer = new CanvasLayer();
		AddChild(_uiPromocionLayer);

		PanelContainer panel = new PanelContainer();
		_uiPromocionLayer.AddChild(panel);

		StyleBoxFlat estiloPanel = new StyleBoxFlat();
		estiloPanel.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.92f);
		estiloPanel.CornerRadiusTopLeft = 8;
		estiloPanel.CornerRadiusTopRight = 8;
		estiloPanel.CornerRadiusBottomLeft = 8;
		estiloPanel.CornerRadiusBottomRight = 8;
		estiloPanel.ContentMarginLeft = 18;
		estiloPanel.ContentMarginRight = 18;
		estiloPanel.ContentMarginTop = 14;
		estiloPanel.ContentMarginBottom = 14;
		estiloPanel.BorderWidthLeft = 2;
		estiloPanel.BorderWidthRight = 2;
		estiloPanel.BorderWidthTop = 2;
		estiloPanel.BorderWidthBottom = 2;
		estiloPanel.BorderColor = new Color(1.0f, 0.84f, 0.0f);
		panel.AddThemeStyleboxOverride("panel", estiloPanel);

		VBoxContainer vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 10);
		panel.AddChild(vbox);

		Label titulo = new Label();
		titulo.Text = "PROMOCIÓN"; // sin emojis, a pedido
		titulo.HorizontalAlignment = HorizontalAlignment.Center;
		titulo.AddThemeColorOverride("font_color", new Color(1.0f, 0.84f, 0.0f));
		titulo.AddThemeFontSizeOverride("font_size", 28); // un poco más chico que antes (era 34)
		vbox.AddChild(titulo);

		Button btnTorre   = CrearBotonOpcion("TORRE", RUTA_TORRE);
		Button btnDama    = CrearBotonOpcion("DAMA", RUTA_DAMA);
		Button btnCaballo = CrearBotonOpcion("CABALLO", RUTA_CABALLO);
		Button btnArfil   = CrearBotonOpcion("ARFIL", RUTA_ARFIL);

		vbox.AddChild(btnTorre);
		vbox.AddChild(btnDama);
		vbox.AddChild(btnCaballo);
		vbox.AddChild(btnArfil);

		// Tamaño REAL calculado a mano (no un offset a ciegas): con estos márgenes/separaciones/
		// botones, el panel mide 296×360. Antes se posicionaba con un offset fijo (80,-50) desde el
		// Peón sin mirar el alto real del panel ni el borde de la pantalla, así que con un Peón en la
		// mitad inferior del tablero el panel se iba de largo por abajo (se veía cortado/afuera).
		const float PANEL_ANCHO = 296f;
		const float PANEL_ALTO  = 360f;
		const float MARGEN_PANTALLA = 16f;
		const float OFFSET_ADELANTE = 70f; // "adelante" del Peón, no encima

		Vector2 posPeon = GetGlobalTransformWithCanvas().Origin;
		Vector2 pantalla = GetViewport().GetVisibleRect().Size;

		float x = posPeon.X + OFFSET_ADELANTE;
		float y = posPeon.Y - PANEL_ALTO / 2f; // centrado verticalmente respecto al Peón

		// Recorte a los bordes reales de la pantalla: nunca puede quedar ni abajo, ni arriba, ni a
		// los costados. Como resultado, siempre queda pegado al Peón o, si no entra a su derecha
		// (Peón cerca del borde), se acomoda solo hacia adentro de la pantalla.
		x = Mathf.Clamp(x, MARGEN_PANTALLA, pantalla.X - PANEL_ANCHO - MARGEN_PANTALLA);
		y = Mathf.Clamp(y, MARGEN_PANTALLA, pantalla.Y - PANEL_ALTO - MARGEN_PANTALLA);

		panel.Position = new Vector2(x, y);
	}

	private Button CrearBotonOpcion(string texto, string rutaEscena)
	{
		Button btn = new Button();
		btn.Text = texto;
		btn.CustomMinimumSize = new Vector2(260, 64); // un poco más chico que antes (era 300x74), sigue cómodo al dedo
		btn.AddThemeFontSizeOverride("font_size", 26);
		btn.Pressed += () => { TransformarEnPieza(rutaEscena); };
		return btn;
	}

	// ── TRANSFORMACIÓN ────────────────────────────────────────────────────────
	// El panel de promoción abierto = habilidad todavía sin concretar. Si el turno se va sin elegir
	// pieza, no se gasta ni la habilidad ni el movimiento (se cierra el panel y listo).
	public override bool SeleccionPendiente =>
		_uiPromocionLayer != null && IsInstanceValid(_uiPromocionLayer) && !habilidadUsada;

	public override void CancelarSeleccionPendiente()
	{
		if (_uiPromocionLayer != null && IsInstanceValid(_uiPromocionLayer)) _uiPromocionLayer.QueueFree();
		_uiPromocionLayer = null;
	}

	private void TransformarEnPieza(string rutaEscena)
	{
		bool eraPendiente = SeleccionPendiente;
		habilidadUsada = true;
		if (eraPendiente) AvisarHabilidadConfirmada(); // elegiste pieza: recién acá se cobra el movimiento

		PackedScene escenaNueva = GD.Load<PackedScene>(rutaEscena);
		if (escenaNueva == null) return;

		Node2D nuevaTropa = escenaNueva.Instantiate<Node2D>();

		// Centro de colisión, no el origen crudo: la pieza nueva puede tener un
		// CollisionShape2D en una posición local distinta a la del Peón.
		if (nuevaTropa is TropaBase nuevaTB) nuevaTB.ColocarPorCentroColision(PosicionCentroColision);
		else                                 nuevaTropa.GlobalPosition = GlobalPosition;
		// La pieza promovida hereda el Z-Index exacto del carril del Peón (profundidad visual
		// por carril: Mod1/ModRival1=1, Mod2/ModRival2=5, Mod3/ModRival3=10), en vez de quedarse
		// con el ZIndex por defecto de su propia escena.
		nuevaTropa.ZIndex = ZIndex;

		// 🔄 PRESERVAR LA ORIENTACIÓN Y SENTIDO (Rival vs Jugador)
		// Evaluamos el bando una sola vez para no duplicar variables
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

		// 1. Orientación: usar AsegurarOrientacionRival o forzar volteo horizontal
		Node campo = ObtenerEscenaCampoActual();
		if (esRivalTropa)
		{
			if (campo != null && campo.HasMethod("AsegurarOrientacionRival"))
			{
				campo.Call("AsegurarOrientacionRival", nuevaTropa);
			}
			else
			{
				// Volteo de respaldo si el campo no responde
				var animSprite = nuevaTropa.FindChild("*Sprite*", true, false);
				if (animSprite is AnimatedSprite2D aSprite) aSprite.FlipH = true;
				else if (animSprite is Sprite2D sSprite) sSprite.FlipH = true;
				else nuevaTropa.Scale = new Vector2(-Mathf.Abs(nuevaTropa.Scale.X), nuevaTropa.Scale.Y);
			}
		}
		
		// 3. Transferir los metadatos de la casilla (carril, zona, idx_mazo, estados de hechizo...).
		// NO se copian las marcas de muerte: son del Peon que esta desapareciendo, no de la pieza que
		// nace. "muerte_nuclear" haria que la pieza nueva muera en polvo (efecto exclusivo de la bomba)
		// la proxima vez que la maten, y "muerte_procesada" haria que su muerte se ignore y se quede
		// clavada en el carril.
		foreach (string meta in GetMetaList())
		{
			if (meta == "muerte_nuclear" || meta == "muerte_procesada") continue;
			nuevaTropa.SetMeta(meta, GetMeta(meta));
		}

		// 3b. Re-apuntar el marcador "Ocupado" de la zona a la pieza nueva. Sin este paso, la
		// zona sigue referenciando al Peón (a punto de destruirse) y el carril puede aparecer
		// vacío o bloqueado para acciones futuras (Enroque, invocación, castigo por carril vacío).
		if (HasMeta("carril"))
		{
			string carrilActual = (string)GetMeta("carril");
			Node2D zonaActual = GetTree().Root.FindChild(carrilActual, true, false) as Node2D;
			Node ocupadoActual = zonaActual?.GetNodeOrNull("Ocupado");
			if (ocupadoActual != null) ocupadoActual.SetMeta("tropa_instanciada", nuevaTropa);
		}

		Node padre = GetParent();
		int indiceMiNodo = GetIndex();

		string nombreOriginal = Name;
		Name = nombreOriginal + "_old";
		nuevaTropa.Name = nombreOriginal;

		padre.AddChild(nuevaTropa);
		padre.MoveChild(nuevaTropa, indiceMiNodo);

		// 4. Conservar el bando exacto (Jugador o Rival)
		if (IsInGroup("tropas_jugador")) nuevaTropa.AddToGroup("tropas_jugador");
		if (IsInGroup("tropas_rival"))   nuevaTropa.AddToGroup("tropas_rival");

		try
		{
			Variant valRival = Get("esRival");
			if (valRival.VariantType != Variant.Type.Nil)
			{
				nuevaTropa.Set("esRival", valRival);
			}
		}
		catch { }

		// Súper Buff (+150)
		try
		{
			int vMax = (int)nuevaTropa.Get("vidaMaxima") + 150;
			int eMax = (int)nuevaTropa.Get("escudoMaximo") + 150;
			int atk  = (int)nuevaTropa.Get("puntosAtaque") + 150;

			nuevaTropa.Set("vidaMaxima", vMax);
			nuevaTropa.Set("vidaActual", vMax);
			nuevaTropa.Set("escudoMaximo", eMax);
			nuevaTropa.Set("escudoActual", eMax);
			nuevaTropa.Set("puntosAtaque", atk);
		}
		catch { }

		// Mismo cierre que una invocacion normal (ver Campo1.Flujo.TropaInvocada): la pieza nace
		// visible y ACTIVA, para que pueda usarse igual que cualquier tropa recien puesta. Sin el
		// SetActivo quedaba con las barras de vida/escudo visibles de entrada, porque es ese metodo
		// el que las oculta al activar.
		nuevaTropa.Visible = true;
		if (nuevaTropa.HasMethod("SetActivo")) nuevaTropa.Call("SetActivo", true);

		// Efecto visual — bloquea el tablero mientras dura, para que la IA no dispare otra
		// acción encima de la transformación todavía en curso.
		campo?.Call("IniciarBloqueoTablero");
		Tween twDestello = nuevaTropa.CreateTween();
		twDestello.TweenProperty(TropaBase.NodoParaTinte(nuevaTropa), "modulate", new Color(2.0f, 1.8f, 0.5f), 0.2f);
		twDestello.TweenProperty(TropaBase.NodoParaTinte(nuevaTropa), "modulate", Colors.White, 0.4f);
		twDestello.Finished += () => campo?.Call("FinalizarBloqueoTablero");

		// 🚨 REEVALUACIÓN AUTOMÁTICA DE VARIABLES EN EL SCRIPT DEL CAMPO Y DEL PADRE
		if (padre != null)
		{
			ReemplazarReferenciaEnCampo(padre, this, nuevaTropa);
		}

		if (campo != null)
		{
			ReemplazarReferenciaEnCampo(campo, this, nuevaTropa);

			if (campo.HasMethod("ActualizarTextoConsola")) campo.Call("ActualizarTextoConsola");
			if (campo.HasMethod("ActualizarInformacionSlot")) campo.Call("ActualizarInformacionSlot");
			if (campo.HasMethod("RefrescarUiCompleta")) campo.Call("RefrescarUiCompleta");
			if (campo.HasMethod("CargarTropasExistentes")) campo.Call("CargarTropasExistentes");
		}

		if (_uiPromocionLayer != null && IsInstanceValid(_uiPromocionLayer))
		{
			_uiPromocionLayer.QueueFree();
		}

		QueueFree();
	}

	/// <summary>
	/// Escanea dinámicamente las variables de C# en el Campo y sustituye las referencias viejas
	/// del Peón por la nueva pieza promocionada.
	/// </summary>
	private void ReemplazarReferenciaEnCampo(Node campo, Node2D viejo, Node2D nuevo)
	{
		var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
		var campos = campo.GetType().GetFields(flags);

		foreach (var f in campos)
		{
			try
			{
				var val = f.GetValue(campo);
				if (val == (object)viejo)
				{
					f.SetValue(campo, nuevo);
				}
			}
			catch { }
		}
	}

	private Node ObtenerEscenaCampoActual()
	{
		Node currentScene = GetTree().CurrentScene;
		if (currentScene != null) return currentScene;

		Node root = GetTree().Root;
		Node cp = root.FindChild("campo_pruebas", true, false);
		if (cp != null) return cp;

		Node c1 = root.FindChild("campo_1", true, false);
		if (c1 != null) return c1;

		return null;
	}
}
