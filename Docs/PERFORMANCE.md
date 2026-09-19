# Performans

Referans build'in (optimizasyon öncesi) ve optimize build'in ölçümleri. Hepsi oyun içindeki benchmark ile aynı
koşullarda alındı (bkz. `TECH.md` > Benchmark modu).

## Test koşulları

| | |
|------|------|
| Cihaz | Xiaomi Redmi Note 14 Pro (4G, model 24116RACCG) |
| SoC / GPU | MediaTek Helio G100-Ultra (MT6789) / Mali-G57 MC2 |
| RAM / İşletim sistemi | 8 GB / Android 16 (HyperOS 3) |
| Ekran | 2400 x 1080, yatay |
| Grafik API'si | Vulkan |
| Build | Release (development değil), IL2CPP, ARM64, URP "Mobile" kalite seviyesi |
| Senaryo | BENCHMARK butonu: seed 12345, 150 düşman sınırı, hasar almayan ve yerinde duran oyuncu, 10 sn ısınma + 60 sn ölçüm, 120 FPS sınırı |
| Prosedür | Telefon şarjda, ekran açık, uygulama yeni başlatılmış, benchmark arka arkaya iki kez |

Ham sonuçlar: `Benchmarks/reference_run1.json`, `Benchmarks/reference_run2.json`, ekran görüntüsü `Benchmarks/reference_run2.jpg`.

## Referans build (`v1.0-reference`)

Orijinal modeller, dokular ve Humanoid Animator'lar; optimizasyon yok.

| Ölçüm | Koşu 1 | Koşu 2 |
|------|------|------|
| Ortalama FPS | 14,5 | 14,6 |
| 1% low FPS | 10,7 | 13,1 |
| Frame süresi ort. / p99 / en fazla (ms) | 69,0 / 93,4 / 161,3 | 68,6 / 76,4 / 169,8 |
| **Frame başına GPU süresi (ms)** | **69,0** | **68,6** |
| Frame başına CPU ana thread (ms) | 17,4 | 17,4 |
| Canlı düşman (150 sınırında ortalama) | 149,8 | 149,7 |
| Kill | 51 | 53 |
| Ayrılmış bellek | 119 MB | 119 MB |

İki koşu ortalama FPS, GPU ve CPU süresinde yaklaşık %1 içinde tutarlı. Spawn konumları aynı (sabit seed); kill
sayıları biraz farklı, çünkü simülasyon gerçek frame süresiyle ilerliyor ve mermi isabetleri koşular arasında birkaç
milisaniye kayıyor. Yükün kendisi (canlı düşman sayısı) aynı.

### Analiz

**Oyun GPU'ya takılıyor.** Frame süresi GPU süresine eşit (yaklaşık 69 ms), CPU ana thread ise sadece yaklaşık 17 ms
harcıyor. GPU maliyeti düşmeden CPU'yu hızlandırmak FPS'i artırmaz.

Olası GPU maliyetleri, beklenen etkiye göre sıralı:

1. **Geometri.** 150 düşman x 36.902 üçgen = frame başına yaklaşık 5,5 milyon üçgen, gölge haritası için bir kez
   daha çiziliyor. Mali-G57 MC2'nin 60 FPS'te kaldırabileceğinin çok ötesinde.
2. **Skinning.** Her düşman her frame 65 kemikle 18.453 vertex'lik bir mesh'i deforme ediyor. İş vertex sayısıyla
   ölçeklendiği için düşük poli mesh onu orantılı olarak azaltır; daha az kemik de ayrıca yardımcı olur.
3. **Dokular.** Sekiz 4096 x 4096 doku (iki materyal için diffuse, normal, specular, glossiness) mobil GPU'larda kıt
   olan bellek bant genişliğini harcıyor. Düşmanların ekrandaki boyutunda ASTC ile sıkıştırılmış tek bir 1024 (ya da
   daha küçük) atlas yeterli.
4. **Gölgeler ve materyaller.** 150 skinned mesh için gölge ve düşman başına iki materyal (iki draw call).

CPU tarafında (GPU maliyeti düşünce sıradaki darboğaz): 150 Humanoid Animator (retargeting Generic'ten pahalı),
her birinde 65 kemiğin animasyonu ve düşman mantığı.

### Optimizasyon planı (beklenen etkiye göre öncelikli)

| # | Değişiklik | Hedef | Durum |
|------|------|------|------|
| 1 | Düşman mesh'i Blender'da yeni dosya olarak yaklaşık 4-5 bin üçgene (LOD0) indirilir, bir alt LOD eklenir | GPU geometri, skinning | Yapıldı (Adım 1) |
| 2 | Düşman dokuları tek bir 1024 (ya da 512) atlasa birleştirilir, ASTC, tek materyal | GPU bant genişliği, draw call, bellek | Yapıldı (Adım 1) |
| 3 | Düşman gölgeleri (daha ucuz ya da uzakta gölge yok) | GPU gölge geçişi | Yapıldı (Adım 2, blob shadow) |
| 4 | Animator: daha az kemik (parmaklar atılır), daha ucuz culling modu, belki Generic klipler | CPU animasyon | Kemikler yapıldı (Adım 1); gerisi ölçüme göre |
| 5 | Oyuncu modeli: orta seviye decimation, birleştirilmiş materyaller; rifle dokuları 512 | GPU, bellek | Yapıldı (Adım 3, ölçüm Adım 4 ile birlikte) |

Her değişiklik bir sonrakine geçmeden önce aynı cihazda aynı benchmark ile tekrar ölçülür.

## Adım 1: optimize düşman (`v1.1.0`, commit `749ca37`)

Değişiklik: düşman prefab'ı `Enemy_Optimized`'ı kullanıyor (4.500 / 1.500 üçgenlik LOD'lar, 22 kemik, en fazla 4
ağırlık, tek materyal, 1024 x 512 ASTC atlas). Ayrıntılar `TECH.md` > Optimize asset'ler. Başka hiçbir şey değişmedi.

| Ölçüm | Referans (koşu 2) | Adım 1 koşu 1 | Adım 1 koşu 2 | Değişim |
|------|------|------|------|------|
| Ortalama FPS | 14,6 | 49,3 | 49,4 | **x3,4** |
| 1% low FPS | 13,1 | 29,5 | 39,2 | x3,0 |
| Frame süresi ort. / p99 (ms) | 68,6 / 76,4 | 20,3 / 33,9 | 20,2 / 25,5 | -%71 |
| **GPU süresi (ms)** | **68,6** | 19,9 | **19,9** | **-%71** |
| CPU ana thread (ms) | 17,4 | 12,4 | 12,2 | -%30 |
| Ayrılmış bellek | 119 MB | 112 MB | 112 MB | -%6 |
| APK boyutu | 50,2 MB | 41,8 MB | | -%17 |

Ham sonuçlar: `Benchmarks/step1_enemy_run1.json`, `Benchmarks/step1_enemy_run2.json`, `Benchmarks/step1_enemy_run2.jpg`.

**Yorum:** referans analizinin öngördüğü gibi en büyük GPU maliyeti düşman asset'iydi. CPU da hızlandı: düşman başına
43 kemik daha az canlandırılıyor ve daha az ağırlık skin'leniyor. Frame süresi (20,2 ms) hâlâ GPU süresine eşit; oyun
hâlâ GPU'ya takılıyor ve 60 FPS'e (16,7 ms) yaklaşık 3-4 ms uzak. Sıradaki: render ayarları (gölgeler, çözünürlük,
post-processing).

## Adım 2: mobil render ayarları (`v1.1.1`, commit `7cafdc1`)

Değişiklik (sadece Mobile kalite seviyesi): post-processing kapalı, HDR kapalı, gölge mesafesi 35 m, düşmanlar gerçek
zamanlı gölge yerine blob shadow kullanıyor. Ayrıntılar `TECH.md` > Render ayarları.

| Ölçüm | Referans | Adım 1 | Adım 2 koşu 1 | Adım 2 koşu 2 |
|------|------|------|------|------|
| Ortalama FPS | 14,6 | 49,4 | 78,3 | **78,3** |
| 1% low FPS | 13,1 | 39,2 | 58,7 | **58,7** |
| Frame süresi ort. / p99 / en fazla (ms) | 68,6 / 76,4 / 169,8 | 20,2 / 25,5 / 42,5 | 12,8 / 17,0 / 34,0 | **12,8 / 17,0 / 25,5** |
| GPU süresi (ms) | 68,6 | 19,9 | 12,5 | **12,6** |
| CPU ana thread (ms) | 17,4 | 12,2 | 10,3 | **10,4** |
| Ayrılmış bellek | 119 MB | 112 MB | 112 MB | 112 MB |

Ham sonuçlar: `Benchmarks/step2_render_run1.json`, `Benchmarks/step2_render_run2.json`, `Benchmarks/step2_render_run2.jpg`.

**Yorum:** post-processing'i, HDR'yi ve gölge atan 150 skinned mesh'i kaldırmak GPU süresinden 7 ms daha kazandırdı.
60 FPS hedefine ulaşıldı: ortalama frame 16,7 ms'lik bütçenin 12,8 ms'ini kullanıyor ve 1% low, benchmark'ın 120 FPS
sınırında 58,7 FPS (normal oyun 60'ta kilitli). CPU (10,4 ms) ve GPU (12,6 ms) artık birbirine yakın; tek bir baskın
darboğaz kalmadı. Toplamda: **referans FPS'inin 5,4 katı**.

## Adım 3: optimize oyuncu ve rifle dokuları (commit `46c3046`)

Değişiklik: oyuncu `Player_Optimized` (19.450 -> 7.999 üçgen, 2 mesh -> 1, 3 materyal slotu -> 2, dokular 512),
rifle dokuları 2048 -> 512. Ayrıntılar `TECH.md` > Optimize asset'ler > Oyuncu / Rifle.

Tek bir karakter olduğu için ayrıca ölçülmedi; etkisinin küçük olması bekleniyor. Adım 4 ile birlikte ölçülecek.

## Adım 3 + 4: optimize oyuncu ve düşman ayrışması (`v1.1.2`, commit `aebae33`)

Değişiklik: Adım 3'teki optimize oyuncu ve rifle dokularına ek olarak düşmanlar artık birbirini itiyor (spatial grid ile
ayrışma, yarıçap 1,2 m). Ayrıntılar `TECH.md` > Düşmanlar > Ayrışma.

| Ölçüm | Adım 2 | Adım 4 koşu 1 | Adım 4 koşu 2 |
|------|------|------|------|
| Ortalama FPS | 78,3 | 67,5 | **67,8** |
| 1% low FPS | 58,7 | 58,6 | **58,7** |
| Frame süresi ort. / p99 / en fazla (ms) | 12,8 / 17,0 / 25,5 | 14,8 / 17,1 / 42,4 | **14,7 / 17,0 / 33,9** |
| GPU süresi (ms) | 12,6 | 14,5 | **14,5** |
| CPU ana thread (ms) | 10,4 | 10,3 | **10,9** |
| Ayrılmış bellek | 112 MB | 111 MB | 111 MB |

Ham sonuçlar: `Benchmarks/step4_separation_run1.json`, `Benchmarks/step4_separation_run2.json`, `Benchmarks/step4_separation_run2.jpg`.

**Yorum: FPS düştü, ama ölçülen sahne değişti.**
- **CPU aynı kaldı** (10,3-10,9 ms, önceden 10,4). Ayrışmanın maliyeti ölçülemeyecek kadar küçük: grid 150 düşman
  için frame başına yaklaşık 1.600 mesafe kontrolü yapıyor, kaba kuvvet 22.350 yapardı. Editörde `EnemySystem.Tick`
  ayrışma dahil yaklaşık 0,1 ms.
- **GPU 1,9 ms arttı.** Ayrışmadan önce 150 düşman oyuncunun üstünde tek bir yığındı: ekranın küçük bir bölümünü
  kaplıyor, büyük kısmı birbirinin arkasında kalıyordu. Artık ekranın büyük bölümüne yayılıyorlar (ekran görüntüsünü
  Adım 2'ninkiyle karşılaştırın). GPU'nun boyadığı piksel sayısı ve ekranın yakın yarısında LOD0 ile çizilen düşman
  sayısı arttı. Benchmark artık daha gerçekçi ve daha ağır bir sahneyi ölçüyor.
- 60 FPS hedefi korunuyor: ortalama frame 16,7 ms'lik bütçenin 14,7 ms'ini kullanıyor, 1% low değişmedi.
- Adım 3'ün (oyuncu) ayrı etkisi bu ölçümde ayrıştırılamıyor; tek karakter için küçük olması bekleniyor.
