# Distribuidora · .NET 10 + Entity Framework Core + SQL Server

Aplicación de consola que traslada a EF Core la base de datos **Distribuidora** (`distribuidora_sqlserver.sql`):
modela las 7 tablas con Fluent API, crea el esquema mediante migraciones, siembra los mismos datos dummy del
script de forma idempotente y ejecuta un recorrido de consultas LINQ (listados, filtros, relaciones y agregaciones).

Funciona en dos modos, seleccionados por configuración:

| Modo | Base de datos | Cómo se configura |
|------|---------------|-------------------|
| **A · Local** | SQL Server instalado o LocalDB | `appsettings.json`, user-secrets o variable `ConnectionStrings__Distribuidora` |
| **B · Docker** | Contenedor `mssql/server:2022-latest` | `docker-compose.yml` + `.env` (entorno `Docker`) |

---

## Estructura del proyecto

```
Distribuidora/
├── Distribuidora.csproj              # net10.0 + paquetes NuGet
├── Program.cs                        # Host, DI, arranque (migrar → sembrar) y consultas LINQ
├── appsettings.json                  # Configuración base (modo local)
├── appsettings.Docker.json           # Sobrescrituras del entorno Docker
├── Properties/launchSettings.json    # `dotnet run` usa el entorno Development
├── .config/dotnet-tools.json         # Herramienta local dotnet-ef 10
├── Data/
│   ├── Entities/                     # POCOs sin atributos (Categoria, Proveedor, Producto, …)
│   ├── DistribuidoraContext.cs       # DbContext: todo el mapeo en OnModelCreating (Fluent API)
│   ├── DataSeeder.cs                 # Sembrado idempotente, transaccional y con reintentos
│   ├── PersistenceSetup.cs           # Registro en DI + opciones de SQL Server (compartido)
│   └── DistribuidoraContextDesignTimeFactory.cs   # Usado por `dotnet ef`
├── Infrastructure/
│   ├── StartupOptions.cs             # Sección "Startup" de appsettings
│   └── ConsoleTable.cs               # Impresión de tablas en consola
├── Migrations/
│   ├── 20261022190000_InitialCreate.cs
│   └── DistribuidoraContextModelSnapshot.cs
├── Dockerfile                        # Multi-stage: sdk:10.0 → runtime:10.0, usuario no-root
├── docker-compose.yml                # sqlserver (healthcheck + volumen) + app
└── .env                              # Contraseña SA y puerto para compose
```

## Del script SQL al modelo EF Core

| T-SQL | Fluent API |
|-------|-----------|
| `INT IDENTITY(1,1) PRIMARY KEY` | `HasKey(...)` + `UseIdentityColumn(1, 1)` |
| `VARCHAR(n) NOT NULL` | `HasMaxLength(n).IsUnicode(false).IsRequired()` |
| `DECIMAL(10,2)` | `HasPrecision(10, 2)` |
| `DATE` | `DateOnly` + `HasColumnType("date")` |
| `DEFAULT 0` / `DEFAULT (CAST(GETDATE() AS DATE))` | `HasDefaultValue(0)` / `HasDefaultValueSql(...)` |
| `CHECK (precio > 0)`, `CHECK (estado IN (...))` | `ToTable(t => t.HasCheckConstraint(...))` |
| `estado VARCHAR(20)` con 4 valores | `enum EstadoPedido` + `HasConversion<string>()` (se guarda el texto) |
| `UNIQUE` (una o varias columnas) | `HasIndex(...).IsUnique().HasDatabaseName("uq_…")` |
| `FOREIGN KEY … ON DELETE NO ACTION` | `HasOne/WithMany/HasForeignKey` + `OnDelete(DeleteBehavior.NoAction)` |
| `ON DELETE CASCADE` (detalle → pedido) | `OnDelete(DeleteBehavior.Cascade)` |
| `ON UPDATE CASCADE` | No expresable en el modelo de EF Core; se declara en la migración (`onUpdate: ReferentialAction.Cascade`) |

Los nombres de tablas, columnas y llaves foráneas son idénticos al script (`id_categoria`, `fk_producto_categoria`, …),
por lo que las consultas SQL del curso funcionan sin cambios sobre la base creada por EF Core.

**Sobre la migración inicial.** Está escrita para replicar el script con exactitud, incluidas dos reglas que
`dotnet ef migrations add` no generaría por sí solo: `ON UPDATE CASCADE` y el índice único sin filtro en
`Proveedores.email` (como el `UNIQUE` original, admite un único `NULL`). Si prefieres una migración 100 %
autogenerada, consulta la sección [Regenerar la migración](#regenerar-la-migración).

### Datos semilla

`DataSeeder` inserta exactamente los registros del script (3 categorías, 3 proveedores, 6 productos,
3 clientes, 3 empleados, 4 pedidos y 7 detalles), para que los resultados coincidan con los de las versiones
SQL, MongoDB y Cassandra del caso de estudio.

- **Idempotente:** cada tabla se siembra solo si está vacía (`!context.Set<T>().Any()`); ejecutar la app N veces no duplica datos.
- **Sin IDs fijos:** las llaves foráneas se resuelven por llaves naturales (nombre, email, fecha + cliente), así que no depende de los valores IDENTITY.
- **Atómico:** todo ocurre en una transacción dentro de la estrategia de reintentos de EF Core.

### Configuración de arranque

La sección `Startup` de `appsettings*.json` (o variables `Startup__*`) controla qué se ejecuta:

| Clave | Por defecto | Efecto |
|-------|-------------|--------|
| `ApplyMigrations` | `true` | `Database.MigrateAsync()`: crea la base si no existe y aplica migraciones pendientes |
| `SeedData` | `true` | Ejecuta `DataSeeder` |
| `RunDemoQueries` | `true` | Imprime las consultas LINQ |
| `MaxStartupRetries` / `RetryDelaySeconds` | `5` / `3` (Docker: `15` / `4`) | Reintentos mientras SQL Server no acepta conexiones |

Ejemplo: `dotnet run -- --Startup:RunDemoQueries=false` solo migra y siembra.

---

## Modo A · SQL Server local

### Prerrequisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) (`dotnet --version` → `10.0.x`)
- Una de estas opciones:
  - **Windows:** SQL Server Express LocalDB (incluido con Visual Studio) — es la cadena por defecto.
  - **Windows / Linux:** SQL Server 2019+ instalado (Developer o Express).
  - **Linux / macOS sin SQL Server instalado:** solo el contenedor de base de datos del compose (ver abajo).

### 1. Restaurar paquetes y herramientas

```bash
cd Distribuidora
dotnet restore
dotnet tool restore        # instala dotnet-ef 10 como herramienta local
dotnet build
```

### 2. Configurar la cadena de conexión

**LocalDB (Windows):** no hay que hacer nada; `appsettings.json` ya apunta a
`Server=(localdb)\MSSQLLocalDB;Database=Distribuidora;Trusted_Connection=True;TrustServerCertificate=True`.

**SQL Server con autenticación SQL (Linux, macOS o Windows):** guarda la cadena fuera del repositorio con user-secrets:

```bash
dotnet user-secrets set "ConnectionStrings:Distribuidora" \
  "Server=localhost,1433;Database=Distribuidora;User Id=sa;Password=TU_PASSWORD;Encrypt=True;TrustServerCertificate=True"
```

O bien, solo para la sesión actual:

```bash
# Linux / macOS
export ConnectionStrings__Distribuidora="Server=localhost,1433;Database=Distribuidora;User Id=sa;Password=TU_PASSWORD;Encrypt=True;TrustServerCertificate=True"
# Windows PowerShell
$env:ConnectionStrings__Distribuidora = "Server=localhost,1433;Database=Distribuidora;User Id=sa;Password=TU_PASSWORD;Encrypt=True;TrustServerCertificate=True"
```

**Sin SQL Server instalado:** levanta solo el contenedor de base de datos y usa la contraseña de `.env`:

```bash
docker compose up -d sqlserver
dotnet user-secrets set "ConnectionStrings:Distribuidora" \
  "Server=localhost,1433;Database=Distribuidora;User Id=sa;Password=Distr1buidora_Dev2026;Encrypt=True;TrustServerCertificate=True"
```

### 3. Ejecutar

```bash
dotnet run
```

Al arrancar, la aplicación aplica `InitialCreate` (creando la base `Distribuidora` si no existe), siembra los
datos y muestra las cuatro secciones de consultas. Una segunda ejecución debe registrar
`Esquema al día` y `la tabla … ya contiene datos; se omite`.

### Comandos de migraciones (opcionales)

La app migra sola al arrancar, pero también puedes gestionar el esquema con la CLI:

```bash
dotnet ef migrations list                      # migraciones y su estado
dotnet ef database update                      # aplicar sin ejecutar la app
dotnet ef migrations script --idempotent -o esquema.sql   # script T-SQL para revisar o entregar
dotnet ef migrations add NombreDelCambio       # tras modificar el modelo
dotnet ef database update 0                    # revertir todo (borra las tablas)
dotnet ef database drop --force                # eliminar la base de datos
```

#### Regenerar la migración

Si prefieres que EF Core genere la migración inicial por sí mismo (perdiendo `ON UPDATE CASCADE` y con el índice
filtrado `[email] IS NOT NULL` en Proveedores):

```bash
dotnet ef database drop --force
rm -r Migrations
dotnet ef migrations add InitialCreate
dotnet run
```

### Verificación

Con `sqlcmd`, SSMS o Azure Data Studio contra la base `Distribuidora`:

```sql
SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory;   -- 20261022190000_InitialCreate
SELECT COUNT(*) FROM Productos;                                    -- 6
SELECT d.id_pedido, SUM(d.cantidad * d.precio_unitario) AS total
FROM DetallePedidos d GROUP BY d.id_pedido;                        -- 727.50 · 366.00 · 350.00 · 231.00
SELECT name, update_referential_action_desc, delete_referential_action_desc
FROM sys.foreign_keys;                                             -- CASCADE / NO_ACTION como en el script
```

---

## Modo B · Docker y Docker Compose

### Prerrequisitos

- Docker Engine 24+ con el plugin Compose v2 (`docker compose version`), o Docker Desktop.
- ~2 GB de RAM libres para SQL Server.
- En Apple Silicon (M1/M2/M3…): la imagen de SQL Server es `amd64`; activa la emulación Rosetta en Docker Desktop.

La contraseña de `sa` vive en `.env` (`MSSQL_SA_PASSWORD`). Cámbiala antes de usar el proyecto fuera del aula;
debe tener ≥ 8 caracteres con mayúsculas, minúsculas, dígitos y símbolos, y no contener `;` ni comillas.

### Levantar el entorno

```bash
cd Distribuidora
docker compose up --build
```

Qué ocurre:

1. Se construye la imagen de la app (etapa `sdk:10.0` → imagen final `runtime:10.0`, usuario no-root).
2. Arranca `sqlserver` con el volumen persistente `distribuidora-sqlserver-data`.
3. El healthcheck ejecuta `SELECT 1` con `sqlcmd` cada 10 s; la app no inicia hasta que el servicio está *healthy*.
4. La app aplica migraciones (crea la base), siembra los datos, imprime las consultas y termina con código 0.
   SQL Server sigue en ejecución.

Para ejecutarlo en segundo plano y ver después la salida:

```bash
docker compose up --build -d
docker compose logs -f app
```

Para volver a ejecutar solo la app (comprueba la idempotencia del seed):

```bash
docker compose run --rm app
```

### Verificación de datos

```bash
# Estado de los servicios (sqlserver debe aparecer como healthy; app como exited (0))
docker compose ps -a

# Consultas dentro del contenedor (usa la contraseña definida en el propio contenedor)
docker compose exec sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d Distribuidora -Q "SELECT MigrationId FROM __EFMigrationsHistory; SELECT nombre, precio, stock FROM Productos;"'
```

También puedes conectarte desde SSMS/Azure Data Studio a `localhost,1433` con el usuario `sa` y la contraseña de `.env`.

### Detener el entorno

```bash
docker compose stop          # detiene los contenedores, conserva todo
docker compose down          # elimina contenedores y red, conserva el volumen (los datos sobreviven)
docker compose down -v       # elimina también el volumen: la próxima vez se recrea y se vuelve a sembrar
```

---

## Consultas demostrativas

| Sección | Qué muestra | Operadores LINQ |
|---------|-------------|-----------------|
| 1. Listado general | Todos los registros de las 7 tablas | `OrderBy`, `ToListAsync` |
| 2. Filtros y ordenamiento | Productos ≥ $40, pedidos pendientes, empleados contratados antes de 2023, stock bajo, top 5 por stock | `Where`, `OrderBy(Descending)`, `ThenBy`, `Take` |
| 3. Relacionales | Productos con categoría y proveedor, resumen de pedidos, detalle por la tabla N:M, pedidos de un cliente | `Include`, `ThenInclude`, proyección `Select`, carga explícita `Entry().Collection().LoadAsync()` |
| 4. Agregaciones | Conteo por tabla, inventario por categoría, pedidos por estado, total por pedido, ventas por empleado, más vendidos | `Count`, `Sum`, `Average`, `GroupBy`, `Distinct` |

Todas las consultas de lectura usan `AsNoTracking()` salvo la de carga explícita, que necesita una entidad rastreada.
Para ver el SQL que genera EF Core, cambia en `appsettings.json` el nivel de
`Microsoft.EntityFrameworkCore` a `Information` (o añade `"Microsoft.EntityFrameworkCore.Database.Command": "Information"`).

---

## Solución de problemas

| Síntoma | Causa probable / solución |
|---------|---------------------------|
| `A network-related or instance-specific error…` | SQL Server no está corriendo o el puerto difiere. En Docker: `docker compose ps`. |
| `Login failed for user 'sa'` | Contraseña distinta a la del volumen existente. SQL Server fija la contraseña al crear el volumen: usa la original o `docker compose down -v`. |
| El contenedor `sqlserver` se reinicia en bucle | Contraseña que no cumple la política o RAM insuficiente: `docker compose logs sqlserver`. |
| `The certificate chain was issued by an authority that is not trusted` | Falta `TrustServerCertificate=True` en la cadena (Microsoft.Data.SqlClient cifra por defecto). |
| `No se encontró la cadena de conexión 'Distribuidora'` | Define `ConnectionStrings:Distribuidora` (appsettings, user-secrets o variable de entorno). |
| LocalDB no existe en Linux/macOS | Es exclusivo de Windows; usa un SQL Server o el contenedor `sqlserver`. |

## Versiones

Paquetes fijados en `10.0.0` mediante las propiedades `EfCoreVersion` y `ExtensionsVersion` del `.csproj`.
Para pasar al último parche de .NET 10:

```bash
dotnet list package --outdated
# actualiza EfCoreVersion / ExtensionsVersion en el .csproj y la versión de dotnet-ef en .config/dotnet-tools.json
```
