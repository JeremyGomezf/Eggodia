using System;
using Godot;
using System.Text;
using System.Text.Json;

/// <summary>
/// PanelLogin — pantalla de login/registro.
/// Se muestra antes del menú principal.
/// Conecta con el backend Eggodia.
///
/// Nodos requeridos en la escena:
///   TabContainer (con tabs "Iniciar Sesión" y "Registrarse")
///   LOGIN:
///     LineEdit "EmailLogin", LineEdit "PasswordLogin"
///     Button "BtnLogin", Label "LblErrorLogin"
///   REGISTRO:
///     LineEdit "NombreReg", LineEdit "EmailReg", LineEdit "PasswordReg"
///     Button "BtnRegistro", Label "LblErrorRegistro"
///   Button "BtnContinuarSinLogin" (jugar como invitado)
/// </summary>
public partial class PanelLogin : Control
{
	private static string URL_BASE => ApiConfig.Usuarios;

	// ── NODOS LOGIN ───────────────────────────────────────────────────────
	private LineEdit _emailLogin;
	private LineEdit _passLogin;
	private Button   _btnLogin;
	private Label    _lblErrorLogin;

	// ── NODOS REGISTRO ────────────────────────────────────────────────────
	private LineEdit _nombreReg;
	private LineEdit _emailReg;
	private LineEdit _passReg;
	private Button   _btnRegistro;
	private Label    _lblErrorRegistro;

	// ── INVITADO ──────────────────────────────────────────────────────────
	private Button _btnInvitado;

	// ── OJO MOSTRAR/OCULTAR CONTRASEÑA ────────────────────────────────────
	private Texture2D _ojoVer;      // icono cuando la contraseña está oculta
	private Texture2D _ojoOcultar;  // icono cuando la contraseña está visible

	private Godot.HttpRequest _http;
	private string _accionPendiente = ""; // "login" o "registro"

	[Export] public string RutaMenuPrincipal = "res://escenas/menu/menu_principal.tscn";

	// Factor para agrandar TODAS las letras del login (es una pantalla de teléfono).
	[Export] public float EscalaFuenteMovil = 1.35f;

	public override void _Ready()
	{
		// Agrandar las letras para que se lean bien en el celular
		EscalarFuentes(this);

		_http = new Godot.HttpRequest();
		AddChild(_http);
		_http.RequestCompleted += OnRespuestaHTTP;

		// Si ya hay sesión, saltar directo al menú
		if (SesionJuego.Instance != null && SesionJuego.Instance.EstaLogueado)
		{
			IrAlMenu(); return;
		}

		// Conectar nodos de login
		_emailLogin    = GetNodeOrNull<LineEdit>("TabContainer/Login/EmailLogin");
		_passLogin     = GetNodeOrNull<LineEdit>("TabContainer/Login/PasswordLogin");
		_btnLogin      = GetNodeOrNull<Button>("TabContainer/Login/BtnLogin");
		_lblErrorLogin = GetNodeOrNull<Label>("TabContainer/Login/LblErrorLogin");

		// Conectar nodos de registro
		_nombreReg      = GetNodeOrNull<LineEdit>("TabContainer/Registro/NombreReg");
		_emailReg       = GetNodeOrNull<LineEdit>("TabContainer/Registro/EmailReg");
		_passReg        = GetNodeOrNull<LineEdit>("TabContainer/Registro/PasswordReg");
		_btnRegistro    = GetNodeOrNull<Button>("TabContainer/Registro/BtnRegistro");
		_lblErrorRegistro = GetNodeOrNull<Label>("TabContainer/Registro/LblErrorRegistro");

		_btnInvitado = GetNodeOrNull<Button>("BtnContinuarSinLogin");

		if (_btnLogin    != null) _btnLogin.Pressed    += IntentarLogin;
		if (_btnRegistro != null) _btnRegistro.Pressed += IntentarRegistro;
		if (_btnInvitado != null) _btnInvitado.Pressed += JugarComoInvitado;

		// Botón de ojo para mostrar/ocultar contraseña
		_ojoVer     = GD.Load<Texture2D>("res://imagenes/login/icono_ojo.png");
		_ojoOcultar = GD.Load<Texture2D>("res://imagenes/login/icono_ojo_off.png");
		ConfigurarOjo(_passLogin);
		ConfigurarOjo(_passReg);

		// Ocultar errores al inicio
		if (_lblErrorLogin    != null) _lblErrorLogin.Visible    = false;
		if (_lblErrorRegistro != null) _lblErrorRegistro.Visible = false;
	}

	// ── LOGIN ─────────────────────────────────────────────────────────────
	private void IntentarLogin()
	{
		if (_emailLogin == null || _passLogin == null) return;
		string email = _emailLogin.Text.Trim();
		string pass  = _passLogin.Text;

		if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass))
		{
			MostrarError(_lblErrorLogin, "Completa todos los campos.");
			return;
		}

		_accionPendiente = "login";
		string json = JsonSerializer.Serialize(new { Email = email, Password = pass });
		EnviarPost($"{URL_BASE}/login", json);
		if (_btnLogin != null) { _btnLogin.Disabled = true; _btnLogin.Text = "Entrando..."; }
	}

	// ── REGISTRO ──────────────────────────────────────────────────────────
	private void IntentarRegistro()
	{
		if (_nombreReg == null || _emailReg == null || _passReg == null) return;
		string nombre = _nombreReg.Text.Trim();
		string email  = _emailReg.Text.Trim();
		string pass   = _passReg.Text;

		if (string.IsNullOrEmpty(nombre) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass))
		{
			MostrarError(_lblErrorRegistro, "Completa todos los campos.");
			return;
		}

		_accionPendiente = "registro";
		string json = JsonSerializer.Serialize(new { Nombre = nombre, Email = email, Password = pass });
		EnviarPost($"{URL_BASE}/registro", json);
		if (_btnRegistro != null) { _btnRegistro.Disabled = true; _btnRegistro.Text = "Registrando..."; }
	}

	// ── INVITADO ──────────────────────────────────────────────────────────
	private void JugarComoInvitado()
	{
		if (SesionJuego.Instance != null)
		{
			SesionJuego.Instance.UsuarioId    = -1;
			SesionJuego.Instance.NombreJugador = "Invitado";
		}
		IrAlMenu();
	}

	// ── OJO: alterna mostrar/ocultar la contraseña del campo ──────────────
	private void ConfigurarOjo(LineEdit campo)
	{
		if (campo == null) return;
		var btn = campo.GetNodeOrNull<TextureButton>("EyeToggle");
		if (btn == null) return;

		btn.TextureNormal = _ojoVer; // arranca oculta (secret = true)
		btn.Pressed += () =>
		{
			campo.Secret = !campo.Secret;
			btn.TextureNormal = campo.Secret ? _ojoVer : _ojoOcultar;
		};
	}

	// ── Agranda las letras de todos los controles del login (para teléfono) ──
	private void EscalarFuentes(Node nodo)
	{
		if (nodo is Label || nodo is Button || nodo is LineEdit || nodo is TabContainer)
		{
			var c = (Control)nodo;
			int actual = c.GetThemeFontSize("font_size");
			if (actual > 0)
				c.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(actual * EscalaFuenteMovil));
		}
		foreach (var hijo in nodo.GetChildren())
			EscalarFuentes(hijo);
	}

	// ── RESPUESTA HTTP ────────────────────────────────────────────────────
	private void OnRespuestaHTTP(long result, long code, string[] headers, byte[] body)
	{
		// Rehabilitar botones
		if (_btnLogin    != null) { _btnLogin.Disabled    = false; _btnLogin.Text    = "Iniciar Sesión"; }
		if (_btnRegistro != null) { _btnRegistro.Disabled = false; _btnRegistro.Text = "Registrarse"; }

		if (result != (long)Godot.HttpRequest.Result.Success || (code != 200 && code != 201))
		{
			string msg = "Error de conexión con el servidor.";
			try
			{
				string resp = Encoding.UTF8.GetString(body);
				var data = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(resp);
				if (data != null && data.ContainsKey("mensaje")) msg = data["mensaje"];
			}
			catch { }

			if (_accionPendiente == "login")  MostrarError(_lblErrorLogin,    msg);
			else                               MostrarError(_lblErrorRegistro, msg);
			return;
		}

		try
		{
			string json = Encoding.UTF8.GetString(body);
			var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			var usuario = JsonSerializer.Deserialize<UsuarioRespuesta>(json, opts);

			if (usuario != null && SesionJuego.Instance != null)
			{
				SesionJuego.Instance.UsuarioId     = usuario.Id;
				SesionJuego.Instance.NombreJugador  = usuario.Nombre;
				GD.Print($"[Login] ¡Bienvenido, {usuario.Nombre}! (ID: {usuario.Id})");
				IrAlMenu();
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[Login] Error parseando respuesta: {e.Message}");
			MostrarError(_lblErrorLogin, "Error procesando respuesta.");
		}
	}

	private void MostrarError(Label lbl, string msg)
	{
		if (lbl == null) return;
		lbl.Text    = msg;
		lbl.Visible = true;
	}

	private void EnviarPost(string url, string json)
	{
		string[] headers = { "Content-Type: application/json" };
		_http.Request(url, headers, HttpClient.Method.Post, json);
	}

	private void IrAlMenu()
	{
		GetTree().ChangeSceneToFile(RutaMenuPrincipal);
	}

	// DTO para parsear respuesta del backend
	private class UsuarioRespuesta
	{
		public int    Id      { get; set; }
		public string Nombre  { get; set; } = "";
		public string Email   { get; set; } = "";
		public int    Victorias { get; set; }
		public int    Derrotas  { get; set; }
	}
}
