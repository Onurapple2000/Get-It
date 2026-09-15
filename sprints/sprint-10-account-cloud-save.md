# Sprint 10 — Kalıcı Hesap & Bulut Kayıt (UGS + IAP)

**Durum:** ⬜ Başlanmadı
**Bağımlılık:** Sprint 8 (AdMob — noAds satın alma buraya bağlanır). **Sprint 9'dan (mağaza gönderimi) ÖNCE bitmeli** — IAP ürünleri ve hesap sistemi yayına hazır olmalı.
**Amaç:** Oyuncunun satın almaları (coin/powerup/noAds), skoru ve level ilerlemesi **asla kaybolmasın** — uygulama silinse veya telefon değişse bile. **Zorunlu login YOK:** girişsiz anonim kimlik + satın almada/istekte Google/Apple'a bağlama.

> Karar (2026-08-18): Altyapı = **Unity Gaming Services (UGS)**. Kendi server YOK.
> Store'lar ödemeyi + noAds "restore"u halleder; coin bakiyesi/skor/level ise BİZİM verimiz → Cloud Save.
> Maliyet: Auth ücretsiz; Cloud Save free tier 5 GiB + 1M yazma + 1M okuma/ay → ~25.000 aylık aktif
> oyuncuya kadar $0 (aşınca cüzi: $0.50/GiB, $0.025/10k yazma). Detay: [[project_account_cloud]].

---

## Mimari özet

- **Kimlik:** UGS Authentication → **Anonim** (ilk açılışta arka planda, oyuncu login görmez).
  İstenince/satın almada **Google Play Games (Android) / Apple (iOS)** hesabına **link** → cihaz-ötesi taşınır.
- **Veri:** Tüm PlayerPrefs değerleri tek bir **SaveState** objesinde toplanır → UGS **Cloud Save**'e yazılır.
  PlayerPrefs **yerel cache** olarak kalır → oyun çevrimdışı da çalışır; bulut sadece senkron katmanı.
- **Ödeme:** **Unity IAP** → Play/App Store. noAds = non-consumable (restore edilebilir), coin/powerup = consumable
  (bakiye Cloud Save'de). Client-side receipt validation (başlangıç); server-side sonra opsiyonel.

### Cloud'a taşınacak veri (mevcut PlayerPrefs envanteri)
| Alan | Şu anki anahtar | Kaynak |
|---|---|---|
| Coin | `Coins` | PlayerProfile |
| Toplam skor | `TotalScore` | PlayerProfile |
| noAds | `NoAds` | PlayerProfile |
| İsim | `PlayerName`, `PlayerNameChosen` | PlayerProfile |
| Level yıldızları | `Stars_{w}_{l}` | StarManager |
| Dünya kilidi | `Unlock_{w}` | LevelManager |
| Powerup envanteri | `PU_{type}` | PowerUpInventory |
| Yıldız hediyesi verildi | `StarGiftGranted_{type}` | StarRewards |
| Canlar | `Lives_Count/AnchorTicks/UnlimitedUntilTicks` | LivesManager |
| Dünya reveal gösterildi | `WorldReveal_{w}` | MainMenuController |

> `Ad_LevelCounter` (AdManager) YEREL kalır — cloud'a gerek yok.

---

## Adımlar / Görevler

### Adım 1 — UGS kurulum + Anonim Auth ✅
- [x] UGS paketleri: core 1.18 / authentication 3.7.4 / cloudsave 3.4.1
- [x] Unity Dashboard'da proje bağlı (Project ID: e8ac9e8a-…, org onurapple2000)
- [x] `AccountManager.cs`: `UnityServices.InitializeAsync()` + `SignInAnonymouslyAsync()` (RuntimeInitialize bootstrap, OnSignedIn olayı)
- [x] Çevrimdışı/başarısız init'e dayanıklı (try/catch → yerel cache ile devam)
- [x] **Cihazda doğrulandı:** `[Account] Anonim giriş tamam. PlayerId=VReOBWXTZ…`

### Adım 2 — SaveState + Cloud Save senkron ✅
- [x] `SaveState.cs`: tüm PlayerPrefs anahtarlarının serileştirilebilir **aynası** (Capture/Apply/Merge, JSON)
- [x] **Non-invaziv köprü:** 5 sistem YENİDEN YAZILMADI — PlayerPrefs yerel doğruluk kaynağı kalır, SaveState bilinen anahtarları toplar (düşük risk)
- [x] `CloudSyncService.cs`: giriş → cloud'dan çek → yerelle **birleştir** (max kazanır, noAds OR) → uygula → geri yaz; güvenlik: synced olmadan buluta yazmaz
- [x] **Yazma azaltma:** FlushNow yalnız level tamam + fail (GameManager hook) + arka plana geçiş/çıkış (OnApplicationPause/Quit)
- [x] İlk açılış migrasyonu: CaptureLocal mevcut PlayerPrefs'i okur → ilk senkronda buluta gider (otomatik)
- [ ] (Polish) OnSynced → HUD/menü değer yenileme (cross-device ilk yüklemede anlık güncelleme için)

### Adım 3 — Hesap bağlama (link) akışı 🟡 (iskelet hazır)
- [x] **AccountManager link API'si:** `IsLinked`, `OnAccountLinked`, `LinkGooglePlayGamesAsync(authCode)`, `LinkAppleAsync(idToken)`, `LinkCurrentPlatform()` (platform TODO'ları ile) — derlendi
- [ ] **Google Play Games plugin** (`com.google.play.games`) kur → `RequestServerSideAccess` ile authCode al → `LinkGooglePlayGamesAsync` doldur (Play Console hesabı doğrulanınca)
- [ ] **iOS Sign in with Apple** → idToken → `LinkAppleAsync`
- [ ] **Dashboard Identity Providers:** Google Play Games + Apple sağlayıcılarını yapılandır (Play Console/Apple credential'ları ile)
- [ ] Ayarlar'a "İlerlemeyi Kaydet / Hesaba Bağla" **butonu** → `LinkCurrentPlatform()`
- [ ] Satın alma öncesi/sonrası: anonimse **link'e davet** (zorunlu değil, önerilir)
- [ ] Link çakışması (kimlikte zaten veri var) → kullanıcıya "buluttaki mi / bu cihazdaki mi" seçtir veya birleştir

### Adım 4 — Unity IAP + noAds restore
- [ ] `com.unity.purchasing` paketi + IAP Catalog (coins, powerup paketleri = consumable; noAds = non-consumable)
- [ ] `Store.cs` stub'ını GERÇEK IAP'a bağla (`RemoveAds/BuyCoins/Purchase` → satın alma akışı → başarıda ver)
- [ ] **"Restore Purchases"** butonu (iOS zorunlu) → noAds geri yükle
- [ ] Client-side receipt validation (Unity IAP obfuscated tangle)
- [ ] Satın alınan bakiye anında SaveState'e + cloud'a yazılır

---

## Kabul Kriterleri
- Oyuncu girişsiz oynayabiliyor; ilerleme buluta yazılıyor (anonim)
- Uygulama silinip yeniden kurulunca: **hesaba bağlıysa** coin/skor/level/noAds/powerup GERİ geliyor
- Farklı cihazda aynı hesapla giriş → aynı ilerleme
- noAds satın alma "Restore" ile geri geliyor (yeni cihaz/kurulum)
- İnternet yokken oyun normal çalışıyor (yerel cache), bağlanınca senkron oluyor
- Satın alınan coin/powerup asla kaybolmuyor (bağlı hesapta)

## 🔴 Senin yapman gereken hesap işleri (kod dışı)
- **Google Play Console** hesabı (25$ tek sefer) → IAP ürünlerini tanımla (coins, noAds) + Play Games Services yapılandır
- **Apple Developer** hesabı (99$/yıl) → App Store Connect'te aynı IAP ürünleri + Sign in with Apple
- **Unity Dashboard** → projeyi UGS'ye bağla (ücretsiz), Cloud Save + Authentication'ı aç
- (Free tier'da ödeme yöntemi girmene gerek yok)

## Notlar / İlgili Dosyalar
- Yeni: `AccountManager.cs`, `SaveState.cs`, `CloudSyncService.cs`
- Değişecek: `PlayerProfile.cs`, `StarManager.cs`, `LevelManager.cs`, `PowerUpInventory.cs`, `LivesManager.cs`, `Store.cs`, `StarRewards.cs`
- Fiyat teyidi (yayına yakın): https://unity.com/products/gaming-services/pricing
