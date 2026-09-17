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

	public override void _Ready()
	{
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
		// congelada y no responde a nada (no se puede tocar ni volver). Por eso se despausa SIEMPRE antes
		// de navegar (en el menú principal no está pausado, así que despausar ahí no molesta).
		if (_btnComoJugar != null)
			_btnComoJugar.Pressed += () => { GetTree().Paused = false; GetTree().ChangeSceneToFile(RUTA_COMO_JUGAR); };

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

	private void ActualizarUI()
	{
		if (GlobalAudioManager.Instance == null || _btnMute == null) return;
		bool isMuted = GlobalAudioManager.Instance.IsMuted();

		_btnMute.Text = isMuted ? "ACTIVAR SONIDO" : "SILENCIAR";
		_btnMute.SelfModulate = isMuted ? Colors.LightGreen : new Color(1f, 0.4f, 0.4f);
	}

	public void Mostrar()
	{
		Visible = true;
	}

	public void Ocultar()
	{
		Visible = false;
	}
}
