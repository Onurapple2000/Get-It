# Sprint 2 — Can Sistemi: Kalıcılık & Yenilenme

**Durum:** ✅ Tamamlandı (2026-06-27) — *yalnız `RegenSeconds` TEST için 30sn; sürümden önce 1800 yapılacak.*

## ✅ Yapıldı
- `Assets/Scripts/Systems/LivesManager.cs`: kalıcı singleton (PlayerPrefs + UTC DateTime anchor), offline
  yenilenme (`RegenSeconds`/can, max 5), `Lives`/`HasLife`/`IsFull`/`LoseLife`/`AddLife`/`SecondsToNextLife`/
  `NextLifeClock`, `OnChanged` event. RuntimeInitializeOnLoadMethod ile otomatik kurulur (DontDestroyOnLoad).
- `GameManager`: `static int lives` kaldırıldı → fail'de `LivesManager.LoseLife()`. Fail/success'te delik
  donar (`HoleController` `GameManager.IsActive` kontrolü → arka planda oynanamaz, son durum görünür).
- **Kalp görselleri** (DALL-E, `Assets/Resources/Hearts/` — full/empty/+1; beyaz bg `HeartBgRemover` ile
  şeffaf yapıldı, RGB→RGBA). `HeartArt` Resources'tan yükler.
- `Assets/Scripts/Systems/LivesBadge.cs`: **fail panelinde** can rozeti [dolu kalp] x/5 [+1 kalp] mm:ss.
- `Assets/Scripts/Systems/LivesHud.cs`: oynanış HUD'u (5 kalp) — **şimdilik DEVRE DIŞI** (kullanıcı kararı:
  can sadece fail + ana sayfada görünsün). Ana sayfa can göstergesi Sprint 3'te eklenecek.
- Fail paneli düzeni elden geçti (OLMADI küçült/aşağı, maskot büyüt/aşağı, butonlar/rozet yerleşimi).

**Kalan/Not:** ⚠️ `LivesManager.RegenSeconds = 30` (TEST). Sürümden önce **1800** yap. "0 canda girilemez"
kapısı ana menüde (Sprint 3) `LivesManager.HasLife` ile bağlanacak.

---

### (Orijinal plan)
**Durum:** ⬜ Başlanmadı
**Bağımlılık:** Yok (Sprint 8 reklamla "can kazan" buna bağlanır)
**Amaç:** GDD'deki can ekonomisini gerçek hâle getirmek: kalıcı, zamanla yenilenen, oturum yaratan.

> GDD referans: "Can Sistemi" — 5 can, kaybedilen level -1 can, **30 dakikada 1 can** (5 can = 2.5 saat).

---

## Kapsam / Görevler

- [ ] **LivesManager (kalıcı singleton):** can sayısı, son yenilenme zaman damgası `PlayerPrefs`'te
- [ ] **Kalıcılık:** `lives` artık `static int` değil, `PlayerPrefs` + `DateTime` ile saklanıyor
- [ ] **Offline yenilenme:** uygulama kapalıyken geçen süreye göre can hesapla (30 dk = 1 can, max 5)
- [ ] **Geri sayım:** bir sonraki cana kalan süre (mm:ss) hesaplanıp UI'a verilsin
- [ ] **Can harcama:** bölüme girişte mi, fail'de mi? → GDD "kaybedilen level -1"; mevcut davranış fail'de -1
- [ ] **"Can bitti" ekranı:** 0 can → oynanamaz, geri sayım göster + (Sprint 8) "reklam izle +1 can"
- [ ] **HUD/Menü can göstergesi:** kalp ikonları + yenilenme sayacı
- [ ] GameManager fail akışı LivesManager'ı kullanacak şekilde güncelle

## Kabul Kriterleri
- Uygulama kapatılıp 30 dk sonra açılınca 1 can artmış oluyor (zaman damgasıyla, doğru hesap)
- Canlar 5'te tavanlanıyor, 0'ın altına inmiyor
- 0 canda oyuna girilemiyor; bir sonraki cana kalan süre gösteriliyor
- Can durumu uygulama yeniden başlatıldığında korunuyor

## Notlar / İlgili Dosyalar
- `Assets/Scripts/GameManager.cs` — `static int lives`, `TriggerFail()` (şu an -1 burada)
- Yeni: `Assets/Scripts/Systems/LivesManager.cs`
- Cihaz saati manipülasyonuna karşı basit önlem (opsiyonel; ileride sunucu saati)
