using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// MenuConstructor — armador de mazo.
/// Conecta ColeccionPanel + MazoPanel + CartaDetalles.
/// Cuando el jugador presiona "CONTINUE" guarda el mazo
/// en SesionJuego y cambia a la escena de batalla.
/// </summary>
public partial class MenuConstructor : Control
{
	[Export] private ColeccionPanel _panelColeccion;
	[Export] private MazoPanel      _panelMazo;
	[Export] private CartaDetalles  _panelDetalles;
	[Export] private Button         _btnBatallar;
	[Export] private Button         _btnVolver;
	[Export] private Label          _lblContadorMazo;
	[Export] private ProgressBar    _progressBar;

	[Export] public string RutaBatalla = "res://escenas/gameplay/campo_1.tscn";
	[Export] public string RutaMenu    = "res://escenas/menu/menu_principal.tscn";

	private const int MIN_CARTAS = 8;
	private const int MAX_CARTAS = 8;

	public override void _Ready()
	{
		// Configurar barra de progreso
		if (_progressBar != null)
		{
			_progressBar.MaxValue = MAX_CARTAS;
			_progressBar.Value = 0;
		}

		// Conectar eventos entre paneles
		if (_panelColeccion != null)
		{
			_panelColeccion.OnCartaElegidaParaMazo += _panelMazo.AgregarCartaAlMazo;
			_panelColeccion.OnCartaElegidaParaMazo += (carta) =>
			{
				_panelDetalles?.MostrarDatos(carta);
				ActualizarContador();
			};
		}

		if (_panelMazo != null)
		{
			if (_panelDetalles != null) _panelMazo.OnCartaSeleccionadaEnMazo += _panelDetalles.MostrarDatos;
			_panelMazo.OnMazoCambiado += ActualizarContador;
		}

		if (_btnBatallar != null)
		{
			_btnBatallar.Pressed += IrABatalla;
			_btnBatallar.Visible = false;
		}
		if (_btnVolver != null)
			_btnVolver.Pressed += () => {
				string ruta = !string.IsNullOrEmpty(RutaMenu) ? RutaMenu : "res://escenas/menu/menu_principal.tscn";
				GD.Print("[Constructor] Volviendo a: " + ruta);
				GetTree().ChangeSceneToFile(ruta);
			};

		ActualizarContador();
	}

	private void IrABatalla()
	{
		int cantidadEnMazo = ContarCartasEnMazo();
		if (cantidadEnMazo < MIN_CARTAS)
		{
			MostrarAviso($"Necesitas al menos {MIN_CARTAS} cartas para continuar.");
			return;
		}

		if (cantidadEnMazo > MAX_CARTAS)
		{
			MostrarAviso($"El mazo no puede superar {MAX_CARTAS} cartas.");
			return;
		}

		string rutaBatalla = !string.IsNullOrEmpty(RutaBatalla) ? RutaBatalla : "res://escenas/gameplay/campo_1.tscn";
		if (!ResourceLoader.Exists(rutaBatalla))
		{
			GD.PrintErr("[MenuConstructor] RutaBatalla inválida: " + rutaBatalla);
			MostrarAviso("No se encontró la escena de batalla.");
			return;
		}
		// Recolectar cartas del mazo
		var escenas  = new List<string>();
		var imagenes = new List<string>();
		if (_panelMazo == null)
		{
			MostrarAviso("No se encontró el panel del mazo.");
			return;
		}

		// Leer cartas del grid del mazo
		var grid = _panelMazo.GetNodeOrNull<GridContainer>("MarginContainer/VBoxContainer/GridMazo");

		if (grid != null)
		{
			foreach (Node slot in grid.GetChildren())
			{
				var cartaMini = slot.GetNodeOrNull<CartaMini>("CartaMini") ?? slot.GetChildOrNull<CartaMini>(slot.GetChildCount() - 1);
				if (cartaMini == null || cartaMini.MisDatos == null) continue;

				// Obtener la escena y la imagen directamente desde el archivo .tres
				if (!string.IsNullOrEmpty(cartaMini.MisDatos.RutaEscena))
				{
					escenas.Add(cartaMini.MisDatos.RutaEscena);
					if (cartaMini.MisDatos.Imagen != null)
					{
						imagenes.Add(cartaMini.MisDatos.Imagen.ResourcePath);
					}
					else
					{
						imagenes.Add("");
					}
				}
				else
				{
					GD.PrintErr($"La carta {cartaMini.MisDatos.Nombre} no tiene RutaEscena configurada en su archivo .tres");
				}
			}
		}

		// Debe haber cartas válidas y mapeadas para iniciar batalla
		if (escenas.Count == 0)
		{
			MostrarAviso("No se pudo construir un mazo válido para batalla.");
			return;
		}
		if (escenas.Count < MIN_CARTAS)
		{
			MostrarAviso($"Tu mazo válido debe tener al menos {MIN_CARTAS} cartas.");
			return;
		}

		// Guardar en sesión y batallar
		if (SesionJuego.Instance != null)
			SesionJuego.Instance.GuardarMazo(escenas, imagenes);
		else
			GD.PrintErr("[MenuConstructor] SesionJuego.Instance es null. Se iniciará batalla sin persistir mazo.");

		GD.Print($"[MenuConstructor] Mazo de {escenas.Count} cartas → ¡A batallar!");
		GetTree().ChangeSceneToFile(rutaBatalla);
	}

	private void IniciarBatallaDirecta()
	{
		GetTree().ChangeSceneToFile(RutaBatalla);
	}

	private void ActualizarContador()
	{
		if (_panelMazo == null) return;

		int count = ContarCartasEnMazo();
		
		// Actualizar label del contador (oculto pero funcional)
		if (_lblContadorMazo != null)
		{
			_lblContadorMazo.Text = $"Cartas: {count}/{MAX_CARTAS}";
			_lblContadorMazo.Modulate = count >= MIN_CARTAS ? Colors.LightGreen : Colors.OrangeRed;
		}

		// Actualizar barra de progreso con animación (fluida)
		if (_progressBar != null)
		{
			var tween = CreateTween();
			tween.TweenProperty(_progressBar, "value", count, 0.35f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			
			// Cambiar color según progreso
			var fillStyle = _progressBar.GetThemeStylebox("fill") as StyleBoxFlat;
			if (fillStyle != null)
			{
				var newStyle = (StyleBoxFlat)fillStyle.Duplicate();
				if (count >= MAX_CARTAS)
					newStyle.BgColor = new Color(0.20f, 0.72f, 0.40f, 1f); // Verde completo
				else if (count >= MIN_CARTAS)
					newStyle.BgColor = new Color(0.30f, 0.65f, 0.85f, 1f); // Azul — suficiente
				else
					newStyle.BgColor = new Color(0.90f, 0.60f, 0.15f, 1f); // Naranja — insuficiente
				_progressBar.AddThemeStyleboxOverride("fill", newStyle);
			}
		}

		if (_btnBatallar != null)
		{
			bool puedeBatallar = count >= MIN_CARTAS;
			_btnBatallar.Disabled = !puedeBatallar;
			_btnBatallar.Modulate = puedeBatallar
				? Colors.White
				: new Color(0.7f, 0.7f, 0.7f, 1f);
		}
	}

	private int ContarCartasEnMazo()
	{
		if (_panelMazo == null) return 0;
		var grid = _panelMazo.GetNodeOrNull<GridContainer>("MarginContainer/VBoxContainer/GridMazo");
		if (grid == null) return 0;

		int count = 0;
		foreach (Node slot in grid.GetChildren())
		{
			var carta = slot.GetNodeOrNull<CartaMini>("CartaMini") ?? slot.GetChildOrNull<CartaMini>(slot.GetChildCount() - 1);
			if (carta != null) count++;
		}
		return count;
	}

	private void MostrarAviso(string msg)
	{
		// Crear label temporal de aviso
		var lbl = new Label();
		lbl.Text = msg;
		lbl.AddThemeColorOverride("font_color", Colors.OrangeRed);
		lbl.AddThemeFontSizeOverride("font_size", 18);
		lbl.Position = new Vector2(400, 20);
		lbl.ZIndex   = 100;
		AddChild(lbl);
		GetTree().CreateTimer(2.5f).Timeout += () => { if (IsInstanceValid(lbl)) lbl.QueueFree(); };
	}
}
