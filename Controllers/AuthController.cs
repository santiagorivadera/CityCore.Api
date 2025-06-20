using System;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using CityCore.Api.Dtos;
using CityCore.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;


[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly CityCoreContext _db;
    private readonly IConfiguration  _config;

    public AuthController(CityCoreContext db, IConfiguration config)
    {
        _db     = db;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        // 1) Validar credenciales (simplificado)
        var user = await _db.Usuarios.SingleOrDefaultAsync(u => u.Correo == dto.Correo);
        if (user == null || !VerifyPassword(dto.Password, user.PassHash))
            return Unauthorized();

        // 2) Crear claims
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UsuarioId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Correo),
            new Claim("fullName", $"{user.Nombre} {user.Apellido}")
        };

        // 3) Generar token
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires= DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:ExpireMinutes"]!));

        var token = new JwtSecurityToken(
            issuer:    _config["Jwt:Issuer"],
            audience:  _config["Jwt:Audience"],
            claims:    claims,
            expires:   expires,
            signingCredentials: creds
        );

        return Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            usuarioId = user.UsuarioId
        });
    }

    private bool VerifyPassword(string plain, string storedHash)
    {
        // aquí tu lógica de hashing/verificación (BCrypt, etc.)
        return BCrypt.Net.BCrypt.Verify(plain, storedHash);
    }
}
