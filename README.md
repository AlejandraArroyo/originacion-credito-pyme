# Asistente de Originación Crediticia PyME

Sistema que preanaliza solicitudes de crédito PyME, aplica las políticas vigentes y recomienda un
dictamen citando la política exacta que lo sustenta. No reemplaza al analista: cualquier decisión
queda pendiente de que un humano la confirme antes de quedar en firme.

## Stack

.NET 8 con Microsoft Agent Framework para el agente, conectado a OpenRouter. El modelo que uso es
`openai/gpt-4o-mini` — empecé con modelos gratuitos, pero los límites de tasa interrumpían las
pruebas constantemente, así que decidí usar mis propios créditos con un modelo económico (el costo
real de todo el desarrollo fue menos de un dólar). PostgreSQL en Docker. React + Vite en el frontend.
Uso Npgsql directo en vez de un ORM porque el proyecto no lo necesitaba y prefería control fino sobre
las consultas.

## Cómo levantarlo

Ver `COMO_EMPEZAR.md` para el detalle completo. En resumen: `docker compose up -d`, correr el
`SeedTool` para cargar los datos, y luego `dotnet run` en el backend y `npm run dev` en el frontend.

## Corpus de políticas: por qué elegí esta estrategia de acceso

Con solo 28 políticas, no le vi sentido a montar búsqueda vectorial con embeddings — es una
dependencia externa más, más costo y latencia, sin ningún beneficio real a ese volumen. Usé búsqueda
de texto completo nativa de PostgreSQL, en español, y en las pruebas funcionó bien: buscar
"endeudamiento" me trae tanto la política general como su excepción en el mismo resultado, que era
justo el caso difícil que quería resolver.

Si el corpus creciera a 500 políticas o cambiara cada semana, ahí sí migraría a embeddings, porque a
ese volumen las coincidencias de palabras exactas empiezan a fallar frente a búsquedas más
conceptuales. También le metería versionado real (ya tengo el campo listo en la tabla, pero hoy es
solo informativo) y filtraría por categoría antes de buscar, para no tener que rankear las 500 de
una vez.

## Cómo calculo los indicadores

Todo en `decimal` de C#, nunca `double` — incluso la fórmula de interés compuesto para la cuota del
crédito, que normalmente se haría con `Math.Pow`, la reescribí como un bucle de multiplicación en
`decimal` puro para no meter punto flotante binario en un cálculo financiero.

Los guardo precalculados en una tabla aparte, con una bandera que se invalida automáticamente (vía
trigger) cuando cambian los datos financieros de la solicitud. Preferí esto sobre una vista
materializada porque de todas formas hay que refrescarla con algo, así que no gano nada de
simplicidad; y una columna generada no sirve aquí porque la fórmula de cuota no es una expresión SQL
simple.

## Las 5 herramientas del agente

`obtener_solicitud`, `calcular_indicadores`, `buscar_politica`, `registrar_dictamen` y
`metricas_cartera`. Cada una vive en su propia clase, con responsabilidad clara — eso me ayudó mucho
después, cuando quise exponerlas también como servidor MCP sin duplicar nada.

## Salida estructurada y qué pasa cuando el modelo se equivoca

Antes de guardar cualquier dictamen, lo valido: decisión y riesgo dentro del enum permitido, al menos
una política citada, confianza entre 0 y 1. Tuve un caso real durante el desarrollo donde el modelo
mandó el campo de motivos como un texto suelto en vez de una lista — en vez de simplemente reintentar
a ciegas, hice que el sistema aceptara ambos formatos de forma explícita, documentando por qué eso no
es lo mismo que confiar ciegamente en lo que venga.

## Los guardarraíles

Cada uno se aplica en código, no solo pidiéndoselo al modelo:

- **G1** — cada cita se verifica contra el corpus real antes de guardar; si no coincide, se fuerza
  el escalamiento. Hay un trigger de respaldo en la base de datos por si la capa de aplicación
  fallara.
- **G2** — antes de persistir, recalculo los indicadores en ese momento y los comparo contra lo que
  trae el dictamen; si no coinciden, se rechaza todo.
- **G3** — el monto recomendado nunca puede pasar el tope de política, aplicado como restricción
  real de base de datos.
- **G4** — un monto alto o riesgo alto dejan el dictamen pendiente de autorización, pero solo si la
  decisión es aprobar (rechazar no compromete fondos, así que no necesita ese paso). Esto lo tuve que
  ajustar a mitad de camino después de aclarar la interpretación correcta de la política.
- **G5** — el campo de destino de fondos, escrito por el solicitante, se envuelve con marcadores
  explícitos antes de dárselo al modelo, para que lo trate como dato y no como instrucción. Lo probé
  con solicitudes que traen intentos reales de manipulación y el agente los detecta sin cambiar su
  decisión.

También agregué una verificación extra en código: ninguna aprobación puede persistirse si la
cobertura de servicio de deuda recalculada está por debajo del mínimo de política, sin importar lo
que el modelo haya concluido. La agregué después de encontrar, revisando dictámenes ya guardados, que
el modelo a veces aprobaba fijándose solo en uno o dos indicadores buenos y pasando por alto uno malo.

## Frontend

El chat usa `fetch` con `ReadableStream` en vez de `EventSource`, porque necesitaba que cancelar la
generación desde el navegador cortara también la ejecución real en el servidor, no solo la vista. El
panel de dictamen se llena en vivo conforme el agente construye su respuesta, y el botón de confirmar
solo aparece cuando el registro fue exitoso.

## Punto extra: servidor MCP

Expuse las mismas 5 herramientas también como servidor MCP, reutilizando la lógica que ya tenía — fue
agregar un par de atributos por método y unas líneas en el arranque del programa, nada nuevo que
construir desde cero. Lo elegí sobre las otras opciones del punto extra porque no dependía de hacer
más llamadas al modelo para tener evidencia de que funciona.
