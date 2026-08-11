import { useEffect, useState } from "react";

const BACKEND_URL = "http://localhost:5251";

type Metricas = {
  solicitudesPorEstado: Record<string, number>;
  montoPromedioRecomendado: number;
  tasaEscalamiento: number;
  totalDictamenes: number;
};

function VistaMetricas() {
  const [metricas, setMetricas] = useState<Metricas | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [cargando, setCargando] = useState(true);

  function cargar() {
    setCargando(true);
    setError(null);
    fetch(`${BACKEND_URL}/api/Dictamenes/metricas`)
      .then((res) => {
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return res.json();
      })
      .then((data) => {
        setMetricas(data);
        setCargando(false);
      })
      .catch((err) => {
        setError(String(err));
        setCargando(false);
      });
  }

  useEffect(cargar, []);

  return (
    <div className="metricas-wrap">
      <div className="metricas-header">
        <h2>Vista de métricas de cartera</h2>
        <button className="btn" onClick={cargar}>Actualizar</button>
      </div>

      {cargando && <p style={{ color: "var(--text-muted)", fontSize: "0.88rem" }}>Cargando...</p>}
      {error && <p style={{ color: "var(--riesgo-alto)", fontSize: "0.88rem" }}>Error: {error}</p>}

      {metricas && (
        <>
          <div className="stat-grid">
            <div className="stat-card">
              <div className="stat-label">Total de dictámenes</div>
              <div className="stat-value">{metricas.totalDictamenes}</div>
            </div>
            <div className="stat-card">
              <div className="stat-label">Monto promedio recomendado</div>
              <div className="stat-value">Q{metricas.montoPromedioRecomendado.toLocaleString()}</div>
            </div>
            <div className="stat-card">
              <div className="stat-label">Tasa de escalamiento</div>
              <div className="stat-value">{(metricas.tasaEscalamiento * 100).toFixed(2)}%</div>
            </div>
          </div>

          <div className="dictamen-section-title" style={{ borderTop: "none", paddingTop: 0, margin: "0 0 0.6rem 0" }}>
            Solicitudes por estado
          </div>
          <table className="estado-table">
            <tbody>
              {Object.entries(metricas.solicitudesPorEstado).map(([estado, cantidad]) => (
                <tr key={estado}>
                  <td>{estado}</td>
                  <td>{cantidad}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
}

export default VistaMetricas;