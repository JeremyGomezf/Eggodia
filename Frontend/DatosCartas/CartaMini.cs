using Godot;
using System;

public partial class CartaMini : Control
{
	[Export] private TextureRect _fotoCarta;
	[Export] private Label _nombreTexto;
	[Export] private TextureRect _fondoNota;
	[Export] private TextureRect _pinIcon;

	public CartaData MisDatos { get; private set; }
	public event Action<CartaMini> OnClickeada; 

	public void CargarDatos(CartaData datos)
	{
		MisDatos = datos;
		if (_fotoCarta != null) _fotoCarta.Texture = datos.Imagen;
		if (_nombreTexto != null) _nombreTexto.Text = datos.Nombre;
	}

	public void SetModoMazo(bool enMazo, bool esArdid = false)
	{
		if (_nombreTexto != null) _nombreTexto.Visible = false;

		if (enMazo)
		{
			if (esArdid)
			{
				// Ardid en el mazo: sin etiqueta de papel ni chincheta, encaja en el cuadro naranja como borde
				if (_fondoNota != null) _fondoNota.Visible = false;
				if (_pinIcon != null) _pinIcon.Visible = false;

				this.CustomMinimumSize = new Vector2(175, 195);
				if (_fotoCarta != null)
				{
					_fotoCarta.AnchorLeft = 0.04f;
					_fotoCarta.AnchorRight = 0.96f;
					_fotoCarta.AnchorTop = 0.04f;
					_fotoCarta.AnchorBottom = 0.96f;
					_fotoCarta.OffsetLeft = 0; _fotoCarta.OffsetRight = 0;
					_fotoCarta.OffsetTop = 0; _fotoCarta.OffsetBottom = 0;
				}
			}
			else
			{
				// Activar estilo con chincheta clavada y fondo transparente (sin nota de papel)
				if (_fondoNota != null) _fondoNota.Visible = false;
				if (_pinIcon != null) _pinIcon.Visible = true;

				// Tropa en el mazo: ranura del cofre 2x4 (130x125)
				this.CustomMinimumSize = new Vector2(130, 125);
				if (_fotoCarta != null)
				{
					_fotoCarta.AnchorLeft = 0.06f;
					_fotoCarta.AnchorRight = 0.94f;
					_fotoCarta.AnchorTop = 0.12f;
					_fotoCarta.AnchorBottom = 0.92f;
					_fotoCarta.OffsetLeft = 0; _fotoCarta.OffsetRight = 0;
					_fotoCarta.OffsetTop = 0; _fotoCarta.OffsetBottom = 0;
				}
				if (_pinIcon != null)
				{
					_pinIcon.OffsetLeft = -14; _pinIcon.OffsetRight = 14;
					_pinIcon.OffsetTop = -14; _pinIcon.OffsetBottom = 20;
				}
			}
		}
		else
		{
			// Colección normal en el selector: sin papel ni chincheta
			if (_fondoNota != null) _fondoNota.Visible = false;
			if (_pinIcon != null) _pinIcon.Visible = false;

			this.CustomMinimumSize = new Vector2(104, 108);
			if (_fotoCarta != null)
			{
				_fotoCarta.AnchorLeft = 0; _fotoCarta.AnchorRight = 1;
				_fotoCarta.AnchorTop = 0; _fotoCarta.AnchorBottom = 1;
				_fotoCarta.OffsetLeft = 0; _fotoCarta.OffsetRight = 0;
				_fotoCarta.OffsetTop = 0; _fotoCarta.OffsetBottom = 0;
			}
		}
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent)
		{
			if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed)
			{
				PivotOffset = Size / 2;
				var tween = CreateTween();
				tween.TweenProperty(this, "scale", new Vector2(0.85f, 0.85f), 0.05f).SetTrans(Tween.TransitionType.Sine);
				tween.TweenProperty(this, "scale", Vector2.One, 0.1f).SetTrans(Tween.TransitionType.Sine);
				
				OnClickeada?.Invoke(this); 
			}
		}
	}

	public override void _Notification(int what)
	{
		if (what == NotificationMouseEnter)
		{
			PivotOffset = Size / 2;
			ZIndex = 10;
			var tween = CreateTween();
			tween.TweenProperty(this, "scale", new Vector2(1.08f, 1.08f), 0.1f).SetTrans(Tween.TransitionType.Sine);
			Modulate = new Color(1.15f, 1.15f, 1.15f);
		}
		else if (what == NotificationMouseExit)
		{
			ZIndex = 0;
			var tween = CreateTween();
			tween.TweenProperty(this, "scale", Vector2.One, 0.1f).SetTrans(Tween.TransitionType.Sine);
			Modulate = Colors.White;
		}
	}
}
