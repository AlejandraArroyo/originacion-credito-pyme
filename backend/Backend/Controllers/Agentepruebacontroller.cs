using System.Threading.Tasks;
using Backend.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentePruebaController : ControllerBase
{
    private readonly AgenteFactory _agenteFactory;

    public AgentePruebaController(AgenteFactory agenteFactory)
    {
        _agenteFactory = agenteFactory;
    }

    [HttpPost("saludo")]
    public async Task<IActionResult> Saludo([FromQuery] string mensaje = "Hola, responde en una sola linea")
    {
        var agente = _agenteFactory.CrearAgente("Eres un asistente que responde de forma breve y en espanol.");
        var respuesta = await agente.RunAsync(mensaje);
        return Ok(new { respuesta = respuesta.ToString() });
    }
}