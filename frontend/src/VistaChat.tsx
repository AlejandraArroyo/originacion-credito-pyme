import { useRef, useState } from "react";

const BACKEND_URL = "http://localhost:5251";

type Mensaje = { rol: "usuario" | "asistente"; texto: string };

type CitaPolitica = { idPolitica: string; seccion: string; textoLiteral: string };

type Dictamen = {
  idSolicitud?: string;
  decision?: string;
  montoRecomendado?: number | null;
  plazoRecomendadoMeses?: number | null;
  indicadores?: Record<string, unknown>;
  politicasCitadas?: CitaPolitica[];
  motivos?: string[];
  nivelRiesgo?: string;
  requiereAutorizacionHumana?: boolean;
  confianza?: number;
};

type DictamenRegistrado = {
  exitoso: boolean;
  idDictamen: string | null;
  estado: string | null;
  errores: string[];
};

function VistaChat() {
  const [mensajes, setMensajes] = useState<Mensaje[]>([]);
  const [entrada, setEntrada] = useState("");
  const [generando, setGenerando] = useState(false);
  const [dictamen, setDictamen] = useState<Dictamen | null>(null);
  const [registro, setRegistro] = useState<DictamenRegistrado | null>(null);
  const [herramientaActual, setHerramientaActual] = useState<string | null>(null);
  const [confirmado, setConfirmado] = useState(false);
  const abortRef = useRef<AbortController | null>(null);

  async function enviarMensaje() {
    if (!entrada.trim() || generando) return;

    const mensajeUsuario = entrada;
    setMensajes((prev) => [...prev, { rol: "usuario", texto: mensajeUsuario }]);
    setEntrada("");
    setGenerando(true);
    setDictamen(null);
    setRegistro(null);
    setConfirmado(false);
    setHerramientaActual(null);

    const controller = new AbortController();
    abortRef.current = controller;

    let textoAsistente = "";
    setMensajes((prev) => [...prev, { rol: "asistente", texto: "" }]);

    try {
      const url = `${BACKEND_URL}/api/AgenteOriginacion/consultar-stream?mensaje=${encodeURIComponent(mensajeUsuario)}`;
      const respuesta = await fetch(url, { signal: controller.signal });

      if (!respuesta.body) throw new Error("Sin cuerpo de respuesta");

      const reader = respuesta.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lineas = buffer.split("\n\n");
        buffer = lineas.pop() ?? "";

        for (const linea of lineas) {
          if (!linea.startsWith("data: ")) continue;
          const json = linea.slice(6);
          const evento = JSON.parse(json);

          if (evento.tipo === "texto") {
            textoAsistente += evento.contenido;
            setMensajes((prev) => {
              const copia = [...prev];
              copia[copia.length - 1] = { rol: "asistente", texto: textoAsistente };
              return copia;
            });
          } else if (evento.tipo === "herramienta_llamada") {
            setHerramientaActual(evento.herramienta);
          } else if (evento.tipo === "dictamen") {
            setDictamen(evento.contenido);
          } else if (evento.tipo === "dictamen_registrado") {
            setRegistro(evento);
          } else if (evento.tipo === "error") {
            setMensajes((prev) => [...prev, { rol: "asistente", texto: `Error: ${evento.mensaje}` }]);
          }
        }
      }
    } catch (err) {
      if ((err as Error).name !== "AbortError") {
        setMensajes((prev) => [...prev, { rol: "asistente", texto: `Error de conexion: ${err}` }]);
      }
    } finally {
      setGenerando(false);
      setHerramientaActual(null);
    }
  }

  function cancelar() {
    abortRef.current?.abort();
    setGenerando(false);
    setHerramientaActual(null);
  }

  async function confirmarDictamen() {
    if (!registro?.idDictamen) return;
    const res = await fetch(`${BACKEND_URL}/api/Dictamenes/${registro.idDictamen}/confirmar?confirmadoPor=analista`, {
      method: "POST",
    });
    if (res.ok) {
      setConfirmado(true);
    } else {
      const data = await res.json();
      alert(`No se pudo confirmar: ${data.errores?.join(", ") ?? "error desconocido"}`);
    }
  }

  return (
    <div style={{ display: "flex", height: "100%", fontFamily: "sans-serif" }}>
      <div style={{ flex: 1, display: "flex", flexDirection: "column", padding: "1rem", borderRight: "1px solid #ccc" }}>
        <h2>Asistente de Originación</h2>
        <div style={{ flex: 1, overflowY: "auto", marginBottom: "1rem" }}>
          {mensajes.map((m, i) => (
            <div key={i} style={{ marginBottom: "0.75rem", textAlign: m.rol === "usuario" ? "right" : "left" }}>
              <div
                style={{
                  display: "inline-block",
                  padding: "0.5rem 0.75rem",
                  borderRadius: 8,
                  background: m.rol === "usuario" ? "#dbeafe" : "#f3f4f6",
                  maxWidth: "80%",
                  whiteSpace: "pre-wrap",
                }}
              >
                {m.texto || (generando && i === mensajes.length - 1 ? "..." : "")}
              </div>
            </div>
          ))}
          {herramientaActual && (
            <div style={{ fontSize: "0.85rem", color: "#666" }}>
              Ejecutando herramienta: <code>{herramientaActual}</code>...
            </div>
          )}
        </div>
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <input
            value={entrada}
            onChange={(e) => setEntrada(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && enviarMensaje()}
            placeholder="Analiza la solicitud <uuid>..."
            style={{ flex: 1, padding: "0.5rem" }}
            disabled={generando}
          />
          {generando ? (
            <button onClick={cancelar} style={{ padding: "0.5rem 1rem" }}>Cancelar</button>
          ) : (
            <button onClick={enviarMensaje} style={{ padding: "0.5rem 1rem" }}>Enviar</button>
          )}
        </div>
      </div>

      <div style={{ width: 420, padding: "1rem", overflowY: "auto" }}>
        <h2>Panel de Dictamen</h2>
        {!dictamen && <p style={{ color: "#888" }}>Esperando análisis...</p>}
        {dictamen && (
          <div>
            <p><strong>Decisión:</strong> {dictamen.decision}</p>
            <p><strong>Monto recomendado:</strong> {dictamen.montoRecomendado ? `Q${dictamen.montoRecomendado}` : "N/A"}</p>
            <p><strong>Plazo:</strong> {dictamen.plazoRecomendadoMeses ?? "N/A"} meses</p>
            <p><strong>Nivel de riesgo:</strong> {dictamen.nivelRiesgo}</p>
            <p><strong>Confianza:</strong> {dictamen.confianza}</p>
            <p><strong>Requiere autorización humana:</strong> {dictamen.requiereAutorizacionHumana ? "Sí" : "No"}</p>

            <h3>Políticas citadas</h3>
            <ul>
              {dictamen.politicasCitadas?.map((c, i) => (
                <li key={i}>
                  <strong>{c.idPolitica}</strong> ({c.seccion})<br />
                  <span style={{ fontSize: "0.85rem" }}>{c.textoLiteral}</span>
                </li>
              ))}
            </ul>

            <h3>Motivos</h3>
            <ul>
              {dictamen.motivos?.map((m, i) => <li key={i}>{m}</li>)}
            </ul>

            {registro && (
              <div style={{ marginTop: "1rem", padding: "0.75rem", background: "#f9fafb", borderRadius: 8 }}>
                <p><strong>Estado del registro:</strong> {registro.estado}</p>
                {registro.errores.length > 0 && (
                  <p style={{ color: "red" }}>Errores: {registro.errores.join(", ")}</p>
                )}
                {registro.exitoso && !confirmado && (
                  <button onClick={confirmarDictamen} style={{ padding: "0.5rem 1rem", marginTop: "0.5rem" }}>
                    Confirmar dictamen
                  </button>
                )}
                {confirmado && <p style={{ color: "green" }}>✓ Dictamen confirmado</p>}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

export default VistaChat;