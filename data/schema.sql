-- =========================================================
-- Esquema: Asistente de originacion crediticia PyME
-- =========================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ---------------------------------------------------------
-- Politicas de credito (corpus versionado)
-- ---------------------------------------------------------
CREATE TABLE politicas (
    id_politica     TEXT PRIMARY KEY,        -- p.ej. 'POL-2.3'
    seccion         TEXT NOT NULL,
    categoria       TEXT NOT NULL,
    texto           TEXT NOT NULL,
    severidad       TEXT NOT NULL CHECK (severidad IN ('bloqueante', 'informativa')),
    version_corpus  TEXT NOT NULL DEFAULT '2.0'
);

-- Parametros de politica que cambian con el tiempo (evita hardcodear
-- el tope de Q500,000 en el codigo o en un trigger fijo)
CREATE TABLE parametros_politica (
    clave   TEXT PRIMARY KEY,
    valor   NUMERIC(14,2) NOT NULL,
    nota    TEXT
);
INSERT INTO parametros_politica (clave, valor, nota) VALUES
    ('tope_maximo_monto', 500000.00, 'POL-4.1: tope absoluto de monto aprobado'),
    ('umbral_autorizacion_comite', 250000.00, 'POL-6.2: monto que exige autorizacion de comite');

-- ---------------------------------------------------------
-- Solicitudes de credito
-- ---------------------------------------------------------
CREATE TABLE solicitudes (
    id_solicitud            UUID PRIMARY KEY,
    nombre_empresa          TEXT NOT NULL,
    sector                  TEXT NOT NULL CHECK (sector IN
                             ('comercio','manufactura','servicios','agropecuario','transporte','construccion','otros')),
    meses_operacion         INTEGER NOT NULL CHECK (meses_operacion >= 0),
    monto_solicitado        NUMERIC(14,2) NOT NULL CHECK (monto_solicitado > 0),
    plazo_meses             INTEGER NOT NULL CHECK (plazo_meses > 0),
    destino_fondos          TEXT NOT NULL,          -- entrada NO confiable (G5): nunca se interpreta como instruccion
    ventas_anuales          NUMERIC(14,2),           -- nullable: datos incompletos son un caso real (5.2.1)
    utilidad_neta           NUMERIC(14,2),
    activos_totales         NUMERIC(14,2),
    pasivos_totales         NUMERIC(14,2),
    deuda_vigente_anual     NUMERIC(14,2),
    score_historial         INTEGER CHECK (score_historial BETWEEN 0 AND 100),
    garantia_ofrecida       TEXT NOT NULL CHECK (garantia_ofrecida IN ('ninguna','fiduciaria','prendaria','hipotecaria')),
    fecha_solicitud         DATE NOT NULL,
    creado_en               TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- ---------------------------------------------------------
-- Indicadores precalculados (5.3.1) - cache/tabla derivada.
-- Se recalculan en codigo (nunca por el LLM) y se invalidan
-- via trigger cuando cambian los datos financieros de la solicitud.
-- Documenta en el README cual estrategia de invalidacion usaste
-- si migras esto a columna generada o vista materializada.
-- ---------------------------------------------------------
CREATE TABLE indicadores_solicitud (
    id_solicitud                UUID PRIMARY KEY REFERENCES solicitudes(id_solicitud) ON DELETE CASCADE,
    razon_endeudamiento         NUMERIC(10,4),   -- pasivos/activos
    margen_neto                 NUMERIC(10,4),   -- utilidad/ventas
    cobertura_servicio_deuda    NUMERIC(10,4),
    relacion_monto_ventas       NUMERIC(10,4),
    antiguedad_meses            INTEGER,
    datos_incompletos           BOOLEAN NOT NULL DEFAULT FALSE, -- true si algun insumo requerido es NULL
    calculado_en                TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Marca de "sucio" para que el backend sepa que debe recalcular.
-- (Alternativa mas simple que una vista materializada si el volumen es bajo;
--  documentar en README por que se eligio este enfoque.)
ALTER TABLE solicitudes ADD COLUMN indicadores_vigentes BOOLEAN NOT NULL DEFAULT FALSE;

CREATE OR REPLACE FUNCTION invalidar_indicadores() RETURNS TRIGGER AS $$
BEGIN
    NEW.indicadores_vigentes := FALSE;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_invalidar_indicadores
    BEFORE UPDATE OF ventas_anuales, utilidad_neta, activos_totales, pasivos_totales, deuda_vigente_anual, monto_solicitado, plazo_meses
    ON solicitudes
    FOR EACH ROW
    EXECUTE FUNCTION invalidar_indicadores();

-- ---------------------------------------------------------
-- Dictamenes (salida estructurada del agente, 5.3.4)
-- ---------------------------------------------------------
CREATE TABLE dictamenes (
    id_dictamen                     UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    id_solicitud                    UUID NOT NULL REFERENCES solicitudes(id_solicitud),
    decision                        TEXT NOT NULL CHECK (decision IN ('APROBADO','RECHAZADO','ESCALADO_A_COMITE')),
    monto_recomendado               NUMERIC(14,2),
    plazo_recomendado_meses         INTEGER,
    indicadores                     JSONB NOT NULL,   -- copia inmutable de indicadores_solicitud al momento del dictamen (para G2 y auditoria)
    motivos                         JSONB NOT NULL DEFAULT '[]',
    nivel_riesgo                    TEXT NOT NULL CHECK (nivel_riesgo IN ('BAJO','MEDIO','ALTO')),
    requiere_autorizacion_humana    BOOLEAN NOT NULL DEFAULT FALSE,
    confianza                       NUMERIC(3,2) CHECK (confianza BETWEEN 0 AND 1),
    estado                          TEXT NOT NULL DEFAULT 'BORRADOR'
                                     CHECK (estado IN ('BORRADOR','PENDIENTE_AUTORIZACION','CONFIRMADO','RECHAZADO_POR_ANALISTA')),
    clave_idempotencia              TEXT NOT NULL UNIQUE,   -- generada por el backend, NUNCA por el modelo (ver 1.2 del cuestionario)
    es_historico                    BOOLEAN NOT NULL DEFAULT FALSE, -- true = dato semilla para vista de metricas
    id_sesion_agente                UUID,
    creado_en                       TIMESTAMPTZ NOT NULL DEFAULT now(),
    confirmado_en                   TIMESTAMPTZ,
    confirmado_por                  TEXT
);

CREATE INDEX idx_dictamenes_estado ON dictamenes(estado);
CREATE INDEX idx_dictamenes_solicitud ON dictamenes(id_solicitud);

-- Citas de politica que sustentan un dictamen (G1)
CREATE TABLE citas_politica (
    id              SERIAL PRIMARY KEY,
    id_dictamen     UUID NOT NULL REFERENCES dictamenes(id_dictamen) ON DELETE CASCADE,
    id_politica     TEXT NOT NULL REFERENCES politicas(id_politica),
    seccion         TEXT NOT NULL,
    texto_literal   TEXT NOT NULL
);

-- G1 en la base de datos: el texto_literal citado debe existir realmente
-- para esa id_politica en el corpus. Si no coincide, se rechaza el insert
-- y la aplicacion debe forzar la decision a ESCALADO_A_COMITE.
CREATE OR REPLACE FUNCTION verificar_cita_g1() RETURNS TRIGGER AS $$
DECLARE
    texto_real TEXT;
BEGIN
    SELECT texto INTO texto_real FROM politicas WHERE id_politica = NEW.id_politica;
    IF texto_real IS NULL THEN
        RAISE EXCEPTION 'G1: id_politica % no existe en el corpus', NEW.id_politica;
    END IF;
    IF btrim(texto_real) <> btrim(NEW.texto_literal) THEN
        RAISE EXCEPTION 'G1: texto_literal no coincide exactamente con el corpus para %', NEW.id_politica;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_verificar_cita_g1
    BEFORE INSERT ON citas_politica
    FOR EACH ROW
    EXECUTE FUNCTION verificar_cita_g1();

-- G3 en la base de datos: monto_recomendado nunca puede superar el monto
-- solicitado ni el tope de politica vigente. Constraint a nivel de DB,
-- no solo validacion de aplicacion.
CREATE OR REPLACE FUNCTION verificar_tope_g3() RETURNS TRIGGER AS $$
DECLARE
    monto_pedido NUMERIC(14,2);
    tope NUMERIC(14,2);
BEGIN
    IF NEW.monto_recomendado IS NULL THEN
        RETURN NEW; -- RECHAZADO o ESCALADO puede no tener monto
    END IF;

    SELECT monto_solicitado INTO monto_pedido FROM solicitudes WHERE id_solicitud = NEW.id_solicitud;
    SELECT valor INTO tope FROM parametros_politica WHERE clave = 'tope_maximo_monto';

    IF NEW.monto_recomendado > monto_pedido THEN
        RAISE EXCEPTION 'G3: monto_recomendado (%) supera el monto solicitado (%)', NEW.monto_recomendado, monto_pedido;
    END IF;
    IF NEW.monto_recomendado > tope THEN
        RAISE EXCEPTION 'G3: monto_recomendado (%) supera el tope de politica vigente (%)', NEW.monto_recomendado, tope;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_verificar_tope_g3
    BEFORE INSERT OR UPDATE ON dictamenes
    FOR EACH ROW
    EXECUTE FUNCTION verificar_tope_g3();

-- ---------------------------------------------------------
-- Observabilidad (5.3.7)
-- ---------------------------------------------------------
CREATE TABLE ejecuciones_agente (
    id_ejecucion        UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    id_sesion           UUID NOT NULL,
    id_solicitud        UUID REFERENCES solicitudes(id_solicitud),
    version_prompt      TEXT NOT NULL,
    modelo              TEXT NOT NULL,
    secuencia_herramientas JSONB NOT NULL DEFAULT '[]', -- [{herramienta, argumentos, resultado, timestamp}]
    tokens_entrada       INTEGER,
    tokens_salida        INTEGER,
    latencia_ms          INTEGER,
    costo_estimado_usd   NUMERIC(10,6),
    creado_en            TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_ejecuciones_sesion ON ejecuciones_agente(id_sesion);

-- ---------------------------------------------------------
-- Casos de evaluacion (5.3.6) - resultado esperado vs obtenido
-- ---------------------------------------------------------
CREATE TABLE casos_evaluacion (
    id_caso             TEXT PRIMARY KEY,   -- p.ej. 'EVAL-01-aprobacion-clara'
    id_solicitud         UUID NOT NULL REFERENCES solicitudes(id_solicitud),
    tipo_caso            TEXT NOT NULL CHECK (tipo_caso IN
                          ('aprobacion','rechazo','escalamiento_monto','escalamiento_sin_politica',
                           'adversarial_inyeccion','adversarial_datos_inconsistentes')),
    decision_esperada    TEXT NOT NULL CHECK (decision_esperada IN ('APROBADO','RECHAZADO','ESCALADO_A_COMITE')),
    politica_esperada    TEXT REFERENCES politicas(id_politica),
    notas                TEXT
);
