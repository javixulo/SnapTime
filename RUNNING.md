# Cómo arrancar SnapTime

SnapTime tiene dos componentes que deben ejecutarse simultáneamente:

1. **API Server** — backend con endpoints REST y lógica de escaneo
2. **Blazor Client** — frontend WebAssembly que se sirve en un puerto distinto

## Requisitos previos

- .NET SDK 8.0+ (el proyecto apunta a `net8.0`)
- Playwright (solo para tests E2E, ver sección correspondiente)

## Arranque paso a paso

Los pasos son idénticos en Windows y macOS. Solo cambian los comandos de troubleshooting.

### 1. API Server (puerto 3000)

Abre una terminal y ejecuta:

```bash
cd src/SnapTime.Server
dotnet run -c Debug
```

El servidor arranca en `http://localhost:3000`. Puedes verificar que funciona abriendo:
- **Swagger UI:** http://localhost:3000/swagger
- **Health:** http://localhost:3000/api/health

La base de datos SQLite se crea automáticamente en la primera ejecución.

### 2. Blazor Client (puerto 3001)

Abre una **segunda terminal** y ejecuta:

```bash
cd src/SnapTime.Client
dotnet run -c Debug --urls "http://localhost:3001"
```

El cliente arranca en `http://localhost:3001`. Abre esa URL en tu navegador.

> El cliente está configurado en `appsettings.Development.json` para llamar al API en `http://localhost:3000`.

### 3. Verificar que funciona

1. Abre `http://localhost:3001` en el navegador
2. Deberías ver el panel izquierdo con "Sistema de archivos"
3. Haz clic en una unidad (Windows: `C:`, `D:`...) o en `/` (macOS) para expandir el árbol
4. Selecciona una carpeta y pulsa "Escanear" para analizar fotos

## Troubleshooting

### Error 404 al expandir el árbol

Si al hacer clic en el triángulo de expansión de una carpeta ves "Error: 404" en el árbol, es porque la ruta se está construyendo con una `/` de más al inicio.

**Solución:** Revisa `src/SnapTime.Client/Components/FolderTreePanel.razor`, línea 24. Debe ser:

```csharp
var rootPath = root;   // bien
```

Y **no**:

```csharp
var rootPath = $"/{root.TrimStart('/')}";   // mal: añade / al inicio
```

### Puerto 3000 ocupado

**Windows:**
```bash
netstat -ano | findstr :3000
taskkill /f /pid <PID>
```

**macOS / Linux:**
```bash
lsof -i :3000
kill -9 <PID>
```

### La web carga pero muestra "An unhandled error has occurred"

Esto suele ser un error de compilación o una petición API que falla. Abre la consola del navegador (F12 → Console o Cmd+Option+J) para ver el error exacto. Las causas más comunes:

- El API no está corriendo (comprueba `http://localhost:3000/swagger`)
- CORS mal configurado (debería funcionar en Development)
- Falta un endpoint que el frontend intenta llamar

### Tests E2E fallan por Playwright

**Windows:**
```bash
cd tests/SnapTime.E2ETests
dotnet build -c Debug
pwsh bin/Debug/net8.0/playwright.ps1 install --force
```

**macOS / Linux:**
```bash
cd tests/SnapTime.E2ETests
dotnet build -c Debug
dotnet tool install --global Microsoft.Playwright.CLI
playwright install --force
```

## URLs de referencia

| Servicio | URL |
|----------|-----|
| App (Blazor WASM) | http://localhost:3001 |
| API (REST) | http://localhost:3000 |
| Swagger (docs API) | http://localhost:3000/swagger |
| Health check | http://localhost:3000/api/health |
