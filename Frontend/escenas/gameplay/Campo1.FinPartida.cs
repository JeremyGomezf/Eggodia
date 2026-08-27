using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ── ESTADÍSTICAS POR TROPA (MVT: Most Valuable Troop) ──────────────────
	private class RegistroTropa
	{
		public string Nombre;
		public bool EsJugador;
		public int Daño;
		public Texture2D Ilustracion;
	}
	private readonly Dictionary<ulong, RegistroTropa> _statsPorTropa = new();
	private Dictionary<string, Texture2D> _cacheIlustraciones;

	/// <summary>Acumula el daño causado por una tropa individual, para el ranking de MVT al
	/// terminar la partida. Debe llamarse en todo punto donde una tropa aplique daño, tanto
	/// desde el combate central (Campo1.Combate.cs) como desde las tropas autogestionadas.</summary>
	public void RegistrarDañoTropa(Node2D atacante, int daño)
	{
		if (!IsInstanceValid(atacante) || daño <= 0) return;
		ulong id = atacante.GetInstanceId();
		if (!_statsPorTropa.TryGetValue(id, out var reg))
		{
			reg = new RegistroTropa
			{
				Nombre      = NombreCorto(atacante),
				EsJugador   = atacante.IsInGroup("tropas_jugador"),
				Ilustracion = ObtenerIlustracionTropa(atacante),
			};
			_statsPorTropa[id] = reg;
		}
		reg.Daño += daño;
	}

	// Empareja la escena de batalla de la tropa (SceneFilePath) con la ilustración
	// definida en su CartaData (res://DatosCartas/*.tres) para mostrarla en el MVT.
	private Texture2D ObtenerIlustracionTropa(Node2D tropa)
	{
		string ruta = tropa.SceneFilePath;
		if (string.IsNullOrEmpty(ruta)) return null;

		if (_cacheIlustraciones == null)
		{
			_cacheIlustraciones = new Dictionary<string, Texture2D>();
			using var dir = DirAccess.Open("res://DatosCartas");
			if (dir != null)
			{
				dir.ListDirBegin();
				string archivo = dir.GetNext();
				while (archivo != "")
				{
					if (archivo.EndsWith(".tres"))
					{
						var datos = GD.Load<CartaData>($"res://DatosCartas/{archivo}");
						if (datos != null && !string.IsNullOrEmpty(datos.RutaEscena) && datos.Imagen != null)
							_cacheIlustraciones[datos.RutaEscena] = datos.Imagen;
					}
					archivo = dir.GetNext();
				}
			}
		}
		return _cacheIlustraciones.TryGetValue(ruta, out var tex) ? tex : null;
	}

	/// <summary>Tropa más valiosa del bando indicado (mayor daño total causado en la partida).</summary>
	private (string nombre, int daño, Texture2D ilustracion) ObtenerMVT(bool ladoJugador)
	{
		RegistroTropa mejor = null;
		foreach (var reg in _statsPorTropa.Values)
		{
			if (reg.EsJugador != ladoJugador) continue;
			if (mejor == null || reg.Daño > mejor.Daño) mejor = reg;
		}
		return mejor != null ? (mejor.Nombre, mejor.Daño, mejor.Ilustracion) : (null, 0, null);
	}

	// ── FIN DE PARTIDA ────────────────────────────────────────────────────
	private void DeterminarGanadorPorTiempo()
	{
		if (vidaJugador > vidaRival) FinalizarPartida("¡VICTORIA!");
		else if (vidaRival > vidaJugador) FinalizarPartida("¡DERROTA!");
		else FinalizarPartida("¡EMPATE!");
	}

	private void CheckEstadoJuego()
	{
		if (vidaJugador <= 0) FinalizarPartida("DERROTA");
		else if (vidaRival <= 0) FinalizarPartida("VICTORIA");
	}

	public void FinalizarPartida(string msg)
	{
		if (juegoTerminado) return;
		juegoTerminado = true;
		timerReloj.Stop();
		GetTree().Paused = false;

		// Actualizar historial de dificultad
		if (msg.Contains("VICTORIA")) _victoriasJugador++;
		else if (msg.Contains("DERROTA")) _derrotasJugador++;

		if (msg.Contains("DERROTA"))
		{
			int monedasConsuelo = Economia.Instancia().RecompensarPartida("derrota", 0);
			var escenaDerrota = GD.Load<PackedScene>("res://escenas/gameplay/PantallaDerrota.tscn");
			if (escenaDerrota != null)
			{
				var pd = escenaDerrota.Instantiate();
				if (pd is PantallaDerrota pdScript)
				{
					pdScript.MonedasGanadas   = monedasConsuelo;
					pdScript.DañoInfligido    = _dañoTotalJugador;
					pdScript.BajasEnemigas    = _tropasEliminadasRival;
					var mvtRival = ObtenerMVT(false);
					pdScript.MvtNombre      = mvtRival.nombre;
					pdScript.MvtDaño        = mvtRival.daño;
					pdScript.MvtIlustracion = mvtRival.ilustracion;
				}
				AddChild(pd);
			}
			return;
		}

		if (msg.Contains("VICTORIA"))
		{
			VerificarLogros(msg);
			var escenaVictoria = GD.Load<PackedScene>("res://escenas/gameplay/PantallaVictoria.tscn");
			int monedasGanadas = Economia.Instancia().RecompensarPartida("victoria", _rachaVictorias + 1);
			if (escenaVictoria != null)
			{
				var pv = (PantallaVictoria)escenaVictoria.Instantiate();
				pv.DañoInfligido    = _dañoTotalJugador;
				pv.TropasEliminadas = _tropasEliminadasRival;
				pv.TurnosJugados    = _turnosJugados;
				pv.Racha            = _rachaVictorias + 1;
				pv.MonedasGanadas   = monedasGanadas;
				var mvtJugador = ObtenerMVT(true);
				pv.MvtNombre      = mvtJugador.nombre;
				pv.MvtDaño        = mvtJugador.daño;
				pv.MvtIlustracion = mvtJugador.ilustracion;
				AddChild(pv);
			}

			if (SesionJuego.Instance != null)
			{
				SesionJuego.Instance.UltimoResultado    = "victoria";
				SesionJuego.Instance.DañoUltimaPartida  = _dañoTotalJugador;
				if (SesionJuego.Instance.EstaLogueado)
					EnviarResultadoBackend("victoria", _dañoTotalJugador);
			}
			return;
		}

		if (!HasNode("PantallaFinal")) return;
		var pantalla = GetNode<Control>("PantallaFinal");
		pantalla.Visible = true;

		// Resultado principal
		var lbl = GetNodeOrNull<Label>("PantallaFinal/MensajeResultado")
			   ?? GetNodeOrNull<Label>("PantallaFinal/LabelResultado");
		if (lbl != null)
		{
			lbl.Text     = msg;
			lbl.Modulate = msg.Contains("VICTORIA") ? Colors.Gold : msg.Contains("EMPATE") ? Colors.White : Colors.Red;
		}

		// ── Nombre del jugador y racha ────────────────────────────────────────
		string nombreJ = SesionJuego.Instance?.NombreJugador ?? "Jugador";
		var lblNombre = new Label();
		lblNombre.Text = nombreJ;
		lblNombre.AddThemeColorOverride("font_color", Colors.LightBlue);
		lblNombre.AddThemeFontSizeOverride("font_size", 17);
		lblNombre.Position = new Vector2(50, 55);
		pantalla.AddChild(lblNombre);

		if (_rachaVictorias > 1)
		{
			var lblRacha = new Label();
			lblRacha.Text = $"Racha: {_rachaVictorias} victorias seguidas";
			lblRacha.AddThemeColorOverride("font_color", Colors.OrangeRed);
			lblRacha.AddThemeFontSizeOverride("font_size", 16);
			lblRacha.Position = new Vector2(50, 80);
			pantalla.AddChild(lblRacha);
		}

		// ── Estadísticas ──────────────────────────────────────────────────────
		var lblStats = new Label();
		lblStats.Text = $"ESTADÍSTICAS\n" +
						$"Daño infligido:     {_dañoTotalJugador}\n" +
						$"Daño recibido:      {_dañoTotalRival}\n" +
						$"Tropas eliminadas:  {_tropasEliminadasRival}\n" +
						$"Tropas perdidas:    {_tropasEliminadasJugador}\n" +
						$"Turnos jugados:     {_turnosJugados}";
		lblStats.AddThemeColorOverride("font_color", Colors.White);
		lblStats.AddThemeFontSizeOverride("font_size", 15);
		lblStats.Position = new Vector2(50, 110);
		lblStats.AutowrapMode = TextServer.AutowrapMode.Word;
		pantalla.AddChild(lblStats);

		// ── Botones ───────────────────────────────────────────────────────────
		var btnReinicio = new Button();
		btnReinicio.Text              = "Jugar de nuevo";
		btnReinicio.Position          = new Vector2(50, 300);
		btnReinicio.CustomMinimumSize = new Vector2(190, 48);
		btnReinicio.Pressed += () => { LimpiezaEfectos.LimpiarEfectosDeCampo(); GetTree().ReloadCurrentScene(); };
		pantalla.AddChild(btnReinicio);

		var btnMenu = new Button();
		btnMenu.Text              = "Menú Principal";
		btnMenu.Position          = new Vector2(255, 300);
		btnMenu.CustomMinimumSize = new Vector2(190, 48);
		btnMenu.Pressed += () => { LimpiezaEfectos.LimpiarEfectosDeCampo(); GetTree().ChangeSceneToFile("res://escenas/menu/menu_principal.tscn"); };
		pantalla.AddChild(btnMenu);

		if (SesionJuego.Instance?.EstaLogueado == true)
		{
			var btnRanking = new Button();
			btnRanking.Text              = "Ver Ranking";
			btnRanking.Position          = new Vector2(50, 358);
			btnRanking.CustomMinimumSize = new Vector2(395, 44);
			btnRanking.SelfModulate      = Colors.Gold;
			btnRanking.Pressed += () =>
			{
				var panel = new PanelRanking();
				pantalla.AddChild(panel);
				panel.Mostrar();
			};
			pantalla.AddChild(btnRanking);
		}

		// Guardar resultado en sesión y enviar al backend
		string resultadoStr = msg.Contains("VICTORIA") ? "victoria"
							: msg.Contains("EMPATE")   ? "empate" : "derrota";
		VerificarLogros(msg);
		if (SesionJuego.Instance != null)
		{
			SesionJuego.Instance.UltimoResultado    = resultadoStr;
			SesionJuego.Instance.DañoUltimaPartida  = _dañoTotalJugador;
			if (SesionJuego.Instance.EstaLogueado)
				EnviarResultadoBackend(resultadoStr, _dañoTotalJugador);
		}
	}

	private void EnviarResultadoBackend(string resultado, int daño)
	{
		var http = new Godot.HttpRequest();
		AddChild(http);
		string json = System.Text.Json.JsonSerializer.Serialize(new {
			UsuarioId = SesionJuego.Instance!.UsuarioId,
			Resultado = resultado,
			DañoHecho = daño
		});
		string[] h = { "Content-Type: application/json" };
		http.Request("http://localhost:5289/api/usuarios/resultado", h, HttpClient.Method.Post, json);
		GD.Print($"[Campo1] Resultado → backend: {resultado}");
	}

	// ── IDENTIDAD VISUAL POR ERA ─────────────────────────────────────────
	private void AplicarIdentidadEra()
	{
		if (SesionJuego.Instance == null || !SesionJuego.Instance.TieneMazo) return;

		var escenas = SesionJuego.Instance.MazoSeleccionado;
		int era1 = 0, era2 = 0, era3 = 0;
		foreach (string e in escenas)
		{
			if (e.Contains("TRex") || e.Contains("Tiburon") || e.Contains("CalamarG")) era1++;
			else if (e.Contains("Torre") || e.Contains("Caballo") || e.Contains("Dama") ||
					 e.Contains("SoldadoReal") || e.Contains("Peon") || e.Contains("Encebollado")) era2++;
			else era3++;
		}

		string nombreEra = era1 >= era2 && era1 >= era3 ? "Era Primordial"
						 : era2 >= era1 && era2 >= era3 ? "Era Medieval"
														: "Era Mística";

		// Mostrar nombre de la era al inicio
		var aviso = new Label();
		aviso.Text = nombreEra;
		aviso.AddThemeFontSizeOverride("font_size", 28);
		aviso.AddThemeColorOverride("font_color", Colors.Gold);
		aviso.Position = new Vector2(450, 250);
		aviso.ZIndex   = 100;
		AddChild(aviso);
		Tween tw = CreateTween();
		tw.TweenInterval(1.5f);
		tw.TweenProperty(aviso, "modulate:a", 0.0f, 0.8f);
		tw.Finished += () => { if (IsInstanceValid(aviso)) aviso.QueueFree(); };
	}

	// ── CPU HECHIZOS ─────────────────────────────────────────────────────
	private void CPUUsarHechizo()
	{
		if (_hechizoUsadoEsteTurno) return; // mismo límite de 1 hechizo/trampa por turno que el jugador

		// Buscar tropa del jugador con más vida para envenenaría
		Node2D objetivo = null;
		int maxVida = 0;
		foreach (Node n in GetTree().GetNodesInGroup("tropas_jugador"))
		{
			if (!(n is Node2D t) || !IsInstanceValid(t)) continue;
			int v = Gi(t, "vidaActual");
			if (v > maxVida) { maxVida = v; objetivo = t; }
		}
		if (objetivo == null) return;

		_hechizoUsadoEsteTurno = true;

		// Alternar entre veneno y bloqueo
		if (random.Next(2) == 0)
		{
			objetivo.SetMeta("envenenado",   true);
			objetivo.SetMeta("dañoVeneno",   40);
			objetivo.SetMeta("turnosVeneno", 2);
			objetivo.Modulate = new Color(0.6f, 1f, 0.4f);
			ActualizarIconosEstado(objetivo);
		}
		else
		{
			objetivo.SetMeta("bloqueado",     true);
			objetivo.SetMeta("turnosBloqueo", 1);
			objetivo.Modulate = new Color(0.4f, 0.6f, 1.4f);
			ActualizarIconosEstado(objetivo);
		}
	}

	// ── IA PRIORIZA TROPAS DÉBILES ────────────────────────────────────────
	private Node2D BuscarObjetivoDebilEnCarril(Node2D atacante, string grupo)
	{
		string carril = ((string)atacante.GetMeta("carril")).ToLower().Replace("modrival","").Replace("mod","");
		Node2D mejor = null; int min = int.MaxValue;
		foreach (Node n in GetTree().GetNodesInGroup(grupo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			if (((string)e.GetMeta("carril")).ToLower().Replace("modrival","").Replace("mod","") != carril) continue;
			int v = Gi(e, "vidaActual");
			if (v < min) { min = v; mejor = e; }
		}
		return mejor;
	}
}
