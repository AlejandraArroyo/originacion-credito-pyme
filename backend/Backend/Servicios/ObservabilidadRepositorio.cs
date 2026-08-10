using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Npgsql;

namespace Backend.Servicios;

public class ObservabilidadRepositorio
{
    private readonly string _connectionString;

    public ObservabilidadRepositorio(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings");
    }

    public async Task RegistrarEjecucionAsync(
        Guid idSesion,
        string versionPrompt,
        string modelo,
        IEnumerable<ChatMessage> mensajes,
        long latenciaMs,
        int? tokensEntrada,
        int? tokensSalida)
    {
        var secuenciaHerramientas = new List<object>();

        foreach (var mensaje in mensajes)
        {
            foreach (var contenido in mensaje.Contents)
            {
                if (contenido is FunctionCallContent llamada)
                {
                    secuenciaHerramientas.Add(new
                    {
                        tipo = "llamada",
                        herramienta = llamada.Name,
                        argumentos = llamada.Arguments,
                    });
                }
                else if (contenido is FunctionResultContent resultado)
                {
                    secuenciaHerramientas.Add(new
                    {
                        tipo = "resultado",
                        id_llamada = resultado.CallId,
                        resultado = resultado.Result?.ToString(),
                    });
                }
            }
        }

        var costoEstimado = 0.0m;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO ejecuciones_agente (
                id_sesion, version_prompt, modelo, secuencia_herramientas,
                tokens_entrada, tokens_salida, latencia_ms, costo_estimado_usd
            ) VALUES (
                @idSesion, @versionPrompt, @modelo, @secuencia::jsonb,
                @tokensEntrada, @tokensSalida, @latenciaMs, @costo
            )", conn);

        cmd.Parameters.AddWithValue("idSesion", idSesion);
        cmd.Parameters.AddWithValue("versionPrompt", versionPrompt);
        cmd.Parameters.AddWithValue("modelo", modelo);
        cmd.Parameters.AddWithValue("secuencia", System.Text.Json.JsonSerializer.Serialize(secuenciaHerramientas));
        cmd.Parameters.AddWithValue("tokensEntrada", (object?)tokensEntrada ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tokensSalida", (object?)tokensSalida ?? DBNull.Value);
        cmd.Parameters.AddWithValue("latenciaMs", (int)latenciaMs);
        cmd.Parameters.AddWithValue("costo", costoEstimado);

        await cmd.ExecuteNonQueryAsync();
    }
}