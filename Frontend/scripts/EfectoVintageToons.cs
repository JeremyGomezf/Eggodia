using Godot;

/// <summary>Filtro visual "1930s Cartoon Aesthetic" (blanco y negro/sepia con viñeta) usado en
/// MenuConstructor (fuerte, al elegir una carta de Serie: Toon) y en Campo1 (muy sutil, cuando
/// el escenario de batalla sorteado es "toon"). Vive dentro de la escena que lo instala — al
/// cambiar de escena desaparece solo, así nunca se "queda pegado" en la siguiente pantalla.</summary>
public static class EfectoVintageToons
{
	private const string RUTA_SHADER = "res://shaders/vintage_toons.gdshader";

	/// <summary>Crea la capa del efecto (arranca en intensidad 0, invisible) como hija de
	/// <paramref name="raiz"/>. Guarda el ColorRect devuelto y pásalo a AplicarIntensidad().</summary>
	public static ColorRect Instalar(Node raiz, int layer = 400)
	{
		var capa = new CanvasLayer();
		capa.Name = "CapaVintageToons";
		capa.Layer = layer;
		raiz.AddChild(capa);

		var rect = new ColorRect();
		rect.Name = "EfectoVintageToons";
		rect.Color = Colors.White; // el color real lo decide el shader leyendo la pantalla
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		rect.MouseFilter = Control.MouseFilterEnum.Ignore;

		var shader = GD.Load<Shader>(RUTA_SHADER);
		if (shader != null)
		{
			var mat = new ShaderMaterial();
			mat.Shader = shader;
			mat.SetShaderParameter("intensidad", 0.0f);
			rect.Material = mat;
		}
		capa.AddChild(rect);
		return rect;
	}

	/// <summary>Anima la intensidad del efecto hacia el valor pedido (0 = a color, 1 = B/N fuerte).</summary>
	public static void AplicarIntensidad(ColorRect rect, float intensidad, float duracion = 0.35f)
	{
		if (rect == null || !GodotObject.IsInstanceValid(rect) || rect.Material is not ShaderMaterial mat) return;
		float actual = (float)mat.GetShaderParameter("intensidad").AsDouble();
		Tween tw = rect.CreateTween();
		tw.TweenMethod(Callable.From((float v) => mat.SetShaderParameter("intensidad", v)), actual, intensidad, duracion);
	}
}
