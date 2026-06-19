using Godot;
using System;

public partial class ColeccionPanel : PanelContainer
{
	[Export] private LineEdit _buscador;
	[Export] private GridContainer _gridCartas;
	[Export] private PackedScene _escenaCartaMini;

	// Espacios para arrastrar tus 4 botones
	[Export] private Button _btnTodas;
	[Export] private Button _btnUnidades;
	[Export] private Button _btnHechizos;
	[Export] private Button _btnEstructuras;

	// Tu base de datos con todas las cartas del juego
	[Export] private Godot.Collections.Array<CartaData> _todasLasCartas = new Godot.Collections.Array<CartaData>();

	// ¡EL MEGÁFONO! Este evento grita "¡Ey, el jugador eligió esta carta!"
	public event Action<CartaData> OnCartaElegidaParaMazo;

	private string _filtroActual = "TODAS";

	public override void _Ready()
	{
		// Conectar el buscador
		if (_buscador != null) 
		{
			_buscador.TextChanged += BuscarCartas;
		}

		// Conectar el Clic de los botones a nuestra lógica
		if (_btnTodas != null) _btnTodas.Pressed += () => CambiarFiltro("TODAS");
		if (_btnUnidades != null) _btnUnidades.Pressed += () => CambiarFiltro("UNIDAD");
		if (_btnHechizos != null) _btnHechizos.Pressed += () => CambiarFiltro("HECHIZO");
		if (_btnEstructuras != null) _btnEstructuras.Pressed += () => CambiarFiltro("ESTRUCTURA");

		CargarCartasDesdeCarpeta();
		PoblarCuadricula();
	}

	private void CargarCartasDesdeCarpeta()
	{
		string rutaCarpeta = "res://DatosCartas/";
		using var dir = DirAccess.Open(rutaCarpeta);
		if (dir != null)
		{
			dir.ListDirBegin();
			string nombreArchivo = dir.GetNext();
			while (nombreArchivo != "")
			{
				// Permitir .tres (y .tres.remap si está exportado)
				if (!dir.CurrentIsDir() && (nombreArchivo.EndsWith(".tres") || nombreArchivo.EndsWith(".tres.remap")))
				{
					string nombreReal = nombreArchivo.Replace(".remap", "");
					string rutaCompleta = rutaCarpeta + nombreReal;
					
					var recurso = ResourceLoader.Load(rutaCompleta) as CartaData;
					if (recurso != null)
					{
						bool yaExiste = false;
						foreach (var c in _todasLasCartas)
						{
							if (c != null && (c.ResourcePath == rutaCompleta || c.Nombre == recurso.Nombre))
							{
								yaExiste = true;
								break;
							}
						}
						
						if (!yaExiste)
						{
							_todasLasCartas.Add(recurso);
							GD.Print($"[ColeccionPanel] Carta cargada dinámicamente: {recurso.Nombre}");
						}
					}
				}
				nombreArchivo = dir.GetNext();
			}
		}
		else
		{
			GD.PrintErr("[ColeccionPanel] No se pudo acceder a la carpeta: " + rutaCarpeta);
		}
	}

	private void PoblarCuadricula()
	{
		// Limpiamos todo por si acaso
		foreach (Node hijo in _gridCartas.GetChildren()) 
		{ 
			hijo.QueueFree(); 
		}

		int index = 0;
		// Creamos cada carta visual basándonos en tus archivos .tres
		foreach (CartaData datosCarta in _todasLasCartas)
		{
			if (datosCarta == null) continue;

			CartaMini nuevaCarta = _escenaCartaMini.Instantiate<CartaMini>();
			_gridCartas.AddChild(nuevaCarta);
			nuevaCarta.CargarDatos(datosCarta);
			nuevaCarta.SetModoMazo(false);

			// Efecto de spawn en cascada dinámico
			nuevaCarta.Modulate = new Color(1, 1, 1, 0); 
			nuevaCarta.Scale = new Vector2(0.6f, 0.6f);
			nuevaCarta.PivotOffset = new Vector2(50f, 75f); // Centro para escalar
			var tween = nuevaCarta.CreateTween();
			float delay = index * 0.035f; 
			tween.TweenInterval(delay);
			tween.TweenProperty(nuevaCarta, "modulate", Colors.White, 0.15f);
			tween.Parallel().TweenProperty(nuevaCarta, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

			// ¡LA CONEXIÓN CLAVE! Escuchamos cuando hacen clic en ESTA carta en específico
			nuevaCarta.OnClickeada += (carta) => 
			{
				// Disparamos el evento enviando los datos de la carta
				OnCartaElegidaParaMazo?.Invoke(carta.MisDatos);
			};
			index++;
		}
	}

	private void CambiarFiltro(string nuevoFiltro)
	{
		_filtroActual = nuevoFiltro;
		// Re-filtramos usando el botón nuevo + lo que esté escrito en el buscador
		string textoBuscador = _buscador != null ? _buscador.Text : "";
		AplicarFiltros(textoBuscador); 
	}

	private void BuscarCartas(string textoEscrito)
	{
		AplicarFiltros(textoEscrito);
	}

	private void AplicarFiltros(string textoEscrito)
	{
		textoEscrito = textoEscrito.ToLower();

		foreach (Node node in _gridCartas.GetChildren())
		{
			if (node is not CartaMini cartaVisual) continue;

			// 1. ¿Coincide con el texto escrito?
			bool coincideTexto = cartaVisual.MisDatos.Nombre.ToLower().Contains(textoEscrito);

			// 2. ¿Coincide con el botón apretado abajo?
			bool coincideCategoria = true;
			if (_filtroActual != "TODAS")
			{
				string categoriaDeEstaCarta = cartaVisual.MisDatos.Categoria.ToString().ToUpper();
				coincideCategoria = (categoriaDeEstaCarta == _filtroActual);
			}

			// Muestra la carta SOLO si cumple AMBAS condiciones
			cartaVisual.Visible = coincideTexto && coincideCategoria;
		}
	}
}
