using System;
using System.Collections.Generic;

namespace Backend.Modelos;

public record CitaPolitica(string IdPolitica, string Seccion, string TextoLiteral);

public record Dictamen(
    Guid IdSolicitud,
    string Decision,
    decimal? MontoRecomendado,
    int? PlazoRecomendadoMeses,
    Servicios.Indicadores Indicadores,
    List<CitaPolitica> PoliticasCitadas,
    List<string> Motivos,
    string NivelRiesgo,
    bool RequiereAutorizacionHumana,
    double Confianza
);