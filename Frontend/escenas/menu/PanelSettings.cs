using Godot;
using System;

public partial class PanelSettings : PanelContainer
{
	private const string RUTA_COMO_JUGAR = "res://escenas/menu/PantallaComoJugar.tscn";
	private const string RUTA_LOGIN      = "res://escenas/menu/PanelLogin.tscn";

	private HSlider _sliderVolumen;
	private Button _btnMute;
	private Button _btnCerrar;
	private Button _btnComoJugar;
	private Button _btnCerrarSesion;
	private CheckButton _chkScreenShake;

	public static bool ScreenShakeEnabled { get; set; } = true;

	// Se invoca al cerrar el panel (botón CERRAR / Ocultar). Lo usa el menú de pausa para volver a
	// mostrar sus botones (CONTINUAR/OPCIONES/RENDIRSE), que se ocultan mientras se ven los ajustes.
	public System.Action AlCerrar;

	// Escala visual del panel (1 = tamaño normal). El menú principal lo pone un poco más chico; el
	// menú de pausa (VS BOT) lo deja en 1. Se aplica centrado para no descolocar el panel.
	private float _escala = 1f;

	public void FijarEscala(float s)
	{
		_escala = s;
		AplicarEscalaCentrada();
	}

	private void AplicarEscalaCentrada()
	{
		if (!IsInsideTree()) return;
		PivotOffset = Size / 2f;                 // pivote al centro (tras el layout) → no se descentra
		Scale = new Vector2(_escala, _escala);
	}

	public override void _Ready()
	{
		// Reaplicar la escala cuando el panel se dimensiona o se muestra (ahí ya tiene Size real y el
		// pivote queda bien centrado).
		Resized += AplicarEscalaCentrada;
		VisibilityChanged += AplicarEscalaCentrada;

		_sliderVolumen   = GetNodeOrNull<HSlider>("Margin/VBox/HBoxVolumen/SliderVolumen");
		_btnMute         = GetNodeOrNull<Button>("Margin/VBox/BtnMute");
		_btnCerrar       = GetNodeOrNull<Button>("Margin/VBox/BtnCerrar");
		_btnComoJugar    = GetNodeOrNull<Button>("Margin/VBox/BtnComoJugar");
		_btnCerrarSesion = GetNodeOrNull<Button>("Margin/VBox/BtnCerrarSesion");
		_chkScreenShake  = GetNodeOrNull<CheckButton>("Margin/VBox/ChkScreenShake");

		if (_btnCerrar != null) _btnCerrar.Pressed += Ocultar;

		// Disponibles sin importar si la sesión es de invitado o de una cuenta real.
		// IMPORTANTE: este panel también se abre desde el menú de PAUSA (VS BOT), donde el árbol está
		// PAUSADO (GetTree().Paused = true). Si se cambia de escena sin despausar, la escena nueva nace
		// congelada y no responde a nada. Y "Cómo jugar" desde una partida NO debe cambiar de escena
		// (destruiría la partida y al volver caías al menú): se muestra como CAPA encima. Ver AbrirComoJugar.
		if (_btnComoJugar != null)
			_btnComoJugar.Pressed += AbrirComoJugar;

		if (_btnCerrarSesion != null)
			_btnCerrarSesion.Pressed += () =>
			{
				SesionJuego.Instance?.CerrarSesion();
				GetTree().Paused = false;
				GetTree().ChangeSceneToFile(RUTA_LOGIN);
			};

		var audioManager = GlobalAudioManager.Instance;
		if (audioManager != null)
		{
			if (_sliderVolumen != null)
			{
				_sliderVolumen.Value = audioManager.GetVolumen();
				_sliderVolumen.ValueChanged += (v) => audioManager.CambiarVolumen((float)v);
			}
			if (_btnMute != null)
			{
				_btnMute.Pressed += () =>
				{
					audioManager.SetMute(!audioManager.IsMuted());
					ActualizarUI();
				};
			}
			ActualizarUI();
		}

		if (_chkScreenShake != null)
		{
			_chkScreenShake.ButtonPressed = ScreenShakeEnabled;
			_chkScreenShake.Toggled += (on) => ScreenShakeEnabled = on;
		}
	}

	// Abre "Cómo jugar". Si venimos de una partida en curso (árbol pausado, p. ej. VS BOT desde la
	// pausa), la muestra como CAPA encima de la partida SIN destruirla: "volver" cierra la capa y sigues
	// en la partida. Desde el menú principal (no pausado) se comporta como antes: cambia de escena.
	private void AbrirComoJugar()
	{
		var escena = GD.Load<PackedScene>(RUTA_COMO_JUGAR);

		if (GetTree().Paused && escena != null)
		{
			// Capa que procesa AUNQUE el juego esté pausado (Always), por encima de todo.
			var capa = new CanvasLayer { Layer = 400, ProcessMode = Node.ProcessModeEnum.Always };
			var guia = escena.Instantiate<PantallaComoJugar>();
			guia.EnModoCapa = true;
			guia.AlVolver   = () => { if (Godot.GodotObject.IsInstanceValid(capa)) capa.QueueFree(); };
			capa.AddChild(guia);
			// Se cuelga de la escena actual (la partida): así, si la partida se cierra por lo que sea,
			// la capa se libera con ella y no queda huérfana. Layer alto = por encima de todo.
			Node destino = GetTree().CurrentScene ?? GetTree().Root;
			destino.AddChild(capa);
			return;
		}

		// Menú principal (sin partida): navegación normal.
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile(RUTA_COMO_JUGAR);
	}

	private void ActualizarUI()
	{
		if (GlobalAudioManager.Instance == null || _btnMute == null) return;
		bool isMuted = GlobalAudioManager.Instance.IsMuted();

		_btnMute.Text = isMuted ? "ACTIVAR SONIDO" : "SILENCIAR";
		_btnMute.SelfModulate = isMuted ? Colors.LightGreen : new Color(1f, 0.4f, 0.4f);
	}

	// Oculta el botón "CERRAR SESIÓN". Lo usa el menú de pausa (VS BOT): no tiene sentido cerrar sesión
	// a mitad de una partida. En el menú principal el botón sigue visible.
	public void OcultarCerrarSesion()
	{
		if (_btnCerrarSesion == null)
			_btnCerrarSesion = GetNodeOrNull<Button>("Margin/VBox/BtnCerrarSesion");
		if (_btnCerrarSesion != null) _btnCerrarSesion.Visible = false;
	}

	public void Mostrar()
	{
		Visible = true;
	}

	public void Ocultar()
	{
		Visible = false;
		AlCerrar?.Invoke();
	}
}
