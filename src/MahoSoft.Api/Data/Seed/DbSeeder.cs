using MahoSoft.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MahoSoft.Api.Data.Seed;

/// <summary>
/// Loads the same sample data the frontend uses, so both look alike while the API is wired in.
/// Runs only on an empty database.
/// </summary>
public static class DbSeeder
{
    /// <summary>Password given to the sample users; change it before real use.</summary>
    public const string PasswordInicial = "Maho2026!";

    // Colombia is UTC-5 all year
    private static readonly TimeSpan Colombia = TimeSpan.FromHours(-5);

    private static DateTimeOffset At(string localDateTime) => new(DateTime.Parse(localDateTime), Colombia);

    public static async Task SeedAsync(AppDbContext db, IPasswordHasher<Usuario> hasher)
    {
        if (await db.Negocio.AnyAsync())
            return;

        var ahora = DateTimeOffset.UtcNow.ToOffset(Colombia);
        await using var tx = await db.Database.BeginTransactionAsync();

        // ---- Configuración
        db.Negocio.Add(
            new Negocio
            {
                Id = 1,
                Nombre = "Maho Boutique",
                MensajeRecibo = "Gracias por tu compra en Maho Boutique",
                StockBajoProducto = 5,
                StockBajoTalla = 3,
            }
        );

        var tipos = new[] { "CC", "CE", "NIT", "TI", "Pasaporte" }
            .Select((c, i) => new TipoDocumento { Codigo = c, Orden = i })
            .ToDictionary(t => t.Codigo);
        db.TiposDocumento.AddRange(tipos.Values);

        var grupos = new[]
        {
            ("Letras", new[] { "XS", "S", "M", "L", "XL", "XXL" }),
            ("Numéricas", new[] { "25", "26", "27", "28", "29", "30", "32" }),
        }.Select(
            (g, i) =>
                new GrupoTalla
                {
                    Nombre = g.Item1,
                    Orden = i,
                    Tallas = g.Item2.Select((v, j) => new Talla { Valor = v, Orden = j }).ToList(),
                }
        );
        db.GruposTalla.AddRange(grupos);
        var tallas = db.ChangeTracker.Entries<Talla>().Select(e => e.Entity).ToDictionary(t => t.Valor);

        db.Bancos.AddRange(
            new[] { "Nequi", "Daviplata", "Bancolombia", "Davivienda", "Banco de Bogotá", "BBVA", "Otro" }.Select(
                (b, i) => new Banco { Nombre = b, Orden = i }
            )
        );
        db.DescuentosPos.AddRange(new[] { 0m, 5, 10, 15, 20, 30 }.Select(p => new DescuentoPos { Porcentaje = p }));

        // ---- Usuarios
        var todos = Enum.GetValues<Permiso>();
        Usuario NuevoUsuario(string nombre, string email, Rol rol, Permiso[] permisos, bool activo, string ultimo)
        {
            var u = new Usuario
            {
                Nombre = nombre,
                Email = email,
                Rol = rol,
                Activo = activo,
                UltimoAcceso = At(ultimo),
                CreadoEn = ahora,
                Permisos = permisos.Select(p => new UsuarioPermiso { Permiso = p }).ToList(),
            };
            u.PasswordHash = hasher.HashPassword(u, PasswordInicial);
            return u;
        }
        var usuarios = new[]
        {
            NuevoUsuario("Ana Martínez", "ana@ellaboutique.co", Rol.Administradora, todos, true, "2026-09-23 08:15"),
            NuevoUsuario("Carla Rodríguez", "carla@ellaboutique.co", Rol.Vendedora, [Permiso.POS], true, "2026-09-23 09:02"),
            NuevoUsuario("Sofía Parra", "sofia@ellaboutique.co", Rol.Vendedora, [Permiso.POS], true, "2026-09-22 18:45"),
            NuevoUsuario("Valentina Ruiz", "vale@ellaboutique.co", Rol.Vendedora, [Permiso.POS], false, "2026-09-10 12:30"),
            NuevoUsuario("Jorge Mejía", "jorge@ellaboutique.co", Rol.Bodega, [Permiso.Compras], true, "2026-09-23 07:58"),
        }.ToDictionary(u => u.Nombre);
        db.Usuarios.AddRange(usuarios.Values);
        var ana = usuarios["Ana Martínez"];

        // ---- Catálogo
        var categorias = new[]
        {
            ("Vestidos", "Vestidos de día, noche y ocasión especial"),
            ("Blusas", "Tops, blusas y camisetas"),
            ("Pantalones", "Jeans, pantalones formales y casuales"),
            ("Faldas", "Faldas mini, midi y maxi"),
            ("Conjuntos", "Sets de dos y tres piezas"),
            ("Abrigos", "Cardigans, blazers y abrigos"),
            ("Tops", "Tops de fiesta y crop tops"),
        }.Select(c => new Categoria { Nombre = c.Item1, Descripcion = c.Item2 }).ToDictionary(c => c.Nombre);
        db.Categorias.AddRange(categorias.Values);

        // Target stock per size, as in the frontend sample
        var productos = new[]
        {
            P("Vestido Floral Verano", "Vestidos", 89900, 45000, "Vestido floral de verano en tela fresca y liviana.", ["Rosa", "Azul"], new() { ["XS"] = 4, ["S"] = 8, ["M"] = 10, ["L"] = 6 }, "1572804013309-59a88b7e92f1"),
            P("Blusa Seda Negra", "Blusas", 65000, 30000, "Blusa de seda con caída suave.", ["Negro", "Blanco"], new() { ["XS"] = 2, ["S"] = 5, ["M"] = 5, ["L"] = 3, ["XL"] = 0 }, "1485462537746-965f33f7f6a7"),
            P("Jean Skinny Azul", "Pantalones", 119000, 60000, "Jean tiro alto con elasticidad.", ["Azul oscuro"], new() { ["25"] = 1, ["26"] = 0, ["28"] = 2, ["30"] = 0 }, "1541099649105-f69ad21f3246"),
            P("Falda Plisada Beige", "Faldas", 75000, 35000, "Falda midi plisada.", ["Beige", "Negro"], new() { ["XS"] = 3, ["S"] = 6, ["M"] = 8, ["L"] = 5 }, "1583496661160-fb5886a0aaaa"),
            P("Conjunto Lino Blanco", "Conjuntos", 185000, 90000, "Conjunto de dos piezas en lino.", ["Blanco"], new() { ["S"] = 0, ["M"] = 0, ["L"] = 0 }, "1515886657613-9f3515b0c78f"),
            P("Cardigan Tejido Crema", "Abrigos", 145000, 70000, "Cardigan tejido de punto grueso.", ["Crema", "Gris"], new() { ["S"] = 3, ["M"] = 5, ["L"] = 4, ["XL"] = 6 }, "1434389677669-e08b4cac3105"),
            P("Top Crop Lentejuelas", "Tops", 98000, 45000, "Top corto con lentejuelas para la noche.", ["Dorado", "Plateado"], new() { ["XS"] = 3, ["S"] = 4, ["M"] = 2 }, "1594938298603-c8148c4b4017"),
            P("Pantalón Palazzo Rojo", "Pantalones", 109000, 52000, "Pantalón palazzo de pierna ancha.", ["Rojo"], new() { ["XS"] = 2, ["S"] = 4, ["M"] = 3, ["L"] = 2 }, "1506629082955-511b1aa562c8", activo: false),
        };
        db.Productos.AddRange(productos.Select(p => p.Producto));
        var producto = productos.ToDictionary(p => p.Producto.Nombre, p => p.Producto);

        (Producto Producto, Dictionary<string, int> Stock) P(
            string nombre,
            string categoria,
            decimal precio,
            decimal costo,
            string descripcion,
            string[] colores,
            Dictionary<string, int> stock,
            string unsplashId,
            bool activo = true
        ) =>
            (
                new Producto
                {
                    Nombre = nombre,
                    Categoria = categorias[categoria],
                    PrecioVenta = precio,
                    CostoActual = costo,
                    Descripcion = descripcion,
                    Activo = activo,
                    CreadoEn = ahora,
                    Colores = colores.Select(c => new ProductoColor { Color = c }).ToList(),
                    Imagen = new Archivo
                    {
                        Nombre = $"{nombre}.jpg",
                        TipoMime = "image/jpeg",
                        Almacen = AlmacenArchivo.Externo,
                        Ubicacion = $"https://images.unsplash.com/photo-{unsplashId}?w=300&h=300&fit=crop&auto=format",
                        SubidoEn = ahora,
                    },
                    // Stock is filled in below from the movements
                    Tallas = stock.Keys.Select(t => new ProductoTalla { Talla = tallas[t] }).ToList(),
                },
                stock
            );

        // ---- Proveedores
        var proveedores = new[]
        {
            Prov("Textiles Bogotá S.A.S.", "900123456-1", "Pedro Vargas", "pvargas@textilesbog.com", "601-234-5678", "Cra 13 # 45-20, Chapinero", "Bogotá", 19),
            Prov("ModaCali S.A.", "800234567-2", "Sandra Lozano", "slozano@modacali.com", "602-345-6789", "Calle 5 # 38-25, San Fernando", "Cali", 19),
            Prov("DenimCo", "901345678-3", "Ricardo Montoya", "r.montoya@denimco.co", "604-456-7890", "CL 48 # 53-62, CC Venaver piso 10", "Medellín", 0),
            Prov("LuxFashion Ltda.", "830456789-4", "Andrea Silva", "asilva@luxfashion.com", "605-567-8901", "Av. Calle 80 # 69-70, Bodega 4", "Bogotá", 19),
            Prov("KnitCo Textiles", "900567890-5", "Jorge Pérez", "jperez@knitco.co", "607-678-9012", "Cra 23 # 64-15, Cable Plaza", "Manizales", 0, activo: false),
            Prov("GlamourBtq", "901678901-6", "Lucía Ramírez", "lucia@glamourbtq.co", "601-876-5432", "Calle 72 # 10-34, Local 102", "Bogotá", 0),
        }.ToDictionary(p => p.Nombre);
        db.Proveedores.AddRange(proveedores.Values);

        Proveedor Prov(
            string nombre,
            string nit,
            string contacto,
            string email,
            string tel,
            string direccion,
            string ciudad,
            decimal iva,
            bool activo = true
        ) =>
            new()
            {
                Nombre = nombre,
                TipoDocumento = tipos["NIT"],
                Documento = nit,
                Contacto = contacto,
                Email = email,
                Telefono = tel,
                Direccion = direccion,
                Ciudad = ciudad,
                IvaPorcentaje = iva,
                Activo = activo,
            };

        // ---- Compras (registered before IVA existed, so no IVA)
        var movimientos = new List<MovimientoInventario>();
        Compra NuevaCompra(string numero, string fecha, string proveedor, string factura, string notas, string usuario, string prod, string talla, int cant, decimal costo)
        {
            var total = cant * costo;
            var c = new Compra
            {
                Numero = numero,
                Proveedor = proveedores[proveedor],
                TipoComprobante = "Factura electrónica de venta",
                NumeroComprobante = factura,
                FechaComprobante = At(fecha),
                CondicionPago = CondicionPago.Contado,
                EstadoPago = EstadoPago.Pagada,
                Subtotal = total,
                Total = total,
                Notas = notas == "" ? null : notas,
                Usuario = usuarios[usuario],
                CreadoEn = At(fecha),
                Items =
                [
                    new CompraItem
                    {
                        Producto = producto[prod],
                        Talla = tallas[talla],
                        Cantidad = cant,
                        PrecioUnitario = costo,
                        CostoUnitario = costo,
                    },
                ],
            };
            movimientos.Add(Mov(At(fecha), TipoMovimiento.Entrada, prod, talla, cant, $"Compra {numero}", usuarios[usuario], compra: c));
            return c;
        }
        db.Compras.AddRange(
            NuevaCompra("OC-2026-044", "2026-09-21", "LuxFashion Ltda.", "LF-10233", "", "Jorge Mejía", "Cardigan Tejido Crema", "XL", 12, 72000),
            NuevaCompra("OC-2026-045", "2026-09-23", "Textiles Bogotá S.A.S.", "TB-8841", "Llegó completo", "Ana Martínez", "Vestido Floral Verano", "M", 20, 45000)
        );
        movimientos.Add(Mov(At("2026-09-22 10:00"), TipoMovimiento.Salida, "Blusa Seda Negra", "S", -2, "Devolución proveedor", usuarios["Jorge Mejía"]));
        movimientos.Add(Mov(At("2026-09-22 17:30"), TipoMovimiento.Ajuste, "Falda Plisada Beige", "L", -1, "AJ-0091", ana));

        // ---- Ventas
        var clientes = new[] { "Laura Gómez", "Daniela Torres", "Marcela Ríos" }
            .Select(n => new Cliente { Nombre = n, CreadoEn = ahora })
            .ToDictionary(c => c.Nombre);
        db.Clientes.AddRange(clientes.Values);

        Venta NuevaVenta(string factura, string fecha, string cliente, string vendedor, MetodoPago pago, decimal descuento, params (string Prod, string Talla, int Cant, decimal Precio)[] items)
        {
            var subtotal = items.Sum(i => i.Cant * i.Precio);
            var monto = Math.Round(subtotal * descuento / 100);
            var v = new Venta
            {
                NumeroFactura = factura,
                Fecha = At(fecha),
                Tipo = TipoVenta.Tienda,
                Cliente = clientes.GetValueOrDefault(cliente),
                Vendedor = usuarios[vendedor],
                MetodoPago = pago,
                DescuentoPorcentaje = descuento,
                Subtotal = subtotal,
                Descuento = monto,
                Total = subtotal - monto,
                Estado = EstadoVenta.Registrada,
                Items = items
                    .Select(i => new VentaItem
                    {
                        Producto = producto[i.Prod],
                        Talla = tallas[i.Talla],
                        NombreProducto = i.Prod,
                        Cantidad = i.Cant,
                        PrecioUnitario = i.Precio,
                        CostoUnitario = producto[i.Prod].CostoActual,
                    })
                    .ToList(),
            };
            foreach (var i in items)
                movimientos.Add(Mov(v.Fecha, TipoMovimiento.Salida, i.Prod, i.Talla, -i.Cant, $"Venta {factura}", v.Vendedor, venta: v));
            return v;
        }
        db.Ventas.AddRange(
            NuevaVenta("VTA-2026-0842", "2026-09-21 10:24", "Laura Gómez", "Carla Rodríguez", MetodoPago.Tarjeta, 0, ("Vestido Floral Verano", "M", 1, 89900), ("Blusa Seda Negra", "S", 1, 65000)),
            NuevaVenta("VTA-2026-0843", "2026-09-21 16:05", "Cliente general", "Sofía Parra", MetodoPago.Efectivo, 10, ("Falda Plisada Beige", "M", 2, 75000)),
            NuevaVenta("VTA-2026-0844", "2026-09-22 11:40", "Daniela Torres", "Carla Rodríguez", MetodoPago.Transferencia, 5, ("Conjunto Lino Blanco", "L", 1, 185000), ("Top Crop Lentejuelas", "M", 1, 98000)),
            NuevaVenta("VTA-2026-0845", "2026-09-23 09:15", "Marcela Ríos", "Ana Martínez", MetodoPago.Tarjeta, 0, ("Cardigan Tejido Crema", "S", 1, 145000)),
            NuevaVenta("VTA-2026-0846", "2026-09-23 10:02", "Cliente general", "Sofía Parra", MetodoPago.Efectivo, 0, ("Jean Skinny Azul", "28", 1, 119000), ("Blusa Seda Negra", "M", 1, 65000))
        );

        // ---- Inventario inicial: an opening adjustment per size so movements add up to the stock.
        // Where the sample stock is lower than what later movements imply, the opening is 0 and the
        // stock becomes whatever the movements leave.
        foreach (var (p, objetivo) in productos)
        {
            foreach (var pt in p.Tallas)
            {
                var valor = pt.Talla.Valor;
                var neto = movimientos.Where(m => m.Producto == p && m.Talla == pt.Talla).Sum(m => m.Cantidad);
                var inicial = Math.Max(0, objetivo[valor] - neto);
                if (inicial > 0)
                    movimientos.Add(Mov(At("2026-09-01 08:00"), TipoMovimiento.Ajuste, p.Nombre, valor, inicial, "Inventario inicial", ana));
                pt.Stock = inicial + neto;
            }
        }
        db.MovimientosInventario.AddRange(movimientos);

        MovimientoInventario Mov(DateTimeOffset fecha, TipoMovimiento tipo, string prod, string talla, int cant, string motivo, Usuario usuario, Compra? compra = null, Venta? venta = null) =>
            new()
            {
                Fecha = fecha,
                Tipo = tipo,
                Producto = producto[prod],
                Talla = tallas[talla],
                Cantidad = cant,
                Motivo = motivo,
                Usuario = usuario,
                Compra = compra,
                Venta = venta,
            };

        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
