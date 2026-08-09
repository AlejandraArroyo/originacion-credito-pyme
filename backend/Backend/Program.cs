using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;

var connectionString = "Host=localhost;Port=5432;Database=originacion_credito;Username=originacion;Password=originacion_dev";
var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "data");

var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

var politicasPath = Path.Combine(dataDir, "politicas_credito.json");
var solicitudesPath = Path.Combine(dataDir, "solicitudes.json");
var historicoPath = Path.Combine(dataDir, "historico_dictamenes.json");

var politicasDoc = JsonSerializer.Deserialize<PoliticasDocumento>(File.ReadAllText(politicasPath), jsonOptions)
    ?? throw new Exception("No se pudo leer politicas_credito.json");
var solicitudes = JsonSerializer.Deserialize<List<Solicitud>>(File.ReadAllText(solicitudesPath), jsonOptions)
    ?? throw new Exception("No se pudo leer solicitudes.json");
var historico = JsonSerializer.Deserialize<List<DictamenHistorico>>(File.ReadAllText(historicoPath), jsonOptions)
    ?? throw new Exception("No se pudo leer historico_dictamenes.json");

await using var conn = new NpgsqlConnection(connectionString);
await conn.OpenAsync();
await using var tx = await conn.BeginTransactionAsync();

try
{
    await SeedPoliticasAsync(conn, tx, politicasDoc);
    await SeedSolicitudesAsync(conn, tx, solicitudes);
    await SeedHistoricoAsync(conn, tx, historico);

    await tx.CommitAsync();
    Console.WriteLine("Seed completado.");
}
catch
{
    await tx.RollbackAsync();
    throw;
}

async Task SeedPoliticasAsync(NpgsqlConnection c, NpgsqlTransaction t, PoliticasDocumento doc)
{
    foreach (var p in doc.Politicas)
    {
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO politicas (id_politica, seccion, categoria, texto, severidad, version_corpus)
            VALUES (@id, @seccion, @categoria, @texto, @severidad, @version)
            ON CONFLICT (id_politica) DO UPDATE SET
                seccion = EXCLUDED.seccion,
                categoria = EXCLUDED.categoria,
                texto = EXCLUDED.texto,
                severidad = EXCLUDED.severidad,
                version_corpus = EXCLUDED.version_corpus", c, t);
        cmd.Parameters.AddWithValue("id", p.Id);
        cmd.Parameters.AddWithValue("seccion", p.Seccion);
        cmd.Parameters.AddWithValue("categoria", p.Categoria);
        cmd.Parameters.AddWithValue("texto", p.Texto);
        cmd.Parameters.AddWithValue("severidad", p.Severidad);
        cmd.Parameters.AddWithValue("version", doc.Version);
        await cmd.ExecuteNonQueryAsync();
    }
    Console.WriteLine($"politicas: {doc.Politicas.Count} filas");
}

async Task SeedSolicitudesAsync(NpgsqlConnection c, NpgsqlTransaction t, List<Solicitud> lista)
{
    foreach (var s in lista)
    {
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO solicitudes (
                id_solicitud, nombre_empresa, sector, meses_operacion, monto_solicitado,
                plazo_meses, destino_fondos, ventas_anuales, utilidad_neta, activos_totales,
                pasivos_totales, deuda_vigente_anual, score_historial, garantia_ofrecida, fecha_solicitud
            ) VALUES (
                @id, @nombre, @sector, @meses, @monto, @plazo, @destino,
                @ventas, @utilidad, @activos, @pasivos, @deuda, @score, @garantia, @fecha
            ) ON CONFLICT (id_solicitud) DO NOTHING", c, t);

        cmd.Parameters.AddWithValue("id", Guid.Parse(s.IdSolicitud));
        cmd.Parameters.AddWithValue("nombre", s.NombreEmpresa);
        cmd.Parameters.AddWithValue("sector", s.Sector);
        cmd.Parameters.AddWithValue("meses", s.MesesOperacion);
        cmd.Parameters.AddWithValue("monto", decimal.Parse(s.MontoSolicitado));
        cmd.Parameters.AddWithValue("plazo", s.PlazoMeses);
        cmd.Parameters.AddWithValue("destino", s.DestinoFondos);
        cmd.Parameters.AddWithValue("ventas", (object?)ParseDecimalOrNull(s.VentasAnuales) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("utilidad", (object?)ParseDecimalOrNull(s.UtilidadNeta) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("activos", (object?)ParseDecimalOrNull(s.ActivosTotales) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pasivos", (object?)ParseDecimalOrNull(s.PasivosTotales) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("deuda", (object?)ParseDecimalOrNull(s.DeudaVigenteAnual) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("score", s.ScoreHistorial);
        cmd.Parameters.AddWithValue("garantia", s.GarantiaOfrecida);
        cmd.Parameters.AddWithValue("fecha", DateOnly.Parse(s.FechaSolicitud));

        await cmd.ExecuteNonQueryAsync();
    }
    Console.WriteLine($"solicitudes: {lista.Count} filas");
}

async Task SeedHistoricoAsync(NpgsqlConnection c, NpgsqlTransaction t, List<DictamenHistorico> lista)
{
    foreach (var h in lista)
    {
        await using (var cmdSolicitud = new NpgsqlCommand(@"
            INSERT INTO solicitudes (
                id_solicitud, nombre_empresa, sector, meses_operacion, monto_solicitado,
                plazo_meses, destino_fondos, score_historial, garantia_ofrecida, fecha_solicitud
            ) VALUES (@id, @nombre, @sector, @meses, @monto, @plazo, @destino, @score, @garantia, @fecha)
            ON CONFLICT (id_solicitud) DO NOTHING", c, t))
        {
            cmdSolicitud.Parameters.AddWithValue("id", Guid.Parse(h.IdSolicitud));
            cmdSolicitud.Parameters.AddWithValue("nombre", "Historico (placeholder)");
            cmdSolicitud.Parameters.AddWithValue("sector", "otros");
            cmdSolicitud.Parameters.AddWithValue("meses", 24);
            cmdSolicitud.Parameters.AddWithValue("monto", ParseDecimalOrNull(h.MontoRecomendado) ?? 10000.00m);
            cmdSolicitud.Parameters.AddWithValue("plazo", 24);
            cmdSolicitud.Parameters.AddWithValue("destino", "n/a");
            cmdSolicitud.Parameters.AddWithValue("score", 50);
            cmdSolicitud.Parameters.AddWithValue("garantia", "ninguna");
            cmdSolicitud.Parameters.AddWithValue("fecha", DateOnly.Parse(h.FechaDictamen));
            await cmdSolicitud.ExecuteNonQueryAsync();
        }

        await using var cmdDictamen = new NpgsqlCommand(@"
            INSERT INTO dictamenes (
                id_dictamen, id_solicitud, decision, monto_recomendado, plazo_recomendado_meses,
                indicadores, motivos, nivel_riesgo, requiere_autorizacion_humana,
                confianza, estado, clave_idempotencia, es_historico, creado_en
            ) VALUES (
                @idDictamen, @idSolicitud, @decision, @monto, 24,
                '{}', '[]', @nivelRiesgo, @requiereAuth,
                0.85, 'CONFIRMADO', @claveIdem, TRUE, @creadoEn
            )", c, t);

        cmdDictamen.Parameters.AddWithValue("idDictamen", Guid.Parse(h.IdDictamen));
        cmdDictamen.Parameters.AddWithValue("idSolicitud", Guid.Parse(h.IdSolicitud));
        cmdDictamen.Parameters.AddWithValue("decision", h.Decision);
        cmdDictamen.Parameters.AddWithValue("monto", (object?)ParseDecimalOrNull(h.MontoRecomendado) ?? DBNull.Value);
        cmdDictamen.Parameters.AddWithValue("nivelRiesgo", h.NivelRiesgo);
        cmdDictamen.Parameters.AddWithValue("requiereAuth", h.RequiereAutorizacionHumana);
        cmdDictamen.Parameters.AddWithValue("claveIdem", Guid.NewGuid().ToString());
        cmdDictamen.Parameters.AddWithValue("creadoEn", DateTime.Parse(h.FechaDictamen));

        await cmdDictamen.ExecuteNonQueryAsync();
    }
    Console.WriteLine($"historico_dictamenes: {lista.Count} filas");
}

static decimal? ParseDecimalOrNull(string? valor)
    => string.IsNullOrEmpty(valor) ? null : decimal.Parse(valor);

class PoliticasDocumento
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";
    [JsonPropertyName("politicas")]
    public List<Politica> Politicas { get; set; } = new();
}

class Politica
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("seccion")] public string Seccion { get; set; } = "";
    [JsonPropertyName("categoria")] public string Categoria { get; set; } = "";
    [JsonPropertyName("texto")] public string Texto { get; set; } = "";
    [JsonPropertyName("severidad")] public string Severidad { get; set; } = "";
}

class Solicitud
{
    [JsonPropertyName("id_solicitud")] public string IdSolicitud { get; set; } = "";
    [JsonPropertyName("nombre_empresa")] public string NombreEmpresa { get; set; } = "";
    [JsonPropertyName("sector")] public string Sector { get; set; } = "";
    [JsonPropertyName("meses_operacion")] public int MesesOperacion { get; set; }
    [JsonPropertyName("monto_solicitado")] public string MontoSolicitado { get; set; } = "";
    [JsonPropertyName("plazo_meses")] public int PlazoMeses { get; set; }
    [JsonPropertyName("destino_fondos")] public string DestinoFondos { get; set; } = "";
    [JsonPropertyName("ventas_anuales")] public string? VentasAnuales { get; set; }
    [JsonPropertyName("utilidad_neta")] public string? UtilidadNeta { get; set; }
    [JsonPropertyName("activos_totales")] public string? ActivosTotales { get; set; }
    [JsonPropertyName("pasivos_totales")] public string? PasivosTotales { get; set; }
    [JsonPropertyName("deuda_vigente_anual")] public string? DeudaVigenteAnual { get; set; }
    [JsonPropertyName("score_historial")] public int ScoreHistorial { get; set; }
    [JsonPropertyName("garantia_ofrecida")] public string GarantiaOfrecida { get; set; } = "";
    [JsonPropertyName("fecha_solicitud")] public string FechaSolicitud { get; set; } = "";
}

class DictamenHistorico
{
    [JsonPropertyName("id_dictamen")] public string IdDictamen { get; set; } = "";
    [JsonPropertyName("id_solicitud")] public string IdSolicitud { get; set; } = "";
    [JsonPropertyName("decision")] public string Decision { get; set; } = "";
    [JsonPropertyName("monto_recomendado")] public string? MontoRecomendado { get; set; }
    [JsonPropertyName("nivel_riesgo")] public string NivelRiesgo { get; set; } = "";
    [JsonPropertyName("requiere_autorizacion_humana")] public bool RequiereAutorizacionHumana { get; set; }
    [JsonPropertyName("fecha_dictamen")] public string FechaDictamen { get; set; } = "";
}