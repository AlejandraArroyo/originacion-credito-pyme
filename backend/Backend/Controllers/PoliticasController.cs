using System.Threading.Tasks;
using Backend.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoliticasController : ControllerBase
{
    private readonly PoliticaRepositorio _politicaRepositorio;

    public PoliticasController(PoliticaRepositorio politicaRepositorio)
    {
        _politicaRepositorio = politicaRepositorio;
    }

    [HttpGet("buscar")]
    public async Task<IActionResult> Buscar([FromQuery] string consulta, [FromQuery] int topK = 5)
    {
        var resultados = await _politicaRepositorio.BuscarPoliticasAsync(consulta, topK);
        return Ok(resultados);
    }
}