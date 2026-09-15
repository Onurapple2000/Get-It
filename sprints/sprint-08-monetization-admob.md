# Sprint 8 — Monetizasyon (AdMob)

**Durum:** 🟡 Kod hazır (test id'leri) — SDK içe aktarımı + AdMob hesabı bekliyor
**Bağımlılık:** Sprint 2 (can — rewarded ile can kazan), Sprint 3 (menü/akış)
**Amaç:** GDD'deki reklam ekonomisini kurmak — oyuncuyu boğmadan, karşılığı olan reklamlar.

> GDD referans: "Monetizasyon" tablosu — Rewarded (can kazan / süre uzat), Banner (bekleme),
> Interstitial (her 3 level). Platform: Google AdMob (Android + iOS).

---

## Kapsam / Görevler

- [ ] **Google Mobile Ads SDK** içe aktarımı — .unitypackage (SENİN ADIMIN, aşağıda)
- [x] **AdManager:** init, yükleme, gösterme, hata/yeniden deneme, **test reklam id'leri**, facade (`#if GOOGLE_MOBILE_ADS` korumalı → SDK yokken de derlenir)
- [x] **Rewarded — "Can bitti":** fail panelinde can 0 → "Reklam izle → +1 Can" → `LivesManager.AddLife(1)` → Retry açılır
- [x] **Rewarded — "Süreyi uzat":** süre-doldu fail'inde "İzle → +20sn Devam" (aynı bölümü sürdürür, can iadesi, level başına 1)
- [x] **Interstitial:** her 3 level (kalıcı sayaç) + min 90sn frekans penceresi; reklam kapanınca sahne yüklenir
- [x] **Banner:** ana menüde alt banner (oynanışa girince gizlenir)
- [x] **Reklam sıklığı/frekans sınırı:** interstitial her 3 + 90sn arası; rewarded yalnız kullanıcı isterse
- [x] iOS **ATT** Info.plist anahtarı (`NSUserTrackingUsageDescription`, build post-processor)
- [ ] iOS ATT **prompt çağrısı** + GDPR/UMP consent formu → Sprint 9 (gerçek id + gizlilik mesajı ile)
- [ ] Gerçek reklam id'leri **yayın öncesi** → `AdManager.UseTestIds=false` + Prod* id'leri doldur

---

## 🔴 SENİN YAPMAN GEREKENLER (hesap/üyelik — kod dışı)

Bunlar Google/Apple sisteminde senin kimliğinle yapılır, ben yapamam. **Test için hiçbiri gerekmez.**

1. **AdMob hesabı aç** — https://admob.google.com (Google hesabıyla ücretsiz).
2. **Uygulama ekle** → Android + iOS ayrı → 2 **App ID** alırsın.
3. **Ad unit oluştur** (Rewarded, Interstitial, Banner) → id'ler → `AdManager.cs` Prod* sabitlerine yapıştırırız.
4. **Ödeme/vergi bilgisi** (banka) — sadece *para almak* için, yayına yakın.
5. **iOS için Apple Developer** hesabı + **Google Play** bağlama — yayın aşaması (Sprint 9).

### SDK içe aktarma adımı (senin, bir kez):
1. https://github.com/googleads/googleads-mobile-unity/releases → en son `GoogleMobileAds-vX.X.X.unitypackage` indir.
2. Unity → Assets → Import Package → Custom Package → seç → Import.
3. **Player Settings → Scripting Define Symbols**'a `GOOGLE_MOBILE_ADS` ekle (Android + iOS).
   → bunu ekleyince tüm reklam kodu otomatik devreye girer (test reklamları görünür).
4. (SDK sonrası) **Assets → Google Mobile Ads → Settings** → App ID'leri gir (şimdilik test App ID: Android `ca-app-pub-3940256099942544~3347511713`, iOS `ca-app-pub-3940256099942544~1458002511`).

## Kabul Kriterleri
- Test reklamları sorunsuz gösteriliyor (rewarded/interstitial/banner)
- "Can bitti" → reklam izle → +1 can çalışıyor
- Interstitial her 3 levelda bir, akışı bozmadan çıkıyor
- iOS'ta ATT izni isteniyor

## Notlar / İlgili Dosyalar
- `Packages/manifest.json` — şu an reklam paketi yok
- Yeni: `Assets/Scripts/Systems/AdManager.cs`
- `Assets/Scripts/Systems/LivesManager.cs` (Sprint 2) — rewarded entegrasyonu
- **Önce test id'leriyle**; prod id'leri ve gizlilik politikası Sprint 9 ile
