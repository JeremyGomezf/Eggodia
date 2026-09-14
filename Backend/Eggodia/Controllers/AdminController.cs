using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eggodia.API.Data;
using Eggodia.API.model;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Panel de administración servido por el mismo backend: ver EN VIVO las cuentas y gestionarlas
/// (crear de prueba, dar/quitar monedas, editar nombre/correo, resetear contraseña, borrar).
///
///   GET  /api/admin/panel?clave=XXXX       → página HTML que se auto-refresca.
///   GET  /api/admin/usuarios?clave=XXXX    → JSON con la lista (lo que consume el panel).
///   POST /api/admin/crear?clave=XXXX       → crea usuario { nombre, email, password }.
///   POST /api/admin/monedas?clave=XXXX     → { id, cantidad, modo:"set"|"add" }.
///   POST /api/admin/editar?clave=XXXX      → { id, nombre?, email? }.
///   POST /api/admin/password?clave=XXXX    → { id, nueva }.
///   POST /api/admin/borrar?clave=XXXX      → { id }.
///
/// La clave se configura SOLO en el servidor (appsettings Admin:Clave o env Admin__Clave). Vacía = panel
/// deshabilitado (todo responde 401).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    public AdminController(AppDbContext db, IConfiguration cfg) { _db = db; _cfg = cfg; }

    private bool ClaveOk(string clave)
    {
        string real = _cfg["Admin:Clave"] ?? "";
        return !string.IsNullOrEmpty(real) && clave == real;
    }

    // ── Lista de usuarios (más nuevos primero) + contadores ───────────────────
    [HttpGet("usuarios")]
    public async Task<IActionResult> Usuarios([FromQuery] string clave = "")
    {
        if (!ClaveOk(clave)) return Unauthorized(new { mensaje = "clave inválida" });

        List<object> usuarios;
        int hoy = 0;
        try
        {
            var conFecha = await _db.Usuarios
                .OrderByDescending(u => u.Id)
                .Select(u => new {
                    u.Id, u.Nombre, u.Email, u.Monedas,
                    u.Victorias, u.Derrotas, u.Empates, u.DañoTotal,
                    fecha = (DateTime?)u.FechaRegistro
                })
                .ToListAsync();
            var hoyUtc = DateTime.UtcNow.Date;
            hoy = conFecha.Count(x => x.fecha.HasValue && x.fecha.Value.Date == hoyUtc);
            usuarios = conFecha.Cast<object>().ToList();
        }
        catch
        {
            var sinFecha = await _db.Usuarios
                .OrderByDescending(u => u.Id)
                .Select(u => new {
                    u.Id, u.Nombre, u.Email, u.Monedas,
                    u.Victorias, u.Derrotas, u.Empates, u.DañoTotal,
                    fecha = (DateTime?)null
                })
                .ToListAsync();
            usuarios = sinFecha.Cast<object>().ToList();
        }
        return Ok(new { total = usuarios.Count, hoy, usuarios });
    }

    // ── Crear usuario (de prueba o manual) ────────────────────────────────────
    public class CrearReq { public string Nombre { get; set; } = ""; public string Email { get; set; } = ""; public string Password { get; set; } = ""; }

    [HttpPost("crear")]
    public async Task<IActionResult> Crear([FromQuery] string clave, [FromBody] CrearReq req)
    {
        if (!ClaveOk(clave)) return Unauthorized(new { mensaje = "clave inválida" });
        if (req == null || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Nombre) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { mensaje = "nombre, email y password son obligatorios" });
        if (await _db.Usuarios.AnyAsync(u => u.Email == req.Email))
            return Conflict(new { mensaje = "El email ya está registrado." });

        var u = new Usuario { Nombre = req.Nombre, Email = req.Email, Password = BCrypt.Net.BCrypt.HashPassword(req.Password) };
        _db.Usuarios.Add(u);
        await _db.SaveChangesAsync();
        return Ok(new { u.Id, u.Nombre, u.Email });
    }

    // ── Dar / quitar / fijar monedas ──────────────────────────────────────────
    public class MonedasReq { public int Id { get; set; } public int Cantidad { get; set; } public string Modo { get; set; } = "add"; }

    [HttpPost("monedas")]
    public async Task<IActionResult> Monedas([FromQuery] string clave, [FromBody] MonedasReq req)
    {
        if (!ClaveOk(clave)) return Unauthorized(new { mensaje = "clave inválida" });
        var u = await _db.Usuarios.FindAsync(req?.Id ?? 0);
        if (u == null) return NotFound(new { mensaje = "Usuario no encontrado." });

        u.Monedas = (req.Modo == "set") ? req.Cantidad : u.Monedas + req.Cantidad;
        if (u.Monedas < 0) u.Monedas = 0;
        await _db.SaveChangesAsync();
        return Ok(new { u.Id, u.Monedas });
    }

    // ── Editar nombre / email ─────────────────────────────────────────────────
    public class EditarReq { public int Id { get; set; } public string? Nombre { get; set; } public string? Email { get; set; } }

    [HttpPost("editar")]
    public async Task<IActionResult> Editar([FromQuery] string clave, [FromBody] EditarReq req)
    {
        if (!ClaveOk(clave)) return Unauthorized(new { mensaje = "clave inválida" });
        var u = await _db.Usuarios.FindAsync(req?.Id ?? 0);
        if (u == null) return NotFound(new { mensaje = "Usuario no encontrado." });

        if (!string.IsNullOrWhiteSpace(req.Email) && req.Email != u.Email)
        {
            if (await _db.Usuarios.AnyAsync(x => x.Email == req.Email))
                return Conflict(new { mensaje = "Ese email ya está en uso." });
            u.Email = req.Email;
        }
        if (!string.IsNullOrWhiteSpace(req.Nombre)) u.Nombre = req.Nombre;
        await _db.SaveChangesAsync();
        return Ok(new { u.Id, u.Nombre, u.Email });
    }

    // ── Resetear contraseña ───────────────────────────────────────────────────
    public class PasswordReq { public int Id { get; set; } public string Nueva { get; set; } = ""; }

    [HttpPost("password")]
    public async Task<IActionResult> Password([FromQuery] string clave, [FromBody] PasswordReq req)
    {
        if (!ClaveOk(clave)) return Unauthorized(new { mensaje = "clave inválida" });
        if (req == null || string.IsNullOrWhiteSpace(req.Nueva)) return BadRequest(new { mensaje = "contraseña vacía" });
        var u = await _db.Usuarios.FindAsync(req.Id);
        if (u == null) return NotFound(new { mensaje = "Usuario no encontrado." });
        u.Password = BCrypt.Net.BCrypt.HashPassword(req.Nueva);
        await _db.SaveChangesAsync();
        return Ok(new { u.Id, mensaje = "contraseña actualizada" });
    }

    // ── Borrar usuario ────────────────────────────────────────────────────────
    public class BorrarReq { public int Id { get; set; } }

    [HttpPost("borrar")]
    public async Task<IActionResult> Borrar([FromQuery] string clave, [FromBody] BorrarReq req)
    {
        if (!ClaveOk(clave)) return Unauthorized(new { mensaje = "clave inválida" });
        var u = await _db.Usuarios.FindAsync(req?.Id ?? 0);
        if (u == null) return NotFound(new { mensaje = "Usuario no encontrado." });
        _db.Usuarios.Remove(u);
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    // ── Página HTML del panel ─────────────────────────────────────────────────
    [HttpGet("panel")]
    public ContentResult Panel([FromQuery] string clave = "")
    {
        if (!ClaveOk(clave))
            return new ContentResult
            {
                ContentType = "text/html; charset=utf-8",
                StatusCode = 401,
                Content = "<body style='background:#0d1017;color:#ff6b6b;font-family:sans-serif;text-align:center;padding-top:60px'><h2>Clave inválida</h2></body>"
            };

        string html = HTML.Replace("%%CLAVE%%", Uri.EscapeDataString(clave));
        return new ContentResult { ContentType = "text/html; charset=utf-8", Content = html };
    }

    private const string HTML = @"<!doctype html>
<html lang='es'><head><meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1'>
<title>Eggodia · Admin</title>
<style>
  :root{color-scheme:dark}
  *{box-sizing:border-box}
  body{margin:0;background:#0b0e15;color:#e8ecf5;font-family:system-ui,'Segoe UI',sans-serif}
  header{padding:14px 18px;background:#11151f;border-bottom:1px solid #232b3d;position:sticky;top:0;z-index:5;display:flex;gap:14px;align-items:center;flex-wrap:wrap}
  h1{font-size:17px;margin:0;color:#ffd25a}
  .stats{display:flex;gap:10px;margin-left:auto;flex-wrap:wrap}
  .chip{background:#1a2030;border:1px solid #2b3550;border-radius:10px;padding:6px 12px;font-size:12px}
  .chip b{font-size:19px;display:block;color:#7ee0a1}
  .chip.hoy b{color:#ffd25a}
  .upd{font-size:12px;color:#6b768f;width:100%}
  .crear{display:flex;gap:8px;flex-wrap:wrap;padding:12px 18px;background:#0f1420;border-bottom:1px solid #1c2233}
  .crear input{background:#161d2c;border:1px solid #2b3550;border-radius:8px;color:#e8ecf5;padding:9px 11px;font-size:14px}
  button{background:#243049;border:1px solid #35507d;border-radius:8px;color:#dce6ff;padding:8px 12px;font-size:13px;cursor:pointer}
  button:hover{background:#2d3d5e}
  button.g{background:#1e3a2a;border-color:#2f6a45;color:#9ff0bc}
  button.r{background:#3a1e22;border-color:#6a2f38;color:#ffb0b8}
  button.crear-btn{background:#3a3212;border-color:#7a6a1f;color:#ffe08a}
  .wrap{padding:12px;overflow-x:auto}
  table{border-collapse:collapse;width:100%;min-width:820px}
  th,td{padding:9px 10px;text-align:left;border-bottom:1px solid #1c2233;font-size:13px;white-space:nowrap}
  th{color:#8b98b5;font-weight:600;font-size:11px;text-transform:uppercase;letter-spacing:.04em}
  tr:hover td{background:#131a28}
  td.n{color:#7ee0a1;font-variant-numeric:tabular-nums}
  td.oro{color:#ffd25a;font-weight:700;font-variant-numeric:tabular-nums}
  .acc{display:flex;gap:5px;flex-wrap:wrap}
  .acc button{padding:5px 8px;font-size:12px}
  .nuevo{animation:flash 2.5s ease-out}
  @keyframes flash{0%{background:#1e3a2a}100%{background:transparent}}
  .vacio{padding:40px;text-align:center;color:#6b768f}
  #toast{position:fixed;bottom:18px;left:50%;transform:translateX(-50%);background:#1a2030;border:1px solid #35507d;color:#dce6ff;padding:10px 18px;border-radius:10px;font-size:14px;opacity:0;transition:opacity .3s;pointer-events:none;z-index:20}
  #toast.show{opacity:1}
</style></head>
<body>
<header>
  <h1>🥚 Eggodia · Administración</h1>
  <div class='stats'>
    <div class='chip'>Total<b id='total'>—</b></div>
    <div class='chip hoy'>Hoy<b id='hoy'>—</b></div>
  </div>
  <div class='upd' id='upd'>conectando…</div>
</header>

<div class='crear'>
  <input id='cn' placeholder='Nombre'>
  <input id='ce' placeholder='Email' type='email'>
  <input id='cp' placeholder='Contraseña'>
  <button class='crear-btn' onclick='crear()'>➕ Crear cuenta</button>
</div>

<div class='wrap'>
  <table><thead><tr>
    <th>#</th><th>Nombre</th><th>Email</th><th>💰 Monedas</th><th>Registro</th>
    <th>V</th><th>D</th><th>E</th><th>Acciones</th>
  </tr></thead><tbody id='tb'></tbody></table>
  <div class='vacio' id='vacio' hidden>Sin cuentas todavía.</div>
</div>
<div id='toast'></div>

<script>
  const CLAVE='%%CLAVE%%';
  let maxVisto=0, primera=true;
  function fecha(f){ if(!f) return '—'; const d=new Date(f); return isNaN(d)?'—':d.toLocaleString('es'); }
  function esc(s){ return (s??'').toString().replace(/[&<>""']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','""':'&quot;',""'"":'&#39;'}[c])); }
  function toast(m){ const t=document.getElementById('toast'); t.textContent=m; t.classList.add('show'); setTimeout(()=>t.classList.remove('show'),2200); }

  async function api(ruta, cuerpo){
    const r=await fetch('/api/admin/'+ruta+'?clave='+CLAVE,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(cuerpo)});
    let d={}; try{ d=await r.json(); }catch(e){}
    if(!r.ok){ toast('⚠ '+(d.mensaje||('error '+r.status))); return null; }
    return d;
  }

  async function crear(){
    const n=cn.value.trim(), e=ce.value.trim(), p=cp.value.trim();
    if(!n||!e||!p){ toast('Completa nombre, email y contraseña'); return; }
    const d=await api('crear',{nombre:n,email:e,password:p});
    if(d){ toast('✔ Cuenta creada #'+d.id); cn.value=ce.value=cp.value=''; cargar(); }
  }
  async function monedas(id,nombre){
    const s=prompt('Monedas para '+nombre+':\n  +100  (dar)   ·   -50  (quitar)   ·   =1000  (fijar)','+100');
    if(s===null) return;
    let modo='add', cant=parseInt(s.replace(/[^0-9-]/g,''),10);
    if(s.trim().startsWith('=')) modo='set';
    if(isNaN(cant)){ toast('Valor inválido'); return; }
    const d=await api('monedas',{id,cantidad:cant,modo});
    if(d){ toast('✔ '+nombre+' ahora tiene '+d.monedas+' monedas'); cargar(); }
  }
  async function editar(id,nombre,email){
    const nn=prompt('Nombre:',nombre); if(nn===null) return;
    const ne=prompt('Email:',email);   if(ne===null) return;
    const d=await api('editar',{id,nombre:nn,email:ne});
    if(d){ toast('✔ Actualizado'); cargar(); }
  }
  async function pass(id,nombre){
    const p=prompt('Nueva contraseña para '+nombre+':'); if(!p) return;
    const d=await api('password',{id,nueva:p});
    if(d){ toast('✔ Contraseña cambiada'); }
  }
  async function borrar(id,nombre){
    if(!confirm('¿Borrar la cuenta de '+nombre+'? No se puede deshacer.')) return;
    const d=await api('borrar',{id});
    if(d){ toast('✔ Cuenta borrada'); cargar(); }
  }

  async function cargar(){
    try{
      const r=await fetch('/api/admin/usuarios?clave='+CLAVE,{cache:'no-store'});
      if(!r.ok) throw new Error(r.status);
      const d=await r.json();
      total.textContent=d.total; hoy.textContent=d.hoy;
      upd.textContent='actualizado '+new Date().toLocaleTimeString('es');
      const tb=document.getElementById('tb'); tb.innerHTML='';
      document.getElementById('vacio').hidden = d.usuarios.length>0;
      let nuevoMax=maxVisto;
      for(const u of d.usuarios){
        const esNuevo=!primera && u.id>maxVisto; if(u.id>nuevoMax) nuevoMax=u.id;
        const tr=document.createElement('tr'); if(esNuevo) tr.className='nuevo';
        const nm=esc(u.nombre), em=esc(u.email);
        tr.innerHTML='<td class=n>'+u.id+'</td><td>'+nm+'</td><td>'+em+'</td>'
          +'<td class=oro>'+u.monedas+'</td><td>'+fecha(u.fecha)+'</td>'
          +'<td class=n>'+u.victorias+'</td><td class=n>'+u.derrotas+'</td><td class=n>'+u.empates+'</td>'
          +'<td><div class=acc>'
          +'<button class=g onclick=\'monedas('+u.id+',""'+nm+'"")\'>💰</button>'
          +'<button onclick=\'editar('+u.id+',""'+nm+'"",""'+em+'"")\'>✏️</button>'
          +'<button onclick=\'pass('+u.id+',""'+nm+'"")\'>🔑</button>'
          +'<button class=r onclick=\'borrar('+u.id+',""'+nm+'"")\'>🗑️</button>'
          +'</div></td>';
        tb.appendChild(tr);
      }
      maxVisto=nuevoMax; primera=false;
    }catch(e){ upd.textContent='error de conexión ('+e.message+')'; }
  }
  cargar(); setInterval(cargar, 4000);
</script>
</body></html>";
}
