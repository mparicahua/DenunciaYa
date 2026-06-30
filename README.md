# DenunciaYA API

Sistema de denuncias penales.

# Participantes:

Nombre: Mharco Paricahua (mparicahua)</br>
Nombre: Paolo Lovon (Lov-Oro)</br>
Nombre: Fernando Flores (Farox17)</br>
Nombre: Arturo Benavente (Wirkuf)


## Descripción

API REST para gestión de denuncias penales que permite registro de ciudadanos, creación de denuncias, asignación a funcionarios, derivación a fiscales y generación de reportes estadísticos.

## Requisitos

- .NET Core 10
- PostgreSQL (Supabase)

## Variables de entorno

| Variable | Descripción |
|---|---|
| `ConnectionStrings__PostgreSQL` | Cadena de conexión a PostgreSQL en Supabase |


Ejemplo:
```
ConnectionStrings__PostgreSQL=Host=...;Port=5432;Database=postgres;Username=...;Password=...;SSL Mode=Require
```

## Cómo ejecutar

```bash
cd DenunciaYA.API
dotnet run
```

## Base de datos (imagen Docker)

En `BaseDatos/denunciaya-db.tar` se incluye una imagen Docker con la base de datos PostgreSQL ya preparada, útil para levantar un entorno local sin depender de Supabase.

### Paso 1 — Cargar la imagen desde el archivo

```bash
docker load -i BaseDatos/denunciaya-db.tar
```

### Paso 2 — Levantar un contenedor desde esa imagen

```bash
docker run -d --name postgres-denunciaya -p 5432:5432 denunciaya-db:v1
```

### Paso 3 — Apuntar la API a la base de datos local

Configura `ConnectionStrings__PostgreSQL` para que apunte al contenedor:

```
ConnectionStrings__PostgreSQL=Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres;SSL Mode=Disable
```



## Documentación interactiva

Disponible en: `/scalar`

## Endpoints

### Autenticación
| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/auth/register` | Registrar nuevo ciudadano |
| POST | `/api/auth/login` | Iniciar sesión (devuelve JWT) |

### Denuncias
| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/denuncias` | Crear nueva denuncia |
| PUT | `/api/denuncias/{id}/estado` | Cambiar estado de una denuncia |
| DELETE | `/api/denuncias/{denuncia_id}/evidencias/{id}` | Eliminar evidencia |
| GET | `/api/denuncias/{codigo}/detalle` | Detalle completo por código de seguimiento |
| GET | `/api/denuncias/{id}/historial` | Historial de cambios de estado |
| GET | `/api/denuncias/pendientes-asignacion` | Denuncias admitidas sin funcionario asignado |

### Asignaciones y Derivaciones
| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/asignaciones` | Asignar denuncia a funcionario |
| POST | `/api/derivaciones` | Derivar denuncia a fiscal |

### Funcionarios
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/funcionarios/{id}/denuncias` | Denuncias activas de un funcionario |

### Personas
| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/personas` | Registrar persona |
| GET | `/api/personas` | Listar personas |
| GET | `/api/personas/{id}` | Obtener persona por id |
| PUT | `/api/personas/{id}` | Editar persona |
| DELETE | `/api/personas/{id}` | Eliminar persona |

### Reportes
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/reportes/denuncias-por-delito` | Denuncias agrupadas por tipo de delito |
| GET | `/api/reportes/denuncias-por-delito/exportar` | Mismo reporte en formato CSV |
| GET | `/api/reportes/denuncias-por-mes` | Denuncias agrupadas por mes |
| GET | `/api/reportes/denuncias-por-zona` | Denuncias agrupadas por zona geográfica |

## Códigos de seguimiento

Formato: `DEN-{AÑO}-{NNNNN}` — Ejemplo: `DEN-2024-00021`

## Estados de una denuncia

`INGRESADA`  `EN_EVALUACION`  `ADMITIDA`  `DERIVADA`  `EN_INVESTIGACION`  `ARCHIVADA` / `RESUELTA`
