using System;
using System.Threading.Tasks;
using Backend.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SolicitudesController : ControllerBase
{
    private readonly SolicitudRepositorio _solicitudRepositorio;
    private readonly IndicadoresRepositorio _indicadoresRepositorio;

    public SolicitudesController(
        SolicitudRepositorio solicitudRepositorio,
        IndicadoresRepositorio indicadoresRepositorio)
    {
        _solicitudRepositorio = solicitudRepositorio;
        _indicadoresRepositorio = indicadoresRepositorio;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ObtenerSolicitud(Guid id)
    {
        var solicitud = await _solicitudRepositorio.ObtenerSolicitudAsync(id);
        if (solicitud is null)
        {
            return NotFound(new { mensaje = "Solicitud no encontrada" });
        }
        return Ok(solicitud);
    }

    [HttpGet("{id}/indicadores")]
    public async Task<IActionResult> CalcularIndicadores(Guid id)
    {
        var indicadores = await _indicadoresRepositorio.CalcularIndicadoresAsync(id);
        if (indicadores is null)
        {
            return NotFound(new { mensaje = "Solicitud no encontrada" });
        }
        return Ok(indicadores);
    }

    [HttpGet("muestra-demo")]
    public async Task<IActionResult> MuestraDemo()
    {
        var muestra = await _solicitudRepositorio.ObtenerMuestraParaDemoAsync();
        return Ok(muestra);
    }
}