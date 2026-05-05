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

		PoblarCuadricula();
	}

	private void PoblarCuadricula()
	{
		// Limpiamos todo por si acaso
		foreach (Node hijo in _gridCartas.GetChildren()) 
		{ 
			hijo.QueueFree(); 
		}

		// Creamos cada carta visual basándonos en tus archivos .tres
		foreach (CartaData datosCarta in _todasLasCartas)
		{
			if (datosCarta == null) continue;

			CartaMini nuevaCarta = _escenaCartaMini.Instantiate<CartaMini>();
			_gridCartas.AddChild(nuevaCarta);
			nuevaCarta.CargarDatos(datosCarta);

			// ¡LA CONEXIÓN CLAVE! Escuchamos cuando hacen clic en ESTA carta en específico
			nuevaCarta.OnClickeada += (carta) => 
			{
				// Disparamos el evento enviando los datos de la carta
				OnCartaElegidaParaMazo?.Invoke(carta.MisDatos);
			};
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

		foreach (CartaMini cartaVisual in _gridCartas.GetChildren())
		{
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
