# mercado-comunidad-api — Backend .NET 8

## Arquitectura

- **Minimal APIs** (sin Controllers): todas las rutas se registran directamente en `Program.cs`
- **Service layer**: toda la lógica de negocio vive en `Services/`
- **Inyección de dependencias**: todos los servicios registrados como `Singleton` con interfaces
- **DTOs**: en `Models/DTOs/` — nunca exponer modelos de dominio directamente en respuestas

---

## Archivos Clave

| Archivo | Propósito |
|---|---|
| `Program.cs` | Entry point: registro de servicios, middleware, y todas las rutas |
| `appsettings.json` | Config base (strings vacíos) |
| `appsettings.Development.json` | Config local con credenciales reales — no commitear |
| `Configuration/*.cs` | Clases tipadas para settings |
| `Models/` | Entidades de dominio (mapeadas a MongoDB) |
| `Models/DTOs/` | Objetos de transferencia de datos |
| `Services/` | Lógica de negocio + interfaces |
| `Security/UserRoles.cs` | Constantes de roles |
| `Security/AuthorizationHelpers.cs` | Helpers para verificar permisos en endpoints |

---

## Configuración (appsettings)

```json
"MongoDbSettings": { "ConnectionString": "", "DatabaseName": "Mercado" }
"R2StorageSettings": { "AccountId": "", "AccessKeyId": "", "SecretAccessKey": "", "BucketName": "mercado-comunidad-images", "PublicBaseUrl": "" }
"EmailSettings": { ... }
"Jwt": { "Key": "", "Issuer": "", "Audience": "", "ExpiryMinutes": 60 }
"Cors": { "AllowedOrigins": "" }
```

---

## Modelos de Dominio

- **User**: Id, Name, Email, Password (BCrypt), Role, IsActive, EmailVerified, Address (nested)
- **Products**: Id, IdStore, Title, Price, Images (array URLs), Category, Active, Synchronized
- **Store**: Id, Name, LinkStore, Users (array con Role: `"1"` = admin, `"2"` = member), IsGlobal, Active
- **Community**: Id, CommunityId (string slug), Name, Open, Active, Visible
- **Sale**, **MetricEvent**, **Plan**: entidades adicionales

---

## Convenciones Backend

- Nuevos endpoints → registrar en `Program.cs` siguiendo el patrón existente
- Nueva entidad → crear: `Models/Entity.cs`, `Models/DTOs/EntityDTOs.cs`, `Services/IEntityService.cs`, `Services/EntityService.cs`
- Settings nuevos → clase en `Configuration/`, registrar en `Program.cs` con `builder.Services.Configure<T>()`
- **Nunca exponer passwords ni datos sensibles en DTOs de respuesta**
- Autorización: usar `AuthorizationHelpers` para verificar permisos (no duplicar lógica de roles)
- Límites de plan: usar `CheckStoreCreationLimitAsync` / `CheckProductCreationLimitAsync` al crear recursos

---

## Roles y Autorización

```csharp
UserRoles.Buyer          = "buyer"           // solo puede comprar
UserRoles.Seller         = "user"            // puede crear tiendas y productos
UserRoles.CommunityAdmin = "community_admin" // administra comunidades
UserRoles.SuperAdmin     = "super_admin"     // acceso total
```

Helpers disponibles en `AuthorizationHelpers`:
- `IsAdmin(user)` → `community_admin` o `super_admin`
- `IsSuperAdmin(user)` → solo `super_admin`
- `CanManageStoreAsync(storeId, user, storeService)` → admin global o miembro de la tienda
- `CanManageProductAsync(productId, user, ...)` → admin global o dueño del producto
- `CanManageImageAsync(folder, entityId, user, ...)` → verifica por carpeta: `user`, `store`, `product`

---

## Endpoints API — Referencia Rápida

### Auth
```
POST /auth/register
POST /auth/login
POST /auth/verify-email/{userId}
POST /auth/request-password-reset
POST /auth/validate-reset-code
POST /auth/reset-password
```

### Products
```
GET    /products                          # paginado: pageNumber, pageSize
GET    /products/{id}
GET    /products/categoria/{categoria}    # paginado
GET    /products/store/{storeId}          # paginado
GET    /products/active/list
POST   /products                          # requiere Title, IdStore — verifica límite de plan
PUT    /products/{id}
DELETE /products/{id}
POST   /products/{id}/images
DELETE /products/{id}/images
PUT    /products/{id}/images/reorder
POST   /products/{id}/synchronize
POST   /products/synchronize/all
POST   /products/synchronize/store/{storeId}
```

### Stores
```
GET    /stores
GET    /stores/{id}
GET    /stores/link/{linkStore}
GET    /stores/user/{userId}
GET    /stores/active/list
GET    /stores/global/list
POST   /stores                            # verifica límite de plan
PUT    /stores/{id}
DELETE /stores/{id}
POST   /stores/{storeId}/users
DELETE /stores/{storeId}/users/{userId}
```

### Communities
```
GET /communities
GET /communities/{id}
GET /communities/by-community-id/{communityId}
GET /communities/active
GET /communities/visible
```

### Community Products
```
GET /community-products/{communityId}                          # paginado
GET /community-products/{communityId}/categoria/{categoria}
GET /community-products/product/{id}
```

### Users
```
GET    /users/{id}
GET    /users/email/{email}
PUT    /users/{id}
POST   /users/{id}/change-password
DELETE /users/{id}
```

### Images
```
POST   /images/upload                    # multipart/form-data
GET    /images/{folder}/{entityId}
GET    /images/{id}
DELETE /images/{id}
DELETE /images/by-url
```

### Categories
```
GET    /categories
GET    /categories/{id}
GET    /categories/name/{name}
POST   /categories
PUT    /categories/{id}
DELETE /categories/{id}
```

### Otros
```
GET /health
GET /sales/*
GET /metrics/*
GET /plans/*
```
