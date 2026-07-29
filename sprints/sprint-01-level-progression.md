# Sprint 1 — Level & İlerleme Sistemi

**Durum:** 🟡 Devam — **Kota/Hedef çekirdeği BİTTİ ve onaylandı (2026-06-26)**

## ✅ Yapıldı — Kota/Hedef çekirdeği (sahnedeki mevcut nesnelerle, runtime spawn YOK)
- `PhysicsSwallowable`: `objectType` + `isBomb` + `ResolvedType`; yutunca → `GameManager.ReportSwallowed`.
- `GameManager`: bomba→fail, skor, `objectives.ReportSwallow`; eski "hepsini ye" köprüsü kaldırıldı.
- `Assets/Scripts/Level/ObjectiveTracker.cs`: inspector hedef listesi; prosedürel HUD (nesne RESMİ + kalan sayı);
  ghost nesne resmiyle sayaca uçar→sayı düşer→pulse; 0'da prosedürel yeşil ✓; tümü 0→success; her yutmada
  delik üstünde **+skor popup**.
- `Assets/Scripts/Level/IconRenderer.cs`: HUD/ghost ikonları runtime'da prefab thumbnail'ından (URP SubmitRenderRequest).
- GameScene: ObjectiveTracker GameManager objesinde; örnek hedef FlowerPot×3 / SmallBarrel×2 / StreetLamp×1.

## ▶️ Kalan (ikinci faz): LevelData(SO) + LevelManager spawn + ilerleme/PlayerPrefs + NextLevel/Retry + bombalı level + delik 1.5 reset.

---

**Durum (orijinal):** ⬜ Başlanmadı
**Bağımlılık:** Yok (temel sprint)
**Amaç:** "NextLevel = aynı sahneyi reload" yerine **gerçek bölüm ilerlemesi**. Her bölümün
kendi konfigürasyonu (süre, hedef nesneler/sayılar, bombalı mı) olsun ve oyuncu bölümler arasında ilerlesin.

farklı temalara sahip dünyalar olacak ana ekrandan oyuncu istediği dünyayı seçebilecek. daha sonra o dünya içerisindeki levellardan seçim yapacak. her dünya bir temaya sahip olacak, (araçlar, evler, yiyecekler, tatlılar, karma vs) her 5 dünya bölümü bir grup olacak ve bir grup seviyelerinin hepsi geçilince sonraki grubun 5 dünyası açılacak. velel ilerlemesi bu şekilde olacak. oyunce next level dediğinde aynı dünyanın bir sonraki level ına geçecek, dünyanın son level ı ise ana sayfaya geçerek orada bir sonraki dünya ya geçildiği animasyon ile gösterilecek ve kullanıcı continue diyerek yeni dünyanın levellerına başlayacak.

> GDD referans: "Temel Mekanikler", "Zorluk Eğrisi", "Dünya Tasarımı".
> Dünya/tema/grup yapısı Sprint 5'te derinleşir; bu sprint level akışını ve **kazanma/kaybetme** koşulunu kurar.

---

## Kararlaştırılan Mekanikler (2026-06-25)

- **Dünya başına 5 level** (şimdilik; ileride iyi dünyaların level sayısı artırılabilir — `LevelData` veri odaklı olduğu için kolay)
- **Kazanma = Kota/Hedef sistemi** (tüm nesneleri yeme DEĞİL):
  - Her level'da **belirli nesnelerden belirli sayıda** yutmak gerekir.
  - Üstte **HUD hedef çubuğu:** hedef nesne ikonları yan yana + her birinin yanında kalan sayı.
  - Bir hedef nesne yutulunca, o nesnenin **küçük hayaleti (ghost)** ekrandaki ilgili sayaca doğru **uçar**, sayaca varınca sayı **1 azalır**.
  - Sayaç **0** olunca ikonun üstünde **yeşil ✓** belirir.
  - **Tüm sayaçlar 0** → level kazanıldı (success ekranı).
  - Hedef olmayan nesneler de yutulabilir (skor/büyüme için) ama kazanmayı doğrudan etkilemez.
- **Kaybetme:**
  - **Süre bitti** → fail.
  - **Bombalı level'larda:** sahnedeki **bomba** yutulursa → anında fail. (Bombalar bazı level'larda olur, hepsinde değil → `LevelData` flag'i.)
- **Her level taze başlar:** delik `currentSize = 1.5`'ten başlar (dünya içinde taşınmaz).
- **Kilit/sıra:** Oynanmamış level'lar **sırayla** açılır. Oynanıp bitirilen level'lara oyuncu **serbestçe** dönüp tekrar oynayabilir (level seçim — Sprint 3).

---

## Kapsam / Görevler

- [ ] **LevelData (ScriptableObject):** `worldId`, `levelIndex` (0-4), `levelTime`,
      `spawn listesi` (prefab + adet), **`hedefler`** (nesne türü → gereken sayı), `bombsEnabled` (bool) + bomba spawn
- [ ] **LevelManager / LevelLoader:** aktif level'ı yükler, nesneleri + (varsa) bombaları spawn eder, hedefleri GameManager'a verir
- [ ] **Hedef/Kota sistemi (ObjectiveTracker):**
  - [ ] Hedef tanımını HUD'a çiz (ikon + kalan sayı, yan yana)
  - [ ] Hedef nesne yutulunca sayaç güncelle
  - [ ] **Ghost uçuş animasyonu:** yutulan nesnenin küçük kopyası sayaca uçar (varınca sayı düşer)
  - [ ] Sayaç 0 → ikon üstüne **yeşil ✓**
  - [ ] Tüm hedefler tamam → `TriggerSuccess()`
- [ ] **Bomba nesnesi:** `Swallowable` türevi/flag — yutulursa `TriggerFail()` (sadece `bombsEnabled` level'larda spawn)
- [ ] **GameManager refactor:** `levelTime` + kazanma koşulu LevelData/ObjectiveTracker'dan gelsin (mevcut "tüm Swallowable yutuldu" kaldır)
- [ ] **Taze başlangıç:** her level yüklenişinde delik `currentSize = 1.5`
- [ ] **İlerleme kaydı (PlayerPrefs):** dünya bazında en yüksek açılan level; success → sonraki level açılır
- [ ] **NextLevel():** aynı dünyanın bir sonraki level'ı; **son level ise** → ana sayfaya dön + dünya geçiş bayrağı (animasyon Sprint 3/5)
- [ ] **RetryLevel():** aynı level'ı taze kur
- [ ] İlk dünya için 5 örnek level verisi (artan zorluk, en az 1 bombalı)

## Kabul Kriterleri
- Level'da sadece **hedef nesneler+sayılar** tamamlanınca kazanılıyor (tüm nesneleri yemek gerekmiyor)
- Hedef yutulunca ghost sayaca uçuyor, sayı düşüyor, 0'da yeşil ✓ çıkıyor
- Süre biterse veya (bombalı level'da) bomba yutulursa kaybediliyor
- Her level delik 1.5'ten taze başlıyor
- Dünyanın son level'ı bitince ana sayfaya dönülüyor; ara level'larda Next Level sonraki level'ı kuruyor
- Oynanmamış level kilitli/sıralı; oynanmış level tekrar oynanabiliyor (PlayerPrefs)

## Notlar / İlgili Dosyalar
- `Assets/Scripts/GameManager.cs` — `levelTime`, `NextLevel()`, `RetryLevel()`, `TriggerSuccess/Fail`, eski kazanma koşulu (`CheckAllSwallowed`)
- `Assets/Scripts/Swallowable.cs` — hedef nesne tanımı + bomba türevi için
- `Assets/Scripts/HoleController.cs` — `currentSize` reset
- `Assets/Scenes/GameScene.unity` — tek oyun sahnesi, level'lar veri ile kurulur
- Yeni: `Assets/Scripts/Level/LevelData.cs`, `LevelManager.cs`, `ObjectiveTracker.cs`, `Assets/Levels/*.asset`
- Ghost uçuş animasyonunun **cilası** Sprint 7'de iyileştirilebilir; çekirdek feedback burada kurulur.
