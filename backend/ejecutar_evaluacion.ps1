# Script de evaluacion del agente de originacion crediticia (punto 5.3.6)
# Requiere que el backend este corriendo en http://localhost:5251

$backend = "http://localhost:5251"

Write-Host "=== Paso 1: seleccionando los 10 casos de evaluacion ===" -ForegroundColor Cyan
$seleccion = Invoke-RestMethod -Uri "$backend/api/Evaluacion/seleccionar-casos" -Method Post
Write-Host "Casos seleccionados: $($seleccion.total)" -ForegroundColor Green
$seleccion.casos | ForEach-Object {
    Write-Host "  - $($_.idCaso): $($_.tipoCaso) -> se espera $($_.decisionEsperada)"
}

Write-Host ""
Write-Host "=== Paso 2: ejecutando los 10 casos contra el agente real ===" -ForegroundColor Cyan
Write-Host "(esto puede tardar varios minutos, cada caso hace varias llamadas al modelo)" -ForegroundColor Yellow

$resultado = Invoke-RestMethod -Uri "$backend/api/Evaluacion/ejecutar" -Method Post

Write-Host ""
Write-Host "=== REPORTE FINAL ===" -ForegroundColor Cyan
Write-Host "Total de casos: $($resultado.total)"
Write-Host "Pasaron: $($resultado.pasaron) / $($resultado.total)" -ForegroundColor $(if ($resultado.pasaron -ge 7) { "Green" } else { "Yellow" })
Write-Host ""

foreach ($r in $resultado.resultados) {
    $estado = if ($r.paso) { "PASO" } else { "FALLO" }
    $color = if ($r.paso) { "Green" } else { "Red" }
    Write-Host "[$estado] $($r.idCaso)" -ForegroundColor $color
    Write-Host "  Esperado: $($r.decisionEsperada) | Obtenido: $($r.decisionObtenida)"
    Write-Host "  Criterio: $($r.criterioUsado)"
    if ($r.detalle) {
        Write-Host "  Detalle: $($r.detalle)" -ForegroundColor Yellow
    }
    Write-Host ""
}

# Guarda el reporte completo en un archivo JSON con marca de tiempo
$fechaHora = Get-Date -Format "yyyyMMdd-HHmmss"
$rutaReporte = "reporte-evaluacion-$fechaHora.json"
$resultado | ConvertTo-Json -Depth 10 | Out-File -FilePath $rutaReporte -Encoding utf8
Write-Host "Reporte completo guardado en: $rutaReporte" -ForegroundColor Cyan