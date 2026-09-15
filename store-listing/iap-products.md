# Play Console — Uygulama içi ürünler (IAP)

**Yol:** Google Play ile para kazanın → Ürünler → **Tek seferlik ürünler** → Ürün oluştur

⚠️ **Ürün kimliği (Product ID) kodla BİREBİR aynı olmalı** — bir harf sapması satın almayı bozar.
⚠️ Ürün kimliği **kalıcıdır**, sonradan değiştirilemez (paket adı gibi).
Kaynak: `Assets/Scripts/Systems/IapService.cs` → `Packs[]`

Mağaza varsayılan dili **İngilizce** → ürün adlarını İngilizce gir, Türkçe'yi çeviri olarak ekle.

| # | Ürün kimliği | Ad (EN) | Ad (TR) | İçerik | Fiyat | Tip |
|---|---|---|---|---|---|---|
| 1 | `getit.pack.starter` | Starter Pack | Başlangıç Paketi | 1000 Coin + 4 Hız | ₺9,99 | Tüketilebilir |
| 2 | `getit.pack.coinbag` | Coin Bag | Coin Kesesi | 3000 Coin | ₺14,99 | Tüketilebilir |
| 3 | `getit.pack.magnet` | Magnet Pack | Mıknatıs Paketi | 2000 Coin + 10 Mıknatıs | ₺19,99 | Tüketilebilir |
| 4 | `getit.pack.super` | Super Pack | Süper Paket | 5000 Coin + 6 Büyüme + 2 Süper | ₺29,99 | Tüketilebilir |
| 5 | `getit.pack.super3` | Super Power ×6 | Süper Güç ×6 | 6 Süper | ₺24,99 | Tüketilebilir |
| 6 | `getit.noads` | Remove Ads | Reklamları Kaldır | Tüm reklamları kalıcı olarak kaldırır | ₺49,99 | **Tüketilemez** (restore edilir) |

### Açıklama metinleri (EN / TR)

1. `Get 1000 coins and 4 Speed power-ups to start strong.` / `Güçlü başlamak için 1000 coin ve 4 Hız gücü.`
2. `A bag of 3000 coins.` / `3000 coinlik bir kese.`
3. `2000 coins plus 10 Magnet power-ups.` / `2000 coin ve 10 Mıknatıs gücü.`
4. `5000 coins, 6 Growth and 2 Super power-ups — the best value.` / `5000 coin, 6 Büyüme ve 2 Süper güç — en avantajlı paket.`
5. `6 Super power-ups — all abilities at once.` / `6 Süper güç — tüm yetenekler bir arada.`
6. `Remove all ads permanently. Restores on any device with your Google account.` / `Tüm reklamları kalıcı olarak kaldırır. Google hesabınla her cihazda geri yüklenir.`

### Notlar
- **Tüketilebilir / tüketilemez ayrımı Play'de seçilmez** — kodda hallediliyor:
  `IapService` 5 paketi `ProductType.Consumable`, `getit.noads`'ı `NonConsumable` olarak tanımlar.
- Fiyatı ₺ gir → Play diğer ülkelere otomatik dönüştürür (gözden geçirebilirsin).
- Her ürünü **Etkin/Active** yap, yoksa satın alınamaz.
- Test için: Play Console → Ayarlar → **Lisans testi** hesapları ekle → test satın almaları ücretsiz olur.
- ⚠️ 2026-08-21: paket ödülleri 2×'e çıkarıldı; `pkSuper3` adı "×3"ten **"×6"**ya güncellendi (içerikle tutarlı olsun).
