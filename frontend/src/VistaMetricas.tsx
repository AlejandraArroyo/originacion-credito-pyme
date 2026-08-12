import { useEffect, useState } from "react";

const BACKEND_URL = "http://localhost:5251";

type Metricas = {
  solicitudesPorEstado: Record<string, number>;
  montoPromedioRecomendado: number;
  tasaEscalamiento: number;
  totalDictamenes: number;
};

type DictamenRechazado = {
  idDictamen: string;
  idSolicitud: string;
  nombreEmpresa: string;
  motivos: string[];
  creadoEn: string;
};

function VistaMetricas() {
  const [metricas, setMetricas] = useState<Metricas | null>(null);
  const [rechazados, setRechazados] = useState<DictamenRechazado[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [cargando, setCargando] = useState(true);
  const [mostrarRechazados, setMostrarRechazados] = useState(false);

  function cargar() {
    setCargando(true);
    setError(null);

    Promise.all([
      fetch(`${BACKEND_URL}/api/Dictamenes/metricas`).then((r) => {
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        return r.json();
      }),
      fetch(`${BACKEND_URL}/api/Dictamenes/rechazados`).then((r) => {
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        return r.json();
      }),
    ])
      .then(([datosMetricas, datosRechazados]) => {
        setMetricas(datosMetricas);
        setRechazados(datosRechazados);
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

      {cargando && <p style={{ color: "var(--texto-mudo)", fontSize: "0.88rem" }}>Cargando...</p>}
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

      <div style={{ marginTop: "1.5rem" }}>
        <button
          className="btn"
          onClick={() => setMostrarRechazados(!mostrarRechazados)}
        >
          {mostrarRechazados ? "Ocultar" : "Ver"} bandeja de rechazados ({rechazados.length})
        </button>

        {mostrarRechazados && (
          <div style={{ marginTop: "1rem" }}>
            {rechazados.length === 0 && (
              <p style={{ color: "var(--texto-mudo)", fontSize: "0.85rem" }}>
                No hay rechazos registrados todavía.
              </p>
            )}
            {rechazados.map((r) => (
              <div key={r.idDictamen} className="cita-politica" style={{ marginBottom: "0.7rem" }}>
                <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "0.3rem" }}>
                  <strong style={{ fontFamily: "var(--font-display)", fontSize: "0.9rem" }}>
                    {r.nombreEmpresa}
                  </strong>
                  <span style={{ fontFamily: "var(--font-mono)", fontSize: "0.75rem", color: "var(--texto-mudo)" }}>
                    {new Date(r.creadoEn).toLocaleDateString()}
                  </span>
                </div>
                <ul className="motivos-list">
                  {r.motivos.map((m, i) => <li key={i}>{m}</li>)}
                </ul>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

export default VistaMetricas;