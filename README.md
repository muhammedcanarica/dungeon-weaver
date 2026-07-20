# Procedural Dungeon Prototype

Unity 2D üzerinde geliştirilen, seed tabanlı ve tekrar üretilebilir procedural dungeon sistemlerini araştıran bağımsız bir portfolyo prototipidir.

Projenin amacı yalnızca rastgele odalar çizmek değil; ilerleyen aşamalarda erişilebilirliği doğrulanabilen, oynanış kurallarıyla zenginleştirilebilen ve aynı seed ile yeniden oluşturulabilen modüler bir dungeon üretim hattı geliştirmektir.

> Bu proje başka bir oyuna bağlı değildir. Dungeon generation, veri modelleme, deterministik algoritmalar ve Unity editör araçları üzerine odaklanan bağımsız bir teknik çalışmadır.

## Mevcut Durum

İlk geliştirme aşaması tamamlandı. Proje şu anda:

- Inspector üzerinden ayarlanabilen bir seed kullanır.
- Ayarlanabilir sayıda dikdörtgen oda üretir.
- Minimum ve maksimum oda boyutlarını destekler.
- Odaları belirlenen `RectInt` üretim alanı içinde tutar.
- Odaların birbiriyle çakışmasını engeller.
- Odalar arasında ayarlanabilir minimum boşluk bırakır.
- Toplam yerleştirme denemesi bütçesiyle sonsuz döngüyü önler.
- Aynı seed ve aynı ayarlarla aynı sıralı oda listesini üretir.
- Odaları, merkezlerini ve oda ID değerlerini Scene görünümünde Gizmos ile gösterir.
- Oda verisini ayrı GameObject'ler oluşturmadan serialized bir listede saklar.

Henüz koridor, Tilemap çizimi veya oynanış içeriği bulunmamaktadır.

## Kullanım

1. Projeyi Unity `6000.3.9f1` veya uyumlu bir Unity 6 sürümüyle açın.
2. `Assets/Scenes/SampleScene.unity` sahnesini açın.
3. Hierarchy içindeki `DungeonGenerator` nesnesini seçin.
4. Inspector'da `DungeonGenerator` component başlığındaki üç nokta menüsünü açın.
5. Odaları oluşturmak için **Generate Dungeon** seçeneğini kullanın.
6. Üretilen veriyi temizlemek için **Clear Dungeon** seçeneğini kullanın.
7. Oda sınırlarını ve ID etiketlerini görebilmek için Scene penceresindeki **Gizmos** seçeneğini açık tutun.

### Varsayılan Üretim Ayarları

| Ayar | Varsayılan değer |
|---|---:|
| Seed | `12345` |
| Hedef oda sayısı | `15` |
| Üretim alanı | `x=-25, y=-15, width=50, height=30` |
| Minimum oda boyutu | `4 x 4` |
| Maksimum oda boyutu | `9 x 8` |
| Minimum oda boşluğu | `1` hücre |
| Maksimum yerleştirme denemesi | `500` |

## Üretim Mantığı

Her Generate işleminde yerel bir `System.Random` örneği verilen seed ile yeniden oluşturulur. Aday oda değerleri her zaman aynı sırayla üretilir:

1. Genişlik
2. Yükseklik
3. X koordinatı
4. Y koordinatı

Aday oda üretim alanının dışına taşıyorsa veya mevcut odalardan biriyle minimum boşluk kuralını ihlal ediyorsa reddedilir. Geçerli odalara eklenme sırasına göre `0` değerinden başlayan benzersiz bir oda ID'si verilir.

Hedef oda sayısına deneme bütçesi içinde ulaşılamazsa generator hata fırlatmaz. Geçerli odaları korur ve Unity Console'a açıklayıcı bir warning yazar.

## Deterministik Davranış

Proje `UnityEngine.Random` global durumunu değiştirmez. Üretimde zaman, GUID veya sahne nesnesi sırası kullanılmaz.

Aşağıdaki koşullar aynı kaldığı sürece aynı dungeon düzeni yeniden oluşturulur:

- Seed değeri
- Inspector üretim ayarları
- Oda üretim algoritmasının sürümü
- Unity/.NET çalışma ortamı

Farklı seed değerleri genellikle farklı düzenler üretir, ancak her farklı seed için matematiksel olarak benzersiz bir dungeon garanti edilmez.

## Kod Yapısı

```text
Assets/
  Scripts/
    ProceduralDungeon/
      Data/
        RoomData.cs
      Generation/
        DungeonGenerator.cs
      Visualization/
        DungeonGizmoDrawer.cs
```

- **RoomData:** Oda ID'sini, sınırlarını ve türetilmiş merkez/boyut bilgilerini taşıyan sade serializable veri sınıfıdır.
- **DungeonGenerator:** Ayar doğrulama, deterministik aday üretimi, sınır kontrolü ve spacing-aware çakışma kontrolünden sorumludur.
- **DungeonGizmoDrawer:** Generator verisini değiştirmeden üretim alanını, odaları, merkezleri ve oda ID etiketlerini çizer.

## Planlanan Özellikler

Projenin sonraki aşamalarında aşağıdaki sistemlerin eklenmesi planlanmaktadır:

- Odalar arasında koridor üretimi
- Oda bağlantı graph'ı
- Tüm dungeon için BFS erişilebilirlik kontrolü
- Başlangıç ve boss odası seçimi
- Anahtar ve kilitli kapı ilişkisi
- Başlangıç noktasına olan mesafeye göre düşman zorluğu
- Tilemap tabanlı zemin ve duvar çizimi
- Minimap
- Deterministik içerik yerleşimi
- EditMode otomatik testleri

Bu özellikler aşamalı olarak eklenecek; mevcut oda verisi ve üretim mantığı görselleştirme/oynanış katmanlarından ayrı tutulacaktır.

## Yol Haritası

### Aşama 1 — Temel oda üretimi

- [x] Seed tabanlı üretim
- [x] Çakışmayan dikdörtgen odalar
- [x] Minimum oda boşluğu
- [x] Maksimum deneme bütçesi
- [x] Gizmo görselleştirmesi
- [x] Inspector Context Menu işlemleri

### Aşama 2 — Bağlantılar ve doğrulama

- [ ] Oda merkezleri arasında bağlantı graph'ı
- [ ] Koridor üretimi
- [ ] BFS erişilebilirlik kontrolü
- [ ] Bağlantısız düzenlerin onarılması veya reddedilmesi

### Aşama 3 — Oynanış kuralları

- [ ] Başlangıç ve boss odası
- [ ] Anahtar ve kilitli kapılar
- [ ] Mesafe tabanlı zorluk dağılımı

### Aşama 4 — Görsel çıktı

- [ ] Tilemap çizimi
- [ ] Oda/koridor duvarları
- [ ] Minimap

## Mevcut Sınırlamalar

- Odalar arasında henüz koridor veya bağlantı bilgisi yoktur.
- Üretim yalnızca dikdörtgen odaları kapsar.
- Yoğun ayarlarda hedeflenen oda sayısından daha az oda üretilebilir.
- Çakışma kontrolü oda çiftlerini karşılaştırdığı için mevcut sürümde `O(n²)` maliyetlidir.
- Gizmo çizimi geliştirme amaçlıdır; runtime dungeon görünümü değildir.
- Aynı seed'in farklı algoritma veya runtime sürümlerinde aynı sonucu vermesi garanti edilmez.

## Portfolyo Odağı

Bu prototip özellikle aşağıdaki teknik konuları göstermeyi amaçlar:

- Deterministik procedural generation
- Unity serialization ve Inspector ayarları
- Global state kullanmadan rastgele sayı yönetimi
- Veri, üretim ve görselleştirme sorumluluklarının ayrılması
- Grid tabanlı geometrik sınır ve çakışma kontrolleri
- Aşamalı ve test edilebilir sistem tasarımı
