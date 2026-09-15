# Sprint 9 — Build & Mağaza Hazırlığı

**Durum:** ⬜ Başlanmadı
**Bağımlılık:** Tüm sprintler (yayın öncesi son aşama)
**Amaç:** GDD'deki "Test + Bugfix" ve "Store Submission" aşamalarını tamamlamak.

> GDD referans: "Store Bilgileri", "Geliştirme Aşamaları" (cihaz testleri, Apple/Google süreçleri).

---

## Kapsam / Görevler

- [ ] **ASSET DELIVERY FAZ 2** (küçük ilk indirme — aşağıda ayrıntı) — LevelData→AssetReference, async yükleme, prefetch, PAD build
- [ ] **Android build ayarları:** package name, min/target SDK, keystore (imzalama), AAB
- [ ] **iOS build ayarları:** bundle id, imzalama, capabilities, ATT açıklaması
- [ ] **Uygulama ikonu + splash:** köstebek temalı (mevcut maskot sprite'ları)
- [ ] **Mağaza görselleri:** ekran görüntüleri, feature grafiği, tanıtım metni (EN — global)
- [ ] **Performans testi:** gerçek cihazda FPS, bellek, batarya; nesne sayısı/partikül optimizasyonu
- [ ] **Gerçek cihaz testleri:** farklı ekran oranları, dokunmatik kontrol, ses/titreşim
- [ ] **Gizlilik politikası + izinler** (reklam/ATT/GDPR — Sprint 8 ile)
- [ ] **Reklam prod id'leri** test'ten prod'a geçiş
- [ ] **Sürüm/versiyon yönetimi**, build numaraları
- [ ] (Karar) Oyun adı kesinleştir — GDD: "GET-IT benzer isim çakışması var"

---

## 🧹 YAYIN ÖNCESİ TEMİZLİK (Dev Kısayollarını Geri Al)

> Geliştirme kolaylığı için konan geçici kısayollar. **Release build'den önce** tek tek geri alınacak.
> Not: Bazıları zaten build-tipine bağlı (release'de otomatik doğru) — yine de doğrulanacak.

- [ ] **Can yenilenme 30sn → 30dk:** [LivesManager.cs](../Assets/Scripts/Systems/LivesManager.cs) `RegenSeconds = 30` → `1800`. (Dev'de 30 kalması için `#if UNITY_EDITOR || DEVELOPMENT_BUILD` ile ayırılabilir.)
- [ ] **Level/dünya kilidi doğrula:** `LevelManager.UnlockedIndex` release'de PlayerPrefs ilerlemesi kullanır (dev/editör'de HEPSİ açık — `#if DEVELOPMENT_BUILD || UNITY_EDITOR`). Release build'de sıralı açılmayı test et.
- [ ] **Kısaltılmış süreler / açık bırakılan levellar:** tüm dev kısayol sabitlerini tara → gerçek değerlere çek.
- [ ] **Satın alma ekranını güzelleştir** (Store UI — Adım 4/IAP ile birlikte).
- [ ] **Diagnostik loglar:** `[Ads]`, `[Account]`, `[CloudSync]` Debug.Log'larını release'de sustur (veya `#if DEVELOPMENT_BUILD`).
- [ ] **PerfHud / dev-unlock / debug menüleri** release'de kapalı.
- [ ] **AdMob:** `AdManager.UseTestIds = false` + prod reklam id'leri (Sprint 8).
- [ ] **Reklam/hesap test id'leri → prod:** UGS prod environment, AdMob prod, IAP prod ürünleri.

---

## Kabul Kriterleri
- Android (AAB) ve iOS build alınabiliyor, gerçek cihazda çalışıyor
- Tüm dev kısayolları geri alınmış (can 30dk, sıralı level kilidi, loglar kapalı)
- İkon/splash/mağaza görselleri hazır
- Hedef cihazlarda performans kabul edilebilir (stabil FPS)
- Mağaza gereksinimleri (gizlilik, izinler) karşılanmış

## Notlar / İlgili Dosyalar
- `ProjectSettings/` — Player Settings (Android/iOS)
- Apple Developer ($99/yıl), Google Play ($25) hesapları (GDD)
- Bu sprint büyük; gerekirse "test/bugfix" ve "store submission" diye ikiye bölünebilir

---

## 📦 ASSET DELIVERY — Küçük İlk İndirme (Addressables + PAD/ODR)

**Hedef (kullanıcı kararı, 2026-07-15):** İlk indirme MİNİMUM olsun = **çekirdek + Dünya 0 (Park)** (~50-80MB).
Diğer dünyalar oyuncu ilerledikçe on-demand insin. "10 dünya baştan" fikri İPTAL (çok büyük). Kendi server GEREKMEZ
(Android=Google Play Asset Delivery, iOS=On-Demand Resources; Unity tarafı=Addressables).

### ✅ FAZ 1 — ZATEN YAPILDI (2026-07-15, bu oturumda)
- **Addressables 2.11.1 kuruldu** (⚠️ 2.3.16 Unity 6000.5'te derlenmez — `GetInstanceID` hata-obsolete; 2.11.1 uyumlu).
- **Grup iskeleti kuruldu:** `Assets/Scripts/Editor/AddressableWorldSetup.cs` → menü **`Tools/GET_IT/Setup Addressable
  Groups`** (idempotent). Gruplar:
  - **Core** (install-time / ilk indirme): `Assets/Prefabs/PowerUps`, `Assets/Prefabs/Bomb.prefab` — paylaşılan.
  - **World0-Park / World1-Foods / World2-Cars** (on-demand): her dünyanın prefab + model klasörleri
    (`Prefabs/DecoObjects`, `Prefabs/Foods`+`Art/worlds/foods`, `Prefabs/Cars`+`Art/worlds/cars`).
  - Etiketler: `install-time`, `on-demand`.
- **Yeni dünya eklenince:** o dünyanın klasörlerini AddressableWorldSetup.Setup()'a ekle + menüyü tekrar çalıştır.
- **Şu an oyun AYNEN çalışıyor** (LevelData prefab'lara doğrudan bağlı); bu sadece organizasyon — boyut kazancı FAZ 2'de.

### ⬜ FAZ 2 — BURADA YAPILACAK (içerik bitince / sürüme yakın)
1. **LevelData refactor:** prefab `GameObject` ref'leri → `AssetReferenceGameObject` (Addressables). LevelManager
   spawn'ları **`Addressables.LoadAssetAsync`** ile async yüklesin (şu an senkron doğrudan ref).
2. **Dünya prefetch:** oyuncu bir dünyaya GİRİNCE (LevelManager.Start veya menüde) **sonraki dünyayı** arka planda
   indir: `Addressables.DownloadDependenciesAsync(label:"World{n+1}...")`. ⚠️ Geçişte değil ERKEN başlat → "Devam Et"
   anında hazır olsun, bekleme olmasın. Buffer 1 (belki 2). Dünya geçiş animasyonu ([[project-worlds]]) kontrol noktası.
3. **İndirme UI:** hazır değilse "Sonraki dünya hazırlanıyor %XX" progress + offline/hata durumu (bağlantı yoksa mesaj).
4. **PAD build:** Addressables Groups → her on-demand grubu bir **asset pack**'e eşle (install-time / fast-follow /
   on-demand). AAB al, **Google Play internal test** track'te gerçek indirme davranışını doğrula (local build'de
   tam test edilmez). iOS için ODR karşılığı.
5. **Ses:** on-demand GEREKMEZ (~6MB toplam) → install-time'da kalsın.
- Detay/karar geçmişi: [[project-mobile-optimization]] (boyut bulguları + revize plan).
