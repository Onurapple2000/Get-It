# Sprint 4 — Güç-Up / Materyal Sistemi

**Durum:** ✅ Tamamlandı (2026-06-27)
**Bağımlılık:** Yok (oynanış üstüne kurulur; Sprint 1 ile daha anlamlı)
**Amaç:** Belirli özel nesneler yutulunca **otomatik** geçici güç versin (oyuncu seçim yapmaz).

> GDD referans: "Güç Dönüşümü (Materyal Sistemi)":
> Altın → hız bonusu · Elmas → manyetik çekim · Araç → boyut patlaması.

---

## Kapsam / Görevler

- [x] **PowerUpType enum** (None/Speed/Magnet/SizeBurst) ve `PhysicsSwallowable.powerUp` alanı
- [x] **PowerUpManager:** süreli efektleri başlat/bitir + HUD bildirimi (GameScene'de GameManager objesinde)
- [x] **Altın → Hız bonusu:** `HoleController.moveSpeed` 6sn ×1.8 (sonra baseSpeed'e döner)
- [x] **Elmas → Manyetik çekim:** `PhysicsSwallowable.All` taranır, yakındakiler deliğe çekilir (bombalar hariç), 6sn
- [x] **Araç → Boyut patlaması:** `hole.Grow(0.6)` anlık + flash bildirimi
- [x] **Süre/efekt göstergesi:** sol-üst renkli etiket + boşalan bar (Hız=sarı, Mıknatıs=mavi)
- [x] **Prefablara güç-up ataması:** Meshy GLB'lerden `PowerGrow/PowerSpeed/PowerMagnet` prefabları
      (`PowerUpCreator` editor tool) + World0 Level1-3 spawn'larına grow×2/speed×2/magnet×1
- [x] **Öğretici ipucu (ekstra):** her gücün ilk 3 karşılaşmasında yuvarlak toast ile açıklama (PlayerPrefs)
- [ ] (Sprint 6/7 ile) güce özel ses + partikül + HUD fancy ikon — sonraki sprintlere bırakıldı

## Kabul Kriterleri
- Altın yutunca delik gözle görülür şekilde hızlanıyor, süre dolunca normale dönüyor
- Elmas yutunca yakın nesneler otomatik deliğe çekiliyor
- Araç yutunca delik bir anda büyüyor (patlama hissi)
- Aynı anda birden çok güç düzgün yönetiliyor (çakışma/süre)

## Notlar / İlgili Dosyalar
- `Assets/Scripts/Swallowable.cs` — yutulma anı (`SwallowRoutine`, `Grow`/`AddScore` çağrısı civarı)
- `Assets/Scripts/HoleController.cs` — `moveSpeed`, `currentSize`, `Grow()`
- Yeni: `Assets/Scripts/Systems/PowerUpManager.cs`, `Assets/Scripts/PowerUp.cs`
- `Assets/Prefabs/Gold_Ignots.prefab` vb. — güç-up ataması
