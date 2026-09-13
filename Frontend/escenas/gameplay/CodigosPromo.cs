using Godot;

/// <summary>Sistema de códigos de canje (skins, monedas, etc.) — hoy solo entrega monedas.
/// Los códigos de un solo uso se rastrean POR DISPOSITIVO en Preferencias (ConfigFile local);
/// no hay servidor, así que "un solo uso" significa "una vez en este dispositivo", no a nivel
/// global de todos los jugadores.</summary>
public static class CodigosPromo
{
	public enum Resultado { Canjeado, YaUsado, Erroneo, Expirado }

	private struct Codigo
	{
		public string Clave;
		public int Monedas;
		public int UsosMax; // -1 = ilimitado (se puede usar muchas veces); 1 = un solo uso
		public bool Expirado;
	}

	// 5 códigos de prueba pedidos: un solo uso cada uno, 200 monedas de recompensa.
	private static readonly Codigo[] CODIGOS =
	{
		new Codigo { Clave = "EGGODIA2026",   Monedas = 200, UsosMax = 1 },
		new Codigo { Clave = "HUEVODEORO",    Monedas = 200, UsosMax = 1 },
		new Codigo { Clave = "BATALLAEPICA",  Monedas = 200, UsosMax = 1 },
		new Codigo { Clave = "CASAABIERTA",   Monedas = 200, UsosMax = 1 },
		new Codigo { Clave = "BIENVENIDOEGG", Monedas = 200, UsosMax = 1 },
	};

	public static (Resultado resultado, int monedas) Canjear(string entrada)
	{
		if (string.IsNullOrWhiteSpace(entrada)) return (Resultado.Erroneo, 0);
		string clave = entrada.Trim().ToUpperInvariant();

		foreach (var c in CODIGOS)
		{
			if (c.Clave != clave) continue;
			if (c.Expirado) return (Resultado.Expirado, 0);
			if (c.UsosMax == 1 && Preferencias.CodigoYaCanjeado(clave)) return (Resultado.YaUsado, 0);

			if (c.UsosMax == 1) Preferencias.MarcarCodigoCanjeado(clave);
			Economia.Instancia()?.Agregar(c.Monedas);
			return (Resultado.Canjeado, c.Monedas);
		}
		return (Resultado.Erroneo, 0);
	}
}
