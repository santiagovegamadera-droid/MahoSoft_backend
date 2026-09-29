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

Requisitos: .NET 9 SDK y SQL Server en `localhost` con autenticación de Windows (la cadena de conexión está en `appsettings.json`, clave `ConnectionStrings:MahoSoft`).

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

Las facturas de proveedores se guardan en el disco, en `Archivos:Carpeta` (`App_Data/archivos` dentro de la API por defecto; está en `.gitignore`).

La API queda en `http://localhost:5241`. El frontend (`http://localhost:8443`) está permitido en `Cors:Origenes`.

En modo desarrollo, al arrancar se aplican las migraciones y, si la base está vacía, se cargan los datos de ejemplo. Los usuarios de ejemplo tienen la contraseña `Maho2026!`.

Para cambiar el modelo:

```bash
dotnet ef migrations add NombreDelCambio --project src/MahoSoft.Datos --startup-project src/MahoSoft.Api --output-dir Migrations
```

## Autenticación

- `POST /api/auth/login` `{ email, password }` → token JWT + usuario (nombre, rol, permisos). Máximo 5 intentos por minuto por dirección.
- `GET /api/auth/me` → el usuario conectado, con sus permisos actuales.
- `POST /api/auth/cambiar-password` `{ actual, nueva }`.

Todas las rutas exigen sesión salvo las marcadas con `[AllowAnonymous]`. Para exigir un permiso: `[Authorize(Policy = nameof(Permiso.Compras))]`. En cada petición el usuario se vuelve a leer de la base de datos, así que desactivarlo o cambiarle permisos aplica de inmediato. La sesión dura 12 horas (`Jwt:ExpiraHoras`).

## Configuración

Leer: cualquier usuario con sesión (recibos, punto de venta, formularios). Modificar: solo la administradora (rol `Administradora`).

- `GET /api/configuracion` → `{ nombre, nit, direccion, ciudad, telefono, correo, instagram, mensajeRecibo, stockBajoProducto, stockBajoTalla, descuentos: [0, 5…], bancos: ["Nequi"…], tallas: [{ nombre, valores: ["XS"…] }], tiposDocumento: ["CC"…] }`. Los textos vacíos llegan como `""`; bancos y tipos de documento, solo los activos y en orden.
- Cada sección se guarda entera y responde con toda la configuración: `PUT /api/configuracion/negocio`, `/inventario`, `/pos` (`{ descuentos, bancos }`), `/tallas` (`{ tallas }`) y `/tipos-documento` (`{ tiposDocumento }`).

Las listas se guardan como el usuario las ve. Lo que sale de la lista se borra, salvo lo que tiene historial:

- Un banco usado en un comprobante de transferencia, o un tipo de documento que tiene un proveedor, usuario o cliente, se **desactiva** (deja de ofrecerse y el historial queda intacto). Si se vuelve a agregar, se reactiva.
- Una talla que ya usan productos, compras, ventas o movimientos de inventario **no se puede quitar** (409). Una talla puede cambiar de grupo sin perder su stock.
- Los descuentos del POS se borran sin más: cada venta guarda su propio porcentaje.

## Ventas

Todo con permiso `POS`.

- `GET /api/ventas` → las ventas, la más reciente primero, **incluidas las anuladas**: `{ id, numeroFactura, fecha, tipo: "Tienda" | "Pedido", cliente: { id, nombre, tipoDocumento, documento, telefono, correo }, vendedorId, vendedor, metodoPago: "Efectivo" | "Tarjeta" | "Transferencia", descuentoPorcentaje, subtotal, descuento, envio, total, estado: "Registrada" | "Anulada", anuladaEn, anuladaPor, motivoAnulacion, entrega: { direccion, barrio, ciudad, fechaEntrega, notas }, comprobante: { banco, referencia, archivo }, unidades, items: [{ productoId, producto, talla, cantidad, precioUnitario }] }`.
- `GET /api/ventas/{id}`.
- `POST /api/ventas` (multipart): `datos` = JSON `{ tipo, metodoPago, descuentoPorcentaje, cliente: { nombre, tipoDocumento, documento, telefono, correo } | null, entrega: { direccion, barrio, ciudad, fechaEntrega, envio, notas } | null, comprobante: { banco, referencia } | null, items: [{ productoId, talla, cantidad }] }` y `comprobante` = foto o PDF de la transferencia (opcional, mismas reglas que las facturas de compra). → 201.
- `POST /api/ventas/{id}/anular` `{ motivo }`.
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
