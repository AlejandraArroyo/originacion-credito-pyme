using System.Threading.Tasks;
using Backend.Modelos;
using Backend.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

public record RegistrarDictamenRequest(Dictamen Dictamen, string ClaveIdempotencia);

[ApiController]
[Route("api/[controller]")]
public class DictamenesController : ControllerBase
{
    private readonly DictamenRepositorio _dictamenRepositorio;
    private readonly MetricasRepositorio _metricasRepositorio;

    public DictamenesController(DictamenRepositorio dictamenRepositorio, MetricasRepositorio metricasRepositorio)
    {
        _dictamenRepositorio = dictamenRepositorio;
        _metricasRepositorio = metricasRepositorio;
    }

    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] RegistrarDictamenRequest request)
    {
        var resultado = await _dictamenRepositorio.RegistrarDictamenAsync(request.Dictamen, request.ClaveIdempotencia);
        if (!resultado.Exitoso)
        {
            return BadRequest(resultado);
        }
        return Ok(resultado);
    }

    [HttpGet("metricas")]
    public async Task<IActionResult> Metricas()
    {
        var metricas = await _metricasRepositorio.ObtenerMetricasAsync();
        return Ok(metricas);
    }
}