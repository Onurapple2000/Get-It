# Sprint 8 — Monetizasyon (AdMob)

**Durum:** ⬜ Başlanmadı
**Bağımlılık:** Sprint 2 (can — rewarded ile can kazan), Sprint 3 (menü/akış)
**Amaç:** GDD'deki reklam ekonomisini kurmak — oyuncuyu boğmadan, karşılığı olan reklamlar.

> GDD referans: "Monetizasyon" tablosu — Rewarded (can kazan / süre uzat), Banner (bekleme),
> Interstitial (her 3 level). Platform: Google AdMob (Android + iOS).

---

## Kapsam / Görevler

- [ ] **Google Mobile Ads SDK** kurulumu (Packages/manifest veya .unitypackage) + App ID konfigürasyonu
- [ ] **AdManager:** init, yükleme, gösterme, hata/yeniden deneme, **test reklam id'leri**
- [ ] **Rewarded — "Can bitti":** 30 sn izle → +1 can (Sprint 2 LivesManager'a bağla)
- [ ] **Rewarded — "Süreyi uzat":** level kaybına yakın/fail'de süre uzatma teklifi
- [ ] **Interstitial:** her 3 levelda bir (sayaç ile), akışı bölmeden
- [ ] **Banner:** bekleme/menü ekranında (oynanışı kapatmayacak şekilde)
- [ ] **Reklam sıklığı/frekans sınırı:** baskı kurmadan (GDD: "reklam baskı kurmaz")
- [ ] iOS **ATT (App Tracking Transparency)** izni ve GDPR/consent akışı
- [ ] Gerçek reklam id'leri **yayın öncesi** (test → prod ayrımı net)

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
