using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Modelos;
using Npgsql;

namespace Backend.Servicios;

public record ResultadoRegistro(bool Exitoso, Guid? IdDictamen, string? Estado, List<string> Errores);

public class DictamenRepositorio
{
    private readonly string _connectionString;
    private readonly PoliticaRepositorio _politicaRepositorio;
    private readonly IndicadoresRepositorio _indicadoresRepositorio;

    public DictamenRepositorio(
        IConfiguration config,
        PoliticaRepositorio politicaRepositorio,
        IndicadoresRepositorio indicadoresRepositorio)
    {
        _connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings");
        _politicaRepositorio = politicaRepositorio;
        _indicadoresRepositorio = indicadoresRepositorio;
    }

    public async Task<ResultadoRegistro> RegistrarDictamenAsync(Dictamen dictamen, string claveIdempotencia)
    {
        var validacionEsquema = DictamenValidador.Validar(dictamen);
        if (!validacionEsquema.EsValido)
        {
            return new ResultadoRegistro(false, null, null, validacionEsquema.Errores);
        }

        var existente = await BuscarPorClaveIdempotenciaAsync(claveIdempotencia);
        if (existente is not null)
        {
            return new ResultadoRegistro(true, existente.Value.Id, existente.Value.Estado, new List<string>());
        }

        var decisionFinal = dictamen.Decision;
        var montoFinal = dictamen.MontoRecomendado;
        var plazoFinal = dictamen.PlazoRecomendadoMeses;
        var motivosFinal = new List<string>(dictamen.Motivos);
        var requiereAutorizacionFinal = dictamen.RequiereAutorizacionHumana;

        foreach (var cita in dictamen.PoliticasCitadas)
        {
            var esVerificable = await _politicaRepositorio.ExisteCitaVerificableAsync(cita.IdPolitica, cita.TextoLiteral);
            if (!esVerificable)
            {
                decisionFinal = "ESCALADO_A_COMITE";
                montoFinal = null;
                plazoFinal = null;
                requiereAutorizacionFinal = true;
                motivosFinal.Add($"G1: cita '{cita.IdPolitica}' no coincide con el corpus de politicas, escalado automatico");
                break;
            }
        }

        var indicadoresRecalculados = await _indicadoresRepositorio.CalcularIndicadoresAsync(dictamen.IdSolicitud);
        if (indicadoresRecalculados is null)
        {
            return new ResultadoRegistro(false, null, null,
                new List<string> { "No se pudo recalcular indicadores: solicitud no encontrada" });
        }

        if (!IndicadoresCoinciden(dictamen.Indicadores, indicadoresRecalculados))
        {
            return new ResultadoRegistro(false, null, null,
                new List<string> { "G2: los indicadores del dictamen no coinciden con calcular_indicadores" });
        }

        var umbralAutorizacion = await ObtenerUmbralAutorizacionAsync();
        if ((montoFinal is decimal monto && monto > umbralAutorizacion) || dictamen.NivelRiesgo == "ALTO")
        {
            requiereAutorizacionFinal = true;
        }

        var estadoFinal = requiereAutorizacionFinal ? "PENDIENTE_AUTORIZACION" : "BORRADOR";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            var idDictamen = Guid.NewGuid();

            await using (var cmd = new NpgsqlCommand(@"
                INSERT INTO dictamenes (
                    id_dictamen, id_solicitud, decision, monto_recomendado, plazo_recomendado_meses,
                    indicadores, motivos, nivel_riesgo, requiere_autorizacion_humana,
                    confianza, estado, clave_idempotencia, es_historico
                ) VALUES (
                    @idDictamen, @idSolicitud, @decision, @monto, @plazo,
                    @indicadores::jsonb, @motivos::jsonb, @nivelRiesgo, @requiereAuth,
                    @confianza, @estado, @claveIdem, FALSE
                )", conn, tx))
            {
                cmd.Parameters.AddWithValue("idDictamen", idDictamen);
                cmd.Parameters.AddWithValue("idSolicitud", dictamen.IdSolicitud);
                cmd.Parameters.AddWithValue("decision", decisionFinal);
                cmd.Parameters.AddWithValue("monto", (object?)montoFinal ?? DBNull.Value);
                cmd.Parameters.AddWithValue("plazo", (object?)plazoFinal ?? DBNull.Value);
                cmd.Parameters.AddWithValue("indicadores", System.Text.Json.JsonSerializer.Serialize(indicadoresRecalculados));
                cmd.Parameters.AddWithValue("motivos", System.Text.Json.JsonSerializer.Serialize(motivosFinal));
                cmd.Parameters.AddWithValue("nivelRiesgo", dictamen.NivelRiesgo);
                cmd.Parameters.AddWithValue("requiereAuth", requiereAutorizacionFinal);
                cmd.Parameters.AddWithValue("confianza", (decimal)dictamen.Confianza);
                cmd.Parameters.AddWithValue("estado", estadoFinal);
                cmd.Parameters.AddWithValue("claveIdem", claveIdempotencia);

                await cmd.ExecuteNonQueryAsync();
            }

            foreach (var cita in dictamen.PoliticasCitadas)
            {
                await using var cmdCita = new NpgsqlCommand(@"
                    INSERT INTO citas_politica (id_dictamen, id_politica, seccion, texto_literal)
                    VALUES (@idDictamen, @idPolitica, @seccion, @textoLiteral)", conn, tx);
                cmdCita.Parameters.AddWithValue("idDictamen", idDictamen);
                cmdCita.Parameters.AddWithValue("idPolitica", cita.IdPolitica);
                cmdCita.Parameters.AddWithValue("seccion", cita.Seccion);
                cmdCita.Parameters.AddWithValue("textoLiteral", cita.TextoLiteral);
                await cmdCita.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return new ResultadoRegistro(true, idDictamen, estadoFinal, new List<string>());
        }
        catch (PostgresException ex)
        {
            await tx.RollbackAsync();
            return new ResultadoRegistro(false, null, null, new List<string> { $"Rechazado por base de datos: {ex.MessageText}" });
        }
    }

    private bool IndicadoresCoinciden(Indicadores a, Indicadores b)
    {
        return a.RazonEndeudamiento == b.RazonEndeudamiento
            && a.MargenNeto == b.MargenNeto
            && a.CoberturaServicioDeuda == b.CoberturaServicioDeuda
            && a.RelacionMontoVentas == b.RelacionMontoVentas
            && a.AntiguedadMeses == b.AntiguedadMeses;
    }

    private async Task<decimal> ObtenerUmbralAutorizacionAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT valor FROM parametros_politica WHERE clave = 'umbral_autorizacion_comite'", conn);
        var resultado = await cmd.ExecuteScalarAsync();
        return resultado is decimal valor ? valor : 250000.00m;
    }

    private async Task<(Guid Id, string Estado)?> BuscarPorClaveIdempotenciaAsync(string claveIdempotencia)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id_dictamen, estado FROM dictamenes WHERE clave_idempotencia = @clave", conn);
        cmd.Parameters.AddWithValue("clave", claveIdempotencia);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return (reader.GetGuid(0), reader.GetString(1));
        }
        return null;
    }

    public async Task<ResultadoRegistro> ConfirmarDictamenAsync(Guid idDictamen, string confirmadoPor)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            UPDATE dictamenes
            SET estado = 'CONFIRMADO', confirmado_en = now(), confirmado_por = @confirmadoPor
            WHERE id_dictamen = @id AND estado IN ('BORRADOR', 'PENDIENTE_AUTORIZACION')
            RETURNING estado", conn);

        cmd.Parameters.AddWithValue("id", idDictamen);
        cmd.Parameters.AddWithValue("confirmadoPor", confirmadoPor);

        var estadoResultante = await cmd.ExecuteScalarAsync() as string;

        if (estadoResultante is null)
        {
            return new ResultadoRegistro(false, null, null,
                new List<string> { "El dictamen no existe o ya no esta en un estado confirmable (BORRADOR/PENDIENTE_AUTORIZACION)." });
        }

        return new ResultadoRegistro(true, idDictamen, estadoResultante, new List<string>());
    }
}