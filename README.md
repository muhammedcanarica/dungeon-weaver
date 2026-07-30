# DungeonWeaver

DungeonWeaver, Unity 6 ile geliştirilen seed tabanlı ve tekrar üretilebilir bir 2D procedural dungeon paketi ve geliştirme prototipidir. Aşama 12D ile procedural dungeon çekirdeği Git URL üzerinden kurulabilen gerçek bir Unity Package Manager paketine dönüştürülmüştür.

## Mevcut özellikler

- Aynı seed ile aynı oda, graph, koridor, doorway, kapı ve enemy planı üretilir.
- Odalar deterministik `Rectangle`, `LShape`, `Cross` ve `Alcove` geometrileri kullanır; gerçek occupied/interior/boundary hücreleri rendering, doorway ve spawn sistemlerine taşınır.
- Her oda ayrıca deterministik bir `Compact`, `Standard`, `Large` veya `Arena` arketipine sahiptir. Arketip profilleri Inspector üzerinden ağırlık, minimum/maksimum boyut ve kullanılabilir alan eşikleriyle yapılandırılır.
- Varsayılan `12345` seed değeri 15 oda üretir. Oda boyutları global bir aralık yerine seçilen arketip profilinden gelir; varsayılan profiller 8x8 Compact odalardan 16x13 Arena odalara kadar uzanır.
- Graph topology; ölçeklenen bir Start–Boss critical path, kontrollü branch/dead-end odaları, sınırlı junction derecesi ve az sayıda anlamlı loop üretir. Varsayılan 15 odalık profil 3 dead-end, maksimum 4 room degree, 2 loop ve en az %45 critical-path oranı hedefler.
- Koridor genişliği 3 hücredir ve fiziksel kapılar açıklığın tamamını kapatır.
- Start, Normal ve Boss oda rolleri deterministik atanır.
- Room exploration ile encounter durumları birbirinden bağımsız tutulur.
- Doorway girişinde encounter pending olur; actor güvenli iç hücreye ulaştığında oda kilitlenir.
- Sample prototipi WASD/yön tuşları/gamepad hareketi, `Space` saldırısı, enemy spawn ve `C` debug clear davranışını sağlar.
- Runtime seed UI yeni seed üretimini ve son başarılı seed ile regenerate işlemini destekler.
- `PrototypePlayerHealth` can, invulnerability, damage ve death durumlarını yönetir.
- Enemy contact damage yalnızca aktif Locked encounter içinde hasar verir; oyuncu damage feedback alır.
- Death sonrasında aynı başarılı seed yeniden üretilir, oyuncu respawn olur ve gameplay yeniden etkinleştirilir.
- `PrototypeHealthUI` oyuncu canını runtime segmentleriyle gösterir.

## Katman sınırı

- **Core package:** `Packages/com.muhammedcanarica.dungeon-weaver`
- **Prototype/demo:** `Assets/Scripts/ProceduralDungeon/Samples/Prototype`

Health, combat, enemy implementation ve gameplay UI yalnızca Prototype katmanındadır. Prototype'ın Core'a bağımlı olması beklenen yöndür; Core, Prototype assembly'sine veya bu gameplay sistemlerine bağımlı değildir.

## Kod ve assembly yapısı

```text
Packages/com.muhammedcanarica.dungeon-weaver/
  package.json
  Runtime/
    DungeonWeaver.Core.asmdef
    Data/                 Dungeon veri modelleri
    Generation/           Oda, graph, koridor, doorway ve rol üretimi
    Rendering/            Tilemap üretimi
    Runtime/              Area, room state, door, encounter ve generation pipeline
    Player/               Genel actor spawn ve kamera bounds/takip sistemleri
    Visualization/        Runtime-safe dungeon Gizmos çizimi
  Editor/
    DungeonWeaver.Editor.asmdef
    Editor-only tile GUID ve Gizmo label köprüleri
  Tests/Editor/
    DungeonWeaver.Core.Tests.Editor.asmdef

Assets/Scripts/ProceduralDungeon/Samples/Prototype/
    DungeonWeaver.Samples.Prototype.asmdef
    Prototype movement, attack, enemy, input, UI ve runtime adapter

Assets/Tests/Editor/
  DungeonWeaver.Prototype.Tests.Editor.asmdef
```

### DungeonWeaver.Core

Core; shape-aware room data ve generation, topology-aware graph, corridor, doorway, room role, Tilemap, area tracking, exploration state, fiziksel kapılar, genel encounter lifecycle, actor spawn ve runtime generation pipeline sistemlerini içerir. `DungeonGenerator` üzerindeki shape ağırlıkları geometri çeşitliliğini; dört arketip profili ise seçim ağırlığı, boyut ve kullanılabilir alan eşiklerini ayarlar. `DungeonGraphAnalysis` degree, dead-end, junction, loop, diameter ve Start–Boss critical-path sorgularını sağlar. Aynı seed ve aynı ayarlar aynı arketipleri, oda geometrisini, graph topology'sini ve gerçek oda hücrelerini üretir.

Core doğrudan Input System, Unity UI, prototype player controller, player attack veya enemy sınıflarına referans vermez. Takip edilen varlık `Transform` ve `Collider2D` üzerinden bağlanır. `DungeonRoomEncounterController.TryClearRoom` ve `TryClearActiveRoom` API’leri enemy, puzzle, timer, boss veya başka bir oyun sisteminden çağrılabilir.

Core kendi başına player health, combat, enemy AI, death/respawn veya oyun içi UI sağlamaz; bu sistemler Prototype/demo katmanındadır.

### DungeonWeaver.Samples.Prototype

Samples/Prototype assembly’si Core üzerine kurulmuş örnek oyundur. Şunları içerir:

- `TopDownPlayerController` ve `TopDownPlayerAttack`
- `DungeonEnemy`, deterministik spawn planları ve `DungeonEnemyEncounterRegistry`
- `PrototypePlayerHealth`, `DungeonEnemyContactDamage` ve `PrototypePlayerDamageFeedback`
- `DungeonPrototypeDeathRespawn` ve `PrototypeHealthUI`
- `DungeonPrototypeRuntimeAdapter`
- `DungeonEncounterDebugInput`
- Runtime seed UI ve input-focus yardımcısı

Sample enemy registry, Core encounter event’ini dinler; oda Locked olduğunda düşmanları üretir ve son düşman öldüğünde genel Core clear API’sini çağırır. Enemy registry veya sample adapter olmadan da Core dungeon generation tamamlanabilir.

### DungeonWeaver.Editor

Editor assembly yalnızca Unity Editor’da derlenir. Tile asset GUID çözümleme ve Scene görünümündeki oda etiketleri burada bulunur; runtime Core assembly’si `UnityEditor` referansı taşımaz.

### Test assembly'leri

Package içindeki `DungeonWeaver.Core.Tests.Editor` yalnızca Core'a referans verir. Assets içindeki `DungeonWeaver.Prototype.Tests.Editor` ise sample combat, enemy, UI focus ve debug input davranışlarını doğrular.

## Kullanım

1. Projeyi Unity `6000.3.9f1` veya uyumlu Unity 6 sürümüyle açın.
2. `Assets/Scenes/SampleScene.unity` sahnesini açın.
3. Play’e basın; seed `12345` otomatik üretilir.
4. WASD, yön tuşları veya gamepad sol çubuğuyla hareket edin.
5. Locked odada `Space` ile saldırın.
6. Debug için `C` ile aktif Locked odayı temizleyin.
7. Seed panelinden yeni bir 32-bit integer üretin veya **Regenerate** kullanın.

UI input alanı odaktayken movement, attack ve debug clear girişleri engellenir.

## Core’u başka bir oyuna bağlama

1. `DungeonPlayerSpawner.SetTrackedActor` veya `DungeonRuntimeController.SetTrackedActor` ile actor bağlayın.
2. `DungeonRoomEncounterController.EncounterStateChanged` event’ini dinleyin.
3. Locked odada kendi enemy, puzzle, timer veya boss sisteminizi başlatın.
4. Tamamlandığında `TryClearRoom(roomId)` çağırın; Core pending kayıtlarını temizler ve kapıları açar.

## UPM kurulumu

Unity Package Manager içindeki **Add package from git URL** alanına şunu girin:

```text
https://github.com/muhammedcanarica/dungeon-weaver.git?path=/Packages/com.muhammedcanarica.dungeon-weaver
```

Paket yalnızca Core Runtime ve Editor entegrasyonunu getirir. Repository'nin `Assets/Scripts/ProceduralDungeon/Samples/Prototype` klasörü development/demo host projesine aittir ve pakete dahil değildir.

## Kapsam dışı

Gelişmiş boss davranışı, loot, minimap, save/load ve envanter bu prototipin kapsamında değildir.
