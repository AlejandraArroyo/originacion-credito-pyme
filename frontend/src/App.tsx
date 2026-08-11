import { useState } from "react";
import VistaChat from "./VistaChat";
import VistaMetricas from "./VistaMetricas";
import "./tokens.css";

type Pestana = "chat" | "metricas";

function App() {
  const [pestana, setPestana] = useState<Pestana>("chat");

  return (
    <div className="app-shell">
      <nav className="app-nav">
        <div className="app-brand">Origina<span>ción</span> PyME</div>
        <div className="app-tabs">
          <button
            className={`app-tab ${pestana === "chat" ? "active" : ""}`}
            onClick={() => setPestana("chat")}
          >
            Chat
          </button>
          <button
            className={`app-tab ${pestana === "metricas" ? "active" : ""}`}
            onClick={() => setPestana("metricas")}
          >
            Métricas
          </button>
        </div>
      </nav>
      <div className="franja-textil" />

      <div style={{ flex: 1, overflow: "hidden" }}>
        <div style={{ display: pestana === "chat" ? "block" : "none", height: "100%" }}>
          <VistaChat />
        </div>
        <div style={{ display: pestana === "metricas" ? "block" : "none", height: "100%", overflowY: "auto" }}>
          <VistaMetricas />
        </div>
      </div>
    </div>
  );
}

export default App;