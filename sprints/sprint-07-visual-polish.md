# Sprint 7 — Görsel Cila / Juice

**Durum:** ⬜ Başlanmadı
**Bağımlılık:** Sprint 4 (güçler), Sprint 6 (ses) — cila bunlarla birlikte daha etkili
**Amaç:** GDD'nin "Lüks Cartoon" hissini ve "her yutmada dopamin" geri bildirimini vermek.

> GDD referans: "Görsel Stil" (altın parlasın, elmas ışık kırsın, çekilme animasyonu) ve
> "her yutma anında skor animasyonu".

---

## Kapsam / Görevler

- [ ] **Yutma "çekilme" cilası:** nesne direnir → içeri çekilir hissini güçlendir (mevcut tipping üstüne)
- [ ] **Partikül efektleri:** yutma tozu/parıltı, güç-up patlaması (Sprint 4)
- [ ] **Parlama/shine:** altın ve elmas nesnelerde ışık/emisyon (materyal veya shader)
- [ ] **Skor pop animasyonu:** skor artışında "+10" yüzen yazı + sayaç tween
- [ ] **Kamera shake:** büyük yutma / boyut patlamasında hafif sarsıntı
- [ ] **UI geçişleri:** success/fail ekranı açılış animasyonu (köstebek zıplaması, buton pop)
- [ ] **Delik kenarı görseli:** çukur kenarına çerçeve/gölge ile daha "premium" görünüm (opsiyonel)
- [ ] Renk/doygunluk geçişi GDD'ye göre zengin tutuldu mu kontrolü

## Kabul Kriterleri
- Yutma anı görsel olarak tatmin edici (partikül + animasyon)
- Altın/elmas gözle görülür şekilde parlıyor
- Skor artışı animasyonlu, his veriyor
- Success/fail ekranları statik değil, canlı açılıyor

## Notlar / İlgili Dosyalar
- `Assets/Scripts/Swallowable.cs` — `SwallowRoutine` (çekilme/scale/rot)
- `Assets/Scripts/Editor/SuccessScreenBuilder.cs` — sonuç ekranı animasyon hook'u
- Yeni: partikül prefabları `Assets/Art/VFX/`, shader/materyal `Assets/Shaders/`
- Tween için DOTween veya basit coroutine (karar Sprint başında)
