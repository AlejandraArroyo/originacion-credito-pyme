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
    <div style={{ padding: "1.5rem", maxWidth: 700 }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <h2>Vista de métricas de cartera</h2>
        <button onClick={cargar} style={{ padding: "0.4rem 0.8rem" }}>Actualizar</button>
      </div>

      {cargando && <p>Cargando...</p>}
      {error && <p style={{ color: "red" }}>Error: {error}</p>}

      {metricas && (
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: "1rem", marginTop: "1rem" }}>
          <div style={{ padding: "1rem", background: "#f3f4f6", borderRadius: 8 }}>
            <div style={{ fontSize: "0.85rem", color: "#666" }}>Total de dictámenes</div>
            <div style={{ fontSize: "1.8rem", fontWeight: "bold" }}>{metricas.totalDictamenes}</div>
          </div>
          <div style={{ padding: "1rem", background: "#f3f4f6", borderRadius: 8 }}>
            <div style={{ fontSize: "0.85rem", color: "#666" }}>Monto promedio recomendado</div>
            <div style={{ fontSize: "1.8rem", fontWeight: "bold" }}>
              Q{metricas.montoPromedioRecomendado.toLocaleString()}
            </div>
          </div>
          <div style={{ padding: "1rem", background: "#f3f4f6", borderRadius: 8 }}>
            <div style={{ fontSize: "0.85rem", color: "#666" }}>Tasa de escalamiento</div>
            <div style={{ fontSize: "1.8rem", fontWeight: "bold" }}>
              {(metricas.tasaEscalamiento * 100).toFixed(2)}%
            </div>
          </div>

          <div style={{ gridColumn: "1 / -1", marginTop: "0.5rem" }}>
            <h3>Solicitudes por estado</h3>
            <table style={{ width: "100%", borderCollapse: "collapse" }}>
              <tbody>
                {Object.entries(metricas.solicitudesPorEstado).map(([estado, cantidad]) => (
                  <tr key={estado} style={{ borderBottom: "1px solid #e5e7eb" }}>
                    <td style={{ padding: "0.4rem 0" }}>{estado}</td>
                    <td style={{ padding: "0.4rem 0", textAlign: "right", fontWeight: "bold" }}>{cantidad}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}

export default VistaMetricas;