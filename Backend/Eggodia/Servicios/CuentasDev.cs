/// <summary>
/// Cuentas de los desarrolladores, por su Id FIJO (nunca por el texto del nombre: antes un nombre
/// parecido, como "ggonzalo", recibía cosas de dev). Cada una tiene su skin exclusiva.
///   · Ganarle en línea a una de estas cuentas regala el Huevo Dorado (ver UsuariosController.Recompensa).
///   · Su perfil público muestra todas las skins exclusivas.
/// </summary>
public static class CuentasDev
{
    public static readonly IReadOnlyDictionary<int, (string ruta, string nombre)> Skins = new Dictionary<int, (string, string)>
    {
        { 1, ("res://imagenes/PersonajesPng/JeremiHuevo.png", "Jeremi Huevo") }, // Jeremy_dev
        { 2, ("res://imagenes/PersonajesPng/GonzaHuevo.png",  "Gonza Huevo")  }, // SrGonza
        { 4, ("res://imagenes/PersonajesPng/CarlosHuevo.png", "Carlos Huevo") }, // kankox_dev (Carlos)
    };

    public static bool Es(int usuarioId) => Skins.ContainsKey(usuarioId);

    /// <summary>Premio por ganarle a una cuenta dev (la misma ruta que guarda el código de Huevo Dorado).</summary>
    public const string RUTA_HUEVO_DORADO   = "res://imagenes/RendersTropa/Huevo render/HuevoDorado_Render.png";
    public const string NOMBRE_HUEVO_DORADO = "Huevo Dorado";
}
