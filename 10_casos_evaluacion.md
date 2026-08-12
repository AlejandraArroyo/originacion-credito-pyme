# Los 10 casos de evaluación

## Cómo elegí los casos

No los elegí a mano. Un criterio en SQL selecciona automáticamente, de mis 200 solicitudes, los 10
casos con la distribución que pide la prueba: 3 de aprobación clara, 3 de rechazo cada uno por un
motivo de política distinto, 2 de escalamiento, y 2 adversariales.

Uno de los casos (rechazo por endeudamiento) lo armé a propósito con datos diseñados, porque mi
generador de solicitudes nunca producía ese perfil específico por su propio rango de valores.

## Cómo decido si un caso pasa

Para aprobación y rechazo con política esperada, exijo decisión correcta y la cita exacta de esa
política. Para los dos casos de escalamiento, solo exijo la decisión correcta. Para el caso de
inyección de prompt, no puedo exigir una decisión fija porque depende del perfil real de esa
solicitud; ahí reviso que el agente no haya seguido la instrucción inyectada. Para datos
inconsistentes, espero que escale en vez de decidir automáticamente con información contradictoria.

## Qué obtuve, y por qué

Corrí la evaluación varias veces durante el desarrollo. Los resultados variaron entre 3 y 7 de 10
según la corrida, con el mismo prompt exacto — evidencia real de que el modelo no responde siempre
igual ante el mismo caso, no algo que oculté.

El ajuste que más ayudó fue mover la lógica de "monto alto necesita autorización" del prompt al
código del backend, en vez de pedirle al modelo que la maneje él mismo. Antes de eso, confundía
"esto necesitará autorización después" con "debo escalar mi decisión ahora".

En la corrida que quedó como definitiva, pasaron 6 de 10. Los 4 que fallan comparten un patrón: el
modelo no siempre cita la política exacta que esperaba aunque la decisión sea correcta, y en los
casos de escalamiento a veces prefiere una respuesta decisiva (aprobar o rechazar) en vez de admitir
que el caso necesita revisión de comité.

Decidí no seguir ajustando el prompt de forma indefinida, porque ya viví el efecto contrario: un
cambio que arreglaba un caso rompía otro que antes funcionaba. Preferí un resultado real y
documentado en vez de perseguir un número perfecto.
