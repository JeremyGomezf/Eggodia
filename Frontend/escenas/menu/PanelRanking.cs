using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

/// <summary>
/// PanelRanking — tabla de líderes.
/// Muestra top 20 jugadores ordenados por victorias.
///
/// Nodos requeridos:
///   VBoxContainer "ListaRanking"  (aquí se crean las filas)
///   Button "BtnCerrar"
///   Label "LblTitulo"
/// </summary>
public partial class PanelRanking : Control
{
    private const string URL_RANKING = "http://localhost:5289/api/usuarios/ranking";

    private VBoxContainer _lista;
    private Button        _btnCerrar;
    private Godot.HttpRequest _http;

    public override void _Ready()
    {
        _lista     = GetNodeOrNull<VBoxContainer>("ListaRanking");
        _btnCerrar = GetNodeOrNull<Button>("BtnCerrar");

        _http = new Godot.HttpRequest();
        AddChild(_http);
        _http.RequestCompleted += OnRankingRecibido;

        if (_btnCerrar != null) _btnCerrar.Pressed += () => Visible = false;

        // Encabezado de tabla
        AgregarFila("🏆 POS", "JUGADOR", "V", "D", "W%", true);
        AgregarSeparador();

        CargarRanking();
    }

    public void Mostrar()
    {
        Visible = true;
        // Limpiar y recargar
        if (_lista != null)
            foreach (Node n in _lista.GetChildren()) n.QueueFree();
        AgregarFila("🏆 POS", "JUGADOR", "V", "D", "W%", true);
        AgregarSeparador();
        CargarRanking();
    }

    private void CargarRanking()
    {
        _http.Request(URL_RANKING);
    }

    private void OnRankingRecibido(long result, long code, string[] headers, byte[] body)
    {
        if (result != (long)Godot.HttpRequest.Result.Success || code != 200)
        {
            AgregarFila("—", "Sin conexión al servidor", "—", "—", "—", false);
            return;
        }

        try
        {
            string json = Encoding.UTF8.GetString(body);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var lista = JsonSerializer.Deserialize<List<EntradaRanking>>(json, opts);

            if (lista == null || lista.Count == 0)
            {
                AgregarFila("—", "Sin jugadores aún", "—", "—", "—", false);
                return;
            }

            int pos = 1;
            foreach (var e in lista)
            {
                string medalla = pos == 1 ? "🥇" : pos == 2 ? "🥈" : pos == 3 ? "🥉" : $"{pos}.";
                // Resaltar usuario actual
                bool esYo = SesionJuego.Instance != null && e.Id == SesionJuego.Instance.UsuarioId;
                AgregarFila(medalla, e.Nombre + (esYo ? " ◀ TÚ" : ""),
                            e.Victorias.ToString(), e.Derrotas.ToString(),
                            $"{e.WinRate}%", false, esYo);
                pos++;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Ranking] Error: {ex.Message}");
            AgregarFila("—", "Error cargando datos", "—", "—", "—", false);
        }
    }

    private void AgregarFila(string pos, string nombre, string v, string d, string wr,
                              bool esEncabezado, bool resaltar = false)
    {
        if (_lista == null) return;

        var hbox = new HBoxContainer();
        hbox.CustomMinimumSize = new Vector2(0, 32);

        Color colorTexto = esEncabezado ? Colors.Gold
                         : resaltar     ? new Color(0.3f, 1f, 0.5f)
                                        : Colors.White;

        AgregarCelda(hbox, pos,     70,  colorTexto);
        AgregarCelda(hbox, nombre,  220, colorTexto);
        AgregarCelda(hbox, v,       60,  colorTexto);
        AgregarCelda(hbox, d,       60,  colorTexto);
        AgregarCelda(hbox, wr,      70,  colorTexto);

        _lista.AddChild(hbox);
    }

    private void AgregarCelda(HBoxContainer parent, string texto, int ancho, Color color)
    {
        var lbl = new Label();
        lbl.Text = texto;
        lbl.CustomMinimumSize = new Vector2(ancho, 0);
        lbl.AddThemeColorOverride("font_color", color);
        parent.AddChild(lbl);
    }

    private void AgregarSeparador()
    {
        if (_lista == null) return;
        var sep = new HSeparator();
        _lista.AddChild(sep);
    }

    private class EntradaRanking
    {
        public int    Id        { get; set; }
        public string Nombre    { get; set; } = "";
        public int    Victorias { get; set; }
        public int    Derrotas  { get; set; }
        public int    Empates   { get; set; }
        public int    DañoTotal { get; set; }
        public int    WinRate   { get; set; }
    }
}
