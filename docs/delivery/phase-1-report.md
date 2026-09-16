# Faz 1 — Identity + Authorization — Teslim Raporu

Tarih: 16 Eylül 2026
Branch: `feature/phase-1-identity-authorization`
Başlangıç commit'i: `82ed1b0 chore: bootstrap CommerceOps phase 0`

Bu rapor secret, gerçek parola, geçici parola, cookie veya token içermez.

---

## 1. Teslim durumu

**Tamamlandı.** Faz 1 kapsamı gerçek API ve gerçek PostgreSQL ile uçtan uca
çalışıyor; hiçbir ekran mock veriyle beslenmiyor. Tarayıcıda giriş, kullanıcı
oluşturma, grup oluşturma, atama, normal kullanıcı görünümü, zorunlu parola
değişikliği ve çıkış akışları fiilen yürütüldü.

### Teslim edilen ekranlar

| Ekran | Yol | Erişim |
|---|---|---|
| Giriş | `/login` | Anonim |
| Parola değiştirme (zorunlu ve isteğe bağlı) | `/change-password` | Kimlikli |
| Ürün grupları (normal kullanıcının başlangıç ekranı) | `/product-groups` | `ProductGroup.View` |
| Ürün grubu detayı | `/product-groups/:id` | `ProductGroup.View` |
| Ürün grubu oluşturma | `/product-groups/new` | `Admin.ProductGroupManagement` |
| Ürün grubu düzenleme / aktif-pasif | `/product-groups/:id/edit` | `Admin.ProductGroupManagement` |
| Kullanıcı listesi | `/admin/users` | `Admin.UserManagement` |
| Kullanıcı oluşturma (+ tek seferlik parola paneli) | `/admin/users/new` | `Admin.UserManagement` |
| Kullanıcı düzenleme + rol + ürün grubu + parola sıfırlama | `/admin/users/:id` | `Admin.UserManagement` |
| Sistem (Faz 0 health görünümü) | `/system` | `Admin.UserManagement` |
| Erişim reddedildi | `/forbidden` | Kimlikli |
| Sayfa bulunamadı | `*` | — |

### Teslim edilen işlevler

- Giriş / çıkış, oturumdaki kullanıcı ve etkin yetkilerin okunması.
- Anonim kullanıcının korunan ekranlardan girişe yönlendirilmesi; API tarafında
  HTML yönlendirme yerine 401/403.
- Süresi dolmuş / iptal edilmiş oturumun "Oturumunuz sonlandı" mesajıyla ele
  alınması.
- İlk yöneticinin tekrar çalıştırılabilir `bootstrap-admin` komutuyla
  oluşturulması.
- Yönetici tarafından kullanıcı oluşturma, aktif/pasif yapma, rol atama, ürün
  grubu atama, parola sıfırlama.
- Rol tabanlı yetkilendirme + ProductGroup veri kapsamı; liste, detay ve
  değişiklik yollarının tamamı tek filtreden geçiyor.
- Hesap bazlı lockout ve istemci bazlı giriş sınırlaması.
- CSRF koruması gerçek istek akışında.
- Ürün grubu oluşturma / listeleme / düzenleme / aktif-pasif; silme yok.

### Eksik iş

Faz 1 kapsamında eksik bırakılan bir madde yok. Bilinçli olarak **kapsam dışı**
tutulanlar (sonraki fazlara sessizce taşınmadı, burada listeleniyor):

- Herkese açık kayıt, sosyal giriş, MFA, e-posta gönderimi, self-service
  "şifremi unuttum".
- Gelişmiş rol/izin tasarım ekranı.
- Ürün CRUD, kategori yönetimi, SKU işlemleri (Faz 2).
- R2, Gemini, Amazon/SP-API, Hangfire, Workflow, Audit modülü.
- `identity.permissions` / `identity.role_permissions` tabloları ve
  `user_product_groups.access_level` kolonu — gerekçeleri bölüm 3'te.

---

## 2. Repo ve değişiklikler

- Repo: `D:\Git\wiseflow`
- Branch: `feature/phase-1-identity-authorization` (bu fazda oluşturuldu)
- Başlangıç commit'i: `82ed1b0`
- **Commit, push, merge veya deploy yapılmadı.** Tüm değişiklikler inceleme için
  çalışma ağacında duruyor.

### Git çalışma ağacı özeti

```
18 değişen dosya (M)
32 yeni yol (??)
```

### Değişen dosyalar ve amaçları

| Dosya | Amaç |
|---|---|
| `src/Api/Program.cs` | `bootstrap-admin` verbi; Identity/authorization kayıtları; auth, rate limiter, no-store ve parola kapısı middleware'leri; yeni slice'ların `Map*` çağrıları |
| `src/Api/CommerceOps.Api.csproj` | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.12 |
| `Infrastructure/Persistence/CommerceOpsDbContext.cs` | Taban tip `IdentityDbContext<User, Role, Guid>`; snake_case geçişi |
| `Infrastructure/Persistence/CommerceOpsDbContextFactory.cs` | (net etki yok — eklenen convention çağrısı geri alındı, bkz. bölüm 3) |
| `Migrations/CommerceOpsDbContextModelSnapshot.cs` | EF tarafından yeni migration ile güncellendi |
| `src/WebApp/package.json`, `package-lock.json` | Tek yeni runtime bağımlılığı `react-router` 7.18.4; `check:api`, `check:permissions`, `check` scriptleri |
| `src/WebApp/vite.config.ts` | `/api` proxy kuralı |
| `src/WebApp/tsconfig.app.json`, `tsconfig.node.json` | `"strict": true` |
| `src/WebApp/src/app/App.tsx` | Router + `AuthProvider` |
| `src/WebApp/src/app/AppShell.tsx` | Gezinme, oturum bilgisi, çıkış; layout route |
| `src/WebApp/src/app/styles/tokens.css` | Tek yeni token `--color-accent-text`, üç bloğa da eklendi |
| `src/WebApp/src/app/styles/global.css` | `components.css` import'u |
| `.github/workflows/ci.yml` | Frontend job'a `check:api` ve `check:permissions` adımları |
| `README.md` | Faz 1 durumu, `bootstrap-admin`, yapılandırma anahtarları, ilk erişim akışı, sorun giderme |
| `docs/architecture/ADR-002-postgresql.md` | snake_case uygulama yönteminin değişme gerekçesi |
| `docs/architecture/module-conventions.md` | Catalog'un Faz 1'de açılması, `Set<T>()` kuralı, izin kataloğunun yeri, 404 kuralı |

### Yeni dosyalar

**Backend — `src/Api/Modules/Identity/` (27 dosya)**

| Klasör | İçerik |
|---|---|
| `Users/` | `User`, `UserConfiguration`, `UserRules`, `UserContracts`, `UserEndpoints`, `LastAdminGuard` |
| `Roles/` | `Role`, `RoleCodes`, `RoleConfiguration` (Admin/Viewer seed) |
| `Sessions/` | `SessionEndpoints`, `SessionContracts` |
| `ProductGroupAccess/` | `UserProductGroup`, `UserProductGroupConfiguration`, `ProductGroupScope` |
| `Authentication/` | `IdentityRegistration`, `CommerceOpsSignInManager`, `PermissionClaimsPrincipalFactory`, `PasswordChangeRequiredMiddleware`, `AllowPendingPasswordChange`, `ApiCacheControlMiddleware`, `AntiforgeryEndpointFilter`, `TemporaryPasswordGenerator`, `LoginTimingDecoy`, `PasswordRules`, `LoginRateLimitOptions`, `IdentityErrorTranslator` |
| `Authorization/` | `Permissions`, `RolePermissions`, `CommerceOpsClaims`, `CurrentUserExtensions`, `AuthorizationRegistration` |
| `Bootstrap/` | `AdminBootstrapper`, `BootstrapSettingsResolver` |
| (kök) | `IdentitySchema`, `IdentityStoreConfiguration` |

**Backend — diğer**

- `src/Api/Modules/Catalog/` — `CatalogSchema`, `ProductGroups/` (entity, configuration, rules, contracts, endpoints)
- `src/Api/Infrastructure/ErrorHandling/` — `ApiProblems`, `ProblemCodes`
- `src/Api/Infrastructure/Persistence/SnakeCaseNaming.cs`
- `Migrations/20260916051620_AddIdentityAndProductGroups.cs` (+ Designer)

**Frontend — 28 dosya**

- `src/shared/` — `apiClient`, `apiClient.check`, `messages`, `ui`, `useApiResource`
- `src/features/auth/` — `authContext`, `AuthProvider`, `useAuth`, `authApi`, `types`, `permissions`, `permissions.check`, `RequireAuth`, `RequirePermission`, `LoginPage`, `ChangePasswordPage`, `ForbiddenPage`, `NotFoundPage`
- `src/features/productGroups/` — `types`, `productGroupApi`, liste/detay/form sayfaları
- `src/features/users/` — `types`, `userApi`, `UserListPage`, `UserFormPage`, `TemporaryPasswordNotice`
- `src/app/SystemPage.tsx`, `src/app/styles/components.css`

**Test — 17 dosya**

- Integration: `IdentityFixture`, `IdentityCollection`, `ApiSession`, `AuthenticationTests`, `AuthorizationTests`, `ProductGroupScopeTests`, `SessionLifecycleTests`, `CsrfTests`, `LastAdminGuardTests`, `LastAdminRaceTests`, `LoginRateLimitTests`, `BootstrapAdminTests`, `MigrationChainTests`
- Unit: `SnakeCaseNamingTests`, `PermissionsCatalogTests`, `TemporaryPasswordGeneratorTests`, `BootstrapSettingsResolverTests`

**Dokümantasyon**

- `docs/architecture/ADR-003-authentication.md`
- `docs/delivery/phase-1-report.md` (bu dosya)
- `docs/delivery/screenshots/` (3 görsel)

---

## 3. Mimari kararlar ve sapmalar

### Seçilen yaklaşım

| Konu | Karar |
|---|---|
| Kimlik | ASP.NET Core Identity (`AddIdentityCore` + EF stores + `AddRoles`) |
| Parola hash | Identity'nin `PasswordHasher<User>`'ı — kendi kripto tasarımı yok |
| Oturum | Cookie (`IdentityConstants.ApplicationScheme`), Data Protection ile şifreli+imzalı |
| Cookie | `commerceops.auth`, `HttpOnly`, `SameSite=Lax`, 8 saat kayan; `Secure`: Development/Testing'de `SameAsRequest`, diğer ortamlarda `Always` |
| CSRF | `IAntiforgery`; cookie `commerceops.csrf` (HttpOnly), request token `X-CSRF-TOKEN` header'ında; POST/PUT/PATCH uçlarında endpoint filtresi |
| Lockout | 5 hatalı deneme → 15 dakika (`IdentityOptions`) |
| Giriş sınırlaması | Yerleşik rate limiting middleware, istemci adresine göre bölümlenmiş; `Security:LoginRateLimit:PermitLimit` (varsayılan 30/dk) |
| Parola politikası | En az 12 karakter, kompozisyon kuralı yok (uzunluk odaklı) |
| API hata gövdesi | RFC 7807 + `code` uzantısı; `GlobalExceptionHandler` değiştirilmeden son çare olarak kaldı |
| Önbellek | `/api` altındaki tüm yanıtlarda `Cache-Control: no-store` |

Ayrıntılı gerekçe: `docs/architecture/ADR-003-authentication.md`.

### Oturum iptali ve yetki değişikliklerinin etkili olma süresi

`SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero` — principal
her istekte veritabanından yeniden doğrulanıyor ve yeniden kuruluyor.

| Değişiklik | Security stamp | Açık oturumlara etkisi |
|---|---|---|
| Rol ekleme/çıkarma | Değişmez | **Bir sonraki istekte** yeni yetkilerle devam eder, çıkış olmaz |
| ProductGroup ataması | Değişmez | **Bir sonraki istekte** etkili |
| Çıkış | Değişmez | Yalnız bu oturumun cookie'si silinir; diğer cihazlar etkilenmez |
| Pasifleştirme | Döner | Kullanıcının **tüm** oturumları bir sonraki istekte 401 |
| Yönetici parola sıfırlama | Döner | Kullanıcının **tüm** oturumları bir sonraki istekte 401 |
| Kullanıcının kendi parola değişikliği | Döner (Identity) | Diğer tüm oturumlar 401; işlemi yapan oturum `RefreshSignInAsync` ile korunur |

Pasifleştirme iki noktada uygulanıyor (`CommerceOpsSignInManager`):
`CanSignInAsync` yeni girişi engelliyor, `ValidateSecurityStampAsync` açık
oturumu düşürüyor. Böylece `is_active` doğrudan SQL ile değiştirilse bile oturum
yaşamıyor — bu, tarayıcıda fiilen doğrulandı.

### Yeni bağımlılıklar

| Bağımlılık | Gerekçe |
|---|---|
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.12 | Identity'nin EF store'ları |
| `react-router` 7.18.4 | Korumalı rotalar, `returnTo` ile girişe yönlendirme, iç içe layout, 403/404 rotaları. Form/validasyon kütüphanesi **eklenmedi**. |

Kimlik doğrulama, cookie, antiforgery ve rate limiting bileşenlerinin tamamı
ASP.NET Core 10 shared framework'ünden geliyor; bunlar için paket eklenmedi.

### Migration

Tek yeni migration: `20260916051620_AddIdentityAndProductGroups`.

- `identity` şeması: `users`, `roles`, `user_roles`, `user_product_groups` (aktif
  kullanımda) + `user_claims`, `user_logins`, `user_tokens`, `role_claims`
  (Identity modelinin parçası, Faz 1'de yazılmıyor/okunmuyor).
- `catalog` şeması: `product_groups`.
- `identity.roles` içine `Admin` ve `Viewer` sabit GUID ve sabit
  `ConcurrencyStamp` ile seed edildi.
- Faz 0 migration'ı silinmedi, yeniden yazılmadı.

### Onaylı plandan sapmalar

**1. `EFCore.NamingConventions` kullanılmadı — planın önceden kararlaştırılmış
yedek planı devreye girdi.**

Plan bu riski öngörmüştü ve ampirik doğrulama şart koşmuştu. Doğrulama paketi
eledi: paket bir `IConventionSetPlugin`; EF Core `__EFMigrationsHistory`
modelini aynı convention set'ten kuruyor ve o tablonun kolon adlarını
sabitlemiyor. Üretilen script bunu açıkça gösterdi:

```sql
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
```

Temiz veritabanında bu görünmez; yalnız **mevcut bir veritabanının
yükseltilmesinde** patlar, üstelik hiçbir migration çalışmadan önce. Yerine
`Infrastructure/Persistence/SnakeCaseNaming.cs` konuldu: `OnModelCreating`
sonunda tablo/kolon/anahtar/FK/index adlarını snake_case'e çeviriyor. History
repository kendi modelini ayrı kurduğu için bu geçiş ona erişemiyor. Karar
ADR-002'ye işlendi, `MigrationChainTests` ile korunuyor.

**2. `GET /api/auth/me` ürün gruplarını döndürmüyor.** Plan bunu öngörüyordu;
uygulamada çıkarıldı çünkü aynı veriyi `GET /api/product-groups` zaten kapsam
filtresiyle veriyor. İki kod yolu, kapsam filtresinin ikiye ayrılması demekti.

**3. `Role` üzerinde ayrı bir `Code` kolonu yok.** Identity'nin `Name` alanı
zaten kodun kendisi (`AddToRoleAsync`, `IsInRole` onu kullanıyor). Ayrı bir
`Code` kolonu ikinci bir doğruluk kaynağı olurdu. Bunun yerine `DisplayName`
eklendi; arayüzdeki Türkçe etiket oradan geliyor.

**4. `/system` rotası ve menü öğesi eklendi.** Faz 0'ın health ekranı kök
ekranını ürün gruplarına devredince erişilemez kalacaktı. Gerçek ve çalışan bir
görünüm olduğu için menüye alındı; `Admin.UserManagement` gerektiriyor.

**5. Son yönetici testleri iki sınıfa ayrıldı.** `LastAdminRaceTests` veritabanı
genelindeki yönetici sayısına bakıyor, bu yüzden kendi container'ını kullanıyor;
paylaşılan bir veritabanında diğer testlerin yöneticileri sayımı kirletiyordu.

**6. `user_product_groups.assigned_at` kolonu eklendi.** Planın kolon listesinde
yoktu. Atamanın ne zaman yapıldığı, Faz 7'de audit gelene kadar tek kayıt; tek
kolon ve tek tüketicisi var (yazma yolu).

**7. `useApiResource` davranışı, tarayıcı doğrulamasında bulunan bir hata
nedeniyle değiştirildi.** Ayrıntı aşağıda.

### Tarayıcı doğrulamasında bulunup düzeltilen hata

Kaydetme sonrası çağrılan `reload()`, kaynağı "loading" durumuna döndürüyor ve
formu **unmount** ediyordu. Sonuç:

- Başarı mesajı ("Roller kaydedildi." vb.) hiç görünmüyordu.
- Daha ciddisi: **"Parolayı sıfırla" sonrası tek seferlik geçici parola paneli,
  yönetici okuyamadan kayboluyordu.** Parola bir daha gösterilemediği için bu,
  hesabı erişilemez bırakabilecek bir veri kaybıydı.

`useApiResource` artık yeniden yüklerken mevcut veriyi ekranda tutuyor; yalnız
loader değiştiğinde (başka bir kayda geçildiğinde) "loading" durumuna dönüyor.
Düzeltme tarayıcıda tekrar doğrulandı: başarı mesajı görünüyor ve parola paneli
sıfırlama sonrası ekranda kalıyor.

### Davranış netleştirmesi

Yetkisiz bir kullanıcının CSRF token'sız değişiklik isteği **403** alıyor, 400
değil: authorization, endpoint filtresinden önce çalışıyor. CSRF kontrolünün
kendisi, yetkili bir oturumla `CsrfTests` içinde ayrıca doğrulanıyor (400 +
`csrf_failed`).

---

## 4. Çalıştırma ve erişim

Tüm komutlar repo kökünden (`D:\Git\wiseflow`), aksi belirtilmedikçe.

```powershell
# 1. Veritabanı
docker compose up -d --wait

# 2. Şema
dotnet ef database update --project src/Api

# 3. İlk yönetici (aşağıdaki anahtarlar ayarlandıktan sonra)
dotnet run --project src/Api -- bootstrap-admin

# 4. API            -> http://localhost:5080
dotnet run --project src/Api

# 5. Web uygulaması -> http://localhost:5173
cd src\WebApp
npm run dev
```

| Servis | Adres | Durum |
|---|---|---|
| Web uygulaması | http://localhost:5173 | Doğrulama sonrası **kapatıldı** |
| API | http://localhost:5080 | Doğrulama sonrası **kapatıldı** |
| PostgreSQL (Compose) | 127.0.0.1:5432 | **Açık bırakıldı** (`commerceops-postgres`, `restart: unless-stopped`) |

### Gerekli yapılandırma anahtarları

Değerler bu raporda ve repoda **yok**; kendiniz ayarlayacaksınız.

| Anahtar | Development | CI / production |
|---|---|---|
| `ConnectionStrings:CommerceOpsDb` | .NET User Secrets | `ConnectionStrings__CommerceOpsDb` |
| `Bootstrap:AdminEmail` | .NET User Secrets | `Bootstrap__AdminEmail` |
| `Bootstrap:AdminDisplayName` | .NET User Secrets | `Bootstrap__AdminDisplayName` |
| `Bootstrap:AdminPassword` | .NET User Secrets | `Bootstrap__AdminPassword` |
| `Security:LoginRateLimit:PermitLimit` | isteğe bağlı (varsayılan 30) | `Security__LoginRateLimit__PermitLimit` |
| `Security:LoginRateLimit:WindowSeconds` | isteğe bağlı (varsayılan 60) | `Security__LoginRateLimit__WindowSeconds` |

Güvenli ayarlama:

```powershell
dotnet user-secrets set "Bootstrap:AdminEmail"       "<sizin e-postanız>"  --project src/Api
dotnet user-secrets set "Bootstrap:AdminDisplayName" "<adınız>"            --project src/Api
dotnet user-secrets set "Bootstrap:AdminPassword"    "<seçtiğiniz parola>" --project src/Api
```

### İlk yönetici oluşturma ve tekrar çalıştırma

```powershell
dotnet run --project src/Api -- bootstrap-admin
```

- Kaynak kodda varsayılan yönetici parolası **yok**; HTTP üzerinden yönetici
  oluşturan bir uç **yok**.
- Tekrar çalıştırmak güvenli: hesap varsa **parolasına dokunulmaz**, yalnız aktif
  olduğu ve `Admin` rolünde bulunduğu garanti edilir.
- Anahtarlar eksikse komut durur ve çalıştırılacak tam komutları yazar; değerleri
  yazdırmaz.
- Veritabanı güncel değilse durur ve `dotnet ef database update` demenizi söyler.

### Yeni kullanıcıya ilk erişim

Yönetici **Kullanıcılar → Yeni kullanıcı** ile hesabı açar. API geçici parolayı
üretir ve yalnız o yanıtın gövdesinde bir kez döndürür; ekran onu "Kopyala"
düğmesiyle gösterir ve sayfadan ayrılınca kaybolur. Yönetici parolayı uygulama
dışından iletir. Kullanıcı ilk girişte parolayı değiştirmek zorundadır; değişene
kadar diğer tüm `/api` çağrıları 403 `password_change_required` döner ve arayüz
onu parola değiştirme ekranında tutar. E-posta servisi ve self-service sıfırlama
kapsam dışı; parolasını unutan kullanıcı için yönetici yeni bir geçici parola
üretebilir.

### Doğrulama ortamı hakkında

Tarayıcı doğrulaması **ayrı ve geçici** bir veritabanında (`commerceops_verify`)
yapıldı; bootstrap değerleri yalnız o sürecin ortam değişkenlerinden verildi.
Sizin geliştirme veritabanınıza ve User Secrets'ınıza kullanıcı yazılmadı.
Doğrulama bittiğinde geçici veritabanı ve onu kuran script silindi; kontrol
edildi: geliştirme veritabanında `identity.users` **0 satır**. Geliştirme
veritabanına yapılan tek işlem migration uygulamaktır.

---

## 5. Doğrulama kanıtları

### Yerel sonuçlar

| Kontrol | Komut veya senaryo | Sonuç | Test sayısı / çıkış kodu | Kanıt |
|---|---|---|---|---|
| Backend derleme (CI ayarlarıyla) | `dotnet build -c Release -p:ContinuousIntegrationBuild=true` | **Geçti** | 0 uyarı, 0 hata | Terminal |
| Backend test (tümü) | `dotnet test -c Release --no-build -p:ContinuousIntegrationBuild=true` | **Geçti** | **97 / 97**, çıkış kodu 0, 18,5 sn | `scratchpad/full-test.log` |
| — Unit | `dotnet test tests/UnitTests` | **Geçti** | **50 / 50** | Terminal |
| — Integration (Testcontainers) | `dotnet test tests/IntegrationTests` | **Geçti** | **47 / 47** | Terminal |
| Faz 0 regresyonu (değiştirilmemiş testler) | Health + ConnectionStringResolver + HealthResponse sınıfları | **Geçti** | **13 / 13** (10 unit + 3 integration) | Terminal |
| Frontend lint | `npm run lint -- --deny-warnings` | **Geçti** | 0 uyarı | Terminal |
| Frontend typecheck | `npm run typecheck` (`strict: true` ile) | **Geçti** | 0 hata | Terminal |
| Health regresyon scripti | `npm run check:health` | **Geçti** | 7 kontrol | Terminal |
| API client kontrolleri | `npm run check:api` | **Geçti** | 18 kontrol | Terminal |
| İzin kontrolleri | `npm run check:permissions` | **Geçti** | 11 kontrol | Terminal |
| Frontend build | `npm run build` | **Geçti** | 53 modül, 298 kB | Terminal |
| Migration — boş veritabanı | `MigrationChainTests` senaryo A | **Geçti** | 1 test | Test çıktısı |
| Migration — Faz 0'dan yükseltme | `MigrationChainTests` senaryo B | **Geçti** | 1 test | Test çıktısı |
| `__EFMigrationsHistory` kolon adları | `MigrationChainTests` | **Geçti** | `MigrationId`, `ProductVersion` korunuyor | Test çıktısı |
| Migration — gerçek Faz 0 dev veritabanı | `dotnet ef database update --project src/Api` | **Geçti** | Çıkış kodu 0 | `__EFMigrationsHistory` 2 satır; `identity` + `catalog` şemaları mevcut |
| `bootstrap-admin` — ilk çalıştırma | CLI, geçici veritabanı | **Geçti** | "First administrator created: &lt;id&gt;" | Terminal |
| `bootstrap-admin` — ikinci çalıştırma | CLI, aynı veritabanı | **Geçti** | "already exists … password unchanged" | Terminal |

**Yeni testler gerçekten keşfediliyor:** Faz 0 tabanı 13 testti; şimdi 97 test
çalışıyor, yani bu fazda **84 yeni test** eklendi (40 unit + 44 integration).
Sıfır test hiçbir adımda başarı sayılmadı.

Testler kendi izole PostgreSQL 18 container'larını kullanıyor (Testcontainers),
`UseEnvironment("Testing")` ile User Secrets yüklenmiyor; geliştirme veritabanı
test veritabanı olarak kullanılmadı.

### Uzak CI sonucu

**DOĞRULANAMADI.** `gh` CLI bu makinede kurulu değil ve GitHub Actions API deposu
için 404 dönüyor (depo private). Bu fazda uzak CI çalıştırılmadı ve sonucu
görülmedi. Yerel olarak CI'ın çalıştırdığı komutların tamamı (yukarıdaki tabloda)
aynı bayraklarla çalıştırıldı ve geçti; bu, uzak CI'ın geçeceğinin garantisi
değildir.

### Çalıştırılmayan kontroller

- Uzak CI — yukarıda.
- Yük/performans testi — kapsamda yoktu.
- Erişilebilirlik denetim aracı (axe vb.) — kapsamda yoktu; klavye erişimi,
  görünür odak, etiketler ve `aria-*` elle uygulandı ve tarayıcıda gözlendi,
  ancak otomatik bir denetimden geçirilmedi.

---

## 6. Arayüz teslimi

### Tarayıcıda fiilen yürütülen senaryolar

Chrome üzerinden, gerçek API ve gerçek PostgreSQL ile:

1. `/` → `/login` yönlendirmesi; giriş ekranı Türkçe, kilitlenme uyarısı sabit.
2. **Yanlış parola** → kontrollü hata: "E-posta veya parola hatalı."
3. **Doğru parola** → `/product-groups`; AppShell'de gezinme, kullanıcı adı,
   tema seçimi, Çıkış.
4. Boş liste durumu → **ürün grubu oluşturma** (ORNAMENTS, KITCHEN); detay
   ekranında "✓ Aktif" ve açıklama.
5. **Kullanıcı oluşturma** (Görüntüleyici rolü + ORNAMENTS ataması) → tek
   seferlik geçici parola paneli, "Kopyala" düğmesi ve uyarı metniyle.
6. Sayfadan ayrılıp dönünce **panel kayboluyor** — parola tekrar gösterilmiyor.
7. Kullanıcı listesinde roller, "✓ Aktif" ve "! Parola değişikliği bekliyor"
   rozetleri.
8. Yöneticinin **kendi kaydını** açması → "Bu sizin hesabınız" uyarısı; rol,
   ürün grubu ve aktiflik kontrolleri devre dışı.
9. **Çıkış** → `/login`.
10. Yeni kullanıcıyla **geçici parolayla giriş** → zorunlu parola değiştirme
    ekranına yönlendirme; menüde yalnız "Ürün grupları".
11. **Kısa parola** → alan bazlı doğrulama: "Parola en az 12 karakter olmalıdır."
12. Geçerli parola → kapı açılıyor, kullanıcı `/product-groups`'a düşüyor ve
    **yalnız ORNAMENTS** görüyor (KITCHEN yok).
13. Adres çubuğuna **başka grubun id'si** → "Ürün grubu bulunamadı."
14. Adres çubuğuna **`/admin/users`** → "Erişim reddedildi" ekranı.
15. Aynı oturumdan doğrudan API çağrıları: `/api/admin/users` → **403**,
    kapsam dışı grup → **404**, yazma denemesi → **403**,
    `Cache-Control: no-store` mevcut.
16. Kullanıcı veritabanından **pasifleştirildi** → bir sonraki istekte oturum
    sonlandı ve `/login`'e düşüldü.
17. Yönetici olarak geri girildi → **her iki grup** da görünüyor (yönetici tümünü
    görür).
18. Security stamp sunucu tarafında döndürüldü → uygulama içi gezinmede
    **"Oturumunuz sonlandı. Devam etmek için tekrar giriş yapın."**
19. Kullanıcıyı **yeniden aktif etme** (hesap kaydetme) → doğrulandı.
20. **Ürün grubu ataması değiştirme** → kayıt başarılı, başarı mesajı göründü.
21. **Parolayı sıfırla** → yeni tek seferlik parola paneli göründü ve ekranda
    kaldı.
22. **Light / Dark tema** ve **400 px** genişlik.

> **Dürüstlük notu:** 1–19 ve 22 adımları gerçek fare tıklamaları ve klavye
> girişiyle yürütüldü. 20 ve 21. adımlarda tarayıcı otomasyonunun işaretçi
> eşlemesi (pencere yeniden boyutlandırma denemelerinden sonra) isabet etmeyi
> bıraktı — ekran görüntüleri de zaman aşımına uğramaya başladı. Bu iki adım,
> aynı DOM olaylarını üreten programatik `element.click()` ile sürüldü ve sonuç
> hem arayüzde hem API'den okunarak doğrulandı. Bu bir uygulama hatası değil,
> otomasyon ortamı sorunudur; aynı uçlar `SessionLifecycleTests` ve
> `AuthorizationTests` içinde ayrıca kapsanıyor.

### Ekran görüntüleri

Kişisel veri, secret, cookie veya parola içermiyor; tamamı yerel doğrulama
verisidir.

| Görsel | Yol |
|---|---|
| Kullanıcı listesi — koyu tema | `docs/delivery/screenshots/users-dark.jpg` |
| Kullanıcı listesi — açık tema | `docs/delivery/screenshots/users-light.jpg` |
| 400 px genişlik — liste ve form | `docs/delivery/screenshots/narrow-400px.jpg` |

Dar ekran, gerçek bir 400 px viewport'ta (sayfaya gömülü iframe) ölçüldü, çünkü
bu Chrome örneğinde pencere yeniden boyutlandırma etkisiz kaldı. Media query'ler
iframe viewport'una göre değerlendirildiğinden bu, CSS'in gerçek bir sınamasıdır:
başlık üç satıra sarıyor, sayfa içeriği tek sütuna iniyor, tablo kendi
kapsayıcısında yatay kayıyor ve **sayfa gövdesi yatay taşmıyor**.

### Kısa demo sırası

1. `docker compose up -d --wait` → `dotnet ef database update --project src/Api`
2. `Bootstrap:*` User Secrets'ı ayarlayın →
   `dotnet run --project src/Api -- bootstrap-admin`
3. `dotnet run --project src/Api` ve ayrı terminalde `cd src\WebApp; npm run dev`
4. http://localhost:5173 → yönetici olarak giriş
5. **Ürün grupları → Yeni ürün grubu** ile bir grup açın
6. **Kullanıcılar → Yeni kullanıcı**: Görüntüleyici rolü + o grup → geçici
   parolayı kopyalayın
7. Çıkış → yeni kullanıcıyla geçici parolayla girin → parolayı değiştirin
8. Yalnız atanan grubu gördüğünü, `/admin/users` adresinin "Erişim reddedildi"
   verdiğini görün
9. Yönetici olarak geri girip kullanıcıyı pasifleştirin; diğer tarayıcıda bir
   sonraki istekte oturumun sonlandığını görün

---

## 7. Kabul matrisi

| # | Kabul ölçütü | Durum | Kanıt |
|---|---|---|---|
| 1 | Doğru bilgilerle giriş başarılı | **Geçti** | `AuthenticationTests` + tarayıcı adım 3 |
| 2 | Yanlış bilgilerle kontrollü hata; dört başarısızlık nedeni birebir aynı yanıt | **Geçti** | `The_four_sign_in_failures_return_one_identical_answer` (gövdeler `traceId` hariç byte düzeyinde karşılaştırılıyor) + tarayıcı adım 2 |
| 3 | Anonim korunan API çağrısı 401, HTML yönlendirme yok | **Geçti** | `Anonymous_request_to_a_protected_endpoint_is_401_and_not_a_redirect` (Location yok, `problem+json`, HTML yok) |
| 4 | Yetkisiz oturum açmış kullanıcının yönetici işlemi 403 | **Geçti** | `A_viewer_may_not_reach_an_administration_endpoint` + tarayıcı adım 15 |
| 5 | Normal kullanıcı başka grubun detayına / yönetimine erişemiyor | **Geçti** | `ProductGroupScopeTests` (404 / 403) + tarayıcı adım 13–15 |
| 6 | Kullanıcı kendi yetkisini yükseltemiyor | **Geçti** | `A_user_may_not_change_their_own_roles_or_product_groups`, `A_user_may_not_deactivate_their_own_account` + tarayıcı adım 8 |
| 7 | Son aktif yönetici koruması | **Geçti** | `LastAdminRaceTests`: iki yönetici eşzamanlı olarak birbirini düşürmeye çalışıyor → biri 204, diğeri 409 `last_admin_protected`, geriye 1 aktif yönetici kalıyor |
| 8 | Çıkış sonrası eski oturumun davranışı tanımlı politikaya uyuyor | **Geçti** | `Sign_out_ends_this_session`, `Signing_out_on_one_device_leaves_the_other_device_signed_in` (politika: yalnız bu oturum) |
| 9 | Kullanıcı pasifleştirme bir sonraki istekte etkili | **Geçti** | `Deactivating_a_user_ends_their_open_session_on_the_next_request` + tarayıcı adım 16 |
| 10 | Rol/grup değişikliği bir sonraki istekte etkili, yeniden giriş gerekmiyor | **Geçti** | `A_role_change_reaches_an_open_session_without_a_new_sign_in`, `A_product_group_assignment_reaches_an_open_session_without_a_new_sign_in` |
| 11 | CSRF eksik/geçersizken değişiklik reddediliyor | **Geçti** | `CsrfTests` (token yok / sahte token / login dahil → 400 `csrf_failed`; geçerli token → 201) |
| 12 | Lockout: 5 hatalı denemeden sonra doğru parola bile reddediliyor | **Geçti** | `Lockout_rejects_even_the_correct_password` |
| 13 | `bootstrap-admin` tekrarlanabiliyor, parolayı sessizce değiştirmiyor | **Geçti** | `BootstrapAdminTests` (arada parola değiştirilip ikinci kez çalıştırılıyor; kullanıcının kendi parolası çalışmaya devam ediyor) + gerçek CLI ile iki kez çalıştırıldı |
| 14 | Migration boş PostgreSQL veritabanına uygulanabiliyor | **Geçti** | `MigrationChainTests` senaryo A |
| 15a | Migration Faz 0'dan yükseltilebiliyor; history iki satır, kolon adları korunuyor | **Geçti** | `MigrationChainTests` senaryo B + `The_migrations_history_table_keeps_the_names_phase_0_created` |
| 15b | Migration gerçek Faz 0 dev veritabanına uygulanabiliyor | **Geçti** | `dotnet ef database update` çıkış 0; `__EFMigrationsHistory` 2 satır |
| 16 | `must_change_password` kapısı çalışıyor ve `/health`, statik dosyalar, login/logout, `/api/auth/me`, `/api/auth/csrf`, change-password akışını bozmuyor | **Geçti** | `The_password_change_gate_blocks_the_api_but_not_the_flow_that_clears_it`, `The_password_change_gate_leaves_the_health_endpoint_alone` + tarayıcı adım 10–12 |
| 16b | Geçici parola yalnız oluşturan yanıtta, `no-store` ile; tekrar okunamıyor | **Geçti** | `A_generated_password_is_returned_once_and_is_never_readable_again` + tarayıcı adım 5–6 |
| 17 | Parola değişikliği diğer oturumları düşürüyor, mevcut oturumu koruyor | **Geçti** | `Changing_a_password_ends_the_other_sessions_and_keeps_this_one` |
| 18 | Faz 0 health davranışı korunuyor | **Geçti** | Health testleri değiştirilmeden geçiyor (13/13); `/health` anonim ve 200 |
| 19 | Mevcut backend/frontend kontrolleri geçiyor | **Geçti** | Bölüm 5 tablosu |
| 20 | Yeni testler gerçekten keşfediliyor | **Geçti** | 13 → 97 test (84 yeni) |
| 21 | Arayüzde gerçek API ile giriş, kullanıcı oluşturma, grup oluşturma, atama, normal kullanıcı görünümü, çıkış | **Geçti** | Bölüm 6, adım 1–21 (20–21 için otomasyon notu) |
| 22 | Light/dark tema ve dar ekran düzeni | **Geçti** | Bölüm 6 ekran görüntüleri |
| — | Uzak CI | **Doğrulanamadı** | `gh` yok, depo private |

---

## 8. Açık sorunlar ve riskler

**1. Çıkış, diğer oturumları iptal etmiyor (bilinçli, kabul edilmiş).**
Tekrarlama: bir cihazda giriş yapın, cookie değerini kopyalayın, çıkış yapın,
kopyalanan cookie ile istek atın. Cookie `ExpireTimeSpan` (8 saat) dolana kadar
kriptografik olarak geçerli kalır — cookie tabanlı kimlik doğrulamanın standart
davranışı. `HttpOnly` + `Secure` + `SameSite=Lax` pencereyi daraltıyor; anında
iptal gerektiğinde pasifleştirme veya yönetici parola sıfırlama kullanılır.
**Teslimi engellemiyor.** Karar gerekiyorsa: çıkışın da tüm oturumları
sonlandırmasını ister misiniz? (Tek satırlık değişiklik; bedeli, bir cihazdan
çıkışın diğer cihazlardan da atması.)

**2. Kullanılmayan dört Identity tablosu.** `user_claims`, `user_logins`,
`user_tokens`, `role_claims` oluşuyor ama Faz 1'de hiç yazılmıyor/okunmuyor.
Identity modelinin parçası oldukları için `IdentityDbContext` üzerinde
budanamazlar. Tam Identity kararının bilinen bedeli. **Teslimi engellemiyor.**

**3. Kimlikli her istekte bir veritabanı sorgusu.** `ValidationInterval =
TimeSpan.Zero`, gecikmesiz iptalin bedeli. Yönetim paneli yükü için uygun;
önbellek bilinçli olarak eklenmedi. Ölçümle birlikte yeniden değerlendirilmeli.
**Teslimi engellemiyor.**

**4. `last_admin_protected` kuralının tek iş parçacıklı yolu yok.** Self-koruma
kuralları (kendi rolünü değiştirememe, kendini pasifleştirememe) bir yöneticinin
sistemi yöneticisiz bırakabileceği tek yolu zaten 403 ile kapatıyor. Kural
yalnız yarış koşulunda tetikleniyor ve orada test ediliyor. Faz 2+'de
`Admin.UserManagement` başka bir role verilirse sıralı yol da erişilebilir olur.
**Teslimi engellemiyor.**

**5. Giriş hataları hesabın kilitli olduğunu söylemiyor.** Bilinçli: dört
başarısızlık nedeni tek tip yanıt döndürüyor. Kullanıcı kilitlendiğini
anlamayabilir; giriş formundaki sabit yardım metni bunu telafi ediyor, ayrım
sunucu logunda duruyor. **Teslimi engellemiyor.**

**6. Uzak CI doğrulanmadı.** `gh` kurulu değil ve depo private. Yerel olarak
CI'ın çalıştırdığı komutların tamamı aynı bayraklarla geçti, ama uzak koşuya
bakılmadı. **Karar gerekiyor:** `gh` kurulmasını ve uzak CI'ın kontrol
edilmesini ister misiniz?

**7. Erişilebilirlik otomatik denetimden geçmedi.** Klavye erişimi, görünür odak,
form etiketleri, `aria-invalid`/`aria-describedby`, `role="alert"` ve
ikon+metin durum gösterimi elle uygulandı ve tarayıcıda gözlendi; axe benzeri bir
araç çalıştırılmadı. **Teslimi engellemiyor.**

**8. Tarayıcı otomasyonu doğrulamanın sonunda kararsızlaştı.** İşaretçi eşlemesi
isabet etmeyi bıraktı ve ekran görüntüleri zaman aşımına uğradı (pencere yeniden
boyutlandırma denemelerinden sonra). İki UI adımı bu yüzden programatik DOM
click ile sürüldü (bölüm 6). Uygulama davranışı değil, ortam sorunu.

---

## 9. İnceleme için kısa özet

### Önce bakılacak dosyalar

1. **`docs/architecture/ADR-003-authentication.md`** — kimlik, oturum, iptal, CSRF
   ve izin kararlarının tamamı ve bedelleri.
2. **`src/Api/Modules/Identity/Authentication/IdentityRegistration.cs`** —
   Identity, cookie, CSRF ve throttling'in tek kurulum noktası; ortam bazlı
   `Secure` politikası; 401/403 event'leri.
3. **`src/Api/Modules/Identity/ProductGroupAccess/ProductGroupScope.cs`** — tek
   veri kapsamı filtresi. Liste, detay ve değişiklik yollarının hepsi buradan
   geçiyor.
4. **`src/Api/Modules/Identity/Users/LastAdminGuard.cs`** — transaction +
   advisory lock + **kilit alındıktan sonra** yeniden sayım. Sıra bağlayıcı.
5. **`src/Api/Infrastructure/Persistence/SnakeCaseNaming.cs`** ve ADR-002 eki —
   `__EFMigrationsHistory` uyumluluğunun neden böyle çözüldüğü.
6. **`src/Api/Modules/Identity/Sessions/SessionEndpoints.cs`** — tek tip giriş
   hatası, zamanlama denkleştirme, loglama kuralları.
7. **`tests/IntegrationTests/LastAdminRaceTests.cs`** ve
   **`MigrationChainTests.cs`** — bu fazın en çok düşünülmüş iki testi.
8. **`src/WebApp/src/shared/apiClient.ts`** — CSRF, 401 ve ProblemDetails'in tek
   ele alındığı yer.

### Kritik yetki/oturum davranışları

- Yetki kontrolü **iki katmanlı**: endpoint policy'si + sorgu filtresi. İzin tek
  başına asla yeterli değil.
- Kapsam dışı kayıt **404** döner, 403 değil.
- Aktif kullanıcı **yalnız** `HttpContext.User`'dan okunur; gövdedeki hiçbir
  id doğrulanmadan kullanılmaz.
- Oturum iptali ve yetki tazeleme **gecikmesiz** (bölüm 3 tablosu).
- Geçici parola **yalnız** oluşturan yanıtın gövdesinde, bir kez; saklanmaz,
  loglanmaz, URL'ye konmaz.
- Parola, cookie, token, CSRF token ve giriş denemesindeki e-posta **hiçbir
  seviyede loglanmaz**.

### Kalan işler

- Uzak CI'ın bu branch üzerinde çalıştırılması ve sonucunun görülmesi.
- Bölüm 8'deki iki açık karar: çıkışın global iptal olup olmayacağı ve `gh`
  kurulumu.

### Faz 2'ye geçilmedi

Catalog modülü yalnız `ProductGroups` slice'ını içeriyor. `catalog.categories`,
`catalog.products`, ürün CRUD, SKU işlemleri, Media, AI, Marketplace, Workflow,
Audit, R2, Gemini ve Hangfire ile ilgili **hiçbir kod yazılmadı**. Commit, push,
merge ve deploy yapılmadı.
