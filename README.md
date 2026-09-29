# MahoSoft Backend

Backend de MahoSoft desarrollado en .NET.

El frontend está en [MahoSoft](https://github.com/santiagovegamadera-droid/MahoSoft).

## Tecnología

- .NET 9 (ASP.NET Core Web API)
- Entity Framework Core 9 con SQL Server
- Imágenes de productos en Cloudinary; PDF de facturas de proveedores en el disco del servidor

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
- Nada con historial se borra: productos, proveedores, categorías y usuarios se desactivan, y las ventas anuladas quedan marcadas como `Anulada`. Solo las líneas de una compra o venta se borran con ella.
- El stock vive en `ProductoTallas` y cada cambio queda en `MovimientosInventario`; la suma de los movimientos de una talla es su stock.
- El costo de un producto es el de su última compra, sin IVA.

## Cómo levantarlo en local

Requisitos: .NET 9 SDK y SQL Server en `localhost` con autenticación de Windows (la cadena de conexión está en `appsettings.Development.json`, clave `ConnectionStrings:MahoSoft`).

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

Para enviar correos (factura al cliente y recuperar la contraseña), la cuenta de Gmail de la tienda con una [contraseña de aplicación](https://myaccount.google.com/apppasswords) (requiere verificación en 2 pasos). Sin esto, esas dos funciones responden 503 "todavía no está configurado":

```bash
dotnet user-secrets set "Correo:Usuario" "<correo>@gmail.com" --project src/MahoSoft.Api
dotnet user-secrets set "Correo:Password" "<contraseña de aplicación>" --project src/MahoSoft.Api
```

`App:UrlFrontend` es la dirección del frontend que va en el enlace para recuperar la contraseña.

Las facturas de proveedores se guardan en el disco, en `Archivos:Carpeta` (`App_Data/archivos` dentro de la API por defecto; está en `.gitignore`).

La API queda en `http://localhost:5241`. El frontend (`http://localhost:8443`) está permitido en `Cors:Origenes`.

En modo desarrollo, al arrancar se aplican las migraciones y, si la base está vacía, se cargan los datos de ejemplo. Los usuarios de ejemplo tienen la contraseña `Maho2026!`.

Para cambiar el modelo:

```bash
dotnet ef migrations add NombreDelCambio --project src/MahoSoft.Datos --startup-project src/MahoSoft.Api --output-dir Migrations
```

## Publicar (producción)

Sirve para cualquier proveedor (Azure, un VPS, un servidor propio con IIS…). Fuera de desarrollo **no se cargan datos de ejemplo** y toda la configuración sale de **variables de entorno**; en el código y en `appsettings.json` no hay secretos. La configuración local (base, CORS, dirección del frontend) está solo en `appsettings.Development.json`.

### Variables de entorno de la API

| Variable | Obligatoria | Para qué |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | sí | `Production` |
| `ConnectionStrings__MahoSoft` | sí | Cadena de conexión a SQL Server |
| `Jwt__Key` | sí | Clave para firmar las sesiones: al menos 32 caracteres aleatorios, distinta a la de desarrollo |
| `Cors__Origenes__0` | sí | Dirección exacta del frontend, con `https://` (más orígenes: `__1`, `__2`…) |
| `App__UrlFrontend` | sí | La misma dirección del frontend; va en el enlace para recuperar la contraseña |
| `Inicial__AdminEmail`, `Inicial__AdminPassword`, `Inicial__AdminNombre` | solo el primer arranque | Primera administradora (contraseña de al menos 12 caracteres). Quitarlas después |
| `Inicial__NombreNegocio` | no | Nombre del negocio al crear la base (se cambia luego en Configuración) |
| `Archivos__Carpeta` | recomendada | Carpeta de las facturas y comprobantes, en un disco con copia de seguridad |
| `Cloudinary__CloudName`, `Cloudinary__ApiKey`, `Cloudinary__ApiSecret` | para subir fotos | Sin ellas todo funciona, pero subir fotos responde "no está configurado" |
| `Correo__Usuario`, `Correo__Password` | para enviar correos | Gmail y contraseña de aplicación; sin ellas no salen facturas por correo ni enlaces de contraseña |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | detrás de un proxy | `true` si HTTPS lo termina un proxy o el proveedor (Azure App Service, Nginx…) |

Si falta una obligatoria, la API **no arranca** y el error dice cuál falta.

### Primer arranque

1. Crear la base vacía en SQL Server (solo la base: las tablas las crea la API).
2. Definir las variables, incluidas las `Inicial__…`, y arrancar la API. Al iniciar aplica las migraciones y, en una base nueva, crea la configuración básica (tipos de documento, tallas, bancos, descuentos del POS, umbrales de stock) y la primera administradora con todos los permisos. En cada arranque vuelve a aplicar las migraciones pendientes y no toca los datos existentes.
3. Entrar con esa cuenta, cambiar la contraseña si se quiere, completar **Configuración → Datos del negocio** y crear los demás usuarios en **Usuarios**.
4. Quitar las variables `Inicial__…`.

Compilar la API: `dotnet publish src/MahoSoft.Api -c Release -o publicar` y subir la carpeta `publicar`.

### Frontend

Es un sitio estático. Se compila con la dirección pública de la API y se sube la carpeta `dist` a cualquier hosting estático (Azure Static Web Apps, Netlify, IIS…):

```bash
cd frontend
VITE_API_URL=https://api.tudominio.com npm run build
```

(En PowerShell: `$env:VITE_API_URL="https://api.tudominio.com"; npm run build`.) El dominio del frontend debe estar en `Cors__Origenes__0` y en `App__UrlFrontend` de la API, y ambos deben servirse con HTTPS: la API responde con HSTS fuera de desarrollo.

## Pruebas automáticas

`tests/MahoSoft.Pruebas` levanta la API completa en memoria contra una base propia, `MahoSoft_Pruebas`, que se borra y se crea con los datos de ejemplo en cada corrida (la base de desarrollo no se toca). Necesita el mismo SQL Server local y la clave `Jwt:Key` en user-secrets.

```bash
dotnet test
```

Cubren lo que no se puede romper: totales, costo y stock de las compras; precio, descuento y stock de las ventas; que compras y ventas simultáneas no pierdan ni vendan de más; anulaciones; ajustes de inventario; que el stock de cada talla sea la suma de sus movimientos; y qué puede hacer cada rol.

## Autenticación

- `POST /api/auth/login` `{ email, password }` → token JWT + usuario (nombre, rol, permisos). Máximo 5 intentos por minuto por dirección.
- `GET /api/auth/me` → el usuario conectado, con sus permisos actuales.
- `POST /api/auth/cambiar-password` `{ actual, nueva }`.
- `POST /api/auth/recuperar-password` `{ email }` → 204 exista o no el correo (así no se puede averiguar quién tiene cuenta). Si existe y está activo, envía un enlace `{App:UrlFrontend}/?restablecer=<código>` que vence en 1 hora. Solo se guarda el hash SHA-256 del código.
- `POST /api/auth/restablecer-password` `{ codigo, nueva }` → 204; 400 si el código no existe, venció o ya se usó.

Login, recuperar y restablecer comparten el límite de 5 intentos por minuto por dirección.

Todas las rutas exigen sesión salvo las marcadas con `[AllowAnonymous]`. Para exigir un permiso: `[Authorize(Policy = nameof(Permiso.Compras))]`. En cada petición el usuario se vuelve a leer de la base de datos, así que desactivarlo o cambiarle permisos aplica de inmediato. La sesión dura 12 horas (`Jwt:ExpiraHoras`).

## Configuración

Leer: cualquier usuario con sesión (recibos, punto de venta, formularios). Modificar: solo la administradora (rol `Administradora`).

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

## Usuarios y perfil

Usuarios, con permiso `Usuarios`:

- `GET /api/usuarios` → `[{ id, nombre, email, rol, telefono, tipoDocumento, documento, activo, ultimoAcceso, creadoEn, permisos }]`, por nombre. Nunca incluye la contraseña.
- `POST /api/usuarios` `{ nombre, email, rol, telefono, tipoDocumento, documento, permisos, activo, password }` → 201. La contraseña inicial es obligatoria (mínimo 8 caracteres).
- `PUT /api/usuarios/{id}` (los mismos campos, sin `password`); también activa o desactiva.
- `POST /api/usuarios/{id}/password` `{ nueva }` → restablece la contraseña.

Reglas: el email no se repite (409) y se guarda en minúsculas. Los usuarios no se borran, se desactivan (un desactivado no puede iniciar sesión y su sesión abierta deja de servir). Nadie puede desactivarse ni quitarse el permiso `Usuarios` a sí mismo, y siempre debe quedar al menos un usuario activo con ese permiso.

Perfil, para cualquier usuario con sesión:

- `GET /api/perfil` → los datos propios, con la misma forma que un usuario.
- `PUT /api/perfil` `{ nombre, email, telefono, tipoDocumento, documento }`. El rol y los permisos solo los cambia quien tiene el permiso `Usuarios`.
- La contraseña propia se cambia con `POST /api/auth/cambiar-password` `{ actual, nueva }`.

## Ventas

Todo con permiso `POS`.

- `GET /api/ventas` → las ventas, la más reciente primero, **incluidas las anuladas**: `{ id, numeroFactura, fecha, tipo: "Tienda" | "Pedido", cliente: { id, nombre, tipoDocumento, documento, telefono, correo }, vendedorId, vendedor, metodoPago: "Efectivo" | "Tarjeta" | "Transferencia", descuentoPorcentaje, subtotal, descuento, envio, total, estado: "Registrada" | "Anulada", anuladaEn, anuladaPor, motivoAnulacion, entrega: { direccion, barrio, ciudad, fechaEntrega, notas }, comprobante: { banco, referencia, archivo }, unidades, items: [{ productoId, producto, talla, cantidad, precioUnitario }] }`.
- `GET /api/ventas/{id}`.
- `POST /api/ventas` (multipart): `datos` = JSON `{ tipo, metodoPago, descuentoPorcentaje, cliente: { nombre, tipoDocumento, documento, telefono, correo } | null, entrega: { direccion, barrio, ciudad, fechaEntrega, envio, notas } | null, comprobante: { banco, referencia } | null, items: [{ productoId, talla, cantidad }] }` y `comprobante` = foto o PDF de la transferencia (opcional, mismas reglas que las facturas de compra). → 201.
- `POST /api/ventas/{id}/anular` `{ motivo }`.
- `POST /api/ventas/{id}/enviar` `{ correo }` → envía el comprobante por correo (503 si el correo no está configurado).
- `GET /api/ventas/{id}/comprobante` → el comprobante de la transferencia.
- `GET /api/clientes?q=` → hasta 8 clientes cuyo documento, teléfono o nombre contiene el texto (mínimo 3 caracteres).

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
