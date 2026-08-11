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

  function limpiarChat() {
    if (generando) return;
    setMensajes([]);
    setDictamen(null);
    setRegistro(null);
    setConfirmado(false);
    setHerramientaActual(null);
    setEntrada("");
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

  function traducirError(mensaje: string): string {
    if (mensaje.includes("politicas_citadas debe tener al menos 1 elemento")) {
      return "El agente no citó ninguna política que respalde esta decisión. Pídele de nuevo el análisis, indicando explícitamente que cite la política exacta que aplica.";
    }
    if (mensaje.includes("G2")) {
      return "Los indicadores del dictamen no coinciden con el cálculo más reciente del sistema. Vuelve a pedir el análisis para recalcular con datos actualizados.";
    }
    if (mensaje.includes("G3") || mensaje.includes("Rechazado por base de datos")) {
      return "El monto recomendado no cumple con los topes de política vigentes. Revisa el monto solicitado contra el límite permitido.";
    }
    if (mensaje.toLowerCase().includes("decision fuera del enum") || mensaje.toLowerCase().includes("nivel_riesgo fuera del enum")) {
      return "El agente devolvió un valor de decisión o riesgo no reconocido por el sistema. Vuelve a intentar el análisis.";
    }
    return mensaje;
  }

  return (
    <div className="chat-shell">
      <div className="chat-col">
        <div className="chat-col-header">
          <h2>Asistente de Originación</h2>
          <button className="btn" onClick={limpiarChat} disabled={generando}>
            Nuevo análisis
          </button>
        </div>

        <div className="messages">
          {mensajes.map((m, i) => (
            <div key={i} className={`msg-row ${m.rol}`}>
              <div className="msg-bubble">
                {m.texto || (generando && i === mensajes.length - 1 ? "..." : "")}
              </div>
            </div>
          ))}
          {herramientaActual && (
            <div className="tool-indicator">
              <span className="dot" />
              Ejecutando <code>{herramientaActual}</code>
            </div>
          )}
        </div>

        <div className="chat-input-row">
          <input
            value={entrada}
            onChange={(e) => setEntrada(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && enviarMensaje()}
            placeholder="Analiza la solicitud <uuid>..."
            disabled={generando}
          />
          {generando ? (
            <button className="btn btn-danger" onClick={cancelar}>Cancelar</button>
          ) : (
            <button className="btn btn-primary" onClick={enviarMensaje}>Enviar</button>
          )}
        </div>
      </div>

      <div className="dictamen-col">
        <h2>Panel de Dictamen</h2>
        {!dictamen && <p className="dictamen-empty">Esperando análisis...</p>}
        {dictamen && (
          <div>
            {dictamen.decision && (
              <div className={`decision-pill ${dictamen.decision.toLowerCase()}`}>
                {dictamen.decision.replace(/_/g, " ")}
              </div>
            )}

            <div className="dictamen-grid">
              <div>
                <div className="field-label">Monto recomendado</div>
                <div className="field-value">
                  {dictamen.montoRecomendado ? `Q${dictamen.montoRecomendado.toLocaleString()}` : "N/A"}
                </div>
              </div>
              <div>
                <div className="field-label">Plazo</div>
                <div className="field-value">{dictamen.plazoRecomendadoMeses ?? "N/A"} meses</div>
              </div>
              <div>
                <div className="field-label">Nivel de riesgo</div>
                <div className="field-value">{dictamen.nivelRiesgo}</div>
              </div>
              <div>
                <div className="field-label">Confianza</div>
                <div className="field-value">{dictamen.confianza}</div>
              </div>
              <div style={{ gridColumn: "1 / -1" }}>
                <div className="field-label">Requiere autorización humana</div>
                <div className="field-value">{dictamen.requiereAutorizacionHumana ? "Sí" : "No"}</div>
              </div>
            </div>

            <div className="dictamen-section-title">Políticas citadas</div>
            {dictamen.politicasCitadas?.map((c, i) => (
              <div key={i} className="cita-politica">
                <span className="cita-tag">{c.idPolitica}</span>
                <span className="cita-seccion">{c.seccion}</span>
                <blockquote>{c.textoLiteral}</blockquote>
              </div>
            ))}

            <div className="dictamen-section-title">Motivos</div>
            <ul className="motivos-list">
              {dictamen.motivos?.map((m, i) => <li key={i}>{m}</li>)}
            </ul>

            {registro && registro.exitoso && (
              <div className="registro-box">
                <div className="estado-label">Estado del registro</div>
                <div className="estado-value">{registro.estado}</div>
                {registro.exitoso && !confirmado && (
                  <button className="btn btn-primary" onClick={confirmarDictamen}>
                    Confirmar dictamen
                  </button>
                )}
                {confirmado && <p className="confirmado-check">✓ Dictamen confirmado</p>}
              </div>
            )}

            {registro && !registro.exitoso && (
              <div className="alerta-fallo">
                <div className="alerta-fallo-titulo">
                  ⚠ El dictamen no se registró
                </div>
                <ul className="alerta-fallo-lista">
                  {registro.errores.map((e, i) => (
                    <li key={i}>{traducirError(e)}</li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

export default VistaChat;