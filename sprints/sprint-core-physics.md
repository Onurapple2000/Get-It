# Sprint ⚙️ — Çekirdek Fizik / Gerçekçi Yutma (ÖNCELİK)

**Durum:** ⬜ Başlanmadı
**Bağımlılık:** Yok — **temel sprint, diğer her şeyden ÖNCE.**
**Amaç:** Yutma mekaniğini "sahte/script'li" yöntemden **gerçek fizik temelli geometri/oturma-alanı**
modeline taşımak. İstenen davranışların hepsi (takılma, sallayıp çıkarma, devirip-sığdırma) **emergent** olsun.

> Bu his oyunun çekirdeği; Sprint 1 (kota/ghost) ve sonrası bunun üstüne oturur.

---

## 🔖 Kaldığımız Yer / Sıradaki Adım (2026-06-26 session sonu — ADIM 3 BİTTİ)

**Aktif/oynanır sahne artık `Assets/Scenes/GameScene.unity`** (PhysicsTest hâlâ deney/sandbox olarak duruyor).
Unity'de aç, **odakta tut** — yoksa MCP timeout.

### ✅ ADIM 3 — GameScene Entegrasyonu TAMAMLANDI (2026-06-26)
GameScene eski prototip sahnesiydi (Abyss zemini + ~37 eski `Swallowable` test objesi). Komple temizlendi
ve PhysicsTest'teki doğrulanmış çekirdek fizik kuruldu. **Yutma, success, delik içi görünüm — hepsi onaylandı.**

**GameScene'in son temiz hâli (11 root):** Main Camera (HoleCamera) · Directional Light · Global Volume ·
Hole · Canvas · EventSystem · GameManager · **_DecoGroup** (yeni fizik prefabları) · **HoleFloor** ·
**HoleRim** · **Bounds** (4 duvar, ±15).

**Yapılanlar:**
1. **DecoObjectCreator menüsü çalıştırıldı** (Tools/GET_IT/Place Deco Objects) → prefablar yeni fizikle
   yeniden üretildi (serbest rotasyon + PhysicsSwallowable + compound collider). _DecoGroup taze yerleşti.
2. Eski **Ground** silindi → **HoleFloor** eklendi. **HoleRim** + **Bounds** (4 duvar) eklendi.
3. **Main Camera:** eski `CameraController` disabled, **HoleCamera** eklendi.
4. **Sahne temizliği:** `Abyss` (delikli OLMAYAN eski katı zemin — nesneleri tutup yutmayı engelliyordu!) +
   tüm eski Swallowable objeleri (Coin, Cube, SmallObj×8, MediumObj×4, 1_coin×10, Gold_Ignots, eski Taxi/Cop,
   2Story binalar, duplike ışıklar) SİLİNDİ (37 obje).
5. **Hole temizlendi:** eski disk MeshRenderer kapatıldı, Rigidbody constraints → None.
6. **Galip koşulu bağlandı:** `PhysicsSwallowable` yutunca → `GameManager.AddScore()` + `ObjectSwallowed()`.
   `GameManager.CheckAllSwallowed()` artık `PhysicsSwallowable` sayıyor (eski `Swallowable` değil). Hepsi
   yutulunca **success paneli** açılıyor. (Bu "hepsini ye=success" geçici köprü; asıl **kota/hedef sistemi**
   Sprint 1'de gelecek.)
7. **Delik içi görünüm güzelleştirildi:** `HoleFloor` artık deliğin içini **tek kaynaktan** çiziyor —
   2 submesh: üst halka = zemin malzemesi, çukur duvarı+dip kapağı = **koyu unlit gradient** malzeme
   (dip ~siyah → rim hafif toprak). Çakışan `HoleController._HoleDepth` kapatıldı (`createDepthPit=false`
   default) → z-fight bitti, zemin rengi artık sızmıyor. Dip kapağı gökyüzü görünmesini engelliyor.
   Ayarlanabilir: HoleFloor → `Pit Deep Color` / `Pit Rim Color`.

### ✅ ADIM 3.1 — Zemin geçirimsizliği & rim senkronu (2026-06-26, ek oturum, ONAYLANDI)
Yutma sırasında nesnelerin delik dışı sert zeminden geçmesi/içinde kalması düzeltildi:
- **HoleFloor artık hareket ediyor (rebuild etmiyor):** kinematik Rigidbody + Interpolate, mesh yerel uzayda,
  `FixedUpdate`'te `MovePosition` ile deliğin peşinden yumuşakça kayar (sadece büyüyünce rebuild). Çukur duvarı
  nesneyi normal kuvvetle iter → takla deliğin içinden tamamlanır, zemin iki yönde de geçirimsiz.
- **Üst yüzey sürtünmesiz** (hareketli zemin üstündeki dekoru sürüklemesin) + kapalı katı washer geometri.
- **Anti-burial yönü deliğe doğru** çevrildi.
- **HoleRim** de aynı kinematik-interpolate mekanizmasına geçti → hızlı sürüklemede tuğla halka kayıklığı bitti.
Detay: [[physics-swallow-learnings]] #11–14.

### ▶️ SIRADAKI
- **(Küçük, ertelendi) Cop araba prefabı:** GameScene'de araba yok (eski Cop silindi). PhysicsTest'teki Cop
  kurulumu (body BoxCollider, mass 8, serbest rotasyon, ContinuousSpeculative, PhysicsSwallowable growAmount
  0.3) referans. İstersen GameScene'e temiz bir araç prefabı ekleriz.
- **Sonra → Sprint 1: Kota/Hedef sistemi** (asıl galip koşulu): sahnedeki belirli nesnelerden belirli
  sayılarda ye; HUD hedef ikon+sayaç; yutunca "ghost" sayaca uçar, azalır; sayaç 0 → yeşil check; tüm
  sayaçlar 0 → success. Süre bitince / bomba yutunca fail. (Bkz. `sprints/` Sprint 1 dokümanı.)

### ⚠️ Not (PhysicsTest vs GameScene paylaşımı)
`HoleController.cs`, `HoleFloor.cs`, `PhysicsSwallowable.cs`, `HoleCamera.cs`, `HoleRim.cs` her iki sahnede
ORTAK. Birinde script davranışı değişince ikisi de etkilenir. Eski self-contained delik sistemi
(`_HoleDepth` görsel pit, disk mesh, `Swallowable.All` döngüsü) artık kullanılmıyor ama HoleController'da
kod olarak duruyor (zararsız; `createDepthPit=false`, eski Swallowable objesi yok → döngü boş).

---

### (Arşiv) Eski "Kaldığımız Yer" — PhysicsTest sandbox (2026-06-25/26)

**Çalışma sahnesi:** `Assets/Scenes/PhysicsTest.unity` (Unity'de aç, **odakta tut** — yoksa MCP timeout).
İçinde çalışan kurulum: `HoleFloor` (delikli zemin), sürülebilir `Hole` (HoleController), `HoleCamera`
(45°, merkezde, boyutla zoom), test nesneleri (PrimTree, pelletler, FallCube/RestCube) + gerçek
**Cop** arabası ve gerçek **Tree** prefab instance'ı. (Taxi silindi.)

**Çalıştığı doğrulanan davranışlar:** katı zemin, delikten geçiş, compound ile rim'e takılma,
sallayıp çıkarma, devirip-sığdırma, yutunca esneyen büyüme, **delik gidince doğal devrilerek çıkma**
(FreezeRotation kaldırılınca). Taxi'de compound tekerlek (1 teker çukura girince meyil) doğrulandı.

**Bitti:**
- `HoleFloor.cs`, `PhysicsSwallowable.cs` (yutma + anti-burial), `HoleCamera.cs`, `HoleController.Grow` (SmoothDamp)
- Taxi/Cop: Rigidbody + collider + PhysicsSwallowable (+ Taxi compound tekerlek demosu)
- Tree (PhysicsTest instance): compound (Trunk kapsül r0.3 h2.6 @y1.3 + Canopy küre r1.4 @y2.7),
  Rigidbody mass2/serbest rotasyon/ContinuousSpeculative, eski box+Swallowable disabled, PhysicsSwallowable
- `DecoObjectCreator.cs` **refactor edildi** (FreezeRotation yok, drag düşük, PhysicsSwallowable,
  BuildColliders: Tree compound). **Menü ÇALIŞTIRILMADI** (GameScene'i bozmamak için).

**2026-06-26 eklendi:**
- **Sınır (Bounds):** PhysicsTest'e 4 görünmez duvar (±14.5 arena) — nesneler savrulup/yuvarlanıp
  dışarı çıkamıyor. (Gerçek oyunda level/dünya kurulumundan gelecek.)
- **StreetLamp** compound: Pole kapsül + Head box (-X) + Base box. **Bench** compound: Body box + Back box (-Z).
- **DecoObjectCreator.BuildColliders TAMAM:** Tree + StreetLamp + Bench compound'ları kodda (AddCapsule/AddSphere/AddBox yardımcıları). Basitler default tek box. **Menü hâlâ çalıştırılmadı.**
- `.claude/settings.json` → `mcp__mcp-unity__*` allowlist (MCP onayları otomatik).

**2026-06-26 (2. tur) eklendi:**
- **HoleRim.cs** — deliğin ağzına dekoratif tuğla "kuyu ağzı" halkası (prosedürel doku, collider YOK, zeminle hizada, deliği takip eder). Sahnede `HoleRim` objesi. Onaylandı.
- **Takla (somersault) fix:** PhysicsSwallowable.Start → `rb.maxAngularVelocity = maxAngularSpeed` (varsayılan 5) → yavaş girişte yumuşak devrilme. Anti-burial'a "aşağı düşerken karışma" (`descending`) guard'ı.
- **Taxi SİLİNDİ:** kök+gövde aynı "Taxi" adını taşıdığı için düzenlemelerde yapı bozuldu (kök boş, parçalar dağıldı, gövde görünmez). Temizlendi. **Cop** sağlam araba örneği olarak duruyor. (İleride taxi gerekirse temiz instantiate + kökü hemen yeniden adlandır.)
- Açılış: `mcp__mcp-unity__*` allowlist `.claude/settings.json`'da.

**✅ Adım 3: GameScene Entegrasyonu — TAMAMLANDI (2026-06-26).** Detay yukarıdaki "Kaldığımız Yer" bölümünde.
1. ✅ DecoObjectCreator menüsü GameScene aktifken çalıştırıldı → prefablar yenilendi + _DecoGroup taze.
2. ✅ HoleFloor + HoleRim + Bounds eklendi, eski Ground silindi.
3. ✅ Eski Swallowable objeleri + Abyss zemini temizlendi; galip koşulu PhysicsSwallowable'a bağlandı.
4. ⏳ Araba (Cop) prefabı — ertelendi (GameScene'de şu an araba yok).

---

## İlerleme

**2026-06-25 — Faz 1+2 doğrulandı** (`Assets/Scenes/PhysicsTest.unity`, `Assets/Scripts/Physics/HoleFloor.cs`):
Delikli zemin mesh + hareketli MeshCollider çalışıyor. Canlı testte kanıtlanan: katı zemin
(nesne geçmiyor), delikten geçiş, **bileşik collider ile rim'e takılma** (ince gövde girer/geniş
tepe takılır), deliği hızlı oynatınca **sallayıp çıkarma**, **devirip-sığdırma**, nesnelerin
birbirine çarpması. Test rig: HoleFloor + sürülebilir HoleController + bileşik "ağaç"
(Trunk kapsül + Canopy küre) + küpler.

**2026-06-25 — Adım 1 tamam: yutma + büyüme + kamera** (doğrulandı):
- `Assets/Scripts/Physics/PhysicsSwallowable.cs` — nesnenin TAMAMI (tüm collider'larının tepesi)
  yer seviyesinin altına inince "yutuldu" sayılır → `HoleController.Grow()` + despawn.
  "Büyüyünce-sığma" testte doğrulandı (pelletleri yut → delik büyür → ağaç sığar).
- `HoleController.Grow()` artık **anlık değil**: `targetSize` + `SmoothDamp` ile **esneyerek** büyür
  (`growSmoothTime`). Bu gerçek oyun scriptinde de geçerli.
- `Assets/Scripts/Physics/HoleCamera.cs` — 45° açı, deliği **hep merkezde** tutar (XZ anında takip),
  delik büyüdükçe **yumuşak zoom-out** (`baseDistance`/`distancePerSize`/`zoomSmoothTime`).

**2026-06-25 — Adım 2 (gerçek nesneler) + kritik tecrübe:**
- Taxi & Cop arabalarına Rigidbody + collider + PhysicsSwallowable. Taxi'de **compound demo**:
  body box + 4 tekerlek SphereCollider (child) → bir teker çukura girince araba gerçekçi meyilleniyor.
- Gerçek **Tree** prefab'ı: gövde = kapsül (Trunk child), tepe = küre (Canopy child), eski box/Swallowable kapatıldı, PhysicsSwallowable eklendi.
- **⚠️ EN KRİTİK ÇÖZÜLEN SORUN — FreezeRotation:** DecoObject prefabları (DecoObjectCreator)
  Rigidbody'de `FreezeRotation` + `drag 0.4/0.5` ile geliyor → nesne **devrilemiyor**, delik gidince
  gömülü kalıyor / dikine çıkıyor (yapay). **Çözüm:** constraints=None, drag~0, angularDrag~0.05.
  Bununla ağaç primitive gibi **doğal devrilip yatık seriliyor.** Detay: [[physics-swallow-learnings]].
- Yardımcı: `PhysicsSwallowable`'a anti-burial (gömülü uca yukarı+yana kuvvet) — yedek; asıl çözüm serbest rotasyon.

**Kalan — GameScene entegrasyonu (Adım 3):**
- [ ] **TÜM swallowable prefablardan FreezeRotation kaldır** + drag düşür (DecoObjectCreator'ı güncelle — kaynak orası)
- [ ] Concave olanlara compound collider (Tree ✓ pattern, StreetLamp, Bench), basitler tek collider
- [ ] `Swallowable` → `PhysicsSwallowable` geçişi (eski script'li emme/tipping kaldır)
- [ ] GameScene'e HoleFloor + HoleCamera, eski zemini değiştir
- [ ] Tree sahne instance'ındaki disabled eski BoxCollider/Swallowable, prefab yeniden üretilince temizlenecek

---

## Kararlaştırılan Mekanikler (2026-06-25)

- **Geometriye dayalı yutma:** Nesne ancak **o anda deliğe bakan alt kesiti** delik açıklığından geçebiliyorsa düşer.
- **Rim'e takılma:** Geniş kısmı (örn. ağacın tepesi) delik kenarına oturur → nesne **sıkışır**, düşemez.
- **Sallayıp çıkarma:** Oyuncu deliği hızlıca oynatınca rim, sıkışan nesneyi iter; nesne ya geri çıkar ya da pozisyon değiştirir. Sıkışan nesne diğer nesnelere de **çarpıp** onları oynatır.
- **Devirip-sığdırma:** Ağaç devrilince yere temas alanı değişir; oyuncu nesneyi delikle oynatarak **uygun (küçük) yüzeyini tabana** getirip yutturabilir. Farklı yüzey alanlı nesneler, uygun yüzey altta ise yutulur.
- **Tam katı çarpışma:** Delik hariç **hiçbir nesne hiçbir nesneden geçmez**; **hiçbir parçası katı zeminden geçmez.** Çarpışırlar ve **çok hafif** savrulurlar.
- **Bomba:** Sadece **delik yeterince büyükse** yutulur (yutulursa fail — Sprint 1).

---

## Mimari — Kod Tarafı (ben yaparım)

- [ ] **Delikli zemin + hareketli MeshCollider:** Zemin, ortasında deliğin yarıçapı kadar **boşluk olan bir mesh**; delik hareket/büyüdükçe güncellenir. Mesh'in iç kenarı (**rim**) = gerçek çarpışma yüzeyi. (Performans için mesh/MeshCollider sadece delik belirli eşik kadar hareket edince yeniden üretilir.)
- [ ] **Çukur huni collider'ı:** Mevcut görsel çukura (silindir/huni) collider → içeri giren nesne dibe kayar, duvardan kaçmaz.
- [ ] **Yutma algısı:** Nesne çukur dibine (y < eşik) ulaşınca "yutuldu" → despawn + `HoleController.Grow()` + (Sprint 1) hedef sayacı.
- [ ] **Swallowable yeniden yazımı:** Script'li tipping/suck-in **kaldırılır**; collider hiç kapatılmaz; düşme tamamen fizikle olur. `IsBeingSwallowed` yerine "dibe ulaştı mı" mantığı.
- [ ] **Physics Material:** düşük bounciness + uygun sürtünme (hafif savrulma).
- [ ] **Layer & collision matrix:** Hole/Floor/Swallowable/Bomb layer'ları; gereksiz çarpışmalar kapatılır.
- [ ] **Rigidbody ayarları:** kütle, **Collision Detection = Continuous/ContinuousDynamic** (küçük-hızlı nesnelerin zeminden tünellemesini önler).
- [ ] **Physics proje ayarları:** sabit timestep + solver iteration artışı (jitter/stabilite).
- [ ] **HoleController uyarlaması:** delik yarıçapı/pozisyonu zemin mesh'ine ve rim'e beslenir.

## Unity Kurulumu — Kullanıcı Tarafı (seni yönlendiririm)

- [ ] **Her prefab'a bileşik (compound) primitive collider** — şeklin silüetini birkaç primitive ile temsil:
  - **Ağaç:** gövde = ince **CapsuleCollider**, tepe = geniş **SphereCollider** (veya BoxCollider). Böylece tepe rim'e takılır, gövde geçer.
  - Diğer nesneler: kutu/varil = Box, küre = Sphere, karışık = birkaç primitive.
  - **Not:** Tek **convex MeshCollider** YETMEZ (ağacı tek koniye çevirir, incelik kaybolur). Bu yüzden compound primitive.
- [ ] **Rigidbody** her swallowable'da (ayarları birlikte uygularız).
- [ ] Collider'ların **gerçek görsel boyutla** örtüşmesi (taban alanı doğru olsun — mekanik buna bağlı).
- [ ] (Birlikte) Physics Material ataması, layer ataması.

## Test Sahnesi (önce küçük doğrulama)
- [ ] Yeni `Assets/Scenes/PhysicsTest.unity`: 1 büyük delik + 1 ağaç + birkaç farklı boyutlu nesne + (opsiyonel) 1 bomba.
- [ ] Senaryolar elle doğrulanır (aşağıdaki kabul kriterleri).

## Kabul Kriterleri
- Küçük delik ağaca yaklaşınca **gövde girer ama tepe rim'e takılır**, ağaç düşmez
- Delik hızlı oynatılınca takılan ağaç **geri çıkar / oynar** ve yakın nesnelere çarpıp onları oynatır
- Ağaç **devrilip** uygun yüzeyi tabana gelince delikten **düşer**
- Delik büyüyünce daha önce sığmayan nesne artık yutulur
- **Hiçbir nesne** başka nesneden veya zeminden **geçmiyor**; çarpışmalar hafif savrulma veriyor
- Bomba sadece delik yeterince büyükse yutuluyor

## Notlar / İlgili Dosyalar
- `Assets/Scripts/HoleController.cs` — disk + görsel çukur (`_HoleDepth` mesh); buraya zemin-mesh + collider eklenecek
- `Assets/Scripts/Swallowable.cs` — **büyük oranda yeniden yazılacak** (script'li yutma kaldırılır)
- `Assets/Prefabs/**` — her prefab'a compound collider + Rigidbody
- Yeni: `Assets/Scripts/Physics/HoleFloor.cs` (delikli zemin mesh + MeshCollider), Physics Material, layer'lar
- **Riskler:** mesh cook maliyeti (throttle ile), tünelleme (Continuous ile), jitter (solver/timestep ile). Önce test sahnesinde çözülür.
- **Canlı test için Unity açık + odakta + MCP bağlı olmalı** (bkz. MCP bağlantı notları).
