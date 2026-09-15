# Yayın Öncesi Test Planı — GET IT

3 katman: **otomatik testler** (saniyeler) → **performans ölçümü** (cihazda) → **elle test turu** (akışlar).

---

## Katman 1 — Otomatik testler (EditMode)

**Çalıştırma:** Window → General → Test Runner → **EditMode** → Run All
**Dosya:** `Assets/Editor/Tests/CoreLogicTests.cs`

| Sınıf | Ne koruyor |
|---|---|
| `LocalizationTests` | 7 dilin tam olması, boş çeviri olmaması → oyunda anahtar/kutu metin çıkmasın |
| `SaveStateMergeTests` | **Bulut birleştirmede ilerleme kaybı** (coin/yıldız/level/güç-up), JSON gidiş-dönüş, bozuk JSON'da çökmeme |
| `IapCatalogTests` | Ürün kimlikleri benzersiz, tam 1 noAds, her paketin Loc adı var, her paket bir şey veriyor |
| `EconomyTests` | Fiyat/ödül sabitleri pozitif |

> Bu testler kod değiştikçe tekrar çalıştırılmalı — özellikle **yeni Loc anahtarı** veya **yeni IAP paketi** eklerken.

---

## Katman 2 — Performans ölçümü (gerçek cihaz)

**Araç:** `PerfHud` (oyunda açılır; FPS + frame ms + son 3 sn en kötü frame)

### Ölçüm turu
| Senaryo | Neden | Hedef |
|---|---|---|
| Yiyecekler L1 (hafif) | taban çizgisi | 60 fps stabil |
| **Landmark level (L3/6/9/12)** | ⚠️ geçmişte 818 nesne patlaması + donma yaşandı | ≥45 fps, dip < 33 ms |
| Arabalar / Binalar (yoğun ~800 nesne) | en kalabalık sahneler | ≥45 fps |
| Delik büyükken (level sonu) | çok nesne yutulmuş, fizik yükü | dip kontrolü |
| Ana menü → Dünyalar → level geçişi | yükleme takılması | donma yok |

### Not edilecekler
- **FPS** ve **worst frame (ms)** — PerfHud gösteriyor
- **Isınma:** 10 dk oyundan sonra cihaz ısısı / fps düşüşü
- **Bellek:** Android Studio Profiler veya `adb shell dumpsys meminfo com.okatch.getit`
- **Build boyutu:** AAB boyutu (geçmiş sorun: 298 MB → asset delivery planı)

### Referans (önceki baseline)
520 nesnede stabil 60 fps · CPU 17 ms / GPU 12 ms

---

## Katman 3 — Elle test turu (kritik akışlar)

### A. Hesap & bulut (Sprint 10)
- [ ] İlk açılış: dil **cihaz diline** göre otomatik geldi mi (dil ekranı çıkmamalı)
- [ ] Oyna → level bitir → coin/yıldız kazan
- [ ] Ayarlar → **"İlerlemeyi Kaydet"** → Google Play Games bağlandı, buton **"✓ Bağlı"** oldu
- [ ] **Uygulamayı sil → yeniden kur → aç** → isim, coin, yıldız, level, **dil** geri geldi mi
- [ ] Ayarlar → **"Hesabı ve Verileri Sil"** → çift onay → her şey sıfırlandı, oyun çökmedi
- [ ] Silme sonrası tekrar oynanabiliyor (yeni anonim hesap)

### B. Ekonomi & IAP
- [ ] Mağaza açılıyor, 5 paket + fiyatlar görünüyor
- [ ] Paket satın al (test) → coin/güç-up **doğru miktarda** eklendi
- [ ] **Süper Güç ×6** paketi gerçekten 6 Süper veriyor (ad-içerik tutarlılığı)
- [ ] Reklamsız satın al → banner/interstitial **kayboldu**
- [ ] Coin ile güç-up satın alma çalışıyor
- [ ] Yetersiz coin → uyarı çıkıyor, negatife düşmüyor

### C. Reklamlar
- [ ] Ana menüde banner görünüyor (Dünyalar ekranında isimleri **kapatmıyor**)
- [ ] Can bitti → "Reklam izle → +1 Can" çalışıyor
- [ ] Süre doldu → "İzle → +süre" çalışıyor
- [ ] Her 3 levelda bir interstitial çıkıyor, akışı bozmuyor

### D. Oynanış & UI
- [ ] Yiyecekler L1 tanıtımı: yazı yerleşimi doğru, el ile çakışmıyor, **bir kez** gösteriliyor (release build)
- [ ] Joystick her ekran boyutunda çalışıyor, güvenli alan dışına taşmıyor
- [ ] Pause / X menüleri, Devam / Retry / Quit
- [ ] Zorluk (Kolay/Normal/Zor) farkı hissediliyor
- [ ] 7 dilin hepsinde menüler taşmıyor (özellikle **Almanca** uzun kelimeler, **Arapça** RTL)
- [ ] Çentikli ekranda (SafeArea) hiçbir UI kesilmiyor

### E. Dayanıklılık
- [ ] **Uçak modunda** aç → oyun çalışıyor, çökmüyor (bulut senkronu sessizce atlıyor)
- [ ] Oyun ortasında ana ekrana çık → geri dön → durum korunuyor
- [ ] Telefon çağrısı / bildirim sırasında donma yok
- [ ] Uzun oturum (20+ dk) → bellek şişmesi/çökme yok

---

---

## 🚨 PRODUCTION'A ÇIKMADAN ÖNCE KALDIRILACAK

- [ ] **PerfHud test kısayolları** (`Assets/Scripts/Systems/PerfHud.cs`): "Tüm Dünyaları Aç", "Güç-Up ×100",
      "İlerlemeyi SIFIRLA" düğmeleri → silinmeli ya da `#if DEVELOPMENT_BUILD` içine alınmalı.
      **Sebep:** oyuncu gizli hareketi bulursa (sol-alt köşeye 6 dokunuş) tüm ekonomi bedava olur.
      *(Internal/closed test sırasında KALSIN — test için gerekli.)*
- [ ] `AdManager.UseTestIds` → gerçek AdMob id'leri girilince `false`
- [ ] `app-ads.txt` → gerçek AdMob yayıncı kimliği (`pub-XXXX`)

## Bilinen riskler (geçmişten)
- ⚠️ **Shader stripping crash** (cihaza özel) — SafeShader/Always-Included listesi korunmalı
- ⚠️ **Landmark levellarda nesne patlaması** — perf ölçümünde özellikle bak
- ⚠️ Build boyutu — asset delivery (PAD) planı sprint-09 FAZ2
