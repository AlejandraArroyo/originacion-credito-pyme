import { useState } from "react";
import VistaChat from "./VistaChat";
import VistaMetricas from "./VistaMetricas";

type Pestana = "chat" | "metricas";

function App() {
  const [pestana, setPestana] = useState<Pestana>("chat");

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100vh" }}>
      <nav style={{ display: "flex", gap: "0.5rem", padding: "0.75rem 1rem", borderBottom: "1px solid #ccc" }}>
        <button
          onClick={() => setPestana("chat")}
          style={{
            padding: "0.4rem 1rem",
            fontWeight: pestana === "chat" ? "bold" : "normal",
            background: pestana === "chat" ? "#dbeafe" : "transparent",
            border: "1px solid #ccc",
            borderRadius: 6,
          }}
        >
          Chat
        </button>
        <button
          onClick={() => setPestana("metricas")}
          style={{
            padding: "0.4rem 1rem",
            fontWeight: pestana === "metricas" ? "bold" : "normal",
            background: pestana === "metricas" ? "#dbeafe" : "transparent",
            border: "1px solid #ccc",
            borderRadius: 6,
          }}
        >
          Métricas
        </button>
      </nav>

      <div style={{ flex: 1, overflow: "hidden" }}>
        <div style={{ display: pestana === "chat" ? "block" : "none", height: "100%" }}>
          <VistaChat />
        </div>
        <div style={{ display: pestana === "metricas" ? "block" : "none", height: "100%" }}>
          <VistaMetricas />
        </div>
      </div>
    </div>
  );
}

export default App;