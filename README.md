# Arena Survivor

Skyloft Studios Unity Developer case study için yapılmış, Survivor.io tarzı bir mobil oyun. Oyuncu tek bir arenada
sanal joystick ile hareket eder, rifle menzildeki en yakın düşmana otomatik ateş eder, dalgalar hâlinde gelen
zombilere karşı 3 dakika hayatta kalmaya çalışır.

- **Unity** 6000.3.16f1, URP 17.3, Input System 1.19, Android (IL2CPP, ARM64)
- **APK:** _(link eklenecek)_
- **Video:** _(link eklenecek)_

## Case gereksinimleri

| Gereksinim | Nasıl karşılandı |
|------|------|
| Tek arena, joystick ile hareket | 40 x 40 arena; ekranda dokunulan yerde beliren, kendi yazdığımız joystick (`VirtualJoystick` + `JoystickModel`) |
| Menzildeki düşmana otomatik saldırı | Rifle en yakın düşmanı hedefler; mermiler simüle edilir, yol üzerinde isabet testi yapılır (hızlı mermi düşmanın içinden geçmez) |
| Dalgalar, kovalama, hasar | `WaveSpawner` oyuncunun etrafında, ekran dışında bir halkada dalga çıkarır; `EnemySystem` bütün düşmanları tek döngüde günceller |
| 3 dakika hayatta kal = kazan, öl = kaybet | `GameSession` sayacı; kazanma ve kaybetme tam bir kez gerçekleşir |
| Sonuç ekranı: kill + tekrar oyna | "YOU SURVIVED" / "YOU DIED", kill, süre, toplam kill, Play Again ve Menu |
| Toplam kill uygulama kapanınca korunur | `persistentDataPath/save.json`, `JsonUtility`, atomik yazma (geçici dosya + değiştirme), bozuk dosyada varsayılanlar |
| Aynı sahnede 3 zorluk | `DifficultySettings` ScriptableObject'leri: düşman sayısı ve spawn sıklığı (aşağıda) |
| Verilen modellerin kullanılması | Oyuncu, düşman ve rifle verilen modellerden; orijinaller `Assets/Models` altında hiç değiştirilmedi, optimize sürümler ayrı dosyalarda |
| Unity MCP ile uçtan uca bir görev | Sahne, UI, Animator controller'lar ve bir optimizasyon adımı MCP ile yapıldı ve doğrulandı; bkz. [AI çalışma günlüğü](Docs/AI_WORKLOG.md) |
| Referans build, profil, optimizasyon, tekrar ölçüm | Oyun içi benchmark modu, aynı cihaz ve aynı koşullar; `v1.0-reference` ve `v1.1-optimized` tag'leri |

## Oynanış

| Mod | Açıklama |
|------|------|
| **Easy / Normal / Hard** | Case'in istediği 3 dakikalık tur. Zorluklar sadece spawn sıklığı, dalga boyutu ve aynı anda canlı düşman sınırıyla ayrılır. |
| **Endless** | Case sonrası eklenen ek mod. Süre yok, ölene kadar sürer. Düşmanlar XP ve can düşürür, seviye atlanınca oyun durur ve 3 yükseltme kartından biri seçilir (hasar, saldırı hızı, çoklu atış, menzil, can, hız, mıknatıs). Düşmanlar zamanla daha çok, daha dayanıklı ve daha hızlı gelir. En iyi süre ve seviye kaydedilir. |
| **Benchmark** | Sabit seed'li, 150 düşmanlı, tekrarlanabilir performans testi (aşağıda). |

| Zorluk | Spawn aralığı (sn) | Dalga boyutu | En fazla canlı düşman |
|------|------|------|------|
| Easy | 3,0 -> 1,5 | 2 -> 6 | 40 |
| Normal | 2,5 -> 1,0 | 3 -> 10 | 80 |
| Hard | 2,0 -> 0,6 | 4 -> 16 | 150 |

Değerler tur boyunca başlangıçtan bitişe doğru doğrusal olarak artar.

**Kontroller:** telefonda ekranın herhangi bir yerine dokunup sürükleyerek hareket; editörde WASD / ok tuşları ya da gamepad.
Oyun yatay (landscape) çalışır.

## Projeyi açmak ve build almak

1. Projeyi Unity **6000.3.16f1** ile açın (Git LFS gerekmez).
2. `Assets/Scenes/Arena.unity` sahnesini açıp Play'e basın.
3. Android build: **File > Build Profiles > Android**, sahne listesinde `Arena` var; **Build**. Player ayarları
   (IL2CPP, ARM64, LandscapeLeft, Frame Timing Stats) projede kayıtlı.

Testler: **Window > General > Test Runner > EditMode > Run All** (278 EditMode testi, Core sistemlerinin tamamı).

## Referans ve optimize sürüm

| Tag | İçerik |
|------|------|
| `v1.0-reference` | İlk çalışan build: orijinal modeller ve dokular, optimizasyon yok |
| `v1.1-optimized` | Optimize asset'ler, mobil render ayarları, animasyon optimizasyonu |

```
git checkout v1.0-reference   # referans build
git checkout v1.1-optimized   # optimize build
```

Endless modu `v1.1-optimized`'dan sonra eklendi; benchmark senaryosunu değiştirmez.

### Performans (Xiaomi Redmi Note 14 Pro, Mali-G57 MC2, 150 düşman)

| | Referans | Optimize | Değişim |
|------|------|------|------|
| Ortalama FPS | 14,6 | **83,8** | **x5,7** |
| GPU süresi | 68,6 ms | 11,7 ms | -%83 |
| CPU ana thread | 17,4 ms | 8,2 ms | -%53 |
| Ayrılmış bellek | 119 MB | 107 MB | -%10 |
| APK boyutu | 50,2 MB | ~42 MB | -%16 |

Oyun 60 FPS'e kilitli; optimize build bu bütçenin yaklaşık %70'ini kullanıyor. Her adım ayrı ayrı ölçüldü:
[Docs/PERFORMANCE.md](Docs/PERFORMANCE.md) (ham JSON sonuçları `Docs/Benchmarks` altında).

**Nasıl ölçüldü:** menüdeki BENCHMARK butonu sabit seed ile 150 düşmanlık bir tur başlatır; oyuncu hasar almaz ve
hareket etmez, 10 sn ısınmadan sonra 60 sn ölçülür. Ortalama FPS, 1% low, frame süreleri ve `FrameTimingManager`'dan
CPU/GPU süreleri ekranda gösterilir, JSON olarak kaydedilir ve logcat'e yazılır. Release build, Profiler bağlantısı
gerekmez.

### Asset optimizasyonu: kalite / performans ödünleşimleri

Orijinal modeller `Assets/Models` altında olduğu gibi duruyor. Optimize sürümler Blender script'leriyle
(`Tools/Blender`, arayüzsüz, tekrar üretilebilir) `Assets/Optimized` altına üretildi.

| Asset | Orijinal | Optimize | Kazanç | Bedeli |
|------|------|------|------|------|
| Düşman | 36.902 üçgen, 65 kemik, 2 materyal, 8 x 4096² doku | LOD0 4.500 / LOD1 1.500 üçgen, 22 kemik, 1 atlas materyal (1024 x 512, ASTC) | GPU'nun ana yükü; tek başına 14,6 -> 49,4 FPS | Parmaklar animasyonsuz, yakından daha az detay. Oyun kamerasından (düşman ~100 x 150 px) LOD0 ile LOD1 ayırt edilemiyor, render karşılaştırmasıyla kontrol edildi. |
| Düşman materyali | URP Lit + normal map | URP Simple Lit, normal map yok | GPU 13,2 -> 11,7 ms | Speküler vurgu yok; üç varyant aynı sahnede render edilip karşılaştırıldı, bu boyutta fark görünmüyor. |
| Düşman gölgesi | Gerçek zamanlı | Blob shadow (yumuşak yuvarlak quad) | Her düşmanın ikinci kez çizilmesi kalktı | Gölge vücudun şeklini taşımıyor. |
| Düşman animasyonu | Humanoid, kemikler GameObject | Pişirilmiş Generic klipler, Optimize Game Objects, Cull Completely | Animator süresi -%54, CPU 9,9 -> 8,4 ms | Klip değişince pişirme aracının tekrar çalıştırılması gerekiyor. |
| Oyuncu | 19.450 üçgen, 2 mesh, 3 materyal | 7.999 üçgen, 1 mesh, 2 materyal, 512² dokular | Bellek ve GPU | Parmak kemikleri bilinçli olarak korundu (tek karakter, rifle'ı tutan el görünüyor). |
| Rifle | 3 x 2048² doku | 3 x 512² (ASTC) | Bellek | Rifle ekranda 30-40 px; fark görünmüyor. |

Ayrıntılar ve her kararın gerekçesi: [Docs/TECH.md](Docs/TECH.md) > Optimize asset'ler.

## Mimari (kısaca)

- **Oyun mantığı saf C#** (`Assets/Scripts/Core`, `ArenaSurvivor.Core` assembly'si): MonoBehaviour yok, Unity'nin
  saatine bağımlı değil. Bütün oyun bir EditMode testinde, Play modu olmadan simüle edilebiliyor.
- **İnce Unity katmanı** (`Assets/Scripts/Unity`): girdi, view'lar, kamera, UI. Sadece `GameBootstrap`'in `Update()`'i
  var; bir frame'de olan her şeyin sırası tek bir metotta görünür.
- **Tek sistem, tek döngü:** 150 düşman için 150 `Update()` değil, `EnemySystem.Tick` içinde tek bir döngü.
  Düşman ayrışması için bir spatial grid (O(n²) yerine O(n)).
- **Pooling:** düşmanlar, mermiler, pickup'lar, view GameObject'leri ve partiküller; oyun sırasında
  `Instantiate`/`Destroy` yok, frame başına çöp yok.
- **Veri odaklı ayar:** oyuncu, düşman, silah, zorluklar ve Endless ScriptableObject'lerde (`Assets/Data`).
- DI framework'ü yok; bağımlılıklar constructor'dan geçiyor ve tek bir composition root'ta elle bağlanıyor.

```
Assets/
  Models/        verilen orijinal modeller (dokunulmadı)
  Optimized/     optimize modeller, dokular, materyaller, pişirilmiş klipler
  Scripts/Core   oyun mantığı (saf C#)
  Scripts/Unity  Unity katmanı
  Scripts/Editor editör araçları (klip pişirme, Endless kurulumu)
  Tests/EditMode Core testleri
  Data/          ScriptableObject ayarları
Tools/Blender/   asset analiz ve optimizasyon script'leri
Docs/            teknik döküman, performans, AI çalışma günlüğü
```

## Dökümanlar

| Dosya | İçerik |
|------|------|
| [Docs/TECH.md](Docs/TECH.md) | Her sistemin nasıl çalıştığı ve neden öyle yapıldığı |
| [Docs/PERFORMANCE.md](Docs/PERFORMANCE.md) | Referans ve optimize ölçümleri, adım adım |
| [Docs/AI_WORKLOG.md](Docs/AI_WORKLOG.md) | AI ile çalışma günlüğü: 3 kritik karar, MCP görevi, AI'ın hataları |

## Bilinen sınırlamalar

- Arena placeholder: düz zemin ve alçak duvarlar.
- Rifle Run klibinde sol el silahtan ayrılıyor (klibin kendi sınırlaması).
- Ses yok.
- Endless modu için ayrı bir cihaz ölçümü yapılmadı; düşman sayısı benchmark'ın altında (en fazla 100) tutuldu.
