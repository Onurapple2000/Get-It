# Sprint 5 — Dünyalar, Temalar & Zorluk Eğrisi

**Durum:** ⬜ Başlanmadı
**Bağımlılık:** Sprint 1 (level sistemi), Sprint 3 (dünya/level seçim UI)
**Amaç:** Tema bazlı dünyalar, grup/kilit açma mantığı ve zorluk eğrisini kurmak.

> GDD referans: "Dünya Tasarımı" ve "Zorluk Eğrisi". (GDD'deki Kuyumcu/Showroom örnekleri yerine
> kullanıcı temaları kullanılacak: araçlar, evler, yiyecekler, tatlılar, karma…)

---

## Kararlaştırılan Mekanikler (2026-06-25)

- **Her dünya = 1 tema.** Temalar (örnek): **araçlar, evler, yiyecekler, tatlılar, karma…** (genişletilebilir)
- **Dünya başına 5 level** (şimdilik; iyi dünyalarda artırılabilir).
- **5 dünya = 1 grup.** Bir grubun **tüm dünyalarının tüm level'ları** bitince → **sonraki grubun 5 dünyası açılır**.
- **Kilitli dünyalar görünür** ama oynanamaz (UI Sprint 3).
- **Dünya geçişi:** bir dünyanın son level'ı bitince → ana sayfa → **geçiş animasyonu** → Continue → yeni dünya.
- **Bombalar/engeller:** bazı dünya/level'larda bomba bulunur (yutulursa fail — Sprint 1); hangi dünyalarda olacağı tema/zorluğa göre.

---

## Kapsam / Görevler

- [ ] **WorldData (ScriptableObject):** `worldId`, `groupId`, tema adı, görsel tema (zemin/skybox/renk),
      nesne seti / spawn tablosu, **hedef nesne havuzu** (Sprint 1 kota sistemi için), bombalı mı
- [ ] **WorldData → 5 LevelData üretimi/eşlemesi** (artan zorluk: süre, nesne yoğunluğu, hedef sayıları)
- [ ] **Grup açma mantığı:** grup tamamlanınca sonraki grubun dünyalarını aç (PlayerPrefs)
- [ ] **İlk grup (5 dünya) iskeleti:** en az 2 dünyayı tam doldur (araçlar + yiyecekler gibi), kalanlar iskelet
- [ ] **Tema asset setleri:** her dünyaya nesne/zemin görselleri (mevcut prefablar + yeni; AI→freelancer GDD yolu)
- [ ] **Zorluk eğrisi parametreleri:** level içi süre/nesne/hedef ölçeklemesi (erken dünyalar kolay, sonrakiler zor)
- [ ] **Dünya geçiş animasyonu** (Sprint 3 ana sayfa ile ortak): yeni dünya açılış sahnesi + Continue
- [ ] Tema/zorluk geçişi **kademeli** (oyuncu şok olmasın)

## Kabul Kriterleri
- Her dünya kendi temasında (nesne seti + görsel zemin) yükleniyor; dünya başına 5 level var
- Bir grubun tüm dünya/level'ları bitince sonraki grubun 5 dünyası açılıyor
- Kilitli dünyalar görünür ama oynanamıyor
- Dünya bitince geçiş animasyonu oynuyor, Continue ile yeni dünyaya geçiliyor
- Zorluk dünyadan dünyaya ölçülebilir şekilde artıyor; erken dünyalar büyük oranda kazanılıyor

## Notlar / İlgili Dosyalar
- Yeni: `Assets/Scripts/Level/WorldData.cs`, `Assets/Worlds/*.asset`
- `Assets/Scripts/Level/LevelData.cs` (Sprint 1) — WorldData'dan beslenir
- `Assets/Prefabs/` — tema bazlı nesne setleri (Taxi/araçlar; yiyecek/tatlı için yeni asset)
- Görsel temalar için asset ihtiyacı (Asset Store / AI → freelancer; GDD "Asset Yol Haritası")
