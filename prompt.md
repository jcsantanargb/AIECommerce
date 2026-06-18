## Contexto

Se debe construir el backend de un e-commerce para clientes mexicanos que pueda:

- Registrar usuarios
- Registrar tarjetas de crédito como métodos de pago
- Realizar compras en un market place
- Dar seguimiento a las órdenes de compra

El sistema NO ejecuta cargos reales a las tarjetas de crédito

## Alcance funcional

Registro de usuarios/clientes:
- Nombre completo
- CURP
- Fecha de nacimiento
- Domicilio:
    - Calle y numero
    - Colonia
    - C.P.
    - Municipio
    - Estado

Registro de métodos de pago:
- Número de tarjeta
- Tipo de tarjeta (VISA, Mastercard, AMEX)
- Nombre de tarjetahabiente
- Expiración
- CVV

Listado de productos:
- Nombre de producto
- Precio
- Características (en general)
- SKU (clave de producto)

Creación de órdenes de compra:
- Clave de cliente
- Fecha
- Total de compra (Precio total)
- Productos (listado de productos incluidos en la orden de compra)
- Código de autorización o rechazo (simulado)
- Status

Seguimiento de órdenes de compra:
- Clave de orden de compra
- Fecha
- Total de compra (Precio total)
- Status
Detalle de status
    - Motivos de rechazo
    - Tracking de entrega

Reglas de validación
Duplicidad de clientes
Estructura de la CURP válida
Mayoría de edad
Duplicidad de métodos de pago
Número de tarjeta
Correspondencia de Colonia vs C.P. vs Municipio vs Estado, se puede usar unicamente 1 estado para la dirección
Las órdenes de compra no pueden superar lso 5,000 pesos

Consideraciones técnicas
Los datos se pueden almacenar en JSON locales, SQLite, SQL Server
.Net 8 o 10
Los endpoints pueden funcionar localmente nada más, no es necesario hacer el deploy en la nube
Si se desea usar la nube, unicamente cuentas de prueba o dev
Sugerencia, hacer la colección de postman o el swagger para probar las llamadas de los endpoints
No es necesario hacer frontend
Se deben incluir pruebas unitarias
Realizar logs de todos los intentos de compra con datos relevantes

Entregables
Diagrama de arquitectura
Documentación
    - Pruebas
    - Códigos de error
    - Flujo de cada proceso (registros, consultas, etc)
    - Funcionalidad (que sí hace y que no hace)
    - Logs y troubleshooting
Código fuente
Modelos de datos y logs
Validación de cobertura de código
Demo funcional
