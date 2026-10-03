using System.Text.Json;
using Eggodia.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

/// <summary>
/// Pedidos que solo puede hacer el DUEÑO de la cuenta (sus monedas, su mazo, sus compras, sus partidas).
/// Se indica de dónde sale la cuenta del pedido: "id" (la dirección, /api/usuarios/{id}/...),
/// "req.UserId" o "req.JugadorId" (el cuerpo) o "jugadorId" (la consulta).
///
///   · Con token: tiene que ser una sesión válida Y de esa misma cuenta (si no, 401/403).
///   · Sin token: son los APK viejos (≤ 1.1.2), que no conocen las sesiones. Se aceptan solo mientras
///     "Seguridad:ExigirToken" esté en false (appsettings o variable Seguridad__ExigirToken). Cuando
///     todos tengan la versión nueva se pone en true y el hueco queda cerrado del todo.
///   · SoloSinToken: pedidos que solo usaban los APK viejos y que con sesión no tienen sentido
///     (fijar el saldo a mano, sumar estadísticas sueltas). Con token se rechazan siempre.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class CuentaPropiaAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _origen;
    public bool SoloSinToken { get; set; }

    public CuentaPropiaAttribute(string origen) => _origen = origen;

    public const string CLAVE_USUARIO_SESION = "UsuarioSesion";

    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var http = ctx.HttpContext;
        bool exigir = http.RequestServices.GetRequiredService<IConfiguration>().GetValue("Seguridad:ExigirToken", false);
        string? token = Sesiones.TokenDe(http);

        if (token == null)
        {
            if (exigir)
            {
                ctx.Result = Rechazo(401, "Vuelve a iniciar sesión para seguir jugando.");
                return;
            }
            await next(); // APK viejo: todavía se acepta (ver Seguridad:ExigirToken)
            return;
        }

        if (SoloSinToken)
        {
            ctx.Result = Rechazo(410, "Este pedido ya no se usa.");
            return;
        }

        var db = http.RequestServices.GetRequiredService<AppDbContext>();
        int? dueño = await Sesiones.UsuarioDe(db, token);
        if (dueño == null)
        {
            ctx.Result = Rechazo(401, "Tu sesión venció. Vuelve a iniciar sesión.");
            return;
        }

        int? pedido = CuentaDelPedido(ctx.ActionArguments);
        if (pedido != dueño)
        {
            ctx.Result = Rechazo(403, "No puedes hacer esto con otra cuenta.");
            return;
        }

        http.Items[CLAVE_USUARIO_SESION] = dueño.Value;
        await next();
    }

    private static ObjectResult Rechazo(int codigo, string mensaje) =>
        new(new { ok = false, mensaje, sesionInvalida = codigo == 401 }) { StatusCode = codigo };

    /// <summary>Número de cuenta al que apunta el pedido, según _origen.</summary>
    private int? CuentaDelPedido(IDictionary<string, object?> args)
    {
        string[] partes = _origen.Split('.', 2);
        if (!args.TryGetValue(partes[0], out object? valor) || valor == null) return null;
        if (partes.Length == 2)
        {
            var prop = valor.GetType().GetProperty(partes[1]);
            valor = prop?.GetValue(valor);
        }
        return ComoId(valor);
    }

    // Acepta 12, "12", "u12" (jugadores en línea) o el número dentro de un JSON sin tipo.
    private static int? ComoId(object? v) => v switch
    {
        int i => i,
        long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
        string s when s.StartsWith('u') => int.TryParse(s[1..], out int n) ? n : null,
        string s => int.TryParse(s, out int n) ? n : null,
        JsonElement je when je.ValueKind == JsonValueKind.Number => je.TryGetInt32(out int n) ? n : null,
        JsonElement je when je.ValueKind == JsonValueKind.String => ComoId(je.GetString()),
        _ => null,
    };
}
