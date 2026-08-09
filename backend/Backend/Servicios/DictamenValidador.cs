using System;
using System.Collections.Generic;
using System.Linq;
using Backend.Modelos;

namespace Backend.Servicios;

public record ResultadoValidacion(bool EsValido, List<string> Errores);

public static class DictamenValidador
{
    private static readonly HashSet<string> DecisionesValidas = new()
    {
        "APROBADO", "RECHAZADO", "ESCALADO_A_COMITE"
    };

    private static readonly HashSet<string> NivelesRiesgoValidos = new()
    {
        "BAJO", "MEDIO", "ALTO"
    };

    public static ResultadoValidacion Validar(Dictamen dictamen)
    {
        var errores = new List<string>();

        if (!DecisionesValidas.Contains(dictamen.Decision))
        {
            errores.Add($"decision fuera del enum permitido: '{dictamen.Decision}'");
        }

        if (!NivelesRiesgoValidos.Contains(dictamen.NivelRiesgo))
        {
            errores.Add($"nivel_riesgo fuera del enum permitido: '{dictamen.NivelRiesgo}'");
        }

        if (dictamen.PoliticasCitadas is null || dictamen.PoliticasCitadas.Count == 0)
        {
            errores.Add("politicas_citadas debe tener al menos 1 elemento");
        }

        if (dictamen.Confianza < 0.0 || dictamen.Confianza > 1.0)
        {
            errores.Add($"confianza fuera de rango [0.0, 1.0]: {dictamen.Confianza}");
        }

        if (dictamen.Decision == "APROBADO" && dictamen.MontoRecomendado is null)
        {
            errores.Add("decision APROBADO requiere monto_recomendado no nulo");
        }

        if (dictamen.Decision == "RECHAZADO" && dictamen.MontoRecomendado is not null)
        {
            errores.Add("decision RECHAZADO no debe traer monto_recomendado");
        }

        if (dictamen.MontoRecomendado is decimal monto && monto <= 0)
        {
            errores.Add("monto_recomendado debe ser mayor a 0 cuando esta presente");
        }

        if (dictamen.Motivos is null || dictamen.Motivos.Count == 0)
        {
            errores.Add("motivos debe tener al menos 1 elemento");
        }

        foreach (var cita in dictamen.PoliticasCitadas ?? new List<CitaPolitica>())
        {
            if (string.IsNullOrWhiteSpace(cita.IdPolitica) || string.IsNullOrWhiteSpace(cita.TextoLiteral))
            {
                errores.Add("cada CitaPolitica requiere id_politica y texto_literal no vacios");
            }
        }

        return new ResultadoValidacion(errores.Count == 0, errores);
    }
}