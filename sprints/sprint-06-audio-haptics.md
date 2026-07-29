# Sprint 6 — Ses & Haptik

**Durum:** ✅ Tamamlandı (2026-07-15) — AudioManager, boyuta göre pentatonik yutma sesi, müzik/jingle/güç/UI/exit sesleri, haptik + VIBRATE izni hook'u, ayar toggle'ları
**Bağımlılık:** Yok (oynanış üstüne eklenir; Sprint 4 güçlerine özel ses bağlanabilir)
**Amaç:** GDD'nin "görsel kadar kritik" dediği ses katmanı + her yutmada titreşim hissi.

> GDD referans: "Ses Tasarımı" (küçük→tık, büyük→BOOM, elmas→kling, araba motoru) ve
> "Temel Mekanikler" (her yutmada ses + titreşim + skor animasyonu).

---

## Kapsam / Görevler

- [x] **AudioManager:** SFX havuzu (8 AudioSource pool), müzik kanalı, ses ayarı (PlayerPrefs). Oto-bootstrap (DontDestroyOnLoad).
- [x] **Yutma sesleri:** scoreValue eşiğiyle küçük "tık" (`<22`) → orta (`22-39`) → büyük "BOOM" (`≥40`), pitch varyasyonu.
- [x] **Lüks/özel sesler:** Mıknatıs(Elmas)→"kling" (inharmonik çan), Hız→motor revvi, Büyüme→tok BOOM.
- [x] **Arka plan müziği:** prosedürel yumuşak ambient arpej döngüsü (A-minör pentatonik, ~%22 ses), toggle'la aç/kapa.
- [x] **Haptik/titreşim:** yutmada kısa (12/25/55ms) Android VibrationEffect; ayar ile aç/kapa.
- [x] **UI sesleri:** buton tık (UiButtons + PauseMenu + MainMenu), success/fail jingle (yükselen/alçalan arpej).
- [~] Ses asset'leri: **PROSEDÜREL üretildi** (asset dosyası YOK, build +0). Beğenilmezse freesound.org'a geçilebilir.
- [x] Ayarlar toggle: **PauseMenu** duraklat ekranına Ses/Müzik/Titreşim AÇIK-KAPALI satırları (PlayerPrefs kalıcı).

## Kabul Kriterleri
- Küçük ve büyük nesne yutmak farklı ses çıkarıyor
- Her yutmada titreşim hissediliyor (ayardan kapatılabiliyor)
- Success/fail ekranlarında uygun jingle çalıyor
- Ses açma/kapama kalıcı (PlayerPrefs)

## Notlar / İlgili Dosyalar
- **Yutma tetik noktası (güncel):** `Assets/Scripts/Physics/PhysicsSwallowable.cs` — yutulma anında
  `GameManager.ReportSwallowed(this, pos)` çağırır. Ses/titreşimi merkezi olarak **`Assets/Scripts/GameManager.cs`
  → `ReportSwallowed`** içine bağla (boyut/skor + `isBomb` + `objectType`/`worldId` burada mevcut → küçük "tık" /
  büyük "BOOM" / bomba / lüks sesi buradan seçilir). (Eski `Assets/Scripts/Swallowable.cs` artık kullanılmıyor — legacy.)
- Yeni: `Assets/Scripts/Systems/AudioManager.cs`, `Assets/Audio/`
- Mobil haptik için platforma göre (`Handheld.Vibrate` temel; gelişmiş için eklenti)

---

## Uygulama Notları (2026-07-12 — kodlama tamam)

**`Assets/Scripts/Systems/AudioManager.cs`** — TÜM sesler RUNTIME'da prosedürel sentezlenir
(`AudioClip.Create` + float örnek sentezi), asset dosyası yok → build boyutu +0.
- Oto-bootstrap: `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` → `AudioManager` GameObject'i +
  8'li AudioSource havuzu + 1 müzik kanalı (DontDestroyOnLoad). Sahneye elle eklemeye gerek yok.
- Klip sentezi: `MakePop` (tık, freq kayışı+üstel sönüm), `MakeBoom` (pitch düşen alçak sinüs),
  `MakeBell` (inharmonik çan oranları = kling), `MakeExplosion` (boğuk gürültü+rumble), `MakeClick` (UI),
  `MakeArpeggio` (üçgen dalga nota dizisi; yükselen=win, alçalan=lose), `MakeMotor` (testere rev),
  `MakeMusic` (2-bar pentatonik pad döngüsü).
- Ayarlar: `AudioManager.SfxOn/MusicOn/HapticOn` static prop'ları PlayerPrefs (`opt_sfx/opt_music/opt_haptic`,
  default AÇIK). `SfxOn=false` → tüm `Play()` susar; `MusicOn` setter müziği anında başlat/durdur.
- Haptik: `HapticKind Light/Medium/Heavy` → 12/25/55ms. Android'de `Vibrator.vibrate` (SDK≥26 VibrationEffect,
  yoksa legacy), try/catch guard, editör/masaüstü no-op. AndroidManifest'e VIBRATE izni gerekebilir (build'de kontrol).

**Hook noktaları:**
- `GameManager.ReportSwallowed` → `PlaySwallow(scoreValue)` (haptik dahil). `OnBombSwallowed` → `PlayBomb()`.
  `TriggerSuccess` → `PlaySuccess()`, `ShowFailPanel` → `PlayFail()`.
- `PowerUpManager.Activate` → Speed=`PlayMotor`, Magnet=`PlayKling`, SizeBurst=`PlayBurst`.
- UI tık: `UiButtons.Build` (merkezî, tüm pill butonlar), `PauseMenu` yuvarlak butonlar, `MainMenu` durak+dünya hücreleri.
- Ayar toggle'ları: `PauseMenu` duraklat overlay'inde 3 satır (Ses/Müzik/Titreşim, AÇIK-yeşil/KAPALI-gri rozet).

**⚠️ TEST BEKLİYOR (kulakla):** Play → yut (küçük tık vs büyük boom farkı), bomba, güç-up sesleri,
success/fail jingle, UI tık, müzik döngüsü kalitesi, PauseMenu toggle'ları (kapatınca susuyor mu + kalıcı mı).
Cihazda: haptik hissi + VIBRATE izni.

## DOSYA-TABANLI SES + PER-DÜNYA MÜZİK (2026-07-13)

Kullanıcı prosedürel sesleri beğenmedi → gerçek dosyalar koyacak (kendisi indirir) + **dünya başına müzik**.
Sistem dosya-öncelikli: dosya varsa onu, yoksa prosedürel fallback çalar (kod değişmeden devreye girer).

**Kullanıcı dosyaları buraya, TAM bu isimlerle koyacak** (uzantı fark etmez — ogg/wav/mp3):
- **Müzik → `Assets/Resources/Audio/Music/`:** `music_menu`, `music_world_0` (Park), `music_world_1` (Yiyecekler),
  `music_world_2` (Arabalar), `music_world_3`… (sonraki dünyalar). Format: .ogg stereo, **seamless LOOP**.
- **SFX → `Assets/Resources/Audio/SFX/`:** `pop_small`, `pop_med`, `boom`, `kling`, `motor`, `bomb`, `ui_click`,
  `success`, `fail`. Format: .wav mono, kısa.

**Kaynak önerileri (ticari-güvenli):** Pixabay Music (royalty-free, atıfsız) · Kenney.nl (CC0 SFX/jingle paketleri) ·
FreePD / Incompetech-KevinMacLeod (müzik) · freesound.org (tekil SFX). ⚠️ CC-BY-NC KULLANMA (ticari yasak).
Müzik arama: "casual game loop", "cozy/happy ukulele/marimba loop". SFX: "bubble pop", "coin", "cartoon explosion" vb.

**Kod:** `AudioManager.Or()` SFX'i Resources'tan yükler; `PlayMusicForWorld/PlayMenuMusic` müziği. Tetik:
`LevelManager.Start`→dünya müziği, `MainMenuController.Start`→menü müziği. ⚠️ Recompile doğrulaması Unity öne
gelince yapılacak (arka planda MCP timeout).
