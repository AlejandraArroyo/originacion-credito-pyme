using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;

namespace Backend.Servicios;

public record FragmentoPolitica(string IdPolitica, string Seccion, string TextoLiteral, string Categoria);

public class PoliticaRepositorio
{
    private readonly string _connectionString;

    public PoliticaRepositorio(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings");
    }

    public async Task<List<FragmentoPolitica>> BuscarPoliticasAsync(string consulta, int topK)
    {
        var resultados = new List<FragmentoPolitica>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT id_politica, seccion, texto, categoria
            FROM politicas
            WHERE to_tsvector('spanish', texto || ' ' || seccion || ' ' || categoria)
                  @@ plainto_tsquery('spanish', @consulta)
            ORDER BY ts_rank(
                to_tsvector('spanish', texto || ' ' || seccion || ' ' || categoria),
                plainto_tsquery('spanish', @consulta)
            ) DESC
            LIMIT @topK", conn);

        cmd.Parameters.AddWithValue("consulta", consulta);
        cmd.Parameters.AddWithValue("topK", topK);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            resultados.Add(new FragmentoPolitica(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)
            ));
        }

        return resultados;
    }

    public async Task<bool> ExisteCitaVerificableAsync(string idPolitica, string textoLiteral)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "SELECT texto FROM politicas WHERE id_politica = @id", conn);
        cmd.Parameters.AddWithValue("id", idPolitica);

        var textoReal = await cmd.ExecuteScalarAsync() as string;
        return textoReal is not null && textoReal.Trim() == textoLiteral.Trim();
    }
}