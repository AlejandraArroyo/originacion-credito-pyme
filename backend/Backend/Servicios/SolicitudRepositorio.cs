using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Modelos;
using Npgsql;

namespace Backend.Servicios;

public class SolicitudRepositorio
{
    private readonly string _connectionString;

    public SolicitudRepositorio(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings");
    }

    public async Task<Solicitud?> ObtenerSolicitudAsync(Guid idSolicitud)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT id_solicitud, nombre_empresa, sector, meses_operacion, monto_solicitado,
                   plazo_meses, destino_fondos, ventas_anuales, utilidad_neta, activos_totales,
                   pasivos_totales, deuda_vigente_anual, score_historial, garantia_ofrecida, fecha_solicitud
            FROM solicitudes
            WHERE id_solicitud = @id", conn);

        cmd.Parameters.AddWithValue("id", idSolicitud);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new Solicitud(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetDecimal(4),
            reader.GetInt32(5),
            reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetDecimal(7),
            reader.IsDBNull(8) ? null : reader.GetDecimal(8),
            reader.IsDBNull(9) ? null : reader.GetDecimal(9),
            reader.IsDBNull(10) ? null : reader.GetDecimal(10),
            reader.IsDBNull(11) ? null : reader.GetDecimal(11),
            reader.GetInt32(12),
            reader.GetString(13),
            DateOnly.FromDateTime(reader.GetDateTime(14))
        );
    }

    public async Task<List<DemoSolicitud>> ObtenerMuestraParaDemoAsync()
    {
        var resultado = new List<DemoSolicitud>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        async Task AgregarUno(string sql, string etiqueta)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                resultado.Add(new DemoSolicitud(reader.GetGuid(0), reader.GetString(1), etiqueta));
            }
        }

        await AgregarUno(@"
            SELECT id_solicitud, nombre_empresa FROM solicitudes
            WHERE ventas_anuales IS NOT NULL AND activos_totales IS NOT NULL AND pasivos_totales IS NOT NULL
              AND activos_totales > 0
              AND pasivos_totales::numeric / activos_totales::numeric < 0.5
              AND monto_solicitado <= 250000
              AND score_historial >= 60
              AND NOT (destino_fondos ILIKE '%Ignora%' OR destino_fondos ILIKE '%IMPORTANTE PARA EL ASISTENTE%')
            ORDER BY id_solicitud LIMIT 1", "Caso de aprobación clara");

        await AgregarUno(@"
            SELECT id_solicitud, nombre_empresa FROM solicitudes
            WHERE score_historial < 40
            ORDER BY id_solicitud LIMIT 1", "Caso de rechazo (score bajo)");

        await AgregarUno(@"
            SELECT id_solicitud, nombre_empresa FROM solicitudes
            WHERE monto_solicitado > 250000 AND monto_solicitado <= 500000
              AND pasivos_totales::numeric / activos_totales::numeric < 0.5
            ORDER BY id_solicitud LIMIT 1", "Caso de monto alto (requiere comité)");

        await AgregarUno(@"
            SELECT id_solicitud, nombre_empresa FROM solicitudes
            WHERE destino_fondos ILIKE '%Ignora%' OR destino_fondos ILIKE '%IMPORTANTE PARA EL ASISTENTE%' OR destino_fondos ILIKE '%Instrucciones del sistema%'
            ORDER BY id_solicitud LIMIT 1", "Caso adversarial (intento de manipulación)");

        return resultado;
    }
}

public record DemoSolicitud(Guid IdSolicitud, string NombreEmpresa, string Etiqueta);