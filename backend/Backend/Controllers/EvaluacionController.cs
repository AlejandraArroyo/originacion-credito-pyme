using System.Threading.Tasks;
using Backend.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EvaluacionController : ControllerBase
{
    private readonly EvaluacionRepositorio _evaluacionRepositorio;

    public EvaluacionController(EvaluacionRepositorio evaluacionRepositorio)
    {
        _evaluacionRepositorio = evaluacionRepositorio;
    }

    [HttpPost("seleccionar-casos")]
    public async Task<IActionResult> SeleccionarCasos()
    {
        var casos = await _evaluacionRepositorio.SeleccionarCasosAsync();
        return Ok(new { total = casos.Count, casos });
    }

    [HttpPost("ejecutar")]
    public async Task<IActionResult> Ejecutar()
    {
        var resultados = await _evaluacionRepositorio.EjecutarCasosAsync();
        var totalPasaron = resultados.FindAll(r => r.Paso).Count;
        return Ok(new { total = resultados.Count, pasaron = totalPasaron, resultados });
    }
}