using Godot;
using System.Text;
using System.Text.Json;

/// <summary>
/// Fase 1 del multijugador: emparejamiento por sondeo (polling REST) contra el backend.
/// Entra a la cola (POST /api/match/cola), y mientras espera consulta el estado cada 1.5s
/// (GET /api/match/{id}). Cuando el rival entra, muestra "¡RIVAL ENCONTRADO!".
///
/// Es un nodo autocontenido: crea su propia CanvasLayer con la UI y se libera al cancelar/volver.
/// La Fase 2 (sincronizar la partida real) arrancará desde aquí cuando el estado sea "emparejado".
/// </summary>
public partial class MatchmakingOnline : Node
{
	private static string _idSesion = ""; // id de jugador estable durante toda la sesión

	private HttpRequest _http;
	private Timer _timerSondeo;
	private bool _ocupado = false;

	// Cancelación de la búsqueda (salir de la cola): espera la confirmación del server para no dejar
	// una partida "fantasma", con feedback "Cancelando…". _cerrado hace idempotente el cierre.
	private HttpRequest _httpCancelar;
	private bool _cancelando = false;
	private bool _cerrado = false;

	private string _jugadorId = "";
	private string _nombre = "Jugador";
	private string _matchId = "";
	private string _estado = "";
	private string _asiento = "A";
	private string _semilla = "";
	private ulong _inicioBusquedaMs = 0; // para variar el mensaje del que se quedó sin rival (nº impar)

	// UI
	private Label _lblEstado, _lblDetalle;
	private Control _spinner;
	private Button _btnCancelar, _btnVolver;

	public override void _Ready()
	{
		// Id de jugador: si está logueado usa su id; si es invitado, un GUID estable por sesión.
		var ses = SesionJuego.Instance;
		if (ses != null && ses.UsuarioId > 0) { _jugadorId = $"u{ses.UsuarioId}"; _nombre = ses.NombreJugador; }
		else
		{
			if (string.IsNullOrEmpty(_idSesion)) _idSesion = System.Guid.NewGuid().ToString("N").Substring(0, 10);
			_jugadorId = $"g{_idSesion}";
			_nombre = ses != null && !string.IsNullOrEmpty(ses.NombreJugador) ? ses.NombreJugador : "Invitado";
		}

		ConstruirUI();

		_http = new HttpRequest();
		AddChild(_http);
		_http.RequestCompleted += OnRespuesta;

		_timerSondeo = new Timer { WaitTime = 1.5, OneShot = false };
		AddChild(_timerSondeo);
		_timerSondeo.Timeout += Sondear;

		EntrarACola();
	}

	private void EntrarACola()
	{
		if (_ocupado) return;
		_ocupado = true;
		string cuerpo = JsonSerializer.Serialize(new {
			jugadorId = _jugadorId,
			nombre = _nombre,
			skinIdx = Preferencias.SkinActivaIdx,
			tronoIdx = Preferencias.TronoActivoIdx
		});
		string[] headers = { "Content-Type: application/json" };
		if (_http.Request($"{ApiConfig.Base}/api/match/cola", headers, HttpClient.Method.Post, cuerpo) != Error.Ok)
		{
			_ocupado = false;
			MostrarError("Revisa tu conexión a internet e inténtalo de nuevo.");
		}
	}

	private void Sondear()
	{
		if (_ocupado || string.IsNullOrEmpty(_matchId) || _estado == "emparejado") return;
		_ocupado = true;
		if (_http.Request($"{ApiConfig.Base}/api/match/{_matchId}?jugadorId={_jugadorId}") != Error.Ok)
			_ocupado = false; // reintenta en el próximo tick
	}

	private void OnRespuesta(long result, long code, string[] headers, byte[] body)
	{
		_ocupado = false;
		if (result != (long)HttpRequest.Result.Success || (code != 200 && code != 201))
		{
			// Un fallo puntual de sondeo no es fatal si ya estamos en cola; solo error si aún no hay match.
			if (string.IsNullOrEmpty(_matchId)) MostrarError("Revisa tu conexión a internet e inténtalo de nuevo.");
			return;
		}
		try
		{
			var doc = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(body));
			_matchId = doc.GetProperty("matchId").GetString() ?? _matchId;
			_estado  = doc.GetProperty("estado").GetString() ?? "";
			string rival = doc.TryGetProperty("rival", out var r) ? (r.GetString() ?? "") : "";
			_asiento = doc.TryGetProperty("asiento", out var a) ? (a.GetString() ?? "A") : "A";
			_semilla = doc.TryGetProperty("semilla", out var s) ? (s.GetString() ?? "") : "";
			int rivalSkinIdx  = doc.TryGetProperty("rivalSkinIdx", out var rs) ? rs.GetInt32() : 0;
			int rivalTronoIdx = doc.TryGetProperty("rivalTronoIdx", out var rt) ? rt.GetInt32() : 0;

			if (_estado == "emparejado")
			{
				if (!_timerSondeo.IsStopped()) _timerSondeo.Stop();
				MostrarEmparejado(rival, rivalSkinIdx, rivalTronoIdx);
			}
			else // esperando: emparejamiento aleatorio; si el nº de jugadores es impar, uno queda sin
			     // rival y sigue en cola hasta que entre otro (no se lo saca ni se lo empareja con nadie).
			{
				if (_inicioBusquedaMs == 0) _inicioBusquedaMs = Time.GetTicksMsec();
				ulong seg = (Time.GetTicksMsec() - _inicioBusquedaMs) / 1000;
				_lblDetalle.Text = seg < 15
					? "buscando rival…"
					: "no hay más jugadores por ahora… seguimos buscando";
				if (_timerSondeo.IsStopped()) _timerSondeo.Start();
			}
		}
		catch { if (string.IsNullOrEmpty(_matchId)) MostrarError("Respuesta inválida del servidor"); }
	}

	private void Cancelar()
	{
		if (_cancelando) return;
		_cancelando = true;

		// Dejar de sondear: ya no estamos buscando.
		if (_timerSondeo != null && !_timerSondeo.IsStopped()) _timerSondeo.Stop();

		// Feedback claro de que estamos saliendo de la búsqueda.
		if (_spinner != null)     _spinner.Visible = false;
		if (_lblEstado != null)   _lblEstado.Text  = "CANCELANDO…";
		if (_lblDetalle != null)  _lblDetalle.Text = "Saliendo de la búsqueda…";
		if (_btnCancelar != null) { _btnCancelar.Disabled = true; _btnCancelar.Modulate = new Color(1, 1, 1, 0.5f); }

		// Avisar al servidor para salir de la cola y ESPERAR a que confirme antes de cerrar. Antes se
		// cerraba de inmediato (fire-and-forget), lo que liberaba el HttpRequest antes de enviarse: la
		// partida en espera quedaba "fantasma" y el próximo jugador se emparejaba con alguien ya ido.
		if (!string.IsNullOrEmpty(_matchId) && _estado != "emparejado")
		{
			string cuerpo = JsonSerializer.Serialize(new { jugadorId = _jugadorId });
			string[] headers = { "Content-Type: application/json" };
			_httpCancelar = new HttpRequest();
			AddChild(_httpCancelar);
			_httpCancelar.RequestCompleted += (long r, long c, string[] hd, byte[] bd) => Cerrar();
			if (_httpCancelar.Request($"{ApiConfig.Base}/api/match/{_matchId}/cancelar", headers, HttpClient.Method.Post, cuerpo) != Error.Ok)
				Cerrar();
			// Red de seguridad: si el server no responde en 2.5s, cerramos igual.
			GetTree().CreateTimer(2.5).Timeout += Cerrar;
		}
		else
		{
			Cerrar();
		}
	}

	private void Cerrar()
	{
		if (_cerrado) return;   // idempotente (lo llaman la respuesta del server y el timeout de respaldo)
		_cerrado = true;
		QueueFree(); // libera este nodo y su CanvasLayer hija → vuelve a verse el menú
	}

	// ── UI ──────────────────────────────────────────────────────────────────
	private void ConstruirUI()
	{
		var capa = new CanvasLayer { Layer = 300 };
		AddChild(capa);

		var fondo = new TextureRect();
		fondo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		fondo.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		fondo.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
		var tex = GD.Load<Texture2D>("res://imagenes/Fondo_de_pantalla_eggodia.png");
		if (tex != null) fondo.Texture = tex;
		capa.AddChild(fondo);

		var oscurecer = new ColorRect();
		oscurecer.Color = new Color(0, 0, 0, 0.65f);
		oscurecer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		oscurecer.MouseFilter = Control.MouseFilterEnum.Stop;
		fondo.AddChild(oscurecer);

		var centro = new VBoxContainer();
		centro.SetAnchorsPreset(Control.LayoutPreset.Center);
		centro.OffsetLeft = -360; centro.OffsetRight = 360;
		centro.OffsetTop = -250; centro.OffsetBottom = 250;
		centro.Alignment = BoxContainer.AlignmentMode.Center;
		centro.AddThemeConstantOverride("separation", 30);
		oscurecer.AddChild(centro);

		_spinner = new Control { CustomMinimumSize = new Vector2(130, 130) };
		_spinner.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		centro.AddChild(_spinner);
		const int nP = 8; const float radio = 52f, c = 65f;
		for (int i = 0; i < nP; i++)
		{
			float ang = i * Mathf.Tau / nP;
			var punto = new Panel { Size = new Vector2(20, 20) };
			punto.Position = new Vector2(c + Mathf.Cos(ang) * radio - 10, c + Mathf.Sin(ang) * radio - 10);
			var sb = new StyleBoxFlat { BgColor = new Color(0.95f, 0.76f, 0.25f) };
			sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 10;
			punto.AddThemeStyleboxOverride("panel", sb);
			_spinner.AddChild(punto);
			Tween tw = punto.CreateTween().SetLoops();
			tw.TweenProperty(punto, "modulate:a", 0.15f, 0.5f).SetDelay(i * (0.9f / nP));
			tw.TweenProperty(punto, "modulate:a", 1.0f, 0.5f);
		}

		_lblEstado = new Label { Text = "BUSCANDO RIVAL" };
		_lblEstado.AddThemeColorOverride("font_color", Colors.White);
		_lblEstado.AddThemeColorOverride("font_outline_color", Colors.Black);
		_lblEstado.AddThemeConstantOverride("outline_size", 6);
		_lblEstado.AddThemeFontSizeOverride("font_size", 56);
		_lblEstado.HorizontalAlignment = HorizontalAlignment.Center;
		centro.AddChild(_lblEstado);

		_lblDetalle = new Label { Text = "conectando…" };
		_lblDetalle.AddThemeColorOverride("font_color", new Color(0.85f, 0.88f, 0.95f));
		_lblDetalle.AddThemeFontSizeOverride("font_size", 30);
		_lblDetalle.HorizontalAlignment = HorizontalAlignment.Center;
		centro.AddChild(_lblDetalle);

		_btnCancelar = CrearBoton("CANCELAR", new Color(0.32f, 0.32f, 0.38f));
		_btnCancelar.Pressed += Cancelar;
		centro.AddChild(_btnCancelar);

		_btnVolver = CrearBoton("VOLVER", new Color(0.62f, 0.16f, 0.16f));
		_btnVolver.Visible = false;
		_btnVolver.Pressed += Cerrar;
		centro.AddChild(_btnVolver);
	}

	private static Button CrearBoton(string texto, Color color)
	{
		var b = new Button { Text = texto };
		b.CustomMinimumSize = new Vector2(300, 84);
		b.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		b.AddThemeFontSizeOverride("font_size", 32);
		var sb = new StyleBoxFlat { BgColor = color };
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 12;
		b.AddThemeStyleboxOverride("normal", sb);
		return b;
	}

	private void MostrarEmparejado(string rival, int rivalSkinIdx, int rivalTronoIdx)
	{
		if (_spinner != null) _spinner.Visible = false;
		_lblEstado.Text = "¡RIVAL ENCONTRADO!";
		_lblEstado.AddThemeColorOverride("font_color", new Color(0.5f, 1f, 0.55f));
		// No se muestra el nombre del rival aquí: recién se ve dentro de la partida (sobre su barra).
		_lblDetalle.Text = "Entrando a la partida…";
		_btnCancelar.Visible = false;
		_btnVolver.Visible = false;

		// Guardar el contexto de la partida en línea y entrar a Campo1 tras una breve pausa.
		ContextoOnline.Activo      = true;
		ContextoOnline.MatchId     = _matchId;
		ContextoOnline.JugadorId   = _jugadorId;
		ContextoOnline.Asiento     = _asiento;
		ContextoOnline.Semilla     = _semilla;
		ContextoOnline.RivalNombre = string.IsNullOrEmpty(rival) ? "Rival" : rival;
		ContextoOnline.RivalSkinIdx  = rivalSkinIdx;
		ContextoOnline.RivalTronoIdx = rivalTronoIdx;

		GetTree().CreateTimer(1.6).Timeout += () =>
		{
			if (IsInstanceValid(this)) GetTree().ChangeSceneToFile("res://escenas/gameplay/campo_1.tscn");
		};
	}

	private void MostrarError(string msg)
	{
		if (_timerSondeo != null && !_timerSondeo.IsStopped()) _timerSondeo.Stop();
		if (_spinner != null) _spinner.Visible = false;
		_lblEstado.Text = "SIN INTERNET";
		_lblEstado.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.45f));
		_lblDetalle.Text = msg;
		_btnCancelar.Visible = false;
		_btnVolver.Visible = true;
	}
}
