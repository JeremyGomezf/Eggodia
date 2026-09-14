using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eggodia.API.Data;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Panel de administración simple para ver EN VIVO las cuentas registradas (y sus stats), servido por
/// el mismo backend — así se abre desde cualquier navegador (PC o celular) sin instalar nada.
///
///   GET /api/admin/panel?clave=XXXX      → página HTML que se auto-refresca cada pocos segundos.
///   GET /api/admin/usuarios?clave=XXXX   → JSON con la lista de usuarios (lo que consume el panel).
///
/// La clave se configura en appsettings.json (sección Admin → Clave). Cambiala por una tuya.
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

    // ── Datos: lista de usuarios (más nuevos primero) + contadores ────────────
    [HttpGet("usuarios")]
    public async Task<IActionResult> Usuarios([FromQuery] string clave = "")
    {
        if (!ClaveOk(clave)) return Unauthorized(new { mensaje = "clave inválida" });

        List<object> usuarios;
        int hoy = 0;
        try
        {
            // Intento con FechaRegistro (si la columna existe en la BD).
            var conFecha = await _db.Usuarios
                .OrderByDescending(u => u.Id)
                .Select(u => new {
                    u.Id, u.Nombre, u.Email,
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
            // BD antigua sin la columna FechaRegistro: se lista igual, sin fecha.
            var sinFecha = await _db.Usuarios
                .OrderByDescending(u => u.Id)
                .Select(u => new {
                    u.Id, u.Nombre, u.Email,
                    u.Victorias, u.Derrotas, u.Empates, u.DañoTotal,
                    fecha = (DateTime?)null
                })
                .ToListAsync();
            usuarios = sinFecha.Cast<object>().ToList();
        }

        return Ok(new { total = usuarios.Count, hoy, usuarios });
    }

    // ── Página HTML del panel (se auto-refresca) ──────────────────────────────
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
<title>Eggodia · Cuentas</title>
<style>
  :root{color-scheme:dark}
  *{box-sizing:border-box}
  body{margin:0;background:#0b0e15;color:#e8ecf5;font-family:system-ui,'Segoe UI',sans-serif}
  header{padding:16px 20px;background:#11151f;border-bottom:1px solid #232b3d;position:sticky;top:0;display:flex;gap:16px;align-items:center;flex-wrap:wrap}
  h1{font-size:18px;margin:0;color:#ffd25a}
  .stats{display:flex;gap:12px;margin-left:auto;flex-wrap:wrap}
  .chip{background:#1a2030;border:1px solid #2b3550;border-radius:10px;padding:8px 14px;font-size:13px}
  .chip b{font-size:20px;display:block;color:#7ee0a1}
  .chip.hoy b{color:#ffd25a}
  .upd{font-size:12px;color:#6b768f}
  .wrap{padding:14px;overflow-x:auto}
  table{border-collapse:collapse;width:100%;min-width:640px}
  th,td{padding:10px 12px;text-align:left;border-bottom:1px solid #1c2233;font-size:14px;white-space:nowrap}
  th{color:#8b98b5;font-weight:600;font-size:12px;text-transform:uppercase;letter-spacing:.04em}
  tr:hover td{background:#131a28}
  td.n{color:#7ee0a1;font-variant-numeric:tabular-nums}
  .nuevo{animation:flash 2.5s ease-out}
  @keyframes flash{0%{background:#1e3a2a}100%{background:transparent}}
  .vacio{padding:40px;text-align:center;color:#6b768f}
</style></head>
<body>
<header>
  <h1>🥚 Eggodia · Cuentas en vivo</h1>
  <div class='stats'>
    <div class='chip'>Total<b id='total'>—</b></div>
    <div class='chip hoy'>Hoy<b id='hoy'>—</b></div>
  </div>
  <div class='upd' id='upd'>conectando…</div>
</header>
<div class='wrap'>
  <table><thead><tr>
    <th>#</th><th>Nombre</th><th>Email</th><th>Fecha registro</th>
    <th>V</th><th>D</th><th>E</th><th>Daño</th>
  </tr></thead><tbody id='tb'></tbody></table>
  <div class='vacio' id='vacio' hidden>Sin cuentas todavía.</div>
</div>
<script>
  const CLAVE='%%CLAVE%%';
  let maxVisto=0, primera=true;
  function fecha(f){ if(!f) return '—'; const d=new Date(f); return isNaN(d)?'—':d.toLocaleString('es'); }
  async function cargar(){
    try{
      const r=await fetch('/api/admin/usuarios?clave='+CLAVE,{cache:'no-store'});
      if(!r.ok) throw new Error(r.status);
      const d=await r.json();
      document.getElementById('total').textContent=d.total;
      document.getElementById('hoy').textContent=d.hoy;
      document.getElementById('upd').textContent='actualizado '+new Date().toLocaleTimeString('es');
      const tb=document.getElementById('tb'); tb.innerHTML='';
      document.getElementById('vacio').hidden = d.usuarios.length>0;
      let nuevoMax=maxVisto;
      for(const u of d.usuarios){
        const esNuevo = !primera && u.id>maxVisto;
        if(u.id>nuevoMax) nuevoMax=u.id;
        const tr=document.createElement('tr'); if(esNuevo) tr.className='nuevo';
        tr.innerHTML='<td class=n>'+u.id+'</td><td>'+esc(u.nombre)+'</td><td>'+esc(u.email)+'</td>'
          +'<td>'+fecha(u.fecha)+'</td><td class=n>'+u.victorias+'</td><td class=n>'+u.derrotas
          +'</td><td class=n>'+u.empates+'</td><td class=n>'+u.dañoTotal+'</td>';
        tb.appendChild(tr);
      }
      maxVisto=nuevoMax; primera=false;
    }catch(e){ document.getElementById('upd').textContent='error de conexión ('+e.message+')'; }
  }
  function esc(s){ return (s??'').replace(/[&<>]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;'}[c])); }
  cargar(); setInterval(cargar, 4000);
</script>
</body></html>";
}
