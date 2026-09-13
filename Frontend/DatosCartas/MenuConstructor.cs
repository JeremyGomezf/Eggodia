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

	// Renders estáticos de tropas para el showcase central del selector de mazo (carpeta RendersTropa).
	// Al seleccionar cualquier tropa, se muestra su render estático apoyado sobre el borde superior del mazo.
	private static readonly (string clave, string rutaRender)[] RENDERS_TROPA = new[]
	{
		("peon",           "res://imagenes/RendersTropa/Peon_Menu.png"),
		("torre",          "res://imagenes/RendersTropa/Torre_Menu.png"),
		("arfil",          "res://imagenes/RendersTropa/Arfil_Menu.png"),
		("caballo",        "res://imagenes/RendersTropa/Caballo_Menu.png"),
		("dama",           "res://imagenes/RendersTropa/Dama_Menu.png"),
		("soldadocartoon", "res://imagenes/RendersTropa/SoldadoCartoon_Menu.png"),
		("tanque",         "res://imagenes/RendersTropa/Tanque_Menu.png"),
		("granadero",      "res://imagenes/RendersTropa/Granadero_Menu.png"),
		("kabar",          "res://imagenes/RendersTropa/Kabar_Menu.png"),
		("campero",        "res://imagenes/RendersTropa/Campero_Menu.png"),
		("soldadoreal",    "res://imagenes/RendersTropa/SoldadoReal_Render.png"),
		("maguin",         "res://imagenes/RendersTropa/Maguin_Menu.png"),
		("majin",          "res://imagenes/RendersTropa/Maguin_Menu.png"),
		("dragon",         "res://imagenes/RendersTropa/Dragon_Menu.png"),
		("golem",          "res://imagenes/RendersTropa/Golem_Menu.png"),
		("tiburon",        "res://imagenes/RendersTropa/Tiburon_Menu.png"),
		("calamar",        "res://imagenes/RendersTropa/Calamar_Menu.png"),
		("paperex",        "res://imagenes/RendersTropa/Paperex_Menu.png"),
	};

	// Ajustes finos de posición vertical por render (en píxeles; negativo = sube, positivo = baja).
	// Retocar acá si algún render queda muy arriba/abajo respecto al resto.
	private static readonly Dictionary<string, float> AJUSTE_VERTICAL_RENDER = new()
	{
		{ "res://imagenes/RendersTropa/Maguin_Menu.png",         -110f },
		{ "res://imagenes/RendersTropa/Dragon_Menu.png",          -20f },
		{ "res://imagenes/RendersTropa/Calamar_Menu.png",         -20f },
		{ "res://imagenes/RendersTropa/Paperex_Menu.png",          20f },
		{ "res://imagenes/RendersTropa/Peon_Menu.png",              0f },
		{ "res://imagenes/RendersTropa/Torre_Menu.png",             0f },
		{ "res://imagenes/RendersTropa/Arfil_Menu.png",             0f },
		{ "res://imagenes/RendersTropa/Caballo_Menu.png",          -10f },
		{ "res://imagenes/RendersTropa/Dama_Menu.png",              0f },
		{ "res://imagenes/RendersTropa/SoldadoCartoon_Menu.png",   -10f },
		{ "res://imagenes/RendersTropa/Tanque_Menu.png",            -5f },
		{ "res://imagenes/RendersTropa/Granadero_Menu.png",        -10f },
		{ "res://imagenes/RendersTropa/Kabar_Menu.png",            -30f },
		{ "res://imagenes/RendersTropa/Campero_Menu.png",          -20f },
		{ "res://imagenes/RendersTropa/Golem_Menu.png",             -5f },
		{ "res://imagenes/RendersTropa/Tiburon_Menu.png",           0f },
	};

	private const string RUTA_SOLDADO_REAL_MENU = "res://imagenes/RendersTropa/SoldadoReal_Menu.png";
	private const string RUTA_SOLDADO_REAL_RENDER = "res://imagenes/RendersTropa/SoldadoReal_Render.png";

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
	[Export] private Sprite2D _fallbackTextureCenter;
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
	private List<CartaData> _mazoInicial = null;
	private string _pestanaActual = "TROPAS"; // "TROPAS" o "ARDID"
	private CartaData _cartaSeleccionada = null;
	private AnimatedSprite2D _spriteAnimadoActual = null;
	private Tween _idleTween = null;
	private Tween _soldadoRealTween = null;
	private Sprite2D _overlayTextureCenter = null;
	private bool _estaAtacando = false;

	// Efecto "1930s Cartoon Aesthetic" (blanco y negro/sepia) al elegir una carta de Serie: Toon.
	private ColorRect _rectVintage;

	public override void _Ready()
	{
		// Si se llegó acá reintentando desde la pantalla de Derrota, la música global quedó
		// detenida a propósito durante la batalla (ver Campo1.SilenciarOtrasMusicas).
		GlobalAudioManager.Instance?.AsegurarReproduccion();

		_rectVintage = EfectoVintageToons.Instalar(this);

		CargarTexturasPorDefecto();

		if (_btnTabTropas != null) _btnTabTropas.Pressed += () => CambiarPestana("TROPAS");
		if (_btnTabArdid != null) _btnTabArdid.Pressed += () => CambiarPestana("ARDID");

		if (_txtBuscador != null)
		{
			_txtBuscador.TextChanged += (txt) => FiltrarCartas(txt);
			_txtBuscador.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
			_txtBuscador.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
		}

		if (_btnBatallar != null)
		{
			_btnBatallar.Text = "SELECCIONAR";
			_btnBatallar.Pressed += ConfirmarSeleccionMazo;
		}
		if (_btnVolver != null) _btnVolver.Pressed += VolverAlMenu;

		if (_btnDuda != null)
		{
			_btnDuda.Pressed += AbrirAyuda;
			AgregarJuiceBotonDuda(_btnDuda);
		}
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

		if (_fallbackTextureCenter != null)
		{
			// Sprite2D no tiene GuiInput (eso es exclusivo de Control) — el clic para la animación
			// de ataque queda disponible a través de _containerSpriteCenter, como ya estaba previsto.

			// Capa superior para el cross-fade del aura de Soldado Real. Al ser hija de un Sprite2D
			// centrado, con Position en (0,0) queda automáticamente alineada encima sin más cálculo.
			_overlayTextureCenter = new Sprite2D();
			_overlayTextureCenter.Name = "SoldadoRealAuraOverlay";
			_overlayTextureCenter.Visible = false;
			_fallbackTextureCenter.AddChild(_overlayTextureCenter);
		}

		CargarCartasDesdeDisco();
		InicializarMazoJugador();
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

	private void InicializarMazoJugador()
	{
		_cartasEnMazo.Clear();

		if (SesionJuego.Instance != null && SesionJuego.Instance.TieneMazo)
		{
			foreach (string ruta in SesionJuego.Instance.MazoSeleccionado)
			{
				string norm = ClasificacionCartas.Normalizar(ruta);
				var match = _todasLasCartas.Find(c => !string.IsNullOrEmpty(c.RutaEscena) && ClasificacionCartas.Normalizar(c.RutaEscena) == norm);
				if (match != null && !_cartasEnMazo.Contains(match))
				{
					_cartasEnMazo.Add(match);
				}
			}
		}

		if (_cartasEnMazo.Count < MAX_CARTAS)
		{
			AutoCompletarMazoValido();
		}

		_mazoInicial = new List<CartaData>(_cartasEnMazo);
	}

	private void AutoCompletarMazoValido()
	{
		int nTac = ContarPorTipo(TipoTropa.Tactico);
		int nAse = ContarPorTipo(TipoTropa.Asesino);
		int nCol = ContarPorTipo(TipoTropa.Coloso);

		foreach (var c in _todasLasCartas)
		{
			if (c.Categoria != CategoriaCarta.Unidad || string.IsNullOrEmpty(c.RutaEscena) || _cartasEnMazo.Contains(c))
				continue;

			var tipo = ClasificacionCartas.TipoDe(c.RutaEscena, c.Nombre);
			if (tipo == TipoTropa.Tactico && nTac < MAX_TACTICOS)
			{
				_cartasEnMazo.Add(c);
				nTac++;
			}
			else if (tipo == TipoTropa.Asesino && nAse < MAX_ASESINOS)
			{
				_cartasEnMazo.Add(c);
				nAse++;
			}
			else if (tipo == TipoTropa.Coloso && nCol < MAX_COLOSOS)
			{
				_cartasEnMazo.Add(c);
				nCol++;
			}

			if (_cartasEnMazo.Count >= MAX_CARTAS) break;
		}

		if (_cartasEnMazo.Count == MAX_CARTAS && (SesionJuego.Instance == null || !SesionJuego.Instance.TieneMazo))
		{
			var esc = new List<string>();
			var img = new List<string>();
			foreach (var c in _cartasEnMazo)
			{
				esc.Add(c.RutaEscena);
				string png = ClasificacionCartas.ImagenBatalla(c.RutaEscena, c.Nombre);
				if (string.IsNullOrEmpty(png)) png = c.Imagen != null ? c.Imagen.ResourcePath : "";
				img.Add(png);
			}
			SesionJuego.Instance?.GuardarMazo(esc, img);
		}
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
				mazoContainer.Position = new Vector2(595, 465);
				mazoContainer.Size = new Vector2(730, 455);
			}

			if (_gridMazoSlots != null)
			{
				_gridMazoSlots.Columns = 4;
				_gridMazoSlots.AnchorLeft = 0.5f;
				_gridMazoSlots.AnchorRight = 0.5f;
				_gridMazoSlots.AnchorTop = 0.5f;
				_gridMazoSlots.AnchorBottom = 0.5f;
				_gridMazoSlots.OffsetLeft = -290.0f;
				_gridMazoSlots.OffsetRight = 340.0f;
				_gridMazoSlots.OffsetTop = -135.0f;
				_gridMazoSlots.OffsetBottom = 185.0f;
				_gridMazoSlots.AddThemeConstantOverride("h_separation", 22);
				_gridMazoSlots.AddThemeConstantOverride("v_separation", 16);
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
				mazoContainer.Position = new Vector2(595, 400);
				mazoContainer.Size = new Vector2(730, 520);
			}

			if (_gridMazoSlots != null)
			{
				_gridMazoSlots.Columns = 3;
				_gridMazoSlots.AnchorLeft = 0.5f;
				_gridMazoSlots.AnchorRight = 0.5f;
				_gridMazoSlots.AnchorTop = 0.5f;
				_gridMazoSlots.AnchorBottom = 0.5f;
				_gridMazoSlots.OffsetLeft = -315.0f;
				_gridMazoSlots.OffsetRight = 315.0f;
				_gridMazoSlots.OffsetTop = -220.0f;
				_gridMazoSlots.OffsetBottom = 220.0f;
				_gridMazoSlots.AddThemeConstantOverride("h_separation", 22);
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

		// "1930s Cartoon Aesthetic": blanco y negro/sepia mientras se tiene seleccionada una
		// carta de Serie: Toon; al elegir cualquier otra, todo vuelve a color normal.
		bool esToon = ClasificacionCartas.SerieDe(datos.RutaEscena, datos.Nombre) == SerieTropa.Toon;
		EfectoVintageToons.AplicarIntensidad(_rectVintage, esToon ? 0.9f : 0.0f);

		if (_lblShowcaseNombre != null)
		{
			string nombreUpper = datos.Nombre.ToUpper();
			_lblShowcaseNombre.Text = nombreUpper;

			// Ajustar tamaño de fuente y ancho del banner holgadamente para que no quede "a la medida" apretado,
			// sino que mantenga proporciones amplias y elegantes como en el diseño original.
			int fontSize = 25;
			if (nombreUpper.Length > 14) fontSize = 23;
			if (nombreUpper.Length > 18) fontSize = 20;
			_lblShowcaseNombre.AddThemeFontSizeOverride("font_size", fontSize);

			var font = _lblShowcaseNombre.GetThemeFont("font");
			float textWidth = font != null
				? font.GetStringSize(nombreUpper, HorizontalAlignment.Center, -1, fontSize).X
				: nombreUpper.Length * (fontSize * 0.65f);

			// Mantener un ancho amplio con márgenes generosos a los lados (mínimo 440px, máximo 560px)
			float bannerWidth = Mathf.Clamp(textWidth + 180f, 440f, 560f);

			var banner = _lblShowcaseNombre.GetParent<Control>();
			if (banner != null)
			{
				banner.OffsetLeft = -bannerWidth / 2f;
				banner.OffsetRight = bannerWidth / 2f;
			}

			_lblShowcaseNombre.PivotOffset = _lblShowcaseNombre.Size / 2f;
			_lblShowcaseNombre.Scale = new Vector2(1.06f, 1.06f);
			var tw = _lblShowcaseNombre.CreateTween();
			tw.TweenProperty(_lblShowcaseNombre, "scale", Vector2.One, 0.18f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}

		ActualizarShowcaseAnimado(datos);
		ActualizarFichaDetalles(datos);
	}

	private static string ObtenerRutaRenderTropa(CartaData datos)
	{
		if (datos == null) return null;
		string norm = ClasificacionCartas.Normalizar(datos.RutaEscena) + "|" + ClasificacionCartas.Normalizar(datos.Nombre);
		foreach (var entry in RENDERS_TROPA)
		{
			if (norm.Contains(entry.clave))
			{
				return entry.rutaRender;
			}
		}
		return null;
	}

	private void ActualizarShowcaseAnimado(CartaData datos)
	{
		if (_fallbackTextureCenter == null) return;

		_idleTween?.Kill();
		_idleTween = null;
		_soldadoRealTween?.Kill();
		_soldadoRealTween = null;
		_estaAtacando = false;

		if (_spriteAnimadoActual != null && IsInstanceValid(_spriteAnimadoActual))
		{
			_spriteAnimadoActual.QueueFree();
			_spriteAnimadoActual = null;
		}

		string norm = ClasificacionCartas.Normalizar(datos.RutaEscena) + "|" + ClasificacionCartas.Normalizar(datos.Nombre);
		bool esSoldadoReal = norm.Contains("soldadoreal");
		string rutaRender = ObtenerRutaRenderTropa(datos);

		_fallbackTextureCenter.Visible = true;
		_fallbackTextureCenter.Position = Vector2.Zero;
		_fallbackTextureCenter.Rotation = 0f;

		if (rutaRender != null)
		{
			// Tamaño real: se carga cada render 1:1 tal cual es su imagen original (ej. Soldado
			// Real 830x666), sin escalar hacia abajo — a diferencia del fallback de ardides, que
			// sí se ajusta a una caja (ver AplicarTexturaAjustada más abajo).
			if (esSoldadoReal)
			{
				var texRender = GD.Load<Texture2D>(RUTA_SOLDADO_REAL_RENDER);
				CargarTexturaNativa(_fallbackTextureCenter, texRender);

				if (_overlayTextureCenter != null)
				{
					var texMenu = GD.Load<Texture2D>(RUTA_SOLDADO_REAL_MENU);
					CargarTexturaNativa(_overlayTextureCenter, texMenu);
					_overlayTextureCenter.Visible = true;
					_overlayTextureCenter.Modulate = new Color(1, 1, 1, 0);

					// Efecto "aura de imágenes": se ve el render de combate 2.5s y ahí, una sola vez
					// (no en bucle), se funde al render de menú y se queda en él.
					_soldadoRealTween = CreateTween();
					_soldadoRealTween.TweenInterval(2.5f);
					_soldadoRealTween.TweenProperty(_overlayTextureCenter, "modulate:a", 1.0f, 0.3f)
						.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
					_soldadoRealTween.TweenCallback(Callable.From(() =>
					{
						if (!IsInstanceValid(_fallbackTextureCenter) || !IsInstanceValid(_overlayTextureCenter)) return;
						CargarTexturaNativa(_fallbackTextureCenter, texMenu);
						_overlayTextureCenter.Visible = false;
					}));
				}
			}
			else
			{
				if (_overlayTextureCenter != null)
				{
					_overlayTextureCenter.Visible = false;
				}
				CargarTexturaNativa(_fallbackTextureCenter, GD.Load<Texture2D>(rutaRender));
				if (AJUSTE_VERTICAL_RENDER.TryGetValue(rutaRender, out float ajusteY))
					_fallbackTextureCenter.Position = new Vector2(0f, ajusteY);
				// Todas las demás tropas se muestran de forma completamente estática
			}
		}
		else
		{
			// Cartas de ardid / hechizos u otros sin render de tropa: siguen ajustadas a una caja
			// chica (son íconos, no renders grandes) para no salir gigantes en el mismo slot.
			if (_overlayTextureCenter != null)
			{
				_overlayTextureCenter.Visible = false;
			}
			AplicarTexturaAjustada(_fallbackTextureCenter, datos.Imagen, 180f, 220f);
		}
	}

	// Carga la textura a su tamaño real (Scale 1:1) — usado para los renders grandes de tropa.
	private static void CargarTexturaNativa(Sprite2D sprite, Texture2D tex)
	{
		sprite.Texture = tex;
		sprite.Scale = Vector2.One;
	}

	// Escala el Sprite2D para que su textura quepa dentro de una caja de cajaAncho×cajaAlto
	// SIN deformarse (mantiene proporción) — usado solo para el ícono de ardides/hechizos.
	private static void AplicarTexturaAjustada(Sprite2D sprite, Texture2D tex, float cajaAncho, float cajaAlto)
	{
		sprite.Texture = tex;
		if (tex == null) return;
		float w = tex.GetWidth(), h = tex.GetHeight();
		if (w <= 0 || h <= 0) return;
		float factor = Mathf.Min(cajaAncho / w, cajaAlto / h);
		sprite.Scale = new Vector2(factor, factor);
	}

	public void EjecutarAnimacionAtaqueShowcase()
	{
		if (_estaAtacando) return;

		if (_fallbackTextureCenter != null && _fallbackTextureCenter.Visible)
		{
			_estaAtacando = true;
			GlobalAudioManager.Instance?.PlayClickSound();
			Vector2 escalaBase = _fallbackTextureCenter.Scale;
			var tw = CreateTween();
			tw.TweenProperty(_fallbackTextureCenter, "scale", escalaBase * 1.08f, 0.08f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			tw.TweenProperty(_fallbackTextureCenter, "scale", escalaBase, 0.12f);
			tw.TweenCallback(Callable.From(() => _estaAtacando = false));
		}
	}

	private void ActualizarFichaDetalles(CartaData datos)
	{
		if (_lblDetalleTitulo != null) _lblDetalleTitulo.Text = "Datos";
		if (_lblDetalleHp != null) _lblDetalleHp.Text = $"HP: {datos.Vida}";
		if (_lblDetalleAtk != null) _lblDetalleAtk.Text = $"ATK: {datos.Ataque}";
		if (_lblDetalleDef != null) _lblDetalleDef.Text = $"DEF: {datos.Defensa}";
		if (_lblDetalleCosto != null) _lblDetalleCosto.Visible = false; // el juego no tiene sistema de costo

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
			_btnBatallar.Text = "SELECCIONAR";
			bool listo = (_cartasEnMazo.Count == MAX_CARTAS);
			_btnBatallar.Disabled = !listo;
			_btnBatallar.Modulate = listo ? Colors.White : new Color(0.75f, 0.75f, 0.75f, 0.8f);
		}
	}

	#endregion

	#region Acciones de Botones y Navegación

	private void ConfirmarSeleccionMazo()
	{
		if (_cartasEnMazo.Count < MIN_CARTAS)
		{
			MostrarMensajeAviso($"Necesitas {MIN_CARTAS} cartas para completar tu mazo.");
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

		// Actualizar snapshot confirmado
		_mazoInicial = new List<CartaData>(_cartasEnMazo);
		GlobalAudioManager.Instance?.PlayClickSound();

		GD.Print($"[MenuConstructor] Mazo de {_cartasEnMazo.Count} cartas seleccionado y guardado → Volviendo al Menú Principal");
		EfectoVintageToons.AplicarIntensidad(_rectVintage, 0f, 0.15f);
		GetTree().ChangeSceneToFile(RutaMenu);
	}

	private void VolverAlMenu()
	{
		GlobalAudioManager.Instance?.PlayClickSound();
		// Descartar cambios no guardados y restaurar el mazo tal como estaba al entrar
		_cartasEnMazo.Clear();
		if (_mazoInicial != null)
		{
			_cartasEnMazo.AddRange(_mazoInicial);
		}
		EfectoVintageToons.AplicarIntensidad(_rectVintage, 0f, 0.15f);
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

	private Control _avisoBloqueoActual;

	// Pantalla semi oscura + texto grande blanco con borde negro grueso (ej. "Tipo Colosos ya
	// lleno. Prueba otro tipo.") — reemplaza el texto amarillo anterior, que quedaba arriba
	// y era poco legible.
	// Juice del botón de ayuda: crece al pasar el mouse cerca (hover) y se achica/aplasta al
	// presionarlo — mismo lenguaje visual que ya usamos en otros botones del juego.
	private void AgregarJuiceBotonDuda(BaseButton btn)
	{
		Vector2 escalaBase = btn.Scale;
		btn.PivotOffset = btn.Size / 2f;

		btn.MouseEntered += () =>
		{
			if (btn.Disabled) return;
			btn.CreateTween().TweenProperty(btn, "scale", escalaBase * 1.12f, 0.15f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		};
		btn.MouseExited += () =>
		{
			btn.CreateTween().TweenProperty(btn, "scale", escalaBase, 0.15f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		};
		btn.ButtonDown += () =>
		{
			btn.CreateTween().TweenProperty(btn, "scale", escalaBase * 0.88f, 0.06f);
		};
		btn.ButtonUp += () =>
		{
			btn.CreateTween().TweenProperty(btn, "scale", escalaBase, 0.12f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		};
	}

	private void MostrarMensajeAviso(string mensaje)
	{
		if (_avisoBloqueoActual != null && IsInstanceValid(_avisoBloqueoActual)) _avisoBloqueoActual.QueueFree();

		var overlay = new Control();
		overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
		overlay.ZIndex = 300;
		// Dentro del CanvasLayer "CapaUI": la escena tiene una Camera2D, y cualquier Control
		// agregado directo a "this" queda sujeto a su transform (se ve corrido/no centrado).
		Node capaUI = GetNodeOrNull<CanvasLayer>("CapaUI") ?? (Node)this;
		capaUI.AddChild(overlay);
		_avisoBloqueoActual = overlay;

		var fondo = new ColorRect();
		fondo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		fondo.Color = new Color(0, 0, 0, 0.55f);
		fondo.MouseFilter = Control.MouseFilterEnum.Ignore;
		overlay.AddChild(fondo);

		var centro = new CenterContainer();
		centro.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		centro.MouseFilter = Control.MouseFilterEnum.Ignore;
		overlay.AddChild(centro);

		var lbl = new Label();
		lbl.Text = mensaje;
		lbl.CustomMinimumSize = new Vector2(1000, 0);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		lbl.AddThemeFontSizeOverride("font_size", 52);
		lbl.AddThemeColorOverride("font_color", Colors.White);
		lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
		lbl.AddThemeConstantOverride("outline_size", 13);
		lbl.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
		lbl.AddThemeConstantOverride("shadow_offset_x", 3);
		lbl.AddThemeConstantOverride("shadow_offset_y", 4);
		centro.AddChild(lbl);

		overlay.Modulate = new Color(1, 1, 1, 0);
		var tw = overlay.CreateTween();
		tw.TweenProperty(overlay, "modulate:a", 1.0f, 0.2f);
		tw.TweenInterval(1.8f);
		tw.TweenProperty(overlay, "modulate:a", 0.0f, 0.35f);
		tw.TweenCallback(Callable.From(() => { if (IsInstanceValid(overlay)) overlay.QueueFree(); }));
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
