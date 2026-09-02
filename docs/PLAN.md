# OpenData API — Uygulama Planı

> Kaynak: [acik-veri-api-brd.md](../acik-veri-api-brd.md) (BRD v1.0)
> Bu doküman BRD'deki iş gereksinimlerini teknik uygulama adımlarına çevirir.
> Durum: Faz 0-1 tamam, Vercel A1-A5 tamam, **Faz 2'nin C1-C5'i tamam**, TR adres hiyerarsisi
> kendi derlememize gecti, **countries/flags/timezones lisans yuzeyi azaltmak icin kaldirildi**
> (2 Eylul 2026, bkz. DATA-LICENSES.md) — planin kaldirilan kisimlara ait izleri de temizlendi.
> Kalan: C6 (anahtar portali), A6-A7, B (yayin), D (Faz 3), E (teknik borc). Bkz. §7.

---

## 1. Çözüm ve repo yapısı

```
YaMiSoFt.OpenData/
├── src/
│   ├── YaMiSoFt.OpenData.Api/          # ASP.NET Core Minimal API host
│   ├── YaMiSoFt.OpenData.Core/         # modeller, sözleşmeler, arama/normalizasyon
│   └── YaMiSoFt.OpenData.Data/         # JSON yükleme, FrozenDictionary indeksler
├── data/                               # "veri = kod" — tek doğruluk kaynağı
│   ├── provinces.json | districts.json | quarters.json | neighborhoods.json
│   ├── currencies.json | languages.json
│   ├── overrides/*.json                # türetilemeyen küratörlü değerler
│   └── schemas/*.schema.json           # JSON Schema doğrulama
├── tests/
│   ├── YaMiSoFt.OpenData.Data.Tests/   # veri bütünlüğü (CI kapısı)
│   └── YaMiSoFt.OpenData.Api.Tests/    # WebApplicationFactory entegrasyon
├── tools/YaMiSoFt.OpenData.DataTool/   # kaynak → data/ dönüştürücü CLI
├── .github/workflows/
└── Dockerfile, LICENSE, DATA-LICENSES.md
```

**Neden bu ayrım:** `Core` + `Data` host'tan bağımsız kalırsa BRD §7.2'deki "NuGet paketi bonus"
ek iş olmadan çıkar — aynı derlemeler `OpenData.Turkey` olarak paketlenebilir.

---

## 2. Faz 0 iş kırılımı (2 hafta)

| # | İş | Gereksinim | Durum |
|---|---|---|---|
| 1 | git init, solution + projeler, `Directory.Build.props`, nullable + warnaserror | — | ✅ |
| 2 | Veri modelleri + `System.Text.Json` source-gen context | AOT uyumu | ✅ |
| 3 | Startup loader: JSON → `FrozenDictionary`/`FrozenSet`, immutable | NFR-06, §7.2 | ✅ |
| 4 | Türkçe duyarlı arama normalizasyonu + önceden hesaplanmış arama anahtarları | FR-05 | ✅ |
| 5 | Sayfalama / sıralama / `fields=` projeksiyon altyapısı | FR-03 | ✅ |
| 7 | ProblemDetails (RFC 9457) + global exception handler | FR-09 | ✅ |
| 8 | OutputCache + ETag/304 + Cache-Control + Brotli/Gzip | NFR-06/07/09 | ✅ |
| 9 | RateLimiter + `X-RateLimit-*` header middleware | NFR-01/02/03 | ✅ |
| 11 | OpenAPI + Scalar UI | FR-08 | ⚠️ 3.0 |
| 12 | Health check, OpenTelemetry + Prometheus | NFR-16/19 | ✅ |
| 13 | Dockerfile (çok aşamalı, chiseled) + GitHub Actions CI | §7.1 | ✅ |
| 14 | Veri bütünlüğü test paketi | §8 | ✅ |

---

## 3. Kritik teknik kararlar

BRD'nin netleştirmediği, şimdi karar verilmezse sonra pahalıya patlayan noktalar.

### 3.1 NFR-01 ↔ NFR-04 çelişkisi — rate limit dağıtıklığı
.NET yerleşik `RateLimiter` **process içi**dir; Redis desteği yoktur. Çoklu pod'da tutarlı
limit için Lua script'li özel sliding-window ya da topluluk paketi gerekir.

**Karar (uygulandı):** `IRateLimitStore` soyutlaması. MVP'de `InMemoryRateLimitStore`,
Faz 2'de Redis. Sayaçlar **duvar saatine hizalı** sabit pencereler (dakika sınırı, UTC gece
yarısı) — bu, `X-RateLimit-Reset` değerini tahmin değil kesin zaman damgası yapar ve Redis'te
`INCR` + pencere sonunda `EXPIRE` ile birebir aynı semantiği verir. Böylece Faz 2 geçişi
sayaçların *nerede durduğunu* değiştirir, limitlerin *nasıl davrandığını* değil.

Pencere süresi dolunca `MemoryCache` girdiyi kendisi düşürüyor; ayrı temizlik görevi yok.

### 3.2 `X-RateLimit-*` header'ları
Yerleşik middleware bu header'ları üretmez. NFR-03 kotayı **her** yanıtta istiyor — başarılı
olanlar dahil — bu da sayaçların istek yolunda okunup yanıta yazılmasını gerektiriyor.

**Karar (uygulandı):** `RateLimitMiddleware` yazıldı. `/all` uçları ayrı partition ve ayrı kota
kullanıyor; toplu indirme kotasını tüketmek sayfalı okumaları kilitlemiyor. Health ve metrics
uçları hiç limitlenmiyor — orkestratörün readiness probe'u kota tüketip pod'u rotasyondan
düşürememeli.

### 3.3 Cloudflare arkasında IP bazlı limit
`ForwardedHeaders` + `KnownProxies` yapılandırılmazsa **tüm trafik tek IP** görünür ve anonim
limit ilk gün kilitlenir. `CF-Connecting-IP` okunmalı.

**Karar (uygulandı):** `OpenData:Proxy` bölümü eklendi, varsayılan **kapalı**. Açıkken istemci
kendi adresini söyleyebilir — yani her istekte yeni bir adres uydurup anonim limiti tamamen
atlayabilir. Yalnızca gerçekten bir CDN arkasındayken, `KnownProxies`/`KnownNetworks` dolu
olarak açılmalı. IPv6 istemciler **/64 bazında** gruplandı: tek bir abone rutin olarak o
aralığın tamamını alır, tam adrese göre bölmek ona sınırsız yeni kova verirdi.

### 3.4 FR-01 XML desteği ↔ Minimal API
Minimal API'de içerik müzakeresi (content negotiation) yoktur.

**Karar (uygulandı):** MVP saf JSON. XML Faz 2'ye alındı; gerekirse `NegotiatedResult<T>` yazılır.

### 3.5 FR-03 `fields=` ↔ OutputCache anahtar patlaması
`page × pageSize × lang × fields × search` kombinasyonu cache'i şişirir.

**Karar (uygulandı):** `fields` alfabetik sıralanıp normalize edilir, cache anahtarı
kanonikleştirilir; `fields=a,b` ile `fields=b,a` tek ETag paylaşır. Bilinmeyen alan sessizce
atılmaz, 400 döner — yazım hatası fark edilmeden eksik yanıt dönmesindense hata dönsün.
`search` içeren istekler kısa TTL (5 dk) ile ayrı tutulur.

**Uygulamada çıkan iki hata — ikisi de teste bağlandı:**

1. **Projeksiyon zarfa uygulanıyordu.** `?fields=alpha2` sayfalı yanıtta `items`, `page` ve
   `totalCount` alanlarının hepsini birden siliyor, `{}` döndürüyordu. Projeksiyon artık
   `itemsProperty` ile kayıtların içine iniyor, zarf meta verisi korunuyor.
2. **`SetVaryByQuery` allow-list'i cache'i çarpıştırıyordu.** Açık liste vermek cache anahtarını
   o parametrelere *daraltıyor*; listede olmayan `format` ve `size` anahtara girmediği için
   PNG isteyen çağrı cache'lenmiş SVG'yi alıyordu. Allow-list kaldırıldı, OutputCache'in
   varsayılanı olan "tüm query string" davranışına dönüldü — endpoint sayısı arttıkça güvenli
   olan tek davranış bu.

### 3.6 ETag stratejisi
Veri deploy'lar arası değişmez → `ETag: W/"{dataVersion}-{kanonikSorguHash}"`.
Payload hash'lemeye gerek yok, bedava 304.

### 3.7 Türkçe diakritik-duyarsızlık (FR-05)
Standart `FormD` + birleşik işaret ayıklama `ı` ve `İ` için çalışmaz — `ı` ayrıştırılabilir
bir karakter değildir. Açık eşleme gerekir: `İ,I,ı→i`, `Ğ,ğ→g`, `Ş,ş→s`, `Ü,ü→u`, `Ö,ö→o`, `Ç,ç→c`.
Arama anahtarları startup'ta bir kez hesaplanır, istek başına değil.

### 3.8 FR-07 toplu indirme ↔ rate limit
`/all` = 1 istek ama megabaytlarca yanıt; sayaç maliyeti yansıtmıyor.

**Karar (uygulandı):** `/all` için ayrı partition ve daha sıkı limit (5/dk, 100/gün),
`max-age=86400` ile.

### 3.9 AOT
Reflection tabanlı XML ve bazı OTel exporter'ları AOT'yi kısıtlar.

**Karar (uygulandı):** MVP'de AOT *uyumlu tasarla* (source-gen JSON, istek yolunda hiç
reflection tabanlı serileştirme yok) ama açma.

**Ölçüm — Faz 2 için somut girdi:** `IsAotCompatible` analizörü API projesinde açıldığında
build iki gerçek engelde patıyor: (a) minimal API handler delegate'leri Request Delegate
Generator istiyor, (b) `Configure<TOptions>` / `ConfigurationBinder.Get<T>` configuration
source generator istiyor. `Core` ve `Data` kütüphaneleri analizörü **temiz geçiyor** — yani
engel bizim kodumuzda değil, framework yüzeyinde. Analizör API projesinde kapalı (gerekçe
csproj'da yazılı), kütüphanelerde açık.

### 3.10 Veri lisansı
TÜİK/İçişleri il-ilçe verisinin yeniden dağıtım şartları teyit edilmeli (BRD §11
"lisans ihlali" riski).
`DATA-LICENSES.md` her veri seti için kaynak + lisans satırı içerir; ayrıca her veri dosyası
kendi `source` ve `license` alanını taşıyor, böylece köken veriyle birlikte seyahat ediyor.

---

## 4. Veri hattı ve test stratejisi

"Veri = kod" kararı tamamen CI kapısına bağlıdır. `Data.Tests` her PR'da doğrular:

- JSON Schema uyumu (her veri seti için şema)
- Benzersizlik: ISO kodları, plaka kodları, id'ler
- Referans bütünlüğü: `district.provinceId` mevcut bir il mi
- Sayısal beklentiler: tam **81** il, koordinat aralıkları, ISO kod formatı
- Kişisel veri sızıntısı taraması (KVKK, BRD §3.2)

`tools/DataTool` ham kaynakları `data/` formatına dönüştürür — **build'de değil, elle
çalıştırılıp çıktısı commit edilir**. Böylece build deterministik ve offline kalır, veri
değişimi git geçmişinde okunabilir olur.

---

## 4b. Faz 1 iş kırılımı

| # | İş | Gereksinim | Durum |
|---|---|---|---|
| 1 | Türkiye il verisi (81 il, plaka/alan kodu/bölge/koordinat/nüfus) | BRD §3.1 | ✅ |
| 2 | Türkiye ilçe verisi (973 ilçe) + hiyerarşik uçlar | BRD §3.1, FR-06 | ✅ |
| 3 | Para birimleri (ISO 4217, sembol, ondalık hane) | BRD §3.1 | ✅ |
| 4 | Diller (ISO 639-1/639-2, yerel isim) | BRD §3.1 | ✅ |
| 5 | Ortak liste sorgu iskeleti (tüm uçlarda aynı doğrulama) | FR-03/04/05 | ✅ |
| 6 | Veri bütünlüğü testleri (referans bütünlüğü dahil) | §8 | ✅ |
| 7 | Küratörlü override mekanizması | §4b.3 | ✅ |

### 4b.1 TR adres kaynağı

**Faz 1'de:** `ubeydeozdmr/turkiye-api` (MIT), TÜİK ve İçişleri rakamlarının yeniden yayını.

**Şu an (2 Eylül 2026'dan itibaren):** dört seviyenin tamamı — il, ilçe, semt, mahalle/köy —
bu proje için derlenmiş tek bir Türkçe export'tan üretildi, MIT. Export commit'li **değil**;
üretilmiş dört dosya doğruluk kaynağı. Neden değişti ve nesi kayboldu: §7 C7.

Kısaca: turkiye-api semt seviyesini hiç taşımıyordu ve posta kodunu her yerleşime kopyalıyordu.
Semt, posta kodunun gerçekten atandığı seviye; onu modellemek posta kodu aramasını 50 binlik
tembel veri setinden 2.433 satırlık başlangıçta yüklü bir dosyaya taşıdı. Karşılığında il/ilçe
nüfus ve yüzölçümü alanları düştü — iki kaynağın ilçe kimlikleri ortak olmadığı için
birleştirme ancak isim tahminiyle olurdu.

### 4b.2 Para birimi ve dil: indirme yok

.NET'in içinde gelen ICU tabloları ISO 4217 kodlarını sembol ve ondalık hane sayısıyla,
ISO 639 kodlarını da istenen dildeki adıyla zaten taşıyor. Bu iki veri setini oradan türetmek:

- takip edilecek üçüncü parti lisansı bırakmıyor (Unicode-3.0, attribution yeterli),
- Faz 0'da restcountries'in çürüdüğü gibi çürüyebilecek bir ağ bağımlılığı yaratmıyor,
- isimleri runtime'ın kendi kültür verisiyle senkron tutuyor.

**Ondalık hane önemli:** JPY 0, KWD 3, çoğu 2. Bunu taşımayan bir para birimi listesi,
tüketicisinde para yuvarlama hatasına yol açar — görüntü detayı değil, doğruluk meselesi.

### 4b.3 Küratörlü override mekanizması

ICU bir para biriminin adını İngilizce ve kendi ana dilinde verir, **hedef dilde vermez** —
yani Türkçe para birimi adları türetilemiyor. Türetmeye çalışıp yanlış üretmek yerine
küratörlü bir katman gerekiyor.

**Karar (uygulandı):** `data/overrides/currencies.tr.json`. Veri aracı üretim anında bu
katmanı ICU çıktısının üzerine bindiriyor. Katmanda artık var olmayan bir kod listelenirse
araç hata veriyor (sessizce eskimesin), aynı doğrulama testlerde commit'lenmiş çıktı
üzerinden de yapılıyor (elle düzenlenen katman fark edilmeden geçmesin). 43 para birimi
TCMB kullanımına göre dolduruldu; listede olmayanlar İngilizce adıyla kalıyor.

### 4b.4 Ortak sorgu iskeleti

Dört veri seti aynı `page`/`pageSize`/`search`/`sort`/`order`/`fields`/`lang` yüzeyini
paylaşıyor. Her uçta yeniden yazmak, dört ayrı doğrulama davranışı ve dört ayrı hata şekli
demekti. `ListRequest.TryParse` + `ApiProblem` ile tek noktaya alındı: bir uçtaki 400'ü
işleyen istemci kodu hepsindekini işliyor. `SearchIndex<T>` ve `ReferenceOrdering` de aynı
gerekçeyle çıkarıldı.

**Türkçe collation:** il/ilçe listeleri `tr-TR` ile sıralanıyor. Invariant comparer ile
sıralanmış bir il listesi, tam da bu projenin var olma sebebi olan kullanıcı kitlesine
gözle görülür biçimde yanlış görünür.

---

## 5. Sonraki fazlar

**Faz 1 (tamamlandı):** il/ilçe, para birimi, dil endpoint'leri. Veri hacmi küçük kaldı,
mimari değişmedi — beş veri seti toplam ~400 KB JSON.

**Faz 2:** Mahalle/posta kodu. Kaynak belirlendi (turkiye-api, MIT) ve **tek başına 7,5 MB** —
mevcut tüm veri setlerinin ~10 katı. In-memory kararı burada **ölçülerek** yeniden verilmeli;
`SearchIndex` her kayıt için normalize edilmiş anahtar tuttuğundan bellek maliyeti ham JSON'un
üzerine biniyor. Gerekirse mahalle için embedded SQLite ya da tembel yükleme. API anahtar
portalı kalıcılık gerektirir → ayrı modül, okuma API'si stateless kalır. Redis'e geçiş burada.

**Faz 2 sonucu:** C1-C5 tamamlandı, ardından C7 ile adres verisi tek kaynağa taşındı. Bellek
endişesi ölçümle çürütüldü (önce 13,8 MB, yeni veriyle ~39 MB — 1024 MB'lık fonksiyonda hâlâ
kısıt değil); gerçek kısıt soğuk başlangıç çıktı ve tembel yüklemeyle çözüldü. Embedded SQLite
gerekmedi. Posta kodu araması C7'den sonra o tembel veri setine hiç dokunmuyor.

**C6 neden yapılmadı:** diğer beş kalem veri işiydi — kaynak bul, dönüştür, doğrula, sun.
C6 bir veri seti değil, **kalıcılık gerektiren bir alt uygulama**: anahtar üretimi ve saklama,
e-posta ile self-service kayıt, kullanım istatistikleri, yönetim arayüzü (FR-11, FR-12).
Sıfıra inen serverless'ta kalıcılık harici bir veritabanı demek, yani hem yeni bir bağımlılık
hem de "veritabanı yok" mimari kararının (BRD §7.2) ilk istisnası. Ayrı planlanmayı hak
ediyor; okuma API'sinin stateless kalması şartı §5'te zaten yazılı.

**Faz 3:** IBAN/TC checksum saf hesaplamadır (veri yok, en ucuz kazanç — Faz 1'e çekilebilir).
Döviz kurları **tek dinamik veri**: TCMB XML çekimi + zamanlanmış görev + farklı cache
politikası gerektirir, statik mimariden ilk sapmadır.

---

## 6. Karar bekleyen (BRD §12)

BRD'nin kendi önerileri varsayım olarak alındı:
- Anahtar portalı → **Faz 2**
- Dokümantasyon → **İngilizce ana, Türkçe ek** — OpenAPI endpoint özetleri için uygulandı: `WithBilingualSummary`
  ile her endpoint iki metin taşıyor, iki ayrı doküman servis ediliyor (`/openapi/v1.json` İngilizce
  varsayılan, `/openapi/v1-tr.json` Türkçe), Scalar'da seçilebiliyor (§7 B5). Tek dokümanı istek
  başına çevirmek yerine iki döküman seçildi çünkü `OpenApiOperationTransformerContext`'in
  `HttpContext`'e erişimi yok — veri tarafındaki `?lang=` (FR-04) burada uygulanamıyor.

Planı gerçekten değiştiren tek soru **barındırma**: cloud + Cloudflare mı, on-prem K8s mi?
Faz 0'ın 13. maddesinin (deploy) somut içeriğini bu belirler. Diğer 13 madde bu karardan
bağımsız ilerler.

---

## 7. Kalan İşler (canlı takip)

> Bu bölüm projenin **tek iş listesi**. Bir iş bittiğinde durumu burada güncellenir; ayrı bir
> issue listesi ya da not tutulmaz. Durum: ⬜ bekliyor · 🔄 devam ediyor · ✅ bitti · ⏸️ bloke.

### A. Vercel'e geçiş

Barındırma kararı **Vercel** (1 Eylül 2026). Vercel'in .NET runtime'ı yok ama Vercel Functions
özel OCI container image çalıştırıyor, yani mevcut Dockerfile kullanılabilir. Bu kararın
mimariye dokunan sonuçları:

| # | İş | Gerekçe | Durum |
|---|---|---|---|
| A1 | Katmanlı rate limiting: kenarda WAF, uygulamada in-memory | Kötüye kullanım compute faturalanmadan kenarda durduruluyor; Redis opsiyonel yükseltme yolu olarak duruyor | ✅ |
| A2 | Veri yanıtlarını CDN'e ittir | Fonksiyondan yanıt sunmak her istekte Active CPU faturası | ✅ |
| A3 | `Dockerfile.vercel` + `$PORT` binding | Vercel kökte bu dosyayı arıyor; port varsayılanı 80, `PORT` ile değişiyor | ✅ |
| A4 | `vercel.json` (bölge) | Bölge `fra1` — hedef kitle Türkiye | ✅ (ilk deploy'da `functions.Dockerfile.vercel` hatası düzeltildi, 2 Eylül 2026) |
| A5 | `ProxyOptions` Vercel profili | Yapılmazsa tüm trafik tek IP görünür, anonim limit ilk gün kilitlenir | ✅ |
| A6 | Gözlemlenebilirlik: Prometheus → Vercel OTel | Sıfıra inen serverless'ta kazınacak bir şey yok; her örnek kısmi veri tutar (NFR-16/17) | ⬜ |
| A7 | NFR-10 hedefini "sıcak örnek P95" olarak düzelt | Vercel 5 dk trafiksizlikte sıfıra iniyor; soğuk başlangıç 100 ms'yi aşar | ⬜ |
| A8 | Runtime imajı `-chiseled` → `-chiseled-extra` | İlk canlı istek `CultureNotFoundException` ile 500 verdi (bkz. aşağıda) | ✅ (2 Eylül 2026) |

**Ölçüm:** süreç başlangıcı ~300 ms (ilk çalıştırma 5,8 sn ama o disk cache ısınması). Gerçek
soğuk boot buna container açılışını ekler, arşivlenmiş fonksiyonda Vercel +1 sn diyor.

**Cloudflare düştü:** BRD §7.1'deki CDN satırı geçersiz, Vercel kendi CDN'ini getiriyor.

#### A1-A5 nasıl yapıldı

**A1 — Katmanlı rate limiting.** Önce Redis zorunlu sanılmıştı; Vercel'in **kendi WAF rate
limiting'inin Hobby planda ücretsiz olduğu** görülünce mimari değişti. Doğru katman ayrımı:

| Katman | Ne yapıyor | Neden orada |
|---|---|---|
| **Kenar (Vercel WAF)** | IP başına dakikalık burst limiti | Kötüye kullanımı **compute faturalanmadan** durduruyor; kenar her isteği gördüğü için sayım kesin |
| **Uygulama (in-memory)** | Günlük pencere, katman ayrımı (anonim/anahtar/toplu), `X-RateLimit-*` header'ları | Kenarın ifade edemediği her şey |

Kenarın iki sınırı bu bölünmeyi zorunlu kılıyor: Hobby'de **proje başına 1 kural** ve pencere
**en fazla 10 dakika** — yani günlük limit kenarda kurulamıyor. Sayaçlar bölge bazlı ama
`vercel.json` tek bölgeye (`fra1`) sabitlendiği için pratikte kesin.

**Uygulama içi sayaçlar neden yeterli:** CDN sayesinde origin'e trafiğin ~%5'i ulaşıyor
(BRD §10 hedefi cache hit > %95). BRD §2.2'nin 1M istek/ay hedefinde bu ortalama 0,02 istek/sn
demek — Vercel bu yükte neredeyse her zaman **tek örnek** çalıştırır ve in-memory sayım o
durumda tam doğrudur. Ölçeklenme yalnızca ani yüklerde olur, ki orada zaten asıl işi kenar
kuralı yapıyor.

**Ne kaybediyoruz:** ölçeklenme anında günlük limit ve header'lar yaklaşık hale geliyor. Bu
NFR-04'ün lafzından sapma, ama *niyetini* (örnekler arası tutarlı limit) kenar kuralı daha iyi
karşılıyor. BRD NFR-20 zaten "SLA taahhüdü verilmez, best effort" duruşunu kuruyor; limitler
de bu çerçevede yayınlanıyor.

**Redis kodu duruyor.** `IRateLimitStore` seam'i sayesinde seçim bir yapılandırma meselesi —
bağlantı dizesi verilirse Redis, verilmezse in-memory. Sıfır maliyetle duruyor ve API anahtar
portalı (C6) gelip **kişi başı günlük kotanın kesin olması gerektiğinde** yükseltme yolu
olarak hazır.

Teknik tarafta `RateLimitWindow` iki store'un da paylaştığı tip: pencere hizalaması, sayaç
anahtarı ve karar üretimi tek yerde. Bu, "store değişimi sayaçların yerini değiştirir,
davranışını değil" iddiasını kod seviyesinde garantiliyor. Redis tarafı tek Lua script'i:
`INCR` ve `PEXPIRE` atomik. İki round trip olsaydı aradaki bir çökme anahtarı süresiz bırakır
ve çağıranı pencere kapanana kadar değil **kalıcı olarak** kilitlerdi. Redis erişilemezse
**fail open** — ücretsiz, salt-okunur bir açık veri API'sinde kesintinin her isteği 500'e
çevirmesi, kısa süreli ölçümsüz trafikten daha kötü bir arızadır.

**Yapılandırma dürüstlüğü:** `EdgeBurstProtection` ayarı, in-memory sayımın bilinçli bir
tercih mi yoksa gözden kaçmış bir eksik mi olduğunu uygulamaya söylüyor. Bilinçliyse
başlangıçta düzenlemeyi *bildiriyor*; değilse **uyarı** basıyor. Yanlış yapılandırma sessizce
üretime çıkmasın.

**Kurulacak kenar kuralı** (Vercel → Project → Firewall → New Rule):
`If Request Path starts with /api/` → `Then Rate Limit` → 30 istek / 60 sn, anahtar `IP`,
aksiyon `Deny (429)`.

**A2 — CDN.** Kenar için ayrı cache direktifi verildi: tarayıcı `max-age=86400`, kenar
`max-age=31536000` + `stale-while-revalidate`. Böylece veri düzeltmesi bir sonraki deploy'da kullanıcıya ulaşıyor
(deploy kenarı temizler), buna karşılık kenar neredeyse her şeyi origin'e hiç dokunmadan
yanıtlıyor. Sıfıra inen bir platformda **uyanmayan container hiç faturalanmıyor** — maliyet
modelinin (BRD §8) en büyük kaldıracı bu.

**A3 — Port.** Chiselled runtime imajında **shell yok**, yani `sh -c` ile `$PORT`
genişletilemiyor. Portu uygulamanın kendisi okuyor; açık `ASPNETCORE_URLS` yine kazanıyor, o
yüzden yerel geliştirme ve launchSettings etkilenmiyor. Ayrıca Vercel kenarında TLS
sonlandığı ve container'a düz HTTP geldiği için, proxy arkasındayken `UseHttpsRedirection`
kapatıldı — açık kalsa yönlendirme döngüsü olurdu.

**A5 — Proxy.** Sağlayıcı **tahmin edilmiyor, isimlendiriliyor** (`None`/`Vercel`/
`Cloudflare`/`Generic`), çünkü güvenli header platforma göre değişiyor ve yanlış tahmin iki
kötü şekilden birine düşüyor: fazla güvenirsen çağıran kendi adresini uydurur, fazla katı
olursan herkes tek kovaya düşer. Varsayılan `None` — doğrudan açıktayken hiçbir forwarded
header'a güvenilmiyor.

Vercel tarafında `KnownProxies` listelemeye gerek yok: Vercel `X-Forwarded-For`'u kendisi
üzerine yazdığını ve dış IP'leri **iletmediğini** açıkça belgeliyor, gerekçesi de tam olarak
IP spoofing'i önlemek. Birincil olarak `x-vercel-forwarded-for` okunuyor — Vercel'in üstüne
bir proxy konsa bile o korunuyor.

**A4 — vercel.json, ilk gerçek deploy'da bulunan hata.** İlk sürüm `functions` altında
`"Dockerfile.vercel": { memory, maxDuration }` taşıyordu — B3'ün işaret ettiği "hiç
doğrulanmadı" riski tam burada gerçekleşti. Vercel build'i `Error: The pattern
"Dockerfile.vercel" defined in \`functions\` doesn't match any Serverless Functions inside
the \`api\` directory` ile reddetti: `functions` anahtarı yalnızca `api/` altındaki dosya
yollarını glob'lar, kökteki otomatik algılanan `Dockerfile.vercel`'i değil — container
image fonksiyonları için ayrı bir `services` yapılandırması var ve tek servisli bu kurulumda
hiç gerekmiyor (Vercel dosyayı kökte kendisi bulup tüm trafiği yönlendiriyor). Ayrıca
`memory` zaten Fluid compute açıkken vercel.json'dan ayarlanamıyor, proje panosundan
(Functions bölümü) ayarlanması gerekiyor. **Karar:** `functions` bloğu tamamen kaldırıldı;
bölge (`fra1`) dışında vercel.json'da fonksiyon ayarı yok.

**A8 — chiseled imaj ICU'suz çıktı.** vercel.json düzeldikten sonraki ilk gerçek istekte
`ReferenceOrdering`'in statik constructor'ı `CultureNotFoundException: Only the invariant
culture is supported in globalization-invariant mode` ile patladı — `tr-TR` "geçersiz kültür
tanımlayıcısı" oldu. Sebep: `mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled` (hem
`Dockerfile` hem `Dockerfile.vercel`'de kullanılan taban imaj) ICU/tzdata içermiyor ve
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`'yu imajın içine gömülü getiriyor — "shell yok,
küçük yüzey" seçimi (§2 madde 13) globalization'ı hiç hesaba katmadan yapılmıştı. Türkçe
collation ise süsleme değil: FR-04/§3.7'nin ta kendisi, il/ilçe/semt listelerinin varsayılan
sıralaması buna bağlı.

**Karar:** her iki Dockerfile'da taban imaj `9.0-noble-chiseled-extra`'ya çevrildi — ICU ve
tzdata içeren, "shell yok" özelliğini koruyan varyant. `9.0-noble-chiseled-extra` etiketi
gerçek imajı barındırıyor, `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` da ayarlanmıyor. Not:
Docker imajının build'i bu commit'e kadar hiç yerelde de doğrulanmamıştı (B3) — hata ancak
canlıda, ilk istekte ortaya çıktı.

### B. Faz 1'den kalan yayın işleri

BRD Faz 1 teslimatı "dokümantasyon, açık kaynak yayın" diyor. Kod tarafı bitti, yayın bitmedi.

| # | İş | Durum |
|---|---|---|
| B1 | `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, issue/PR şablonları | ⬜ |
| B2 | GitHub'da yayınla (public repo, açıklama, topics) | ⬜ |
| B3 | **Docker imajını doğrula** — ilk Vercel deploy'u `vercel.json` aşamasında düştü (bkz. §7 A4), imaj build'i henüz görülmedi | ⬜ |
| B4 | NuGet paketleri (`OpenData.Turkey` vb.) — BRD §7.2 bonus, mimari hazır | ⬜ |
| B5 | OpenAPI özetleri iki dilde (§6 kararı) | ✅ (2 Eylül 2026) |

### C. Faz 2

| # | İş | Not | Durum |
|---|---|---|---|
| C2 | Resmi tatiller | 2015-2050, dini tatiller hesaplamalı + düzeltme katmanı | ✅ |
| C3 | Telefon kodları | TR alan kodları + 50 GSM öneki | ✅ |
| C4 | Mahalle/köy | 73.552 yerleşim, tembel yükleniyor (ölçüm aşağıda) | ✅ |
| C5 | Posta kodları | Semt seviyesinde, 2.433 kod; başlangıçta yükleniyor | ✅ |
| C7 | TR adres verisini tek kaynağa taşı | il/ilçe/**semt**/mahalle tek derlemeden; ölçüm ve gerekçe aşağıda | ✅ |
| C6 | API anahtar portalı | **Yapılmadı** — diğerlerinden farklı türde iş, aşağıya bak | ⬜ |

#### C1-C5 ve C7 nasıl yapıldı

**C2 — Resmi tatiller.** Tarihler istek anında hesaplanmıyor, bir yıl aralığı için önceden
üretilip commit'leniyor (2015-2050, 621 kayıt). Böylece API saf bir aramaya dönüşüyor ve —
daha önemlisi — dini tatiller **gözden geçirilebilir** oluyor: Diyanet'le uyuşmayan bir tarih
kod değişikliği ve deploy gerektirmeden, PR ile düzeltilebiliyor.

Dini tarihler UmAlQura takviminden geliyor. 2023-2026 için Diyanet'in yayımladığı tarihlerle
birebir tutuyor, ama **otorite Diyanet ve Resmî Gazete**, bu araç değil. Ayrılan bir yıl için
`data/overrides/holidays.tr.json` var. Testler 2024-2026 tarihlerini sabitliyor: yeniden
üretim bunlardan birini kaydırırsa hesaplama ayrışmış demektir.

Yarım günler ayrı kayıt: arife öğleden sonrası ve 28 Ekim öğleden sonrası çalışılmıyor ama
sabahları çalışılıyor — iş günü hesaplayan bir çağıranın bunları kendi tarihlerinde
adresleyebilmesi gerekiyor.

**C3 — Telefon kodları.** Kaynak Google libphonenumber (Apache-2.0). GSM önekleri
**operatör adı taşımıyor**. Türkiye'de 2008'den beri numara taşınabilirliği var,
yani bir önek numaranın *ilk tahsis edildiği* operatörü gösterir, bugün kimin taşıdığını
değil — libphonenumber da Türkiye'yi taşınabilir bölge olarak işaretliyor. Operatör alanı
yayınlamak, otoriter görünen ve milyonlarca numara için yanlış olan veri olurdu. Önekler
doğrulama için sunuluyor.

**C4/C5/C7 — Adres hiyerarşisi.** İlk uygulama iki ayrı upstream'e dayanıyordu: il/ilçe
turkiye-api'den, mahalle/köy yine oradan. 2 Eylül 2026'da tamamı **bu proje için derlenmiş
tek bir Türkçe export'a** taşındı (MIT). Taşımanın üç sonucu var.

**1. Semt seviyesi kazanıldı.** Türk adres hiyerarşisi dört katmanlı: il → ilçe → **semt** →
mahalle/köy. Eski veri semti hiç taşımıyordu ve posta kodunu her yerleşime kopyalıyordu. Yeni
veride posta kodu ait olduğu yerde: **2.433 semt, 2.433 tekil posta kodu**, birebir. Bu bir
modelleme kazancından fazlası:

| | Eski | Yeni |
|---|---|---|
| Posta kodu araması | 50.437 satırlık tembel veri setini yüklüyordu | 2.433 satırlık, başlangıçta yüklü `QuarterStore` |
| Kod → yer | Liste (kaç satır döneceği belirsiz) | Tek semt nesnesi |
| Kodun sahibi | Yok; 50 binde tekrarlanan bir değer | Semt; tekillik `DataIndex` ile başlangıçta zorlanıyor |

`/postal-codes/{code}` artık **soğuk başlangıçta 20 MB'lık dosyayı hiç açmıyor**. Yerleşimler
isteniyorsa `/postal-codes/{code}/neighborhoods` var, bedeli orada ödeniyor.

**2. Kimlik şeması sadeleşti.** Eski birleşik anahtar (`kind * 1.000.000 + upstreamId`) iki
upstream dosyanın satırlarını bağımsız numaralaması yüzünden gerekiyordu — 2.731 kimlik
çakışıyordu. Tek kaynakta böyle bir çakışma yok; `MahId` doğrudan kimlik. `upstreamId` ve
türetilmiş anahtar kalktı.

**3. Nüfus/alan/koordinat düştü.** Eski il ve ilçe kayıtları TÜİK nüfusu, yüzölçümü, bölge ve
koordinat taşıyordu. Yeni kaynakta yok ve **birleştirilmedi**: ilçe kimlikleri iki kaynak
arasında ortak değil, yani birleştirme ancak isim eşlemesiyle olurdu ve sessizce yanlış
eşleşen satırlar üretirdi. Aynı kayıtta iki farklı vintage yayınlamak, çağıranın hangisinin
hangisi olduğunu ayırt edemeyeceği bir veri kalitesi sorunudur. `sort=population` ve
`sort=area` kalktı; yerlerine `sort=districts`, `sort=quarters`, `sort=settlements` geldi —
bunlar veriden türetiliyor ve testlerle dosyayla uyumu doğrulanıyor.

**Mahalle/köy ayrımı isimden türetiliyor.** Kaynak ayrımı isim içinde kodluyor, üç şekilde:
"X KÖYÜ" köy, "X MAH" kentsel mahalle, "X MAH (Y KÖYÜ)" ise Y köyüne bağlı mahalle. Bu üçüncü
şekil 31.391 satır — mahalle işaretine önce bakılmazsa hepsi köy sayılırdı. Köy adı ayrıca
`villageName` olarak yayınlanıyor. Tuzak: "YENİMAHALLE KÖYÜ" bir **köy**; "MAH" kelime sınırı
aranmazsa mahalle sanılır (naif bir regex bu iki satırı yanlış sınıflandırmıştı).

**Yeniden ölçüm.** Veri %46 büyüdü (50.437 → 73.552 satır, 15 MB → 20 MB), maliyet de büyüdü:

| Ölçüm | Eski (50.437) | Yeni (73.552) |
|---|---|---|
| JSON ayrıştırma | 436 ms | ~350-500 ms |
| + arama indeksi (ilk kullanım) | 521 ms | **~1,1-1,4 sn** |
| Yönetilen bellek | 13,8 MB | **~39 MB** |
| Semt + il/ilçe (başlangıçta) | — | 71-79 ms / 2.433 semt |

Bellek hâlâ kısıt değil (`vercel.json` 1024 MB). Asıl artış **indeksleme**de: artık ilçe ve
semt için iki gruplama var. Bu, tembel yükleme kararını iptal etmiyor, **güçlendiriyor** —
ödenmesi gereken maliyet daha da büyük ve hâlâ yalnızca yerleşim isteyen ilk istek ödüyor.

İndeks tarafında bir şey kesildi: `villageName` arama anahtarı **değil**, çünkü `name`'in
birebir alt dizesi — isim anahtarı onun eşleşebileceği her terimi zaten eşliyor. 73.552 satırda
dördüncü bir anahtar, hiçbir şey için harcanan bellekti.

**Ham export commit'lenmiyor.** Bir süre `data/source/` altında tutuldu, sonra çıkarıldı:
üretmediği hiçbir şey için okunmayan 16 MB, ürettiği 21 MB'ın yanında duruyordu. `datatool
turkey --source <dir>` dönüşümü hâlâ yapıyor ama artık kopyayı elinde tutan için; **doğruluk
kaynağı üretilmiş dört dosya**.

Bu, denetlenebilirliği kaybetmek anlamına gelmiyor — **yerini değiştiriyor**. Eskiden iddia
"girdiden yeniden üret, karşılaştır"dı; şimdi bütünlük testleri: her slug ismiden yeniden
türetiliyor, her denormalize üst isim, her bildirilen alt sayı ve her posta kodu geldiği
dosyaya karşı yeniden doğrulanıyor. Hiyerarşiyle çelişen elle düzenleme CI'da düşüyor. Dört
dosya yine **tek komutla birlikte** üretiliyor: kimlikler yalnızca birbirlerine göre anlamlı,
kısmi yeniden üretim yüklenen ama yanlış cevaplayan bir hiyerarşi verirdi.

**Filtresiz yerleşim listesi hâlâ reddediliyor**: 73.552 satırı 500'erlik sayfalarla gezmek,
toplu indirme uçlarının başka yerde ortadan kaldırdığı kazıma davranışının ta kendisi. `/all`
de yok — 20 MB bir yanıt değil, bir indirmedir; isteyen dosyayı repodan alır. Semtlerin `/all`
u **var**: 2.433 satır tek yanıtta makul ve posta kodu tablosunun tamamını veriyor.

### D. Faz 3

| # | İş | Not | Durum |
|---|---|---|---|
| D1 | IBAN doğrulama | Saf hesaplama, veri yok — en ucuz kazanç | ⬜ |
| D2 | TC kimlik checksum | Saf hesaplama, KVKK açısından güvenli | ⬜ |
| D3 | Bankalar (EFT/SWIFT kodları) | TCMB | ⬜ |
| D4 | Vergi daireleri | GİB | ⬜ |
| D5 | Üniversiteler | YÖK | ⬜ |
| D6 | NUTS / İBBS bölgeleri | TÜİK | ⬜ |
| D7 | Döviz kurları | **Tek dinamik veri** — zamanlanmış görev + farklı cache politikası | ⬜ |
| D8 | Hicri-Miladi çevrim, dini günler | Diyanet | ⬜ |

### E. Teknik borç ve ertelenenler

| # | Konu | Durum |
|---|---|---|
| E1 | XML yanıt (FR-01) | ⏸️ Faz 2'ye ertelendi |
| E2 | OpenAPI 3.1 (şu an 3.0, .NET 9 varsayılanı) | ⬜ |
| E3 | AOT değerlendirmesi | ⏸️ Engeller ölçüldü (§3.9), karar Faz 2'de |
| E4 | Türkçe para birimi adları (43/152) | ⬜ Katkıya açık |
| E6 | Admin/veri güncelleme uçları (NFR-15) | ⬜ Şu an her şey CI/CD ile |
| E7 | Public status sayfası (NFR-18) | ⬜ |
| E8 | Deprecation politikası (NFR-21/22) | ⬜ |
| E9 | Alan adı ve marka (BRD §12.1) | ⬜ **Kalan tek BRD sorusu** |
