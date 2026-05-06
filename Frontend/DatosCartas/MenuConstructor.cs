using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// MenuConstructor — armador de mazo.
/// Conecta ColeccionPanel + MazoPanel + CartaDetalles.
/// Cuando el jugador presiona "¡A BATALLAR!" guarda el mazo
/// en SesionJuego y cambia a la escena de batalla.
/// </summary>
public partial class MenuConstructor : Control
{
    [Export] private ColeccionPanel _panelColeccion;
    [Export] private MazoPanel      _panelMazo;
    [Export] private CartaDetalles  _panelDetalles;
    [Export] private Button         _btnBatallar;
    [Export] private Button         _btnVolver;
    [Export] private Label          _lblContadorMazo;
    [Export] private Label          _lblNombreJugador;

    [Export] public string RutaBatalla = "res://escenas/gameplay/campo_1.tscn";
    [Export] public string RutaMenu    = "res://escenas/menu/menu_principal.tscn";

    private const int MIN_CARTAS = 3;
    private const int MAX_CARTAS = 12;

    public override void _Ready()
    {
        // Mostrar nombre del jugador
        if (_lblNombreJugador != null && SesionJuego.Instance != null)
            _lblNombreJugador.Text = $"Mazo de: {SesionJuego.Instance.NombreJugador}";

        // Conectar eventos entre paneles
        if (_panelColeccion != null)
        {
            _panelColeccion.OnCartaElegidaParaMazo += _panelMazo.AgregarCartaAlMazo;
            _panelColeccion.OnCartaElegidaParaMazo += (carta) =>
            {
                _panelDetalles?.MostrarDatos(carta);
                ActualizarContador();
            };
        }

        if (_panelMazo != null)
        {
            _panelMazo.OnCartaSeleccionadaEnMazo += _panelDetalles?.MostrarDatos;
            _panelMazo.OnCartaSeleccionadaEnMazo += (_) => ActualizarContador();
        }

        if (_btnBatallar != null) _btnBatallar.Pressed += IrABatalla;
        if (_btnVolver   != null) _btnVolver.Pressed   += () => GetTree().ChangeSceneToFile(RutaMenu);

        ActualizarContador();
    }

    private void IrABatalla()
    {
        // Recolectar cartas del mazo
        var escenas  = new List<string>();
        var imagenes = new List<string>();

        if (_panelMazo == null) { IniciarBatallaDirecta(); return; }

        // Leer cartas del grid del mazo
        var grid = _panelMazo.GetNodeOrNull<GridContainer>("GridMazo")
                ?? _panelMazo.GetNodeOrNull<GridContainer>("_gridMazo");

        if (grid != null)
        {
            foreach (Node slot in grid.GetChildren())
            {
                if (slot.GetChildCount() == 0) continue;
                var cartaMini = slot.GetChild(0) as CartaMini;
                if (cartaMini?.MisDatos == null) continue;

                // Buscar escena correspondiente en GestorCartas
                if (GestorCartas.Instance != null)
                {
                    var dato = GestorCartas.Instance.ObtenerCartas()
                                .Find(c => c.Nombre == cartaMini.MisDatos.Nombre);
                    if (dato != null)
                    {
                        escenas.Add(dato.RutaEscena);
                        imagenes.Add(dato.RutaImagen);
                    }
                }
            }
        }

        if (escenas.Count < MIN_CARTAS)
        {
            MostrarAviso($"Necesitas al menos {MIN_CARTAS} cartas en tu mazo.");
            return;
        }

        // Guardar en sesión y batallar
        if (SesionJuego.Instance != null)
            SesionJuego.Instance.GuardarMazo(escenas, imagenes);

        GD.Print($"[MenuConstructor] Mazo de {escenas.Count} cartas → ¡A batallar!");
        GetTree().ChangeSceneToFile(RutaBatalla);
    }

    private void IniciarBatallaDirecta()
    {
        GetTree().ChangeSceneToFile(RutaBatalla);
    }

    private void ActualizarContador()
    {
        if (_lblContadorMazo == null || _panelMazo == null) return;

        int count = ContarCartasEnMazo();
        _lblContadorMazo.Text = $"Cartas: {count}/{MAX_CARTAS}";
        _lblContadorMazo.Modulate = count >= MIN_CARTAS ? Colors.LightGreen : Colors.OrangeRed;

        if (_btnBatallar != null)
        {
            _btnBatallar.Disabled = count < MIN_CARTAS;
            _btnBatallar.Modulate = count >= MIN_CARTAS ? Colors.White : new Color(1,1,1,0.5f);
        }
    }

    private int ContarCartasEnMazo()
    {
        if (_panelMazo == null) return 0;
        var grid = _panelMazo.GetNodeOrNull<GridContainer>("GridMazo")
                ?? _panelMazo.GetNodeOrNull<GridContainer>("_gridMazo");
        if (grid == null) return 0;

        int count = 0;
        foreach (Node slot in grid.GetChildren())
            if (slot.GetChildCount() > 0) count++;
        return count;
    }

    private void MostrarAviso(string msg)
    {
        // Crear label temporal de aviso
        var lbl = new Label();
        lbl.Text = msg;
        lbl.AddThemeColorOverride("font_color", Colors.OrangeRed);
        lbl.AddThemeFontSizeOverride("font_size", 18);
        lbl.Position = new Vector2(400, 20);
        lbl.ZIndex   = 100;
        AddChild(lbl);
        GetTree().CreateTimer(2.5f).Timeout += () => { if (IsInstanceValid(lbl)) lbl.QueueFree(); };
    }
}
