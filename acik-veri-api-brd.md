# İş Gereksinimleri Dokümanı (BRD)
## Açık Veri API Platformu — "OpenData API"

**Versiyon:** 1.0
**Tarih:** 31 Ağustos 2026
**Hazırlayan:** Yakup
**Durum:** Taslak

---

## 1. Yönetici Özeti

Geliştiricilerin projelerinde sık ihtiyaç duyduğu referans verilerini (il/ilçe, mahalle, posta kodu, resmi tatiller vb.) ücretsiz, hızlı, güvenilir ve dokümante bir REST API olarak sunan bir platform geliştirilecektir. Platform C# / .NET ile yazılacak, açık kaynak olarak yayınlanacak ve topluluk katkısına açık olacaktır.

**Temel değer önerisi:** Türkiye odaklı verilerde (il/ilçe/mahalle, plaka, posta kodu, resmi tatiller) eksiksiz ve güncel tek kaynak olmak; global verilerde ise (para birimleri, diller) standartlara uygun, tutarlı bir alternatif sunmak.

---

## 2. Amaç ve Hedefler

### 2.1 Amaçlar
- Geliştiricilerin her projede tekrar tekrar topladığı referans verileri tek merkezden sunmak
- Ücretsiz ve kayıt gerektirmeyen (anonim erişimli) bir kamu hizmeti sağlamak
- Açık kaynak topluluk projesi olarak itibar ve görünürlük kazanmak

### 2.2 Ölçülebilir Hedefler (ilk 12 ay)
| Hedef | Metrik |
|---|---|
| Aylık aktif API tüketicisi | 1.000+ benzersiz IP/anahtar |
| Aylık istek hacmi | 1M+ istek |
| Uptime | %99,5 |
| P95 yanıt süresi | < 100 ms (cache hit) |
| GitHub yıldızı | 500+ |

---

## 3. Kapsam

### 3.1 Kapsam İçi — Veri Setleri

**Faz 1 (MVP):**
| Veri Seti | İçerik | Kaynak |
|---|---|---|
| Türkiye İl/İlçe | 81 il, tüm ilçeler, plaka kodu, telefon alan kodu, bölge, koordinat, nüfus | TÜİK, İçişleri Bakanlığı |
| Para Birimleri | ISO 4217 kod, sembol, ondalık hane, TR/EN isim | ISO |
| Diller | ISO 639-1/639-2 kodlar, yerel isim | ISO |

**Faz 2:**
| Veri Seti | İçerik |
|---|---|
| Mahalle/Köy | Türkiye mahalle ve köy listesi (il/ilçe hiyerarşisi altında) |
| Posta Kodları | Türkiye posta kodları (mahalle eşleşmeli) |
| Resmi Tatiller | Türkiye resmi ve dini tatilleri (yıl bazlı, dini tatiller hesaplamalı) |
| Telefon Kodları | Türkiye şehir alan kodları, GSM operatör ön ekleri |

**Faz 3 (Öneriler):**
| Veri Seti | İçerik | Not |
|---|---|---|
| Bankalar | Türkiye banka listesi, EFT/banka kodları, SWIFT/BIC | Kamuya açık TCMB verisi |
| IBAN Doğrulama | IBAN format + checksum doğrulama endpoint'i | Hesaplama servisi, veri değil |
| TC Kimlik Doğrulama | Algoritma bazlı format doğrulama (sadece checksum, sorgulama değil) | KVKK açısından güvenli |
| Vergi Daireleri | İl bazlı vergi dairesi listesi ve kodları | GİB verisi |
| Üniversiteler | Türkiye üniversite listesi, tür, şehir, kuruluş yılı | YÖK verisi |
| NUTS Bölgeleri | İBBS-1/2/3 istatistiki bölge sınıflandırması | TÜİK |
| Döviz Kurları | TCMB günlük kurlar (gösterge niteliğinde) | TCMB XML servisi |
| Burçlar/Takvim | Hicri-Miladi çevrim, dini günler | Diyanet takvimi |

### 3.2 Kapsam Dışı
- Kişisel veri içeren hiçbir veri seti (KVKK/GDPR riski)
- Gerçek zamanlı finansal işlem verileri (borsa vb.)
- Ücretli/lisans kısıtlı üçüncü parti veriler
- Adres doğrulama / geocoding (Faz 3 sonrası değerlendirilebilir)

---

## 4. Hedef Kitle ve Kullanım Senaryoları

| Persona | Senaryo |
|---|---|
| Mobil geliştirici | Kayıt formunda il/ilçe dropdown'ı doldurmak |
| Backend geliştirici | Sipariş adresinde il/ilçe doğrulaması yapmak |
| E-ticaret ekibi | Fatura ekranında vergi dairesi listesi çekmek |
| Fintech geliştirici | IBAN format doğrulaması, banka kodu eşleşmesi |
| Öğrenci/hobi | Demo projelerde gerçekçi referans verisi kullanmak |

---

## 5. Fonksiyonel Gereksinimler

### 5.1 API Tasarımı
- **FR-01:** RESTful, JSON öncelikli yanıt formatı; `Accept` header ile XML opsiyonel
- **FR-02:** URL tabanlı versiyonlama: `/api/v1/...`
- **FR-03:** Tüm liste endpoint'lerinde sayfalama (`page`, `pageSize`, max 500), sıralama ve alan filtreleme (`fields=name,code` ile partial response)
- **FR-04:** Çoklu dil desteği: `?lang=tr|en` veya `Accept-Language` header
- **FR-05:** Arama: `?search=` ile isim/kod üzerinde diakritik-duyarsız arama (İstanbul = istanbul = ISTANBUL)
- **FR-06:** Hiyerarşik erişim: `/api/v1/provinces/34/districts`, `/api/v1/districts/{id}/neighborhoods`
- **FR-07:** Toplu indirme: her veri setinin tamamı tek istekle JSON dump olarak indirilebilmeli (`/api/v1/provinces/all`) — istemcilerin veriyi lokalde cache'lemesini teşvik eder, sunucu yükünü azaltır
- **FR-08:** OpenAPI 3.1 şeması + Swagger UI + Scalar dokümantasyon arayüzü
- **FR-09:** Hata yanıtları RFC 9457 (Problem Details) formatında

### 5.2 Örnek Endpoint Yapısı
```
GET /api/v1/provinces
GET /api/v1/provinces/34
GET /api/v1/provinces/34/districts
GET /api/v1/districts/{id}/neighborhoods
GET /api/v1/currencies
GET /api/v1/languages
GET /api/v1/holidays/2026
GET /api/v1/validate/iban/TR330006100519786457841326
GET /api/v1/banks
```

### 5.3 API Anahtarı ve Katmanlar
- **FR-10:** Anonim erişim mümkün (kayıt bariyeri yok) ancak düşük limitli
- **FR-11:** Ücretsiz API anahtarı ile daha yüksek limit (e-posta ile self-service kayıt)
- **FR-12:** Anahtar yönetim portalı: anahtar oluşturma/iptal, kullanım istatistikleri görüntüleme

| Katman | Limit | Kimlik |
|---|---|---|
| Anonim | 30 istek/dk, 1.000 istek/gün | IP bazlı |
| Ücretsiz anahtar | 120 istek/dk, 10.000 istek/gün | API key bazlı |
| Sponsor/Partner | Özel limit | API key bazlı |

---

## 6. Fonksiyonel Olmayan Gereksinimler

### 6.1 Rate Limiting
- **NFR-01:** .NET yerleşik `RateLimiter` middleware (Fixed Window + Token Bucket kombinasyonu)
- **NFR-02:** Limit aşımında `429 Too Many Requests` + `Retry-After` header
- **NFR-03:** Her yanıtta `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset` header'ları
- **NFR-04:** Dağıtık senaryoda Redis tabanlı sayaç (birden çok pod'da tutarlı limit)
- **NFR-05:** Kötüye kullanımda IP/anahtar bazlı geçici engelleme (otomatik + manuel)

### 6.2 Performans ve Önbellekleme
- **NFR-06:** Veriler statik/yarı-statik olduğundan agresif cache: uygulama içi in-memory cache + `OutputCache` middleware
- **NFR-07:** HTTP cache header'ları: `Cache-Control: public, max-age=86400`, `ETag`, `304 Not Modified` desteği
- **NFR-08:** CDN uyumluluğu (Cloudflare ücretsiz katman önerilir — bant genişliği maliyetini ciddi düşürür)
- **NFR-09:** Response compression (Brotli/Gzip)
- **NFR-10:** P95 < 100 ms (cache hit), P95 < 500 ms (cache miss)

### 6.3 Güvenlik
- **NFR-11:** Zorunlu HTTPS, HSTS
- **NFR-12:** CORS: tüm origin'lere açık (public API doğası gereği), sadece GET/HEAD/OPTIONS
- **NFR-13:** Sadece okuma amaçlı API — hiçbir yazma endpoint'i public değil
- **NFR-14:** Güvenlik header'ları (X-Content-Type-Options vb.), request boyut limitleri
- **NFR-15:** Admin/veri güncelleme endpoint'leri ayrı, kimlik doğrulamalı (API dışı yönetim arayüzü veya CI/CD ile veri güncelleme)

### 6.4 Gözlemlenebilirlik
- **NFR-16:** OpenTelemetry ile trace/metric/log; Prometheus metrik endpoint'i
- **NFR-17:** Grafana dashboard: istek hacmi, endpoint dağılımı, rate limit hit oranı, cache hit oranı, hata oranı
- **NFR-18:** Public status sayfası (uptime, incident geçmişi)
- **NFR-19:** Health check endpoint'leri (`/health/live`, `/health/ready`)

### 6.5 Diğer
- **NFR-20:** Uptime hedefi %99,5 (ücretsiz servis için makul; SLA taahhüdü verilmez, "best effort" açıkça belirtilir)
- **NFR-21:** Veri güncellemeleri versiyonlu ve geriye dönük uyumlu; breaking change sadece major versiyonda
- **NFR-22:** Deprecation politikası: eski versiyon minimum 12 ay desteklenir

---

## 7. Teknik Mimari (Öneri)

### 7.1 Teknoloji Yığını
| Katman | Teknoloji | Gerekçe |
|---|---|---|
| Runtime | .NET 9, ASP.NET Core Minimal API | Performans, düşük bellek, AOT uyumlu |
| Veri depolama | Gömülü JSON/SQLite → startup'ta in-memory | Veriler küçük ve statik; DB sunucusu gereksiz maliyet |
| Dağıtık cache | Redis (sadece rate limit sayaçları için) | Çoklu replica tutarlılığı |
| Dokümantasyon | OpenAPI + Scalar UI | Modern, ücretsiz |
| Container | Docker, çok aşamalı build, distroless imaj | Küçük imaj, hızlı ölçekleme |
| Orkestrasyon | Kubernetes (mevcut on-prem altyapı) veya ücretsiz katman PaaS | Mevcut yetkinlik |
| CI/CD | GitHub Actions (açık kaynak repo için ücretsiz) | Veri güncellemeleri de PR ile gelir |
| CDN | Cloudflare Free | Bant genişliği + DDoS koruması |

### 7.2 Önemli Mimari Kararlar
- **Veri = kod:** Tüm veri setleri repo içinde JSON dosyaları olarak tutulur. Veri güncellemesi PR ile yapılır, CI testlerinden geçer, deploy ile yayınlanır. Topluluk katkısını kolaylaştırır ve veri değişiklik geçmişi git'te izlenir.
- **Veritabanı yok (MVP'de):** Tüm veri < 50 MB. Startup'ta belleğe yüklenir, immutable koleksiyonlarda tutulur. Sıfır DB operasyon maliyeti, maksimum hız.
- **Stateless API:** Yatay ölçekleme serbest; tek durum Redis'teki rate limit sayaçları.
- **NuGet paketi bonus:** Aynı veri setleri `OpenData.Turkey` gibi NuGet paketleri olarak da yayınlanabilir — API'ye hiç istek atmadan offline kullanım isteyenler için ikinci dağıtım kanalı ve ek görünürlük.

---

## 8. Sürdürülebilirlik ve Maliyet Modeli

Ücretsiz servislerin en büyük riski maliyet sürdürülebilirliğidir:

| Kalem | Strateji |
|---|---|
| Bant genişliği | CDN + agresif cache + toplu indirme teşviki |
| Compute | Statik veri + in-memory = tek küçük pod bile 1000+ RPS kaldırır |
| Finansman | GitHub Sponsors, Buy Me a Coffee, opsiyonel "powered by" linki |
| Kötüye kullanım | Katmanlı rate limit + CDN seviyesinde bot koruması |
| Zaman | Veri güncellemeleri topluluk PR'ları ile; otomatik veri doğrulama testleri |

---

## 9. Yol Haritası

| Faz | Süre | Teslimat |
|---|---|---|
| Faz 0 | 2 hafta | Mimari kurulum, CI/CD, rate limit + cache altyapısı |
| Faz 1 (MVP) | 4 hafta | İl/ilçe, para birimleri, diller, dokümantasyon, açık kaynak yayın |
| Faz 2 | 6 hafta | Mahalle, posta kodu, tatiller, API anahtar portalı |
| Faz 3 | Sürekli | Bankalar, IBAN, üniversiteler, döviz kurları; topluluk talepleri |

---

## 10. Başarı Kriterleri ve KPI'lar

- API yanıt süresi, uptime ve istek hacmi hedeflerinin tutturulması (Bölüm 2.2)
- Cache hit oranı > %95
- Rate limit nedeniyle reddedilen istek oranı < %2 (limitlerin doğru kalibre edildiğinin göstergesi)
- En az 10 dış topluluk katkısı (PR) ilk yıl içinde
- Dokümantasyon sayfasından API'ye dönüşüm (ilk istek atma) oranı

---

## 11. Riskler ve Önlemler

| Risk | Etki | Önlem |
|---|---|---|
| Viral kullanım → maliyet patlaması | Yüksek | CDN, toplu indirme, anonim limitlerin düşük tutulması |
| Veri güncelliğinin kaybolması (ilçe değişiklikleri vb.) | Orta | Resmi kaynak takibi, topluluk bildirimi, yılda 2 planlı gözden geçirme |
| Veri lisans ihlali | Orta | Sadece MIT/CC0/kamu malı kaynaklar; lisans dosyası repo'da |
| Tek kişiye bağımlılık | Orta | Açık kaynak, dokümante altyapı, otomasyona yatırım |
| Kötüye kullanım (scraping botları) | Düşük | Zaten açık veri; toplu indirme sunarak scraping ihtiyacını ortadan kaldırma |

---

## 12. Açık Sorular

1. Alan adı ve marka ismi? (ör. `opendata.dev.tr`, `apiveri.com` benzeri)
2. Barındırma: mevcut on-prem altyapı mı, ücretsiz cloud katmanı mı? (Public servis için cloud + CDN önerilir — ev/iş altyapısını public trafiğe açmak risklidir)
3. API anahtar portalı MVP'de mi Faz 2'de mi? (Öneri: Faz 2 — MVP tamamen anonim başlasın)
4. Türkçe mi İngilizce mi öncelikli dokümantasyon? (Öneri: İngilizce ana, Türkçe ek — global erişim için)
