using Godot;
using System;

public partial class PanelSettings : PanelContainer
{
	private HSlider _sliderVolumen;
	private Button _btnMute;
	private Button _btnCerrar;
	private CheckButton _chkFullscreen;
	private CheckButton _chkScreenShake;

	public static bool ScreenShakeEnabled { get; set; } = true;

	public override void _Ready()
	{
		_sliderVolumen  = GetNodeOrNull<HSlider>("Margin/VBox/HBoxVolumen/SliderVolumen");
		_btnMute        = GetNodeOrNull<Button>("Margin/VBox/BtnMute");
		_btnCerrar      = GetNodeOrNull<Button>("Margin/VBox/BtnCerrar");
		_chkFullscreen  = GetNodeOrNull<CheckButton>("Margin/VBox/ChkFullscreen");
		_chkScreenShake = GetNodeOrNull<CheckButton>("Margin/VBox/ChkScreenShake");

		if (_btnCerrar != null) _btnCerrar.Pressed += Ocultar;

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

		if (_chkFullscreen != null)
		{
			_chkFullscreen.ButtonPressed = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
			_chkFullscreen.Toggled += (on) =>
			{
				DisplayServer.WindowSetMode(on
					? DisplayServer.WindowMode.Fullscreen
					: DisplayServer.WindowMode.Windowed);
			};
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
