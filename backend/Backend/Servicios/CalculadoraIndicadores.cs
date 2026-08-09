using System;

namespace Backend.Servicios;

public record SolicitudParaIndicadores(
    decimal MontoSolicitado,
    int PlazoMeses,
    int MesesOperacion,
    decimal? VentasAnuales,
    decimal? UtilidadNeta,
    decimal? ActivosTotales,
    decimal? PasivosTotales,
    decimal? DeudaVigenteAnual
);

public record Indicadores(
    decimal? RazonEndeudamiento,
    decimal? MargenNeto,
    decimal? CoberturaServicioDeuda,
    decimal RelacionMontoVentas,
    int AntiguedadMeses,
    decimal CuotaAnualEstimada,
    bool DatosIncompletos
);

public static class CalculadoraIndicadores
{
    private const decimal TasaAnualReferencia = 0.18m;

    public static Indicadores Calcular(SolicitudParaIndicadores s)
    {
        var datosIncompletos =
            s.VentasAnuales is null ||
            s.UtilidadNeta is null ||
            s.ActivosTotales is null ||
            s.PasivosTotales is null ||
            s.DeudaVigenteAnual is null;

        var cuotaAnual = CalcularCuotaAnual(s.MontoSolicitado, s.PlazoMeses, TasaAnualReferencia);

        decimal? razonEndeudamiento = null;
        if (s.PasivosTotales is decimal pasivos && s.ActivosTotales is decimal activos && activos != 0m)
        {
            razonEndeudamiento = Math.Round(pasivos / activos, 4, MidpointRounding.AwayFromZero);
        }

        decimal? margenNeto = null;
        if (s.UtilidadNeta is decimal utilidad && s.VentasAnuales is decimal ventas && ventas != 0m)
        {
            margenNeto = Math.Round(utilidad / ventas, 4, MidpointRounding.AwayFromZero);
        }

        decimal? coberturaServicioDeuda = null;
        if (s.UtilidadNeta is decimal utilidad2 && s.DeudaVigenteAnual is decimal deuda)
        {
            var denominador = cuotaAnual + deuda;
            if (denominador != 0m)
            {
                coberturaServicioDeuda = Math.Round(utilidad2 / denominador, 4, MidpointRounding.AwayFromZero);
            }
        }

        decimal relacionMontoVentas = 0m;
        if (s.VentasAnuales is decimal ventas2 && ventas2 != 0m)
        {
            relacionMontoVentas = Math.Round(s.MontoSolicitado / ventas2, 4, MidpointRounding.AwayFromZero);
        }

        return new Indicadores(
            razonEndeudamiento,
            margenNeto,
            coberturaServicioDeuda,
            relacionMontoVentas,
            s.MesesOperacion,
            Math.Round(cuotaAnual, 2, MidpointRounding.AwayFromZero),
            datosIncompletos
        );
    }

    private static decimal CalcularCuotaAnual(decimal monto, int plazoMeses, decimal tasaAnual)
    {
        if (plazoMeses <= 0)
        {
            return 0m;
        }

        var tasaMensual = tasaAnual / 12m;

        if (tasaMensual == 0m)
        {
            return Math.Round((monto / plazoMeses) * 12m, 2, MidpointRounding.AwayFromZero);
        }

        var factorElevado = PotenciaDecimal(1m + tasaMensual, plazoMeses);

        var cuotaMensual = monto * tasaMensual * factorElevado / (factorElevado - 1m);

        return Math.Round(cuotaMensual * 12m, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal PotenciaDecimal(decimal baseValor, int exponente)
    {
        var resultado = 1m;
        for (var i = 0; i < exponente; i++)
        {
            resultado *= baseValor;
        }
        return resultado;
    }
}