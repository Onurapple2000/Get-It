# GET-IT — Game Design Document
*Son güncelleme: Haziran 2026*

---

## Vizyon

GET-IT, oyuncuya **"elektrikli süpürge"** hissi veren bir mobil arcade oyunudur. Oyuncu kendini delikle özdeşleştirmez — onu bir aparat olarak kullanır. Gerçek hayatta sahip olunamayan lüks nesneleri yutmak, hem **ego tatmini** hem de **temizleme/boşaltma** hissi yaratır.

Hedef kitle: Hırslı, doyumsuz, sürekli daha fazlasını isteyen insanlar. Kısa süreli ama yoğun dopamin veren bir "terapi oyunu."

---

## Temel Mekanikler

- Oyuncu ekranda serbest hareket eden bir **soyut deliği** kontrol eder
- Delik üzerine gelen nesneleri yutar ve **büyür**
- Yutma **hızlı, refleks bazlı** — düşünme gerektirmez
- Her yutma anında: ses efekti + titreşim + skor animasyonu
- Level süresi: **60–90 saniye**

### Büyüme Sistemi
- Küçük nesneler anında yutulur
- Büyük nesneler delik yeterince büyüdüğünde yutulabilir
- Yuttukça büyürsün, büyüdükçe daha büyük şeyler yutarsın

### Güç Dönüşümü (Materyal Sistemi)
Bazı özel nesneler yutulduğunda geçici güç verir — oyuncu seçim yapmaz, **otomatik aktif olur:**
- Altın yutunca → kısa süre hız bonusu
- Elmas yutunca → manyetik çekim (yakındaki nesneler otomatik çekilir)
- Araç yutunca → boyut bonusu patlaması

---

## Dünya Tasarımı (Her 10 Level Bir Tema)

| Dünya | Tema | İçerik |
|---|---|---|
| 1 | Kuyumcu | Yüzük, kolye, bilezik, altın külçe, elmas |
| 2 | Showroom | Spor araba, motosiklet, vintage klasik |
| 3 | Marina | Sürat teknesi, yelkenli, yat |
| 4 | Havalimanı | Özel jet, helikopter |
| 5 | Gayrimenkul | Villa, çatı katı, gökdelen |
| 6+ | Absürt | Adalar, uzay gemileri, şehirler |

Her dünyada zorluk kademeli artar. Haritalar 10 levelda bir değişir ama değişim küçük ve kademeli — oyuncu şok olmaz.

---

## Zorluk Eğrisi

**Flow zonu** hedeflenir — çok kolay değil, çok zor değil.

- Level 1–10: Neredeyse garantili kazanç. Güven inşa et.
- Level 11–30: İlk engeller girer. İlk kayıp burada olur.
- Level 31+: "Az kalsın" hissi dominant. Oyuncu bir daha dener.

**"Az kalsın" hissi oyunun en güçlü silahı.** Level bitmeden 2–3 saniye kala can biterse oyuncu tekrar oynamak ister.

---

## Can Sistemi

- Başlangıç: **5 can**
- Her kaybedilen level: 1 can eksilir
- Can yenilenme: **30 dakikada 1 can** (5 can = 2.5 saat)
- Günde 3 oturum hedeflenir: sabah, öğle, akşam

Bu sistem oyuncuyu yormadan alışkanlık yaratır.

---

## Monetizasyon

| Durum | Reklam Türü |
|---|---|
| Can bitti → "30 sn izle, 1 can kazan" | Rewarded Video |
| Level geçemedi → "Süreyi uzat" | Rewarded Video |
| Bekleme ekranı | Banner |
| Her 3 levelda bir | Interstitial |

Platform: **Google AdMob** (Android + iOS)

Reklam baskı kurmaz — oyuncu kendi isteğiyle izler çünkü karşılığı var.

---

## Görsel Stil

**"Lüks Cartoon"** — Clash of Clans / Hay Day kalitesi.

- Yüksek detaylı ama çizgi film estetiği
- Altın gerçekten parlasın, elmas ışığı kırsın, araba metalik yansısın
- Her nesne yutulurken küçük **çekilme animasyonu** (nesne direnir, sonra içine çekilir)
- Renkler zengin ve doygun

### Asset Yol Haritası
1. **Prototip aşaması:** Unity Asset Store + ücretsiz paketler
2. **Geliştirme aşaması:** AI araçları (Midjourney) ile konsept → Freelancer ile final
3. **Yayın öncesi:** Profesyonel sanat varlıkları

---

## Ses Tasarımı

- Her yutma sesine ayrı efekt: küçük → "tık", büyük → "BOOM"
- Lüks nesnelere özel ses: elmas "kling", araba motor sesi
- Kaynak: freesound.org (lisanssız, ücretsiz)
- Ses tasarımı görsel kadar kritik — oyuncu kulaklıkla oynamalı

---

## Teknik Yığın

- **Motor:** Unity (C#)
- **Hedef Platform:** iOS + Android
- **Monetizasyon SDK:** Google AdMob
- **Geliştirme Ortamı:** VS Code + Unity

---

## Store Bilgileri

- **Oyun Adı:** GET-IT (kesinleşmedi, benzer isimde farklı oyun var)
- **Apple Developer Program:** $99/yıl
- **Google Play:** $25 tek seferlik
- **Hedef:** Global piyasa (İngilizce)

---

## Geliştirme Aşamaları

| Aşama | Süre | Hedef |
|---|---|---|
| Kurulum + Öğrenme | 3–4 hafta | Unity kurulum, ilk sahne |
| Prototip | 3–4 hafta | Oynanabilir temel mekanik |
| Core Gameplay | 4–6 hafta | Dünyalar, can sistemi, skor |
| Görsel + Ses | 3–4 hafta | Gerçek asset'ler, efektler |
| Monetizasyon | 2–3 hafta | AdMob entegrasyonu |
| Test + Bugfix | 2–3 hafta | Gerçek cihaz testleri |
| Store Submission | 1–2 hafta | Apple + Google süreçleri |
| **Toplam** | **~5–6 ay** | |
