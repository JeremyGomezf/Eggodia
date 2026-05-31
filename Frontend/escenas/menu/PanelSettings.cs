using Godot;
using System;

public partial class PanelSettings : PanelContainer
{
	private HSlider _sliderVolumen;
	private Button _btnMute;
	private Button _btnCerrar;

	public override void _Ready()
	{
		_sliderVolumen = GetNodeOrNull<HSlider>("Margin/VBox/HBoxVolumen/SliderVolumen");
		_btnMute       = GetNodeOrNull<Button>("Margin/VBox/BtnMute");
		_btnCerrar     = GetNodeOrNull<Button>("Margin/VBox/BtnCerrar");

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
	}

	private void ActualizarUI()
	{
		if (GlobalAudioManager.Instance == null || _btnMute == null) return;
		bool isMuted = GlobalAudioManager.Instance.IsMuted();
		
		_btnMute.Text = isMuted ? "🔊 ACTIVAR MÚSICA" : "🔇 SILENCIAR";
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
