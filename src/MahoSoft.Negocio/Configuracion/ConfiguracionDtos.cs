using System.ComponentModel.DataAnnotations;

namespace MahoSoft.Negocio.Configuracion;

/// <summary>
/// All settings in the shape the frontend uses: empty text fields come as "", and the lists hold only
/// what the forms should offer (active document types and banks), in order.
/// </summary>
public record ConfiguracionDto(
    string Nombre,
    string Nit,
    string Direccion,
    string Ciudad,
    string Telefono,
    string Correo,
    string Instagram,
    string MensajeRecibo,
    int StockBajoProducto,
    int StockBajoTalla,
    int[] Descuentos,
    string[] Bancos,
    GrupoTallaDto[] Tallas,
    string[] TiposDocumento
);

public record GrupoTallaDto(string Nombre, string[] Valores);

public record NegocioRequest(
    [Required(ErrorMessage = "El nombre es obligatorio"), MaxLength(150, ErrorMessage = "El nombre admite hasta 150 caracteres")]
        string Nombre,
    [MaxLength(30, ErrorMessage = "El NIT o cédula admite hasta 30 caracteres")] string? Nit,
    [MaxLength(200, ErrorMessage = "La dirección admite hasta 200 caracteres")] string? Direccion,
    [MaxLength(200, ErrorMessage = "La ciudad admite hasta 200 caracteres")] string? Ciudad,
    [MaxLength(30, ErrorMessage = "El teléfono admite hasta 30 caracteres")] string? Telefono,
    [MaxLength(256, ErrorMessage = "El correo admite hasta 256 caracteres")] string? Correo,
    [MaxLength(100, ErrorMessage = "El Instagram admite hasta 100 caracteres")] string? Instagram,
    [MaxLength(500, ErrorMessage = "El mensaje del recibo admite hasta 500 caracteres")] string? MensajeRecibo
);

public record InventarioRequest(
    [Range(0, 100000, ErrorMessage = "El stock bajo por producto debe ser un número entero mayor o igual a 0")]
        int StockBajoProducto,
    [Range(0, 100000, ErrorMessage = "El stock bajo por talla debe ser un número entero mayor o igual a 0")]
        int StockBajoTalla
);

/// <summary>Discounts are whole percentages (0–100); banks in the order they should be offered.</summary>
public record PosRequest([Required] int[] Descuentos, [Required] string[] Bancos);

public record TallasRequest([Required] GrupoTallaDto[] Tallas);

public record TiposDocumentoRequest([Required] string[] TiposDocumento);
