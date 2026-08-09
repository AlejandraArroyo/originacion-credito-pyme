using System;
using System.Threading.Tasks;
using Npgsql;

namespace Backend.Servicios;

public class IndicadoresRepositorio
{
    private readonly string _connectionString;
    private readonly SolicitudRepositorio _solicitudRepositorio;

    public IndicadoresRepositorio(IConfiguration config, SolicitudRepositorio solicitudRepositorio)
    {
        _connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings");
        _solicitudRepositorio = solicitudRepositorio;
    }

    public async Task<Indicadores?> CalcularIndicadoresAsync(Guid idSolicitud)
    {
        var solicitud = await _solicitudRepositorio.ObtenerSolicitudAsync(idSolicitud);
        if (solicitud is null)
        {
            return null;
        }

        var entrada = new SolicitudParaIndicadores(
            solicitud.MontoSolicitado,
            solicitud.PlazoMeses,
            solicitud.MesesOperacion,
            solicitud.VentasAnuales,
            solicitud.UtilidadNeta,
            solicitud.ActivosTotales,
            solicitud.PasivosTotales,
            solicitud.DeudaVigenteAnual
        );

        var indicadores = CalculadoraIndicadores.Calcular(entrada);

        await GuardarIndicadoresAsync(idSolicitud, indicadores);

        return indicadores;
    }

    private async Task GuardarIndicadoresAsync(Guid idSolicitud, Indicadores indicadores)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO indicadores_solicitud (
                id_solicitud, razon_endeudamiento, margen_neto, cobertura_servicio_deuda,
                relacion_monto_ventas, antiguedad_meses, datos_incompletos, calculado_en
            ) VALUES (
                @id, @razon, @margen, @cobertura, @relacion, @antiguedad, @incompletos, now()
            )
            ON CONFLICT (id_solicitud) DO UPDATE SET
                razon_endeudamiento = EXCLUDED.razon_endeudamiento,
                margen_neto = EXCLUDED.margen_neto,
                cobertura_servicio_deuda = EXCLUDED.cobertura_servicio_deuda,
                relacion_monto_ventas = EXCLUDED.relacion_monto_ventas,
                antiguedad_meses = EXCLUDED.antiguedad_meses,
                datos_incompletos = EXCLUDED.datos_incompletos,
                calculado_en = EXCLUDED.calculado_en", conn);

        cmd.Parameters.AddWithValue("id", idSolicitud);
        cmd.Parameters.AddWithValue("razon", (object?)indicadores.RazonEndeudamiento ?? DBNull.Value);
        cmd.Parameters.AddWithValue("margen", (object?)indicadores.MargenNeto ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cobertura", (object?)indicadores.CoberturaServicioDeuda ?? DBNull.Value);
        cmd.Parameters.AddWithValue("relacion", indicadores.RelacionMontoVentas);
        cmd.Parameters.AddWithValue("antiguedad", indicadores.AntiguedadMeses);
        cmd.Parameters.AddWithValue("incompletos", indicadores.DatosIncompletos);

        await cmd.ExecuteNonQueryAsync();

        await using var cmdSolicitud = new NpgsqlCommand(
            "UPDATE solicitudes SET indicadores_vigentes = TRUE WHERE id_solicitud = @id", conn);
        cmdSolicitud.Parameters.AddWithValue("id", idSolicitud);
        await cmdSolicitud.ExecuteNonQueryAsync();
    }
}