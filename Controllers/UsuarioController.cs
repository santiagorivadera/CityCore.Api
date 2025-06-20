using BCrypt.Net;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CityCore.Data;
using CityCore.Api.Models.DTOs;

namespace CityCore.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuarioController : ControllerBase
    {
        private readonly CityCoreContext _context;

        public UsuarioController(CityCoreContext context)
        {
            _context = context;
        }

        // GET: api/Usuario
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UsuarioDto>>> GetUsuarios()
        {
            var usuarios = await _context.Usuarios
                .Select(u => new UsuarioDto
                {
                    UsuarioId    = u.UsuarioId,
                    Nombre       = u.Nombre,
                    Apellido     = u.Apellido,
                    Correo       = u.Correo,
                    FechaRegistro= u.FechaRegistro
                })
                .ToListAsync();

            return Ok(usuarios);
        }

        // GET: api/Usuario/5
        [HttpGet("{id}")]
        public async Task<ActionResult<UsuarioDto>> GetUsuario(int id)
        {
            var u = await _context.Usuarios.FindAsync(id);
            if (u == null) return NotFound();

            var dto = new UsuarioDto
            {
                UsuarioId    = u.UsuarioId,
                Nombre       = u.Nombre,
                Apellido     = u.Apellido,
                Correo       = u.Correo,
                FechaRegistro= u.FechaRegistro
            };

            return Ok(dto);
        }

       // POST: api/Usuario
[HttpPost]
public async Task<ActionResult<UsuarioDto>> CreateUsuario(CreateUsuarioDto dto)
{
    // 1. Verificar existencia de correo
    if (await _context.Usuarios.AnyAsync(u => u.Correo == dto.Correo))
    {
        return Conflict(new { mensaje = "Ya existe un usuario con este correo." });
    }

    // 2. Crear entidad y hashear contraseña
    var usuario = new Usuario
    {
        Nombre        = dto.Nombre,
        Apellido      = dto.Apellido,
        Correo        = dto.Correo,
        PassHash      = BCrypt.Net.BCrypt.HashPassword(dto.PassHash),
        FechaRegistro = dto.FechaRegistro
    };

    _context.Usuarios.Add(usuario);
    await _context.SaveChangesAsync();

    // 3. Mapear a DTO de respuesta
    var result = new UsuarioDto { /* ... */ };

    return CreatedAtAction(nameof(GetUsuario), new { id = result.UsuarioId }, result);
}

// PUT: api/Usuario/5
[HttpPut("{id}")]
public async Task<IActionResult> UpdateUsuario(int id, CreateUsuarioDto dto)
{
    var usuario = await _context.Usuarios.FindAsync(id);
    if (usuario == null) return NotFound();

    usuario.Nombre        = dto.Nombre;
    usuario.Apellido      = dto.Apellido;
    usuario.Correo        = dto.Correo;
    // Si actualizas la contraseña, vuelve a hashearla
    usuario.PassHash      = BCrypt.Net.BCrypt.HashPassword(dto.PassHash);
    usuario.FechaRegistro = dto.FechaRegistro;

    try
    {
        await _context.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        if (!await _context.Usuarios.AnyAsync(e => e.UsuarioId == id))
            return NotFound();
        throw;
    }

    return NoContent();
}

        

        // DELETE: api/Usuario/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUsuario(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null) return NotFound();

            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
