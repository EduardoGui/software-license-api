using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public class UsuarioService : IUsuarioService
{
    private readonly AppDbContext _context;

    public UsuarioService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<UsuarioDto>> GetAllAsync()
    {
        return await _context.Usuarios
            .OrderBy(u => u.Nome)
            .Select(u => new UsuarioDto
            {
                Id = u.Id,
                Nome = u.Nome,
                Email = u.Email,
                DataInicio = u.DataInicio,
                DataFim = u.DataFim,
            })
            .ToListAsync();
    }
}
