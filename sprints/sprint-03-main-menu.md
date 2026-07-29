# Sprint 3 — Ana Menü & Navigasyon

**Durum:** ✅ Tamamlandı (2026-06-27)
**Bağımlılık:** Sprint 1 (level), Sprint 2 (can) — menüde bunları göstereceğiz
**Amaç:** Oyunun giriş ekranı + dünya/level seçim akışı. Fail ekranındaki **Cancel** buraya dönecek
(`ExitToMainMenu` zaten hazır).

bu ana ekrandan oyuncu istediği dünyayı seçebilecek. daha sonra o dünya içerisindeki levellardan seçim yapacak. her dünya bir temaya sahip olacak, (araçlar, evler, yiyecekler, tatlılar, karma vs) her 5 dünya bölümü bir grup olacak ve bir grup seviyelerinin hepsi geçilince sonraki grubun 5 dünyası açılacak.

> GDD referans: genel akış. Cancel → ana sayfa bağlantısı Sprint 0'da hazırlandı, sahne burada yapılacak.

---

## Kararlaştırılan Mekanikler (2026-06-25)

- **Dünya seçim ekranı:** Kilitli dünyalar **görünür ama açılmaz** (kilit ikonu). Açık dünyalar seçilebilir.
- **Level seçim ekranı (dünya içi, 5 level):**
  - Oynanıp **bitirilen** level'lar serbestçe **tekrar oynanabilir**.
  - **Oynanmamış** level'lar **sırayla** açılır (sadece bir sonraki kilitsiz).
- **Bitirilen dünyalara dönüş:** Oyuncu ana sayfadan daha önce bitirdiği dünyaya dönüp level'larını tekrar oynayabilir.
- **Grup yapısı:** 5 dünya = 1 grup. Grubun tüm dünyalarının tüm level'ları bitince **sonraki grubun 5 dünyası açılır** (Sprint 5 ile tam kurgu).
- **Dünya geçiş animasyonu:** Bir dünyanın son level'ı bitince oyun → ana sayfa; burada **sonraki dünyaya geçiş animasyonu** oynar, oyuncu **Continue** ile yeni dünyaya başlar.

---

## Kapsam / Görevler

- [x] **MainMenu sahnesi** (`Assets/Scenes/MainMenu.unity`) — `MainMenuBuilder` editor tool ile kuruluyor
- [x] **Dünya seçim ekranı:** 18 dünya ızgarası (3 sütun), kilitli olanlar soluk + kilit ikonu
- [x] **Level seçim ekranı:** seçili dünyanın 5 duraklı patikası; açık=beyaz circle, kilitli=kırmızı circle+kilit
- [x] **İlerleme okuma (PlayerPrefs, Sprint 1):** `LevelManager.UnlockedIndex` + `WorldCatalog.WorldUnlocked` UI'a yansıyor
- [x] **Play / level seç → GameScene'i doğru LevelData ile başlat** (`LevelManager.CurrentIndex`)
- [x] **Can göstergesi** (Sprint 2 LivesManager) + yenilenme sayacı — `LivesHud`
- [x] **Köstebek maskotu + tema** — `mole_mascot_warm` + `burrow_bg_warm`
- [x] **Dünya geçiş animasyonu + Continue:** panel fade+scale geçişi + "Devam Et" butonu (sıradaki level'a atlar).
      Not: dünyalar-arası tam geçiş kurgusu world>0 içeriğiyle (Sprint 5) tamamlanacak.
- [x] **Build Settings:** MainMenu=0, GameScene=1
- [x] **`GameManager.mainMenuScene` = "MainMenu"** (Cancel çalışıyor)
- [ ] (Opsiyonel) **Ayarlar:** ses/titreşim aç-kapa → Sprint 6

## Kabul Kriterleri
- Uygulama MainMenu ile açılıyor; dünya → level seçim akışı çalışıyor
- Kilitli dünyalar görünür ama seçilemiyor; bitirilen dünyalara dönülüp tekrar oynanabiliyor
- Oynanmamış level'lar sıralı kilitli; bitirilen level'lar serbest tekrar oynanabiliyor
- Level seç → oyun doğru LevelData ile başlıyor
- Dünyanın son level'ı sonrası ana sayfada geçiş animasyonu + Continue çalışıyor
- Fail → Cancel → MainMenu'ye sorunsuz dönülüyor; can/yenilenme doğru görünüyor

## Notlar / İlgili Dosyalar
- `Assets/Scripts/GameManager.cs` — `ExitToMainMenu()`, `mainMenuScene`, NextLevel'ın "son level → ana sayfa" dalı (Sprint 1)
- Yeni: `Assets/Scenes/MainMenu.unity`, `Assets/Scripts/UI/MainMenuController.cs`, `WorldSelectController.cs`, `LevelSelectController.cs`
- **Karar:** UI için **uGUI** (mevcut HUD/sonuç ekranlarıyla tutarlı, önerilen) mi, **App UI** mı? → Sprint başında netleştir.
