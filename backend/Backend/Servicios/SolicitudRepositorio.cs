using System;
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
}