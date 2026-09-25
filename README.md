# MahoSoft Backend

Backend de MahoSoft desarrollado en .NET.

El frontend está en [MahoSoft](https://github.com/santiagovegamadera-droid/MahoSoft).

## Tecnología

- .NET 9 (ASP.NET Core Web API) — `src/MahoSoft.Api`
- Entity Framework Core 9 con SQL Server
- Imágenes de productos en Cloudinary; PDF de facturas de proveedores en el disco del servidor

## Base de datos

El modelo está en `src/MahoSoft.Api/Data`:

- `Entities/` — una clase por tabla, agrupadas por área (configuración, catálogo, compras, ventas, inventario)
- `Configurations/` — longitudes, índices únicos, relaciones y validaciones (check constraints)
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

## Autenticación

- `POST /api/auth/login` `{ email, password }` → token JWT + usuario (nombre, rol, permisos). Máximo 5 intentos por minuto por dirección.
- `GET /api/auth/me` → el usuario conectado, con sus permisos actuales.
- `POST /api/auth/cambiar-password` `{ actual, nueva }`.

Todas las rutas exigen sesión salvo las marcadas con `[AllowAnonymous]`. Para exigir un permiso: `[Authorize(Policy = nameof(Permiso.Compras))]`. En cada petición el usuario se vuelve a leer de la base de datos, así que desactivarlo o cambiarle permisos aplica de inmediato. La sesión dura 12 horas (`Jwt:ExpiraHoras`).

En modo desarrollo, al arrancar se aplican las migraciones y, si la base está vacía, se cargan los datos de ejemplo. Los usuarios de ejemplo tienen la contraseña `Maho2026!`.

Para cambiar el modelo:

```bash
dotnet ef migrations add NombreDelCambio --project src/MahoSoft.Api --output-dir Data/Migrations
```
