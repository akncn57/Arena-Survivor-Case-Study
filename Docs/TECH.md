# Arena Survivor: Teknik Genel Bakış

Projenin nasıl yapılandığı ve her sistemin ne yaptığı. Her yeni sistemle birlikte güncellenir.
Sınıf, dosya ve klasör adları koddaki gibi (İngilizce) bırakılmıştır.

## Mimari

Oyun mantığı saf C# sınıflarından oluşur. MonoBehaviour'lar yalnızca bu mantığı Unity'ye bağlayan ince bir
katmandır: girdiyi okumak, transform'ları hareket ettirmek, animasyon oynatmak, UI göstermek.

Neden:
- **Test edilebilir.** Saf sınıflar bir EditMode unit testinde `new` ile oluşturulabilir; sahne ya da Play modu gerekmez.
- **Hızlı.** Tek bir sistem bütün düşmanları tek döngüde günceller; yüzlerce MonoBehaviour'da yüzlerce
  `Update()` çağrısı yoktur.
- **Açık.** Bağımlılıklar constructor'dan geçer; her sınıfın neye ihtiyaç duyduğu görünür.

Dependency injection framework'ü kullanılmıyor; bağımlılıklar constructor'dan veriliyor. Bağlama iki yerde yapılır:
- `GameWorld` (Core) bütün oynanış sistemlerini oluşturur ve event'lerini birbirine bağlar. Saf C# olduğu için
  oyunun tamamı bir testte simüle edilebilir.
- Sahnedeki bootstrap MonoBehaviour'ı (Unity tarafı) veri asset'lerinden `GameWorld`'ü oluşturur, her frame
  girdiyi verir ve görsel katmanı event'lerine bağlar.

VContainer gibi bir container değerlendirildi. Bu proje ölçeğinde elle bağlama daha kısa, paket bağımlılığı
getirmiyor ve her bağlantıyı tek dosyada görünür tutuyor. Sınıflar zaten constructor injection kullandığı için
ileride geçiş yapmak yalnızca bağlama kodunu değiştirir.

### Assembly'ler

| Assembly | Klasör | İçerik | Referanslar |
|----------|--------|--------|-------------|
| `ArenaSurvivor.Core` | `Assets/Scripts/Core` | Oyun mantığı, saf C#, MonoBehaviour yok | Sadece UnityEngine (matematik tipleri, JsonUtility) |
| `ArenaSurvivor.Unity` | `Assets/Scripts/Unity` | İnce Unity katmanı: bootstrap, view'lar, girdi, kamera, UI | Core, Input System, uGUI, TextMeshPro |
| `ArenaSurvivor.Tests.EditMode` | `Assets/Tests/EditMode` | Core için NUnit EditMode testleri | Core, Unity Test Framework |

Assembly tanımları derleme sürelerini kısa tutar ve bağımlılık yönünü zorlar: Core, Unity katmanını ya da testleri göremez.

### Testleri çalıştırmak

Unity: **Window > General > Test Runner > EditMode > Run All**.
Test klasörleri kaynak klasörlerle aynı düzendedir (`Core/Save` -> `Tests/EditMode/Save`).

## Sistemler

### Kayıt (`Core/Save`)

Toplam kill sayısını uygulama kapanıp açılsa da korur.

| Tip | Görev |
|------|------|
| `SaveData` | Saklanan veri. `version` alanı ve `totalKills` içeren serileştirilebilir sınıf. |
| `ISaveService` | `SaveData` yükleme/kaydetme arayüzü. Oyun kodu sadece bu arayüzü bilir. |
| `JsonFileSaveService` | `SaveData`'yı bir dosyada JSON olarak saklar (oyunda: `Application.persistentDataPath/save.json`). |
| `ProgressService` | Oyunun kullandığı kısım: `TotalKills` ve `AddKills(int)`. Bir kez yükler, her değişiklikte kaydeder. |

Akış:

```
Oyun başlangıcı: ProgressService(new JsonFileSaveService(path))  -> Load() -> veri bellekte tutulur
Tur bitişi:      progress.AddKills(runKills)                     -> Save() -> dosya yazılır
```

Davranış ayrıntıları:
- **Atomik yazma.** `Save` önce `save.json.tmp` dosyasına yazar, sonra `File.Replace` ile (ilk seferde
  `File.Move`) yerine koyar. Uygulama yazma sırasında kapanırsa eski kayıt sağlam kalır.
- **Oyunu asla durdurmaz.** Eksik, boş ya da bozuk dosya varsayılan değerlerle (0 kill) yüklenir ve bir uyarı loglanır.
- **Temizlenir.** Elle düzenlenmiş dosyadaki negatif kill sayısı 0'a çekilir.
- **Sürümlü.** `version` ileride eski dosyaların dönüştürülmesini sağlar. Yeni alanların sadece güvenli varsayılan
  değerleri olmalı; `JsonUtility` eksik anahtarları alanın başlangıç değerinde bırakır.

Testler: `JsonFileSaveServiceTests` (test başına geçici klasörde gerçek dosyalar) ve
`ProgressServiceTests` (bellek içi sahte `ISaveService`, diske erişim yok).

### Oturum (`Core/Session`)

Oyunun bir turu: 3 dakikalık hayatta kalma sayacı, kill sayısı ve sonuç.

| Tip | Görev |
|------|------|
| `GameState` | `Idle` (zorluk seçimi), `Playing`, `Won`, `Lost`. |
| `GameSession` | Sayacı ve kill sayısını tutar, kazanma/kaybetmeye karar verir, `Started` ve `Ended` event'lerini yayar. |
| `RunResult` | `Ended` ile gönderilir: sonuç, kill sayısı, hayatta kalınan süre. Sonuç ekranı kullanır. |

Durum akışı:

```
Idle --Start(180)--> Playing --sayaç 180 sn'ye ulaşır--> Won
                        |
                        +--NotifyPlayerDied()----------> Lost
Won / Lost --Start(180)--> Playing   (tekrar oyna, bütün değerler sıfırlanır)
Won / Lost --ReturnToIdle()--> Idle  (zorluk seçimine dönüş)
```

Davranış ayrıntıları:
- **Zaman dışarıdan gelir.** `Tick(deltaTime)` her frame Unity katmanı tarafından çağrılır. Oturum
  `Time.deltaTime`'ı kendisi okumaz; bu sayede bir test 3 dakikalık turu mikrosaniyeler içinde simüle edebilir.
- **Tam bir kez biter.** `Won` ya da `Lost` sonrasında gelen tick, kill ve ölümler yok sayılır. Süre bittikten
  sonra isabet eden bir mermi kill sayılmaz; oyuncu kazandıktan sonra ölemez.
- **`Progress` (0..1)** diğer sistemlere turun ne kadar ilerlediğini söyler. Spawn bunu zorluğu artırmak için kullanır.
- **Kill'leri kaydetmek oturumun işi değildir.** Composition root `Ended` event'ini `ProgressService.AddKills`'e
  bağlar; oturum kayıt mantığından bağımsız kalır.

Testler: `GameSessionTests` (durum geçişleri, sayaç, kill'ler, tekrar oynamada sıfırlama, simüle edilmiş 60 FPS tur).

### Zorluk (`Core/Difficulty`)

Aynı sahnede üç zorluk seviyesi; sadece düşman sayısı ve spawn sıklığı değişir.

| Tip | Görev |
|------|------|
| `DifficultyConfig` | Ayar değerlerini ve artış hesabını içeren saf serileştirilebilir sınıf. |
| `DifficultySettings` | Bir `DifficultyConfig` ve görünen adı sarmalayan ScriptableObject asset. Mantık içermez. |

Her değer tur boyunca `GameSession.Progress`'e göre başlangıç değerinden bitiş değerine doğrusal olarak artar:

| Asset (`Assets/Data/Difficulty`) | Spawn aralığı (sn) | Dalga boyutu | En fazla canlı |
|------|------|------|------|
| `Difficulty_Easy` | 3.0 -> 1.5 | 2 -> 6 | 40 |
| `Difficulty_Normal` | 2.5 -> 1.0 | 3 -> 10 | 80 |
| `Difficulty_Hard` | 2.0 -> 0.6 | 4 -> 16 | 150 |
| `Difficulty_Benchmark` | 0.5 | 10 | 150 |

`En fazla canlı` kesin bir sınırdır: dalgalar, düşman sayısı bu değeri hiç geçmeyecek şekilde kırpılır. En kötü
durumdaki CPU/GPU yükünü sınırlar; bu yüzden performans testlerinin ana ayar düğmesidir. Benchmark asset'i menüde
görünmez, sadece benchmark modu kullanır.

Neden tek bir ScriptableObject yerine iki tip: ScriptableObject yalnızca Unity üzerinden (`CreateInstance`)
oluşturulabilir ve alanları Inspector'dan ayarlanır. Mantığı saf bir sınıfta tutmak testlerin `new` ile config
oluşturup hesabı doğrudan kontrol etmesini sağlar.

Testler: `DifficultyConfigTests` (başlangıç/bitiş/ara değerler, sınırlama, doğrulama).

### Dövüş: Can (`Core/Combat`)

`Health` oyuncunun ve her düşmanın can puanlarını tutar.
- `TakeDamage` sıfır/negatif miktarları yok sayar ve 0'da durur.
- `Damaged(amount)` her isabette tetiklenir (hasar geri bildirimi için). `Died` **tam bir kez** tetiklenir; ölü bir hedef yeni hasar almaz.
- `Reset()` yeni bir can için doldurur (pool'dan tekrar kullanılan düşman, tekrar oynayan oyuncu).
- `IsInvulnerable` açıkken hasar yok sayılır (benchmark modu kullanır).

Testler: `HealthTests`.

### Düşmanlar (`Core/Enemies`)

Bütün düşmanların doğması, kovalaması, saldırması ve ölmesi.

| Tip | Görev |
|------|------|
| `EnemyConfig` | Özellikler: can, hız, temas hasarı, saldırı aralığı, saldırı menzili. |
| `EnemyDefinition` | `EnemyConfig` için ScriptableObject sarmalayıcı (`Assets/Data/Enemies/Enemy_Zombie`). |
| `Enemy` | Tek bir düşmanın durumu: konum, yön, can, "saldırı menzilinde" bayrağı. Sadece veri, davranış yok. |
| `EnemySystem` | Bütün düşmanların sahibi. Pool'dan spawn eder, hepsini tek döngüde günceller, hasar uygular, despawn eder. |
| `WaveSpawner` | Zorluğa ve tur ilerlemesine göre bir dalganın ne zaman ve nerede çıkacağına karar verir. |

**Tek sistem, tek döngü.** Her düşman için bir MonoBehaviour yoktur. `EnemySystem.Tick` tek bir listeyi dolaşır ve
her düşman için: oyuncuya döner, `hız * deltaTime` kadar ilerler ama saldırı menzilinin sınırında durur, menzildeyse
ve cooldown dolduysa saldırır. 150 düşman için bu, 150 `Update()` yerine frame başına tek bir metot çağrısıdır.

**Unity tarafıyla event'ler üzerinden konuşur.** Sistem GameObject'leri bilmez:
- `Spawned(enemy)`: view katmanı kendi pool'undan bir düşman modeli alır ve bu düşmanı takip eder.
- `Died(enemy)`: oyuncu tarafından öldürüldü. Composition root bunu `GameSession.RegisterKill`'e iletir.
- `Despawned(enemy)`: düşman sahneden çıktı (öldü ya da temizlendi). View katmanı modeli pool'a geri koyar.

**Pooling.** `Enemy` nesneleri `UnityEngine.Pool.ObjectPool<T>`'den gelir (Unity'nin içinde, paket gerekmez).
Ölen düşman pool'a döner ve bir sonraki spawn'da sıfırlanır; ön ısıtmadan (`Prewarm`) sonra oyun sırasında
bellek ayırma olmaz. Aktif listeden çıkarma O(1)'dir: son düşman boşalan yere taşınır (`ActiveIndex` her
düşmanın yerini hatırlar).

**Oyuncuya verilen hasar tick başına bir kez, döngüden sonra uygulanır.** Saldıran bütün düşmanların hasarı
toplanıp sonda uygulanır. Oyuncunun ölmesi bütün düşmanları temizleyen dinleyicileri tetikleyebilir; bu döngünün
ortasında olsaydı liste dolaşımı bozulurdu. Bu durum bir testle korunuyor.

**Dalga spawn'ı.**
- İlk dalga turun ilk tick'inde çıkar, sonra her `GetSpawnInterval(progress)` saniyede bir dalga gelir.
- Dalga boyutu `GetWaveSize(progress)`'tir; `AliveCount` hiçbir zaman `MaxAliveEnemies`'i geçmeyecek şekilde kırpılır.
- Düşmanlar oyuncunun etrafında `spawnRadius` yarıçaplı bir halkada (kamera görüşünün dışında), rastgele bir açıda
  çıkar ve arenanın içinde tutulur. Arena kenarında bu sınırlama spawn'ı halkadan daha yakına getirebilir; basitlik
  için kabul edilmiş küçük bir ödünleşim.
- Rastgelelik dışarıdan verilen bir `System.Random`'dan gelir; testler sabit seed kullanır ve tekrarlanabilirdir.

**Ayrışma (separation).** Düşmanlar birbirinin içine girmez; oyuncunun etrafında tek bir yığın yerine tek tek
seçilebilen bir sürü oluştururlar. `EnemyConfig`'te `separationRadius` (1,2 m) ve `separationStiffness` (1) ile
ayarlanır; ikisinden biri 0 ise kapalıdır.

- **Komşu arama: `SpatialGrid` (`Core/Spatial`).** Arena, kenarı ayrışma yarıçapı kadar olan hücrelere bölünür. Her
  hücre, iki int dizisinde tutulan bir bağlı listedir (hücre başına `heads`, eleman başına `next`); her frame O(n)
  yeniden kurulur ve bellek ayırmaz. Her düşman sadece etrafındaki 3 x 3 hücredekilerle karşılaştırılır. 150 düşman
  için kaba kuvvet her çifti karşılaştırır (150 x 149 = 22.350 mesafe kontrolü); benchmark kalabalığında grid yaklaşık
  1.600 kontrol yapıyor (MCP ile Play modunda ölçüldü, `EnemySystem.LastSeparationChecks`).
- **Kuvvet değil, konum düzeltmesi.** İlk deneme, iç içe geçen düşmanları bir kuvvetle itiyordu. Oyuncuya doğru yürüyen
  arka sıralar ön sıraları sıkıştırdı ve kalabalık yaklaşık 4 m'lik sıkışık bir diske döndü (en yakın çift 0,32 m,
  oyuncunun 1,5 m yakınında 36 düşman). Şimdiki çözüm, iç içe geçen her çifti örtüşmenin yarısı kadar (sertlikle
  çarpılarak) birbirinden uzaklaştırıyor; örtüşme yürüme hızı ne olursa olsun birkaç frame'de çözülüyor. Bütün
  düzeltmeler önce hesaplanıp sonra uygulanıyor, sonuç liste sırasına bağlı değil.
- **Ayar seçimi** (150 düşman, 15 sn simülasyon, MCP ile ölçüldü):

  | Yarıçap / sertlik | Ortalama en yakın komşu | Kalabalık yarıçapı | Aynı anda saldıran | Tick |
  |------|------|------|------|------|
  | Kuvvet tabanlı itme (ilk deneme) | - | ~4 m | 36 | - |
  | 1,0 m / 0,5 | 0,66 m | 4,8 m | - | 0,13 ms |
  | 1,0 m / 1,0 | 0,73 m | 5,9 m | - | 0,10 ms |
  | **1,2 m / 1,0 (seçilen)** | **0,90 m** | **7,1 m** | **6** | **0,11 ms** |
  | 1,4 m / 1,0 | 1,10 m | 8,5 m | 4 | 0,09 ms |

- **Oynanışa etkisi.** Aynı anda oyuncuya ulaşan düşman sayısı sınırlanıyor (6 civarı); kalabalık oyuncuyu bir anda
  ezemiyor, sırayla saldırıyor. Hard zorluğu bu yüzden eskisinden daha kolay olabilir; değerler oynanarak ayarlanabilir.

Testler: `EnemySystemTests` (hareket, menzil, saldırı cooldown'u, hasarların toplanması, ölüm event'leri, pool'dan
tekrar kullanım, oyuncunun ölümü sırasında temizleme, ayrışma: simetrik itme, yarıçap dışını yok sayma, üst üste
doğan düşmanların dağılması, tam sertlikte tek frame'de çözülme, 150 düşmanlık kalabalığın aralığını koruması, sadece
komşuların kontrol edilmesi), `SpatialGridTests` (grid'in kaba kuvvet aramasıyla birebir aynı komşuları bulması, arena
dışının kenar hücrelere sıkıştırılması, kapasitenin büyümesi) ve `WaveSpawnerTests` (zamanlama, en fazla canlı sınırı,
ilerlemeye göre artış, spawn halkası, arena sınırı, yeniden başlatma).

Testlerin yakaladığı bir zayıflık: ilk "sadece komşular kontrol ediliyor" testi düşmanları tam 2 m arayla diziyordu;
hiçbiri komşu hücreye düşmediği için kontrol sayısı 0 çıktı ve test hiçbir şey ölçmeden geçti. Test, düşmanlar rastgele
bir kalabalık gibi dağıtılıp "en az bir kontrol yapıldı" şartı eklenerek düzeltildi.

### Oyuncu (`Core/Player`)

| Tip | Görev |
|------|------|
| `PlayerConfig` | Özellikler: en fazla can, hareket hızı. |
| `PlayerDefinition` | ScriptableObject sarmalayıcı (`Assets/Data/Player/Player_Default`). |
| `PlayerCharacter` | Oyuncunun konumu, yönü, hızı ve `Health`'i. |

(Sınıfın adı `Player` değil `PlayerCharacter`: namespace'iyle aynı adı taşıyan bir tip başka her yerde uzun,
tam nitelikli isimler yazmayı gerektirir.)

- **Girdi.** `Move(deltaTime, Vector2 input)` joystick vektörünü alır. `x` dünyada X'e, `y` dünyada Z'ye karşılık gelir.
  Kamera dikey eksen etrafında dönmediği için "joystick yukarı" her zaman "ekranda yukarı" demektir.
- **Analog hız.** Joystick yarıya itilince yarım hızda gidilir. 1'den uzun girdi sınırlanır; çapraz hareket daha hızlı olmaz.
- **Arena sınırı.** Konum arenanın içinde tutulur.
- **Yön.** Hareket, oyuncuyu hareket yönüne çevirir. Bir hedef varsa ardından `AimAt(point)` çağrılır; böylece
  oyuncu ters yöne koşsa bile rifle vurulan düşmanı gösterir.
- **Ölü oyuncu hareket etmez.** `Reset(position)` yeni tur için konumu ve canı sıfırlar.
- `SpeedFraction` (0..1) koşma/durma animasyon karışımını sürer.

Testler: `PlayerCharacterTests`.

### Silahlar (`Core/Weapons`)

| Tip | Görev |
|------|------|
| `WeaponConfig` | Rifle özellikleri: hasar, atış aralığı, menzil, mermi hızı. |
| `WeaponDefinition` | ScriptableObject sarmalayıcı (`Assets/Data/Weapons/Weapon_Rifle`). |
| `Targeting` | `FindNearest(enemies, origin, range)`: menzildeki en yakın düşman ya da null. |
| `Weapon` | Otomatik ateş: her tick hedef seçer, cooldown dolunca ateş eder. |
| `Projectile` | Tek merminin durumu: konum, yön, kalan mesafe, hasar. |
| `ProjectileSystem` | Bütün mermilerin sahibi: pool, tek güncelleme döngüsü, isabet tespiti, view için event'ler. |

Frame akışı:

```
Weapon.Tick      -> Targeting.FindNearest -> CurrentTarget (oyuncu ona döner)
                 -> cooldown doldu mu?    -> ProjectileSystem.Fire(...) + Fired event'i
ProjectileSystem.Tick -> her mermiyi ilerlet -> düşmana çarptı mı? -> EnemySystem.ApplyDamage + Hit event'i, despawn
                                             -> çok uzağa gitti mi? -> despawn
```

Davranış ayrıntıları:
- **Otomatik saldırı.** Silah her zaman menzildeki (varsayılan 8 birim) en yakın düşmana ateş eder. Hedef
  belirdiğinde ilk atış hemen gider, sonra her `fireInterval`'da bir atış.
- **Mermiler anında vurmaz, simüle edilir.** `projectileSpeed` ile düz uçarlar ve kenara çekilen bir düşmanı
  ıskalayabilirler. Kaybolmadan önce `menzil x 1.5` kadar uçarlar; böylece biraz menzil dışına çıkmış hedefe de ulaşırlar.
- **İçinden geçme yok.** İsabet sadece bitiş noktasına değil, merminin o frame'de kat ettiği doğru parçasına göre
  test edilir. Yavaş bir frame'de mermi bir düşmanın genişliğinden fazla yol alabilir; nokta kontrolü onun üstünden
  atlardı. Bir test tek tick'te 100 birim giden bir mermiyle bunu koruyor.
- **Mermi başına bir isabet.** Mermi yolundaki ilk düşmana hasar verip kaybolur (delip geçme yok).
- **Unity fiziği yok.** Collider ya da rigidbody yok: mermi-düşman çifti başına birkaç çarpma işlemi; testlerde
  sonuçlar deterministik ve Unity'nin fizik motoru hiç çalışmıyor. Maliyet frame başına O(mermi x düşman);
  mermi sayısı az (0.35 sn'de bir, kısa ömürlü) olduğu için küçük kalıyor.
- **Pooling.** Düşmanlarla aynı desen: `ObjectPool<Projectile>`, O(1) swap-remove, `Prewarm`, `Clear`. Güncelleme
  döngüsü sondan başa çalışır; bir çıkarma sadece zaten güncellenmiş bir mermiyi boşalan yere taşır.

Testler: `TargetingTests`, `WeaponTests`, `ProjectileSystemTests` (hareket, süre dolması, isabetler, içinden geçme,
ıskalamalar, kill'ler, birden fazla mermi, pool'dan tekrar kullanım).

### Dünya (`Core/World`)

`GameWorld` tek bir nesne içinde simülasyonun tamamıdır. `WorldConfig` arena geneli ayarları tutar (tur süresi,
arena boyutu, spawn yarıçapı, mermi isabet yarıçapı, pool ön ısıtma sayıları).

**Sahip oldukları.** `GameSession`, `PlayerCharacter`, `EnemySystem`, `ProjectileSystem`, `Weapon` ve
`WaveSpawner`'ı oluşturur ve (spawner hariç) dışarıya açar; Unity tarafı onları çizer ve event'lerine abone olur.

**Event bağlantıları** (sistemlerin birbirine bağlandığı tek yer):

| Event | Dinleyici | Etki |
|------|------|------|
| `Enemies.Died` | `Session.RegisterKill` | Kill sayacı |
| `Player.Health.Died` | `Session.NotifyPlayerDied` | Tur kaybedilir |
| `Session.Ended` | `Progress.AddKills` + `LastResult`'ı sakla | Toplam kill kaydedilir, sonuç ekranı verisi |

`Tick(deltaTime, joystickInput)` içinde **frame sırası**:

```
1. Player.Move            önce oyuncu hareket eder; geri kalan her şey yeni konuma göre tepki verir
2. WaveSpawner.Tick       oyuncunun etrafında yeni düşmanlar
3. Enemies.Tick           kovalama ve saldırı; oyuncuyu öldürebilir -> tur biter, burada durulur
4. Weapon.Tick + AimAt    hedef seç, ateş et, hedefe dön
5. Projectiles.Tick       mermiler uçar ve vurur; kill'ler Enemies.Died üzerinden sayılır
6. Session.Tick           sayaç en sonda; son frame'deki kill de kazanmadan önce sayılır
```

**Tur akışı.**
- `StartRun(difficulty, options)`: düşmanları ve mermileri temizler, oyuncuyu ve silahı sıfırlar, spawner'ı ve sayacı başlatır.
- `RunOptions` (varsayılan: normal oyun): sabit `Seed`, `Invulnerable` oyuncu, `Duration` ve `SkipProgress`
  (toplam kill'e eklenmez). Benchmark modu kullanır.
- `Replay()`: aynı zorluk ve seçeneklerle `StartRun`. Toplam kill korunur.
- `ReturnToMenu()`: arenayı temizler ve zorluk seçimi için `Idle`'a döner.
- Kazanma ya da kaybetmeden sonra `Tick` hiçbir şey yapmaz; arena sonuç ekranının arkasında donmuş kalır.

Testler: `GameWorldTests` bütün Core sistemlerinin birlikte çalıştığı entegrasyon testleridir: kill'ler sayılıyor,
kazanma da kaybetme de kill'leri kaydediyor, tur sonrası arena donuyor, tekrar oynama toplam kill hariç her şeyi
sıfırlıyor, iki turun kill'leri toplanıyor, her `Spawned` event'inin bir `Despawned`'i var (yoksa Unity tarafında
modeller sızardı), aynı seed aynı spawn'ları üretiyor. Bir smoke testi varsayılan ayarlarla 60 FPS'te tam 3 dakikalık
bir turu oynatıyor.

Paylaşılan test yardımcıları `Tests/EditMode/TestDoubles` altında (`InMemorySaveService`).

### Girdi matematiği (`Core/Input`)

`JoystickModel` ekranda beliren (floating) joystick'in matematiğidir; test edilebilsin diye Core'dadır:
- `Press(point)` joystick tabanını parmağın değdiği yere koyar.
- `Drag(point)` tutamacı `radius` ile sınırlı olarak hareket ettirir. `Value`, 0..1 şiddetli yöndür.
- **Ölü bölge** (varsayılan olarak yarıçapın %10'u) dinlenen başparmağı yok sayar. Dışında şiddet 0'dan başlayacak
  şekilde yeniden ölçeklenir; ölü bölgeden çıkarken sıçrama olmaz.
- `Release()` her şeyi sıfırlar.

Testler: `JoystickModelTests`.

### Sunum yardımcıları (`Core/Presentation`)

`TimeFormat` saniyeleri HUD metnine çevirir; yuvarlama kuralları test edilsin diye Core'dadır:
- `CountdownSeconds` **yukarı** yuvarlar (0.2 sn kala hâlâ `0:01` gösterir; `0:00` sadece süre gerçekten bitince).
- `ElapsedSeconds` **aşağı** yuvarlar (59.9 sn hayatta kalınmışsa `0:59`).
- `MinutesSeconds(125)` sonucu `2:05`.

Testler: `TimeFormatTests`.

## Unity katmanı (`Assets/Scripts/Unity`)

Sadece `GameBootstrap`'in `Update()`'i var. Diğer her bileşen ya saf C# ya da bootstrap tarafından sürülen bir
MonoBehaviour; bir frame'de olan her şeyin sırası tek bir metotta görünür.

| Tip | Tür | Görev |
|------|------|------|
| `GameBootstrap` | MonoBehaviour | Composition root. Veri asset'lerinden `GameWorld`'ü kurar, view'ları event'lerine bağlar, frame'i çalıştırır. |
| `VirtualJoystick` | MonoBehaviour (uGUI) | Pointer event'lerini `JoystickModel` çağrılarına çevirir, iki joystick görselini hareket ettirir. |
| `MoveInput` | Saf C# | Dokunulurken joystick, aksi hâlde klavye (WASD/oklar) ya da gamepad; editörde test için. |
| `ViewRegistry<TModel, TView>` | Saf C# | Simülasyon nesnelerini pool'lanan GameObject'lere eşler. |
| `PlayerView` | MonoBehaviour | Oyuncunun konumunu kopyalar, yönüne yumuşakça döner, animasyonu sürer. |
| `EnemyView` | MonoBehaviour | Bir düşmanın konumunu, yönünü ve animasyonunu sürer. |
| `EnemyDeathViews` | Saf C# | Ölen düşmanın modelini ölüm animasyonu boyunca sahnede tutar. |
| `FollowCamera` | Saf C# | Sabit ofsetli ve hafif yumuşatmalı eğik takip kamerası. |

**Frame** (`GameBootstrap.Update`):

```
input  = MoveInput.Read()                 (benchmark sırasında sıfır)
world.Tick(deltaTime, input)              simülasyon (bkz. GameWorld)
playerView.Sync                           oyuncuyu çiz
enemyViews.Sync / projectileViews.Sync    her düşmanı ve mermiyi çiz
enemyDeaths.Tick                          ceset süreleri
camera.Follow                             kamera en sonda; oyuncunun son konumunu görür
flow.Tick                                 HUD, hasar flaşı, benchmark kaydı
```

**View pooling.** `ViewRegistry` bir sistemin event'lerine bağlıdır: `Spawned -> Show` pool'dan bir GameObject alır,
aktif eder ve hangi modeli çizdiğini hatırlar; `Despawned -> Hide` onu kapatıp geri koyar. Düşman ve mermi
GameObject'lerinin hepsi açılışta oluşturulur (ön ısıtma); oyun sırasında `Instantiate`/`Destroy` olmaz. Yeni
gösterilen bir view hemen yerleştirilir; eski konumunda bir an görünmez.

**Frame başına çöp yok.** Sync callback'leri önbelleğe alınmış delegate'lerdir. Metot adını doğrudan vermek
(`Sync(SyncProjectile)`) her frame yeni bir delegate nesnesi oluştururdu.

### Sahne (`Assets/Scenes/Arena.unity`)

Unity MCP ile kuruldu.

| Obje | İçerik |
|------|------|
| `Arena` | 40 x 40 zemin ve dört alçak duvar (placeholder, statik batch'li, collider yok). |
| `Player` | `Assets/Prefabs/Player_Optimized.prefab`: `PlayerView`, optimize oyuncu modeli, `mixamorig:RightHand` altında rifle. |
| `GameBootstrap` | Veri asset'lerine, prefab'lara, kameraya, joystick'e ve ekranlara referanslar. |
| `UI` | Ekran uzayı canvas (referans 1920 x 1080, yatay), tam ekran `JoystickArea`. |
| `EventSystem` | `InputSystemUIInputModule` kullanır (proje sadece yeni Input System'i kullanıyor). |

Prefab'lar: `Enemy_Optimized.prefab` (oyunda kullanılan), `Enemy.prefab` ve `Player.prefab` (orijinal modellerle,
karşılaştırma için), `Bullet.prefab` (unlit materyalli küçük uzatılmış küre, collider ve gölge yok). Modeller orijinal ya
da optimize FBX dosyalarının bağlı prefab instance'larıdır.

Oyun yatay (landscape) çalışır ve Player Settings'te `LandscapeLeft`'e kilitlidir (telefonun üstü solda).

**Kamera kadrajı.** Kamera ayarları, kamera MCP üzerinden 16:9 render edilerek, silah menziline ve spawn yarıçapına
işaret düşmanlar konarak seçildi:
- Ofset `(0, 20, -11.5)` (yaklaşık 60 derece aşağı) ve 40 derecelik görüş açısı. Uzaktan dar açı perspektifi
  düzleştirir; ekranın üst tarafı geniş bir lensin göstereceğinden daha az zemin gösterir.
- Oyuncunun etrafında görünen zemin: yaklaşık 15 birim sağa/sola, 8 geriye, 12 ileriye.
- Silah menzili (8) ekrana sığar; oyuncu sadece kullanıcının gördüğü düşmanlara ateş eder.
- Spawn yarıçapı (18) görüşün dışındadır; düşmanlar birden belirmek yerine ekran dışından yürüyerek gelir.
  Ekranın uzak köşelerinde görüş 18'den geniş olduğu için orada bir spawn nadiren görünebilir.

## UI (`Assets/Scripts/Unity/UI`)

uGUI ve TextMeshPro (TMP Essential Resources `Assets/TextMesh Pro` altına import edildi).

| Tip | Tür | Görev |
|------|------|------|
| `GameFlow` | Saf C# | Hangi ekranın görüneceğine ve butonların ne yapacağına karar verir. |
| `MenuScreen` | MonoBehaviour | Zorluk başına bir buton (etiketler `DifficultySettings.DisplayName`'den), toplam kill, BENCHMARK butonu. |
| `HudScreen` | MonoBehaviour | Kalan süre, kill sayısı, can barı. Joystick'i barındırır. |
| `ResultScreen` | MonoBehaviour | "YOU SURVIVED" / "YOU DIED", kill'ler, hayatta kalınan süre, toplam kill, Play Again ve Menu. |
| `BenchmarkScreen` | MonoBehaviour | Benchmark sonucu ve Menu butonu. |
| `DamageFlash` | MonoBehaviour | Oyuncu vurulunca tam ekran kırmızı ton, 0.35 sn'de söner. |

Ekran akışı:

```
Menu --zorluk butonu--> HUD (oyun) --tur biter--> Result --Play Again--> HUD
                                                         --Menu--------> Menu
Menu --BENCHMARK------> HUD (girdi yok) --biter--> Benchmark sonucu --Menu--> Menu
```

Davranış ayrıntıları:
- **Joystick HUD'un içinde.** Tur sonunda HUD'u gizlemek joystick'i kapatır ve `OnDisable` onu bırakır; ekranda
  kalan bir parmak bir sonraki turda yönlendirmeye devam etmez.
- **Sonuç ekranı okuduğunda toplam kill zaten kaydedilmiştir.** `GameWorld` `Session.Ended`'e constructor'ında,
  `GameFlow`'dan önce abone olur; C# event'leri dinleyicileri abonelik sırasıyla çağırır.
- **HUD çöp üretmez.** Süre ve kill metinleri her frame değil, gösterilen sayı değişince (süre için saniyede bir)
  yeniden oluşturulur.
- **Hasar geri bildirimi.** `Player.Health.Damaged` `DamageFlash`'i tetikler. Flaş görseli tamamen şeffafken kapatılır;
  mobilde tam ekran şeffaf bir görsel bile GPU doldurma maliyeti taşır.
- Oyun sırasında FPS 60'ta kilitlidir (Android aksi hâlde 30'a kilitler); benchmark sırasında 120.

Canvas hiyerarşisi (`UI`, kardeş sırası = çizim sırası):

```
UI
|- HudScreen        JoystickArea (dokunma alanı + Background/Handle), HealthBar/Fill, Timer, Kills
|- DamageFlash
|- ResultScreen     Title, Kills, Survived, TotalKills, ReplayButton, MenuButton
|- BenchmarkScreen  Title, Result, MenuButton
|- MenuScreen       Title, Subtitle, Easy/Normal/HardButton, TotalKills, BenchmarkButton
```

**MCP ile Play modunda doğrulandı:** menü kaydedilmiş toplam kill'i gösteriyor; Hard 150 düşman sınırıyla tur
başlatıyor; oyuncunun ölümü kill, süre ve güncellenmiş toplamla "YOU DIED" gösteriyor; sonuç ekranında joystick
kapalı; Play Again canı dolduruyor; Menu arenayı temizliyor; Easy 40 düşman sınırıyla tur başlatıyor; hasar kırmızı
flaşı gösteriyor.

## Animasyon (`Assets/Animations`)

Mixamo klipleri (FBX for Unity, skinsiz, 30 FPS, in place). Mixamo'nun Y Bot karakteri üzerinde indirildi ve
Unity'nin **Humanoid** sistemiyle bizim karakterlere aktarıldı (retargeting).

| Klip | Kullanan | Döngü |
|------|------|------|
| Rifle Aiming Idle, Rifle Run | Oyuncu (`AC_Player`) | evet |
| Zombie Walk, Zombie Attack | Düşman (`AC_Enemy`) | evet |
| Zombie Death | Düşman ve oyuncu ölümü | hayır |

Import ayarları: Humanoid rig (her dosyadan avatar), kök dönüşü / yüksekliği / konumu poza gömülü, çünkü karakterleri
kod hareket ettiriyor. `player.fbx` ve `enemy.fbx` klipler aktarılabilsin diye Generic'ten Humanoid'e alındı; sadece
import ayarları (`.meta`) değişti, FBX dosyalarına dokunulmadı.

**Controller'lar** (`Assets/Animations/Controllers`, MCP ile oluşturuldu):
- `AC_Player`: `Locomotion` 1D blend tree (`Speed` 0'da Rifle Aiming Idle, 1'de Rifle Run) ve `Dead` tetikleyicisiyle Any State'ten `Death`.
- `AC_Enemy`: `InRange` bool'u ile `Walk` <-> `Attack`, `Dead` ile Any State'ten `Death`. Attack durumunun hızı
  `AttackSpeed` parametresinden gelir.

**Animator'ları sürmek** (Core'da Animator mantığı yok):
- `PlayerView` `Speed`'i `PlayerCharacter.SpeedFraction`'dan (0..1, joystick eğimi) kısa bir yumuşatmayla ayarlar.
- `EnemyView.Begin` (pool'dan alındığında) Animator'ı sıfırlar, `Walk`'u döngünün rastgele bir noktasından başlatır
  (dalga adım adım aynı yürümesin) ve `AttackSpeed = attackClipLength / attackInterval` yapar; böylece bir vuruş
  simülasyonun tam bir saldırı aralığı sürer.
- `EnemyView.Sync` `InRange`'i sadece `Enemy.IsInAttackRange` değişince ayarlar.
- Parametreler string ile değil, hash'lenmiş id'lerle (`AnimatorIds`) ayarlanır.

**Ölüm animasyonları.** Simülasyon ölen düşmanı hemen kaldırır. `EnemyDeathViews` `EnemySystem.Died`'ı dinler
(`Despawned`'dan önce yayılır), modeli registry'den **ayırır**, `Death`'i oynatır ve `corpseSeconds` (2.2 sn) sonra
modeli pool'a döndürür. Ardından gelen `Despawned -> Hide` gizleyecek bir şey bulmaz. Yeni tur başlarken cesetler
temizlenir. Oyuncunun `Health.Died`'ı oyuncunun ölüm animasyonunu tetikler.

**Tur sonrası donmuş arena.** `Session.Ended` tetiklenince bootstrap görünen her düşman ve cesette
`animator.speed = 0` yapar; sonuç ekranının arkasındaki arena durağan bir karedir. Oyuncu animasyonuna devam eder
(kayıpta ölüm animasyonu). Cesetler donmuşken kaybolmaz; Play Again ya da Menu'de temizlenir. `EnemyView.Begin`
pool'dan tekrar kullanılan view'da hızı 1'e geri çeker.

MCP ile Play modunda `EditorApplication.Step()` ve sabit `Time.captureDeltaTime` kullanılarak doğrulandı (editör
arka plandayken Play modunu ilerletmiyor): oyuncunun ölümünden sonra görünen 16 düşman Animator'ının hepsinin hızı
0'dı ve bir saniye sonra pozları aynıydı, oyuncu `Death`'teydi; Play Again sonrası yeni düşmanlar normal
animasyonluydu; Menu sonrası hiç düşman view'ı kalmadı.

**Rifle.** `rifle.fbx`'in gömülü materyalinde doku yok. Oyuncu prefab'ında verilen albedo, metallic/smoothness ve
normal dokularıyla bir URP Lit materyal kullanılıyor. Rifle'ın `mixamorig:RightHand` altındaki ofseti MCP ile
örneklenen Rifle Aiming Idle pozundan hesaplandı: namlu sağ elden (kabza) sol ele (namlu altı) doğru bakıyor;
idle ve koşu pozlarının yakın plan render'larıyla kontrol edildi.

**Bilinen küçük eksik:** Rifle Run'da sol el silahtan ayrılıyor; koşarken rifle yukarı doğru eğiliyor (klibin kendi
sınırlaması). İlk idle klibi (Rifle Idle, rifle vücudun önünde çapraz) Rifle Aiming Idle ile değiştirildi; oyuncu
dururken ve ateş ederken rifle hedefe doğrultuluyor.

## Benchmark modu (`Core/Benchmark`, `Unity/Benchmark`)

Menüdeki **BENCHMARK** butonuyla başlayan sabit, tekrarlanabilir bir performans testi. Referans ve optimize build'in
cihaz üzerinde, Profiler bağlantısı gerekmeden birebir aynı koşullarda ölçülebilmesi için var.

**Senaryo** (bootstrap'teki `BenchmarkSettings`, spawn ayarları `Difficulty_Benchmark`'ta):

| Ayar | Değer | Neden |
|------|------|------|
| Spawn | 0.5 sn'de bir 10 düşman, sınır 150 | En kötü duruma (150 canlı) ısınma süresi içinde ulaşır |
| Seed | 12345 | Her koşuda aynı spawn konumları (`RunOptions.Seed`) |
| Oyuncu | Hasar almaz, girdi yok | Tur her zaman tam sürer, yük aynıdır |
| Isınma | 10 sn, ölçülmez | Arena dolar, shader'lar ve pool'lar ısınır |
| Ölçüm | 60 sn | |
| FPS sınırı | 120 (oyun: 60) | 60 sınırı build'ler arasındaki farkı gizlerdi |
| Kayıt dosyası | Dokunulmaz (`RunOptions.SkipProgress`) | Bir test koşusu toplam kill'i değiştirmemeli |

**Ölçülen değerler** (`BenchmarkRecorder`, `BenchmarkResult`):
- `Time.unscaledDeltaTime`'dan frame süresi: ortalama FPS, **1% low FPS** (99. yüzdelik frame süresinden),
  ortalama / p99 / en fazla frame süresi. 1% low, ortalamanın gizlediği takılmaları gösterir.
- `FrameTimingManager`'dan frame başına CPU ana thread ve GPU süresi (Player Settings'te "Frame Timing Stats" açık;
  release build'de de çalışır). Cihaz bildirmiyorsa `n/a` gösterilir.
- Canlı düşman (en fazla, ortalama), kill'ler, ayrılmış bellek ve GC heap boyutu, cihaz, GPU ve grafik API'si.

Kayıt sırasında bellek ayrılmaz: `SampleStats` örnekleri önceden ayrılmış dizilerde tutar ve sonda bir kez sıralar.

**Çıktı:** sonuç ekranı, `persistentDataPath/benchmarks/` altında bir JSON dosyası ve USB üzerinden okunabilen,
`BENCHMARK_RESULT` ile başlayan tek bir log satırı:

```
adb logcat -s Unity | findstr BENCHMARK_RESULT
```

Benchmark için Core'a eklenenler (hepsi testli): `SampleStats` (ortalama, en fazla, en yakın sıra yöntemiyle
yüzdelik), `GameWorld.StartRun` için `RunOptions` (seed, dokunulmazlık, süre, toplam kill'e eklememe),
`Health.IsInvulnerable`. İlk test koşusu yüzdelik hesabında bir float yuvarlama hatası yakaladı (`0.99f * 100`
99'dan biraz büyük, sıra bir kayıyordu); küçük bir epsilon ile düzeltildi.

Editörde MCP ile uçtan uca doğrulandı (frame'ler elle ilerletildi): koşu 150 düşmana ulaştı, 70 sn sürdü, sonuç
ekranını gösterdi, JSON dosyasını ve log satırını yazdı, 60 FPS sınırını geri getirdi.

**Test cihazı:** Xiaomi Redmi Note 14 Pro (4G, model 24116RACCG), MediaTek Helio G100-Ultra (MT6789),
Mali-G57 MC2 GPU, 8 GB RAM, 1080 x 2400, 120 Hz'e kadar, Android 16.

## Optimize asset'ler (`Assets/Optimized`, `Tools/Blender`)

`Assets/Models` altındaki verilen modeller hiç değiştirilmez. Optimize sürümler bunlardan Blender script'leriyle
(Blender 5.2, arayüzsüz) üretilir ve `Assets/Optimized` altında durur. Bir script'i tekrar çalıştırmak aynı sonucu
üretir; her adımın gerekçesi script'in yorumlarında yazılıdır.

```
blender --background --factory-startup --python Tools/Blender/optimize_enemy.py -- Assets/Models/enemy.fbx Assets/Optimized/Enemy
blender --background --factory-startup --python Tools/Blender/optimize_player.py -- Assets/Models/player.fbx Assets/Optimized/Player
```

| Script | Amaç |
|------|------|
| `inspect_model.py` | Bir FBX'in üçgen, vertex, kemik, kemik etkisi, materyal ve doku istatistikleri |
| `inspect_uv.py` | Materyal başına UV aralığı ve yüz sayısı, kemik etkisi dağılımı |
| `optimize_enemy.py` | Optimize düşmanı üretir (aşağıda) |
| `optimize_player.py` | Optimize oyuncuyu üretir (aşağıda) |

### Düşman

| | Orijinal `enemy.fbx` | `Enemy_Optimized.fbx` LOD0 | LOD1 |
|------|------|------|------|
| Üçgen | 36.902 | 4.500 | 1.500 |
| Vertex (Unity, UV/normal ayrımlarından sonra) | 18.453 | 2.777 | 1.087 |
| Kemik | 65 | 22 | 22 |
| Vertex başına en fazla kemik etkisi | 6 | 4 | 4 |
| Materyal / draw call | 2 | 1 | 1 |
| Doku | 8 x 4096 x 4096 PNG | 2 x 1024 x 512, Android'de ASTC 6x6 | (ortak) |
| Dosya boyutu | 100 MB FBX (dokular gömülü) | 0,35 MB FBX + 1,8 MB PNG | |

Script ne yapıyor ve neden:
1. **Parmak kemikleri kaldırıldı.** 40 parmak kemiği ve ağırlığı olmayan 3 uç kemik gidiyor (65 -> 22). Ağırlıkları
   önce el kemiğine ekleniyor; eller şeklini koruyor ve bileği takip ediyor. Parmaklar ekranda birkaç piksel;
   onları canlandırmak saf CPU maliyeti. Humanoid avatarlar parmak kemiği gerektirmediği için Mixamo klipleri
   aktarılmaya devam ediyor.
2. **Vertex başına en fazla 4 kemik ağırlığı**, yeniden normalize edilerek. 18.453 vertex'in sadece 668'inde 4'ten fazlası vardı.
3. **İki yerine tek materyal.** İki orijinal materyal de 0..1 UV karesinin tamamını ayrı doku setleriyle kullanıyordu.
   UV'leri bir atlasın sol ve sağ yarısına sıkıştırıldı; her düşman tek draw call.
4. **Decimation ile LOD'lar** (Blender'ın Decimate modifier'ı, collapse). `_LOD0`/`_LOD1` ile biten isimler Unity'nin
   import'ta LOD Group oluşturmasını sağlıyor. Skin ağırlıkları decimation tarafından enterpole edildiği için LOD'lar
   rig'li kalıyor.
5. **Doku atlası.** İki materyalin diffuse ve normal dokuları 4096'dan 512'ye ölçeklenip yan yana konuyor (1024 x 512).
   Bir düşman 1080p ekranda yaklaşık 100 x 150 piksel kaplıyor; 4096 dokular görülebilenin çok ötesindeydi.
   Specular dokular düz (58 KB'lık bir 4096 PNG), gloss dokularıyla birlikte atıldı; materyal sabit 0,25 smoothness kullanıyor.

**Kalite karşılaştırması** (MCP ile render, aynı Zombie Walk pozu): yakından optimize LOD0 siluet, kas detayı,
parmaklar ve kafa şeklini koruyor; oyun kamerasının mesafesinden LOD0 ile LOD1 birbirinden ayırt edilemiyor.

Optimize düşman aslında orijinal import'tan **verilen referans görsele daha yakın** görünüyor (`Assets/Models/enemy.jpg`,
mat kırmızı ten): orijinal normal dokular düz renk dokusu olarak import edilmiş; URP normalleri yanlış okuyor ve ten
mavimsi, parlak görünüyor. Optimize normal atlası normal map olarak import ediliyor. Karşılaştırma dürüst kalsın diye
referans build orijinal import'u olduğu gibi tutuyor.

**LOD geçişi.** Bu kamerayla (18-28 m uzaklık, 40 derece görüş açısı) bir düşman ekran yüksekliğinin %9-14'ünü
kaplıyor. %12'nin üstünde (ekranın yakın yarısı) LOD0, altında LOD1 kullanılıyor. Unity tarafı
`Assets/Prefabs/Enemy_Optimized.prefab` (EnemyView + `AC_Enemy`'li Animator, `M_Enemy` URP Lit materyal, blob shadow).

### Oyuncu

| | Orijinal `player.fbx` | `Player_Optimized.fbx` |
|------|------|------|
| Üçgen | 19.450 (kafa 8.256 + gövde 11.194) | 7.999 |
| Mesh / skinned renderer | 2 | 1 |
| Materyal slotu | 3 (aynı dokuyu kullanan iki gövde materyali) | 2 (gövde, kafa) |
| UV seti | 3 (sadece biri kullanılıyor) | 1 |
| Kemik | 69 | 69 |
| Diffuse / normal doku | 1024 / 512 (x2) + specular | 512 / 512 (x2), ASTC 6x6 |

Düşmandan farklı verilen kararlar ve nedenleri:
- **Atlas yok.** Gövde UV'leri 0..1'in dışına taşıyor (u: -0,78 ile 1,32 arası); doku tekrar ederek kullanılıyor.
  Atlas bu tekrarı bozar. Tek bir karakter için bir fazla draw call önemsiz.
- **Parmak kemikleri korundu.** Sadece bir oyuncu var; maliyetleri önemsiz, rifle'ı kavrayan el ise görünüyor.
  Düşmandan 150 kopya olduğu için parmakları atılmıştı.
- **LOD yok.** Oyuncu her zaman ekranın ortasında, aynı mesafede.
- Specular dokular atıldı; materyaller sabit 0,3 smoothness kullanıyor.

MCP ile yapılırken yakalanan iki hata: Blender'da materyal listesini temizlemek (`materials.clear()`) yüzlerin
materyal indekslerini de sıfırlıyor; mesh tek submesh'e düşmüştü (Unity'de submesh sayısı okunarak fark edildi,
indeksler temizlikten sonra geri yazılarak düzeltildi). Ölçeklenmeyen bir doku ise Blender pikselleri tembel yüklediği
için boş kaydediliyordu (pikseller kaydetmeden önce zorla yükletilerek düzeltildi).

Düşmanda olduğu gibi orijinal oyuncunun normal dokuları da renk dokusu olarak import edilmiş; optimize oyuncu verilen
`player.jpg` referansına (açık mavi-turkuaz üniforma, koyu yelek) daha yakın görünüyor. Rifle'ın eldeki konumu yeni
iskelette aynı yöntemle yeniden hesaplandı; namlu yönü orijinalle birebir aynı çıktı.

### Rifle

Mesh (1.988 üçgen) zaten hafif olduğu için değiştirilmedi. Üç doku (albedo, metallic/smoothness, normal)
`Assets/Optimized/Rifle` altına kopyalanıp 2048 yerine 512 ve ASTC 6x6 olarak import ediliyor (`M_Rifle_Optimized`).
Rifle ekranda 30-40 piksel uzunluğunda.

### Render ayarları (sadece Mobile kalite seviyesi)

`Mobile_RPAsset` / `Mobile_Renderer` üzerinde değişti; PC kalite seviyesine dokunulmadı.

| Ayar | Önce | Sonra | Neden |
|------|------|------|------|
| Post-processing (renderer) | Açık (Tonemapping Neutral, Bloom 0.25, Vignette 0.2) | Kapalı | Frame başına birkaç tam ekran geçiş. Bloom'un 1 olan eşiğine bu sahnede neredeyse hiç ulaşılmıyor; görünür bir etkisi olmadan GPU süresi harcıyordu. |
| HDR | Açık | Kapalı | HDR renk tamponu LDR'nin iki katı bant genişliği; post-processing kapalıyken ihtiyaç yok. |
| Gölge mesafesi | 50 m | 35 m | Kamera en fazla yaklaşık 35 m görüyor; ötesindeki gölgeler hiç görünmüyordu. |
| Düşman gölgeleri | Her düşman gölge haritasına ikinci kez çiziliyordu | Kapalı, yerine blob shadow | Aşağıda. |

**Blob shadow.** 150 skinned mesh için gerçek zamanlı gölge, her düşmanı iki kez çizmek (ve skin'lemek) demek.
Her düşmanın artık bir `BlobShadow` alt objesi var: 64 x 64 yumuşak yuvarlak dokulu bir quad
(`Assets/Optimized/Shared`), unlit, alpha blend, GPU instancing açık. Önce sadece uzak LOD'da gölgeyi kapatmak
denendi ve reddedildi: gölgeler ekranın üst yarısında LOD sınırında aniden kayboluyordu, tutarsız görünüyordu.
Oyuncu gerçek gölgesini koruyor.

MCP ile yaparken öğrenilen ders: bir URP materyalini özelliklerini (`_Surface`, blend faktörleri, keyword'ler) atayarak
şeffaf yapmak yetmedi, quad görünmez kaldı. Materyalin URP'nin Inspector'ın da çağırdığı
`BaseShaderGUI.SetupMaterialBlendMode` fonksiyonuyla kurulması gerekti. Sorun, quad önce opak kırmızı bir materyalle
render edilerek (çizildi) sonra gerçek materyalle (çizilmedi) ayrıştırıldı.

### Animasyon optimizasyonu (düşman)

Ölçüm önce yapıldı: editörde benchmark sahnesinde (150 düşman) Animator'ların güncellenmesi, Transform sayısı ve tam
frame süresi MCP ile ölçüldü; her değişiklik aynı ölçümle karşılaştırıldı. Editör rakamları telefonla aynı değil,
sadece hangi değişikliğin işe yaradığını ayırt etmek için kullanıldı; kesin sonuç telefondaki benchmark'tan geldi.

| Değişiklik | Editör ölçümü (150 düşman) |
|------|------|
| Başlangıç (Humanoid, kemikler GameObject) | Animator 2,33 ms, 3.978 Transform, frame 3,76 ms |
| + Optimize Game Objects | Animator 2,33 ms, **459 Transform**, frame 3,62 ms |
| + Generic pişirilmiş klipler | **Animator 1,07 ms (-%54)**, frame 3,28 ms |

- **Generic klipler (`GenericClipBaker`, `Assets/Scripts/Editor`).** Humanoid Animator her frame, her düşman için klibi
  ortak "insan" formatından iskelete çevirir (retargeting). Menüdeki **Tools > Arena Survivor > Bake Enemy Generic
  Clips** bu çeviriyi editörde bir kez yapar: Humanoid klip 30 FPS ile optimize düşmanın kemikleri geri açılmış geçici
  bir kopyasına örneklenir ve her kemiğin yerel konumu ve dönüşü `GameObjectRecorder` ile yeni bir klibe kaydedilir
  (ölçek eğrileri atılır; 22 kemik x 7 eğri = 154 eğri). `AC_Enemy_Generic` aynı durumları bu kliplerle kullanır.
  Pozların Humanoid oynatmayla aynı olduğu iki farklı anda yan yana render alınarak doğrulandı. Oyuncu Humanoid kaldı
  (tek karakter, rifle el kemiğine bağlı).
- **Optimize Game Objects.** Düşman modelinin import ayarı. Kemikler için GameObject/Transform oluşturulmaz; animasyon
  doğrudan skinning matrislerine yazılır. Düşmana hiçbir şey bağlı olmadığı için güvenle açılabildi. Bir yan etkisi:
  editörde `AnimationMode` ile poz örneklemek kemik objeleri olmadan çalışmıyor; karşılaştırma render'larında ve
  pişirme aracında kopyanın kemikleri `AnimatorUtility.DeoptimizeTransformHierarchy` ile geri açılıyor.
- **Cull Completely.** Ekran dışındaki düşmanların animasyonu hiç hesaplanmaz. Benchmark'ta düşmanların çoğu ekranda
  olduğu için kazancı küçük, normal oyunda spawn halkasından yürüyerek gelenler için geçerli.

Telefondaki sonuç (`PERFORMANCE.md` > Adım 6): CPU ana thread 9,9 ms -> 7,8-8,4 ms, bellek 4-5 MB daha az.

### Düşman materyali

Düşmanlar `M_Enemy_SimpleLit_NoNormal` kullanır: URP **Simple Lit** (Blinn-Phong), specular vurgu yok, normal map yok,
sadece diffuse atlas. Lit (fiziksel tabanlı), normal map'li Simple Lit ve normal map'siz Simple Lit, MCP ile Mobile kalite
seviyesinde aynı 40 düşmanlık kalabalıkta, oyun kamerasından daha yakın bir açıdan yan yana render edildi; üçü
ayırt edilemedi. Bir düşman ekranda yaklaşık 100 x 150 piksel kapladığı için normal map'in ve PBR ışığın katkısı bu
boyutta görünmüyor. En ucuz seçenek seçildi; piksel başına bir doku okuması ve tangent-space normal hesabı da gitti.
Karşılaştırma için `M_Enemy` (Lit) ve `M_Enemy_SimpleLit` (normal map'li) `Assets/Optimized/Enemy` altında duruyor.
Telefondaki sonuç: GPU süresi 13,2 -> 11,7 ms (`PERFORMANCE.md` > Adım 7).

## Vuruş geri bildirimi (`Unity/Views`, `Unity/Effects`)

Case "oyuncunun hareketi, saldırılar ve hasar geri bildirimi anlaşılır olmalıdır" diyor. Oyuncunun hasar alması zaten
kırmızı ekran flaşıyla görünüyordu; bu bölüm saldırı tarafını anlaşılır yapar.

| Geri bildirim | Nasıl | Tetikleyen |
|------|------|------|
| **Vurulan düşman parlar** (0,1 sn) | `EnemyView` her frame düşmanın canını okur; önceki frame'den düşükse `MaterialPropertyBlock` ile `_BaseColor`'ı parlak bir renkle çarpar, süre bitince bloğu temizler | Can azalması; ölümcül vuruşta `PlayDeath` |
| **Namlu ışığı** (0,05 sn) | Rifle'ın ucunda, her atışta açılan tek parçacıklık bir partikül (kameraya dönük) | `Weapon.Fired` -> `PlayerView.OnFired` |
| **İsabet kıvılcımı** (~0,3 sn) | 16 parçacıklık turuncu patlama, `HitSpark` prefab'ı | `ProjectileSystem.Hit` -> `ImpactEffects.Play` |

Ayrıntılar ve nedenleri:
- **Event yerine can okuma.** Düşman parlaması `Health.Damaged`'a abone olmak yerine canın düşüp düşmediğine bakar.
  Pool'lanan view'lar farklı düşmanlara tekrar bağlandığı için abonelikleri her seferinde doğru bırakmak gerekirdi;
  okuma yöntemi bu hata riskini tamamen ortadan kaldırıyor.
- **Ölümcül vuruş.** Düşman aynı frame'de simülasyondan çıktığı için o vuruş `Sync`'e hiç ulaşmaz; parlama
  `PlayDeath`'te başlatılır. Cesetler artık sync edilmediği için parlamayı `EnemyDeathViews.Tick` ilerletir.
- **SRP Batcher.** `MaterialPropertyBlock` olan bir renderer SRP Batcher'ın toplu çiziminden çıkar. Blok sadece
  parlama süresince (0,1 sn) duruyor ve sonra `SetPropertyBlock(null)` ile temizleniyor.
- **Pool'lu partiküller.** `ImpactEffects` (saf C#) kıvılcımları `ObjectPool<ParticleSystem>` ile tutar, 16 tanesini
  açılışta oluşturur ve süresi dolanları geri koyar; oyun sırasında `Instantiate`/`Destroy` yok. Yeni tur başlarken
  hepsi temizlenir.
- **Materyal.** Kıvılcım ve namlu ışığı `M_AdditiveParticle` kullanır: URP Particles/Unlit, additive blend (ışık gibi
  toplanır), 64 x 64 yumuşak nokta dokusu. Additive'i doğru kurmak için yine URP'nin `BaseShaderGUI.SetupMaterialBlendMode`
  fonksiyonu kullanıldı (bkz. Blob shadow notu).

**MCP ile Play modunda doğrulandı:** Normal zorlukta 900 frame'lik turda 32 atış ve 31 isabet sayıldı; her atışta namlu
ışığı, isabetlerde kıvılcım ve parlayan düşman görüldü. İsabet ve atış anları yakın plan render ile yakalandı. İlk render'da
kıvılcımlar oyun mesafesinden neredeyse görünmüyordu; boyutları (0,2-0,4 m) ve sayıları (16) artırıldı.
