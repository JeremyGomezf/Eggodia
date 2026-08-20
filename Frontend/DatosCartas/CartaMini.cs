using Godot;
using System;

public partial class CartaMini : Control // (O el nodo que uses de raíz, como VBoxContainer)
{
	// 1. Las referencias a la parte visual de tu carta
	[Export] private TextureRect _fotoCarta;
	[Export] private Label _nombreTexto;

	// 2. Los datos y el evento
	public CartaData MisDatos { get; private set; }
	public event Action<CartaMini> OnClickeada; 

	// 3. ¡LA FUNCIÓN QUE FALTABA! Esta es la que recibe los datos y pinta la carta
	public void CargarDatos(CartaData datos)
	{
		MisDatos = datos; // Guardamos los datos en la carta

		// Actualizamos la imagen y el texto
		if (_fotoCarta != null) _fotoCarta.Texture = datos.Imagen;
		if (_nombreTexto != null) _nombreTexto.Text = datos.Nombre;
	}

	public void SetModoMazo(bool enMazo)
	{
		if (_nombreTexto == null) return;
		if (enMazo)
		{
			// Mazo: Letras más grandes, permite hasta 2 líneas y luego recorta
			_nombreTexto.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_nombreTexto.TextOverrunBehavior = TextServer.OverrunBehavior.TrimWordEllipsis;
			_nombreTexto.ClipText = true;
			_nombreTexto.MaxLinesVisible = 2;
			_nombreTexto.AddThemeFontSizeOverride("font_size", 14); // Aumentado a 14
			_nombreTexto.AddThemeColorOverride("font_color", new Color(0.96f, 0.97f, 1.0f, 1f));
			
			_nombreTexto.CustomMinimumSize = new Vector2(80, 42); // Un poco más de espacio vertical
			this.CustomMinimumSize = new Vector2(85, 130);
		}
		else
		{
			// Coleccion: Mostrar texto completamente sin recortes, espacio vertical expandido
			_nombreTexto.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_nombreTexto.TextOverrunBehavior = TextServer.OverrunBehavior.TrimWordEllipsis;
			_nombreTexto.ClipText = false;
			_nombreTexto.MaxLinesVisible = -1; // Ilimitado
			_nombreTexto.AddThemeFontSizeOverride("font_size", 14); // Aumentado a 14
			_nombreTexto.AddThemeColorOverride("font_color", new Color(0.96f, 0.97f, 1.0f, 1f));
			
			_nombreTexto.CustomMinimumSize = new Vector2(90, 56);
			this.CustomMinimumSize = new Vector2(100, 150);
		}
	}

	// 4. La función que detecta cuando le das clic con el mouse
	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent)
		{
			if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed)
			{
				// Efecto Pop de clic
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
			// Efecto Hover
			PivotOffset = Size / 2;
			ZIndex = 10; // Traer al frente
			var tween = CreateTween();
			tween.TweenProperty(this, "scale", new Vector2(1.08f, 1.08f), 0.1f).SetTrans(Tween.TransitionType.Sine);
			Modulate = new Color(1.15f, 1.15f, 1.15f); // Brillo
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
