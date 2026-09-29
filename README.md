# MahoSoft Backend

Backend de MahoSoft desarrollado en .NET.

El frontend está en [MahoSoft](https://github.com/santiagovegamadera-droid/MahoSoft).

## Tecnología

- .NET 9 (ASP.NET Core Web API)
- Entity Framework Core 9 con PostgreSQL (Npgsql). En producción, la base de Supabase
- Imágenes de productos en Cloudinary; facturas de proveedores y comprobantes de transferencia en Supabase Storage (en local, en el disco)

## Arquitectura en capas

Cada capa es un proyecto y solo conoce a la de abajo:

```
MahoSoft.Api  →  MahoSoft.Negocio  →  MahoSoft.Datos  →  MahoSoft.Entidades
```

| Proyecto | Qué contiene |
| --- | --- |
| `src/MahoSoft.Api` | Presentación: controladores (delgados, sin lógica), autenticación JWT, `Program.cs`. |
| `src/MahoSoft.Negocio` | Servicios con las reglas del negocio, DTOs de entrada y salida, excepciones de negocio. Una carpeta por módulo (`Auth/`, `Categorias/`…). |
| `src/MahoSoft.Datos` | `AppDbContext`, configuraciones de EF, migraciones, datos de ejemplo y repositorios. |
| `src/MahoSoft.Entidades` | Una clase por tabla y los enums. La usan todas las capas. |

Reglas:

- La API no ve `MahoSoft.Datos` (`DisableTransitiveProjectReferences` en su `.csproj`): un controlador que intente usar el `AppDbContext` o un repositorio no compila. Los controladores reciben y devuelven DTOs, nunca entidades.
- Los servicios leen y escriben solo a través de los repositorios (`IXxxRepositorio`) y guardan con `IUnidadDeTrabajo.GuardarCambiosAsync()`, una vez por operación.
- Cuando una regla impide la operación, el servicio lanza una excepción de `Negocio/Excepciones.cs` (`ValidacionException` 400, `NoAutenticadoException` 401, `AccesoDenegadoException` 403, `NoEncontradoException` 404, `ConflictoException` 409). `NegocioExceptionFilter` la convierte en una respuesta ProblemDetails cuyo `detail` es el mensaje para el usuario.
- Cada capa registra lo suyo: `AddDatos` (repositorios) y `AddNegocio` (servicios, que a su vez llama a `AddDatos`). La API solo llama a `AddNegocio`.

Para agregar un módulo: repositorio en `Datos/Repositorios`, servicio y DTOs en `Negocio/<Modulo>/`, registrarlos en `DatosSetup` y `NegocioSetup`, y el controlador en `Api/Controllers`.

## Base de datos

El modelo está en `src/MahoSoft.Entidades` (una clase por tabla, agrupadas por área: configuración, catálogo, compras, ventas, inventario) y `src/MahoSoft.Datos`:

- `Configuraciones/` — longitudes, índices únicos, relaciones y validaciones (check constraints)
- `Migrations/` — historial de cambios del esquema
- `Seed/DbSeeder.cs` — datos de ejemplo iguales a los del frontend

Reglas generales:

- Dinero en `decimal(18,2)`; los estados (rol, método de pago…) se guardan como texto.
- Fechas en UTC (`timestamp with time zone`); las horas de Colombia se convierten al guardar.
- Los nombres y códigos únicos que se escriben a mano (categorías, bancos, grupos de tallas, colores, número de factura del proveedor, email) usan la collation `sin_mayusculas`: "Blusas" y "blusas" cuentan como el mismo.
- Los consecutivos (OC-…, VTA-…) se numeran con un bloqueo de PostgreSQL (`pg_advisory_xact_lock`), así dos cajas no sacan el mismo número.
- Nada con historial se borra: productos, proveedores, categorías y usuarios se desactivan, y las ventas anuladas quedan marcadas como `Anulada`. Solo las líneas de una compra o venta se borran con ella.
- El stock vive en `ProductoTallas` y cada cambio queda en `MovimientosInventario`; la suma de los movimientos de una talla es su stock.
- El costo de un producto es el de su última compra, sin IVA.

## Cómo levantarlo en local

Requisitos: .NET 9 SDK y PostgreSQL 17 en `localhost:5432`, usuario `postgres` con contraseña `postgres` (en Windows: `winget install PostgreSQL.PostgreSQL.17`). La cadena de conexión está en `appsettings.Development.json`, clave `ConnectionStrings:MahoSoft`; la base `mahosoft` la crea la API al arrancar.

La primera vez, crea la clave secreta con la que se firman las sesiones (no se guarda en el repositorio):

```bash
dotnet user-secrets set "Jwt:Key" "<al menos 32 caracteres aleatorios>" --project src/MahoSoft.Api
```

Para subir fotos de productos, las credenciales de Cloudinary (Dashboard → Settings → API Keys). Sin ellas la API arranca igual, pero subir una foto falla:

```bash
dotnet user-secrets set "Cloudinary:CloudName" "<cloud name>" --project src/MahoSoft.Api
dotnet user-secrets set "Cloudinary:ApiKey" "<api key>" --project src/MahoSoft.Api
dotnet user-secrets set "Cloudinary:ApiSecret" "<api secret>" --project src/MahoSoft.Api
```

Luego:

```bash
dotnet tool restore
dotnet run --project src/MahoSoft.Api --launch-profile http
```

Para enviar correos (factura al cliente y recuperar la contraseña) hay dos opciones. Sin ninguna, esas dos funciones responden 503 "todavía no está configurado".

- **Brevo** (la que se usa en producción, porque Render gratis bloquea SMTP): una clave de API de [Brevo](https://app.brevo.com/settings/keys/api) y el correo de la tienda verificado como remitente en Brevo (Senders & IP → Senders).

  ```bash
  dotnet user-secrets set "Correo:BrevoApiKey" "xkeysib-…" --project src/MahoSoft.Api
  dotnet user-secrets set "Correo:Remitente" "<correo de la tienda>" --project src/MahoSoft.Api
  ```

- **Gmail por SMTP**: la cuenta con una [contraseña de aplicación](https://myaccount.google.com/apppasswords) (requiere verificación en 2 pasos).

  ```bash
  dotnet user-secrets set "Correo:Usuario" "<correo>@gmail.com" --project src/MahoSoft.Api
  dotnet user-secrets set "Correo:Password" "<contraseña de aplicación>" --project src/MahoSoft.Api
  ```

Si está `Correo:BrevoApiKey` se usa Brevo; si no, SMTP.

`App:UrlFrontend` es la dirección del frontend que va en el enlace para recuperar la contraseña.

Las facturas de proveedores y los comprobantes se guardan en Supabase Storage si están `Supabase:Url` y `Supabase:ServiceKey`; si no, en el disco, en `Archivos:Carpeta` (`App_Data/archivos` dentro de la API por defecto; está en `.gitignore`). En local lo normal es el disco.

La API queda en `http://localhost:5241`. El frontend (`http://localhost:8443`) está permitido en `Cors:Origenes`.

En modo desarrollo, al arrancar se aplican las migraciones y, si la base está vacía, se cargan los datos de ejemplo. Los usuarios de ejemplo tienen la contraseña `Maho2026!`.

Para cambiar el modelo:

```bash
dotnet ef migrations add NombreDelCambio --project src/MahoSoft.Datos --startup-project src/MahoSoft.Api --output-dir Migrations
```

## Publicar (producción)

El sistema se publica así: **API en Render** (Docker), **frontend en Vercel** y **base de datos y archivos en Supabase**. Fuera de desarrollo **no se cargan datos de ejemplo** y toda la configuración sale de **variables de entorno**; en el código y en `appsettings.json` no hay secretos.

### 1. Supabase (base de datos y archivos)

1. Crear el proyecto en [supabase.com](https://supabase.com) y guardar la contraseña de la base.
2. **Cadena de conexión**: botón **Connect** → **Session pooler** (Render no llega a la conexión directa, que es solo IPv6). Pasarla al formato de Npgsql:

   ```
   Host=aws-0-<región>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<ref del proyecto>;Password=<contraseña>;SSL Mode=Require
   ```

3. **Archivos**: Project Settings → API Keys → copiar una **secret key** (`sb_secret_…`; sirve también la `service_role`). La API crea sola el bucket privado `documentos` la primera vez que guarda un archivo. Esa clave salta todas las reglas de seguridad: solo va en Render, nunca en el frontend.

Las tablas las crea la API al arrancar; no hay que ejecutar nada en el editor SQL.

### 2. Render (API)

1. New → **Web Service** → conectar el repositorio del backend. Render detecta el `Dockerfile` de la raíz (runtime Docker). Plan: Free.
2. Variables de entorno (Environment):

| Variable | Obligatoria | Para qué |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | sí | `Production` |
| `ConnectionStrings__MahoSoft` | sí | La cadena del Session pooler de Supabase (paso 1.2) |
| `Jwt__Key` | sí | Clave para firmar las sesiones: al menos 32 caracteres aleatorios, distinta a la de desarrollo |
| `Cors__Origenes__0` | sí | Dirección exacta del frontend en Vercel, con `https://` y sin `/` al final (más orígenes: `__1`, `__2`…) |
| `App__UrlFrontend` | sí | La misma dirección del frontend; va en el enlace para recuperar la contraseña |
| `Supabase__Url` | sí | `https://<ref del proyecto>.supabase.co` |
| `Supabase__ServiceKey` | sí | La secret key de Supabase (paso 1.3) |
| `Inicial__AdminEmail`, `Inicial__AdminPassword`, `Inicial__AdminNombre` | solo el primer arranque | El administrador (contraseña de al menos 12 caracteres). Quitarlas después |
| `Inicial__NombreNegocio` | no | Nombre del negocio al crear la base (se cambia luego en Configuración) |
| `Cloudinary__CloudName`, `Cloudinary__ApiKey`, `Cloudinary__ApiSecret` | para subir fotos | Sin ellas todo funciona, pero subir fotos responde "no está configurado" |
| `Correo__BrevoApiKey`, `Correo__Remitente` | para enviar correos | Clave de Brevo y el correo verificado en Brevo; sin ellas no salen facturas por correo ni enlaces de contraseña. Render gratis bloquea SMTP, así que Gmail directo no funciona ahí |

El `Dockerfile` ya fija el puerto (10000), instala las fuentes que necesitan los PDF y el Excel, y activa `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, porque Render termina HTTPS en su proxy.

Si falta una obligatoria, la API **no arranca** y el log de Render dice cuál falta.

En el plan gratis el servicio se duerme tras 15 minutos sin uso; la primera petición después tarda cerca de un minuto en responder.

### 3. Primer arranque

1. Con las variables definidas, incluidas las `Inicial__…`, desplegar. Al iniciar, la API aplica las migraciones y, en una base nueva, crea la configuración básica (tipos de documento, tallas, bancos, descuentos del POS, umbrales de stock) y el administrador. En cada despliegue vuelve a aplicar las migraciones pendientes y no toca los datos.
2. Entrar con esa cuenta y completar **Configuración → Datos del negocio**.
3. Quitar las variables `Inicial__…` en Render.

### 4. Vercel (frontend)

1. New Project → importar el repositorio del frontend. Vercel detecta Vite (build `npm run build`, salida `dist`).
2. Variable de entorno `VITE_API_URL` = la dirección de la API en Render (`https://<servicio>.onrender.com`, sin `/` al final). Se lee al compilar: si se cambia, hay que volver a desplegar.
3. La dirección que da Vercel va en `Cors__Origenes__0` y `App__UrlFrontend` de Render.

### Copias de seguridad

Supabase gratis no guarda copias descargables. Conviene sacar una cada tanto con `pg_dump` (viene con PostgreSQL):

```bash
pg_dump "<cadena de conexión del Session pooler en formato URI>" -Fc -f mahosoft.backup
```

## Pruebas automáticas

`tests/MahoSoft.Pruebas` levanta la API completa en memoria contra una base propia, `mahosoft_pruebas`, que se borra y se crea con los datos de ejemplo en cada corrida (la base de desarrollo no se toca). Necesita el mismo PostgreSQL local y la clave `Jwt:Key` en user-secrets.

```bash
dotnet test
```

Cubren lo que no se puede romper: totales, costo y stock de las compras; precio, descuento y stock de las ventas; que compras y ventas simultáneas no pierdan ni vendan de más; anulaciones; ajustes de inventario; que el stock de cada talla sea la suma de sus movimientos; que sin sesión nada responda y que no se puedan crear otros usuarios.

## Autenticación

- `POST /api/auth/login` `{ email, password }` → token JWT + usuario (nombre, rol, permisos). Máximo 5 intentos por minuto por dirección.
- `GET /api/auth/me` → el usuario conectado, con sus permisos actuales.
- `POST /api/auth/cambiar-password` `{ actual, nueva }`.
- `POST /api/auth/recuperar-password` `{ email }` → 204 exista o no el correo (así no se puede averiguar quién tiene cuenta). Si existe y está activo, envía un enlace `{App:UrlFrontend}/?restablecer=<código>` que vence en 1 hora. Solo se guarda el hash SHA-256 del código.
- `POST /api/auth/restablecer-password` `{ codigo, nueva }` → 204; 400 si el código no existe, venció o ya se usó.

Login, recuperar y restablecer comparten el límite de 5 intentos por minuto por dirección.

Todas las rutas exigen sesión salvo las marcadas con `[AllowAnonymous]`. Para exigir un permiso: `[Authorize(Policy = nameof(Permiso.Compras))]`. En cada petición el usuario se vuelve a leer de la base de datos, así que desactivarlo o cambiarle permisos aplica de inmediato. La sesión dura 12 horas (`Jwt:ExpiraHoras`).

## Configuración

Leer y modificar: el administrador.

- `GET /api/configuracion` → `{ nombre, nit, direccion, ciudad, telefono, correo, instagram, mensajeRecibo, stockBajoProducto, stockBajoTalla, descuentos: [0, 5…], bancos: ["Nequi"…], tallas: [{ nombre, valores: ["XS"…] }], tiposDocumento: ["CC"…] }`. Los textos vacíos llegan como `""`; bancos y tipos de documento, solo los activos y en orden.
- Cada sección se guarda entera y responde con toda la configuración: `PUT /api/configuracion/negocio`, `/inventario`, `/pos` (`{ descuentos, bancos }`), `/tallas` (`{ tallas }`) y `/tipos-documento` (`{ tiposDocumento }`).

Las listas se guardan como el usuario las ve. Lo que sale de la lista se borra, salvo lo que tiene historial:

- Un banco usado en un comprobante de transferencia, o un tipo de documento que tiene un proveedor, usuario o cliente, se **desactiva** (deja de ofrecerse y el historial queda intacto). Si se vuelve a agregar, se reactiva.
- Una talla que ya usan productos, compras, ventas o movimientos de inventario **no se puede quitar** (409). Una talla puede cambiar de grupo sin perder su stock.
- Los descuentos del POS se borran sin más: cada venta guarda su propio porcentaje.

## Inventario

Con permiso `Compras`.

- `GET /api/inventario/movimientos?productoId=&tipo=` → los 300 movimientos más recientes (opcionalmente de un producto o de un tipo: `Entrada`, `Salida`, `Ajuste`): `{ id, fecha, tipo, productoId, producto, talla, cantidad, motivo, usuario, compra, venta }`. `cantidad` lleva signo; `compra` o `venta` es el número del documento que lo originó.
- `POST /api/inventario/ajustes` `{ productoId, talla, motivo, cantidad, nota }` → 201 con el movimiento creado. Según `motivo`:
  - `Conteo`: `cantidad` son las unidades contadas; se registra un **ajuste** por la diferencia con el stock actual (si coincide, 400).
  - `Danado` (dañada o perdida) y `DevolucionProveedor`: `cantidad` unidades salen (**salida**); 409 si no hay tantas.
  - `Ingreso` (sin compra): `cantidad` unidades entran (**entrada**).

El stock se cambia en la base de datos en la misma transacción que el movimiento, como en compras y ventas.

## Inicio y reportes

Se calculan desde las ventas registradas (las anuladas no cuentan), en hora de Colombia. El ingreso de cada línea es su precio por cantidad menos su parte del descuento de la venta, así que categorías y productos suman lo vendido sin el envío.

- `GET /api/tablero` (permiso `Dashboard`) → ventas y transacciones de hoy, ayer, el mes y el mes anterior hasta el mismo día; ingresos de los últimos 6 meses; ventas por categoría y los 5 productos más vendidos del mes (con su stock); y las tallas de productos activos con stock en o bajo `StockBajoTalla` (las 8 más urgentes y el total).
- `GET /api/reportes?desde=2026-09-01&hasta=2026-09-30` (permiso `Reportes`, fechas incluidas, hasta dos años) → totales (vendido, transacciones, prendas, ticket promedio, ingresos, costo y margen bruto) contra el periodo anterior de igual duración, la serie por día (o por mes si pasa de dos meses), los 10 productos más vendidos, y las ventas por categoría y por medio de pago.
- `GET /api/reportes/excel?desde=…&hasta=…` y `GET /api/reportes/pdf?desde=…&hasta=…` → el mismo reporte como archivo.

El Excel se genera con ClosedXML y el PDF con QuestPDF, con la licencia Community (gratuita para negocios que facturan menos de 1 millón de dólares al año; se declara en `Program.cs`).

## Usuario y perfil

El sistema tiene **un solo usuario: el administrador**, con acceso a todo. No hay pantalla ni endpoints para crear o administrar otros usuarios. En producción esa cuenta se crea en el primer arranque con las variables `Inicial__…` (ver "Publicar"); en desarrollo es la de ejemplo, `ana@ellaboutique.co`.

- `GET /api/perfil` → los datos propios: `{ id, nombre, email, rol, telefono, tipoDocumento, documento, activo, ultimoAcceso, creadoEn, permisos }`. Nunca incluye la contraseña.
- `PUT /api/perfil` `{ nombre, email, telefono, tipoDocumento, documento }`. El email es el de inicio de sesión; no se repite y se guarda en minúsculas.
- La contraseña se cambia con `POST /api/auth/cambiar-password` `{ actual, nueva }`, o por correo con "¿Olvidaste tu contraseña?".

Los roles y permisos siguen en el modelo (el administrador los tiene todos y los endpoints los siguen exigiendo), pero no se usan para nada más.

## Ventas

Todo con permiso `POS`.

- `GET /api/ventas` → las ventas, la más reciente primero, **incluidas las anuladas**: `{ id, numeroFactura, fecha, tipo: "Tienda" | "Pedido", cliente: { id, nombre, tipoDocumento, documento, telefono, correo }, vendedorId, vendedor, metodoPago: "Efectivo" | "Tarjeta" | "Transferencia", descuentoPorcentaje, subtotal, descuento, envio, total, estado: "Registrada" | "Anulada", anuladaEn, anuladaPor, motivoAnulacion, entrega: { direccion, barrio, ciudad, fechaEntrega, notas }, comprobante: { banco, referencia, archivo }, unidades, items: [{ productoId, producto, talla, cantidad, precioUnitario }] }`.
- `GET /api/ventas/{id}`.
- `POST /api/ventas` (multipart): `datos` = JSON `{ tipo, metodoPago, descuentoPorcentaje, cliente: { nombre, tipoDocumento, documento, telefono, correo } | null, entrega: { direccion, barrio, ciudad, fechaEntrega, envio, notas } | null, comprobante: { banco, referencia } | null, items: [{ productoId, talla, cantidad }] }` y `comprobante` = foto o PDF de la transferencia (opcional, mismas reglas que las facturas de compra). → 201.
- `POST /api/ventas/{id}/anular` `{ motivo }`.
- `POST /api/ventas/{id}/enviar` `{ correo }` → envía el comprobante por correo (503 si el correo no está configurado).
- `GET /api/ventas/{id}/comprobante` → el comprobante de la transferencia.

Al registrar, en una sola transacción:

- Factura `VTA-{año}-NNNN`, consecutiva y sin repetirse aunque dos cajas vendan a la vez.
- **El precio y el costo salen del producto**, no de la petición; el costo del momento queda guardado en la venta. El descuento debe ser 0 o uno de los descuentos del POS en Configuración.
- El stock se descuenta **en la base de datos** (`Stock = Stock - n` solo si alcanza). Si dos cajas venden la última unidad a la vez, una recibe 409 "Solo quedan 0 unidades…". Cada línea queda como movimiento de **salida**.
- Un **pedido** necesita nombre y teléfono del cliente y la dirección de entrega; el envío se suma al total.
- El **cliente** se busca por documento y, si no, por teléfono; se actualiza con lo escrito o se crea. Sin datos es "Cliente general".
- El producto debe estar activo y venir en esa talla.

**Anular** pide motivo, guarda quién y cuándo, y devuelve las unidades con un movimiento de **entrada**. La venta no se borra: queda con estado `Anulada`.

El stock de compras y de los ajustes de Productos también se suma en la base de datos, así que ventas, compras y ajustes simultáneos nunca pisan el stock, y la suma de los movimientos de cada talla siempre es igual a su stock.

## Compras

Todo con permiso `Compras`.

- `GET /api/compras` → las compras, la factura más reciente primero: `{ id, numero, proveedorId, proveedor, tipoComprobante, numeroComprobante, fecha, hora, vendedorProveedor, cufe, condicionPago, fechaVencimiento, estadoPago, ivaPorcentaje, preciosIncluyenIva, subtotal, descuento, iva, total, valorComprobante, notas, usuario, documento: { nombre, tipoMime, tamano }, unidades, items: [{ productoId, producto, talla, referenciaProveedor, cantidad, precioUnitario, costoUnitario }] }`.
- `GET /api/compras/{id}`.
- `POST /api/compras` (multipart): `datos` = JSON `{ proveedorId, tipoComprobante, numeroComprobante, fecha: "2026-09-25", hora: "14:30" | "", vendedorProveedor, cufe, condicionPago: "Contado" | "Credito", fechaVencimiento, estadoPago: "Pagada" | "Pendiente", ivaPorcentaje, preciosIncluyenIva, descuento, valorComprobante, notas, items: [{ productoId, talla, referenciaProveedor, cantidad, precioUnitario }] }` y `documento` = PDF o foto opcional (JPG, PNG o WebP, hasta 10 MB; se revisa el contenido, no solo la extensión). → 201.
- `POST /api/compras/{id}/pagada` → marca la compra como pagada.
- `GET /api/compras/{id}/documento` → el PDF o la foto de la factura.

Al registrar, en una sola transacción:

- Se asigna el consecutivo `OC-{año}-NNN`, bloqueando la lectura para que dos compras simultáneas no reciban el mismo número.
- El servidor calcula los totales: subtotal sin IVA, descuento sobre el subtotal, IVA sobre lo que queda. El costo de cada prenda es su precio sin IVA menos su parte del descuento.
- Cada línea suma su stock con un movimiento de **entrada** (si el producto no tenía esa talla, la agrega) y actualiza el costo del producto, salvo que ya haya una compra con fecha de factura posterior.
- La misma factura de un proveedor no se registra dos veces (409). El proveedor debe estar activo.
- Si algo falla, no queda nada: ni la compra ni el archivo en el disco.

Los enums viajan como texto (`"Credito"`, `"Pagada"`) en toda la API.

## Productos

Leer: cualquier usuario con sesión (el punto de venta los vende). Crear, editar, eliminar y subir fotos: permiso `Compras`.

- `GET /api/productos` → `[{ id, nombre, categoriaId, precioVenta, costo, descripcion, colores: [], stock: { "S": 3, "M": 0 }, activo, imagenId, imagenUrl, proveedores: [{ id, nombre }], ultimaCompra: { numero, fecha, proveedorId, proveedor } }]`, por nombre. `stock` sigue el orden de tallas de Configuración; `costo` (sin IVA), `proveedores` (el más reciente primero) y `ultimaCompra` salen de las compras.
- `GET /api/productos/{id}`.
- `POST /api/productos` y `PUT /api/productos/{id}` `{ nombre, categoriaId, precioVenta, descripcion, colores, stock, imagenId, activo }`.
- `DELETE /api/productos/{id}` → 204; 409 si ya tiene compras o ventas (se desactiva en su lugar).
- `POST /api/productos/imagenes` (multipart, campo `archivo`; JPG, PNG o WebP de hasta 5 MB) → `{ id, url }`. Luego se guarda el producto con ese `imagenId`.

Reglas:

- `stock` son las tallas que tiene el producto y sus unidades. Cada cambio de unidades queda como movimiento de **ajuste** con el usuario que guardó ("Stock inicial" al crear). Una talla con unidades no se puede quitar (409): primero se pone en 0.
- Las fotos van a Cloudinary (carpeta `mahosoft/productos`, máximo 1200 px por lado). Al cambiar o quitar la foto, o al eliminar el producto, la anterior se borra de Cloudinary. Las fotos de ejemplo son enlaces externos y no se tocan.

## Proveedores

Leer: permiso `Proveedores` o `Compras` (las compras eligen un proveedor y muestran sus datos). Crear, editar y eliminar: permiso `Proveedores`.

- `GET /api/proveedores` → `[{ id, nombre, tipoDocumento, documento, direccion, ciudad, telefono, email, contacto, ivaPorcentaje, activo, compras, productos, categorias: [{ id, nombre }] }]`, por nombre. `compras`, `productos` (distintos) y `categorias` (lo que surte) salen de sus compras.
- `GET /api/proveedores/{id}`.
- `POST /api/proveedores` y `PUT /api/proveedores/{id}` `{ nombre, tipoDocumento, documento, direccion, ciudad, telefono, email, contacto, ivaPorcentaje, activo }`. `tipoDocumento` es el código (`NIT`, `CC`…); no se repite el mismo documento (409).
- `DELETE /api/proveedores/{id}` → 204; 409 si tiene compras (se desactiva en su lugar).

## Categorías

Leer: cualquier usuario con sesión (el punto de venta filtra por ellas). Crear, editar y eliminar: permiso `Compras`.

- `GET /api/categorias` → `[{ id, nombre, descripcion, activo, productos, productosActivos }]`, por nombre.
- `GET /api/categorias/{id}`.
- `POST /api/categorias` `{ nombre, descripcion, activo }` → 201. El nombre no se puede repetir (409).
- `PUT /api/categorias/{id}` `{ nombre, descripcion, activo }`; también sirve para activar o desactivar.
- `DELETE /api/categorias/{id}` → 204; 409 si tiene productos (se desactiva en su lugar).
