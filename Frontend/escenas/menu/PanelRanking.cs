using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

/// <summary>
/// PanelRanking — tabla de líderes completamente autónoma.
/// Crea todos sus nodos en código — no necesita escena ni nodos hijos.
/// Se usa así: var panel = new PanelRanking(); AddChild(panel); panel.Mostrar();
/// </summary>
public partial class PanelRanking : Control
{
	private static string URL_RANKING => ApiConfig.Ranking;

	private VBoxContainer     _lista;
	private Godot.HttpRequest _http;
	private bool              _construido = false;

	public override void _Ready()
	{
		ConstruirUI();
	}

	private void ConstruirUI()
	{
		if (_construido) return;
		_construido = true;

		// Tamaño y posición centrada
		SetAnchorsPreset(LayoutPreset.FullRect);
		ZIndex = 150;

		// Fondo oscuro semitransparente
		var fondo = new ColorRect();
		fondo.SetAnchorsPreset(LayoutPreset.FullRect);
		fondo.Color = new Color(0f, 0f, 0f, 0.85f);
		fondo.MouseFilter = MouseFilterEnum.Stop;
		AddChild(fondo);

		// Panel central — MARCO DORADO del juego (el del login, con la gema de huevo).
		var panel = new PanelContainer();
		panel.SetAnchorsPreset(LayoutPreset.Center);
		panel.CustomMinimumSize = new Vector2(620, 580);
		panel.GrowHorizontal = GrowDirection.Both;   // se auto-centra aunque el marco crezca
		panel.GrowVertical   = GrowDirection.Both;
		EstiloUI.MarcoDorado(panel, padX: 44);
		AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 4);
		panel.AddChild(vbox);

		// Título
		var titulo = new Label();
		titulo.Text = "🏆 RANKING — EGGODIA";
		titulo.HorizontalAlignment = HorizontalAlignment.Center;
		EstiloUI.Titulo(titulo, 22);
		vbox.AddChild(titulo);

		vbox.AddChild(new HSeparator());

		// Scroll para la lista
		var scroll = new ScrollContainer();
		scroll.CustomMinimumSize = new Vector2(0, 360);
		vbox.AddChild(scroll);

		_lista = new VBoxContainer();
		_lista.AddThemeConstantOverride("separation", 2);
		scroll.AddChild(_lista);

		vbox.AddChild(new HSeparator());

		// Botón cerrar
		var btnCerrar = new Button();
		btnCerrar.Text              = "✕ Cerrar";
		btnCerrar.CustomMinimumSize = new Vector2(150, 40);
		EstiloUI.Boton(btnCerrar, 16);
		btnCerrar.Pressed += () => QueueFree();

		var hboxCerrar = new HBoxContainer();
		hboxCerrar.Alignment = BoxContainer.AlignmentMode.Center;
		hboxCerrar.AddChild(btnCerrar);
		vbox.AddChild(hboxCerrar);

		// HTTP
		_http = new Godot.HttpRequest();
		AddChild(_http);
		_http.RequestCompleted += OnRankingRecibido;

		// Encabezado
		AgregarFila("POS", "JUGADOR", "V", "D", "W%", esEncabezado: true);
		AgregarSeparador();
	}

	public void Mostrar()
	{
		if (!_construido) ConstruirUI();
		Visible = true;

		// Limpiar lista y recargar
		if (_lista != null)
			foreach (Node n in _lista.GetChildren()) n.QueueFree();

		AgregarFila("POS", "JUGADOR", "V", "D", "W%", esEncabezado: true);
		AgregarSeparador();

		// Mostrar "cargando..." mientras espera
		var lblCargando = new Label();
		lblCargando.Name = "LblCargando";
		lblCargando.Text = "⏳ Cargando ranking...";
		lblCargando.AddThemeColorOverride("font_color", Colors.LightGray);
		lblCargando.HorizontalAlignment = HorizontalAlignment.Center;
		_lista.AddChild(lblCargando);

		CargarRanking();
	}

	private void CargarRanking()
	{
		var err = _http.Request(URL_RANKING);
		if (err != Error.Ok)
		{
			LimpiarCargando();
			AgregarFila("—", "Backend no disponible", "—", "—", "—", false);
		}
	}

	private void OnRankingRecibido(long result, long code, string[] headers, byte[] body)
	{
		LimpiarCargando();

		if (result != (long)Godot.HttpRequest.Result.Success || code != 200)
		{
			AgregarFila("—", "Sin conexión al servidor", "—", "—", "—", false);
			return;
		}

		try
		{
			string json = Encoding.UTF8.GetString(body);
			var opts    = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			var lista   = JsonSerializer.Deserialize<List<EntradaRanking>>(json, opts);

			if (lista == null || lista.Count == 0)
			{
				AgregarFila("—", "Aún no hay jugadores", "—", "—", "—", false);
				return;
			}

			int pos = 1;
			foreach (var e in lista)
			{
				string medalla = pos switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => $"{pos}." };
				bool   esYo    = SesionJuego.Instance?.UsuarioId == e.Id;
				AgregarFila(
					medalla,
					e.Nombre + (esYo ? " ◀" : ""),
					e.Victorias.ToString(),
					e.Derrotas.ToString(),
					$"{e.WinRate}%",
					false, esYo
				);
				pos++;
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[Ranking] {ex.Message}");
			AgregarFila("—", "Error al procesar datos", "—", "—", "—", false);
		}
	}

	private void LimpiarCargando()
	{
		_lista?.GetNodeOrNull("LblCargando")?.QueueFree();
	}

	private void AgregarFila(string pos, string nombre, string v, string d, string wr,
							  bool esEncabezado, bool resaltar = false)
	{
		if (_lista == null) return;

		var hbox = new HBoxContainer();
		hbox.CustomMinimumSize = new Vector2(0, 30);

		Color color = esEncabezado ? Colors.Gold
					: resaltar     ? new Color(0.3f, 1f, 0.5f)
								   : Colors.White;

		Celda(hbox, pos,    55,  color, esEncabezado);
		Celda(hbox, nombre, 230, color, esEncabezado);
		Celda(hbox, v,      55,  color, esEncabezado);
		Celda(hbox, d,      55,  color, esEncabezado);
		Celda(hbox, wr,     65,  color, esEncabezado);

		_lista.AddChild(hbox);
	}

	private void Celda(HBoxContainer parent, string texto, int ancho, Color color, bool negrita)
	{
		var lbl = new Label();
		lbl.Text              = texto;
		lbl.CustomMinimumSize = new Vector2(ancho, 0);
		if (EstiloUI.Fuente != null) lbl.AddThemeFontOverride("font", EstiloUI.Fuente); // fuente del juego
		lbl.AddThemeColorOverride("font_color", color);
		if (negrita) lbl.AddThemeFontSizeOverride("font_size", 15);
		parent.AddChild(lbl);
	}

	private void AgregarSeparador() => _lista?.AddChild(new HSeparator());

	private class EntradaRanking
	{
		public int    Id        { get; set; }
		public string Nombre    { get; set; } = "";
		public int    Victorias { get; set; }
		public int    Derrotas  { get; set; }
		public int    Empates   { get; set; }
		public int    DañoTotal { get; set; }
		public int    WinRate   { get; set; }
	}
}
