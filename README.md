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

Luego:

```bash
dotnet tool restore
dotnet run --project src/MahoSoft.Api --launch-profile http
```

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

## Categorías

Leer: cualquier usuario con sesión (el punto de venta filtra por ellas). Crear, editar y eliminar: permiso `Compras`.

- `GET /api/categorias` → `[{ id, nombre, descripcion, activo, productos, productosActivos }]`, por nombre.
- `GET /api/categorias/{id}`.
- `POST /api/categorias` `{ nombre, descripcion, activo }` → 201. El nombre no se puede repetir (409).
- `PUT /api/categorias/{id}` `{ nombre, descripcion, activo }`; también sirve para activar o desactivar.
- `DELETE /api/categorias/{id}` → 204; 409 si tiene productos (se desactiva en su lugar).
