using System;

namespace Backend.Modelos;

public record Solicitud(
    Guid IdSolicitud,
    string NombreEmpresa,
    string Sector,
    int MesesOperacion,
    decimal MontoSolicitado,
    int PlazoMeses,
    string DestinoFondos,
    decimal? VentasAnuales,
    decimal? UtilidadNeta,
    decimal? ActivosTotales,
    decimal? PasivosTotales,
    decimal? DeudaVigenteAnual,
    int ScoreHistorial,
    string GarantiaOfrecida,
    DateOnly FechaSolicitud
);