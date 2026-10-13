using MySqlConnector;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Zapateria
{
    internal class Program
    {
        const string Servidor = "62.171.180.115";
        public record ProductosPorMarca(string Marca, long Total);
        public record ProductoSinStock(string Codigo, string Nombre);
        public record ImportePorMes(string Mes, decimal Importe);
        public record ProductoVendido(string Codigo, string Nombre, string Marca, long Unidades);
        public record FacturacionCategoriaPago(string Categoria, string FormaPago, decimal Importe);
        public record ClienteTop(string Codigo, string Nombre, long Tickets, decimal Importe);
        public record DiferenciaPrecio(long Lineas, decimal Diferencia);

        static string? _usuario;
        static string? _clave;
        private static readonly string _json = Path.Combine(AppContext.BaseDirectory, "zapateria.json");

        public static void PedirUsuario()
        {
           _usuario ??= Environment.GetEnvironmentVariable("ALUMNO_USER");
           if (string.IsNullOrWhiteSpace(_usuario))
           {
               Console.Write("Usuario (alumno_xx): ");
               _usuario = (Console.ReadLine() ?? "").Trim();
           }
        }
        public static void PedirClave()
        {
            _clave ??= Environment.GetEnvironmentVariable("ALUMNO_PASSWORD");
            if (string.IsNullOrWhiteSpace(_clave))
            {
                Console.Write("Contraseña: ");
                _clave = (Console.ReadLine() ?? "").Trim();
            }
        }

        public static MySqlConnectionStringBuilder ConexionString()
        {
            PedirUsuario();
            PedirClave();

            return new MySqlConnectionStringBuilder
            {
                Server = Servidor,
                Port = 3306,
                UserID = _usuario,
                Password = _clave,
                Database = _usuario,
                SslMode = MySqlSslMode.Preferred,
                MaximumPoolSize = 2,
            };
        }

        public static MySqlConnection EstablacerConexion()
        {
            var cadena = ConexionString().ConnectionString;
            MySqlConnection conexion = new MySqlConnection(cadena);
            try
            {
                conexion.Open();
                return conexion;
            }
            catch
            {
                conexion.Dispose();
                throw;
            }
        }

        public static JsonNode? LeerJsonZapateria()
        {
            JsonNode? jsonLeido = null;
            if (!File.Exists(_json))
            {
                Console.WriteLine("El archivo JSON no existe todavía.");
                return null;
            }

            try
            {
                string contenido = File.ReadAllText(_json);
                jsonLeido = JsonNode.Parse(contenido);
                return jsonLeido;
            }
            catch (JsonException ex)
            {
                Console.WriteLine("El archivo JSON no tiene un formato válido (¿se ha editado a mano y le " +
                               "falta alguna llave o coma?).");
                Console.WriteLine($"Detalle técnico: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"No se pudo leer: sin permisos sobre '{_json}'. ({ex.Message})");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"No se pudo leer el JSON (¿está bloqueado por otro programa?): {ex.Message}");
            }
            return jsonLeido;
        }

        public static void CrearTablas(MySqlConnection conexion)
        {
            using (var cmd = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS tienda (
                nombre VARCHAR(100) NOT NULL,
                ciudad VARCHAR(100) NOT NULL,
                ejercicio INT NOT NULL,
                nota VARCHAR(255) NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4", conexion))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS marcas (
                id_marca INT AUTO_INCREMENT PRIMARY KEY,
                nombre VARCHAR(100) NOT NULL UNIQUE,
                pais VARCHAR(100) NOT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4", conexion))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS proveedores (
                cif VARCHAR(20) PRIMARY KEY,
                nombre VARCHAR(150) NOT NULL,
                ciudad VARCHAR(100) NOT NULL,
                telefono VARCHAR(20) NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4", conexion))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS productos (
                codigo VARCHAR(10) PRIMARY KEY,
                nombre VARCHAR(150) NOT NULL,
                id_marca INT NOT NULL,
                categoria VARCHAR(50) NOT NULL,
                genero CHAR(1) NOT NULL,
                precio DECIMAL(10,2) NOT NULL CHECK (precio >= 0),
                CONSTRAINT fk_productos_marca FOREIGN KEY (id_marca) REFERENCES marcas(id_marca)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4", conexion))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS producto_proveedor (
                codigo VARCHAR(10) NOT NULL,
                cif VARCHAR(20) NOT NULL,
                PRIMARY KEY (codigo, cif),
                CONSTRAINT fk_pp_producto FOREIGN KEY (codigo) REFERENCES productos(codigo),
                CONSTRAINT fk_pp_proveedor FOREIGN KEY (cif) REFERENCES proveedores(cif)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4", conexion))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS producto_talla (
                codigo VARCHAR(10) NOT NULL,
                talla INT NOT NULL,
                stock INT NOT NULL CHECK (stock >= 0),
                PRIMARY KEY (codigo, talla),
                CONSTRAINT fk_pt_producto FOREIGN KEY (codigo) REFERENCES productos(codigo)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4", conexion))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS clientes (
                id VARCHAR(10) PRIMARY KEY,
                nombre VARCHAR(150) NOT NULL,
                ciudad VARCHAR(100) NOT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4", conexion))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS tickets (
                id_ticket INT AUTO_INCREMENT PRIMARY KEY,
                numero VARCHAR(20) NOT NULL,
                fecha DATE NOT NULL,
                hora TIME NOT NULL,
                vendedor VARCHAR(50) NOT NULL,
                forma_pago VARCHAR(20) NOT NULL,
                id_cliente VARCHAR(10) NULL,
                CONSTRAINT fk_tickets_cliente FOREIGN KEY (id_cliente) REFERENCES clientes(id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4", conexion))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS lineas_ticket (
                id_linea INT AUTO_INCREMENT PRIMARY KEY,
                id_ticket INT NOT NULL,
                codigo VARCHAR(10) NOT NULL,
                talla INT NOT NULL,
                unidades INT NOT NULL CHECK (unidades > 0),
                descuento_pct INT NULL CHECK (descuento_pct BETWEEN 0 AND 100),
                precio_unitario DECIMAL(10,2) NOT NULL CHECK (precio_unitario >= 0),
                CONSTRAINT fk_lineas_ticket FOREIGN KEY (id_ticket) REFERENCES tickets(id_ticket)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4", conexion))
            {
                cmd.ExecuteNonQuery();
            }

            Console.WriteLine("Tablas creadas (o ya existentes).");
        }

        public static void ImportarMarcas(MySqlConnection conexion, JsonNode? raiz)
        {
            if (raiz is null)
            {
                Console.WriteLine("No hay datos del JSON para importar.");
                return;
            }

            var marcas = new Dictionary<string, string>();
            int descartadas = 0;
            int conflictos = 0;
            int corregidas = 0;

            foreach (JsonNode? producto in raiz["productos"]!.AsArray())
            {
                string? nombreOriginal = producto?["marca"]?["nombre"]?.GetValue<string>();
                string? paisOriginal = producto?["marca"]?["pais"]?.GetValue<string>();

                string? nombre = string.IsNullOrWhiteSpace(nombreOriginal) ? null : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(nombreOriginal.Trim().ToLowerInvariant());
                string? pais = paisOriginal?.Trim();

                if (nombre != nombreOriginal || pais != paisOriginal)
                {
                    corregidas++;
                }

                if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(pais))
                {
                    descartadas++;
                    continue;
                }

                if (marcas.TryGetValue(nombre, out string? paisExistente))
                {
                    if (paisExistente != pais)
                    {
                        conflictos++;
                    }
                }
                else
                {
                    marcas[nombre] = pais;
                }
            }

            using var transaccion = conexion.BeginTransaction();
            try
            {
                const string sql = @"INSERT INTO marcas (nombre, pais) VALUES (@nombre, @pais)
                             ON DUPLICATE KEY UPDATE nombre = nombre";
                int nuevas = 0;

                foreach (KeyValuePair<string, string> marca in marcas)
                {
                    using var comando = new MySqlCommand(sql, conexion, transaccion);
                    comando.Parameters.AddWithValue("@nombre", marca.Key);
                    comando.Parameters.AddWithValue("@pais", marca.Value);
                    nuevas += comando.ExecuteNonQuery();
                }

                transaccion.Commit();
                Console.WriteLine($"Marcas: {nuevas} nuevas, {marcas.Count - nuevas} ya existentes, " +
                                  $"{descartadas} descartadas, {conflictos} con país distinto, " +
                                  $"{corregidas} corregidas (espacios).");
            }
            catch
            {
                transaccion.Rollback();
                throw;
            }
        }

        public static void Reiniciar(MySqlConnection conexion)
        {
            string[] TablasParaBorrar =
            {
                "lineas_ticket", "tickets", "clientes", "producto_talla",
                "producto_proveedor", "productos", "marcas", "proveedores", "tienda"
            };
            Console.Write("Esto borrará TODAS las tablas. Escribe SI para confirmar: ");
            if (Console.ReadLine()?.Trim() != "SI")
            {
                Console.WriteLine("Reinicio cancelado.");
                return;
            }

            foreach (string tabla in TablasParaBorrar)
            {
                // Nombres fijos de una lista interna, no datos de usuario
                using var comando = new MySqlCommand($"DROP TABLE IF EXISTS {tabla}", conexion);
                comando.ExecuteNonQuery();
            }
            Console.WriteLine("Base de datos reiniciada.");
        }
        public static void ImportarProveedores(MySqlConnection conexion, JsonNode? raiz)
        {
            if (raiz is null)
            {
                Console.WriteLine("No hay datos del JSON para importar.");
                return;
            }

            var proveedores = new Dictionary<string, (string Nombre, string Ciudad, string? Telefono)>();
            int descartados = 0;
            int conflictos = 0;
            int corregidos = 0;

            foreach (JsonNode? producto in raiz["productos"]!.AsArray())
            {
                foreach (JsonNode? proveedor in producto?["proveedores"]?.AsArray() ?? new JsonArray())
                {
                    string? cifOriginal = proveedor?["cif"]?.GetValue<string>();
                    string? nombreOriginal = proveedor?["nombre"]?.GetValue<string>();
                    string? ciudadOriginal = proveedor?["ciudad"]?.GetValue<string>();
                    string? telefonoOriginal = proveedor?["telefono"]?.GetValue<string>();

                    string? cif = cifOriginal?.Trim();
                    string? nombre = nombreOriginal?.Trim();
                    string? ciudad = ciudadOriginal?.Trim();
                    string? telefono = string.IsNullOrWhiteSpace(telefonoOriginal) ? null : telefonoOriginal.Trim();

                    if (cif != cifOriginal || nombre != nombreOriginal ||
                        ciudad != ciudadOriginal || telefono != telefonoOriginal)
                    {
                        corregidos++;
                    }

                    if (string.IsNullOrWhiteSpace(cif) || string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(ciudad))
                    {
                        descartados++;
                        continue;
                    }

                    var datos = (Nombre: nombre, Ciudad: ciudad, Telefono: telefono);

                    if (proveedores.TryGetValue(cif, out var existente))
                    {
                        if (existente != datos)
                        {
                            conflictos++;
                        }
                    }
                    else
                    {
                        proveedores[cif] = datos;
                    }
                }
            }

            using var transaccion = conexion.BeginTransaction();
            try
            {
                const string sql = @"INSERT INTO proveedores (cif, nombre, ciudad, telefono)
                             VALUES (@cif, @nombre, @ciudad, @telefono)
                             ON DUPLICATE KEY UPDATE cif = cif";
                int nuevos = 0;

                foreach (var proveedor in proveedores)
                {
                    using var comando = new MySqlCommand(sql, conexion, transaccion);
                    comando.Parameters.AddWithValue("@cif", proveedor.Key);
                    comando.Parameters.AddWithValue("@nombre", proveedor.Value.Nombre);
                    comando.Parameters.AddWithValue("@ciudad", proveedor.Value.Ciudad);
                    comando.Parameters.AddWithValue("@telefono", (object?)proveedor.Value.Telefono ?? DBNull.Value);
                    nuevos += comando.ExecuteNonQuery();
                }

                transaccion.Commit();
                Console.WriteLine($"Proveedores: {nuevos} nuevos, {proveedores.Count - nuevos} ya existentes, " +
                                  $"{descartados} descartados, {conflictos} con datos distintos, " +
                                  $"{corregidos} corregidos.");
            }
            catch
            {
                transaccion.Rollback();
                throw;
            }
        }

        public static void ImportarProductos(MySqlConnection conexion, JsonNode? raiz)
        {
            if (raiz is null)
            {
                Console.WriteLine("No hay datos del JSON para importar.");
                return;
            }

            // 1. Mapa nombre de marca -> id_marca, leído de la base de datos
            var idMarcas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            const string sqlMarcas = "SELECT id_marca, nombre FROM marcas";
            using (var comandoMarcas = new MySqlCommand(sqlMarcas, conexion))
            using (var lector = comandoMarcas.ExecuteReader())
            {
                while (lector.Read())
                {
                    idMarcas[lector.GetString("nombre")] = lector.GetInt32("id_marca");
                }
            }

            var productos = new Dictionary<string, (string Nombre, int IdMarca, string Categoria, string Genero, decimal Precio)>();
            int descartados = 0;
            int conflictos = 0;
            int duplicados = 0;
            int corregidos = 0;
            int preciosConComa = 0;

            foreach (JsonNode? producto in raiz["productos"]!.AsArray())
            {
                string? codigoOriginal = producto?["codigo"]?.GetValue<string>();
                string? nombreOriginal = producto?["nombre"]?.GetValue<string>();
                string? marcaOriginal = producto?["marca"]?["nombre"]?.GetValue<string>();
                string? categoriaOriginal = producto?["categoria"]?.GetValue<string>();
                string? generoOriginal = producto?["genero"]?.GetValue<string>();

                string? codigo = codigoOriginal?.Trim();
                string? nombre = nombreOriginal?.Trim();
                string? marca = marcaOriginal?.Trim();
                string? categoria = categoriaOriginal?.Trim();
                string? genero = generoOriginal?.Trim();

                if (codigo != codigoOriginal || nombre != nombreOriginal || marca != marcaOriginal ||
                    categoria != categoriaOriginal || genero != generoOriginal)
                {
                    corregidos++;
                }

                // El precio puede ser un número o un texto con coma ("82,95")
                decimal? precio = null;
                if (producto?["precio"] is JsonValue valorPrecio)
                {
                    if (valorPrecio.TryGetValue(out decimal numero))
                    {
                        precio = numero;
                    }
                    else if (valorPrecio.TryGetValue(out string? texto) &&
                             decimal.TryParse(texto?.Trim().Replace(',', '.'),
                                              System.Globalization.NumberStyles.Number,
                                              System.Globalization.CultureInfo.InvariantCulture,
                                              out decimal convertido))
                    {
                        precio = convertido;
                        preciosConComa++;
                    }
                }

                if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(nombre) ||
                    string.IsNullOrWhiteSpace(marca) || string.IsNullOrWhiteSpace(categoria) ||
                    string.IsNullOrWhiteSpace(genero) || precio is null || precio < 0)
                {
                    descartados++;
                    continue;
                }

                if (!idMarcas.TryGetValue(marca, out int idMarca))
                {
                    descartados++;
                    continue;
                }

                var datos = (Nombre: nombre, IdMarca: idMarca, Categoria: categoria, Genero: genero, Precio: precio.Value);

                if (productos.TryGetValue(codigo, out var existente))
                {
                    if (existente == datos)
                    {
                        duplicados++;
                    }
                    else
                    {
                        conflictos++;
                    }
                }
                else
                {
                    productos[codigo] = datos;
                }
            }

            // 2. Inserción en una transacción
            using var transaccion = conexion.BeginTransaction();
            try
            {
                const string sql = @"INSERT INTO productos (codigo, nombre, id_marca, categoria, genero, precio)
                             VALUES (@codigo, @nombre, @idMarca, @categoria, @genero, @precio)
                             ON DUPLICATE KEY UPDATE codigo = codigo";
                int nuevos = 0;

                foreach (var producto in productos)
                {
                    using var comando = new MySqlCommand(sql, conexion, transaccion);
                    comando.Parameters.AddWithValue("@codigo", producto.Key);
                    comando.Parameters.AddWithValue("@nombre", producto.Value.Nombre);
                    comando.Parameters.AddWithValue("@idMarca", producto.Value.IdMarca);
                    comando.Parameters.AddWithValue("@categoria", producto.Value.Categoria);
                    comando.Parameters.AddWithValue("@genero", producto.Value.Genero);
                    comando.Parameters.AddWithValue("@precio", producto.Value.Precio);
                    nuevos += comando.ExecuteNonQuery();
                }

                transaccion.Commit();
                Console.WriteLine($"Productos: {nuevos} nuevos, {productos.Count - nuevos} ya existentes, " +
                                  $"{duplicados} duplicados idénticos, {conflictos} con datos distintos, " +
                                  $"{descartados} descartados, {corregidos} con espacios corregidos, " +
                                  $"{preciosConComa} precios convertidos de texto con coma.");
            }
            catch
            {
                transaccion.Rollback();
                throw;
            }
        }

        public static void ImportarProductoProveedor(MySqlConnection conexion, JsonNode? raiz)
        {
            if (raiz is null)
            {
                Console.WriteLine("No hay datos del JSON para importar.");
                return;
            }

            // 1. Códigos de producto y CIF de proveedor que existen en la base
            var codigosExistentes = new HashSet<string>();
            using (var comandoProductos = new MySqlCommand("SELECT codigo FROM productos", conexion))
            using (var lector = comandoProductos.ExecuteReader())
            {
                while (lector.Read())
                {
                    codigosExistentes.Add(lector.GetString("codigo"));
                }
            }

            var cifsExistentes = new HashSet<string>();
            using (var comandoProveedores = new MySqlCommand("SELECT cif FROM proveedores", conexion))
            using (var lector = comandoProveedores.ExecuteReader())
            {
                while (lector.Read())
                {
                    cifsExistentes.Add(lector.GetString("cif"));
                }
            }

            // 2. Relaciones válidas, sin repetir
            var relaciones = new HashSet<(string Codigo, string Cif)>();
            int descartadas = 0;
            int duplicadas = 0;
            int corregidas = 0;

            foreach (JsonNode? producto in raiz["productos"]!.AsArray())
            {
                string? codigoOriginal = producto?["codigo"]?.GetValue<string>();
                string? codigo = codigoOriginal?.Trim();

                if (codigo != codigoOriginal)
                {
                    corregidas++;
                }

                foreach (JsonNode? proveedor in producto?["proveedores"]?.AsArray() ?? new JsonArray())
                {
                    string? cifOriginal = proveedor?["cif"]?.GetValue<string>();
                    string? cif = cifOriginal?.Trim();

                    if (cif != cifOriginal)
                    {
                        corregidas++;
                    }

                    if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(cif) ||
                        !codigosExistentes.Contains(codigo) || !cifsExistentes.Contains(cif))
                    {
                        descartadas++;
                        continue;
                    }

                    if (!relaciones.Add((codigo, cif)))
                    {
                        duplicadas++;
                    }
                }
            }

            // 3. Inserción en una transacción
            using var transaccion = conexion.BeginTransaction();
            try
            {
                const string sql = @"INSERT INTO producto_proveedor (codigo, cif) VALUES (@codigo, @cif)
                             ON DUPLICATE KEY UPDATE codigo = codigo";
                int nuevas = 0;

                foreach (var relacion in relaciones)
                {
                    using var comando = new MySqlCommand(sql, conexion, transaccion);
                    comando.Parameters.AddWithValue("@codigo", relacion.Codigo);
                    comando.Parameters.AddWithValue("@cif", relacion.Cif);
                    nuevas += comando.ExecuteNonQuery();
                }

                transaccion.Commit();
                Console.WriteLine($"Producto-proveedor: {nuevas} nuevas, {relaciones.Count - nuevas} ya existentes, " +
                                  $"{duplicadas} duplicadas en el JSON, {descartadas} descartadas por referencia inexistente, " +
                                  $"{corregidas} con espacios corregidos.");
            }
            catch
            {
                transaccion.Rollback();
                throw;
            }
        }

        public static void ImportarProductoTalla(MySqlConnection conexion, JsonNode? raiz)
        {
            if (raiz is null)
            {
                Console.WriteLine("No hay datos del JSON para importar.");
                return;
            }

            // 1. Códigos de producto que existen en la base
            var codigosExistentes = new HashSet<string>();
            using (var comandoProductos = new MySqlCommand("SELECT codigo FROM productos", conexion))
            using (var lector = comandoProductos.ExecuteReader())
            {
                while (lector.Read())
                {
                    codigosExistentes.Add(lector.GetString("codigo"));
                }
            }

            // 2. Stock por (codigo, talla), sin repetir
            var stocks = new Dictionary<(string Codigo, int Talla), int>();
            int descartadas = 0;
            int duplicadas = 0;
            int conflictos = 0;
            int corregidas = 0;

            foreach (JsonNode? producto in raiz["productos"]!.AsArray())
            {
                string? codigoOriginal = producto?["codigo"]?.GetValue<string>();
                string? codigo = codigoOriginal?.Trim();

                if (codigo != codigoOriginal)
                {
                    corregidas++;
                }

                foreach (JsonNode? talla in producto?["tallas"]?.AsArray() ?? new JsonArray())
                {
                    int? valorTalla = talla?["talla"]?.GetValue<int>();
                    int? stock = talla?["stock"]?.GetValue<int>();

                    if (string.IsNullOrWhiteSpace(codigo) || !codigosExistentes.Contains(codigo) ||
                        valorTalla is null || stock is null || stock < 0)
                    {
                        descartadas++;
                        continue;
                    }

                    var clave = (Codigo: codigo, Talla: valorTalla.Value);

                    if (stocks.TryGetValue(clave, out int stockExistente))
                    {
                        if (stockExistente == stock.Value)
                        {
                            duplicadas++;
                        }
                        else
                        {
                            conflictos++;
                        }
                    }
                    else
                    {
                        stocks[clave] = stock.Value;
                    }
                }
            }

            // 3. Inserción en una transacción
            using var transaccion = conexion.BeginTransaction();
            try
            {
                const string sql = @"INSERT INTO producto_talla (codigo, talla, stock) VALUES (@codigo, @talla, @stock)
                             ON DUPLICATE KEY UPDATE codigo = codigo";
                int nuevas = 0;

                foreach (var entrada in stocks)
                {
                    using var comando = new MySqlCommand(sql, conexion, transaccion);
                    comando.Parameters.AddWithValue("@codigo", entrada.Key.Codigo);
                    comando.Parameters.AddWithValue("@talla", entrada.Key.Talla);
                    comando.Parameters.AddWithValue("@stock", entrada.Value);
                    nuevas += comando.ExecuteNonQuery();
                }

                transaccion.Commit();
                Console.WriteLine($"Producto-talla: {nuevas} nuevas, {stocks.Count - nuevas} ya existentes, " +
                                  $"{duplicadas} duplicadas en el JSON, {conflictos} con stock distinto, " +
                                  $"{descartadas} descartadas (talla nula, stock inválido o producto inexistente), " +
                                  $"{corregidas} con espacios corregidos.");
            }
            catch
            {
                transaccion.Rollback();
                throw;
            }
        }
        public static void ImportarTienda(MySqlConnection conexion, JsonNode? raiz)
        {
            if (raiz is null)
            {
                Console.WriteLine("No hay datos del JSON para importar.");
                return;
            }

            JsonNode? tienda = raiz["tienda"];
            if (tienda is null)
            {
                Console.WriteLine("El JSON no contiene el bloque 'tienda'.");
                return;
            }

            string? nombre = tienda["nombre"]?.GetValue<string>()?.Trim();
            string? ciudad = tienda["ciudad"]?.GetValue<string>()?.Trim();
            int? ejercicio = tienda["ejercicio"]?.GetValue<int>();
            string? nota = tienda["nota"]?.GetValue<string>()?.Trim();

            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(ciudad) || ejercicio is null)
            {
                Console.WriteLine("Datos de tienda incompletos. No se importa.");
                return;
            }

            using var transaccion = conexion.BeginTransaction();
            try
            {
                // La tabla no tiene clave única: comprobamos si ya hay una fila
                using (var comprobar = new MySqlCommand("SELECT COUNT(*) FROM tienda", conexion, transaccion))
                {
                    long existentes = Convert.ToInt64(comprobar.ExecuteScalar());
                    if (existentes > 0)
                    {
                        transaccion.Commit();
                        Console.WriteLine("Tienda: ya estaba importada, no se inserta de nuevo.");
                        return;
                    }
                }

                const string sql = @"INSERT INTO tienda (nombre, ciudad, ejercicio, nota)
                             VALUES (@nombre, @ciudad, @ejercicio, @nota)";
                using (var comando = new MySqlCommand(sql, conexion, transaccion))
                {
                    comando.Parameters.AddWithValue("@nombre", nombre);
                    comando.Parameters.AddWithValue("@ciudad", ciudad);
                    comando.Parameters.AddWithValue("@ejercicio", ejercicio.Value);
                    comando.Parameters.AddWithValue("@nota", (object?)nota ?? DBNull.Value);
                    comando.ExecuteNonQuery();
                }

                transaccion.Commit();
                Console.WriteLine($"Tienda: 1 fila insertada ({nombre}, {ciudad}, {ejercicio.Value}).");
            }
            catch
            {
                transaccion.Rollback();
                throw;
            }
        }
        public static void ImportarClientes(MySqlConnection conexion, JsonNode? raiz)
        {
            if (raiz is null)
            {
                Console.WriteLine("No hay datos del JSON para importar.");
                return;
            }

            // Los clientes no tienen lista propia en el JSON: se sacan de cada ticket
            var clientes = new Dictionary<string, (string Nombre, string Ciudad)>();
            int sinCliente = 0;
            int descartados = 0;
            int conflictos = 0;
            int duplicados = 0;
            int corregidos = 0;

            foreach (JsonNode? ticket in raiz["tickets"]!.AsArray())
            {
                JsonNode? cliente = ticket?["cliente"];

                if (cliente is null)
                {
                    sinCliente++;
                    continue;
                }

                string? idOriginal = cliente["id"]?.GetValue<string>();
                string? nombreOriginal = cliente["nombre"]?.GetValue<string>();
                string? ciudadOriginal = cliente["ciudad"]?.GetValue<string>();

                string? id = idOriginal?.Trim();
                string? nombre = nombreOriginal?.Trim();
                string? ciudad = ciudadOriginal?.Trim();

                if (id != idOriginal || nombre != nombreOriginal || ciudad != ciudadOriginal)
                {
                    corregidos++;
                }

                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(ciudad))
                {
                    descartados++;
                    continue;
                }

                var datos = (Nombre: nombre, Ciudad: ciudad);

                if (clientes.TryGetValue(id, out var existente))
                {
                    if (existente == datos)
                    {
                        duplicados++;
                    }
                    else
                    {
                        conflictos++;
                    }
                }
                else
                {
                    clientes[id] = datos;
                }
            }

            using var transaccion = conexion.BeginTransaction();
            try
            {
                const string sql = @"INSERT INTO clientes (id, nombre, ciudad) VALUES (@id, @nombre, @ciudad)
                             ON DUPLICATE KEY UPDATE id = id";
                int nuevos = 0;

                foreach (var cliente in clientes)
                {
                    using var comando = new MySqlCommand(sql, conexion, transaccion);
                    comando.Parameters.AddWithValue("@id", cliente.Key);
                    comando.Parameters.AddWithValue("@nombre", cliente.Value.Nombre);
                    comando.Parameters.AddWithValue("@ciudad", cliente.Value.Ciudad);
                    nuevos += comando.ExecuteNonQuery();
                }

                transaccion.Commit();
                Console.WriteLine($"Clientes: {nuevos} nuevos, {clientes.Count - nuevos} ya existentes, " +
                                  $"{sinCliente} tickets sin cliente, {duplicados} duplicados en el JSON, " +
                                  $"{conflictos} con datos distintos, {descartados} descartados, " +
                                  $"{corregidos} con espacios corregidos.");
            }
            catch
            {
                transaccion.Rollback();
                throw;
            }
        }
        public static void ImportarTickets(MySqlConnection conexion, JsonNode? raiz)
        {
            if (raiz is null)
            {
                Console.WriteLine("No hay datos del JSON para importar.");
                return;
            }

            // 1. Números de ticket que ya están en la base (la columna no es UNIQUE)
            var numerosExistentes = new HashSet<string>();
            using (var comandoTickets = new MySqlCommand("SELECT numero FROM tickets", conexion))
            using (var lector = comandoTickets.ExecuteReader())
            {
                while (lector.Read())
                {
                    numerosExistentes.Add(lector.GetString("numero"));
                }
            }

            string[] formatosFecha = { "yyyy-MM-dd", "dd/MM/yyyy" };
            string[] formatosHora = { "hh\\:mm", "hh\\:mm\\:ss" };

            var tickets = new Dictionary<string, (DateTime Fecha, TimeSpan Hora, string Vendedor, string FormaPago, string? IdCliente)>();
            int descartados = 0;
            int yaExistentes = 0;
            int duplicados = 0;
            int conflictos = 0;
            int corregidos = 0;
            int fechasConvertidas = 0;

            foreach (JsonNode? ticket in raiz["tickets"]!.AsArray())
            {
                string? numeroOriginal = ticket?["numero"]?.GetValue<string>();
                string? fechaOriginal = ticket?["fecha"]?.GetValue<string>();
                string? horaOriginal = ticket?["hora"]?.GetValue<string>();
                string? vendedorOriginal = ticket?["vendedor"]?.GetValue<string>();
                string? formaPagoOriginal = ticket?["forma_pago"]?.GetValue<string>();
                string? idClienteOriginal = ticket?["cliente"]?["id"]?.GetValue<string>();

                string? numero = numeroOriginal?.Trim();
                string? fechaTexto = fechaOriginal?.Trim();
                string? horaTexto = horaOriginal?.Trim();
                string? vendedor = vendedorOriginal?.Trim();
                string? formaPago = formaPagoOriginal?.Trim();
                string? idCliente = string.IsNullOrWhiteSpace(idClienteOriginal) ? null : idClienteOriginal.Trim();

                if (numero != numeroOriginal || fechaTexto != fechaOriginal || horaTexto != horaOriginal ||
                    vendedor != vendedorOriginal || formaPago != formaPagoOriginal || idCliente != idClienteOriginal)
                {
                    corregidos++;
                }

                if (string.IsNullOrWhiteSpace(numero) || string.IsNullOrWhiteSpace(fechaTexto) ||
                    string.IsNullOrWhiteSpace(horaTexto) || string.IsNullOrWhiteSpace(vendedor) ||
                    string.IsNullOrWhiteSpace(formaPago))
                {
                    descartados++;
                    continue;
                }

                if (!DateTime.TryParseExact(fechaTexto, formatosFecha, System.Globalization.CultureInfo.InvariantCulture,
                                            System.Globalization.DateTimeStyles.None, out DateTime fecha) ||
                    !TimeSpan.TryParseExact(horaTexto, formatosHora, System.Globalization.CultureInfo.InvariantCulture,
                                            out TimeSpan hora))
                {
                    descartados++;
                    continue;
                }

                if (fechaTexto.Contains('/'))
                {
                    fechasConvertidas++;
                }

                if (numerosExistentes.Contains(numero))
                {
                    yaExistentes++;
                    continue;
                }

                var datos = (Fecha: fecha, Hora: hora, Vendedor: vendedor, FormaPago: formaPago, IdCliente: idCliente);

                if (tickets.TryGetValue(numero, out var existente))
                {
                    if (existente == datos)
                    {
                        duplicados++;
                    }
                    else
                    {
                        conflictos++;
                    }
                }
                else
                {
                    tickets[numero] = datos;
                }
            }

            // 2. Inserción en una transacción
            using var transaccion = conexion.BeginTransaction();
            try
            {
                const string sql = @"INSERT INTO tickets (numero, fecha, hora, vendedor, forma_pago, id_cliente)
                             VALUES (@numero, @fecha, @hora, @vendedor, @formaPago, @idCliente)";
                int nuevos = 0;

                foreach (var ticket in tickets)
                {
                    using var comando = new MySqlCommand(sql, conexion, transaccion);
                    comando.Parameters.AddWithValue("@numero", ticket.Key);
                    comando.Parameters.AddWithValue("@fecha", ticket.Value.Fecha);
                    comando.Parameters.AddWithValue("@hora", ticket.Value.Hora);
                    comando.Parameters.AddWithValue("@vendedor", ticket.Value.Vendedor);
                    comando.Parameters.AddWithValue("@formaPago", ticket.Value.FormaPago);
                    comando.Parameters.AddWithValue("@idCliente", (object?)ticket.Value.IdCliente ?? DBNull.Value);
                    nuevos += comando.ExecuteNonQuery();
                }

                transaccion.Commit();
                Console.WriteLine($"Tickets: {nuevos} nuevos, {yaExistentes} ya existentes, " +
                                  $"{duplicados} duplicados idénticos en el JSON, {conflictos} con datos distintos, " +
                                  $"{descartados} descartados, {fechasConvertidas} fechas convertidas de dd/MM/aaaa, " +
                                  $"{corregidos} con espacios corregidos.");
            }
            catch
            {
                transaccion.Rollback();
                throw;
            }
        }
        public static void ImportarLineasTicket(MySqlConnection conexion, JsonNode? raiz)
        {
            if (raiz is null)
            {
                Console.WriteLine("No hay datos del JSON para importar.");
                return;
            }

            // 1. Id de cada ticket según su número
            var idTickets = new Dictionary<string, int>();
            using (var comandoTickets = new MySqlCommand("SELECT id_ticket, numero FROM tickets", conexion))
            using (var lector = comandoTickets.ExecuteReader())
            {
                while (lector.Read())
                {
                    idTickets[lector.GetString("numero")] = lector.GetInt32("id_ticket");
                }
            }

            // 2. Códigos de producto del catálogo (solo para contar, no para descartar)
            var codigosProducto = new HashSet<string>();
            using (var comandoProductos = new MySqlCommand("SELECT codigo FROM productos", conexion))
            using (var lector = comandoProductos.ExecuteReader())
            {
                while (lector.Read())
                {
                    codigosProducto.Add(lector.GetString("codigo"));
                }
            }

            // 3. Tickets que ya tienen líneas, para no duplicarlas al repetir la importación
            var ticketsConLineas = new HashSet<int>();
            using (var comandoLineas = new MySqlCommand("SELECT DISTINCT id_ticket FROM lineas_ticket", conexion))
            using (var lector = comandoLineas.ExecuteReader())
            {
                while (lector.Read())
                {
                    ticketsConLineas.Add(lector.GetInt32("id_ticket"));
                }
            }

            var lineas = new List<(int IdTicket, string Codigo, int Talla, int Unidades, int? Descuento, decimal Precio)>();
            int ticketsOmitidos = 0;
            int ticketsNoEncontrados = 0;
            int descartadas = 0;
            int sinProducto = 0;
            int corregidas = 0;

            foreach (JsonNode? ticket in raiz["tickets"]!.AsArray())
            {
                string? numeroOriginal = ticket?["numero"]?.GetValue<string>();
                string? numero = numeroOriginal?.Trim();

                if (numero != numeroOriginal)
                {
                    corregidas++;
                }

                if (string.IsNullOrWhiteSpace(numero) || !idTickets.TryGetValue(numero, out int idTicket))
                {
                    ticketsNoEncontrados++;
                    continue;
                }

                if (ticketsConLineas.Contains(idTicket))
                {
                    ticketsOmitidos++;
                    continue;
                }

                foreach (JsonNode? linea in ticket?["lineas"]?.AsArray() ?? new JsonArray())
                {
                    string? codigoOriginal = linea?["codigo"]?.GetValue<string>();
                    string? codigo = codigoOriginal?.Trim();

                    if (codigo != codigoOriginal)
                    {
                        corregidas++;
                    }

                    int? talla = linea?["talla"]?.GetValue<int>();
                    int? unidades = linea?["unidades"]?.GetValue<int>();
                    int? descuento = linea?["descuento_pct"]?.GetValue<int>();
                    decimal? precio = linea?["precio_unitario"]?.GetValue<decimal>();

                    if (string.IsNullOrWhiteSpace(codigo) || talla is null || unidades is null || unidades <= 0 ||
                        precio is null || precio < 0 || (descuento is not null && (descuento < 0 || descuento > 100)))
                    {
                        descartadas++;
                        continue;
                    }

                    if (!codigosProducto.Contains(codigo))
                    {
                        sinProducto++;
                    }

                    lineas.Add((idTicket, codigo, talla.Value, unidades.Value, descuento, precio.Value));
                }
            }

            // 4. Inserción en una transacción
            using var transaccion = conexion.BeginTransaction();
            try
            {
                const string sql = @"INSERT INTO lineas_ticket (id_ticket, codigo, talla, unidades, descuento_pct, precio_unitario)
                             VALUES (@idTicket, @codigo, @talla, @unidades, @descuento, @precio)";
                int nuevas = 0;

                foreach (var linea in lineas)
                {
                    using var comando = new MySqlCommand(sql, conexion, transaccion);
                    comando.Parameters.AddWithValue("@idTicket", linea.IdTicket);
                    comando.Parameters.AddWithValue("@codigo", linea.Codigo);
                    comando.Parameters.AddWithValue("@talla", linea.Talla);
                    comando.Parameters.AddWithValue("@unidades", linea.Unidades);
                    comando.Parameters.AddWithValue("@descuento", (object?)linea.Descuento ?? DBNull.Value);
                    comando.Parameters.AddWithValue("@precio", linea.Precio);
                    nuevas += comando.ExecuteNonQuery();
                }

                transaccion.Commit();
                Console.WriteLine($"Líneas de ticket: {nuevas} nuevas, {ticketsOmitidos} tickets omitidos (ya tenían líneas), " +
                                  $"{ticketsNoEncontrados} tickets no encontrados, {descartadas} descartadas, " +
                                  $"{sinProducto} con código sin producto en catálogo (conservadas), " +
                                  $"{corregidas} con espacios corregidos.");
            }
            catch
            {
                transaccion.Rollback();
                throw;
            }
        }
        public static void ImportarTablas(MySqlConnection conexion, JsonNode? raiz)
        {
            ImportarTienda(conexion, raiz);
            ImportarMarcas(conexion, raiz);
            ImportarProveedores(conexion, raiz);
            ImportarProductos(conexion, raiz);
            ImportarProductoProveedor(conexion, raiz);
            ImportarProductoTalla(conexion, raiz);
            ImportarClientes(conexion, raiz);
            ImportarTickets(conexion, raiz);
            ImportarLineasTicket(conexion, raiz);
        }

        public static List<ProductosPorMarca> ConsultarProductosPorMarca(MySqlConnection conexion)
        {
            var resultado = new List<ProductosPorMarca>();

            const string sql = @"SELECT m.nombre, COUNT(p.codigo) AS total
                         FROM marcas m
                         LEFT JOIN productos p ON p.id_marca = m.id_marca
                         GROUP BY m.id_marca, m.nombre
                         ORDER BY total DESC, m.nombre";

            using var comando = new MySqlCommand(sql, conexion);
            using var lector = comando.ExecuteReader();

            while (lector.Read())
            {
                resultado.Add(new ProductosPorMarca(lector.GetString("nombre"), lector.GetInt64("total")));
            }

            return resultado;
        }
        public static List<ProductoSinStock> ConsultarProductosSinStock(MySqlConnection conexion)
        {
            var resultado = new List<ProductoSinStock>();

            const string sql = @"SELECT p.codigo, p.nombre
                         FROM productos p
                         WHERE NOT EXISTS (
                             SELECT 1
                             FROM producto_talla pt
                             WHERE pt.codigo = p.codigo AND pt.stock > 0
                         )
                         ORDER BY p.codigo";

            using var comando = new MySqlCommand(sql, conexion);
            using var lector = comando.ExecuteReader();

            while (lector.Read())
            {
                resultado.Add(new ProductoSinStock(lector.GetString("codigo"), lector.GetString("nombre")));
            }

            return resultado;
        }
        public static List<ImportePorMes> ConsultarImportePorMes(MySqlConnection conexion)
        {
            var resultado = new List<ImportePorMes>();

            const string sql = @"SELECT DATE_FORMAT(t.fecha, '%Y-%m') AS mes,
                                ROUND(SUM(l.unidades * l.precio_unitario * (1 - COALESCE(l.descuento_pct, 0) / 100)), 2) AS importe
                         FROM lineas_ticket l
                         JOIN tickets t ON t.id_ticket = l.id_ticket
                         GROUP BY mes
                         ORDER BY mes";

            using var comando = new MySqlCommand(sql, conexion);
            using var lector = comando.ExecuteReader();

            while (lector.Read())
            {
                resultado.Add(new ImportePorMes(lector.GetString("mes"), lector.GetDecimal("importe")));
            }

            return resultado;
        }

        public static List<ProductoVendido> ConsultarTopProductosVendidos(MySqlConnection conexion)
        {
            var resultado = new List<ProductoVendido>();

            const string sql = @"SELECT p.codigo, p.nombre, m.nombre AS marca,
                                CAST(SUM(l.unidades) AS SIGNED) AS unidades
                         FROM lineas_ticket l
                         JOIN productos p ON p.codigo = l.codigo
                         JOIN marcas m ON m.id_marca = p.id_marca
                         GROUP BY p.codigo, p.nombre, m.nombre
                         ORDER BY unidades DESC, p.codigo
                         LIMIT 5";

            using var comando = new MySqlCommand(sql, conexion);
            using var lector = comando.ExecuteReader();

            while (lector.Read())
            {
                resultado.Add(new ProductoVendido(
                    lector.GetString("codigo"),
                    lector.GetString("nombre"),
                    lector.GetString("marca"),
                    lector.GetInt64("unidades")));
            }

            return resultado;
        }
        public static List<FacturacionCategoriaPago> ConsultarFacturacionCategoriaPago(MySqlConnection conexion)
        {
            var resultado = new List<FacturacionCategoriaPago>();

            const string sql = @"SELECT COALESCE(p.categoria, 'Sin catálogo') AS categoria,
                                t.forma_pago,
                                ROUND(SUM(l.unidades * l.precio_unitario * (1 - COALESCE(l.descuento_pct, 0) / 100)), 2) AS importe
                         FROM lineas_ticket l
                         JOIN tickets t ON t.id_ticket = l.id_ticket
                         LEFT JOIN productos p ON p.codigo = l.codigo
                         GROUP BY categoria, t.forma_pago
                         ORDER BY categoria, t.forma_pago";

            using var comando = new MySqlCommand(sql, conexion);
            using var lector = comando.ExecuteReader();

            while (lector.Read())
            {
                resultado.Add(new FacturacionCategoriaPago(
                    lector.GetString("categoria"),
                    lector.GetString("forma_pago"),
                    lector.GetDecimal("importe")));
            }

            return resultado;
        }

        public static List<ClienteTop> ConsultarTopClientes(MySqlConnection conexion)
        {
            var resultado = new List<ClienteTop>();

            const string sql = @"SELECT c.id, c.nombre,
                                COUNT(DISTINCT t.id_ticket) AS tickets,
                                ROUND(SUM(l.unidades * l.precio_unitario * (1 - COALESCE(l.descuento_pct, 0) / 100)), 2) AS importe
                         FROM clientes c
                         JOIN tickets t ON t.id_cliente = c.id
                         JOIN lineas_ticket l ON l.id_ticket = t.id_ticket
                         GROUP BY c.id, c.nombre
                         ORDER BY importe DESC, c.id
                         LIMIT 10";

            using var comando = new MySqlCommand(sql, conexion);
            using var lector = comando.ExecuteReader();

            while (lector.Read())
            {
                resultado.Add(new ClienteTop(
                    lector.GetString("id"),
                    lector.GetString("nombre"),
                    lector.GetInt64("tickets"),
                    lector.GetDecimal("importe")));
            }

            return resultado;
        }

        public static DiferenciaPrecio ConsultarDiferenciaPrecio(MySqlConnection conexion)
        {
            const string sql = @"SELECT COUNT(*) AS lineas,
                                COALESCE(ROUND(SUM(l.unidades * (p.precio - l.precio_unitario)), 2), 0) AS diferencia
                         FROM lineas_ticket l
                         JOIN productos p ON p.codigo = l.codigo
                         WHERE l.precio_unitario <> p.precio";

            using var comando = new MySqlCommand(sql, conexion);
            using var lector = comando.ExecuteReader();

            lector.Read();
            return new DiferenciaPrecio(lector.GetInt64("lineas"), lector.GetDecimal("diferencia"));
        }

        public static void Main()
        {
            try
            {
                JsonNode? raiz = LeerJsonZapateria();
                if (raiz is null)
                {
                    Console.WriteLine("No se pudo cargar el JSON. Se cancela el programa.");
                    return;
                }

                using MySqlConnection ConexionBBDD = EstablacerConexion();

                CrearTablas(ConexionBBDD);
                ImportarTablas(ConexionBBDD, raiz);

                Console.WriteLine();
                Console.WriteLine("P01 - Productos por marca:");
                foreach (ProductosPorMarca fila in ConsultarProductosPorMarca(ConexionBBDD))
                {
                    Console.WriteLine($"{fila.Marca}: {fila.Total} productos");
                }
                Console.WriteLine();
                Console.WriteLine("P02 - Productos sin stock en ninguna talla:");
                foreach (ProductoSinStock fila in ConsultarProductosSinStock(ConexionBBDD))
                {
                    Console.WriteLine($"{fila.Codigo} - {fila.Nombre}");
                }
                Console.WriteLine();
                Console.WriteLine("P03 - Importe facturado por mes:");
                foreach (ImportePorMes fila in ConsultarImportePorMes(ConexionBBDD))
                {
                    Console.WriteLine($"{fila.Mes}: {fila.Importe:N2} €");
                }

                Console.WriteLine();
                Console.WriteLine("P04 - Los 5 productos más vendidos en unidades:");
                foreach (ProductoVendido fila in ConsultarTopProductosVendidos(ConexionBBDD))
                {
                    Console.WriteLine($"{fila.Codigo} - {fila.Nombre} ({fila.Marca}): {fila.Unidades} unidades");
                }
                Console.WriteLine();
                Console.WriteLine("P05 - Facturación por categoría y forma de pago:");
                foreach (FacturacionCategoriaPago fila in ConsultarFacturacionCategoriaPago(ConexionBBDD))
                {
                    Console.WriteLine($"{fila.Categoria} / {fila.FormaPago}: {fila.Importe:N2} €");
                }

                Console.WriteLine();
                Console.WriteLine("P06 - Los 10 clientes que más han gastado:");
                foreach (ClienteTop fila in ConsultarTopClientes(ConexionBBDD))
                {
                    Console.WriteLine($"{fila.Codigo} - {fila.Nombre}: {fila.Tickets} tickets, {fila.Importe:N2} €");
                }

                Console.WriteLine();
                Console.WriteLine("P07 - Líneas vendidas a precio distinto del de catálogo:");
                DiferenciaPrecio diferencia = ConsultarDiferenciaPrecio(ConexionBBDD);
                Console.WriteLine($"{diferencia.Lineas} líneas, diferencia total: {diferencia.Diferencia:N2} €");
            }
            catch (MySqlException ex)
            {
                Console.WriteLine($"Error de MySQL ({ex.ErrorCode}): {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Configuración de conexión no válida: {ex.Message}");
            }
        }
    }
}
