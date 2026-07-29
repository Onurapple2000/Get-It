# GET-IT — Sprint Planı

Bu klasör, [GET-IT Game Design Document](../GET-IT_Game_Design_Document.md)'taki özelliklerin
sprintlere bölünmüş hâlidir. Sırayla, birlikte yapacağız.

> **Çalışma şekli:** Bir sprint seçeriz → o sprintin görev listesini birlikte tamamlarız →
> kabul kriterleri karşılanınca `Durum: ✅ Tamamlandı` yapılır → sonrakine geçeriz.
> Her sprint dosyasının içinde görev checklistleri var; iş ilerledikçe işaretliyoruz.

---

## Mevcut Durum (Tarama: 2026-06-25)

### ✅ Halihazırda çalışan
- Delik hareketi + sınır (HoleController)
- Büyüme sistemi: `objectSize` eşiği, `growAmount`, devrilme (tipping) animasyonu (Swallowable)
- Kamera büyümeyle zoom-out (CameraController)
- Skor + 60sn süre (GameManager)
- Can: 5 başlangıç, fail'de -1 (ama kalıcı değil, yenilenmiyor)
- **Sonuç ekranları (Sprint 0 — tamamlandı):** köstebek temalı success + fail ekranı,
  Retry/Cancel/Next Level butonları, görsel asset'ler (sprite + zemin temizliği)

### ❌ Eksik → aşağıdaki sprintler
GDD'deki büyüme/güç/dünya/can-yenilenme/menü/ses/reklam/cila özellikleri henüz yok.

---

## Sprint Sırası

| # | Sprint | Bağımlılık | Durum |
|---|--------|-----------|-------|
| 0 | Sonuç Ekranları (Success/Fail) | — | ✅ Tamamlandı |
| ⚙️ | [**Çekirdek Fizik / Gerçekçi Yutma**](sprint-core-physics.md) | — | ✅ Tamamlandı |
| 1 | [Level & İlerleme Sistemi](sprint-01-level-progression.md) | ⚙️ | ✅ Tamamlandı |
| 2 | [Can Sistemi: Kalıcılık & Yenilenme](sprint-02-lives-persistence.md) | — | ✅ Tamamlandı |
| 3 | [Ana Menü & Navigasyon](sprint-03-main-menu.md) | 1, 2 | ✅ Tamamlandı |
| 4 | [Güç-Up / Materyal Sistemi](sprint-04-powerups.md) | — | ✅ Tamamlandı |
| 5 | [Dünyalar, Temalar & Zorluk Eğrisi](sprint-05-worlds-themes.md) | 1, 3 | ⬜ Başlanmadı |
| 6 | [Ses & Haptik](sprint-06-audio-haptics.md) | — | ✅ Tamamlandı |
| 7 | [Görsel Cila / Juice](sprint-07-visual-polish.md) | 4, 6 | ⬜ Başlanmadı |
| 8 | [Monetizasyon (AdMob)](sprint-08-monetization-admob.md) | 2, 3 | ⬜ Başlanmadı |
| 9 | [Build & Mağaza Hazırlığı](sprint-09-release-prep.md) | tümü | ⬜ Başlanmadı |

### Önerilen yol
**1 → 2 → 3** (oynanabilir iskelet: gerçek bölümler, kalıcı can, ana menü)
→ **4 → 5** (oyun derinliği: güçler, dünyalar)
→ **6 → 7** (his ve cila)
→ **8 → 9** (para kazanma ve yayın).

Sıra esnektir; istediğin sprintten başlayabiliriz.
