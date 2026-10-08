using MySqlConnector;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Zapateria
{
    internal class Program
    {
        const string Servidor = "62.171.180.115";

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

                string? nombre = nombreOriginal?.Trim();
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
            var idMarcas = new Dictionary<string, int>();
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

        public static void Main()
        {
            try
            {
                MySqlConnection ConexionBBDD = EstablacerConexion();
                JsonNode? raiz = LeerJsonZapateria();
                Reiniciar(ConexionBBDD);
                CrearTablas(ConexionBBDD);
                ImportarMarcas(ConexionBBDD, raiz);
                ImportarProveedores(ConexionBBDD, raiz);
                ImportarProductos(ConexionBBDD, raiz);

            }
            catch (MySqlException ex)
            {
                Console.WriteLine($"Algo ha salido mal con la conexion Error: {ex.Message}");
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"Algo ha salido mal en los argumentos de la conexion.a");
            }
        }
    }
}
