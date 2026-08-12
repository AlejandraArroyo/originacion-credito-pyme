using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;

namespace Backend.Servicios;

public record MetricasCartera(
    Dictionary<string, long> SolicitudesPorEstado,
    decimal MontoPromedioRecomendado,
    decimal TasaEscalamiento,
    long TotalDictamenes
);

public record DictamenRechazado(
    Guid IdDictamen,
    Guid IdSolicitud,
    string NombreEmpresa,
    List<string> Motivos,
    DateTime CreadoEn
);

public class MetricasRepositorio
{
    private readonly string _connectionString;

    public MetricasRepositorio(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings");
    }

    public async Task<MetricasCartera> ObtenerMetricasAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var porEstado = new Dictionary<string, long>();
        await using (var cmd = new NpgsqlCommand(
            "SELECT decision, count(*) FROM dictamenes GROUP BY decision", conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                porEstado[reader.GetString(0)] = reader.GetInt64(1);
            }
        }

        decimal montoPromedio = 0m;
        await using (var cmd = new NpgsqlCommand(
            "SELECT COALESCE(AVG(monto_recomendado), 0) FROM dictamenes WHERE monto_recomendado IS NOT NULL", conn))
        {
            var resultado = await cmd.ExecuteScalarAsync();
            montoPromedio = resultado is decimal d ? Math.Round(d, 2) : 0m;
        }

        long totalDictamenes = 0;
        long totalEscalados = 0;
        await using (var cmd = new NpgsqlCommand("SELECT count(*) FROM dictamenes", conn))
        {
            totalDictamenes = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        }
        await using (var cmd = new NpgsqlCommand(
            "SELECT count(*) FROM dictamenes WHERE decision = 'ESCALADO_A_COMITE'", conn))
        {
            totalEscalados = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        }

        var tasaEscalamiento = totalDictamenes > 0
            ? Math.Round((decimal)totalEscalados / totalDictamenes, 4)
            : 0m;

        return new MetricasCartera(porEstado, montoPromedio, tasaEscalamiento, totalDictamenes);
    }

    public async Task<List<DictamenRechazado>> ObtenerRechazadosAsync(int limite = 20)
    {
        var resultado = new List<DictamenRechazado>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT d.id_dictamen, d.id_solicitud, s.nombre_empresa, d.motivos, d.creado_en
            FROM dictamenes d
            JOIN solicitudes s ON s.id_solicitud = d.id_solicitud
            WHERE d.decision = 'RECHAZADO'
            ORDER BY d.creado_en DESC
            LIMIT @limite", conn);
        cmd.Parameters.AddWithValue("limite", limite);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var motivosJson = reader.GetString(3);
            var motivos = System.Text.Json.JsonSerializer.Deserialize<List<string>>(motivosJson) ?? new List<string>();

            resultado.Add(new DictamenRechazado(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                motivos,
                reader.GetDateTime(4)
            ));
        }

        return resultado;
    }
}