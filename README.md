# DungeonWeaver

Unity 6 ile geliştirilen, seed tabanlı ve tekrar üretilebilir bir 2D procedural dungeon portfolyo prototipidir.

## Mevcut durum — Aşama 8

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
  Runtime/        Üretim hattı, alan takibi, geçiş olayları ve uGUI paneli
  Visualization/  Scene görünümü Gizmo çizimleri
```

## Runtime üretim akışı

**Generate** işlemi eski tracker, Tilemap, rol, doorway, koridor, graph ve oda verisini güvenli sırayla temizler. Ardından oda üretimi → graph → koridor → doorway verisi → Tilemap → start/boss rolleri → runtime area lookup → player spawn → kamera bounds → ilk alan değerlendirmesi sırasını uygular. Her adım doğrulanır; başarısızlıkta sonraki adıma geçilmez, yarım veri temizlenir ve player hareketi kapalı tutulur.

Doorway kayıtları gerçek bir kapı GameObject'i veya oynanış mekaniği değildir. Her bağlantının oda sınırındaki giriş hücresini, koridora doğru kardinal yönünü ve ilk dış koridor hücresini açık veri olarak tutar; sonraki kapı, geçiş ve içerik yerleşimi aşamalarına deterministik bir temel sağlar. Scene görünümündeki cyan gizmo işaretleri bu veriyi gösterir; Game görünümüne ek bir işaret çizilmez.

Runtime area tracker, player konumunu Grid hücresine çevirerek önce oda, sonra koridor lookup'unda arar. Gerçek alan değişimlerinde `RoomEntered`, `RoomExited` ve `AreaChanged` olayları üretir; doorway üzerinden yapılan geçişlerde ilgili connection bilgisini taşır. Bu yalnızca takip ve olay altyapısıdır; kapı, encounter veya minimap sistemi eklemez.

## Yol haritası

- [x] Aşama 1–5 — Deterministik oda, graph, koridor, Tilemap ve oynanabilir karakter
- [x] Aşama 6 — Runtime seed girişi ve tek düğmeli generation flow
- [x] Aşama 7 — Deterministik doorway/entrance verisi ve Scene gizmo doğrulaması
- [x] Aşama 8 — Runtime room/corridor takibi ve geçiş olayları

## Kontroller

| Girdi | Eylem |
|---|---|
| `WASD` / yön tuşları | Hareket |
| Gamepad sol çubuğu | Hareket |

Çapraz hareket normalize edilir; varsayılan hız `6` birim/saniyedir.

## Kapsam

Bu aşamada düşman, encounter, minimap, savaş, fiziksel/etkileşimli kapı, anahtar veya envanter sistemi bulunmaz. Prototip deterministik üretim hattı, doorway verisi, runtime alan takibi, oynanabilir hareket, fizik çarpışması ve kamera takibine odaklanır.
