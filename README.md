# OpenData API

**[English](README.en.md)**

Geliştiriciler için ücretsiz, hızlı, dokümante edilmiş referans veri — Türkiye'nin tüm adres
hiyerarşisi, para birimleri, diller ve resmî tatiller. Kayıt gerekmez.

.NET 9 minimal API'lerle yazıldı. Tüm veri bu depoya commit'lenir, başlangıçta belleğe
yüklenir ve değişmez koleksiyonlardan sunulur: çalışma zamanında ne veritabanı ne de dış
çağrı vardır.

> Durum: **Faz 2** — Türk adres hiyerarşisi (81 il, 972 ilçe, 2.433 semt, 73.552 mahalle ve
> köy), posta kodları, para birimleri, diller ve resmî tatiller.
> Yol haritası için [docs/PLAN.md](docs/PLAN.md), iş gereksinimleri için
> [acik-veri-api-brd.md](acik-veri-api-brd.md) (BRD) belgelerine bakın.

## Hızlı başlangıç

```bash
dotnet run --project src/YaMiSoFt.OpenData.Api
```

Etkileşimli API referansı için `/docs`'u açın.

```bash
# Türkiye illeri — plaka kodu, slug veya görünen ada göre
curl localhost:5096/api/v1/provinces/34
curl localhost:5096/api/v1/provinces/kahramanmaras
curl "localhost:5096/api/v1/provinces/34/districts"

# Arama her yerde büyük/küçük harf ve Türkçe karakter duyarsız
curl "localhost:5096/api/v1/districts?search=besiktas"

# Para birimleri küsurat basamak sayısını taşır, tutar doğru yuvarlanır
curl localhost:5096/api/v1/currencies/JPY     # decimalDigits: 0
curl localhost:5096/api/v1/currencies/KWD     # decimalDigits: 3

# Sadece ihtiyacınız olan alanlar
curl "localhost:5096/api/v1/provinces?fields=id,name,districtCount&sort=districts&order=desc"

# Tüm veri seti tek istekte; yerel olarak önbelleğe alın
curl localhost:5096/api/v1/provinces/all

# Resmî tatiller, hesaplanan dini tatiller dahil
curl localhost:5096/api/v1/holidays/2026

# Adres hiyerarşisinde aşağı: il -> ilçe -> semt -> yerleşim
curl "localhost:5096/api/v1/districts/430/quarters"
curl "localhost:5096/api/v1/quarters/1154/neighborhoods"

# Posta kodları semt başına atanır, bir kod tek bir semte çözümlenir
curl localhost:5096/api/v1/postal-codes/34357
curl localhost:5096/api/v1/postal-codes/34357/neighborhoods
```

## Uç noktalar

Her liste uç noktası aynı parametreleri kabul eder: `page`, `pageSize` (en fazla 500),
`search`, `sort`, `order`, `fields`, `lang`. Her birinin, tüm veri setini tek yanıtta dönen
bir `/all` kardeşi vardır.

| Rota | Notlar |
|---|---|
| `/api/v1/provinces` · `/{idOrSlug}` | Plaka kodu, slug veya görünen ad. `name`, `id`, `districts` ile sırala |
| `/api/v1/provinces/{idOrSlug}/districts` | Bir ilin ilçeleri |
| `/api/v1/districts` · `/{id}` | `name`, `id`, `province`, `quarters` ile sırala |
| `/api/v1/districts/{id}/quarters` | Bir ilçenin semtleri |
| `/api/v1/quarters` · `/{id}` | `provinceId` veya `districtId` ile filtrele. `name`, `id`, `postalCode`, `settlements` ile sırala |
| `/api/v1/quarters/{id}/neighborhoods` | Bir semtin yerleşimleri |
| `/api/v1/currencies` · `/{code}` | ISO 4217, sembol ve küsurat basamağıyla |
| `/api/v1/languages` · `/{code}` | ISO 639-1 veya 639-2, yerel adlarla |
| `/api/v1/holidays/{year}` | Türkiye'nin tatil günleri, 2015-2050 |
| `/api/v1/neighborhoods` · `/{id}` | `provinceId`, `districtId`, `quarterId`, `kind` veya `search` ile filtrele |
| `/api/v1/districts/{id}/neighborhoods` | Bir ilçenin yerleşimleri |
| `/api/v1/postal-codes/{code}` | Beş haneli kodun atandığı semt |
| `/api/v1/postal-codes/{code}/neighborhoods` | Bir kodun kapsadığı yerleşimler |
| `/health/live` · `/health/ready` | Canlılık ve hazır olma |
| `/metrics` | Prometheus scrape uç noktası |
| `/docs` · `/openapi/v1.json` | Scalar arayüzü ve OpenAPI belgesi |

Hatalar [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) problem belgeleridir.

## Hız sınırları

| Katman | Limit | Tanımlama |
|---|---|---|
| Anonim | 30/dk, 1.000/gün | İstemci adresi |
| Ücretsiz API anahtarı | 120/dk, 10.000/gün | `X-API-Key` başlığı |
| Toplu indirme (`/all`) | 5/dk, 100/gün | Aynı, ayrı sayaç |

Limitler en iyi çaba esasıyla uygulanır, garanti değildir: dakikalık patlama sınırı CDN
kenarında zorlanır, günlük rakamlar uygulama içinde sayılır.

Her yanıt `X-RateLimit-Limit`, `X-RateLimit-Remaining` ve `X-RateLimit-Reset` taşır. Bir
reddedişte `Retry-After` eklenir.

**Lütfen önbelleğe alın.** Yanıtlar ETag'li `public, max-age=86400` ile gelir. Tüm bir veri
seti gerekiyorsa sayfaları gezmek yerine `/all`'u bir kez çekin — hem sizin için daha ucuz
hem de bu servisi ücretsiz tutan şey bu.

## Veriye katkı

Veri `data/` altında JSON olarak durur ve kod gibi incelenir. Değiştirmek için:

1. JSON'u düzenleyin, ya da yeniden üretin:
   ```bash
   dotnet run --project tools/YaMiSoFt.OpenData.DataTool -- all                    # her veri seti
   dotnet run --project tools/YaMiSoFt.OpenData.DataTool -- turkey --source <dir>  # TR hiyerarşisinin tamamı
   ```
2. Bütünlük paketini çalıştırın: `dotnet test`
3. PR açın. CI aynı kontrolleri yeniden çalıştırır — kod tekilliği, il → ilçe → semt →
   yerleşim zincirinin tamamında referans bütünlüğü, slug güvenliği, ilin posta koduyla
   eşleşen ve tek bir semte özgü posta kodları, ve her bildirilen alt sayının altındaki
   dosyayla uyumu.

Veri aracı build sırasında hiç çalışmaz. Çıktısı commit'lidir, böylece build'ler
deterministik olur, çevrimdışı çalışır ve sunduğu her şeyi taşıyan bir container imajı
üretir. Her veri değişikliği git geçmişinde görünür.

**Türk hiyerarşisi bir build çıktısı gibi değil, veri gibi düzenlenir.** Dört adres dosyası,
commit'li **olmayan** bir Türkçe export'tan üretildi — üretmediği 21 MB'ın yanında okunmayan
16 MB girdi. `datatool turkey --source <dir>` kopyayı elinde tutan için dönüşümü hâlâ yapar
ve dört dosyayı birlikte yazar: birini diğerleri olmadan yeniden üretmek, kimlikleri artık
uyuşmayan bir hiyerarşi verirdi, bu yüzden komut buna izin vermez.

Normalde commit'li JSON'u doğrudan düzenlersiniz. Bunu güvenli tutan şey bütünlük paketidir
— her slug'ı isminden yeniden türetir, her denormalize edilmiş üst ismi, her bildirilen alt
sayıyı ve her posta kodunu geldiği dosyaya karşı yeniden doğrular. Hiyerarşinin geri
kalanıyla çelişen bir düzenleme, yayınlanmak yerine CI'da düşer.

**Küratörlü katmanlar.** Bir değer hiçbir upstream'den türetilemiyorsa `data/overrides/`
altında durur ve üretim anında birleştirilir. `currencies.tr.json` bunun ilk örneği: ICU bir
para birimini İngilizce ve kendi ana dilinde adlandırabiliyor ama Türkçe adlandıramıyor, bu
yüzden Türkçe adlar elle bakımlı. Veri aracı, artık var olmayan bir kodu adlandıran bir
katman görürse hata verir; testler aynısını commit'li çıktı üzerinden de doğrular.

## Vercel'e dağıtım

Vercel'in .NET runtime'ı yok ama Vercel Functions özel OCI container imajları çalıştırıyor,
yani repo kökündeki `Dockerfile.vercel` yeterli. Üretimde iki ayar isteğe bağlı değildir:

```bash
# Bu olmadan, her istek Vercel'in kenarından geliyormuş gibi görünür ve anonim katman
# tüm dünya için tek bir kovaya çöker.
OpenData__Proxy__Provider=Vercel

# Bir kenar kuralının patlamaları yönettiğini belirtir; uygulama içi sayaçlar bilinçli
# bir tercih olur.
OpenData__RateLimit__EdgeBurstProtection=true
```

Ardından kenar kuralını ekleyin, **Project → Firewall → New Rule**:

| Alan | Değer |
|---|---|
| If | Request Path starts with `/api/` |
| Then | Rate Limit |
| Window / limit | 60s / 30 istek |
| Key | IP |
| Action | Deny (429) |

**Hız sınırlaması bilinçli olarak katmanlıdır.** Servisi asıl koruyan kenar kuralıdır: kötüye
kullanımı hiçbir compute faturalanmadan önce reddeder ve her isteği gördüğü için doğru sayar.
Uygulamanın kendi sayaçları, bir kenar kuralının ifade edemediği şeyleri kapsar — günlük
pencere, katman başına kotalar ve `X-RateLimit-*` başlıkları.

Bu uygulama içi sayaçlar tek örnekte kesin, Vercel ölçeklendiğinde yaklaşıktır. Bu kabul
edilen bir ödünleşim: CDN trafiğin çoğunu emdiği için origin isteklerin küçük bir kısmını
görür ve genellikle tek örnek çalıştırır; çalıştırmadığı anlar da tam olarak kenar kuralının
asıl işi yaptığı anlardır. Çağıran başına günlük kotanın kesin olması gerekiyorsa — ki API
anahtar portalı bunu isteyecek — `OpenData__RateLimit__RedisConnectionString` ayarlanır ve
sayım davranış değişikliği olmadan Redis'e taşınır; pencere semantiği iki store arasında
paylaşılır.

Uygulama, ne bir kenar kuralı ne de Redis yapılandırılmışsa başlangıçta bir uyarı loglar,
biri yapılandırılmışsa düzenlemeyi bildirir. `appsettings.Vercel.json` profili taşır.

Geri kalanı platformdan geliyor: container portunu `PORT`'tan okur, TLS kenarda sonlandığı
için uygulama HTTPS'e yönlendirmez, ve yanıtlar `CDN-Cache-Control` taşır — böylece kenar,
container'ı hiç uyandırmadan çoğu isteği yanıtlar, ki sıfıra inen bir platformda bu bir
isteğin faturalanıp faturalanmaması arasındaki farktır.

Örnekler beş dakika trafiksizlikten sonra sıfıra iner. Süreç başlangıcı yaklaşık 300 ms
sürer; gerçek bir soğuk başlangıç bunun üzerine container açılışını ekler, yani BRD'nin
"P95 < 100 ms" hedefi yalnızca sıcak örnekler için geçerlidir.

## Bilinen eksikler

- **Dini tatil tarihleri hesaplamalıdır**, resmî değildir. Son yıllarda Diyanet'in
  yayımladığı tarihlerle birebir tutmuştur, ama otorite Diyanet ve Resmî Gazete'dir. Yasal
  veya bordro yükümlülüğü için kontrol etmeden güvenmeyin.
- **GSM önekleri operatör taşımaz.** Türkiye'de 2008'den beri numara taşınabilirliği var,
  bir önek bugün kimin taşıdığını değil, ilk tahsis edildiği operatörü kaydeder.
- **Yerleşimler ilk kullanımda yüklenir**, başlangıçta değil: 73.552 satırı ayrıştırmak
  kabaca 1-1,5 saniye tutar, bu da aksi halde her soğuk başlangıca binerdi. Onlara gerçekten
  ihtiyaç duyan ilk istek bu bedeli öder.
- **Türkçe para birimi adları** katmandaki 43 para birimini kapsar; kalanı İngilizce adına
  düşer. `data/overrides/currencies.tr.json`'a bir satır eklemek tek satırlık bir PR'dır.
- **XML yanıtları** (BRD FR-01) ertelendi; API şimdilik yalnızca JSON.
- **Countries, flags ve time zones kaldırıldı.** Önceki fazların parçasıydılar; nedeni için
  [DATA-LICENSES.md](DATA-LICENSES.md) dosyasına bakın.

## Lisans

Kod [MIT](LICENSE) lisanslıdır. Veri kendi koşullarını taşır — bkz.
[DATA-LICENSES.md](DATA-LICENSES.md).
