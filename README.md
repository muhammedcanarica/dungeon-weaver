# DungeonWeaver

Unity 6 ile geliştirilen, seed tabanlı ve tekrar üretilebilir bir 2D procedural dungeon portfolyo prototipidir.

## Mevcut durum — Aşama 5

Proje şu anda:

- Çakışmayan dikdörtgen odaları aynı seed ile deterministik üretir.
- Oda merkezlerinden minimum spanning tree ve iki ek bağlantı kurar.
- Oda içlerinden geçen bağlantıları engelleyen, gerektiğinde grid BFS kullanan koridorlar üretir.
- Oda ve koridorları ayrı Floor/Wall Tilemap katmanlarına çizer.
- Duvarları `TilemapCollider2D` ve `CompositeCollider2D` ile birleştirir.
- En soldaki odayı başlangıç, başlangıçtan en uzak graph-hop mesafesindeki odayı boss odası seçer.
- Oyuncuyu başlangıç odasının deterministik merkez hücresine yerleştirir.
- Yeni Input System üzerinden WASD, yön tuşları ve gamepad sol çubuğu ile 2D hareket sağlar.
- Kamerayı oyuncuyu yumuşak takip edecek ve üretilen Tilemap sınırlarında kalacak şekilde sınırlar.

Varsayılan `12345` seed değeriyle 15 oda üretilir; başlangıç odası `13`, boss odası `7` olur.

## Kullanım

1. Projeyi Unity `6000.3.9f1` veya uyumlu bir Unity 6 sürümüyle açın.
2. `Assets/Scenes/SampleScene.unity` sahnesini açın.
3. `DungeonGenerator` nesnesinde sırayla şu Context Menu işlemlerini çalıştırın:
   - **Generate Dungeon**
   - **Build Room Graph**
   - **Build Corridors**
   - **Render Dungeon Tilemap**
   - **Assign Room Roles**
   - **Place Player At Start**
4. Play Mode'a girin ve WASD, yön tuşları veya gamepad sol çubuğu ile hareket edin.

Sahne varsayılan ayarlarla hazır üretilmiş ve bağlantıları kurulmuş halde kaydedilmiştir. Seed veya üretim ayarı değiştirildiğinde yukarıdaki işlem sırası yeniden uygulanmalıdır; imza kontrolleri eski graph, koridor, rol ve Tilemap verisinin kullanılmasını engeller.

## Kod yapısı

```text
Assets/Scripts/ProceduralDungeon/
  Data/           Oda, bağlantı ve koridor veri modelleri
  Generation/     Oda, graph, koridor ve oda rolü üretimi
  Rendering/      Tilemap zemin/duvar çizimi
  Player/         Oyuncu hareketi, spawn ve kamera takibi
  Visualization/  Scene görünümü Gizmo çizimleri
```

## Kontroller

| Girdi | Eylem |
|---|---|
| `WASD` / yön tuşları | Hareket |
| Gamepad sol çubuğu | Hareket |

Çapraz hareket normalize edilir; varsayılan hız `6` birim/saniyedir.

## Kapsam

Bu aşamada düşman, savaş, kapı, anahtar veya envanter sistemi bulunmaz. Prototip deterministik üretim hattı, oynanabilir hareket, fizik çarpışması ve kamera takibine odaklanır.
