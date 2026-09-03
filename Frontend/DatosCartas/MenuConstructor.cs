using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// MenuConstructor — Selector interactivo y animado de cartas / constructor de mazo.
/// Incluye pestañas Tropas/Ardid, buscador, previsualización con sprites animados,
/// cofre del mazo con 8 ranuras, ficha ornamental de detalles y ventana de ayuda.
/// </summary>
public partial class MenuConstructor : Control
{
	[ExportGroup("Rutas de Escenas")]
	[Export] public string RutaBatalla = "res://escenas/gameplay/campo_1.tscn";
	[Export] public string RutaMenu    = "res://escenas/menu/menu_principal.tscn";
	[Export] private PackedScene _escenaCartaMini;

	[ExportGroup("Texturas y Assets")]
	[Export] private Texture2D _texTropaSelector;
	[Export] private Texture2D _texArdidSelector;
	[Export] private Texture2D _texTropaMazo;
	[Export] private Texture2D _texArdidMazo;

	[ExportGroup("Referencias Selector")]
	[Export] private TextureRect _rectSelectorBg;
	[Export] private Button _btnTabTropas;
	[Export] private Button _btnTabArdid;
	[Export] private GridContainer _gridSelector;
	[Export] private LineEdit _txtBuscador;

	[ExportGroup("Referencias Showcase Central")]
	[Export] private Label _lblShowcaseNombre;
	[Export] private Control _containerSpriteCenter;
	[Export] private TextureRect _fallbackTextureCenter;
	[Export] private Label _lblTituloMazo;
	[Export] private TextureRect _rectMazoBg;
	[Export] private GridContainer _gridMazoSlots;
	[Export] private Button _btnBatallar;
	[Export] private Button _btnVolver;
	[Export] private Label _lblContadorMazo;

	[ExportGroup("Referencias Ficha Datos Derecha")]
	[Export] private Label _lblDetalleTitulo;
	[Export] private Label _lblDetalleHp;
	[Export] private Label _lblDetalleAtk;
	[Export] private Label _lblDetalleDef;
	[Export] private Label _lblDetalleCosto;
	[Export] private Label _lblDetalleElemento;
	[Export] private Label _lblDetalleHabilidad;
	[Export] private Label _lblDetalleExtra;

	[ExportGroup("Ayuda / Modal")]
	[Export] private BaseButton _btnDuda;
	[Export] private Control _modalAyuda;
	[Export] private Button _btnCerrarAyuda;

	private const int MIN_CARTAS = 8;
	private const int MAX_CARTAS = 8;
	private const int MAX_ARDIDES = 6;

	// Composición obligatoria del mazo: 3 tácticos + 3 asesinos + 2 colosos = 8
	private const int MAX_TACTICOS = 3;
	private const int MAX_ASESINOS = 3;
	private const int MAX_COLOSOS  = 2;

	private readonly List<CartaData> _todasLasCartas = new();
	private readonly List<CartaData> _cartasEnMazo = new();
	private readonly List<CartaData> _cartasArdidEnMazo = new();
	private string _pestanaActual = "TROPAS"; // "TROPAS" o "ARDID"
	private CartaData _cartaSeleccionada = null;
	private AnimatedSprite2D _spriteAnimadoActual = null;
	private Tween _idleTween = null;
	private bool _estaAtacando = false;

	public override void _Ready()
	{
		CargarTexturasPorDefecto();

		if (_btnTabTropas != null) _btnTabTropas.Pressed += () => CambiarPestana("TROPAS");
		if (_btnTabArdid != null) _btnTabArdid.Pressed += () => CambiarPestana("ARDID");

		if (_txtBuscador != null)
		{
			_txtBuscador.TextChanged += (txt) => FiltrarCartas(txt);
		}

		if (_btnBatallar != null) _btnBatallar.Pressed += IrABatalla;
		if (_btnVolver != null) _btnVolver.Pressed += VolverAlMenu;

		if (_btnDuda != null) _btnDuda.Pressed += AbrirAyuda;
		if (_btnCerrarAyuda != null) _btnCerrarAyuda.Pressed += CerrarAyuda;

		if (_containerSpriteCenter != null)
		{
			_containerSpriteCenter.GuiInput += (ev) =>
			{
				if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
				{
					EjecutarAnimacionAtaqueShowcase();
				}
			};
		}

		CargarCartasDesdeDisco();
		CambiarPestana("TROPAS");

		if (_todasLasCartas.Count > 0)
		{
			var primeraTropa = _todasLasCartas.Find(c => c.Categoria == CategoriaCarta.Unidad) ?? _todasLasCartas[0];
			SeleccionarCarta(primeraTropa);
		}
	}

	private void CargarTexturasPorDefecto()
	{
		_texTropaSelector ??= ResourceLoader.Load<Texture2D>("res://imagenes/MenuConstructor/Tropa_Selector.png");
		_texArdidSelector ??= ResourceLoader.Load<Texture2D>("res://imagenes/MenuConstructor/Ardid_Selector.png");
		_texTropaMazo ??= ResourceLoader.Load<Texture2D>("res://imagenes/MenuConstructor/Tropa_Mazo.png");
		_texArdidMazo ??= ResourceLoader.Load<Texture2D>("res://imagenes/MenuConstructor/Ardid_Mazo.png");
		_escenaCartaMini ??= ResourceLoader.Load<PackedScene>("res://DatosCartas/CartaMini.tscn");
	}

	private static readonly string[] OrdenTropas = new string[]
	{
		"Soldado Real", "Maguin", "Golem Pedregal",
		"Dragon de Flama", "Tiburon", "Calamar Gigante",
		"Peon", "Torre", "Caballo",
		"Arfil", "Dama", "Paperex",
		"Soldado Cartoon", "Campero", "Granadero",
		"Tanque", "Kabar"
	};

	private static string NormalizarTexto(string texto)
	{
		if (string.IsNullOrEmpty(texto)) return "";
		return texto.ToLowerInvariant()
			.Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u");
	}

	private static int ObtenerPrioridadTropa(string nombre)
	{
		string norm = NormalizarTexto(nombre);
		for (int i = 0; i < OrdenTropas.Length; i++)
		{
			string clave = NormalizarTexto(OrdenTropas[i]);
			if (norm.Contains(clave) || clave.Contains(norm))
			{
				return i;
			}
		}
		return 999;
	}

	private void CargarCartasDesdeDisco()
	{
		_todasLasCartas.Clear();
		string rutaCarpeta = "res://DatosCartas/";
		using var dir = DirAccess.Open(rutaCarpeta);
		if (dir != null)
		{
			dir.ListDirBegin();
			string archivo = dir.GetNext();
			while (!string.IsNullOrEmpty(archivo))
			{
				if (!dir.CurrentIsDir() && (archivo.EndsWith(".tres") || archivo.EndsWith(".tres.remap")))
				{
					string real = archivo.Replace(".remap", "");
					var recurso = ResourceLoader.Load(rutaCarpeta + real) as CartaData;
					if (recurso != null)
					{
						// Encebollado queda excluido del selector de mazo (petición del diseño)
						if (NormalizarTexto(recurso.Nombre).Contains("encebollado"))
						{
							archivo = dir.GetNext();
							continue;
						}
						bool existe = false;
						foreach (var c in _todasLasCartas)
						{
							if (c.Nombre.Equals(recurso.Nombre, StringComparison.OrdinalIgnoreCase))
							{
								existe = true;
								break;
							}
						}
						if (!existe) _todasLasCartas.Add(recurso);
					}
				}
				archivo = dir.GetNext();
			}
		}

		_todasLasCartas.Sort((a, b) =>
		{
			int prioA = ObtenerPrioridadTropa(a.Nombre);
			int prioB = ObtenerPrioridadTropa(b.Nombre);
			if (prioA != prioB) return prioA.CompareTo(prioB);
			return string.Compare(a.Nombre, b.Nombre, StringComparison.OrdinalIgnoreCase);
		});
	}

	public void CambiarPestana(string nuevaPestana)
	{
		_pestanaActual = nuevaPestana.ToUpper();

		if (_lblTituloMazo != null) _lblTituloMazo.Visible = false;

		var mazoContainer = _gridMazoSlots?.GetParent<Control>();

		if (_pestanaActual == "TROPAS")
		{
			if (_rectSelectorBg != null && _texTropaSelector != null)
				_rectSelectorBg.Texture = _texTropaSelector;

			if (_rectMazoBg != null && _texTropaMazo != null)
				_rectMazoBg.Texture = _texTropaMazo;

			if (mazoContainer != null)
			{
				mazoContainer.Position = new Vector2(615, 460);
				mazoContainer.Size = new Vector2(690, 425);
			}

			if (_gridMazoSlots != null)
			{
				_gridMazoSlots.Columns = 4;
				_gridMazoSlots.AnchorLeft = 0.5f;
				_gridMazoSlots.AnchorRight = 0.5f;
				_gridMazoSlots.AnchorTop = 0.5f;
				_gridMazoSlots.AnchorBottom = 0.5f;
				_gridMazoSlots.OffsetLeft = -290.0f;
				_gridMazoSlots.OffsetRight = 290.0f;
				_gridMazoSlots.OffsetTop = -140.0f;
				_gridMazoSlots.OffsetBottom = 140.0f;
				_gridMazoSlots.AddThemeConstantOverride("h_separation", 18);
				_gridMazoSlots.AddThemeConstantOverride("v_separation", 14);
			}
		}
		else
		{
			if (_rectSelectorBg != null && _texArdidSelector != null)
				_rectSelectorBg.Texture = _texArdidSelector;

			if (_rectMazoBg != null && _texArdidMazo != null)
				_rectMazoBg.Texture = _texArdidMazo;

			if (mazoContainer != null)
			{
				mazoContainer.Position = new Vector2(615, 380);
				mazoContainer.Size = new Vector2(690, 490);
			}

			if (_gridMazoSlots != null)
			{
				_gridMazoSlots.Columns = 3;
				_gridMazoSlots.AnchorLeft = 0.5f;
				_gridMazoSlots.AnchorRight = 0.5f;
				_gridMazoSlots.AnchorTop = 0.5f;
				_gridMazoSlots.AnchorBottom = 0.5f;
				_gridMazoSlots.OffsetLeft = -290.0f;
				_gridMazoSlots.OffsetRight = 290.0f;
				_gridMazoSlots.OffsetTop = -210.0f;
				_gridMazoSlots.OffsetBottom = 210.0f;
				_gridMazoSlots.AddThemeConstantOverride("h_separation", 18);
				_gridMazoSlots.AddThemeConstantOverride("v_separation", 16);
			}
		}

		if (_rectSelectorBg != null)
		{
			var tw = _rectSelectorBg.CreateTween();
			_rectSelectorBg.Scale = new Vector2(0.98f, 0.98f);
			tw.TweenProperty(_rectSelectorBg, "scale", Vector2.One, 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}

		CrearRanurasMazo();
		ActualizarMazoVisual();
		PoblarSelector();

		// Sincronizar selección con la pestaña activa
		bool esTropaTab = (_pestanaActual == "TROPAS");
		if (_cartaSeleccionada == null || 
		    (esTropaTab && _cartaSeleccionada.Categoria != CategoriaCarta.Unidad) ||
		    (!esTropaTab && _cartaSeleccionada.Categoria == CategoriaCarta.Unidad))
		{
			var primeraDePestana = _todasLasCartas.Find(c => esTropaTab 
				? c.Categoria == CategoriaCarta.Unidad 
				: c.Categoria != CategoriaCarta.Unidad);

			if (primeraDePestana != null)
			{
				SeleccionarCarta(primeraDePestana);
			}
		}
	}

	private void PoblarSelector()
	{
		if (_gridSelector == null || _escenaCartaMini == null) return;

		foreach (Node n in _gridSelector.GetChildren())
		{
			n.QueueFree();
		}

		string textoFiltro = _txtBuscador != null ? _txtBuscador.Text.Trim().ToLower() : "";
		int indice = 0;

		foreach (var datos in _todasLasCartas)
		{
			if (datos == null) continue;

			bool esTropa = (datos.Categoria == CategoriaCarta.Unidad);
			if (_pestanaActual == "TROPAS" && !esTropa) continue;
			if (_pestanaActual == "ARDID" && esTropa) continue;

			if (!ClasificacionCartas.CoincideBusqueda(datos.RutaEscena, datos.Nombre, textoFiltro))
				continue;

			var mini = _escenaCartaMini.Instantiate<CartaMini>();
			_gridSelector.AddChild(mini);
			mini.CargarDatos(datos);
			mini.SetModoMazo(false);

			mini.Modulate = new Color(1, 1, 1, 0);
			mini.Scale = new Vector2(0.7f, 0.7f);
			mini.PivotOffset = new Vector2(55f, 72f);

			var tw = mini.CreateTween();
			float delay = indice * 0.025f;
			tw.TweenInterval(delay);
			tw.TweenProperty(mini, "modulate", Colors.White, 0.12f);
			tw.Parallel().TweenProperty(mini, "scale", Vector2.One, 0.18f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

			mini.OnClickeada += (c) =>
			{
				SeleccionarCarta(c.MisDatos);
				AgregarAlMazo(c.MisDatos);
			};

			indice++;
		}
	}

	private void FiltrarCartas(string query)
	{
		foreach (Node node in _gridSelector.GetChildren())
		{
			if (node is CartaMini mini && mini.MisDatos != null)
			{
				mini.Visible = ClasificacionCartas.CoincideBusqueda(mini.MisDatos.RutaEscena, mini.MisDatos.Nombre, query);
			}
		}
	}

	private void SeleccionarCarta(CartaData datos)
	{
		if (datos == null) return;
		_cartaSeleccionada = datos;

		if (_lblShowcaseNombre != null)
		{
			_lblShowcaseNombre.Text = datos.Nombre.ToUpper();
			_lblShowcaseNombre.PivotOffset = _lblShowcaseNombre.Size / 2f;
			_lblShowcaseNombre.Scale = new Vector2(1.1f, 1.1f);
			var tw = _lblShowcaseNombre.CreateTween();
			tw.TweenProperty(_lblShowcaseNombre, "scale", Vector2.One, 0.18f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}

		ActualizarShowcaseAnimado(datos);
		ActualizarFichaDetalles(datos);
	}

	private void ActualizarShowcaseAnimado(CartaData datos)
	{
		if (_containerSpriteCenter == null) return;

		_idleTween?.Kill();
		_estaAtacando = false;

		if (_spriteAnimadoActual != null && IsInstanceValid(_spriteAnimadoActual))
		{
			_spriteAnimadoActual.QueueFree();
			_spriteAnimadoActual = null;
		}

		bool tieneSpriteAnimado = false;

		if (!string.IsNullOrEmpty(datos.RutaEscena) && ResourceLoader.Exists(datos.RutaEscena))
		{
			try
			{
				var packed = ResourceLoader.Load<PackedScene>(datos.RutaEscena);
				if (packed != null)
				{
					var instancia = packed.Instantiate();
					AnimatedSprite2D animEncontrado = null;

					if (instancia is AnimatedSprite2D a) animEncontrado = a;
					else animEncontrado = instancia.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D")
										  ?? BuscarNodoRecursivo<AnimatedSprite2D>(instancia);

					if (animEncontrado != null && animEncontrado.SpriteFrames != null)
					{
						_spriteAnimadoActual = new AnimatedSprite2D();
						_spriteAnimadoActual.SpriteFrames = animEncontrado.SpriteFrames;
						_spriteAnimadoActual.Position = new Vector2(0, 0);
						_spriteAnimadoActual.Scale = new Vector2(0.55f, 0.55f);

						_containerSpriteCenter.AddChild(_spriteAnimadoActual);

						// Buscar la mejor animación de reposo disponible
						string[] posiblesIdles = { "idle", "idle 1", "idle_fantasma", "idle1", "reposo", "quieto" };
						string animIdleElegida = null;
						foreach (var nombreIdle in posiblesIdles)
						{
							if (_spriteAnimadoActual.SpriteFrames.HasAnimation(nombreIdle))
							{
								animIdleElegida = nombreIdle;
								break;
							}
						}

						if (animIdleElegida != null)
						{
							_spriteAnimadoActual.Play(animIdleElegida);
						}
						else if (_spriteAnimadoActual.SpriteFrames.GetAnimationNames().Length > 0)
						{
							_spriteAnimadoActual.Play(_spriteAnimadoActual.SpriteFrames.GetAnimationNames()[0]);
						}

						tieneSpriteAnimado = true;
					}
					instancia.QueueFree();
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[MenuConstructor] Error al cargar sprite animado de {datos.Nombre}: {ex.Message}");
			}
		}

		if (_fallbackTextureCenter != null)
		{
			_fallbackTextureCenter.Visible = !tieneSpriteAnimado;
			if (!tieneSpriteAnimado)
			{
				_fallbackTextureCenter.Texture = datos.Imagen;
			}
		}

		if (_spriteAnimadoActual != null && IsInstanceValid(_spriteAnimadoActual))
		{
			Vector2 posBase = _spriteAnimadoActual.Position;
			_idleTween = CreateTween().SetLoops();
			_idleTween.TweenProperty(_spriteAnimadoActual, "position:y", posBase.Y - 8f, 1.2f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			_idleTween.TweenProperty(_spriteAnimadoActual, "position:y", posBase.Y, 1.2f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		}
		else if (_fallbackTextureCenter != null && _fallbackTextureCenter.Visible)
		{
			_fallbackTextureCenter.AnchorLeft = 0.5f;
			_fallbackTextureCenter.AnchorRight = 0.5f;
			_fallbackTextureCenter.AnchorTop = 0.5f;
			_fallbackTextureCenter.AnchorBottom = 0.5f;
			_fallbackTextureCenter.OffsetLeft = -90.0f;
			_fallbackTextureCenter.OffsetRight = 90.0f;
			_fallbackTextureCenter.OffsetTop = -60.0f;
			_fallbackTextureCenter.OffsetBottom = 160.0f;

			Vector2 posBase = _fallbackTextureCenter.Position;
			_idleTween = CreateTween().SetLoops();
			_idleTween.TweenProperty(_fallbackTextureCenter, "position:y", posBase.Y - 6f, 1.2f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			_idleTween.TweenProperty(_fallbackTextureCenter, "position:y", posBase.Y, 1.2f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		}
	}

	public void EjecutarAnimacionAtaqueShowcase()
	{
		if (_estaAtacando) return;

		if (_spriteAnimadoActual != null && IsInstanceValid(_spriteAnimadoActual))
		{
			string[] posiblesAtaques = { "ataque", "ataque 1", "ataque_fantasma", "habilidad", "habilidad 1", "daño", "daño 1", "defensa" };
			string animAtaque = null;
			foreach (var nombreAtk in posiblesAtaques)
			{
				if (_spriteAnimadoActual.SpriteFrames.HasAnimation(nombreAtk))
				{
					animAtaque = nombreAtk;
					break;
				}
			}

			if (animAtaque != null)
			{
				_estaAtacando = true;
				_spriteAnimadoActual.Play(animAtaque);

				GlobalAudioManager.Instance?.PlayClickSound();

				var tw = CreateTween();
				tw.TweenProperty(_spriteAnimadoActual, "scale", new Vector2(0.65f, 0.65f), 0.1f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
				tw.TweenProperty(_spriteAnimadoActual, "scale", new Vector2(0.55f, 0.55f), 0.15f);

				_spriteAnimadoActual.AnimationFinished += VolverAIdle;
			}
		}
		else if (_fallbackTextureCenter != null && _fallbackTextureCenter.Visible)
		{
			_estaAtacando = true;
			var tw = CreateTween();
			tw.TweenProperty(_fallbackTextureCenter, "scale", new Vector2(1.12f, 1.12f), 0.1f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			tw.TweenProperty(_fallbackTextureCenter, "scale", Vector2.One, 0.15f);
			tw.TweenCallback(Callable.From(() => _estaAtacando = false));
		}
	}

	private void VolverAIdle()
	{
		if (_spriteAnimadoActual != null && IsInstanceValid(_spriteAnimadoActual))
		{
			_spriteAnimadoActual.AnimationFinished -= VolverAIdle;
			string[] posiblesIdles = { "idle", "idle 1", "idle_fantasma", "idle1", "reposo" };
			foreach (var nombreIdle in posiblesIdles)
			{
				if (_spriteAnimadoActual.SpriteFrames.HasAnimation(nombreIdle))
				{
					_spriteAnimadoActual.Play(nombreIdle);
					break;
				}
			}
			_estaAtacando = false;
		}
	}

	private void ActualizarFichaDetalles(CartaData datos)
	{
		if (_lblDetalleTitulo != null) _lblDetalleTitulo.Text = "Datos";
		if (_lblDetalleHp != null) _lblDetalleHp.Text = $"HP: {datos.Vida}";
		if (_lblDetalleAtk != null) _lblDetalleAtk.Text = $"ATK: {datos.Ataque}";
		if (_lblDetalleDef != null) _lblDetalleDef.Text = $"DEF: {datos.Defensa}";
		if (_lblDetalleCosto != null) _lblDetalleCosto.Text = $"Costo: {datos.Costo}";

		var info = ClasificacionCartas.Clasificar(datos.RutaEscena, datos.Nombre);
		// Antes mostraba "Elemento: X"; ahora muestra la Serie de la tropa.
		if (_lblDetalleElemento != null) _lblDetalleElemento.Text = $"Serie: {EtiquetaSerie(info.Serie)}";

		if (_lblDetalleHabilidad != null)
		{
			string desc = !string.IsNullOrEmpty(datos.Descripcion) ? datos.Descripcion : "Habilidad estándar de combate.";
			_lblDetalleHabilidad.Text = desc;
		}

		// Línea extra: tipo de tropa (Táctico/Asesino/Coloso) en vez de ventajas por elemento.
		if (_lblDetalleExtra != null)
		{
			string extra = info.Tipo == TipoTropa.Desconocido ? "" : $"Tipo: {EtiquetaTipoSingular(info.Tipo)}";
			_lblDetalleExtra.Text = extra;
			_lblDetalleExtra.Visible = !string.IsNullOrEmpty(extra);
		}
	}

	private static string EtiquetaSerie(SerieTropa s) => s switch
	{
		SerieTropa.Ajedrez  => "AJEDREZ",
		SerieTropa.Toon     => "TOON",
		SerieTropa.Medieval => "MEDIEVAL",
		SerieTropa.Pacifico => "PACÍFICO",
		SerieTropa.Papeleo  => "PAPELEO",
		_                   => "—"
	};

	private static string EtiquetaTipoSingular(TipoTropa t) => t switch
	{
		TipoTropa.Tactico => "Táctico",
		TipoTropa.Asesino => "Asesino",
		TipoTropa.Coloso  => "Coloso",
		_                 => "—"
	};

	#region Gestión del Mazo (Cofre)

	private void CrearRanurasMazo()
	{
		if (_gridMazoSlots == null) return;

		foreach (Node n in _gridMazoSlots.GetChildren())
		{
			_gridMazoSlots.RemoveChild(n);
			n.QueueFree();
		}

		if (_pestanaActual == "TROPAS")
		{
			for (int i = 0; i < MAX_CARTAS; i++)
			{
				var slot = new PanelContainer();
				slot.Name = $"Slot_Tropa_{i}";
				slot.CustomMinimumSize = new Vector2(130, 125);

				var style = new StyleBoxEmpty();
				slot.AddThemeStyleboxOverride("panel", style);

				_gridMazoSlots.AddChild(slot);
			}
		}
		else
		{
			for (int i = 0; i < MAX_ARDIDES; i++)
			{
				var slot = new PanelContainer();
				slot.Name = $"Slot_Ardid_{i}";
				slot.CustomMinimumSize = new Vector2(175, 195);

				var style = new StyleBoxEmpty();
				slot.AddThemeStyleboxOverride("panel", style);

				_gridMazoSlots.AddChild(slot);
			}
		}
	}

	public void AgregarAlMazo(CartaData datos)
	{
		if (datos == null) return;

		bool esTropa = (datos.Categoria == CategoriaCarta.Unidad);

		if (esTropa)
		{
			foreach (var c in _cartasEnMazo)
			{
				if (c.Nombre.Equals(datos.Nombre, StringComparison.OrdinalIgnoreCase))
				{
					MostrarMensajeAviso($"¡{datos.Nombre} ya está en tu mazo de tropas!");
					return;
				}
			}

			// Límite por tipo: 3 tácticos, 3 asesinos, 2 colosos
			TipoTropa tipo = ClasificacionCartas.TipoDe(datos.RutaEscena, datos.Nombre);
			int enTipo  = ContarPorTipo(tipo);
			int maxTipo = MaxPorTipo(tipo);
			if (tipo != TipoTropa.Desconocido && enTipo >= maxTipo)
			{
				MostrarMensajeAviso($"Tipo {EtiquetaTipo(tipo)} ya lleno ({enTipo}/{maxTipo}). Prueba otro tipo.");
				return;
			}

			if (_cartasEnMazo.Count >= MAX_CARTAS)
			{
				MostrarMensajeAviso($"El mazo de tropas está lleno ({MAX_CARTAS}/{MAX_CARTAS}). Quita una carta primero.");
				return;
			}

			_cartasEnMazo.Add(datos);
		}
		else
		{
			foreach (var c in _cartasArdidEnMazo)
			{
				if (c.Nombre.Equals(datos.Nombre, StringComparison.OrdinalIgnoreCase))
				{
					MostrarMensajeAviso($"¡{datos.Nombre} ya está en tus ardides!");
					return;
				}
			}

			if (_cartasArdidEnMazo.Count >= MAX_ARDIDES)
			{
				MostrarMensajeAviso($"Los espacios de ardid están llenos ({MAX_ARDIDES}/{MAX_ARDIDES}). Quita un hechizo primero.");
				return;
			}

			_cartasArdidEnMazo.Add(datos);
		}

		ActualizarMazoVisual();
		GlobalAudioManager.Instance?.PlayClickSound();
	}

	private int ContarPorTipo(TipoTropa tipo)
	{
		int n = 0;
		foreach (var c in _cartasEnMazo)
			if (ClasificacionCartas.TipoDe(c.RutaEscena, c.Nombre) == tipo) n++;
		return n;
	}

	private static int MaxPorTipo(TipoTropa tipo) => tipo switch
	{
		TipoTropa.Tactico => MAX_TACTICOS,
		TipoTropa.Asesino => MAX_ASESINOS,
		TipoTropa.Coloso  => MAX_COLOSOS,
		_                 => MAX_CARTAS
	};

	private static string EtiquetaTipo(TipoTropa tipo) => tipo switch
	{
		TipoTropa.Tactico => "Tácticos",
		TipoTropa.Asesino => "Asesinos",
		TipoTropa.Coloso  => "Colosos",
		_                 => "Tropa"
	};

	public void RemoverDelMazo(CartaData datos)
	{
		if (datos == null) return;

		if (datos.Categoria == CategoriaCarta.Unidad)
			_cartasEnMazo.Remove(datos);
		else
			_cartasArdidEnMazo.Remove(datos);

		ActualizarMazoVisual();
		GlobalAudioManager.Instance?.PlayClickSound();
	}

	private void ActualizarMazoVisual()
	{
		if (_gridMazoSlots == null || _escenaCartaMini == null) return;

		bool esTropaTab = (_pestanaActual == "TROPAS");
		var listaActiva = esTropaTab ? _cartasEnMazo : _cartasArdidEnMazo;
		int maxActivo = esTropaTab ? MAX_CARTAS : MAX_ARDIDES;

		var slots = _gridMazoSlots.GetChildren();
		for (int i = 0; i < slots.Count; i++)
		{
			var slot = slots[i] as Control;
			if (slot == null) continue;

			foreach (Node h in slot.GetChildren())
			{
				slot.RemoveChild(h);
				h.QueueFree();
			}

			if (i < listaActiva.Count)
			{
				var datos = listaActiva[i];
				var mini = _escenaCartaMini.Instantiate<CartaMini>();
				slot.AddChild(mini);
				mini.CargarDatos(datos);
				mini.SetModoMazo(true, !esTropaTab);

				mini.PivotOffset = mini.Size / 2f;
				mini.Scale = new Vector2(0.4f, 0.4f);
				var tw = mini.CreateTween();
				tw.TweenProperty(mini, "scale", Vector2.One, 0.18f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

				mini.OnClickeada += (c) =>
				{
					SeleccionarCarta(c.MisDatos);
					RemoverDelMazo(c.MisDatos);
				};
			}
		}

		int count = listaActiva.Count;
		if (_lblContadorMazo != null)
		{
			_lblContadorMazo.Text = $" {count}/{maxActivo} ";
			_lblContadorMazo.Modulate = count == maxActivo ? new Color(0.2f, 1f, 0.4f) : new Color(0.9f, 0.7f, 0.2f);
		}

		if (_btnBatallar != null)
		{
			bool listo = (_cartasEnMazo.Count == MAX_CARTAS);
			_btnBatallar.Disabled = !listo;
			_btnBatallar.Modulate = listo ? Colors.White : new Color(0.75f, 0.75f, 0.75f, 0.8f);
		}
	}

	#endregion

	#region Acciones de Botones y Navegación

	private void IrABatalla()
	{
		if (_cartasEnMazo.Count < MIN_CARTAS)
		{
			MostrarMensajeAviso($"Necesitas {MIN_CARTAS} cartas para batallar.");
			return;
		}

		// Validar composición obligatoria: 3 tácticos, 3 asesinos, 2 colosos
		int nTac = ContarPorTipo(TipoTropa.Tactico);
		int nAse = ContarPorTipo(TipoTropa.Asesino);
		int nCol = ContarPorTipo(TipoTropa.Coloso);
		if (nTac != MAX_TACTICOS || nAse != MAX_ASESINOS || nCol != MAX_COLOSOS)
		{
			MostrarMensajeAviso($"Mazo inválido: necesitas {MAX_TACTICOS} tácticos, {MAX_ASESINOS} asesinos y {MAX_COLOSOS} colosos (tienes {nTac}/{nAse}/{nCol}).");
			return;
		}

		var escenas = new List<string>();
		var imagenes = new List<string>();

		foreach (var c in _cartasEnMazo)
		{
			if (!string.IsNullOrEmpty(c.RutaEscena))
			{
				escenas.Add(c.RutaEscena);
				// Imagen grande de batalla (CartasPng), no el icono del selector
				string png = ClasificacionCartas.ImagenBatalla(c.RutaEscena, c.Nombre);
				if (string.IsNullOrEmpty(png)) png = c.Imagen != null ? c.Imagen.ResourcePath : "";
				imagenes.Add(png);
			}
		}

		if (escenas.Count < MIN_CARTAS)
		{
			MostrarMensajeAviso($"El mazo contiene cartas sin escena de combate.");
			return;
		}

		if (SesionJuego.Instance != null)
		{
			SesionJuego.Instance.GuardarMazo(escenas, imagenes);
		}

		GD.Print($"[MenuConstructor] Mazo de {_cartasEnMazo.Count} cartas guardado → ¡A Batallar!");
		GetTree().ChangeSceneToFile(RutaBatalla);
	}

	private void VolverAlMenu()
	{
		GlobalAudioManager.Instance?.PlayClickSound();
		GetTree().ChangeSceneToFile(RutaMenu);
	}

	private void AbrirAyuda()
	{
		GlobalAudioManager.Instance?.PlayClickSound();
		if (_modalAyuda != null)
		{
			_modalAyuda.Visible = true;
			_modalAyuda.Modulate = new Color(1, 1, 1, 0);
			var tw = _modalAyuda.CreateTween();
			tw.TweenProperty(_modalAyuda, "modulate", Colors.White, 0.2f);
		}
	}

	private void CerrarAyuda()
	{
		GlobalAudioManager.Instance?.PlayClickSound();
		if (_modalAyuda != null)
		{
			var tw = _modalAyuda.CreateTween();
			tw.TweenProperty(_modalAyuda, "modulate", new Color(1, 1, 1, 0), 0.15f);
			tw.TweenCallback(Callable.From(() => _modalAyuda.Visible = false));
		}
	}

	private void MostrarMensajeAviso(string mensaje)
	{
		var lbl = new Label();
		lbl.Text = mensaje;
		lbl.AddThemeFontSizeOverride("font_size", 20);
		lbl.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.2f));
		lbl.Position = new Vector2(700, 30);
		lbl.ZIndex = 200;
		AddChild(lbl);

		var tw = lbl.CreateTween();
		lbl.Modulate = new Color(1, 1, 1, 0);
		tw.TweenProperty(lbl, "modulate", Colors.White, 0.2f);
		tw.TweenInterval(1.8f);
		tw.TweenProperty(lbl, "modulate", new Color(1, 1, 1, 0), 0.3f);
		tw.TweenCallback(Callable.From(() => lbl.QueueFree()));
	}

	private static T BuscarNodoRecursivo<T>(Node padre) where T : Node
	{
		foreach (Node hijo in padre.GetChildren())
		{
			if (hijo is T res) return res;
			var sub = BuscarNodoRecursivo<T>(hijo);
			if (sub != null) return sub;
		}
		return null;
	}

	#endregion
}
