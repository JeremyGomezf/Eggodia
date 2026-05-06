using Godot;
using System;

public partial class MenuPrincipal : Control
{
    [Export] public string RutaEscenaJuego      = "res://escenas/gameplay/campo_1.tscn";
    [Export] public string RutaConstructorMazo  = "res://DatosCartas/MenuConstructor.tscn";
    [Export] public string RutaLogin            = "res://escenas/menu/PanelLogin.tscn";

    private Label         _lblBienvenida;
    private PanelRanking  _panelRanking;

    public override void _Ready()
    {
        var btnJugar       = GetNodeOrNull<Button>("VBoxContainer/JUGAR");
        var btnConstructor = GetNodeOrNull<Button>("VBoxContainer/CONSTRUCTOR");
        var btnRanking     = GetNodeOrNull<Button>("VBoxContainer/RANKING");
        var btnCerrarSesion= GetNodeOrNull<Button>("VBoxContainer/CERRARSESION");
        var btnSalir       = GetNodeOrNull<Button>("VBoxContainer/SALIR");

        _lblBienvenida = GetNodeOrNull<Label>("LblBienvenida");
        _panelRanking  = GetNodeOrNull<PanelRanking>("PanelRanking");

        // Mostrar nombre del jugador
        ActualizarNombreJugador();

        // Conectar botones
        if (btnJugar        != null) btnJugar.Pressed        += AlPresionarJugar;
        if (btnConstructor  != null) btnConstructor.Pressed  += AlPresionarConstructor;
        if (btnRanking      != null) btnRanking.Pressed      += AlPresionarRanking;
        if (btnCerrarSesion != null) btnCerrarSesion.Pressed += AlPresionarCerrarSesion;
        if (btnSalir        != null) btnSalir.Pressed        += AlPresionarSalir;

        // Ocultar ranking al inicio
        if (_panelRanking != null) _panelRanking.Visible = false;

        GD.Print("[MenuPrincipal] Listo.");
    }

    private void ActualizarNombreJugador()
    {
        if (_lblBienvenida == null || SesionJuego.Instance == null) return;
        string nombre = SesionJuego.Instance.NombreJugador;
        bool logueado = SesionJuego.Instance.EstaLogueado;
        _lblBienvenida.Text    = logueado ? $"⚔️ Bienvenido, {nombre}!" : $"⚔️ Age of Cards";
        _lblBienvenida.Modulate = logueado ? Colors.Gold : Colors.White;
    }

    private void AlPresionarJugar()
    {
        if (string.IsNullOrEmpty(RutaEscenaJuego))
        {
            GD.PrintErr("¡Falta asignar RutaEscenaJuego en el Inspector!");
            return;
        }
        // Si tiene mazo armado, ir directo. Si no, ir al constructor primero.
        if (SesionJuego.Instance != null && SesionJuego.Instance.TieneMazo)
        {
            GD.Print("Iniciando con mazo guardado...");
            GetTree().ChangeSceneToFile(RutaEscenaJuego);
        }
        else
        {
            GD.Print("Sin mazo → yendo al constructor primero.");
            GetTree().ChangeSceneToFile(RutaConstructorMazo);
        }
    }

    private void AlPresionarConstructor()
    {
        if (!string.IsNullOrEmpty(RutaConstructorMazo))
            GetTree().ChangeSceneToFile(RutaConstructorMazo);
    }

    private void AlPresionarRanking()
    {
        if (_panelRanking != null)
            _panelRanking.Mostrar();
        else
        {
            // Si no hay panel en escena, crear uno en código
            var panel = new PanelRanking();
            panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(panel);
        }
    }

    private void AlPresionarCerrarSesion()
    {
        SesionJuego.Instance?.CerrarSesion();
        if (!string.IsNullOrEmpty(RutaLogin))
            GetTree().ChangeSceneToFile(RutaLogin);
        else
            ActualizarNombreJugador();
    }

    private void AlPresionarSalir()
    {
        GD.Print("Cerrando Age of Cards. ¡Hasta pronto!");
        GetTree().Quit();
    }
}
