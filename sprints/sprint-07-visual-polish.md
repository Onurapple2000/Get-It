# Sprint 7 — Görsel Cila / Juice

**Durum:** 🟢 Uygulandı (kullanıcı testi bekliyor) — 2026-08-17
**Bağımlılık:** Sprint 4 (güçler), Sprint 6 (ses) — cila bunlarla birlikte daha etkili
**Amaç:** GDD'nin "Lüks Cartoon" hissini ve "her yutmada dopamin" geri bildirimini vermek.

> GDD referans: "Görsel Stil" (altın parlasın, elmas ışık kırsın, çekilme animasyonu) ve
> "her yutma anında skor animasyonu".

---

## Kapsam / Görevler

- [x] **Skor pop animasyonu:** "+10" baloncuğu pop-ölçekle (overshoot) yüzer; HUD skor yazısı değişince "punch" zıplar. — `ObjectiveTracker.ScorePopup`, `GameManager.Update` (scorePunch)
- [x] **Kamera shake:** büyük yutmada Perlin-tabanlı sarsıntı (SwallowSize>1.15). NOT: aktif kamera `HoleCamera` (CameraController sahnede KAPALI) → shake oraya taşındı, değerler belirginleştirildi. — `HoleCamera.Shake`
- [x] **Partikül efektleri:** yutma anında delik ağzında TOZ pufu + parlak GLINT (dumansı değil). HAVUZLANMIŞ (POOL_SIZE=6, GC yok), "show" throttle'ına saygılı. — yeni `SwallowVFX.cs`
- [~] **Parlama/shine:** ~~emisyon nabzı + twinkle~~ → **İPTAL** (kullanıcı beğenmedi + başka dünyaları etkiledi). ShineDriver silindi; oyunda parlama-nabız yok.
- [x] **UI geçişleri:** success/fail açılışında maskot zıplaması (easeOutBack) + başlık/buton pop. Yalnız isimli çocukların localScale'i → ödül-uçuş animasyonlarıyla çakışmaz. — `GameManager.PanelIntro/PopIn`
- [~] **Yutma "çekilme" cilası:** ~~delik ağzı "gulp" dalgası~~ → **İPTAL** (kullanıcı; denenen tüm varyantlar belirsiz/artefaktlı). "Çekilme hissi" yutma partikülü + kamera shake ile veriliyor. PhysicsSwallowable'a/collider'a DOKUNULMADI (burst-yutmada per-frame collider rescale = spike riski).
- [~] **Delik kenarı görseli (ops.):** dokunulmadı — tuğla halka zaten tuning'li.
- [ ] Renk/doygunluk geçişi GDD'ye göre zengin tutuldu mu kontrolü (kullanıcı gözden geçirmesi)

## Uygulama notları (2026-08-17)
- **Tween:** basit coroutine (DOTween YOK — ek paket/build boyutu yok). **Parlama:** emisyon+sparkle (Bloom YOK — mobil fill-rate riski yok). Kullanıcı kararı.
- **Perf tavanları:** VFX havuzu 6, twinkle havuzu 4; emisyon nabzı yalnız DISTINCT materyaller üzerinde döner (nesne sayısından bağımsız, sabit maliyet).
- **Yeni dosyalar:** `Assets/Scripts/Level/SwallowVFX.cs`, `Assets/Scripts/Systems/ShineDriver.cs`. Diğerleri mevcut dosyalara eklendi.
- Tümü atomik/geri-alınabilir; her efekt bağımsız. `git diff` ile tek tek geri alınabilir.

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
