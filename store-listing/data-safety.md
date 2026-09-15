# Play Console — Veri güvenliği (Data safety) formu cevapları

**Kaynak:** oyunun kodu (SaveState/AccountManager/IapService/AdManager) + Google Mobile Ads SDK resmi
"Play data disclosure" dokümanı. SDK'lar değişirse bu form güncellenmeli.

Kullanılan SDK'lar: Google Mobile Ads (AdMob), Unity Gaming Services (Authentication + Cloud Save),
Google Play Games (hesap bağlama), Unity IAP / Google Play Billing.

---

## Adım 1 — Veri toplama ve güvenlik

| Soru | Cevap | Neden |
|---|---|---|
| Uygulaman kullanıcı verisi topluyor/paylaşıyor mu? | **Evet** | AdMob + bulut kayıt |
| Toplanan tüm veriler aktarımda şifreleniyor mu? | **Evet** | Google ve Unity servisleri TLS kullanır |
| Kullanıcılar verilerinin silinmesini talep edebiliyor mu? | **Evet** | Gizlilik politikasındaki e-posta ile |

---

## Adım 2 — Veri türleri (işaretlenecekler)

**T** = Toplanıyor · **P** = Paylaşılıyor (3. tarafa aktarılıyor)

| Kategori | Veri türü | T | P | Amaç | Zorunlu/İsteğe bağlı | Kaynak |
|---|---|---|---|---|---|---|
| Konum | Yaklaşık konum | ✅ | ✅ | Reklam | Zorunlu | AdMob (IP'den tahmin) |
| Kişisel bilgiler | Ad | ✅ | ❌ | Uygulama işlevi | **İsteğe bağlı** | Oyuncunun girdiği takma ad |
| Kişisel bilgiler | Kullanıcı kimlikleri | ✅ | ❌ | Uygulama işlevi | Zorunlu | UGS anonim ID + Play Games ID |
| Finansal bilgiler | Satın alma geçmişi | ✅ | ❌ | Uygulama işlevi | Zorunlu | Unity IAP / Play Billing |
| Uygulama etkinliği | **Uygulama işlemleri** (EN: App interactions) | ✅ | ✅ | Reklam, Analiz | Zorunlu | AdMob (dokunma, gösterim) |
| Uygulama etkinliği | Diğer işlemler | ✅ | ❌ | Uygulama işlevi | Zorunlu | Oyun ilerlemesi (bölüm/yıldız/coin) |
| Uygulama bilgi ve performansı | Kilitlenme günlükleri | ✅ | ✅ | Analiz, Sahtekârlık önleme | Zorunlu | AdMob teşhis verisi |
| Uygulama bilgi ve performansı | Teşhis | ✅ | ✅ | Analiz, Sahtekârlık önleme | Zorunlu | AdMob teşhis verisi |
| Cihaz veya diğer kimlikler | Cihaz veya diğer kimlikler | ✅ | ✅ | Reklam, Sahtekârlık önleme, Analiz | Zorunlu | AdMob (reklam kimliği, app set ID) |

### İŞARETLENMEYECEKLER (oyun bunları toplamıyor)
E-posta adresi · Telefon · Adres · Tam konum (GPS) · Kişiler · Fotoğraf/Video · Ses ·
Mesajlar · Takvim · Sağlık/fitness · Web geçmişi · Dosyalar · Ödeme kartı bilgileri
*(kart bilgisi Google Play'de kalır, oyuna hiç gelmez)*

---

---

## Adım 4 — "Veri kullanımı ve işleme" (her tür için tek tek)

Her veri türünde **Başlat** deyince şu sorular gelir:
1. Toplanıyor mu / paylaşılıyor mu? · 2. Geçici olarak mı işleniyor? · 3. Zorunlu mu? · 4. Neden (amaçlar)

**"Bu veriler kısa süreli olarak işleniyor mu?" → HEPSİNDE "Hayır"** (veriler saklanıyor).

Play'in gerçek Türkçe etiketleri: **Uygulama işlevselliği** · **Reklam veya pazarlama** ·
**Sahtekârlığı önleme, güvenlik ve kanunlara uygunluk** · Analiz · Hesap yönetimi.
Finansal bilgiler altındaki tür **"İşlem geçmişi"** adıyla çıkar.
"Kişiselleştirme" ve "Geliştirici iletişimleri" HİÇBİR türde işaretlenmez (push/öneri yok).

| # | Veri türü | Toplanıyor | Paylaşılıyor | Zorunlu? | Amaç(lar) |
|---|---|---|---|---|---|
| 1 | Ad | ✅ | ❌ | **Kullanıcılar seçebilir** | Uygulama işlevi |
| 2 | Kullanıcı kimlikleri | ✅ | ❌ | Zorunlu | Uygulama işlevi · Hesap yönetimi |
| 3 | Satın alma geçmişi | ✅ | ❌ | Zorunlu | Uygulama işlevi |
| 4 | Yaklaşık konum | ✅ | ✅ | Zorunlu | Reklamcılık veya pazarlama |
| 5 | Uygulama işlemleri | ✅ | ✅ | Zorunlu | Reklamcılık veya pazarlama · Analiz |
| 6 | Diğer işlemler | ✅ | ❌ | Zorunlu | Uygulama işlevi |
| 7 | Kilitlenme günlükleri | ✅ | ✅ | Zorunlu | Analiz · Sahtekârlık önleme, güvenlik ve uyumluluk |
| 8 | Teşhis | ✅ | ✅ | Zorunlu | Analiz · Sahtekârlık önleme, güvenlik ve uyumluluk |
| 9 | Cihaz veya diğer kimlikler | ✅ | ✅ | Zorunlu | Reklamcılık veya pazarlama · Sahtekârlık önleme, güvenlik ve uyumluluk · Analiz |

> "Paylaşılıyor" işaretli olanlarda ayrıca **"neden paylaşılıyor"** sorulur → aynı amaçları seç.

## Notlar

- **"Ad" isteğe bağlı**: oyuncu isim ekranında varsayılanı ("Player") bırakabilir, kendi adını
  yazmak zorunda değil → Play'in tanımıyla "İsteğe bağlı".
- **"Paylaşılıyor" neden AdMob satırlarında işaretli**: Google Mobile Ads SDK verileri reklam,
  analiz ve sahtekârlık önleme için Google'a aktarır — Google bunu resmi olarak "collect and share"
  diye tanımlıyor.
- **Reklam kimliği**: AdMob eklentisi `com.google.android.gms.permission.AD_ID` iznini otomatik ekler;
  bu yüzden "Cihaz veya diğer kimlikler" beyanı zorunlu.
- **Reklamsız satın alan kullanıcı** için de beyan aynı kalır (SDK yine yüklü).
- Gizlilik politikası: `https://onurapple2000.github.io/privacy.html`

⚠️ Son beyan geliştiricinin sorumluluğundadır; SDK eklersen/çıkarırsan formu güncelle.
