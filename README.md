# DungeonWeaver

Unity 6 ile geliştirilen, seed tabanlı ve tekrar üretilebilir bir 2D procedural dungeon portfolyo prototipidir.

## Mevcut durum — Aşama 10

Proje şu anda:

- Çakışmayan dikdörtgen odaları aynı seed ile deterministik üretir.
- Oda merkezlerinden minimum spanning tree ve iki ek bağlantı kurar.
- Oda içlerinden geçen bağlantıları engelleyen, gerektiğinde grid BFS kullanan koridorlar üretir.
- Her graph bağlantısı için iki deterministik oda giriş hücresi, dış yön ve ilk koridor hücresi kaydı üretir.
- Oda ve koridorları ayrı Floor/Wall Tilemap katmanlarına çizer.
- Duvarları `TilemapCollider2D` ve `CompositeCollider2D` ile birleştirir.
- En soldaki odayı başlangıç, başlangıçtan en uzak graph-hop mesafesindeki odayı boss odası seçer.
- Oyuncuyu başlangıç odasının deterministik merkez hücresine yerleştirir.
- Yeni Input System üzerinden WASD, yön tuşları ve gamepad sol çubuğu ile 2D hareket sağlar.
- Kamerayı oyuncuyu yumuşak takip edecek ve üretilen Tilemap sınırlarında kalacak şekilde sınırlar.
- Play Mode başlangıcında isteğe bağlı olarak tam üretim hattını otomatik çalıştırır.
- Runtime panelinden yeni bir integer seed ile tek düğmede dungeon üretir.
- Son başarılı seed ile aynı dungeon'ı yeniden üretir.
- Her runtime üretiminden sonra oyuncuyu yeniden doğurur ve kamera sınırlarını yeniler.
- Oyuncunun oda, koridor veya dungeon dışındaki runtime alan durumunu hücre tabanlı olarak takip eder.
- Room enter/exit ve alan değişimi olaylarını doorway/connection bilgisiyle ilişkilendirir.
- Her oda için generation verisinden ayrı `Unvisited`, `Active` veya `Visited` runtime exploration state'i tutar.
- Oda ziyaret sayısını, 1 tabanlı ilk keşif sırasını ve son giriş/çıkış connection bilgisini kaydeder.
- Room state değişikliklerini area tracker olaylarından event tabanlı olarak günceller.
- Doorway kayıtlarından runtime sırasında görünür, collider tabanlı fiziksel kapılar üretir.
- Yeni Normal/Boss odalarına ilk girişte ilgili oda tarafındaki kapıları geçici olarak kilitler.
- `C` tuşuyla aktif kilitli odayı debug amaçlı temizler; Cleared odalar tekrar kilitlenmez.

Varsayılan `12345` seed değeriyle 15 oda üretilir; başlangıç odası `13`, boss odası `7` olur.

## Kullanım

1. Projeyi Unity `6000.3.9f1` veya uyumlu bir Unity 6 sürümüyle açın.
2. `Assets/Scenes/SampleScene.unity` sahnesini açın.
3. Play tuşuna basın; varsayılan `12345` seed değeri otomatik üretilir.
4. Sol üstteki seed alanına başka bir 32-bit integer yazın.
5. **Generate** düğmesine basın.
6. WASD, yön tuşları veya gamepad sol çubuğuyla yeni dungeon içinde dolaşın.

Negatif seed değerleri ile `int.MinValue` ve `int.MaxValue` desteklenir. **Regenerate** düğmesi son başarılı seed'i tekrar kullanır. `DungeonRuntimeController` üzerindeki `Generate On Start` kapatılırsa sahnedeki serialized dungeon korunur ve yalnızca UI üzerinden üretim yapılır.

Edit Mode test ve debug işlemleri için `DungeonGenerator` nesnesindeki Context Menu akışı kullanılmaya devam edilebilir:

   - **Generate Dungeon**
   - **Build Room Graph**
   - **Build Corridors**
   - **Build Doorways**
   - **Render Dungeon Tilemap**
   - **Assign Room Roles**
   - **Place Player At Start**

Sahne varsayılan ayarlarla hazır üretilmiş ve bağlantıları kurulmuş halde kaydedilmiştir. Seed veya üretim ayarı değiştirildiğinde yukarıdaki işlem sırası yeniden uygulanmalıdır; imza kontrolleri eski graph, koridor, rol ve Tilemap verisinin kullanılmasını engeller.

## Kod yapısı

```text
Assets/Scripts/ProceduralDungeon/
  Data/           Oda, bağlantı, koridor ve doorway veri modelleri
  Generation/     Oda, graph, koridor, doorway ve oda rolü üretimi
  Rendering/      Tilemap zemin/duvar çizimi
  Player/         Oyuncu hareketi, spawn ve kamera takibi
  Runtime/        Üretim hattı, alan/room state, fiziksel kapı, encounter ve uGUI paneli
  Visualization/  Scene görünümü Gizmo çizimleri
```

## Runtime üretim akışı

**Generate** işlemi eski area tracker, room/encounter state, fiziksel kapı, Tilemap, rol, doorway, koridor, graph ve oda verisini güvenli sırayla temizler. Ardından oda üretimi → graph → koridor → doorway verisi → Tilemap → start/boss rolleri → runtime area lookup → room state kayıtları → fiziksel kapılar → encounter kayıtları → player spawn → kamera bounds → ilk alan değerlendirmesi sırasını uygular. Her adım doğrulanır; başarısızlıkta sonraki adıma geçilmez, yarım veri temizlenir ve player hareketi kapalı tutulur.

Doorway kayıtları fiziksel kapılardan ayrı deterministik kaynak veridir. Her bağlantının oda sınırındaki giriş hücresini, koridora doğru kardinal yönünü ve ilk dış koridor hücresini tutar. Scene görünümündeki cyan gizmo işaretleri ham doorway verisini, Game görünümündeki runtime sprite'lar ise bu veriden üretilen fiziksel kapıları gösterir.

Runtime area tracker, player konumunu Grid hücresine çevirerek önce oda, sonra koridor lookup'unda arar. Gerçek alan değişimlerinde `RoomEntered`, `RoomExited` ve `AreaChanged` olayları üretir; doorway üzerinden yapılan geçişlerde ilgili connection bilgisini taşır. Bu yalnızca takip ve olay altyapısıdır; kapı, encounter veya minimap sistemi eklemez.

Runtime room state controller, area tracker'ın `AreaChanged` olayını kullanır. İlk girişte keşif sayısını ve sırasını atar; oda giriş/çıkışlarında visit count ile connection geçmişini günceller. Exploration state, ayrı tutulan encounter/Cleared durumunun yerine geçmez.

Runtime kapılar açıkken turkuaz ve geçirgen, kilitliyken kırmızı ve fiziksel engel olacak şekilde görünür. Start Room encounter dışı/Cleared başlar. Yeni bir Normal veya Boss odaya girildiğinde yalnızca o odaya ait kapılar güvenli biçimde kapanır; oyuncuyla çakışan giriş kapısı oyuncu hücreden ayrılana kadar bekletilir. Bu aşamadaki `C` komutu yalnızca geçici debug room-clear yöntemidir; düşman, combat veya gerçek encounter completion bulunmaz.

## Yol haritası

- [x] Aşama 1–5 — Deterministik oda, graph, koridor, Tilemap ve oynanabilir karakter
- [x] Aşama 6 — Runtime seed girişi ve tek düğmeli generation flow
- [x] Aşama 7 — Deterministik doorway/entrance verisi ve Scene gizmo doğrulaması
- [x] Aşama 8 — Runtime room/corridor takibi ve geçiş olayları
- [x] Aşama 9 — Runtime room state ve exploration verisi
- [x] Aşama 10 — Görünür fiziksel kapılar ve geçici room locking prototipi

## Kontroller

| Girdi | Eylem |
|---|---|
| `WASD` / yön tuşları | Hareket |
| Gamepad sol çubuğu | Hareket |
| `C` | Aktif Locked odayı geçici olarak Cleared yap |

Çapraz hareket normalize edilir; varsayılan hız `6` birim/saniyedir.

## Kapsam

Bu aşamada düşman, combat, health, damage, gerçek encounter completion, minimap, save/load, anahtar veya envanter sistemi bulunmaz. Prototip deterministik üretim hattı, runtime kapı/locking davranışı, alan ve exploration state takibi, oynanabilir hareket, fizik çarpışması ve kamera takibine odaklanır.
